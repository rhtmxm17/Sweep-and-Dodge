using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Hazard/Hazard Emitter Emission Profile", fileName = "heep_")]
    public class HazardEmitterEmissionProfileSO : ScriptableObject
    {
        [Header("Common Profile Reference")]
        public EmissionProfileSO Profile;

        [Header("Payload")]
        public BulletDefinitionSO Bullet;

        [Header("Repeat / Cooldown")]
        [Min(1)] public int EventRepeatCount = 1;
        public SourceSpawnEventShotScheduleId EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
        [Min(0f)] public float EventShotIntervalSec = 0f;
        [Min(0f)] public float CooldownSec = 1f;

        [Header("Emission Grammar")]
        [SerializeReference] public WavePositionPatternAuthoringBase PositionPattern = new SinglePointPositionPatternAuthoring();
        [SerializeReference] public WaveAimAuthoringBase Aim = new FixedAimAuthoring();
        [SerializeReference] public WaveShotPatternAuthoringBase ShotPattern = new SingleShotPatternAuthoring();
    }
}
