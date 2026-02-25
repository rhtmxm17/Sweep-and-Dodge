using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum SourceWavePhaseId : byte
    {
        Sustain = 0,
        OnStateEnterOnce = 1
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Bullet/Wave Timeline", fileName = "bwt_")]
    public class WaveTimelineSO : ScriptableObject
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
            public float MaxActiveDensityPerArea;
        }

        [System.Serializable]
        public struct SpawnSamplingProfile
        {
            public SourceSpawnSamplingModeId SamplingMode;
            public SourceSpawnCenterModeId CenterMode;
            public Vector2 FixedPoint;
            public Vector2 SpawnOffset;
            public Vector2 LineStart;
            public Vector2 LineEnd;
            public float SampleSpacing;
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
                if (Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst)
                    return 1;

                return Emission.BurstShotsPerEvent > 0 ? Emission.BurstShotsPerEvent : 1;
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

            public int ResolveSpawnPriority()
            {
                var bullet = ResolveBullet();
                if (bullet == null)
                    return 0;

                // Trash(StandardCollectible)를 명시적으로 최하 우선순위로 둔다.
                return bullet.CaptureRule == BulletCaptureRuleId.RiskTimedResolve ? 0 : -100;
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
        public struct WaveSegment
        {
            public int WaveId;
            public SourceStateId TargetState;
            public SourceWavePhaseId Phase;
            public float StartSec;
            public float EndSec;
            public SpawnEntry[] Entries;
        }

        [Header("Non-overlapping Wave Segments")]
        public WaveSegment[] Segments;
    }
}
