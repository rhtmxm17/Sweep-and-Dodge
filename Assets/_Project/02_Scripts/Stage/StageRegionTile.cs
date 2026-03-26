using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(
        fileName = "stage_region_tile",
        menuName = "SweepN Dodge/Stage/Region Tile")]
    public sealed class StageRegionTile : Tile
    {
        public StageRegionKind RegionKind;
        [Min(0)] public int RegionSlotIndex;
    }
}
