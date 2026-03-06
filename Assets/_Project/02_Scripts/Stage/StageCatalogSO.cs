using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [Serializable]
    public struct StageCatalogEntry
    {
        public bool Enabled;
        public string EntryKey;
        public StageDefinitionSO Definition;
        public StageLayoutSO Layout;
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Catalog", fileName = "sc_")]
    public class StageCatalogSO : ScriptableObject
    {
        public int SchemaVersion = 1;
        public StageCatalogEntry[] Entries;
    }
}
