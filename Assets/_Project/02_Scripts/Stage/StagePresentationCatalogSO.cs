using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [Serializable]
    public struct StagePresentationCatalogEntry
    {
        public string PresentationKey;
        public GameObject Prefab;
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Presentation Catalog", fileName = "spc_")]
    public class StagePresentationCatalogSO : ScriptableObject
    {
        public int SchemaVersion = 1;
        public StagePresentationCatalogEntry[] Entries;
    }
}
