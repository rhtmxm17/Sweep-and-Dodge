using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum HazardEmitterActivationPolicyId : byte
    {
        AlwaysCycle = 0,
        ProgressReactive = 1,
        TriggerReactive = 2,
        RotatingSet = 3,
    }

    public enum HazardEmitterLifecycleStateId : byte
    {
        Dormant = 0,
        Telegraph = 1,
        Emit = 2,
        Cooldown = 3,
    }

    public enum HazardEmitterAnchorKindId : byte
    {
        ObjectBound = 0,
        PointBound = 1,
    }

    public enum HazardEmitterMobilityId : byte
    {
        Static = 0,
        Dynamic = 1,
    }

    public struct HazardEmitterComponent : IComponentData
    {
        public int EmitterId;
        public Entity SourceEntity;
        public HazardEmitterActivationPolicyId ActivationPolicy;
        public HazardEmitterLifecycleStateId InitialLifecycleState;
        public HazardEmitterAnchorKindId AnchorKind;
        public HazardEmitterMobilityId Mobility;
    }

    public struct HazardEmitterAppliedConfigBaselineComponent : IComponentData
    {
        public byte IsEnabled;
        public byte IsSuppressed;
        public float3 LocalOffset;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
    }

    public struct HazardEmitterAppliedConfigComponent : IComponentData
    {
        public byte IsEnabled;
        public byte IsSuppressed;
        public float3 LocalOffset;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
    }

    public struct HazardEmitterTelegraphProfileBaselineComponent : IComponentData
    {
        public int ProfileId;
        public float TelegraphDurationSec;
    }

    public struct HazardEmitterTelegraphProfileComponent : IComponentData
    {
        public int ProfileId;
        public float TelegraphDurationSec;
    }

    public struct HazardEmitterEmissionProfileBaselineComponent : IComponentData
    {
        public int ProfileId;
        public int BulletTypeKey;

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
        public int EventRepeatCount;
        public float CooldownSec;
    }

    public struct HazardEmitterEmissionProfileComponent : IComponentData
    {
        public int ProfileId;
        public int BulletTypeKey;

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
        public int EventRepeatCount;
        public float CooldownSec;
    }

    public struct HazardEmitterRuntimeStateComponent : IComponentData
    {
        public HazardEmitterLifecycleStateId LifecycleState;
        public float StateElapsedSec;
    }

    [InternalBufferCapacity(4)]
    public struct SourceHazardEmitterRefBuffer : IBufferElementData
    {
        public Entity EmitterEntity;
        public int EmitterId;
    }
}
