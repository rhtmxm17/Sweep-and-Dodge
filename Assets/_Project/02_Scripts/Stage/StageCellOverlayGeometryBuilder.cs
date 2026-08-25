using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SweepNDodge.DotsBullets
{
    public readonly struct StageCellOverlayGeometryStats
    {
        public StageCellOverlayGeometryStats(
            int gridQuadCount,
            int movementFillQuadCount,
            int movementHatchQuadCount,
            int sourceOutlineQuadCount,
            int depositFillQuadCount,
            int depositOutlineQuadCount)
        {
            GridQuadCount = gridQuadCount;
            MovementFillQuadCount = movementFillQuadCount;
            MovementHatchQuadCount = movementHatchQuadCount;
            SourceOutlineQuadCount = sourceOutlineQuadCount;
            DepositFillQuadCount = depositFillQuadCount;
            DepositOutlineQuadCount = depositOutlineQuadCount;
        }

        public int GridQuadCount { get; }
        public int MovementFillQuadCount { get; }
        public int MovementHatchQuadCount { get; }
        public int SourceOutlineQuadCount { get; }
        public int DepositFillQuadCount { get; }
        public int DepositOutlineQuadCount { get; }
    }

    /// <summary>
    /// Builds the immutable Stage Cell overlay mesh from StageLayoutSO data.
    /// Source and Deposit use fill/perimeter geometry only; directional hatch is reserved for movement blocking.
    /// </summary>
    public static class StageCellOverlayGeometryBuilder
    {
        public static readonly Color32 GridColor = new(166, 179, 191, 26);
        public static readonly Color32 BlockPlayerColor = new(242, 64, 64, 92);
        public static readonly Color32 BlockBulletColor = new(230, 26, 217, 92);
        public static readonly Color32 BlockBothFillColor = new(140, 13, 13, 46);
        public static readonly Color32 SourceOutlineColor = new(26, 217, 255, 71);
        public static readonly Color32 DepositFillColor = new(255, 191, 26, 41);
        public static readonly Color32 DepositOutlineColor = new(255, 191, 26, 82);

        private const float GridLayerY = 0.012f;
        private const float DepositFillLayerY = 0.018f;
        private const float RegionOutlineLayerY = 0.028f;
        private const float MovementFillLayerY = 0.031f;
        private const float MovementHatchLayerY = 0.034f;

        public static StageCellOverlayGeometryStats BuildStaticMesh(
            in StageGridSpec grid,
            StageCellLayoutData[] cells,
            Mesh mesh)
        {
            if (mesh == null)
                return default;

            mesh.Clear();
            if (cells == null || grid.Width <= 0 || grid.Height <= 0 || grid.CellSize <= 0f)
                return default;

            int expectedCellCount = grid.Width * grid.Height;
            int cellCount = Mathf.Min(expectedCellCount, cells.Length);
            int estimatedQuads = Mathf.Max(32, (grid.Width + grid.Height + 2) + cellCount * 5);
            var vertices = new List<Vector3>(estimatedQuads * 4);
            var colors = new List<Color32>(estimatedQuads * 4);
            var indices = new List<int>(estimatedQuads * 6);

            int gridQuads = AppendGrid(in grid, vertices, colors, indices);
            int movementFillQuads = 0;
            int movementHatchQuads = 0;
            int depositFillQuads = 0;

            float cellInset = grid.CellSize * 0.055f;
            for (int index = 0; index < cellCount; index++)
            {
                int x = index % grid.Width;
                int y = index / grid.Width;
                var cell = cells[index];

                if (cell.DepositRegionId != 0u)
                {
                    AppendCellQuad(
                        x,
                        y,
                        grid.CellSize,
                        cellInset,
                        DepositFillLayerY,
                        DepositFillColor,
                        vertices,
                        colors,
                        indices);
                    depositFillQuads++;
                }

                bool blockPlayer = (cell.MovementFlags & StageCellMovementFlags.BlockPlayer) != 0;
                bool blockBullet = (cell.MovementFlags & StageCellMovementFlags.BlockBullet) != 0;
                if (blockPlayer && blockBullet)
                {
                    AppendCellQuad(
                        x,
                        y,
                        grid.CellSize,
                        cellInset,
                        MovementFillLayerY,
                        BlockBothFillColor,
                        vertices,
                        colors,
                        indices);
                    movementFillQuads++;
                }

                if (blockPlayer)
                    movementHatchQuads += AppendCellHatch(x, y, grid.CellSize, true, BlockPlayerColor, vertices, colors, indices);
                if (blockBullet)
                    movementHatchQuads += AppendCellHatch(x, y, grid.CellSize, false, BlockBulletColor, vertices, colors, indices);
            }

            int sourceOutlineQuads = AppendRegionOutlines(
                in grid,
                cells,
                cellCount,
                true,
                SourceOutlineColor,
                vertices,
                colors,
                indices);
            int depositOutlineQuads = AppendRegionOutlines(
                in grid,
                cells,
                cellCount,
                false,
                DepositOutlineColor,
                vertices,
                colors,
                indices);

            mesh.name = "StageCellOverlay_Static";
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            mesh.RecalculateBounds();

            return new StageCellOverlayGeometryStats(
                gridQuads,
                movementFillQuads,
                movementHatchQuads,
                sourceOutlineQuads,
                depositFillQuads,
                depositOutlineQuads);
        }

        private static int AppendGrid(
            in StageGridSpec grid,
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> indices)
        {
            float width = Mathf.Clamp(grid.CellSize * 0.018f, 0.01f, 0.04f);
            float stageWidth = grid.Width * grid.CellSize;
            float stageHeight = grid.Height * grid.CellSize;
            int count = 0;

            for (int x = 0; x <= grid.Width; x++)
            {
                float localX = x * grid.CellSize;
                AppendLineQuad(
                    new Vector2(localX, 0f),
                    new Vector2(localX, stageHeight),
                    width,
                    GridLayerY,
                    GridColor,
                    vertices,
                    colors,
                    indices);
                count++;
            }

            for (int y = 0; y <= grid.Height; y++)
            {
                float localZ = y * grid.CellSize;
                AppendLineQuad(
                    new Vector2(0f, localZ),
                    new Vector2(stageWidth, localZ),
                    width,
                    GridLayerY,
                    GridColor,
                    vertices,
                    colors,
                    indices);
                count++;
            }

            return count;
        }

        private static int AppendCellHatch(
            int x,
            int y,
            float cellSize,
            bool forward,
            Color32 color,
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> indices)
        {
            var cellCenter = new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
            var direction = forward
                ? new Vector2(1f, 1f).normalized
                : new Vector2(1f, -1f).normalized;
            var normal = new Vector2(-direction.y, direction.x);
            float halfLength = cellSize * 0.54f;
            float width = Mathf.Max(0.025f, cellSize * 0.055f);
            float offset = cellSize * 0.18f;

            for (int i = -1; i <= 1; i += 2)
            {
                var center = cellCenter + normal * (offset * i);
                AppendLineQuad(
                    center - direction * halfLength,
                    center + direction * halfLength,
                    width,
                    MovementHatchLayerY,
                    color,
                    vertices,
                    colors,
                    indices);
            }

            return 2;
        }

        private static int AppendRegionOutlines(
            in StageGridSpec grid,
            StageCellLayoutData[] cells,
            int cellCount,
            bool source,
            Color32 color,
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> indices)
        {
            float width = Mathf.Clamp(grid.CellSize * 0.045f, 0.02f, 0.08f);
            int count = 0;

            for (int index = 0; index < cellCount; index++)
            {
                uint regionId = GetRegionId(cells[index], source);
                if (regionId == 0u)
                    continue;

                int x = index % grid.Width;
                int y = index / grid.Width;
                float x0 = x * grid.CellSize;
                float z0 = y * grid.CellSize;
                float x1 = x0 + grid.CellSize;
                float z1 = z0 + grid.CellSize;

                if (GetNeighborRegionId(x - 1, y, in grid, cells, cellCount, source) != regionId)
                {
                    AppendLineQuad(new Vector2(x0, z0), new Vector2(x0, z1), width, RegionOutlineLayerY, color, vertices, colors, indices);
                    count++;
                }
                if (GetNeighborRegionId(x + 1, y, in grid, cells, cellCount, source) != regionId)
                {
                    AppendLineQuad(new Vector2(x1, z0), new Vector2(x1, z1), width, RegionOutlineLayerY, color, vertices, colors, indices);
                    count++;
                }
                if (GetNeighborRegionId(x, y - 1, in grid, cells, cellCount, source) != regionId)
                {
                    AppendLineQuad(new Vector2(x0, z0), new Vector2(x1, z0), width, RegionOutlineLayerY, color, vertices, colors, indices);
                    count++;
                }
                if (GetNeighborRegionId(x, y + 1, in grid, cells, cellCount, source) != regionId)
                {
                    AppendLineQuad(new Vector2(x0, z1), new Vector2(x1, z1), width, RegionOutlineLayerY, color, vertices, colors, indices);
                    count++;
                }
            }

            return count;
        }

        private static uint GetNeighborRegionId(
            int x,
            int y,
            in StageGridSpec grid,
            StageCellLayoutData[] cells,
            int cellCount,
            bool source)
        {
            if ((uint)x >= (uint)grid.Width || (uint)y >= (uint)grid.Height)
                return 0u;

            int index = y * grid.Width + x;
            if ((uint)index >= (uint)cellCount)
                return 0u;

            return GetRegionId(cells[index], source);
        }

        private static uint GetRegionId(in StageCellLayoutData cell, bool source)
        {
            return source ? cell.SourceRegionId : cell.DepositRegionId;
        }

        private static void AppendCellQuad(
            int x,
            int y,
            float cellSize,
            float inset,
            float layerY,
            Color32 color,
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> indices)
        {
            float x0 = x * cellSize + inset;
            float z0 = y * cellSize + inset;
            float x1 = (x + 1) * cellSize - inset;
            float z1 = (y + 1) * cellSize - inset;
            AppendQuad(
                new Vector3(x0, layerY, z0),
                new Vector3(x1, layerY, z0),
                new Vector3(x1, layerY, z1),
                new Vector3(x0, layerY, z1),
                color,
                vertices,
                colors,
                indices);
        }

        private static void AppendLineQuad(
            Vector2 start,
            Vector2 end,
            float width,
            float layerY,
            Color32 color,
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> indices)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= 0.000001f)
                return;

            direction.Normalize();
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            AppendQuad(
                new Vector3(start.x - normal.x, layerY, start.y - normal.y),
                new Vector3(end.x - normal.x, layerY, end.y - normal.y),
                new Vector3(end.x + normal.x, layerY, end.y + normal.y),
                new Vector3(start.x + normal.x, layerY, start.y + normal.y),
                color,
                vertices,
                colors,
                indices);
        }

        private static void AppendQuad(
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Color32 color,
            List<Vector3> vertices,
            List<Color32> colors,
            List<int> indices)
        {
            int start = vertices.Count;
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }
    }
}
