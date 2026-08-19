using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public struct ResolvedWaveSpawnDirectiveSnapshot
    {
        public ResolvedEmissionCore EmissionCore;
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

            if (!TryResolveEmissionCore(entry, out var core, out error))
            {
                return false;
            }

            ApplyResolvedEmissionCore(ref snapshot, in core);
            ApplyTypedEmission(ref snapshot, entry.Emission);
            ApplyTypedSampling(ref snapshot, entry.Sampling);
            return true;
        }

        private static bool TryResolveEmissionCore(
            WaveSpawnEntryAuthoring entry,
            out ResolvedEmissionCore core,
            out string error)
        {
            return EmissionProfileResolver.TryResolve(entry.Profile, out core, out error);
        }

        private static ResolvedWaveSpawnDirectiveSnapshot CreateDefaultSnapshot()
        {
            return new ResolvedWaveSpawnDirectiveSnapshot
            {
                EmissionCore = default,
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

        private static void ApplyResolvedEmissionCore(
            ref ResolvedWaveSpawnDirectiveSnapshot snapshot,
            in ResolvedEmissionCore core)
        {
            snapshot.EmissionCore = core;
            snapshot.Bullet = core.Bullet;
            snapshot.PositionPatternMode = core.PositionPatternMode;
            snapshot.LineStart = core.LineStart;
            snapshot.LineEnd = core.LineEnd;
            snapshot.SampleSpacing = core.SampleSpacing;
            snapshot.PointSetCount = core.PointSetCount;
            snapshot.Point0 = core.Point0;
            snapshot.Point1 = core.Point1;
            snapshot.Point2 = core.Point2;
            snapshot.Point3 = core.Point3;
            snapshot.AimMode = core.AimMode;
            snapshot.AimSnapshotTiming = core.AimSnapshotTiming;
            snapshot.BaseAngleDeg = core.BaseAngleDeg;
            snapshot.SpiralStepDeg = core.SpiralStepDeg;
            snapshot.AimAngleOffsetDeg = core.AimAngleOffsetDeg;
            snapshot.LineNormalSide = core.LineNormalSide;
            snapshot.LineNormalAngleOffsetDeg = core.LineNormalAngleOffsetDeg;
            snapshot.ShotPatternMode = core.ShotPatternMode;
            snapshot.ShotCount = core.ShotCount;
            snapshot.NWayAngleSpacingDeg = core.NWayAngleSpacingDeg;
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

    }
}
