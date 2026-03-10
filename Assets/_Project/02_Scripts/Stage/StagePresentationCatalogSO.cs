using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [Flags]
    public enum StagePresentationUsageFlags : byte
    {
        None = 0,
        Standalone = 1 << 0,
        SourceLinked = 1 << 1,
        DepositLinked = 1 << 2,
        ObstacleLinked = 1 << 3,
    }

    [Serializable]
    public struct StagePresentationCatalogEntry
    {
        public string PresentationKey;
        public GameObject Prefab;
        public StagePresentationUsageFlags Usage;
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Presentation Catalog", fileName = "spc_")]
    public class StagePresentationCatalogSO : ScriptableObject
    {
        public int SchemaVersion = 1;
        public StagePresentationCatalogEntry[] Entries;
    }
}
