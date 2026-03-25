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
    }
}
