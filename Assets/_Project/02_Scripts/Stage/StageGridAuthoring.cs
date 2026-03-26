using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets
{
    [System.Serializable]
    public struct StageRegionSlotMapping
    {
        [Min(0)] public int RegionSlotIndex;
        [Min(1)] public uint StableId;
    }

    [DisallowMultipleComponent]
    public sealed class StageGridAuthoring : MonoBehaviour
    {
        public Grid Grid;
        public Tilemap MovementTilemap;
        public Tilemap RegionTilemap;
        [HideInInspector] public Tilemap SourceTilemap;
        [HideInInspector] public Tilemap DepositTilemap;
        public Tilemap GroundVisualTilemap;
        public Tilemap WallVisualTilemap;
        public StageRegionSlotMapping[] SourceRegionMappings;
        public StageRegionSlotMapping[] DepositRegionMappings;
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

        public bool TryResolveStableId(StageRegionKind kind, int regionSlotIndex, out uint stableId)
        {
            stableId = 0u;
            if (regionSlotIndex <= 0)
                return false;

            var mappings = kind == StageRegionKind.Source ? SourceRegionMappings : DepositRegionMappings;
            if (mappings == null)
                return false;

            for (int i = 0; i < mappings.Length; i++)
            {
                if (mappings[i].RegionSlotIndex != regionSlotIndex)
                    continue;

                stableId = mappings[i].StableId;
                return stableId > 0u;
            }

            return false;
        }

        public StageRegionSlotMapping[] GetMappings(StageRegionKind kind)
        {
            return kind == StageRegionKind.Source ? SourceRegionMappings : DepositRegionMappings;
        }

        public Tilemap GetRegionTilemap(StageRegionKind kind)
        {
            if (RegionTilemap != null)
                return RegionTilemap;

            return kind == StageRegionKind.Source ? SourceTilemap : DepositTilemap;
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
