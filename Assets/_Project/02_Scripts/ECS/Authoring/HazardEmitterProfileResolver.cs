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
            in HazardActorEmissionAuthoring emission,
            out ResolvedHazardEmitterEmissionProfileSnapshot snapshot,
            out string error)
        {
            if (emission.Profile != null)
            {
                return TryResolve(
                    emission.Profile,
                    emission.EventRepeatCount,
                    emission.EventShotSchedule,
                    emission.EventShotIntervalSec,
                    emission.CooldownSec,
                    out snapshot,
                    out error);
            }

            snapshot = default;
            error = "Hazard actor emission profile is null.";
            return false;
        }

        public static bool TryResolve(
            EmissionProfileSO profile,
            int eventRepeatCount,
            SourceSpawnEventShotScheduleId eventShotSchedule,
            float eventShotIntervalSec,
            float cooldownSec,
            out ResolvedHazardEmitterEmissionProfileSnapshot snapshot,
            out string error)
        {
            snapshot = default;
            error = string.Empty;

            if (profile == null)
            {
                error = "Hazard actor emission profile is null.";
                return false;
            }

            if (!EmissionProfileResolver.TryResolve(profile, out var core, out error))
                return false;

            return TryCreateEmissionSnapshot(
                in core,
                eventRepeatCount,
                eventShotSchedule,
                eventShotIntervalSec,
                cooldownSec,
                out snapshot,
                out error);
        }

        private static bool TryCreateEmissionSnapshot(
            in ResolvedEmissionCore core,
            int eventRepeatCount,
            SourceSpawnEventShotScheduleId eventShotSchedule,
            float eventShotIntervalSec,
            float cooldownSec,
            out ResolvedHazardEmitterEmissionProfileSnapshot snapshot,
            out string error)
        {
            snapshot = default;
            error = string.Empty;

            if (core.AimMode == WaveAimModeId.Random)
            {
                error = "Hazard actor emission profile does not support Random aim.";
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
                EventShotSchedule = eventShotSchedule,
                EventShotIntervalSec = eventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                    ? Mathf.Max(0.001f, eventShotIntervalSec)
                    : 0f,
                EventRepeatCount = Mathf.Max(1, eventRepeatCount),
                CooldownSec = Mathf.Max(0f, cooldownSec),
            };
            return true;
        }
    }
}
