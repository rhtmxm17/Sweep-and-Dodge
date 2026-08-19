using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum EmissionTriggerOriginBindingId : byte
    {
        LifecycleContactPosition = 0,
    }

    public enum EmissionTriggerDirectionBindingId : byte
    {
        LifecycleContactDirection = 0,
    }

    public enum EmissionTriggerSourceBindingId : byte
    {
        CauserSourceEntity = 0,
    }

    public enum EmissionTriggerCauserBindingId : byte
    {
        CompletedBullet = 0,
    }

    [Serializable]
    public sealed class EmissionSpawnTuningAuthoring
    {
        public bool OverrideSpeed;
        [Min(0.001f)] public float SpeedOverride = 0.5f;
        public bool OverrideLifetime;
        [Min(0.001f)] public float LifetimeOverride = 4f;
    }

    [Serializable]
    public sealed class EmissionMovementTuningAuthoring
    {
        public bool OverrideMovement;
        public BulletMovementFamilyId Family = BulletMovementFamilyId.Linear;
        public BulletDampedLinearDefinition DampedLinear = new BulletDampedLinearDefinition
        {
            DampingPerSec = 1f,
            StopSpeedThreshold = 0.1f,
        };
        public BulletHomingLiteDefinition HomingLite = new BulletHomingLiteDefinition
        {
            TurnRateDegPerSec = 90f,
            MaxAcquireDistance = 10f,
            MinRetargetDistance = 0.25f,
        };
    }

    [Serializable]
    public sealed class EmissionMotionCompletedTriggerAuthoring
    {
        public bool Enabled;
        public EmissionProfileSO TargetProfile;
        public EmissionTriggerOriginBindingId OriginPosition = EmissionTriggerOriginBindingId.LifecycleContactPosition;
        public EmissionTriggerDirectionBindingId ForwardDirection = EmissionTriggerDirectionBindingId.LifecycleContactDirection;
        public EmissionTriggerSourceBindingId SourceEntity = EmissionTriggerSourceBindingId.CauserSourceEntity;
        public EmissionTriggerCauserBindingId CauserEntity = EmissionTriggerCauserBindingId.CompletedBullet;
        [Min(0f)] public float DelaySec;
    }

    [Serializable]
    public sealed class EmissionCleanupRemovedTriggerAuthoring
    {
        public bool Enabled;
        public EmissionProfileSO TargetProfile;
        public EmissionTriggerOriginBindingId OriginPosition = EmissionTriggerOriginBindingId.LifecycleContactPosition;
        public EmissionTriggerDirectionBindingId ForwardDirection = EmissionTriggerDirectionBindingId.LifecycleContactDirection;
        public EmissionTriggerSourceBindingId SourceEntity = EmissionTriggerSourceBindingId.CauserSourceEntity;
        public EmissionTriggerCauserBindingId CauserEntity = EmissionTriggerCauserBindingId.CompletedBullet;
        [Min(0f)] public float DelaySec;
    }

    [Serializable]
    public sealed class EmissionLifecycleTriggersAuthoring
    {
        public EmissionMotionCompletedTriggerAuthoring MotionCompleted = new EmissionMotionCompletedTriggerAuthoring();
        public EmissionCleanupRemovedTriggerAuthoring CleanupRemoved = new EmissionCleanupRemovedTriggerAuthoring();
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Bullet/Emission Profile", fileName = "ep_")]
    public sealed class EmissionProfileSO : ScriptableObject
    {
        [Header("Payload")]
        public BulletDefinitionSO Bullet;

        [Header("Spawn / Movement")]
        public EmissionSpawnTuningAuthoring SpawnTuning = new EmissionSpawnTuningAuthoring();
        public EmissionMovementTuningAuthoring MovementTuning = new EmissionMovementTuningAuthoring();

        [Header("Emission Grammar")]
        [SerializeReference] public WavePositionPatternAuthoringBase PositionPattern = new SinglePointPositionPatternAuthoring();
        [SerializeReference] public WaveAimAuthoringBase Aim = new FixedAimAuthoring();
        [SerializeReference] public WaveShotPatternAuthoringBase ShotPattern = new SingleShotPatternAuthoring();

        [Header("Lifecycle Triggers")]
        public EmissionLifecycleTriggersAuthoring LifecycleTriggers = new EmissionLifecycleTriggersAuthoring();
    }
}
