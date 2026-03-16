using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/UI/In-World Dialogue Speaker Catalog", fileName = "iwdspk_")]
    public class InWorldDialogueSpeakerCatalogSO : ScriptableObject
    {
        public int SchemaVersion = 1;
        public InWorldDialogueSpeakerProfile[] Profiles;
    }
}
