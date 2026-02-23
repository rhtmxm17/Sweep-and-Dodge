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
            public float MaxActiveDensityPerArea;
        }

        [System.Serializable]
        public struct SpawnSamplingProfile
        {
            public SourceSpawnSamplingModeId SamplingMode;
            public SourceSpawnCenterModeId CenterMode;
            public Vector2 FixedPoint;
            public Vector2 SpawnOffset;
            public int SpawnSampleBudget;
            public float PlayerNoSpawnRadius;
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

            public SourceSpawnSamplingModeId ResolveSamplingMode()
            {
                return UseDirectiveProfiles
                    ? Sampling.SamplingMode
                    : SourceSpawnSamplingModeId.PollutionTopK;
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
