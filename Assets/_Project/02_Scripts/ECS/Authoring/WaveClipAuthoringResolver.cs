using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public struct ResolvedWaveSpawnDirectiveSnapshot
    {
        public BulletDefinitionSO Bullet;

        public SourceSpawnEmissionModeId EmissionMode;
        public SourceSpawnModeId SpawnMode;
        public float MaxActiveDensityPerArea;
        public float RatePerSecPerArea;
        public float MeanEventsPerSec;
        public int BurstRepeatCount;
        public float BurstIntervalSec;
        public int EventRepeatCount;
        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;

        public WaveSamplingAnchorModeId SamplingAnchorMode;
        public WaveAreaSamplerModeId AreaSamplerMode;
        public Vector2 FixedPoint;
        public Vector2 SpawnOffset;
        public int SpawnSampleBudget;
        public float PlayerNoSpawnRadius;

        public WavePositionPatternModeId PositionPatternMode;
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
        public float SpiralStepDeg;
        public float AimAngleOffsetDeg;
        public WaveLineNormalSideId LineNormalSide;
        public float LineNormalAngleOffsetDeg;

        public WaveShotPatternModeId ShotPatternMode;
        public int ShotCount;
        public float NWayAngleSpacingDeg;
    }

    public static class WaveClipAuthoringResolver
    {
        public const int DefaultSpawnSampleBudget = 16;
        public const float DefaultBurstIntervalSec = 1f;
        public const float DefaultTimedEventShotIntervalSec = 0.1f;

        public static bool TryResolveTypedEntry(
            WaveSpawnEntryAuthoring entry,
            out ResolvedWaveSpawnDirectiveSnapshot snapshot,
            out string error)
        {
            snapshot = CreateDefaultSnapshot();
            error = string.Empty;

            if (entry == null)
            {
                error = "Typed wave directive entry is null.";
                return false;
            }

            if (entry.Emission == null)
            {
                error = "Typed wave directive entry is missing Emission authoring.";
                return false;
            }

            if (entry.Sampling == null)
            {
                error = "Typed wave directive entry is missing Sampling authoring.";
                return false;
            }

            if (entry.Sampling.Anchor == null)
            {
                error = "Typed wave directive entry is missing Sampling.Anchor authoring.";
                return false;
            }

            if (entry.Sampling.AreaSampler == null)
            {
                error = "Typed wave directive entry is missing Sampling.AreaSampler authoring.";
                return false;
            }

            if (entry.PositionPattern == null)
            {
                error = "Typed wave directive entry is missing PositionPattern authoring.";
                return false;
            }

            if (entry.Aim == null)
            {
                error = "Typed wave directive entry is missing Aim authoring.";
                return false;
            }

            if (entry.ShotPattern == null)
            {
                error = "Typed wave directive entry is missing ShotPattern authoring.";
                return false;
            }

            snapshot.Bullet = entry.Payload.Bullet;
            ApplyTypedEmission(ref snapshot, entry.Emission);
            ApplyTypedSampling(ref snapshot, entry.Sampling);
            ApplyTypedPositionPattern(ref snapshot, entry.PositionPattern);
            ApplyTypedAim(ref snapshot, entry.Aim);
            ApplyTypedShotPattern(ref snapshot, entry.ShotPattern);
            return true;
        }

        private static ResolvedWaveSpawnDirectiveSnapshot CreateDefaultSnapshot()
        {
            return new ResolvedWaveSpawnDirectiveSnapshot
            {
                Bullet = null,
                EmissionMode = SourceSpawnEmissionModeId.RateField,
                SpawnMode = SourceSpawnModeId.FixedDensity,
                MaxActiveDensityPerArea = 0f,
                RatePerSecPerArea = 0f,
                MeanEventsPerSec = 0f,
                BurstRepeatCount = 1,
                BurstIntervalSec = DefaultBurstIntervalSec,
                EventRepeatCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                FixedPoint = Vector2.zero,
                SpawnOffset = Vector2.zero,
                SpawnSampleBudget = DefaultSpawnSampleBudget,
                PlayerNoSpawnRadius = 0f,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                LineStart = Vector2.zero,
                LineEnd = Vector2.zero,
                SampleSpacing = 1f,
                PointSetCount = 0,
                Point0 = Vector2.zero,
                Point1 = Vector2.zero,
                Point2 = Vector2.zero,
                Point3 = Vector2.zero,
                AimMode = WaveAimModeId.Random,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 0f,
                SpiralStepDeg = 0f,
                AimAngleOffsetDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                LineNormalAngleOffsetDeg = 0f,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                NWayAngleSpacingDeg = 0f,
            };
        }

        private static void ApplyTypedEmission(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveEmissionAuthoringBase emission)
        {
            snapshot.EmissionMode = emission.EmissionMode;
            snapshot.SpawnMode = emission.SpawnMode;
            snapshot.MaxActiveDensityPerArea = emission.MaxActiveDensityPerArea;

            switch (emission)
            {
                case RateFieldEmissionAuthoring rateField:
                    snapshot.RatePerSecPerArea = rateField.RatePerSecPerArea;
                    break;

                case PoissonEmissionAuthoring poisson:
                    snapshot.MeanEventsPerSec = poisson.MeanEventsPerSec;
                    snapshot.EventRepeatCount = poisson.EventRepeatCount > 0 ? poisson.EventRepeatCount : 1;
                    snapshot.EventShotSchedule = poisson.EventShotSchedule;
                    snapshot.EventShotIntervalSec = poisson.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                        ? (poisson.EventShotIntervalSec > 0f ? poisson.EventShotIntervalSec : DefaultTimedEventShotIntervalSec)
                        : 0f;
                    break;

                case EventBurstEmissionAuthoring eventBurst:
                    snapshot.BurstRepeatCount = eventBurst.BurstRepeatCount == 0 ? 1 : eventBurst.BurstRepeatCount;
                    snapshot.BurstIntervalSec = eventBurst.BurstIntervalSec > 0f ? eventBurst.BurstIntervalSec : DefaultBurstIntervalSec;
                    snapshot.EventRepeatCount = eventBurst.EventRepeatCount > 0 ? eventBurst.EventRepeatCount : 1;
                    snapshot.EventShotSchedule = eventBurst.EventShotSchedule;
                    snapshot.EventShotIntervalSec = eventBurst.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                        ? (eventBurst.EventShotIntervalSec > 0f ? eventBurst.EventShotIntervalSec : DefaultTimedEventShotIntervalSec)
                        : 0f;
                    break;
            }
        }

        private static void ApplyTypedSampling(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveSamplingAuthoring sampling)
        {
            snapshot.SpawnSampleBudget = sampling.SpawnSampleBudget > 0 ? sampling.SpawnSampleBudget : DefaultSpawnSampleBudget;
            snapshot.PlayerNoSpawnRadius = sampling.PlayerNoSpawnRadius;

            switch (sampling.Anchor)
            {
                case FixedPointSamplingAnchorAuthoring fixedPoint:
                    snapshot.SamplingAnchorMode = fixedPoint.AnchorMode;
                    snapshot.FixedPoint = fixedPoint.FixedPoint;
                    break;
                case PlayerRelativeSamplingAnchorAuthoring playerRelative:
                    snapshot.SamplingAnchorMode = playerRelative.AnchorMode;
                    snapshot.SpawnOffset = playerRelative.SpawnOffset;
                    break;
                default:
                    snapshot.SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter;
                    break;
            }

            snapshot.AreaSamplerMode = sampling.AreaSampler.AreaSamplerMode;
        }

        private static void ApplyTypedPositionPattern(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WavePositionPatternAuthoringBase positionPattern)
        {
            snapshot.PositionPatternMode = positionPattern.PositionPatternMode;

            switch (positionPattern)
            {
                case LineEvenPositionPatternAuthoring lineEven:
                    snapshot.LineStart = lineEven.LineStart;
                    snapshot.LineEnd = lineEven.LineEnd;
                    snapshot.SampleSpacing = lineEven.SampleSpacing > 0f ? lineEven.SampleSpacing : 1f;
                    break;

                case PointSetPositionPatternAuthoring pointSet:
                    int pointCount = Mathf.Clamp(pointSet.Points?.Length ?? 0, 0, PointSetPositionPatternAuthoring.MaxPointCount);
                    snapshot.PointSetCount = pointCount;
                    snapshot.Point0 = GetPoint(pointSet.Points, 0);
                    snapshot.Point1 = GetPoint(pointSet.Points, 1);
                    snapshot.Point2 = GetPoint(pointSet.Points, 2);
                    snapshot.Point3 = GetPoint(pointSet.Points, 3);
                    break;
            }
        }

        private static void ApplyTypedAim(
            ref ResolvedWaveSpawnDirectiveSnapshot snapshot,
            WaveAimAuthoringBase aim)
        {
            snapshot.AimMode = aim.AimMode;

            switch (aim)
            {
                case FixedAimAuthoring fixedAim:
                    snapshot.BaseAngleDeg = fixedAim.BaseAngleDeg;
                    break;

                case SpiralAimAuthoring spiral:
                    snapshot.BaseAngleDeg = spiral.BaseAngleDeg;
                    snapshot.SpiralStepDeg = spiral.SpiralStepDeg;
                    break;

                case PlayerPositionAimAuthoring playerPositionAim:
                    snapshot.AimAngleOffsetDeg = playerPositionAim.AngleOffsetDeg;
                    snapshot.AimSnapshotTiming = playerPositionAim.SnapshotTiming;
                    break;

                case LineNormalAimAuthoring lineNormalAim:
                    snapshot.LineNormalSide = lineNormalAim.NormalSide;
                    snapshot.LineNormalAngleOffsetDeg = lineNormalAim.AngleOffsetDeg;
                    break;
            }
        }

        private static void ApplyTypedShotPattern(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveShotPatternAuthoringBase shotPattern)
        {
            snapshot.ShotPatternMode = shotPattern.ShotPatternMode;

            switch (shotPattern)
            {
                case NWayShotPatternAuthoring nWay:
                    snapshot.ShotCount = nWay.ShotCount > 0 ? nWay.ShotCount : 1;
                    snapshot.NWayAngleSpacingDeg = nWay.AngleSpacingDeg;
                    break;

                case RadialShotPatternAuthoring radial:
                    snapshot.ShotCount = radial.ShotCount > 0 ? radial.ShotCount : 1;
                    snapshot.NWayAngleSpacingDeg = 0f;
                    break;

                default:
                    snapshot.ShotCount = 1;
                    snapshot.NWayAngleSpacingDeg = 0f;
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
