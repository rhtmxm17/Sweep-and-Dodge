using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Bullet/Wave Clip", fileName = "bwc_")]
    public class WaveClipSO : ScriptableObject
    {
        [System.Serializable]
        public struct ClipSegment
        {
            public float StartSec;
            public float EndSec;
            public WaveTimelineSO.SpawnEntry[] Entries;
        }

        [Header("Clip Metadata")]
        public int ClipId = 1;
        public SourceWavePhaseId Phase = SourceWavePhaseId.Sustain;
        public SourceSpawnLaneId Lane = SourceSpawnLaneId.Hazard;
        public float DurationSec = 1f;

        [Header("Local Non-overlap Segments")]
        public ClipSegment[] Segments;
    }
}
