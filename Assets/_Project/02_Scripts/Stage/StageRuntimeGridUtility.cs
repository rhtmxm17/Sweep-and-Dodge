using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public static class StageRuntimeGridUtility
    {
        public static bool IsReady(in StageRuntimeGridComponent grid)
        {
            return grid.Ready != 0
                && grid.Width > 0
                && grid.Height > 0
                && grid.CellSize > 0f;
        }

        public static int GetCellIndex(int x, int y, in StageRuntimeGridComponent grid)
        {
            if (!IsInBounds(x, y, in grid))
                return -1;

            return (y * grid.Width) + x;
        }

        public static bool TryGetCellIndex(float2 worldXZ, in StageRuntimeGridComponent grid, out int index)
        {
            index = -1;
            if (!TryGetCellCoord(worldXZ, in grid, out int2 cell))
                return false;

            index = GetCellIndex(cell.x, cell.y, in grid);
            return index >= 0;
        }

        public static bool TryGetCellCoord(float2 worldXZ, in StageRuntimeGridComponent grid, out int2 cell)
        {
            cell = default;
            if (!IsReady(in grid))
                return false;

            float invCellSize = 1f / grid.CellSize;
            cell = (int2)math.floor(new float2(
                (worldXZ.x - grid.OriginX) * invCellSize,
                (worldXZ.y - grid.OriginZ) * invCellSize));
            return IsInBounds(cell.x, cell.y, in grid);
        }

        public static void ComputeCircleCellBounds(float2 centerXZ, float radius, in StageRuntimeGridComponent grid, out int2 minCell, out int2 maxCell)
        {
            float safeRadius = math.max(0f, radius);
            float2 min = centerXZ - safeRadius;
            float2 max = centerXZ + safeRadius;
            minCell = (int2)math.floor(new float2(
                (min.x - grid.OriginX) / grid.CellSize,
                (min.y - grid.OriginZ) / grid.CellSize));
            maxCell = (int2)math.floor(new float2(
                (max.x - grid.OriginX) / grid.CellSize,
                (max.y - grid.OriginZ) / grid.CellSize));
        }

        public static bool TryGetSweptCellBounds(
            float2 prevXZ,
            float2 nextXZ,
            float radius,
            in StageRuntimeGridComponent grid,
            out int2 minCell,
            out int2 maxCell)
        {
            minCell = default;
            maxCell = default;
            if (!IsReady(in grid))
                return false;

            float safeRadius = math.max(0f, radius);
            float2 min = math.min(prevXZ, nextXZ) - safeRadius;
            float2 max = math.max(prevXZ, nextXZ) + safeRadius;
            minCell = (int2)math.floor(new float2(
                (min.x - grid.OriginX) / grid.CellSize,
                (min.y - grid.OriginZ) / grid.CellSize));
            maxCell = (int2)math.floor(new float2(
                (max.x - grid.OriginX) / grid.CellSize,
                (max.y - grid.OriginZ) / grid.CellSize));

            int2 gridMin = int2.zero;
            int2 gridMax = new int2(grid.Width - 1, grid.Height - 1);
            if (maxCell.x < gridMin.x || maxCell.y < gridMin.y || minCell.x > gridMax.x || minCell.y > gridMax.y)
                return false;

            minCell = math.clamp(minCell, gridMin, gridMax);
            maxCell = math.clamp(maxCell, gridMin, gridMax);
            return true;
        }

        public static bool CollectTraversedCells(
            float2 prevXZ,
            float2 nextXZ,
            float radius,
            in StageRuntimeGridComponent grid,
            ref FixedList4096Bytes<int2> cells)
        {
            cells.Clear();
            if (!TryGetSweptCellBounds(prevXZ, nextXZ, radius, in grid, out int2 minCell, out int2 maxCell))
                return true;

            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    if (!DoesSweptCircleIntersectCell(prevXZ, nextXZ, radius, x, y, in grid))
                        continue;

                    if (cells.Length >= cells.Capacity)
                        return false;

                    cells.Add(new int2(x, y));
                }
            }

            return true;
        }

        public static Vector3 GetAnchorWorldPosition(in StageGridSpec grid, int2 anchorCell, float2 anchorOffset, float y)
        {
            return new Vector3(
                grid.Origin.x + (anchorCell.x + anchorOffset.x + 0.5f) * grid.CellSize,
                y,
                grid.Origin.z + (anchorCell.y + anchorOffset.y + 0.5f) * grid.CellSize);
        }

        private static bool IsInBounds(int x, int y, in StageRuntimeGridComponent grid)
        {
            return x >= 0 && y >= 0 && x < grid.Width && y < grid.Height;
        }

        internal static bool DoesSweptCircleIntersectCell(
            float2 prevXZ,
            float2 nextXZ,
            float radius,
            int x,
            int y,
            in StageRuntimeGridComponent grid)
        {
            float2 cellMin = new float2(
                grid.OriginX + (x * grid.CellSize),
                grid.OriginZ + (y * grid.CellSize));
            float2 cellMax = cellMin + grid.CellSize;
            float safeRadius = math.max(0f, radius);
            float distanceSq = DistanceSqSegmentAabb(prevXZ, nextXZ, cellMin, cellMax);
            return distanceSq <= (safeRadius * safeRadius);
        }

        private static float DistanceSqSegmentAabb(float2 a, float2 b, float2 aabbMin, float2 aabbMax)
        {
            float distanceSq = math.min(
                DistanceSqPointAabb(a, aabbMin, aabbMax),
                DistanceSqPointAabb(b, aabbMin, aabbMax));

            float2 edge0Start = new float2(aabbMin.x, aabbMin.y);
            float2 edge0End = new float2(aabbMax.x, aabbMin.y);
            float2 edge1Start = new float2(aabbMax.x, aabbMin.y);
            float2 edge1End = new float2(aabbMax.x, aabbMax.y);
            float2 edge2Start = new float2(aabbMax.x, aabbMax.y);
            float2 edge2End = new float2(aabbMin.x, aabbMax.y);
            float2 edge3Start = new float2(aabbMin.x, aabbMax.y);
            float2 edge3End = new float2(aabbMin.x, aabbMin.y);

            distanceSq = math.min(distanceSq, DistanceSqSegmentSegment(a, b, edge0Start, edge0End));
            distanceSq = math.min(distanceSq, DistanceSqSegmentSegment(a, b, edge1Start, edge1End));
            distanceSq = math.min(distanceSq, DistanceSqSegmentSegment(a, b, edge2Start, edge2End));
            distanceSq = math.min(distanceSq, DistanceSqSegmentSegment(a, b, edge3Start, edge3End));
            return distanceSq;
        }

        private static float DistanceSqPointAabb(float2 point, float2 aabbMin, float2 aabbMax)
        {
            float2 clamped = math.clamp(point, aabbMin, aabbMax);
            return math.lengthsq(point - clamped);
        }

        private static float DistanceSqSegmentSegment(float2 p1, float2 q1, float2 p2, float2 q2)
        {
            const float epsilon = 1e-6f;
            float2 d1 = q1 - p1;
            float2 d2 = q2 - p2;
            float2 r = p1 - p2;
            float a = math.dot(d1, d1);
            float e = math.dot(d2, d2);
            float f = math.dot(d2, r);

            float s;
            float t;
            if (a <= epsilon && e <= epsilon)
                return math.lengthsq(p1 - p2);

            if (a <= epsilon)
            {
                s = 0f;
                t = math.saturate(f / e);
            }
            else
            {
                float c = math.dot(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = math.saturate(-c / a);
                }
                else
                {
                    float b = math.dot(d1, d2);
                    float denom = (a * e) - (b * b);
                    s = denom > epsilon ? math.saturate(((b * f) - (c * e)) / denom) : 0f;
                    t = ((b * s) + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = math.saturate(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = math.saturate((b - c) / a);
                    }
                }
            }

            float2 c1 = p1 + (d1 * s);
            float2 c2 = p2 + (d2 * t);
            return math.lengthsq(c1 - c2);
        }
    }
}
