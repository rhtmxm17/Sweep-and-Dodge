using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public struct ResolvedHazardEmitterTelegraphProfileSnapshot
    {
        public int ProfileId;
        public float TelegraphDurationSec;
    }

    public struct ResolvedHazardEmitterEmissionProfileSnapshot
    {
        public ResolvedEmissionCore EmissionCore;
        public BulletDefinitionSO Bullet;
        public WavePositionPatternModeId PositionPatternMode;
        public Vector2 SpawnOffset;
        public Vector2 LineStart;
        public Vector2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public Vector2 Point0;
        public Vector2 Point1;
        public Vector2 Point2;
        public Vector2 Point3;
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

    public static class HazardEmitterProfileResolver
    {
        public static bool TryResolve(
            HazardEmitterTelegraphProfileSO profile,
            out ResolvedHazardEmitterTelegraphProfileSnapshot snapshot)
        {
            snapshot = default;
            if (profile == null)
                return false;

            snapshot = new ResolvedHazardEmitterTelegraphProfileSnapshot
            {
                ProfileId = profile.GetInstanceID(),
                TelegraphDurationSec = Mathf.Max(0f, profile.TelegraphDurationSec),
            };
            return true;
        }

        public static bool TryResolve(
            HazardEmitterEmissionProfileSO profile,
            out ResolvedHazardEmitterEmissionProfileSnapshot snapshot,
            out string error)
        {
            snapshot = default;
            error = string.Empty;

            if (profile == null)
            {
                error = "Hazard emitter emission profile is null.";
                return false;
            }

            if (!TryResolveEmissionCore(profile, out var core, out error))
                return false;

            if (core.AimMode == WaveAimModeId.Random)
            {
                error = "Hazard emitter emission profile does not support Random aim in Plan D.";
                return false;
            }

            snapshot = new ResolvedHazardEmitterEmissionProfileSnapshot
            {
                EmissionCore = core,
                Bullet = core.Bullet,
                PositionPatternMode = core.PositionPatternMode,
                SpawnOffset = core.SpawnOffset,
                LineStart = core.LineStart,
                LineEnd = core.LineEnd,
                SampleSpacing = core.SampleSpacing,
                PointSetCount = core.PointSetCount,
                Point0 = core.Point0,
                Point1 = core.Point1,
                Point2 = core.Point2,
                Point3 = core.Point3,
                AimMode = core.AimMode,
                AimSnapshotTiming = core.AimSnapshotTiming,
                BaseAngleDeg = core.BaseAngleDeg,
                AimAngleOffsetDeg = core.AimAngleOffsetDeg,
                LineNormalSide = core.LineNormalSide,
                LineNormalAngleOffsetDeg = core.LineNormalAngleOffsetDeg,
                SpiralStepDeg = core.SpiralStepDeg,
                ShotPatternMode = core.ShotPatternMode,
                ShotCount = core.ShotCount,
                NWayAngleSpacingDeg = core.NWayAngleSpacingDeg,
                EventShotSchedule = profile.EventShotSchedule,
                EventShotIntervalSec = profile.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                    ? Mathf.Max(0.001f, profile.EventShotIntervalSec)
                    : 0f,
                EventRepeatCount = Mathf.Max(1, profile.EventRepeatCount),
                CooldownSec = Mathf.Max(0f, profile.CooldownSec),
            };
            return true;
        }

        private static bool TryResolveEmissionCore(
            HazardEmitterEmissionProfileSO profile,
            out ResolvedEmissionCore core,
            out string error)
        {
            if (profile.Profile != null)
                return EmissionProfileResolver.TryResolve(profile.Profile, out core, out error);

            return EmissionProfileResolver.TryResolveInline(
                profile.Bullet,
                profile.PositionPattern,
                profile.Aim,
                profile.ShotPattern,
                profile.GetInstanceID(),
                out core,
                out error);
        }
    }
}
