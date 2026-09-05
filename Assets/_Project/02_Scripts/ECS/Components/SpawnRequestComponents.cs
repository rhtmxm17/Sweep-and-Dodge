using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public struct SpawnedBulletRuntimeTuning
    {
        public int ProfileRefId;
        public byte HasSpeedOverride;
        public float SpeedOverride;
        public byte HasLifetimeOverride;
        public float LifetimeOverride;
        public byte HasMovementOverride;
        public BulletMovementFamilyId MovementFamily;
        public BulletDampedLinearDefinition DampedLinear;
        public BulletHomingLiteDefinition HomingLite;
    }

    // Aggregated spawn request payload per source entity.
    [InternalBufferCapacity(8)]
    public struct SourceSpawnRequestBuffer : IBufferElementData
    {
        public int DirectiveId;
        public int ProfileRefId;
        public SourceWavePhaseId Phase;
        public SourceSpawnLaneId Lane;
        public int LanePriority;
        public int BulletTypeKey;
        public byte HasSpeedOverride;
        public float SpeedOverride;
        public byte HasLifetimeOverride;
        public float LifetimeOverride;
        public byte HasMovementOverride;
        public BulletMovementFamilyId MovementFamily;
        public BulletDampedLinearDefinition DampedLinear;
        public BulletHomingLiteDefinition HomingLite;
        public SourceSpawnEmissionModeId EmissionMode;
        public SourceSpawnModeId SpawnMode;
        public WaveSamplingAnchorModeId SamplingAnchorMode;
        public WaveAreaSamplerModeId AreaSamplerMode;
        public WavePositionPatternModeId PositionPatternMode;
        public WaveAimModeId AimMode;
        public WaveAimSnapshotTimingId AimSnapshotTiming;
        public float AimAngleOffsetDeg;
        public WaveLineNormalSideId LineNormalSide;
        public float LineNormalAngleOffsetDeg;
        public WaveShotPatternModeId ShotPatternMode;
        public int ShotCount;
        public float NWayAngleSpacingDeg;
        public int EventRepeatCount;
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
        public float SpiralStepDeg;
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

    public struct DiscreteEmitChannelSingletonTag : IComponentData { }

    public struct EmissionProfileRuntimeRegistryTag : IComponentData { }

    [InternalBufferCapacity(32)]
    public struct EmissionProfileRuntimeRegistryBuffer : IBufferElementData
    {
        public int ProfileRefId;
        public int BulletTypeKey;
        public byte HasSpeedOverride;
        public float SpeedOverride;
        public byte HasLifetimeOverride;
        public float LifetimeOverride;
        public byte HasMovementOverride;
        public BulletMovementFamilyId MovementFamily;
        public BulletDampedLinearDefinition DampedLinear;
        public BulletHomingLiteDefinition HomingLite;

        public WavePositionPatternModeId PositionPatternMode;
        public float2 SpawnOffset;
        public float2 LineStart;
        public float2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public float2 Point0;
        public float2 Point1;
        public float2 Point2;
        public float2 Point3;

        public WaveAimModeId AimMode;
        public WaveAimSnapshotTimingId AimSnapshotTiming;
        public float BaseAngleDeg;
        public float AimAngleOffsetDeg;
        public WaveLineNormalSideId LineNormalSide;
        public float LineNormalAngleOffsetDeg;
        public float SpiralStepDeg;

        public WaveShotPatternModeId ShotPatternMode;
        public int ShotCount;
        public float NWayAngleSpacingDeg;

        public byte HasMotionCompletedTrigger;
        public int MotionCompletedTargetProfileRefId;
        public EmissionTriggerOriginBindingId MotionCompletedOriginPosition;
        public EmissionTriggerDirectionBindingId MotionCompletedForwardDirection;
        public EmissionTriggerSourceBindingId MotionCompletedSourceEntity;
        public EmissionTriggerCauserBindingId MotionCompletedCauserEntity;
        public float MotionCompletedDelaySec;

        public byte HasCleanupRemovedTrigger;
        public int CleanupRemovedTargetProfileRefId;
        public EmissionTriggerOriginBindingId CleanupRemovedOriginPosition;
        public EmissionTriggerDirectionBindingId CleanupRemovedForwardDirection;
        public EmissionTriggerSourceBindingId CleanupRemovedSourceEntity;
        public EmissionTriggerCauserBindingId CleanupRemovedCauserEntity;
        public float CleanupRemovedDelaySec;
    }

    public enum DiscreteEmitProducerKind : byte
    {
        WaveClipEvent = 0,
        HazardActor = 1,
        TriggeredEmission = 2,
    }

    public enum DiscreteEmitAnchorMode : byte
    {
        FixedWorld = 0,
        SourceRelative = 1,
    }

    [InternalBufferCapacity(32)]
    public struct DiscreteEmitRequestBuffer : IBufferElementData
    {
        public DiscreteEmitProducerKind ProducerKind;
        public Entity SourceEntity;
        public Entity ProducerEntity;
        public Entity CauserEntity;
        public int EmissionId;
        public int ProfileRefId;
        public int BulletTypeKey;
        public byte HasSpeedOverride;
        public float SpeedOverride;
        public byte HasLifetimeOverride;
        public float LifetimeOverride;
        public byte HasMovementOverride;
        public BulletMovementFamilyId MovementFamily;
        public BulletDampedLinearDefinition DampedLinear;
        public BulletHomingLiteDefinition HomingLite;

        public DiscreteEmitAnchorMode AnchorMode;
        public Entity AnchorEntity;
        public float3 AnchorPosition;
        public float3 AnchorLocalOffset;

        public WavePositionPatternModeId PositionPatternMode;
        public float2 SpawnOffset;
        public float2 LineStart;
        public float2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public float2 Point0;
        public float2 Point1;
        public float2 Point2;
        public float2 Point3;

        public WaveAimModeId AimMode;
        public WaveAimSnapshotTimingId AimSnapshotTiming;
        public float BaseAngleDeg;
        public float AimAngleOffsetDeg;
        public WaveLineNormalSideId LineNormalSide;
        public float LineNormalAngleOffsetDeg;
        public float SpiralStepDeg;

        public WaveShotPatternModeId ShotPatternMode;
        public int ShotCount;
        public float NWayAngleSpacingDeg;

        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;
        public int RemainingRepeats;
        public uint RepeatSequence;

        public byte EventAimInitialized;
        public float3 EventAimTargetPosition;
        public float EventShotElapsedSec;

        public byte Priority;
        // Last successful progress or transition into a consumable state; intentional schedule waits refresh it.
        public uint OldestFrame;
        public uint ReadyFrame;
    }

    public struct DiscreteEmitPolicyComponent : IComponentData
    {
        public int BudgetPerFrame;
        public int MaxPendingCount;
        public uint MaxPendingAgeFrames;
        public int WaveClipEventBudgetPerFrame;
        public int HazardActorBudgetPerFrame;
        public int TriggeredEmissionBudgetPerFrame;
        public int WaveClipEventMaxPendingCount;
        public int HazardActorMaxPendingCount;
        public int TriggeredEmissionMaxPendingCount;
    }

    public struct DiscreteEmitBacklogMetricsComponent : IComponentData
    {
        public int PendingCount;
        public int PendingWaveClipEvent;
        public int PendingHazardActor;
        public int PendingTriggeredEmission;
        public int DeferredByBudget;
        public int DeferredByBudgetWaveClipEvent;
        public int DeferredByBudgetHazardActor;
        public int DeferredByBudgetTriggeredEmission;
        public int DeferredByPool;
        public int DeferredByPoolWaveClipEvent;
        public int DeferredByPoolHazardActor;
        public int DeferredByPoolTriggeredEmission;
        public int DroppedByCapacity;
        public int DroppedByCapacityWaveClipEvent;
        public int DroppedByCapacityHazardActor;
        public int DroppedByCapacityTriggeredEmission;
        public int ExpiredByAge;
        public int ExpiredByAgeWaveClipEvent;
        public int ExpiredByAgeHazardActor;
        public int ExpiredByAgeTriggeredEmission;
        public int LastFrameDroppedByCapacity;
        public int LastFrameDroppedByCapacityWaveClipEvent;
        public int LastFrameDroppedByCapacityHazardActor;
        public int LastFrameDroppedByCapacityTriggeredEmission;
        public int LastFrameExpiredByAge;
        public int LastFrameExpiredByAgeWaveClipEvent;
        public int LastFrameExpiredByAgeHazardActor;
        public int LastFrameExpiredByAgeTriggeredEmission;
        public int LastFrameBudgetUsed;
        public int LastFrameBudgetUsedWaveClipEvent;
        public int LastFrameBudgetUsedHazardActor;
        public int LastFrameBudgetUsedTriggeredEmission;
    }
}
