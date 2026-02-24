using UnityEngine;
using UnityEngine.Serialization;

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
            public SourceSpawnWallMaskId WallMask;
            public float WallInset;
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
            [FormerlySerializedAs("Bullet")]
            [Header("Payload")]
            public BulletDefinitionSO Bullet;

            [Header("Emission (Legacy)")]
            public SourceSpawnModeId SpawnMode;
            public float SpawnDensityPerSecPerArea;
            public float MaxActiveDensityPerArea; // FixedDensity일 때 무시

            [Header("Directive Profiles (Preferred)")]
            public bool UseDirectiveProfiles;
            public SpawnPayloadProfile Payload;
            public SpawnEmissionProfile Emission;
            public SpawnSamplingProfile Sampling;
            public SpawnDirectionProfile Direction;

            public BulletDefinitionSO ResolveBullet()
            {
                if (!UseDirectiveProfiles)
                    return Bullet;

                return Payload.Bullet != null ? Payload.Bullet : Bullet;
            }

            public SourceSpawnEmissionModeId ResolveEmissionMode()
            {
                bool hasEmissionData = HasEmissionProfileData();
                return hasEmissionData
                    ? Emission.EmissionMode
                    : SourceSpawnEmissionModeId.RateField;
            }

            public SourceSpawnModeId ResolveSpawnMode()
            {
                bool hasEmissionData = HasEmissionProfileData();
                return hasEmissionData
                    ? Emission.SpawnMode
                    : SpawnMode;
            }

            public float ResolveRatePerSecPerArea()
            {
                return HasEmissionProfileData()
                    ? Emission.RatePerSecPerArea
                    : SpawnDensityPerSecPerArea;
            }

            public float ResolveMaxActiveDensityPerArea()
            {
                return HasEmissionProfileData()
                    ? Emission.MaxActiveDensityPerArea
                    : MaxActiveDensityPerArea;
            }

            public float ResolveMeanEventsPerSec()
            {
                return UseDirectiveProfiles ? Emission.MeanEventsPerSec : 0f;
            }

            public int ResolveBurstRepeatCount()
            {
                if (!UseDirectiveProfiles)
                    return 1;

                if (Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst)
                    return 1;

                return Emission.BurstRepeatCount == 0 ? 1 : Emission.BurstRepeatCount;
            }

            public float ResolveBurstIntervalSec()
            {
                if (!UseDirectiveProfiles || Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst)
                    return 1f;

                return Emission.BurstIntervalSec > 0f ? Emission.BurstIntervalSec : 1f;
            }

            public int ResolveBurstShotsPerEvent()
            {
                if (!UseDirectiveProfiles || Emission.EmissionMode != SourceSpawnEmissionModeId.EventBurst)
                    return 1;

                return Emission.BurstShotsPerEvent > 0 ? Emission.BurstShotsPerEvent : 1;
            }

            public SourceSpawnSamplingModeId ResolveSamplingMode()
            {
                if (!UseDirectiveProfiles)
                    return SourceSpawnSamplingModeId.PollutionTopK;

                // Project policy (2026-02): WallEven은 사용하지 않는다.
                // 기존 데이터 호환을 위해 WallEven 입력 시 LineEven으로 강제 폴백한다.
                if (Sampling.SamplingMode == SourceSpawnSamplingModeId.WallEven)
                    return SourceSpawnSamplingModeId.LineEven;

                return Sampling.SamplingMode;
            }

            public SourceSpawnCenterModeId ResolveCenterMode()
            {
                return UseDirectiveProfiles
                    ? Sampling.CenterMode
                    : SourceSpawnCenterModeId.SourceCenter;
            }

            public Vector2 ResolveFixedPoint()
            {
                return UseDirectiveProfiles ? Sampling.FixedPoint : Vector2.zero;
            }

            public Vector2 ResolveSpawnOffset()
            {
                return UseDirectiveProfiles ? Sampling.SpawnOffset : Vector2.zero;
            }

            public Vector2 ResolveLineStart()
            {
                return UseDirectiveProfiles ? Sampling.LineStart : Vector2.zero;
            }

            public Vector2 ResolveLineEnd()
            {
                return UseDirectiveProfiles ? Sampling.LineEnd : Vector2.zero;
            }

            public float ResolveSampleSpacing()
            {
                if (!UseDirectiveProfiles)
                    return 1f;

                return Sampling.SampleSpacing > 0f ? Sampling.SampleSpacing : 1f;
            }

            public SourceSpawnWallMaskId ResolveWallMask()
            {
                // WallEven 비활성 정책으로 전용 설정값은 사용하지 않는다.
                return SourceSpawnWallMaskId.All;
            }

            public float ResolveWallInset()
            {
                // WallEven 비활성 정책으로 전용 설정값은 사용하지 않는다.
                return 0f;
            }

            public SourceSpawnDirectionModeId ResolveDirectionMode()
            {
                return UseDirectiveProfiles
                    ? Direction.DirectionMode
                    : SourceSpawnDirectionModeId.Random;
            }

            public float ResolveBaseAngleDeg()
            {
                return UseDirectiveProfiles ? Direction.BaseAngleDeg : 0f;
            }

            public int ResolveNWayCount()
            {
                if (!UseDirectiveProfiles)
                    return 1;

                return Direction.NWayCount > 0 ? Direction.NWayCount : 1;
            }

            public float ResolveSpiralStepDeg()
            {
                if (!UseDirectiveProfiles)
                    return 0f;

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
                if (!UseDirectiveProfiles)
                    return 16;

                return Sampling.SpawnSampleBudget > 0 ? Sampling.SpawnSampleBudget : 16;
            }

            public float ResolvePlayerNoSpawnRadius()
            {
                return UseDirectiveProfiles ? Sampling.PlayerNoSpawnRadius : 0f;
            }

            private bool HasEmissionProfileData()
            {
                if (!UseDirectiveProfiles)
                    return false;

                return Emission.EmissionMode != SourceSpawnEmissionModeId.RateField
                       || Emission.SpawnMode != SourceSpawnModeId.FixedDensity
                       || Emission.RatePerSecPerArea != 0f
                       || Emission.MeanEventsPerSec != 0f
                       || Emission.BurstRepeatCount != 0
                       || Emission.BurstIntervalSec != 0f
                       || Emission.BurstShotsPerEvent != 0
                       || Emission.MaxActiveDensityPerArea != 0f;
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
