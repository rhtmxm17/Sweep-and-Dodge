using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public struct ResolvedWaveSpawnDirectiveSnapshot
    {
        public BulletDefinitionSO Bullet;
        public SourceSpawnEmissionModeId EmissionMode;
        public SourceSpawnModeId SpawnMode;
        public SourceSpawnSamplingModeId SamplingMode;
        public SourceSpawnCenterModeId CenterMode;
        public SourceSpawnDirectionModeId DirectionMode;
        public Vector2 FixedPoint;
        public Vector2 SpawnOffset;
        public Vector2 LineStart;
        public Vector2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public Vector2 Point0;
        public Vector2 Point1;
        public Vector2 Point2;
        public Vector2 Point3;
        public int SpawnSampleBudget;
        public float PlayerNoSpawnRadius;
        public float BaseAngleDeg;
        public int NWayCount;
        public float SpiralStepDeg;
        public float RatePerSecPerArea;
        public float MeanEventsPerSec;
        public int BurstRepeatCount;
        public float BurstIntervalSec;
        public int BurstShotsPerEvent;
        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;
        public float MaxActiveDensityPerArea;
    }

    public static class WaveClipAuthoringResolver
    {
        public const int DefaultSpawnSampleBudget = 16;
        public const float DefaultBurstIntervalSec = 1f;
        public const float DefaultTimedEventShotIntervalSec = 0.1f;

        public static bool UsesTypedEntries(in WaveClipSO.ClipSegment segment)
        {
            return segment.Directives != null && segment.Directives.Length > 0;
        }

        public static int GetPreferredEntryCount(in WaveClipSO.ClipSegment segment)
        {
            if (UsesTypedEntries(in segment))
                return segment.Directives.Length;

            return segment.LegacyEntries?.Length ?? 0;
        }

        public static WaveSpawnEntryAuthoring ConvertLegacyEntry(in WaveClipSO.SpawnEntry legacyEntry)
        {
            return new WaveSpawnEntryAuthoring
            {
                Payload = legacyEntry.Payload,
                Emission = ConvertLegacyEmission(legacyEntry.Emission),
                Sampling = ConvertLegacySampling(legacyEntry.Sampling),
                Direction = ConvertLegacyDirection(legacyEntry.Direction),
            };
        }

        public static ResolvedWaveSpawnDirectiveSnapshot ResolveLegacyEntry(in WaveClipSO.SpawnEntry entry)
        {
            var snapshot = CreateDefaultSnapshot();
            snapshot.Bullet = entry.Payload.Bullet;
            ApplyLegacyEmission(ref snapshot, entry.Emission);
            ApplyLegacySampling(ref snapshot, entry.Sampling);
            ApplyLegacyDirection(ref snapshot, entry.Direction);
            return snapshot;
        }

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

            if (entry.Direction == null)
            {
                error = "Typed wave directive entry is missing Direction authoring.";
                return false;
            }

            snapshot.Bullet = entry.Payload.Bullet;
            ApplyTypedEmission(ref snapshot, entry.Emission);
            ApplyTypedSampling(ref snapshot, entry.Sampling);
            ApplyTypedDirection(ref snapshot, entry.Direction);
            return true;
        }

        private static ResolvedWaveSpawnDirectiveSnapshot CreateDefaultSnapshot()
        {
            return new ResolvedWaveSpawnDirectiveSnapshot
            {
                Bullet = null,
                EmissionMode = SourceSpawnEmissionModeId.RateField,
                SpawnMode = SourceSpawnModeId.FixedDensity,
                SamplingMode = SourceSpawnSamplingModeId.UniformField,
                CenterMode = SourceSpawnCenterModeId.SourceCenter,
                DirectionMode = SourceSpawnDirectionModeId.Random,
                FixedPoint = Vector2.zero,
                SpawnOffset = Vector2.zero,
                LineStart = Vector2.zero,
                LineEnd = Vector2.zero,
                SampleSpacing = 1f,
                PointSetCount = 0,
                Point0 = Vector2.zero,
                Point1 = Vector2.zero,
                Point2 = Vector2.zero,
                Point3 = Vector2.zero,
                SpawnSampleBudget = DefaultSpawnSampleBudget,
                PlayerNoSpawnRadius = 0f,
                BaseAngleDeg = 0f,
                NWayCount = 1,
                SpiralStepDeg = 0f,
                RatePerSecPerArea = 0f,
                MeanEventsPerSec = 0f,
                BurstRepeatCount = 1,
                BurstIntervalSec = DefaultBurstIntervalSec,
                BurstShotsPerEvent = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                MaxActiveDensityPerArea = 0f,
            };
        }

        private static void ApplyLegacyEmission(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveClipSO.SpawnEmissionProfile emission)
        {
            snapshot.EmissionMode = emission.EmissionMode;
            snapshot.SpawnMode = emission.SpawnMode;
            snapshot.RatePerSecPerArea = emission.RatePerSecPerArea;
            snapshot.MeanEventsPerSec = emission.MeanEventsPerSec;
            snapshot.BurstRepeatCount = emission.EmissionMode == SourceSpawnEmissionModeId.EventBurst
                ? (emission.BurstRepeatCount == 0 ? 1 : emission.BurstRepeatCount)
                : 1;
            snapshot.BurstIntervalSec = emission.EmissionMode == SourceSpawnEmissionModeId.EventBurst
                ? (emission.BurstIntervalSec > 0f ? emission.BurstIntervalSec : DefaultBurstIntervalSec)
                : DefaultBurstIntervalSec;
            snapshot.BurstShotsPerEvent = (emission.EmissionMode == SourceSpawnEmissionModeId.Poisson
                || emission.EmissionMode == SourceSpawnEmissionModeId.EventBurst)
                ? (emission.BurstShotsPerEvent > 0 ? emission.BurstShotsPerEvent : 1)
                : 1;
            snapshot.EventShotSchedule = (emission.EmissionMode == SourceSpawnEmissionModeId.Poisson
                || emission.EmissionMode == SourceSpawnEmissionModeId.EventBurst)
                ? emission.EventShotSchedule
                : SourceSpawnEventShotScheduleId.Instant;
            snapshot.EventShotIntervalSec = snapshot.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                ? (emission.EventShotIntervalSec > 0f ? emission.EventShotIntervalSec : DefaultTimedEventShotIntervalSec)
                : 0f;
            snapshot.MaxActiveDensityPerArea = emission.MaxActiveDensityPerArea;
        }

        private static void ApplyLegacySampling(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveClipSO.SpawnSamplingProfile sampling)
        {
            snapshot.SamplingMode = sampling.SamplingMode;
            snapshot.CenterMode = sampling.CenterMode;
            snapshot.FixedPoint = sampling.FixedPoint;
            snapshot.SpawnOffset = sampling.SpawnOffset;
            snapshot.LineStart = sampling.LineStart;
            snapshot.LineEnd = sampling.LineEnd;
            snapshot.SampleSpacing = sampling.SampleSpacing > 0f ? sampling.SampleSpacing : 1f;
            snapshot.PointSetCount = Mathf.Clamp(sampling.PointCount, 0, WaveClipSO.SpawnSamplingProfile.PointSetMaxCount);
            snapshot.Point0 = sampling.Point0;
            snapshot.Point1 = sampling.Point1;
            snapshot.Point2 = sampling.Point2;
            snapshot.Point3 = sampling.Point3;
            snapshot.SpawnSampleBudget = sampling.SpawnSampleBudget > 0 ? sampling.SpawnSampleBudget : DefaultSpawnSampleBudget;
            snapshot.PlayerNoSpawnRadius = sampling.PlayerNoSpawnRadius;
        }

        private static void ApplyLegacyDirection(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveClipSO.SpawnDirectionProfile direction)
        {
            snapshot.DirectionMode = direction.DirectionMode;
            snapshot.BaseAngleDeg = direction.BaseAngleDeg;
            snapshot.NWayCount = direction.NWayCount > 0 ? direction.NWayCount : 1;
            snapshot.SpiralStepDeg = direction.SpiralStepDeg;
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
                    snapshot.BurstShotsPerEvent = poisson.BurstShotsPerEvent > 0 ? poisson.BurstShotsPerEvent : 1;
                    snapshot.EventShotSchedule = poisson.EventShotSchedule;
                    snapshot.EventShotIntervalSec = poisson.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                        ? (poisson.EventShotIntervalSec > 0f ? poisson.EventShotIntervalSec : DefaultTimedEventShotIntervalSec)
                        : 0f;
                    break;

                case EventBurstEmissionAuthoring eventBurst:
                    snapshot.BurstRepeatCount = eventBurst.BurstRepeatCount == 0 ? 1 : eventBurst.BurstRepeatCount;
                    snapshot.BurstIntervalSec = eventBurst.BurstIntervalSec > 0f ? eventBurst.BurstIntervalSec : DefaultBurstIntervalSec;
                    snapshot.BurstShotsPerEvent = eventBurst.BurstShotsPerEvent > 0 ? eventBurst.BurstShotsPerEvent : 1;
                    snapshot.EventShotSchedule = eventBurst.EventShotSchedule;
                    snapshot.EventShotIntervalSec = eventBurst.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                        ? (eventBurst.EventShotIntervalSec > 0f ? eventBurst.EventShotIntervalSec : DefaultTimedEventShotIntervalSec)
                        : 0f;
                    break;
            }
        }

        private static void ApplyTypedSampling(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveSamplingAuthoringBase sampling)
        {
            snapshot.SamplingMode = sampling.SamplingMode;
            snapshot.CenterMode = sampling.CenterMode;
            snapshot.FixedPoint = sampling.FixedPoint;
            snapshot.SpawnOffset = sampling.SpawnOffset;
            snapshot.SpawnSampleBudget = sampling.SpawnSampleBudget > 0 ? sampling.SpawnSampleBudget : DefaultSpawnSampleBudget;
            snapshot.PlayerNoSpawnRadius = sampling.PlayerNoSpawnRadius;

            switch (sampling)
            {
                case LineEvenSamplingAuthoring lineEven:
                    snapshot.LineStart = lineEven.LineStart;
                    snapshot.LineEnd = lineEven.LineEnd;
                    snapshot.SampleSpacing = lineEven.SampleSpacing > 0f ? lineEven.SampleSpacing : 1f;
                    break;

                case PointSetSamplingAuthoring pointSet:
                    int pointCount = Mathf.Clamp(pointSet.Points?.Length ?? 0, 0, WaveClipSO.SpawnSamplingProfile.PointSetMaxCount);
                    snapshot.PointSetCount = pointCount;
                    snapshot.Point0 = GetPoint(pointSet.Points, 0);
                    snapshot.Point1 = GetPoint(pointSet.Points, 1);
                    snapshot.Point2 = GetPoint(pointSet.Points, 2);
                    snapshot.Point3 = GetPoint(pointSet.Points, 3);
                    break;
            }
        }

        private static void ApplyTypedDirection(ref ResolvedWaveSpawnDirectiveSnapshot snapshot, WaveDirectionAuthoringBase direction)
        {
            snapshot.DirectionMode = direction.DirectionMode;

            switch (direction)
            {
                case FixedDirectionAuthoring fixedDirection:
                    snapshot.BaseAngleDeg = fixedDirection.BaseAngleDeg;
                    break;

                case NWayDirectionAuthoring nWay:
                    snapshot.BaseAngleDeg = nWay.BaseAngleDeg;
                    snapshot.NWayCount = nWay.NWayCount > 0 ? nWay.NWayCount : 1;
                    break;

                case SpiralDirectionAuthoring spiral:
                    snapshot.BaseAngleDeg = spiral.BaseAngleDeg;
                    snapshot.SpiralStepDeg = spiral.SpiralStepDeg;
                    break;

                case RadialBurstDirectionAuthoring radialBurst:
                    snapshot.BaseAngleDeg = radialBurst.BaseAngleDeg;
                    break;
            }
        }

        private static WaveEmissionAuthoringBase ConvertLegacyEmission(WaveClipSO.SpawnEmissionProfile emission)
        {
            return emission.EmissionMode switch
            {
                SourceSpawnEmissionModeId.Poisson => new PoissonEmissionAuthoring
                {
                    SpawnMode = emission.SpawnMode,
                    MaxActiveDensityPerArea = emission.MaxActiveDensityPerArea,
                    MeanEventsPerSec = emission.MeanEventsPerSec,
                    BurstShotsPerEvent = emission.BurstShotsPerEvent,
                    EventShotSchedule = emission.EventShotSchedule,
                    EventShotIntervalSec = emission.EventShotIntervalSec,
                },
                SourceSpawnEmissionModeId.EventBurst => new EventBurstEmissionAuthoring
                {
                    SpawnMode = emission.SpawnMode,
                    MaxActiveDensityPerArea = emission.MaxActiveDensityPerArea,
                    BurstRepeatCount = emission.BurstRepeatCount,
                    BurstIntervalSec = emission.BurstIntervalSec,
                    BurstShotsPerEvent = emission.BurstShotsPerEvent,
                    EventShotSchedule = emission.EventShotSchedule,
                    EventShotIntervalSec = emission.EventShotIntervalSec,
                },
                _ => new RateFieldEmissionAuthoring
                {
                    SpawnMode = emission.SpawnMode,
                    MaxActiveDensityPerArea = emission.MaxActiveDensityPerArea,
                    RatePerSecPerArea = emission.RatePerSecPerArea,
                },
            };
        }

        private static WaveSamplingAuthoringBase ConvertLegacySampling(WaveClipSO.SpawnSamplingProfile sampling)
        {
            WaveSamplingAuthoringBase result = sampling.SamplingMode switch
            {
                SourceSpawnSamplingModeId.PollutionTopK => new PollutionTopKSamplingAuthoring(),
                SourceSpawnSamplingModeId.LineEven => new LineEvenSamplingAuthoring
                {
                    LineStart = sampling.LineStart,
                    LineEnd = sampling.LineEnd,
                    SampleSpacing = sampling.SampleSpacing,
                },
                SourceSpawnSamplingModeId.PointSet => new PointSetSamplingAuthoring
                {
                    Points = CollectLegacyPoints(sampling),
                },
                _ => new UniformFieldSamplingAuthoring(),
            };

            result.CenterMode = sampling.CenterMode;
            result.FixedPoint = sampling.FixedPoint;
            result.SpawnOffset = sampling.SpawnOffset;
            result.SpawnSampleBudget = sampling.SpawnSampleBudget;
            result.PlayerNoSpawnRadius = sampling.PlayerNoSpawnRadius;
            return result;
        }

        private static WaveDirectionAuthoringBase ConvertLegacyDirection(WaveClipSO.SpawnDirectionProfile direction)
        {
            return direction.DirectionMode switch
            {
                SourceSpawnDirectionModeId.Fixed => new FixedDirectionAuthoring
                {
                    BaseAngleDeg = direction.BaseAngleDeg,
                },
                SourceSpawnDirectionModeId.NWay => new NWayDirectionAuthoring
                {
                    BaseAngleDeg = direction.BaseAngleDeg,
                    NWayCount = direction.NWayCount,
                },
                SourceSpawnDirectionModeId.Spiral => new SpiralDirectionAuthoring
                {
                    BaseAngleDeg = direction.BaseAngleDeg,
                    SpiralStepDeg = direction.SpiralStepDeg,
                },
                SourceSpawnDirectionModeId.RadialBurst => new RadialBurstDirectionAuthoring
                {
                    BaseAngleDeg = direction.BaseAngleDeg,
                },
                _ => new RandomDirectionAuthoring(),
            };
        }

        private static Vector2[] CollectLegacyPoints(WaveClipSO.SpawnSamplingProfile sampling)
        {
            int pointCount = Mathf.Clamp(sampling.PointCount, 0, WaveClipSO.SpawnSamplingProfile.PointSetMaxCount);
            if (pointCount <= 0)
                return Array.Empty<Vector2>();

            var points = new Vector2[pointCount];
            if (pointCount > 0) points[0] = sampling.Point0;
            if (pointCount > 1) points[1] = sampling.Point1;
            if (pointCount > 2) points[2] = sampling.Point2;
            if (pointCount > 3) points[3] = sampling.Point3;
            return points;
        }

        private static Vector2 GetPoint(Vector2[] points, int index)
        {
            if (points == null || index < 0 || index >= points.Length)
                return Vector2.zero;

            return points[index];
        }
    }
}
