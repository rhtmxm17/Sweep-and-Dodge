using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Player/Cleanup Action Set", fileName = "pas_")]
    public class PlayerCleanupActionSetSO : ScriptableObject
    {
        [System.Serializable]
        public struct CleanupActionProfileEntry
        {
            public PlayerCleanupActionId ActionId;

            public float CaptureActiveTime;
            public float CaptureCooldown;
            public float ActiveTime;
            public float Cooldown;

            // legacy compatibility fields for non-default compatibility actions
            public float TrashRange;
            public float TrashFanHalfAngleDeg;
            public float HazardRingRadius;
            public float HazardRingWidth;
            public float HazardLineLength;
            public float HazardLineHalfWidth;

            public float TrashSweepInnerRadius;
            public float TrashSweepOuterRadius;
            public float TrashSweepHalfAngleDeg;
            public float TrashSweepStartAngleDeg;
            public float TrashSweepEndAngleDeg;
            public float HazardRectLength;
            public float HazardRectHalfWidth;
            public float HazardForwardWindowAngleDeg;
        }

        [Header("Initial State")]
        public PlayerCleanupActionId InitialSelectedAction = PlayerCleanupActionId.BroomSweep;
        public PlayerCleanupActionId PrimarySlotAction = PlayerCleanupActionId.BroomSweep;
        public PlayerCleanupActionId SecondarySlotAction = PlayerCleanupActionId.BroomSweep;

        [Header("Motion Constraints")]
        public bool LockFacingWhileActive = true;
        public float ActiveMoveSpeedScale = 0.5f;

        [Header("Action Profiles")]
        public CleanupActionProfileEntry[] Profiles;
    }
}
