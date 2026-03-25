using Unity.Collections;
using Unity.Entities;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StageRuntimeGridDebugDrawer : MonoBehaviour
    {
        [Header("Visibility")]
        public bool OnlyWhenPlaying = true;
        public bool ShowGrid = true;
        public bool ShowMovement = true;
        public bool ShowSource = true;
        public bool ShowDeposit = true;
        public bool ShowAnchors = true;
        public bool ShowAnchorLabels = true;

        [Header("Placement")]
        public float GridPlaneY = 0f;
        public float OverlayThickness = 0.04f;
        public float GridLineYOffset = 0.01f;
        public float AnchorSphereRadius = 0.16f;

        private EntityManager _em;
        private EntityQuery _gridQuery;
        private EntityQuery _sourceAnchorQuery;
        private World _world;
        private bool _isBound;

        private void Update()
        {
            if (Application.isPlaying)
                TryBind();
        }

        private void OnDrawGizmos()
        {
            if (OnlyWhenPlaying && !Application.isPlaying)
                return;

            try
            {
                if (!TryBind())
                    return;

                if (_gridQuery.IsEmptyIgnoreFilter)
                    return;

                _em.CompleteAllTrackedJobs();

                Entity gridEntity = _gridQuery.GetSingletonEntity();
                if (!_em.Exists(gridEntity))
                    return;

                var grid = _em.GetComponentData<StageRuntimeGridComponent>(gridEntity);
                if (!StageRuntimeGridUtility.IsReady(in grid) || !_em.HasBuffer<StageRuntimeGridCellBufferElement>(gridEntity))
                    return;

                var cells = _em.GetBuffer<StageRuntimeGridCellBufferElement>(gridEntity, isReadOnly: true);
                float planeY = GridPlaneY;

                if (ShowMovement || ShowSource || ShowDeposit)
                    DrawCellOverlays(in grid, cells, planeY);
                if (ShowGrid)
                    DrawGridLines(in grid, planeY);
                if (ShowAnchors)
                    DrawSourceAnchors();
            }
            catch (global::System.Exception)
            {
                ResetBinding();
            }
        }

        private bool TryBind()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (_isBound && ReferenceEquals(_world, world) && world != null && world.IsCreated)
                return true;

            if (world == null || !world.IsCreated)
            {
                ResetBinding();
                return false;
            }

            _em = world.EntityManager;
            _gridQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>(), ComponentType.ReadOnly<StageRuntimeGridCellBufferElement>());
            _sourceAnchorQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<StageTopologySourceTag>(),
                ComponentType.ReadOnly<SourceStableIdComponent>(),
                ComponentType.ReadOnly<SourceAnchorComponent>());
            _world = world;
            _isBound = true;
            return true;
        }

        private void OnDisable()
        {
            ResetBinding();
        }

        private void ResetBinding()
        {
            _world = null;
            _isBound = false;
            _gridQuery = default;
            _sourceAnchorQuery = default;
        }

        private void DrawCellOverlays(in StageRuntimeGridComponent grid, DynamicBuffer<StageRuntimeGridCellBufferElement> cells, float planeY)
        {
            float size = grid.CellSize * 0.92f;
            var cubeSize = new Vector3(size, OverlayThickness, size);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int index = StageRuntimeGridUtility.GetCellIndex(x, y, in grid);
                    if (index < 0 || index >= cells.Length)
                        continue;

                    var cell = cells[index];
                    Vector3 center = GetCellCenter(x, y, in grid, planeY);

                    if (ShowMovement && cell.MovementFlags != StageCellMovementFlags.None)
                    {
                        Gizmos.color = ResolveMovementColor(cell.MovementFlags);
                        Gizmos.DrawCube(center, cubeSize);
                    }

                    if (ShowSource && cell.SourceRegionId != 0u)
                    {
                        Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.2f);
                        Gizmos.DrawCube(center + new Vector3(0f, OverlayThickness * 0.3f, 0f), cubeSize);
                    }

                    if (ShowDeposit && cell.DepositRegionId != 0u)
                    {
                        Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.2f);
                        Gizmos.DrawCube(center + new Vector3(0f, OverlayThickness * 0.6f, 0f), cubeSize);
                    }
                }
            }
        }

        private void DrawGridLines(in StageRuntimeGridComponent grid, float planeY)
        {
            Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.9f);
            float y = planeY + GridLineYOffset;

            for (int x = 0; x <= grid.Width; x++)
            {
                float worldX = grid.OriginX + (x * grid.CellSize);
                Gizmos.DrawLine(
                    new Vector3(worldX, y, grid.OriginZ),
                    new Vector3(worldX, y, grid.OriginZ + (grid.Height * grid.CellSize)));
            }

            for (int z = 0; z <= grid.Height; z++)
            {
                float worldZ = grid.OriginZ + (z * grid.CellSize);
                Gizmos.DrawLine(
                    new Vector3(grid.OriginX, y, worldZ),
                    new Vector3(grid.OriginX + (grid.Width * grid.CellSize), y, worldZ));
            }
        }

        private void DrawSourceAnchors()
        {
            if (_sourceAnchorQuery.IsEmptyIgnoreFilter)
                return;

            using var entities = _sourceAnchorQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!_em.Exists(entity))
                    continue;

                var anchor = _em.GetComponentData<SourceAnchorComponent>(entity);
                var stableId = _em.GetComponentData<SourceStableIdComponent>(entity);
                Vector3 position = anchor.Position;

                Gizmos.color = new Color(0.1f, 0.85f, 1f, 1f);
                Gizmos.DrawSphere(position, AnchorSphereRadius);

#if UNITY_EDITOR
                if (ShowAnchorLabels)
                    Handles.Label(position + (Vector3.up * 0.25f), $"Source:{stableId.Value}");
#endif
            }
        }

        private static Vector3 GetCellCenter(int x, int y, in StageRuntimeGridComponent grid, float planeY)
        {
            return new Vector3(
                grid.OriginX + ((x + 0.5f) * grid.CellSize),
                planeY,
                grid.OriginZ + ((y + 0.5f) * grid.CellSize));
        }

        private static Color ResolveMovementColor(StageCellMovementFlags flags)
        {
            bool blockPlayer = (flags & StageCellMovementFlags.BlockPlayer) != 0;
            bool blockBullet = (flags & StageCellMovementFlags.BlockBullet) != 0;
            if (blockPlayer && blockBullet)
                return new Color(0.55f, 0.05f, 0.05f, 0.26f);
            if (blockPlayer)
                return new Color(0.95f, 0.15f, 0.15f, 0.22f);
            if (blockBullet)
                return new Color(0.9f, 0.1f, 0.85f, 0.22f);
            return Color.clear;
        }
    }

    public static class StageRuntimeGridDebugDrawerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeDrawer()
        {
            if (Object.FindFirstObjectByType<StageRuntimeGridDebugDrawer>() != null)
                return;

            var go = new GameObject("[Runtime] StageGridDebugDrawer");
            go.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<StageRuntimeGridDebugDrawer>();
        }
    }
}
