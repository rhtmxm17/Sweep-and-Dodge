using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum SourceWavePhaseId : byte
    {
        Sustain = 0,
        OnStateEnterOnce = 1
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Bullet/Wave Clip", fileName = "bwc_")]
    public class WaveClipSO : ScriptableObject
    {
        [System.Serializable]
        public struct SpawnPayloadProfile
        {
            public BulletDefinitionSO Bullet;
        }

        [System.Serializable]
        public struct SpawnEmissionProfile
        {
            public SourceSpawnEmissionModeId EmissionMode;
            public SourceSpawnModeId SpawnMode;
            public float RatePerSecPerArea;
            public float MeanEventsPerSec;
            public int BurstRepeatCount;
            public float BurstIntervalSec;
            public int BurstShotsPerEvent;
            public SourceSpawnEventShotScheduleId EventShotSchedule;
            public float EventShotIntervalSec;
            public float MaxActiveDensityPerArea;
        }

        [System.Serializable]
        public struct SpawnSamplingProfile
        {
            public const int PointSetMaxCount = 4;

            public SourceSpawnSamplingModeId SamplingMode;
            public SourceSpawnCenterModeId CenterMode;
            public Vector2 FixedPoint;
            public Vector2 SpawnOffset;
            public Vector2 LineStart;
            public Vector2 LineEnd;
            public float SampleSpacing;
            public int PointCount;
            public Vector2 Point0;
            public Vector2 Point1;
            public Vector2 Point2;
            public Vector2 Point3;
            public int SpawnSampleBudget;
            public float PlayerNoSpawnRadius;
        }

        [System.Serializable]
        public struct SpawnDirectionProfile
        {
            public SourceSpawnDirectionModeId DirectionMode;
            public float BaseAngleDeg;
            public int NWayCount;
            public float SpiralStepDeg;
        }

        [System.Serializable]
        public struct SpawnEntry
        {
            [Header("Payload")]
            public SpawnPayloadProfile Payload;

            [Header("Directive Profiles")]
            public SpawnEmissionProfile Emission;
            public SpawnSamplingProfile Sampling;
            public SpawnDirectionProfile Direction;

            public BulletDefinitionSO ResolveBullet()
            {
                return Payload.Bullet;
            }

            public SourceSpawnEmissionModeId ResolveEmissionMode()
            {
                return Emission.EmissionMode;
            }

            public SourceSpawnModeId ResolveSpawnMode()
            {
                return Emission.SpawnMode;
            }

            public float ResolveRatePerSecPerArea()
            {
                return Emission.RatePerSecPerArea;
            }

            public float ResolveMaxActiveDensityPerArea()
            {
                return Emission.MaxActiveDensityPerArea;
            }

            public float ResolveMeanEventsPerSec()
            {
                return Emission.MeanEventsPerSec;
            }

            public int ResolveBurstRepeatCount()
            {
                if (Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst)
                    return 1;

                return Emission.BurstRepeatCount == 0 ? 1 : Emission.BurstRepeatCount;
            }

            public float ResolveBurstIntervalSec()
            {
                if (Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst)
                    return 1f;

                return Emission.BurstIntervalSec > 0f ? Emission.BurstIntervalSec : 1f;
            }

            public int ResolveBurstShotsPerEvent()
            {
                if (Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst
                    && Emission.EmissionMode != SourceSpawnEmissionModeId.Poisson)
                    return 1;

                return Emission.BurstShotsPerEvent > 0 ? Emission.BurstShotsPerEvent : 1;
            }

            public SourceSpawnEventShotScheduleId ResolveEventShotSchedule()
            {
                if (Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst
                    && Emission.EmissionMode != SourceSpawnEmissionModeId.Poisson)
                    return SourceSpawnEventShotScheduleId.Instant;

                return Emission.EventShotSchedule;
            }

            public float ResolveEventShotIntervalSec()
            {
                if (ResolveEventShotSchedule() != SourceSpawnEventShotScheduleId.Timed)
                    return 0f;

                return Emission.EventShotIntervalSec > 0f ? Emission.EventShotIntervalSec : 0.1f;
            }

            public SourceSpawnSamplingModeId ResolveSamplingMode()
            {
                return Sampling.SamplingMode;
            }

            public SourceSpawnCenterModeId ResolveCenterMode()
            {
                return Sampling.CenterMode;
            }

            public Vector2 ResolveFixedPoint()
            {
                return Sampling.FixedPoint;
            }

            public Vector2 ResolveSpawnOffset()
            {
                return Sampling.SpawnOffset;
            }

            public Vector2 ResolveLineStart()
            {
                return Sampling.LineStart;
            }

            public Vector2 ResolveLineEnd()
            {
                return Sampling.LineEnd;
            }

            public float ResolveSampleSpacing()
            {
                return Sampling.SampleSpacing > 0f ? Sampling.SampleSpacing : 1f;
            }

            public int ResolvePointSetCount()
            {
                return Mathf.Clamp(Sampling.PointCount, 0, SpawnSamplingProfile.PointSetMaxCount);
            }

            public Vector2 ResolvePointSetPoint(int index)
            {
                switch (index)
                {
                    case 0:
                        return Sampling.Point0;
                    case 1:
                        return Sampling.Point1;
                    case 2:
                        return Sampling.Point2;
                    case 3:
                        return Sampling.Point3;
                    default:
                        return Vector2.zero;
                }
            }

            public SourceSpawnDirectionModeId ResolveDirectionMode()
            {
                return Direction.DirectionMode;
            }

            public float ResolveBaseAngleDeg()
            {
                return Direction.BaseAngleDeg;
            }

            public int ResolveNWayCount()
            {
                return Direction.NWayCount > 0 ? Direction.NWayCount : 1;
            }

            public float ResolveSpiralStepDeg()
            {
                return Direction.SpiralStepDeg;
            }

            public int ResolveSpawnSampleBudget()
            {
                return Sampling.SpawnSampleBudget > 0 ? Sampling.SpawnSampleBudget : 16;
            }

            public float ResolvePlayerNoSpawnRadius()
            {
                return Sampling.PlayerNoSpawnRadius;
            }
        }

        [System.Serializable]
        public struct ClipSegment
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            [SerializeField, TextArea(2, 5)]
            private string editorOnlyDescription;
            #endif
            public float StartSec;
            public float EndSec;
            public SpawnEntry[] Entries;
        }

        [Header("Clip Metadata")]
        public int ClipId = 1;
        public SourceWavePhaseId Phase = SourceWavePhaseId.Sustain;
        public SourceSpawnLaneId Lane = SourceSpawnLaneId.Hazard;
        public float DurationSec = 1f;

        [Header("Local Segments (Overlap Allowed)")]
        public ClipSegment[] Segments;
    }
}
