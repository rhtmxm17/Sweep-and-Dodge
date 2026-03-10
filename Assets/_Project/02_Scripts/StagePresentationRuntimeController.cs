using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// GO-only stage presentation owner.
    /// - Applied stage의 StageLayoutSO.Presentations를 읽어 prefab을 생성한다.
    /// - runtime topology를 read-only로 resolve하며 ECS write는 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StagePresentationRuntimeController : MonoBehaviour
    {
        [Header("References")]
        public StageCatalogSO StageCatalog;
        public StagePresentationCatalogSO PresentationCatalog;
        public StageTopologyBridge TopologyBridge;

        [Header("Policy")]
        public bool LogWarnings = true;
        public bool RebuildOnEnable = true;
        public bool DestroyOnDisable = true;

        private EntityManager _em;
        private EntityQuery _sourceQuery;
        private EntityQuery _depositQuery;
        private EntityQuery _obstacleQuery;
        private bool _isBound;

        private int _lastAppliedStageId;
        private bool _lastReady;
        private bool _hasBuiltOnce;
        private readonly List<GameObject> _spawnedRoots = new List<GameObject>(32);
        private readonly Dictionary<uint, GameObject> _spawnedByStableId = new Dictionary<uint, GameObject>();
        private readonly HashSet<string> _warningKeys = new HashSet<string>();

        public int LastAppliedStageId => _lastAppliedStageId;
        public bool LastReady => _lastReady;
        public int SpawnedRootCount => _spawnedRoots.Count;

        private void Reset()
        {
            if (TopologyBridge == null)
                TopologyBridge = GetComponent<StageTopologyBridge>();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (RebuildOnEnable)
                _hasBuiltOnce = false;
        }

        private void Update()
        {
            Tick();
        }

        private void OnDisable()
        {
            _lastReady = false;
            _lastAppliedStageId = 0;
            _hasBuiltOnce = false;
            if (DestroyOnDisable)
                ClearPresentations();
        }

        public void Tick()
        {
            EnsureReferences();

            if (TopologyBridge == null || !TopologyBridge.TryGetTopologyState(out var topologyState))
            {
                if (_lastReady)
                {
                    ClearPresentations();
                    _lastReady = false;
                    _lastAppliedStageId = 0;
                }

                return;
            }

            if (topologyState.Ready == 0 || topologyState.AppliedStageId <= 0)
            {
                if (_lastReady)
                {
                    ClearPresentations();
                    _lastReady = false;
                    _lastAppliedStageId = 0;
                }

                return;
            }

            if (!NeedsRebuild(topologyState))
                return;

            RebuildForAppliedStage(topologyState.AppliedStageId);
        }

        public void ForceRebuild()
        {
            EnsureReferences();

            if (TopologyBridge == null || !TopologyBridge.TryGetTopologyState(out var topologyState))
                return;
            if (topologyState.Ready == 0 || topologyState.AppliedStageId <= 0)
                return;

            RebuildForAppliedStage(topologyState.AppliedStageId);
        }

        public void ClearPresentations()
        {
            for (int i = _spawnedRoots.Count - 1; i >= 0; i--)
            {
                var root = _spawnedRoots[i];
                if (root == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(root);
                else
                    DestroyImmediate(root);
            }

            _spawnedRoots.Clear();
            _spawnedByStableId.Clear();
            _warningKeys.Clear();
        }

        private bool NeedsRebuild(StageTopologyStateComponent topologyState)
        {
            if (!_hasBuiltOnce && RebuildOnEnable && topologyState.Ready != 0)
                return true;
            if (!_lastReady && topologyState.Ready != 0)
                return true;
            if (_lastAppliedStageId != topologyState.AppliedStageId && topologyState.Ready != 0)
                return true;

            return false;
        }

        private void RebuildForAppliedStage(int stageId)
        {
            ClearPresentations();

            if (!TryResolveStageLayout(stageId, out var layout))
            {
                WarnOnce($"stage-layout-missing:{stageId}", $"[StagePresentation] StageLayout could not be resolved. stageId={stageId}");
                _lastAppliedStageId = stageId;
                _lastReady = true;
                _hasBuiltOnce = true;
                return;
            }

            if (layout.Presentations != null)
            {
                for (int i = 0; i < layout.Presentations.Length; i++)
                {
                    var presentation = layout.Presentations[i];
                    if (!presentation.Active)
                        continue;

                    if (!TryResolvePresentationPrefab(presentation.PresentationKey, out var prefab, out var entry))
                    {
                        WarnOnce(
                            $"prefab-missing:{stageId}:{presentation.PresentationKey}",
                            $"[StagePresentation] PresentationKey could not be resolved. stageId={stageId}, key={presentation.PresentationKey}");
                        continue;
                    }

                    if (!IsUsageAllowed(entry.Usage, presentation))
                    {
                        WarnOnce(
                            $"usage-mismatch:{stageId}:{presentation.StableId}",
                            $"[StagePresentation] UsageFlags do not allow this placement. stageId={stageId}, stableId={presentation.StableId}, key={presentation.PresentationKey}, usage={entry.Usage}, placement={presentation.PlacementMode}, linkKind={presentation.LinkKind}");
                        continue;
                    }

                    if (!TryResolvePresentationTransform(in presentation, out var worldPosition, out var worldRotation))
                    {
                        WarnOnce(
                            $"target-missing:{stageId}:{presentation.StableId}",
                            $"[StagePresentation] Linked target could not be resolved. stageId={stageId}, stableId={presentation.StableId}, linkKind={presentation.LinkKind}, linkedStableId={presentation.LinkedStableId}");
                        continue;
                    }

                    var instance = Instantiate(prefab, transform);
                    instance.name = $"Presentation_{presentation.StableId}_{presentation.PresentationKey}";
                    instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
                    instance.transform.localScale = presentation.Scale;
                    _spawnedRoots.Add(instance);
                    _spawnedByStableId[presentation.StableId] = instance;
                }
            }

            _lastAppliedStageId = stageId;
            _lastReady = true;
            _hasBuiltOnce = true;
        }

        private bool TryResolveStageLayout(int stageId, out StageLayoutSO layout)
        {
            layout = null;
            if (StageCatalog == null || StageCatalog.Entries == null)
                return false;

            for (int i = 0; i < StageCatalog.Entries.Length; i++)
            {
                var entry = StageCatalog.Entries[i];
                if (!entry.Enabled || entry.Layout == null)
                    continue;
                if (entry.Layout.StageId != stageId)
                    continue;

                layout = entry.Layout;
                return true;
            }

            return false;
        }

        private bool TryResolvePresentationPrefab(string presentationKey, out GameObject prefab, out StagePresentationCatalogEntry entry)
        {
            prefab = null;
            entry = default;

            if (PresentationCatalog == null || PresentationCatalog.Entries == null || string.IsNullOrWhiteSpace(presentationKey))
                return false;

            string key = presentationKey.Trim();
            for (int i = 0; i < PresentationCatalog.Entries.Length; i++)
            {
                var candidate = PresentationCatalog.Entries[i];
                if (!string.Equals(candidate.PresentationKey?.Trim(), key, System.StringComparison.Ordinal))
                    continue;

                entry = candidate;
                prefab = candidate.Prefab;
                return prefab != null;
            }

            return false;
        }

        private bool TryResolvePresentationTransform(in StagePresentationLayoutData presentation, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            if (presentation.PlacementMode == StagePresentationPlacementMode.Standalone)
            {
                worldPosition = presentation.Position;
                worldRotation = Quaternion.Euler(presentation.Euler);
                return true;
            }

            if (presentation.LinkKind == StagePresentationLinkKind.None || presentation.LinkedStableId == 0)
            {
                worldPosition = default;
                worldRotation = default;
                return false;
            }

            if (!TryResolveLinkedAnchor(presentation.LinkKind, presentation.LinkedStableId, out var anchorPosition, out var anchorRotation))
            {
                worldPosition = default;
                worldRotation = default;
                return false;
            }

            worldPosition = anchorPosition + anchorRotation * presentation.Position;
            worldRotation = anchorRotation * Quaternion.Euler(presentation.Euler);
            return true;
        }

        private bool TryResolveLinkedAnchor(StagePresentationLinkKind linkKind, uint stableId, out Vector3 anchorPosition, out Quaternion anchorRotation)
        {
            anchorPosition = default;
            anchorRotation = default;

            if (!TryBind())
                return false;

            EntityQuery query = linkKind switch
            {
                StagePresentationLinkKind.Source => _sourceQuery,
                StagePresentationLinkKind.Deposit => _depositQuery,
                StagePresentationLinkKind.Obstacle => _obstacleQuery,
                _ => default,
            };

            if (query == default || query.IsEmptyIgnoreFilter)
                return false;

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                uint candidateStableId = linkKind switch
                {
                    StagePresentationLinkKind.Source => _em.GetComponentData<SourceStableIdComponent>(entity).Value,
                    StagePresentationLinkKind.Deposit => _em.GetComponentData<DepositStableIdComponent>(entity).Value,
                    StagePresentationLinkKind.Obstacle => _em.GetComponentData<ObstacleStableIdComponent>(entity).Value,
                    _ => 0u,
                };
                if (candidateStableId != stableId)
                    continue;

                var tx = _em.GetComponentData<LocalTransform>(entity);
                anchorPosition = tx.Position;
                anchorRotation = tx.Rotation;
                return true;
            }

            return false;
        }

        private bool TryBind()
        {
            if (_isBound && _em.World.IsCreated)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            _em = world.EntityManager;
            _sourceQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceStableIdComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            _depositQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<DepositStableIdComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            _obstacleQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<ObstacleStableIdComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            _isBound = true;
            return true;
        }

        private void EnsureReferences()
        {
            if (TopologyBridge == null)
                TopologyBridge = GetComponent<StageTopologyBridge>();
            if (StageCatalog == null && TopologyBridge != null)
                StageCatalog = TopologyBridge.StageCatalog;
        }

        private bool IsUsageAllowed(StagePresentationUsageFlags usage, in StagePresentationLayoutData presentation)
        {
            if (presentation.PlacementMode == StagePresentationPlacementMode.Standalone)
                return (usage & StagePresentationUsageFlags.Standalone) != 0;

            return presentation.LinkKind switch
            {
                StagePresentationLinkKind.Source => (usage & StagePresentationUsageFlags.SourceLinked) != 0,
                StagePresentationLinkKind.Deposit => (usage & StagePresentationUsageFlags.DepositLinked) != 0,
                StagePresentationLinkKind.Obstacle => (usage & StagePresentationUsageFlags.ObstacleLinked) != 0,
                _ => false,
            };
        }

        private void WarnOnce(string key, string message)
        {
            if (!LogWarnings || string.IsNullOrEmpty(key))
                return;
            if (!_warningKeys.Add(key))
                return;

            Debug.LogWarning(message);
        }
    }
}
