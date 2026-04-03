using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Player/Cleanup Action Profile/Broom Sweep", fileName = "pcap_broom_")]
    public sealed class BroomSweepCleanupActionProfileSO : PlayerCleanupActionProfileDefinitionSO
    {
        [Header("Trash Sweep")]
        public float TrashSweepInnerRadius = 1f;
        public float TrashSweepOuterRadius = 3.2f;
        public float TrashSweepHalfAngleDeg = 12f;
        public float TrashSweepStartAngleDeg = -20f;
        public float TrashSweepEndAngleDeg = 80f;

        [Header("Hazard Focus")]
        public float HazardRectLength = 3.2f;
        public float HazardRectHalfWidth = 0.55f;
        public float HazardForwardWindowAngleDeg = 7f;

        public override PlayerCleanupActionId ActionKind => PlayerCleanupActionId.BroomSweep;

        internal override void ApplyGeometry(ref PlayerCleanupActionProfileBufferElement runtimeProfile)
        {
            runtimeProfile.TrashSweepInnerRadius = TrashSweepInnerRadius;
            runtimeProfile.TrashSweepOuterRadius = TrashSweepOuterRadius;
            runtimeProfile.TrashSweepHalfAngleDeg = TrashSweepHalfAngleDeg;
            runtimeProfile.TrashSweepStartAngleDeg = TrashSweepStartAngleDeg;
            runtimeProfile.TrashSweepEndAngleDeg = TrashSweepEndAngleDeg;
            runtimeProfile.HazardRectLength = HazardRectLength;
            runtimeProfile.HazardRectHalfWidth = HazardRectHalfWidth;
            runtimeProfile.HazardForwardWindowAngleDeg = HazardForwardWindowAngleDeg;
        }
    }
}
