using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// GO-only owner for the applied stage visual and procedural runtime Cell overlays.
    /// ECS topology and Source pollution state are consumed read-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageGridVisualController : MonoBehaviour
    {
        public const float SourceActiveAlpha = 0.33f;
        public const float SourceInactiveAlpha = 0.05f;
        public const float SourceDepletedAlpha = 0.04f;

        private const float SourceFillLayerY = 0.022f;
        private static readonly Color32 SourceColor = new(26, 217, 255, 255);

        [Header("References")]
        public StageCatalogSO StageCatalog;
        public StageTopologyBridge TopologyBridge;
        public Material CellOverlayMaterial;

        [Header("Cell Overlay")]
        [Min(0.01f)] public float PollutionPollIntervalSec = 0.1f;
        [Min(0f)] public float SourceFadeOutSec = 0.2f;
        [Min(0f)] public float SourceFadeInSec = 0.35f;

        private readonly List<SourceOverlayState> _sourceOverlays = new();
        private readonly Dictionary<uint, SourceOverlayState> _sourceByStableId = new();

        private int _lastAppliedStageId;
        private bool _lastReady;
        private bool _warnedMissingMaterial;
        private GameObject _currentInstance;
        private GameObject _cellOverlayRoot;
        private Mesh _staticOverlayMesh;
        private StageLayoutSO _currentLayout;

        private World _sourceWorld;
        private EntityManager _sourceEntityManager;
        private EntityQuery _sourceQuery;
        private bool _sourceQueryCreated;
        private bool _sourceBindingsReady;
        private float _pollCountdown;

        public int LastAppliedStageId => _lastAppliedStageId;
        public bool LastReady => _lastReady;
        public GameObject CurrentInstance => _currentInstance;
        public GameObject CellOverlayRoot => _cellOverlayRoot;
        public int SourceOverlayCount => _sourceOverlays.Count;

        private void Reset()
        {
            if (TopologyBridge == null)
                TopologyBridge = GetComponent<StageTopologyBridge>();
        }

        private void OnEnable()
        {
            EnsureReferences();
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            ClearVisual();
        }

        public void Tick()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Deterministic tick entry used by Update and behavior tests.
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            EnsureReferences();

            if (TopologyBridge == null || !TopologyBridge.TryGetTopologyState(out var topologyState))
            {
                if (_lastReady)
                    ClearVisual();
                return;
            }

            if (topologyState.Ready == 0 || topologyState.AppliedStageId <= 0)
            {
                if (_lastReady)
                    ClearVisual();
                return;
            }

            if (!_lastReady || _lastAppliedStageId != topologyState.AppliedStageId)
                ApplyVisual(topologyState.AppliedStageId);

            TickSourceOverlays(Mathf.Max(0f, unscaledDeltaTime));
        }

        public bool TryGetSourceCellAlpha(uint stableId, int cellIndex, out float alpha)
        {
            alpha = 0f;
            if (!_sourceByStableId.TryGetValue(stableId, out var state)
                || (uint)cellIndex >= (uint)state.CurrentAlpha.Length
                || state.CellToVertex[cellIndex] < 0)
            {
                return false;
            }

            alpha = state.CurrentAlpha[cellIndex];
            return true;
        }

        public void ClearVisual()
        {
            ResetSourceBinding();

            if (_staticOverlayMesh != null)
                DestroyOwnedObject(_staticOverlayMesh);
            _staticOverlayMesh = null;

            if (_cellOverlayRoot != null)
                DestroyOwnedObject(_cellOverlayRoot);
            _cellOverlayRoot = null;

            if (_currentInstance != null)
                DestroyOwnedObject(_currentInstance);
            _currentInstance = null;

            _currentLayout = null;
            _lastReady = false;
            _lastAppliedStageId = 0;
        }

        private void ApplyVisual(int stageId)
        {
            ClearVisual();

            if (!TryResolveLayout(stageId, out var layout))
            {
                _lastAppliedStageId = stageId;
                _lastReady = true;
                return;
            }

            _currentLayout = layout;
            if (layout.GridVisualPrefab != null)
            {
                _currentInstance = Instantiate(
                    layout.GridVisualPrefab,
                    layout.Grid.Origin,
                    layout.GridVisualPrefab.transform.rotation,
                    transform);
                _currentInstance.name = layout.GridVisualPrefab.name;
            }

            BuildStaticOverlay(layout);
            _lastAppliedStageId = stageId;
            _lastReady = true;
            _pollCountdown = 0f;
        }

        private void BuildStaticOverlay(StageLayoutSO layout)
        {
            _cellOverlayRoot = new GameObject("StageCellOverlay");
            _cellOverlayRoot.layer = gameObject.layer;
            _cellOverlayRoot.transform.SetParent(transform, false);
            _cellOverlayRoot.transform.SetPositionAndRotation(layout.Grid.Origin, Quaternion.identity);

            var staticObject = CreateMeshObject("Static", _cellOverlayRoot.transform, sortingOrder: 0, out var filter, out _);
            _staticOverlayMesh = new Mesh
            {
                name = "StageCellOverlay_Static",
                hideFlags = HideFlags.DontSave,
            };
            StageCellOverlayGeometryBuilder.BuildStaticMesh(in layout.Grid, layout.Cells, _staticOverlayMesh);
            filter.sharedMesh = _staticOverlayMesh;
            staticObject.SetActive(_staticOverlayMesh.vertexCount > 0);
        }

        private void TickSourceOverlays(float deltaTime)
        {
            if (_currentLayout == null || _cellOverlayRoot == null)
                return;

            _pollCountdown -= deltaTime;
            if (!_sourceBindingsReady || _pollCountdown <= 0f)
            {
                _pollCountdown = Mathf.Max(0.01f, PollutionPollIntervalSec);
                if (!_sourceBindingsReady)
                    TryBuildSourceBindings();
                else
                    PollSourceTargets();
            }

            UpdateSourceFades(deltaTime);
        }

        private bool TryBuildSourceBindings()
        {
            if (!EnsureSourceQuery())
                return false;

            _sourceQuery.CompleteDependency();
            using var entities = _sourceQuery.ToEntityArray(Allocator.Temp);
            ClearSourceOverlays();

            // Topology Ready and Source entity materialization can straddle adjacent frames
            // when a stage is reloaded in the same World. Keep retrying instead of caching an
            // empty binding set as final state.
            if (entities.Length == 0)
                return false;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!_sourceEntityManager.Exists(entity))
                    continue;

                uint stableId = _sourceEntityManager.GetComponentData<SourceStableIdComponent>(entity).Value;
                if (stableId == 0u || _sourceByStableId.ContainsKey(stableId))
                    continue;

                var source = _sourceEntityManager.GetComponentData<SourceSpawnComponent>(entity);
                var grid = _sourceEntityManager.GetComponentData<SourcePollutionGridComponent>(entity);
                var cells = _sourceEntityManager.GetBuffer<SourcePollutionCellBuffer>(entity, isReadOnly: true);
                var state = CreateSourceOverlay(entity, stableId, in source, in grid, cells);
                if (state == null)
                    continue;

                _sourceOverlays.Add(state);
                _sourceByStableId.Add(stableId, state);
            }

            _sourceBindingsReady = true;
            return true;
        }

        private void PollSourceTargets()
        {
            if (!EnsureSourceQuery())
            {
                InvalidateSourceBindings();
                return;
            }

            _sourceQuery.CompleteDependency();
            for (int i = 0; i < _sourceOverlays.Count; i++)
            {
                var state = _sourceOverlays[i];
                Entity entity = state.Entity;
                if (!_sourceEntityManager.Exists(entity)
                    || !_sourceEntityManager.HasComponent<SourceSpawnComponent>(entity)
                    || !_sourceEntityManager.HasComponent<SourcePollutionGridComponent>(entity)
                    || !_sourceEntityManager.HasBuffer<SourcePollutionCellBuffer>(entity))
                {
                    InvalidateSourceBindings();
                    return;
                }

                var source = _sourceEntityManager.GetComponentData<SourceSpawnComponent>(entity);
                var grid = _sourceEntityManager.GetComponentData<SourcePollutionGridComponent>(entity);
                var cells = _sourceEntityManager.GetBuffer<SourcePollutionCellBuffer>(entity, isReadOnly: true);
                if (!GridMatches(state, in grid, cells.Length))
                {
                    InvalidateSourceBindings();
                    return;
                }

                bool depleted = source.State == SourceStateId.Depleted;
                for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    if (state.CellToVertex[cellIndex] < 0)
                        continue;

                    state.TargetAlpha[cellIndex] = ResolveTargetAlpha(cells[cellIndex], depleted);
                }
            }
        }

        private void UpdateSourceFades(float deltaTime)
        {
            float fadeOutSpeed = SourceFadeOutSec <= 0f
                ? float.PositiveInfinity
                : (SourceActiveAlpha - SourceDepletedAlpha) / SourceFadeOutSec;
            float fadeInSpeed = SourceFadeInSec <= 0f
                ? float.PositiveInfinity
                : (SourceActiveAlpha - SourceDepletedAlpha) / SourceFadeInSec;

            for (int sourceIndex = 0; sourceIndex < _sourceOverlays.Count; sourceIndex++)
            {
                var state = _sourceOverlays[sourceIndex];
                bool colorsChanged = false;

                for (int cellIndex = 0; cellIndex < state.CurrentAlpha.Length; cellIndex++)
                {
                    int vertexStart = state.CellToVertex[cellIndex];
                    if (vertexStart < 0)
                        continue;

                    float current = state.CurrentAlpha[cellIndex];
                    float target = state.TargetAlpha[cellIndex];
                    if (Mathf.Approximately(current, target))
                        continue;

                    float speed = target < current ? fadeOutSpeed : fadeInSpeed;
                    float next = float.IsPositiveInfinity(speed)
                        ? target
                        : Mathf.MoveTowards(current, target, speed * deltaTime);
                    state.CurrentAlpha[cellIndex] = next;
                    SetQuadAlpha(state.Colors, vertexStart, next);
                    colorsChanged = true;
                }

                if (colorsChanged)
                {
                    state.Mesh.SetColors(
                        state.Colors,
                        0,
                        state.Colors.Length,
                        MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                }
            }
        }

        private SourceOverlayState CreateSourceOverlay(
            Entity entity,
            uint stableId,
            in SourceSpawnComponent source,
            in SourcePollutionGridComponent grid,
            DynamicBuffer<SourcePollutionCellBuffer> cells)
        {
            int cols = Mathf.Max(0, grid.Cols);
            int rows = Mathf.Max(0, grid.Rows);
            int expectedCellCount = cols * rows;
            if (cols <= 0 || rows <= 0 || grid.CellSize <= 0f || cells.Length != expectedCellCount)
                return null;

            int validCount = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].IsValid != 0)
                    validCount++;
            }

            var state = new SourceOverlayState
            {
                Entity = entity,
                StableId = stableId,
                Grid = grid,
                CellToVertex = new int[cells.Length],
                CurrentAlpha = new float[cells.Length],
                TargetAlpha = new float[cells.Length],
                Colors = new Color32[validCount * 4],
                Mesh = new Mesh
                {
                    name = $"StageCellOverlay_Source_{stableId}",
                    hideFlags = HideFlags.DontSave,
                },
            };
            Array.Fill(state.CellToVertex, -1);
            state.Mesh.MarkDynamic();

            var vertices = new Vector3[validCount * 4];
            var indices = new int[validCount * 6];
            float localOriginX = grid.OriginX - _currentLayout.Grid.Origin.x;
            float localOriginZ = grid.OriginZ - _currentLayout.Grid.Origin.z;
            float inset = grid.CellSize * 0.055f;
            bool depleted = source.State == SourceStateId.Depleted;
            int quadIndex = 0;

            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var cell = cells[cellIndex];
                if (cell.IsValid == 0)
                    continue;

                int x = cellIndex % cols;
                int y = cellIndex / cols;
                float x0 = localOriginX + x * grid.CellSize + inset;
                float z0 = localOriginZ + y * grid.CellSize + inset;
                float x1 = localOriginX + (x + 1) * grid.CellSize - inset;
                float z1 = localOriginZ + (y + 1) * grid.CellSize - inset;
                int vertexStart = quadIndex * 4;
                int indexStart = quadIndex * 6;

                vertices[vertexStart] = new Vector3(x0, SourceFillLayerY, z0);
                vertices[vertexStart + 1] = new Vector3(x1, SourceFillLayerY, z0);
                vertices[vertexStart + 2] = new Vector3(x1, SourceFillLayerY, z1);
                vertices[vertexStart + 3] = new Vector3(x0, SourceFillLayerY, z1);
                indices[indexStart] = vertexStart;
                indices[indexStart + 1] = vertexStart + 1;
                indices[indexStart + 2] = vertexStart + 2;
                indices[indexStart + 3] = vertexStart;
                indices[indexStart + 4] = vertexStart + 2;
                indices[indexStart + 5] = vertexStart + 3;

                float alpha = ResolveTargetAlpha(cell, depleted);
                state.CellToVertex[cellIndex] = vertexStart;
                state.CurrentAlpha[cellIndex] = alpha;
                state.TargetAlpha[cellIndex] = alpha;
                SetQuadAlpha(state.Colors, vertexStart, alpha);
                quadIndex++;
            }

            state.Mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            state.Mesh.SetVertices(vertices);
            state.Mesh.SetColors(state.Colors);
            state.Mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            state.Mesh.RecalculateBounds();

            state.GameObject = CreateMeshObject(
                $"Source_{stableId}",
                _cellOverlayRoot.transform,
                sortingOrder: 1,
                out var filter,
                out _);
            filter.sharedMesh = state.Mesh;
            state.GameObject.SetActive(validCount > 0);
            return state;
        }

        private bool EnsureSourceQuery()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (_sourceQueryCreated && ReferenceEquals(_sourceWorld, world) && world != null && world.IsCreated)
                return true;

            DisposeSourceQuery();
            if (world == null || !world.IsCreated)
                return false;

            _sourceWorld = world;
            _sourceEntityManager = world.EntityManager;
            _sourceQuery = _sourceEntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologySourceTag>(),
                    ComponentType.ReadOnly<SourceStableIdComponent>(),
                    ComponentType.ReadOnly<SourceSpawnComponent>(),
                    ComponentType.ReadOnly<SourcePollutionGridComponent>(),
                    ComponentType.ReadOnly<SourcePollutionCellBuffer>(),
                },
            });
            _sourceQueryCreated = true;
            return true;
        }

        private bool TryResolveLayout(int stageId, out StageLayoutSO layout)
        {
            layout = null;
            if (StageCatalog == null || StageCatalog.Entries == null)
                return false;

            for (int i = 0; i < StageCatalog.Entries.Length; i++)
            {
                var entry = StageCatalog.Entries[i];
                if (!entry.Enabled || entry.Layout == null || entry.Layout.StageId != stageId)
                    continue;

                layout = entry.Layout;
                return true;
            }

            return false;
        }

        private void EnsureReferences()
        {
            if (TopologyBridge == null)
                TopologyBridge = GetComponent<StageTopologyBridge>();
            if (StageCatalog == null && TopologyBridge != null)
                StageCatalog = TopologyBridge.StageCatalog;
        }

        private GameObject CreateMeshObject(
            string objectName,
            Transform parent,
            int sortingOrder,
            out MeshFilter filter,
            out MeshRenderer renderer)
        {
            var meshObject = new GameObject(objectName);
            meshObject.layer = gameObject.layer;
            meshObject.transform.SetParent(parent, false);
            filter = meshObject.AddComponent<MeshFilter>();
            renderer = meshObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, sortingOrder);
            return meshObject;
        }

        private void ConfigureRenderer(MeshRenderer renderer, int sortingOrder)
        {
            renderer.sharedMaterial = CellOverlayMaterial;
            renderer.enabled = CellOverlayMaterial != null;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;

            if (CellOverlayMaterial == null && !_warnedMissingMaterial)
            {
                _warnedMissingMaterial = true;
                Debug.LogWarning("[StageGridVisualController] CellOverlayMaterial is not assigned. Runtime Cell overlay renderers are disabled.", this);
            }
        }

        private void InvalidateSourceBindings()
        {
            ClearSourceOverlays();
            _sourceBindingsReady = false;
            _pollCountdown = 0f;
        }

        private void ResetSourceBinding()
        {
            ClearSourceOverlays();
            DisposeSourceQuery();
            _sourceBindingsReady = false;
            _pollCountdown = 0f;
        }

        private void ClearSourceOverlays()
        {
            for (int i = 0; i < _sourceOverlays.Count; i++)
            {
                var state = _sourceOverlays[i];
                if (state.Mesh != null)
                    DestroyOwnedObject(state.Mesh);
                if (state.GameObject != null)
                    DestroyOwnedObject(state.GameObject);
            }

            _sourceOverlays.Clear();
            _sourceByStableId.Clear();
        }

        private void DisposeSourceQuery()
        {
            if (_sourceQueryCreated && _sourceWorld != null && _sourceWorld.IsCreated)
                _sourceQuery.Dispose();

            _sourceQueryCreated = false;
            _sourceWorld = null;
            _sourceEntityManager = default;
            _sourceQuery = default;
        }

        private static bool GridMatches(SourceOverlayState state, in SourcePollutionGridComponent grid, int cellCount)
        {
            return state.CellToVertex.Length == cellCount
                && state.Grid.Cols == grid.Cols
                && state.Grid.Rows == grid.Rows
                && Mathf.Approximately(state.Grid.CellSize, grid.CellSize)
                && Mathf.Approximately(state.Grid.OriginX, grid.OriginX)
                && Mathf.Approximately(state.Grid.OriginZ, grid.OriginZ);
        }

        private static float ResolveTargetAlpha(in SourcePollutionCellBuffer cell, bool depleted)
        {
            if (depleted)
                return SourceDepletedAlpha;
            return cell.IsActive != 0 ? SourceActiveAlpha : SourceInactiveAlpha;
        }

        private static void SetQuadAlpha(Color32[] colors, int vertexStart, float alpha)
        {
            byte alphaByte = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
            var color = SourceColor;
            color.a = alphaByte;
            colors[vertexStart] = color;
            colors[vertexStart + 1] = color;
            colors[vertexStart + 2] = color;
            colors[vertexStart + 3] = color;
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private sealed class SourceOverlayState
        {
            public Entity Entity;
            public uint StableId;
            public SourcePollutionGridComponent Grid;
            public GameObject GameObject;
            public Mesh Mesh;
            public int[] CellToVertex;
            public float[] CurrentAlpha;
            public float[] TargetAlpha;
            public Color32[] Colors;
        }
    }
}
