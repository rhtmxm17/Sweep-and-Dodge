using Unity.Entities;
using Unity.Mathematics;

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
        public SourceSpawnEmissionModeId EmissionMode;
        public SourceSpawnModeId SpawnMode;
        public WaveSamplingAnchorModeId SamplingAnchorMode;
        public WaveAreaSamplerModeId AreaSamplerMode;
        public WavePositionPatternModeId PositionPatternMode;
        public WaveAimModeId AimMode;
        public WaveAimSnapshotTimingId AimSnapshotTiming;
        public float AimAngleOffsetDeg;
        public WaveShotPatternModeId ShotPatternMode;
        public int ShotCount;
        public int EventRepeatCount;
        public SourceSpawnSamplingModeId SamplingMode;
        public SourceSpawnCenterModeId CenterMode;
        public SourceSpawnDirectionModeId DirectionMode;
        public Unity.Mathematics.float2 FixedPoint;
        public Unity.Mathematics.float2 SpawnOffset;
        public Unity.Mathematics.float2 LineStart;
        public Unity.Mathematics.float2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public Unity.Mathematics.float2 Point0;
        public Unity.Mathematics.float2 Point1;
        public Unity.Mathematics.float2 Point2;
        public Unity.Mathematics.float2 Point3;
        public int SpawnSampleBudget;
        public float PlayerNoSpawnRadius;
        public float BaseAngleDeg;
        public int NWayCount;
        public float SpiralStepDeg;
        public int BurstShotsPerEvent;
        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;
        public float EventShotElapsedSec;
        public byte EventAnchorInitialized;
        public Unity.Mathematics.float3 EventAnchorPosition;
        public byte EventAimInitialized;
        public Unity.Mathematics.float3 EventAimTargetPosition;
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

    public struct BulletSecondarySpawnChannelSingletonTag : IComponentData { }

    public enum BulletSecondarySpawnShapeId : byte
    {
        SingleForward = 0,
        ForwardSpread = 1,
        PointBurst = 2,
    }

    [InternalBufferCapacity(32)]
    public struct BulletSecondarySpawnRequestBuffer : IBufferElementData
    {
        public int BulletTypeKey;
        public int Count;
        public byte Priority;
        public Entity SourceEntity;
        public Entity CauserEntity;
        public float3 OriginPosition;
        public float2 BaseDirection;
        public float SpreadAngleDeg;
        public float SpawnRadius;
        public BulletSecondarySpawnShapeId Shape;
        public uint OldestFrame;
        public uint Sequence;
    }

    public struct SecondarySpawnPolicyComponent : IComponentData
    {
        public int BudgetPerFrame;
        public int MaxPendingCount;
        public uint MaxPendingAgeFrames;
    }

    public struct SecondarySpawnBacklogMetricsComponent : IComponentData
    {
        public int PendingCount;
        public int DeferredByBudget;
        public int DeferredByPool;
        public int DroppedByCapacity;
        public int ExpiredByAge;
        public int LastFrameDroppedByCapacity;
        public int LastFrameExpiredByAge;
        public int LastFrameBudgetUsed;
    }
}
