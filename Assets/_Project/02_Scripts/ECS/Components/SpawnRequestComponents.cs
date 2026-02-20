using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    // Aggregated spawn request payload per source entity.
    [InternalBufferCapacity(8)]
    public struct SourceSpawnRequestBuffer : IBufferElementData
    {
        public int BulletTypeKey;
        public int Count;
        public uint OldestFrame;
    }

    // Global spawn request policy (singleton).
    public struct SpawnRequestPolicyComponent : IComponentData
    {
        public int BudgetPerFrame;
        public int MaxPendingCount;
        public uint MaxPendingAgeFrames;
        public uint WarningLogCooldownFrames; // default: 60
        public int WarningBacklogPercent;     // default: 70
        public int WarningHighBacklogPercent; // default: 85
    }

    // Global backlog/diagnostic counters (singleton).
    public struct SpawnBacklogMetricsComponent : IComponentData
    {
        public int PendingCount;
        public int DeferredByBudget;
        public int DeferredByPool;
        public int DroppedByCapacity;
        public int ExpiredByAge;
        public int LastFrameDroppedByCapacity;
        public int LastFrameExpiredByAge;
        public int LastFrameBudgetUsed;
        public uint LastWarningFrame;
    }

    // Round-robin cursor for fair budget consumption (singleton).
    public struct SpawnBudgetCursorComponent : IComponentData
    {
        public int SourceStartIndex;
    }
}
