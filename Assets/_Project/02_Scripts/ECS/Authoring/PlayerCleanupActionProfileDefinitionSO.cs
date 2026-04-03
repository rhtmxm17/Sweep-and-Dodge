using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public abstract class PlayerCleanupActionProfileDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string ProfileKey = "broom_default";

        [Header("Timing")]
        public float CaptureActiveTime = 0.20f;
        public float CaptureCooldown = 0f;
        public float ActiveTime = 0.22f;
        public float Cooldown = 1.8f;

        [Header("Motion Constraint")]
        public bool LockFacingWhileActive = true;
        public float ActiveMoveSpeedScale = 0.5f;

        public abstract PlayerCleanupActionId ActionKind { get; }

        internal void ApplySharedFields(ref PlayerCleanupActionProfileBufferElement runtimeProfile)
        {
            runtimeProfile.CaptureActiveTime = CaptureActiveTime;
            runtimeProfile.CaptureCooldown = CaptureCooldown;
            runtimeProfile.ActiveTime = ActiveTime;
            runtimeProfile.Cooldown = Cooldown;
            runtimeProfile.LockFacingWhileActive = LockFacingWhileActive ? (byte)1 : (byte)0;
            runtimeProfile.ActiveMoveSpeedScale = ActiveMoveSpeedScale;
        }

        internal abstract void ApplyGeometry(ref PlayerCleanupActionProfileBufferElement runtimeProfile);
    }

}
