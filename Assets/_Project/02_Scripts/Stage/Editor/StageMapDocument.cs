using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [Serializable]
    public struct StageMapRegionData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector2Int AnchorCell;
        public Vector2 AnchorOffset;
    }

    [Serializable]
    public struct StageMapHazardActorPlacementData
    {
        [Min(1)] public uint OwningSourceStableId;
        [Min(1)] public int PlacementInstanceId;
        public GameObject ActorArchetypePrefab;
        public Vector3 SourceLocalOffset;
        public float LocalYawDeg;
    }

    [Serializable]
    public struct StageMapPresentationLinkData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public StagePresentationPlacementMode PlacementMode;
        public StagePresentationLinkKind LinkKind;
        [Min(0)] public uint LinkedStableId;
        public string PresentationKey;
        public Vector3 Position;
        public Vector3 Euler;
        public Vector3 Scale;
    }

    /// <summary>
    /// Editor-only authoring SSOT for the replacement Stage Map Editor.
    /// Runtime systems continue to read generated StageLayoutSO, StageDefinitionSO, and StageCatalogSO assets.
    /// </summary>
    [CreateAssetMenu(menuName = "SweepNDodge/Stage Map Editor/Stage Map Document", fileName = "smd_")]
    public sealed class StageMapDocument : ScriptableObject
    {
        public const int CurrentSchemaVersion = 2;

        [Min(1)] public int SchemaVersion = CurrentSchemaVersion;
        [Min(1)] public int StageId = 1;
        public string DisplayName;
        public bool IsFinalStage;
        [Min(0.01f)] public float StageTimeLimitSec = 150f;

        public StageGridSpec Grid;
        public StageCellLayoutData[] Cells = Array.Empty<StageCellLayoutData>();
        public string[] VisualTileKeys = Array.Empty<string>();
        public StageMapRegionData[] SourceRegions = Array.Empty<StageMapRegionData>();
        public StageMapRegionData[] DepositRegions = Array.Empty<StageMapRegionData>();
        public StagePlayerStartLayoutData PlayerStart;
        public StageMapHazardActorPlacementData[] HazardActorPlacements = Array.Empty<StageMapHazardActorPlacementData>();
        public StageMapPresentationLinkData[] PresentationLinks = Array.Empty<StageMapPresentationLinkData>();

        [Header("Generated Runtime Assets")]
        public StageLayoutSO TargetLayout;
        public StageDefinitionSO TargetDefinition;
        public StageCatalogSO TargetCatalog;
        public StagePresentationCatalogSO PresentationCatalog;
        public bool IncludeInCatalog = true;
        public bool EnabledInCatalog = true;
        public string CatalogEntryKey;

        [SerializeField, HideInInspector]
        private string _lastAppliedCatalogEntryKey;

        public string LastAppliedCatalogEntryKey => _lastAppliedCatalogEntryKey ?? string.Empty;

        internal void SetLastAppliedCatalogEntryKey(string entryKey)
        {
            _lastAppliedCatalogEntryKey = entryKey != null ? entryKey.Trim() : string.Empty;
        }
    }
}
