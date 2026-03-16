using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    // Runtime snapshot consumed by PlayerRuntimeHudBridge.
    public struct PlayerHudSnapshotComponent : IComponentData
    {
        public int CarryLoad;
        public int CarryCapacity;
        public int HazardStack;
        public float HazardRiskMultiplier;

        public int DepletedSourceCount;
        public int TotalSourceCount;

        public uint PressureSourceStableId;
        public int PressureSourceCollected;
        public int PressureSourceThresholdWeakened;
        public int PressureSourceThresholdDepleted;
        public float PressureSourceProgress01;

        public RunDirectorStageStateId StageState;
        public float StageStateElapsedSec;

        public int LastHitLossValue;
        public float HitFlashRemainingSec;

        public int TotalCollectValue;
        public int TotalCleanupValue;
        public int TotalHitValue;

        public uint LastUpdatedFrame;
    }
}
