using UnityEngine;

namespace SweepNDodge.DotsBullets
{
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
            public float StartSec;
            public float EndSec;
            public SpawnEntry[] Entries;
        }

        [Header("Non-overlapping Wave Segments")]
        public WaveSegment[] Segments;
    }
}
