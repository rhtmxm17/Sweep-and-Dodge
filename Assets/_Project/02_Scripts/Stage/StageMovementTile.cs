using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Movement Tile", fileName = "smt_")]
    public sealed class StageMovementTile : Tile
    {
        public StageCellMovementFlags MovementFlags;
    }
}
