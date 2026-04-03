using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Player/Cleanup Action Profile/Radial Ring", fileName = "pcap_radial_")]
    public sealed class RadialRingCleanupActionProfileSO : PlayerCleanupActionProfileDefinitionSO
    {
        [Header("Trash Sweep")]
        public float TrashRange = 3.2f;

        [Header("Hazard Ring")]
        public float HazardRingRadius = 2.88f;
        public float HazardRingWidth = 0.8f;

        public override PlayerCleanupActionId ActionKind => PlayerCleanupActionId.RadialRing;

        internal override void ApplyGeometry(ref PlayerCleanupActionProfileBufferElement runtimeProfile)
        {
            runtimeProfile.TrashRange = TrashRange;
            runtimeProfile.HazardRingRadius = HazardRingRadius;
            runtimeProfile.HazardRingWidth = HazardRingWidth;
        }
    }
}
