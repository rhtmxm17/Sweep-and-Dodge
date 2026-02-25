using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    // Aggregated spawn request payload per source entity.
    [InternalBufferCapacity(8)]
    public struct SourceSpawnRequestBuffer : IBufferElementData
    {
        public int DirectiveId;
        public SourceWavePhaseId Phase;
        public SourceSpawnLaneId Lane;
        public int LanePriority;
        public int BulletTypeKey;
        public SourceSpawnSamplingModeId SamplingMode;
        public SourceSpawnCenterModeId CenterMode;
        public SourceSpawnDirectionModeId DirectionMode;
        public Unity.Mathematics.float2 FixedPoint;
        public Unity.Mathematics.float2 SpawnOffset;
        public Unity.Mathematics.float2 LineStart;
        public Unity.Mathematics.float2 LineEnd;
        public float SampleSpacing;
        public int SpawnSampleBudget;
        public float PlayerNoSpawnRadius;
        public float BaseAngleDeg;
        public int NWayCount;
        public float SpiralStepDeg;
        public int BurstShotsPerEvent;
        public int SpawnPriority;
        public uint SpawnSequence;
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

    // Global deterministic run seed for v3 clip/lane selection.
    public struct SpawnRunSeedComponent : IComponentData
    {
        public uint Value;
    }

    // Stable per-source identifier used by deterministic lane selection.
    public struct SourceStableIdComponent : IComponentData
    {
        public uint Value;
    }
}
