using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum HazardActorEmitLifecycleStateId : byte
    {
        Dormant = 0,
        Telegraph = 1,
        Emit = 2,
        Cooldown = 3,
    }

    public struct HazardActorEmitStateComponent : IComponentData
    {
        public HazardActorEmitLifecycleStateId LifecycleState;
        public float StateElapsedSec;
    }

    public struct HazardActorEmitActiveTelegraphComponent : IComponentData
    {
        public int AppliedPatternSlotId;
        public int ProfileId;
        public float TelegraphDurationSec;
    }

    public struct HazardActorEmitActiveEmissionComponent : IComponentData
    {
        public int AppliedPatternSlotId;
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

    public struct HazardActorEmitCycleSignalComponent : IComponentData
    {
        public uint CompletedVersion;
    }

    public static class HazardActorPatternRuntimeUtility
    {
        public const int InvalidPatternSlotId = -1;
        public const int CompatibilityPatternSlotId = 1;
        public const float CompatibilityBaseWeight = 1f;
        public const uint CompatibilityAvailabilityFlags = 0u;

        public static void ApplyExecutionSlotToRuntime(
            in HazardActorPatternExecutionSlotBuffer slot,
            ref HazardActorEmitActiveTelegraphComponent telegraph,
            ref HazardActorEmitActiveEmissionComponent emission)
        {
            telegraph.AppliedPatternSlotId = slot.PatternSlotId;
            telegraph.ProfileId = slot.TelegraphProfileRefId;
            telegraph.TelegraphDurationSec = slot.TelegraphDurationSec;

            emission.AppliedPatternSlotId = slot.PatternSlotId;
            emission.ProfileId = slot.EmissionProfileRefId;
            emission.BulletTypeKey = slot.BulletTypeKey;
            emission.PositionPatternMode = slot.PositionPatternMode;
            emission.SpawnOffset = slot.SpawnOffset;
            emission.LineStart = slot.LineStart;
            emission.LineEnd = slot.LineEnd;
            emission.SampleSpacing = slot.SampleSpacing;
            emission.PointSetCount = slot.PointSetCount;
            emission.Point0 = slot.Point0;
            emission.Point1 = slot.Point1;
            emission.Point2 = slot.Point2;
            emission.Point3 = slot.Point3;
            emission.AimMode = slot.AimMode;
            emission.AimSnapshotTiming = slot.AimSnapshotTiming;
            emission.BaseAngleDeg = slot.BaseAngleDeg;
            emission.AimAngleOffsetDeg = slot.AimAngleOffsetDeg;
            emission.LineNormalSide = slot.LineNormalSide;
            emission.LineNormalAngleOffsetDeg = slot.LineNormalAngleOffsetDeg;
            emission.SpiralStepDeg = slot.SpiralStepDeg;
            emission.ShotPatternMode = slot.ShotPatternMode;
            emission.ShotCount = slot.ShotCount;
            emission.NWayAngleSpacingDeg = slot.NWayAngleSpacingDeg;
            emission.EventShotSchedule = slot.EventShotSchedule;
            emission.EventShotIntervalSec = slot.EventShotIntervalSec;
            emission.EventRepeatCount = slot.EventRepeatCount;
            emission.CooldownSec = slot.CooldownSec;
        }
    }
}
