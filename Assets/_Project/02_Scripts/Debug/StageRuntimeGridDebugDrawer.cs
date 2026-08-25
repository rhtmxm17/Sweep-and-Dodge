using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
        public float HatchInset = 0.08f;
        public int HatchLineCount = 2;

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
                        DrawCellHatch(
                            center,
                            grid.CellSize,
                            planeY,
                            ResolveMovementColor(cell.MovementFlags),
                            StageGridHatchDirection.ForwardSlash);
                    }

                    if (ShowSource && cell.SourceRegionId != 0u)
                    {
                        DrawRegionCellFillAndPerimeter(
                            x,
                            y,
                            cell.SourceRegionId,
                            true,
                            in grid,
                            cells,
                            center,
                            planeY,
                            new Color(0.1f, 0.85f, 1f, 0.16f),
                            new Color(0.1f, 0.85f, 1f, 0.75f));
                    }

                    if (ShowDeposit && cell.DepositRegionId != 0u)
                    {
                        DrawRegionCellFillAndPerimeter(
                            x,
                            y,
                            cell.DepositRegionId,
                            false,
                            in grid,
                            cells,
                            center,
                            planeY,
                            new Color(1f, 0.75f, 0.1f, 0.16f),
                            new Color(1f, 0.75f, 0.1f, 0.75f));
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

        private void DrawCellHatch(Vector3 center, float cellSize, float planeY, Color color, StageGridHatchDirection direction)
        {
            Gizmos.color = color;
            float extent = math.max(0.01f, (cellSize * 0.5f) - HatchInset);
            int lineCount = math.max(1, HatchLineCount);
            float step = (extent * 2f) / lineCount;
            float y = planeY + GridLineYOffset;

            for (int i = 0; i < lineCount; i++)
            {
                float offset = -extent + (i + 0.5f) * step;
                if (direction == StageGridHatchDirection.ForwardSlash)
                {
                    Gizmos.DrawLine(
                        new Vector3(center.x + extent, y, center.z - offset),
                        new Vector3(center.x - offset, y, center.z + extent));
                }
                else
                {
                    Gizmos.DrawLine(
                        new Vector3(center.x - extent, y, center.z - offset),
                        new Vector3(center.x + offset, y, center.z + extent));
                }
            }
        }

        private void DrawRegionCellFillAndPerimeter(
            int x,
            int y,
            uint regionId,
            bool source,
            in StageRuntimeGridComponent grid,
            DynamicBuffer<StageRuntimeGridCellBufferElement> cells,
            Vector3 center,
            float planeY,
            Color fillColor,
            Color outlineColor)
        {
            var size = new Vector3(grid.CellSize, math.max(0.001f, OverlayThickness), grid.CellSize);
            var drawCenter = new Vector3(center.x, planeY + GridLineYOffset, center.z);
            Gizmos.color = fillColor;
            Gizmos.DrawCube(drawCenter, size);

            float x0 = grid.OriginX + x * grid.CellSize;
            float z0 = grid.OriginZ + y * grid.CellSize;
            float x1 = x0 + grid.CellSize;
            float z1 = z0 + grid.CellSize;
            float drawY = planeY + GridLineYOffset + OverlayThickness * 0.5f;
            Gizmos.color = outlineColor;

            if (GetNeighborRegionId(x - 1, y, source, in grid, cells) != regionId)
                Gizmos.DrawLine(new Vector3(x0, drawY, z0), new Vector3(x0, drawY, z1));
            if (GetNeighborRegionId(x + 1, y, source, in grid, cells) != regionId)
                Gizmos.DrawLine(new Vector3(x1, drawY, z0), new Vector3(x1, drawY, z1));
            if (GetNeighborRegionId(x, y - 1, source, in grid, cells) != regionId)
                Gizmos.DrawLine(new Vector3(x0, drawY, z0), new Vector3(x1, drawY, z0));
            if (GetNeighborRegionId(x, y + 1, source, in grid, cells) != regionId)
                Gizmos.DrawLine(new Vector3(x0, drawY, z1), new Vector3(x1, drawY, z1));
        }

        private static uint GetNeighborRegionId(
            int x,
            int y,
            bool source,
            in StageRuntimeGridComponent grid,
            DynamicBuffer<StageRuntimeGridCellBufferElement> cells)
        {
            int index = StageRuntimeGridUtility.GetCellIndex(x, y, in grid);
            if (index < 0 || index >= cells.Length)
                return 0u;

            var cell = cells[index];
            return source ? cell.SourceRegionId : cell.DepositRegionId;
        }

        private static Color ResolveMovementColor(StageCellMovementFlags flags)
        {
            bool blockPlayer = (flags & StageCellMovementFlags.BlockPlayer) != 0;
            bool blockBullet = (flags & StageCellMovementFlags.BlockBullet) != 0;
            if (blockPlayer && blockBullet)
                return new Color(0.55f, 0.05f, 0.05f, 0.55f);
            if (blockPlayer)
                return new Color(0.95f, 0.15f, 0.15f, 0.55f);
            if (blockBullet)
                return new Color(0.9f, 0.1f, 0.85f, 0.55f);
            return Color.clear;
        }

        private enum StageGridHatchDirection : byte
        {
            ForwardSlash = 0,
            BackSlash = 1,
        }
    }

}
