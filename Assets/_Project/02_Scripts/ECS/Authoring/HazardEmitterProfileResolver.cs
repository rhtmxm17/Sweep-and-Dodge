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

            if (profile.Bullet == null)
            {
                error = "Hazard emitter emission profile is missing Bullet.";
                return false;
            }

            if (profile.Bullet.DefinitionId <= 0)
            {
                error = $"Hazard emitter emission profile references invalid DefinitionId {profile.Bullet.DefinitionId}.";
                return false;
            }

            if (profile.PositionPattern == null)
            {
                error = "Hazard emitter emission profile is missing PositionPattern.";
                return false;
            }

            if (profile.Aim == null)
            {
                error = "Hazard emitter emission profile is missing Aim.";
                return false;
            }

            if (profile.ShotPattern == null)
            {
                error = "Hazard emitter emission profile is missing ShotPattern.";
                return false;
            }

            if (profile.Aim.AimMode == WaveAimModeId.Random)
            {
                error = "Hazard emitter emission profile does not support Random aim in Plan D.";
                return false;
            }

            snapshot = new ResolvedHazardEmitterEmissionProfileSnapshot
            {
                Bullet = profile.Bullet,
                PositionPatternMode = profile.PositionPattern.PositionPatternMode,
                SpawnOffset = Vector2.zero,
                LineStart = Vector2.zero,
                LineEnd = Vector2.zero,
                SampleSpacing = 1f,
                PointSetCount = 0,
                Point0 = Vector2.zero,
                Point1 = Vector2.zero,
                Point2 = Vector2.zero,
                Point3 = Vector2.zero,
                AimMode = profile.Aim.AimMode,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 0f,
                AimAngleOffsetDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                LineNormalAngleOffsetDeg = 0f,
                SpiralStepDeg = 0f,
                ShotPatternMode = profile.ShotPattern.ShotPatternMode,
                ShotCount = 1,
                NWayAngleSpacingDeg = 0f,
                EventShotSchedule = profile.EventShotSchedule,
                EventShotIntervalSec = profile.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                    ? Mathf.Max(0.001f, profile.EventShotIntervalSec)
                    : 0f,
                EventRepeatCount = Mathf.Max(1, profile.EventRepeatCount),
                CooldownSec = Mathf.Max(0f, profile.CooldownSec),
            };

            ApplyPositionPattern(ref snapshot, profile.PositionPattern);
            ApplyAim(ref snapshot, profile.Aim);
            ApplyShotPattern(ref snapshot, profile.ShotPattern);
            return true;
        }

        private static void ApplyPositionPattern(
            ref ResolvedHazardEmitterEmissionProfileSnapshot snapshot,
            WavePositionPatternAuthoringBase positionPattern)
        {
            switch (positionPattern)
            {
                case LineEvenPositionPatternAuthoring lineEven:
                    snapshot.LineStart = lineEven.LineStart;
                    snapshot.LineEnd = lineEven.LineEnd;
                    snapshot.SampleSpacing = lineEven.SampleSpacing > 0f ? lineEven.SampleSpacing : 1f;
                    break;

                case PointSetPositionPatternAuthoring pointSet:
                    snapshot.PointSetCount = Mathf.Clamp(pointSet.Points?.Length ?? 0, 0, PointSetPositionPatternAuthoring.MaxPointCount);
                    snapshot.Point0 = GetPoint(pointSet.Points, 0);
                    snapshot.Point1 = GetPoint(pointSet.Points, 1);
                    snapshot.Point2 = GetPoint(pointSet.Points, 2);
                    snapshot.Point3 = GetPoint(pointSet.Points, 3);
                    break;
            }
        }

        private static void ApplyAim(
            ref ResolvedHazardEmitterEmissionProfileSnapshot snapshot,
            WaveAimAuthoringBase aim)
        {
            switch (aim)
            {
                case FixedAimAuthoring fixedAim:
                    snapshot.BaseAngleDeg = fixedAim.BaseAngleDeg;
                    break;

                case SpiralAimAuthoring spiralAim:
                    snapshot.BaseAngleDeg = spiralAim.BaseAngleDeg;
                    snapshot.SpiralStepDeg = spiralAim.SpiralStepDeg;
                    break;

                case PlayerPositionAimAuthoring playerAim:
                    snapshot.AimAngleOffsetDeg = playerAim.AngleOffsetDeg;
                    snapshot.AimSnapshotTiming = playerAim.SnapshotTiming;
                    break;

                case LineNormalAimAuthoring lineNormalAim:
                    snapshot.LineNormalSide = lineNormalAim.NormalSide;
                    snapshot.LineNormalAngleOffsetDeg = lineNormalAim.AngleOffsetDeg;
                    break;
            }
        }

        private static void ApplyShotPattern(
            ref ResolvedHazardEmitterEmissionProfileSnapshot snapshot,
            WaveShotPatternAuthoringBase shotPattern)
        {
            switch (shotPattern)
            {
                case NWayShotPatternAuthoring nWay:
                    snapshot.ShotCount = Mathf.Max(1, nWay.ShotCount);
                    snapshot.NWayAngleSpacingDeg = nWay.AngleSpacingDeg;
                    break;

                case RadialShotPatternAuthoring radial:
                    snapshot.ShotCount = Mathf.Max(1, radial.ShotCount);
                    break;
            }
        }

        private static Vector2 GetPoint(Vector2[] points, int index)
        {
            if (points == null || index < 0 || index >= points.Length)
                return Vector2.zero;

            return points[index];
        }
    }
}
