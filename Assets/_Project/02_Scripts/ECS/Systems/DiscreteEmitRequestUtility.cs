using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public struct DiscreteEmitRequestSeed
    {
        public DiscreteEmitProducerKind ProducerKind;
        public Entity SourceEntity;
        public Entity ProducerEntity;
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
        public int RepeatCount;

        public byte Priority;
    }

    public static class DiscreteEmitRequestUtility
    {
        public static DiscreteEmitRequestSeed BuildDiscreteEmitSeedFromWaveEvent(
            Entity sourceEntity,
            in SourceClipPatternBuffer pattern,
            float3 resolvedAnchorPosition,
            int directiveId,
            byte priority)
        {
            return new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = sourceEntity,
                ProducerEntity = sourceEntity,
                EmissionId = directiveId,
                ProfileRefId = pattern.ProfileRefId,
                BulletTypeKey = pattern.BulletTypeKey,
                HasSpeedOverride = pattern.HasSpeedOverride,
                SpeedOverride = pattern.SpeedOverride,
                HasLifetimeOverride = pattern.HasLifetimeOverride,
                LifetimeOverride = pattern.LifetimeOverride,
                HasMovementOverride = pattern.HasMovementOverride,
                MovementFamily = pattern.MovementFamily,
                DampedLinear = pattern.DampedLinear,
                HomingLite = pattern.HomingLite,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = sourceEntity,
                AnchorPosition = resolvedAnchorPosition,
                AnchorLocalOffset = float3.zero,
                PositionPatternMode = pattern.PositionPatternMode,
                SpawnOffset = pattern.SpawnOffset,
                LineStart = pattern.LineStart,
                LineEnd = pattern.LineEnd,
                SampleSpacing = pattern.SampleSpacing,
                PointSetCount = pattern.PointSetCount,
                Point0 = pattern.Point0,
                Point1 = pattern.Point1,
                Point2 = pattern.Point2,
                Point3 = pattern.Point3,
                AimMode = pattern.AimMode,
                AimSnapshotTiming = pattern.AimSnapshotTiming,
                BaseAngleDeg = pattern.BaseAngleDeg,
                AimAngleOffsetDeg = pattern.AimAngleOffsetDeg,
                LineNormalSide = pattern.LineNormalSide,
                LineNormalAngleOffsetDeg = pattern.LineNormalAngleOffsetDeg,
                SpiralStepDeg = pattern.SpiralStepDeg,
                ShotPatternMode = pattern.ShotPatternMode,
                ShotCount = pattern.ShotCount,
                NWayAngleSpacingDeg = pattern.NWayAngleSpacingDeg,
                EventShotSchedule = pattern.EventShotSchedule,
                EventShotIntervalSec = pattern.EventShotIntervalSec,
                RepeatCount = pattern.EventRepeatCount,
                Priority = priority,
            };
        }

        public static DiscreteEmitRequestSeed BuildDiscreteEmitSeedFromHazardActor(
            Entity sourceEntity,
            Entity actorEntity,
            int emissionId,
            in HazardActorEmitActiveEmissionComponent emissionProfile,
            float3 resolvedAnchorPosition,
            byte priority)
        {
            return new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.HazardActor,
                SourceEntity = sourceEntity,
                ProducerEntity = actorEntity,
                EmissionId = emissionId,
                ProfileRefId = emissionProfile.ProfileId,
                BulletTypeKey = emissionProfile.BulletTypeKey,
                HasSpeedOverride = emissionProfile.HasSpeedOverride,
                SpeedOverride = emissionProfile.SpeedOverride,
                HasLifetimeOverride = emissionProfile.HasLifetimeOverride,
                LifetimeOverride = emissionProfile.LifetimeOverride,
                HasMovementOverride = emissionProfile.HasMovementOverride,
                MovementFamily = emissionProfile.MovementFamily,
                DampedLinear = emissionProfile.DampedLinear,
                HomingLite = emissionProfile.HomingLite,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = actorEntity,
                AnchorPosition = resolvedAnchorPosition,
                AnchorLocalOffset = float3.zero,
                PositionPatternMode = emissionProfile.PositionPatternMode,
                SpawnOffset = emissionProfile.SpawnOffset,
                LineStart = emissionProfile.LineStart,
                LineEnd = emissionProfile.LineEnd,
                SampleSpacing = emissionProfile.SampleSpacing,
                PointSetCount = emissionProfile.PointSetCount,
                Point0 = emissionProfile.Point0,
                Point1 = emissionProfile.Point1,
                Point2 = emissionProfile.Point2,
                Point3 = emissionProfile.Point3,
                AimMode = emissionProfile.AimMode,
                AimSnapshotTiming = emissionProfile.AimSnapshotTiming,
                BaseAngleDeg = emissionProfile.BaseAngleDeg,
                AimAngleOffsetDeg = emissionProfile.AimAngleOffsetDeg,
                LineNormalSide = emissionProfile.LineNormalSide,
                LineNormalAngleOffsetDeg = emissionProfile.LineNormalAngleOffsetDeg,
                SpiralStepDeg = emissionProfile.SpiralStepDeg,
                ShotPatternMode = emissionProfile.ShotPatternMode,
                ShotCount = emissionProfile.ShotCount,
                NWayAngleSpacingDeg = emissionProfile.NWayAngleSpacingDeg,
                EventShotSchedule = emissionProfile.EventShotSchedule,
                EventShotIntervalSec = emissionProfile.EventShotIntervalSec,
                RepeatCount = emissionProfile.EventRepeatCount,
                Priority = priority,
            };
        }

        public static DiscreteEmitRequestBuffer CreateDiscreteEmitRequest(in DiscreteEmitRequestSeed seed, uint frame)
        {
            return new DiscreteEmitRequestBuffer
            {
                ProducerKind = seed.ProducerKind,
                SourceEntity = seed.SourceEntity,
                ProducerEntity = seed.ProducerEntity,
                EmissionId = seed.EmissionId,
                ProfileRefId = seed.ProfileRefId,
                BulletTypeKey = seed.BulletTypeKey,
                HasSpeedOverride = seed.HasSpeedOverride,
                SpeedOverride = seed.SpeedOverride,
                HasLifetimeOverride = seed.HasLifetimeOverride,
                LifetimeOverride = seed.LifetimeOverride,
                HasMovementOverride = seed.HasMovementOverride,
                MovementFamily = seed.MovementFamily,
                DampedLinear = seed.DampedLinear,
                HomingLite = seed.HomingLite,
                AnchorMode = seed.AnchorMode,
                AnchorEntity = seed.AnchorEntity,
                AnchorPosition = seed.AnchorPosition,
                AnchorLocalOffset = seed.AnchorLocalOffset,
                PositionPatternMode = seed.PositionPatternMode,
                SpawnOffset = seed.SpawnOffset,
                LineStart = seed.LineStart,
                LineEnd = seed.LineEnd,
                SampleSpacing = seed.SampleSpacing,
                PointSetCount = math.clamp(seed.PointSetCount, 0, 4),
                Point0 = seed.Point0,
                Point1 = seed.Point1,
                Point2 = seed.Point2,
                Point3 = seed.Point3,
                AimMode = seed.AimMode,
                AimSnapshotTiming = seed.AimSnapshotTiming,
                BaseAngleDeg = seed.BaseAngleDeg,
                AimAngleOffsetDeg = seed.AimAngleOffsetDeg,
                LineNormalSide = seed.LineNormalSide,
                LineNormalAngleOffsetDeg = seed.LineNormalAngleOffsetDeg,
                SpiralStepDeg = seed.SpiralStepDeg,
                ShotPatternMode = seed.ShotPatternMode,
                ShotCount = math.max(1, seed.ShotCount),
                NWayAngleSpacingDeg = seed.NWayAngleSpacingDeg,
                EventShotSchedule = seed.EventShotSchedule,
                EventShotIntervalSec = math.max(0f, seed.EventShotIntervalSec),
                RemainingRepeats = math.max(1, seed.RepeatCount),
                RepeatSequence = 0u,
                EventAimInitialized = 0,
                EventAimTargetPosition = float3.zero,
                EventShotElapsedSec = 0f,
                Priority = seed.Priority,
                OldestFrame = frame,
            };
        }

        public static int ResolveShotPatternUnitCount(in DiscreteEmitRequestBuffer request)
        {
            return request.ShotPatternMode switch
            {
                WaveShotPatternModeId.NWay => math.max(1, request.ShotCount),
                WaveShotPatternModeId.Radial => math.max(1, request.ShotCount),
                _ => 1,
            };
        }

        public static int ResolvePendingBulletEquivalent(in DiscreteEmitRequestBuffer request)
        {
            int repeats = math.max(0, request.RemainingRepeats);
            int shotsPerRepeat = ResolveShotPatternUnitCount(in request);
            if (repeats <= 0 || shotsPerRepeat <= 0)
                return 0;

            long pending = (long)repeats * shotsPerRepeat;
            return pending > int.MaxValue ? int.MaxValue : (int)pending;
        }
    }
}
