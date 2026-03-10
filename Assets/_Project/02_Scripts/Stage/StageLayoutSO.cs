using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Layout", fileName = "sl_")]
    public class StageLayoutSO : ScriptableObject
    {
        [Min(1)] public int StageId = 1;
        public StageSourceLayoutData[] Sources;
        public StageDepositLayoutData[] Deposits;
        public StageObstacleLayoutData[] Obstacles;
        public StagePresentationLayoutData[] Presentations;
    }
}
