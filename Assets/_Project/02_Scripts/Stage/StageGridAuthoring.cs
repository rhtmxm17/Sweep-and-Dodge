using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StageGridAuthoring : MonoBehaviour
    {
        public Grid Grid;
        public Tilemap MovementTilemap;
        public StageRegionPaintAsset SourceRegionPaint;
        public StageRegionPaintAsset DepositRegionPaint;
        public Vector2Int BoundsMinCell = Vector2Int.zero;
        public Vector2Int BoundsSize = Vector2Int.one;
        public bool ShowGridGizmo = true;
        public bool ShowMovementGizmo = true;
        public bool ShowSourceGizmo = true;
        public bool ShowDepositGizmo = true;
        public bool ShowAnchorGizmo = true;

        public BoundsInt GetAuthoringBounds()
        {
            return new BoundsInt(
                BoundsMinCell.x,
                BoundsMinCell.y,
                0,
                Mathf.Max(1, BoundsSize.x),
                Mathf.Max(1, BoundsSize.y),
                1);
        }

        public Vector3Int GetTilemapCell(int localX, int localY)
        {
            return new Vector3Int(BoundsMinCell.x + localX, BoundsMinCell.y + localY, 0);
        }

        public Vector2Int GetLocalCell(Vector3Int tileCell)
        {
            return new Vector2Int(tileCell.x - BoundsMinCell.x, tileCell.y - BoundsMinCell.y);
        }

        public StageGridSpec BuildRuntimeGridSpec()
        {
            float cellSize = Grid != null ? Mathf.Max(0.0001f, Grid.cellSize.x) : 1f;
            Vector3 gridPosition = Grid != null ? Grid.transform.position : Vector3.zero;
            return new StageGridSpec
            {
                Width = Mathf.Max(1, BoundsSize.x),
                Height = Mathf.Max(1, BoundsSize.y),
                CellSize = cellSize,
                Origin = new Vector3(
                    gridPosition.x + (BoundsMinCell.x * cellSize),
                    gridPosition.y,
                    gridPosition.z + (BoundsMinCell.y * cellSize)),
            };
        }
    }
}
