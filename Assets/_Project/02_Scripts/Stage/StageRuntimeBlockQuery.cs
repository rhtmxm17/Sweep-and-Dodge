using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    internal static class StageRuntimeBlockQuery
    {
        /// <summary>
        /// Bullet swept-path block query seam.
        /// P4.1은 full-cell BlockBullet만 구현하고, 이후 partial-cell shape는 이 narrow-phase 경로에서 확장한다.
        /// </summary>
        public static bool HitsBulletFullCell(
            float2 prevXZ,
            float2 nextXZ,
            float radius,
            in StageRuntimeGridComponent grid,
            DynamicBuffer<StageRuntimeGridCellBufferElement> cells)
        {
            var traversedCells = new FixedList4096Bytes<int2>();
            if (StageRuntimeGridUtility.CollectTraversedCells(prevXZ, nextXZ, radius, in grid, ref traversedCells))
            {
                for (int i = 0; i < traversedCells.Length; i++)
                {
                    int2 cell = traversedCells[i];
                    int index = StageRuntimeGridUtility.GetCellIndex(cell.x, cell.y, in grid);
                    if (index < 0)
                        continue;

                    if ((cells[index].MovementFlags & StageCellMovementFlags.BlockBullet) != 0)
                        return true;
                }

                return false;
            }

            if (!StageRuntimeGridUtility.TryGetSweptCellBounds(prevXZ, nextXZ, radius, in grid, out int2 minCell, out int2 maxCell))
                return false;

            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int index = StageRuntimeGridUtility.GetCellIndex(x, y, in grid);
                    if (index < 0)
                        continue;

                    if ((cells[index].MovementFlags & StageCellMovementFlags.BlockBullet) == 0)
                        continue;

                    if (StageRuntimeGridUtility.DoesSweptCircleIntersectCell(prevXZ, nextXZ, radius, x, y, in grid))
                        return true;
                }
            }

            return false;
        }
    }
}
