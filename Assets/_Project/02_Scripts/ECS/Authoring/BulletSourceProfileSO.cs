using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Bullet/Source Spawn Profile", fileName = "bsp_")]
    public class BulletSourceProfileSO : ScriptableObject
    {
        [System.Serializable]
        public struct SpawnEntry
        {
            public BulletDefinitionSO Bullet;
            public SourceSpawnModeId SpawnMode;
            public float SpawnRatePerSec;
            public int MaxActive;  // FixedRated 일 경우 무시
        }

        [System.Serializable]
        public struct StateConfig
        {
            public SourceStateId State;
            public SpawnEntry[] Entries;
        }

        [Header("State -> Entries")]
        public StateConfig[] States;
    }
}
