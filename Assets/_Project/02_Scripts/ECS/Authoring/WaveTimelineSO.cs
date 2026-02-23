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
        public struct SpawnEntry
        {
            public BulletDefinitionSO Bullet;
            public SourceSpawnModeId SpawnMode;
            public float SpawnDensityPerSecPerArea;
            public float MaxActiveDensityPerArea; // FixedDensity일 때 무시
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
