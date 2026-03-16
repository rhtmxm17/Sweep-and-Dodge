using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/UI/In-World Dialogue Catalog", fileName = "iwdc_")]
    public class InWorldDialogueCatalogSO : ScriptableObject
    {
        public int SchemaVersion = 1;
        public InWorldDialogueCatalogEntry[] Entries;
    }
}
