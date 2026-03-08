using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// StageFlow/UI(GameObject) <-> Stage topology(ECS) 브리지.
    /// - GO -> ECS: Stage topology apply 요청
    /// - GO -> ECS: StageCatalog runtime singleton publish
    /// </summary>
    public sealed class StageTopologyBridge : MonoBehaviour
    {
        private static readonly Dictionary<int, StageTopologyBridge> SceneOwnerByHandle = new();

        [Header("Bridge")]
        public bool LogBindWarnings = true;

        [Header("Stage Catalog")]
        public StageCatalogSO StageCatalog;
        public StageTopologyPrefabCatalogSO TopologyPrefabCatalog;

        private EntityManager _em;
        private Entity _topologyRequestEntity;
        private Entity _topologyStateEntity;
        private bool _isBound;
        private bool _warnedBindFailure;
        private bool _warnedCatalogBindingFailure;
        private bool _isSceneOwner;

        private void OnEnable()
        {
            TryAcquireSceneOwnership();
            TryBind();
        }

        private void OnDisable()
        {
            ReleaseSceneOwnership();
        }

        public bool RequestTopologyApply(int stageId)
        {
            if (stageId <= 0)
                return false;
            if (!TryBind())
                return false;
            if (!EnsureRuntimeSingletons())
                return false;

            var request = _em.GetComponentData<StageTopologyRequestComponent>(_topologyRequestEntity);
            request.RequestedStageId = stageId;
            request.ApplyRequested = 1;
            _em.SetComponentData(_topologyRequestEntity, request);
            return true;
        }

        public bool TryGetTopologyState(out StageTopologyStateComponent topologyState)
        {
            topologyState = default;
            if (!TryBind())
                return false;

            if (_topologyStateEntity == Entity.Null || !_em.Exists(_topologyStateEntity))
                return false;

            topologyState = _em.GetComponentData<StageTopologyStateComponent>(_topologyStateEntity);
            return true;
        }

        private bool TryBind()
        {
            if (!EnsureSceneOwnership())
                return false;

            if (_isBound
                && _em.World.IsCreated
                && _em.Exists(_topologyRequestEntity)
                && _em.Exists(_topologyStateEntity))
            {
                return true;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                WarnBindFailureOnce("DefaultGameObjectInjectionWorld is not ready.");
                _isBound = false;
                return false;
            }

            _em = world.EntityManager;
            using var requestQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>());
            using var stateQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyStateComponent>());
            if (requestQuery.IsEmptyIgnoreFilter || stateQuery.IsEmptyIgnoreFilter)
            {
                WarnBindFailureOnce("StageTopology singleton(s) were not found.");
                _isBound = false;
                return false;
            }

            _topologyRequestEntity = ResolveFirstEntity(requestQuery);
            _topologyStateEntity = ResolveFirstEntity(stateQuery);
            _isBound = _topologyRequestEntity != Entity.Null && _topologyStateEntity != Entity.Null;
            if (_isBound)
                _warnedBindFailure = false;

            return _isBound;
        }

        private bool EnsureRuntimeSingletons()
        {
            if (StageCatalog == null)
            {
                WarnCatalogFailureOnce("StageCatalog is not assigned.");
                return false;
            }

            using var stageCatalogRuntimeQuery = _em.CreateEntityQuery(ComponentType.ReadWrite<StageCatalogRuntimeComponent>());
            Entity catalogEntity;
            if (stageCatalogRuntimeQuery.IsEmptyIgnoreFilter)
            {
                catalogEntity = _em.CreateEntity();
                _em.AddComponentObject(catalogEntity, new StageCatalogRuntimeComponent
                {
                    Catalog = StageCatalog,
                });
            }
            else
            {
                catalogEntity = ResolveFirstEntity(stageCatalogRuntimeQuery);
                if (catalogEntity == Entity.Null || !_em.Exists(catalogEntity))
                {
                    WarnCatalogFailureOnce("StageCatalogRuntimeComponent singleton resolve failed.");
                    return false;
                }

                var runtime = _em.GetComponentObject<StageCatalogRuntimeComponent>(catalogEntity);
                runtime.Catalog = StageCatalog;
            }

            using var topologyCatalogQuery = _em.CreateEntityQuery(ComponentType.ReadWrite<StageTopologyPrefabCatalogComponent>());
            var topologyCatalogEntity = EnsureTopologyPrefabCatalogEntity(topologyCatalogQuery);
            if (topologyCatalogEntity == Entity.Null || !_em.Exists(topologyCatalogEntity))
            {
                WarnCatalogFailureOnce("StageTopologyPrefabCatalogComponent singleton resolve failed.");
                return false;
            }

            var topologyPrefabs = _em.GetComponentData<StageTopologyPrefabCatalogComponent>(topologyCatalogEntity);
            if (topologyPrefabs.SourceTemplate == Entity.Null || !_em.Exists(topologyPrefabs.SourceTemplate))
                topologyPrefabs.SourceTemplate = StageTopologyTemplateFactory.CreateSourceTemplate(_em);
            if (topologyPrefabs.DepositTemplate == Entity.Null || !_em.Exists(topologyPrefabs.DepositTemplate))
                topologyPrefabs.DepositTemplate = StageTopologyTemplateFactory.CreateDepositTemplate(_em);
            _em.SetComponentData(topologyCatalogEntity, topologyPrefabs);

            _warnedCatalogBindingFailure = false;
            return true;
        }

        private Entity EnsureTopologyPrefabCatalogEntity(EntityQuery topologyCatalogQuery)
        {
            if (topologyCatalogQuery.IsEmptyIgnoreFilter)
                return _em.CreateEntity(typeof(StageTopologyPrefabCatalogComponent));

            using var entities = topologyCatalogQuery.ToEntityArray(Allocator.Temp);
            Entity keeper = Entity.Null;
            StageTopologyPrefabCatalogComponent keeperValue = default;

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!_em.Exists(entity))
                    continue;

                var candidate = _em.GetComponentData<StageTopologyPrefabCatalogComponent>(entity);
                bool hasAnyPrefab = candidate.SourceTemplate != Entity.Null || candidate.DepositTemplate != Entity.Null;
                if (keeper == Entity.Null || hasAnyPrefab)
                {
                    keeper = entity;
                    keeperValue = candidate;
                    if (hasAnyPrefab)
                        break;
                }
            }

            if (keeper == Entity.Null)
                keeper = entities[0];

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == keeper || !_em.Exists(entity))
                    continue;

                var extra = _em.GetComponentData<StageTopologyPrefabCatalogComponent>(entity);
                if (keeperValue.SourceTemplate == Entity.Null && extra.SourceTemplate != Entity.Null)
                    keeperValue.SourceTemplate = extra.SourceTemplate;
                if (keeperValue.DepositTemplate == Entity.Null && extra.DepositTemplate != Entity.Null)
                    keeperValue.DepositTemplate = extra.DepositTemplate;

                _em.RemoveComponent<StageTopologyPrefabCatalogComponent>(entity);
            }

            _em.SetComponentData(keeper, keeperValue);
            return keeper;
        }

        private void WarnBindFailureOnce(string message)
        {
            if (!LogBindWarnings || _warnedBindFailure)
                return;

            _warnedBindFailure = true;
            Debug.LogWarning($"[StageTopologyBridge] {message}");
        }

        private void WarnCatalogFailureOnce(string message)
        {
            if (!LogBindWarnings || _warnedCatalogBindingFailure)
                return;

            _warnedCatalogBindingFailure = true;
            Debug.LogWarning($"[StageTopologyBridge] {message}");
        }

        private static Entity ResolveFirstEntity(EntityQuery query)
        {
            int count = query.CalculateEntityCount();
            if (count <= 0)
                return Entity.Null;
            if (count == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        private bool TryAcquireSceneOwnership()
        {
            var scene = gameObject.scene;
            int sceneHandle = scene.IsValid() ? scene.handle : int.MinValue;
            if (!SceneOwnerByHandle.TryGetValue(sceneHandle, out var owner) || owner == null)
            {
                SceneOwnerByHandle[sceneHandle] = this;
                _isSceneOwner = true;
                return true;
            }

            if (owner == this)
            {
                _isSceneOwner = true;
                return true;
            }

            _isSceneOwner = false;
            string sceneName = scene.IsValid() ? scene.name : "(invalid-scene)";
            WarnBindFailureOnce($"Duplicate bridge in scene '{sceneName}'. Only one StageTopologyBridge is allowed per scene.");
            return false;
        }

        private bool EnsureSceneOwnership()
        {
            if (_isSceneOwner)
                return true;

            return TryAcquireSceneOwnership();
        }

        private void ReleaseSceneOwnership()
        {
            if (!_isSceneOwner)
                return;

            var scene = gameObject.scene;
            int sceneHandle = scene.IsValid() ? scene.handle : int.MinValue;
            if (SceneOwnerByHandle.TryGetValue(sceneHandle, out var owner) && owner == this)
                SceneOwnerByHandle.Remove(sceneHandle);

            _isSceneOwner = false;
        }
    }
}
