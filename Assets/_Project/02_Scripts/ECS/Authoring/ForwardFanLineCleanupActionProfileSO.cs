using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Player/Cleanup Action Profile/Forward Fan Line", fileName = "pcap_forward_")]
    public sealed class ForwardFanLineCleanupActionProfileSO : PlayerCleanupActionProfileDefinitionSO
    {
        [Header("Trash Sweep")]
        public float TrashRange = 3.2f;
        public float TrashFanHalfAngleDeg = 45f;

        [Header("Hazard Focus")]
        public float HazardLineLength = 3.2f;
        public float HazardLineHalfWidth = 0.5f;

        public override PlayerCleanupActionId ActionKind => PlayerCleanupActionId.ForwardFanLine;

        internal override void ApplyGeometry(ref PlayerCleanupActionProfileBufferElement runtimeProfile)
        {
            runtimeProfile.TrashRange = TrashRange;
            runtimeProfile.TrashFanHalfAngleDeg = TrashFanHalfAngleDeg;
            runtimeProfile.HazardLineLength = HazardLineLength;
            runtimeProfile.HazardLineHalfWidth = HazardLineHalfWidth;
        }
    }
}
