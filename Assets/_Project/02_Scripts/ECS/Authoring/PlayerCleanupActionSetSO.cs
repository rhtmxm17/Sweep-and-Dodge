using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Player/Cleanup Action Set", fileName = "pas_")]
    public class PlayerCleanupActionSetSO : ScriptableObject
    {
        [Header("Initial State")]
        public string InitialSelectedProfileKey = "broom_default";
        public string PrimarySlotProfileKey = "broom_default";
        public string SecondarySlotProfileKey = "broom_default";

        [Header("Action Profiles")]
        public PlayerCleanupActionProfileDefinitionSO[] Profiles;
    }
}
