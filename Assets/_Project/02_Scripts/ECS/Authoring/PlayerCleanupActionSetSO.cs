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
            public float TrashRange;
            public float TrashFanHalfAngleDeg;
            public float HazardRingRadius;
            public float HazardRingWidth;
            public float HazardLineLength;
            public float HazardLineHalfWidth;
        }

        [Header("Initial State")]
        public PlayerCleanupActionId InitialSelectedAction = PlayerCleanupActionId.RadialRing;
        public PlayerCleanupActionId PrimarySlotAction = PlayerCleanupActionId.RadialRing;
        public PlayerCleanupActionId SecondarySlotAction = PlayerCleanupActionId.ForwardFanLine;

        [Header("Action Profiles")]
        public CleanupActionProfileEntry[] Profiles;
    }
}
