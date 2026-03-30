using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Layout", fileName = "sl_")]
    public class StageLayoutSO : ScriptableObject
    {
        [Min(1)] public int SchemaVersion = 2;
        [Min(1)] public int StageId = 1;
        public StageGridSpec Grid;
        public StageCellLayoutData[] Cells;
        public StageSourceRegionLayoutData[] SourceRegions;
        public StageDepositRegionLayoutData[] DepositRegions;
        public StagePlayerStartLayoutData PlayerStart;
        public StagePresentationLayoutData[] Presentations;
    }
}
