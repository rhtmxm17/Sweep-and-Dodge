using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum StageMapEditorToolMode : byte
    {
        Select = 0,
        PaintMovement = 1,
        PaintRegion = 2,
        PlaceAnchor = 3,
        PlacePlayerStart = 4,
        PlaceHazardActor = 5,
        PlacePresentationLink = 6,
    }

    public enum StageMapEditorLayer : byte
    {
        Grid = 0,
        Movement = 1,
        Source = 2,
        Deposit = 3,
        Anchors = 4,
        PlayerStart = 5,
        HazardActors = 6,
        Presentations = 7,
    }

    public enum StageMapSelectionKind : byte
    {
        None = 0,
        Cell = 1,
        SourceRegion = 2,
        DepositRegion = 3,
        SourceAnchor = 4,
        DepositAnchor = 5,
        PlayerStart = 6,
        HazardActor = 7,
        Presentation = 8,
        Document = 9,
        TargetAsset = 10,
        HazardActorRule = 11,
    }

    public enum StageMapInspectorSection : byte
    {
        None = 0,
        Cell = 1,
        RegionOrAnchor = 2,
        PlayerStart = 3,
        HazardActor = 4,
        Presentation = 5,
        Document = 6,
        TargetAsset = 7,
        HazardActorRule = 8,
    }

    /// <summary>
    /// Canonical logical selection identity. Array indices are resolved from the current document on demand.
    /// </summary>
    public readonly struct StageMapSelection : IEquatable<StageMapSelection>
    {
        private StageMapSelection(
            StageMapSelectionKind kind,
            Vector2Int cell,
            uint stableId,
            uint owningSourceStableId,
            int placementInstanceId,
            int ruleId,
            UnityEngine.Object targetAsset)
        {
            Kind = kind;
            Cell = cell;
            StableId = stableId;
            OwningSourceStableId = owningSourceStableId;
            PlacementInstanceId = placementInstanceId;
            RuleId = ruleId;
            TargetAsset = targetAsset;
        }

        public StageMapSelectionKind Kind { get; }
        public Vector2Int Cell { get; }
        public uint StableId { get; }
        public uint OwningSourceStableId { get; }
        public int PlacementInstanceId { get; }
        public int RuleId { get; }
        public UnityEngine.Object TargetAsset { get; }

        public static StageMapSelection None => default;
        public static StageMapSelection ForCell(Vector2Int cell) =>
            new StageMapSelection(StageMapSelectionKind.Cell, cell, 0u, 0u, 0, 0, null);

        public static StageMapSelection ForRegion(StageRegionKind kind, uint stableId) =>
            new StageMapSelection(
                kind == StageRegionKind.Source ? StageMapSelectionKind.SourceRegion : StageMapSelectionKind.DepositRegion,
                default,
                stableId,
                0u,
                0,
                0,
                null);

        public static StageMapSelection ForAnchor(StageRegionKind kind, uint stableId) =>
            new StageMapSelection(
                kind == StageRegionKind.Source ? StageMapSelectionKind.SourceAnchor : StageMapSelectionKind.DepositAnchor,
                default,
                stableId,
                0u,
                0,
                0,
                null);

        public static StageMapSelection ForPlayerStart() =>
            new StageMapSelection(StageMapSelectionKind.PlayerStart, default, 0u, 0u, 0, 0, null);

        public static StageMapSelection ForHazard(uint owningSourceStableId, int placementInstanceId) =>
            new StageMapSelection(StageMapSelectionKind.HazardActor, default, 0u, owningSourceStableId, placementInstanceId, 0, null);

        public static StageMapSelection ForHazardRule(uint owningSourceStableId, int ruleId) =>
            new StageMapSelection(StageMapSelectionKind.HazardActorRule, default, 0u, owningSourceStableId, 0, ruleId, null);

        public static StageMapSelection ForPresentation(uint stableId) =>
            new StageMapSelection(StageMapSelectionKind.Presentation, default, stableId, 0u, 0, 0, null);

        public static StageMapSelection ForDocument(StageMapDocument document) =>
            new StageMapSelection(StageMapSelectionKind.Document, default, 0u, 0u, 0, 0, document);

        public static StageMapSelection ForTargetAsset(UnityEngine.Object targetAsset) =>
            new StageMapSelection(StageMapSelectionKind.TargetAsset, default, 0u, 0u, 0, 0, targetAsset);

        public bool Equals(StageMapSelection other)
        {
            return Kind == other.Kind
                && Cell == other.Cell
                && StableId == other.StableId
                && OwningSourceStableId == other.OwningSourceStableId
                && PlacementInstanceId == other.PlacementInstanceId
                && RuleId == other.RuleId
                && TargetAsset == other.TargetAsset;
        }

        public override bool Equals(object obj) => obj is StageMapSelection other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Cell.GetHashCode();
                hash = (hash * 397) ^ (int)StableId;
                hash = (hash * 397) ^ (int)OwningSourceStableId;
                hash = (hash * 397) ^ PlacementInstanceId;
                hash = (hash * 397) ^ RuleId;
                hash = (hash * 397) ^ (TargetAsset != null ? TargetAsset.GetInstanceID() : 0);
                return hash;
            }
        }

        public static bool operator ==(StageMapSelection left, StageMapSelection right) => left.Equals(right);
        public static bool operator !=(StageMapSelection left, StageMapSelection right) => !left.Equals(right);
    }

    internal sealed class StageMapEditingSessionUndoState : ScriptableObject
    {
        public bool CenterRegionAnchors;
        public bool CenterPlayerStart;
    }

    public sealed class StageMapEditingSession : IDisposable
    {
        private StageMapEditingSessionUndoState _undoState;

        public StageMapDocument Document { get; private set; }
        public StageMapEditorToolMode SelectedTool { get; set; }
        public StageMapEditorLayer SelectedLayer { get; set; }
        public int SelectedIssueIndex { get; set; } = -1;
        public StageMapSelection Selection { get; private set; } = StageMapSelection.None;
        public StageCellMovementFlags MovementBrush { get; set; } = StageCellMovementFlags.None;
        public StageRegionKind RegionBrushKind { get; set; } = StageRegionKind.Source;
        public uint RegionBrushStableId { get; set; } = 1u;
        public StageRegionKind AnchorBrushKind { get; set; } = StageRegionKind.Source;
        public uint AnchorBrushStableId { get; set; } = 1u;
        public float PlayerStartYawDeg { get; set; }
        public uint HazardActorSourceStableId { get; set; } = 1u;
        public GameObject HazardActorArchetypePrefab { get; set; }
        public float HazardActorLocalYawDeg { get; set; }
        public bool PinHazardEncounterSource { get; set; }
        public uint PinnedHazardEncounterSourceStableId { get; set; }
        public uint PresentationStableId { get; set; } = 1u;
        public string PresentationKey { get; set; } = string.Empty;
        public StagePresentationPlacementMode PresentationPlacementMode { get; set; } = StagePresentationPlacementMode.Standalone;
        public StagePresentationLinkKind PresentationLinkKind { get; set; } = StagePresentationLinkKind.None;
        public uint PresentationLinkedStableId { get; set; }
        public Vector3 PresentationEuler { get; set; }
        public Vector3 PresentationScale { get; set; } = Vector3.one;
        public bool ShowGridLayer { get; set; } = true;
        public bool ShowMovementLayer { get; set; } = true;
        public bool ShowSourceLayer { get; set; } = true;
        public bool ShowDepositLayer { get; set; } = true;
        public bool ShowAnchorLayer { get; set; } = true;
        public bool ShowPlayerStartLayer { get; set; } = true;
        public bool ShowHazardActorLayer { get; set; } = true;
        public bool ShowPresentationLayer { get; set; } = true;
        public bool LockGridLayer { get; set; }
        public bool LockMovementLayer { get; set; }
        public bool LockSourceLayer { get; set; }
        public bool LockDepositLayer { get; set; }
        public bool LockAnchorLayer { get; set; }
        public bool LockPlayerStartLayer { get; set; }
        public bool LockHazardActorLayer { get; set; }
        public bool LockPresentationLayer { get; set; }
        public bool Dirty { get; set; }
        public List<ContentValidationIssue> ValidationSnapshot { get; } = new List<ContentValidationIssue>(32);

        public bool CenterRegionAnchors
        {
            get => GetUndoState().CenterRegionAnchors;
            internal set => GetUndoState().CenterRegionAnchors = value;
        }

        public bool CenterPlayerStart
        {
            get => GetUndoState().CenterPlayerStart;
            internal set => GetUndoState().CenterPlayerStart = value;
        }

        public UnityEngine.Object UndoTarget => GetUndoState();

        public void Load(StageMapDocument document)
        {
            Document = document;
            SelectedTool = StageMapEditorToolMode.Select;
            SelectedLayer = StageMapEditorLayer.Grid;
            SelectedIssueIndex = -1;
            Selection = StageMapSelection.None;
            PresentationStableId = StageMapDocumentCommandUtility.GetNextPresentationStableId(document);
            Dirty = false;
            ValidationSnapshot.Clear();
        }

        public void Select(StageMapSelection selection)
        {
            Selection = selection;
        }

        public StageMapInspectorSection GetInspectorSection()
        {
            switch (Selection.Kind)
            {
                case StageMapSelectionKind.Cell: return StageMapInspectorSection.Cell;
                case StageMapSelectionKind.SourceRegion:
                case StageMapSelectionKind.DepositRegion:
                case StageMapSelectionKind.SourceAnchor:
                case StageMapSelectionKind.DepositAnchor:
                    return StageMapInspectorSection.RegionOrAnchor;
                case StageMapSelectionKind.PlayerStart: return StageMapInspectorSection.PlayerStart;
                case StageMapSelectionKind.HazardActor: return StageMapInspectorSection.HazardActor;
                case StageMapSelectionKind.HazardActorRule: return StageMapInspectorSection.HazardActorRule;
                case StageMapSelectionKind.Presentation: return StageMapInspectorSection.Presentation;
                case StageMapSelectionKind.Document: return StageMapInspectorSection.Document;
                case StageMapSelectionKind.TargetAsset: return StageMapInspectorSection.TargetAsset;
                default: return StageMapInspectorSection.None;
            }
        }

        public void ReconcileSelection(StageMapDocument document)
        {
            if (document == null || document != Document)
            {
                Select(StageMapSelection.None);
                return;
            }

            StageMapSelection selection = Selection;
            switch (selection.Kind)
            {
                case StageMapSelectionKind.None:
                    return;
                case StageMapSelectionKind.Cell:
                    if (!StageMapDocumentCommandUtility.TryGetCellIndex(document, selection.Cell, out int cellIndex)
                        || document.Cells == null
                        || cellIndex >= document.Cells.Length)
                    {
                        Select(StageMapSelection.None);
                    }
                    return;
                case StageMapSelectionKind.SourceRegion:
                case StageMapSelectionKind.SourceAnchor:
                    if (!StageMapSelectionUtility.TryFindUniqueRegionIndex(document.SourceRegions, selection.StableId, out _))
                        Select(StageMapSelection.None);
                    return;
                case StageMapSelectionKind.DepositRegion:
                case StageMapSelectionKind.DepositAnchor:
                    if (!StageMapSelectionUtility.TryFindUniqueRegionIndex(document.DepositRegions, selection.StableId, out _))
                        Select(StageMapSelection.None);
                    return;
                case StageMapSelectionKind.PlayerStart:
                    if (!document.PlayerStart.Active)
                        Select(StageMapSelection.None);
                    return;
                case StageMapSelectionKind.HazardActor:
                    if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(
                            document.HazardActorPlacements,
                            selection.OwningSourceStableId,
                            selection.PlacementInstanceId,
                            out _))
                    {
                        Select(StageMapSelection.None);
                    }
                    return;
                case StageMapSelectionKind.HazardActorRule:
                    if (!StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(
                            document,
                            selection.OwningSourceStableId,
                            selection.RuleId,
                            out _))
                    {
                        Select(StageMapSelection.None);
                    }
                    return;
                case StageMapSelectionKind.Presentation:
                    if (!StageMapSelectionUtility.TryFindUniquePresentationIndex(document.PresentationLinks, selection.StableId, out _))
                        Select(StageMapSelection.None);
                    return;
                case StageMapSelectionKind.Document:
                    if (selection.TargetAsset != document)
                        Select(StageMapSelection.None);
                    return;
                case StageMapSelectionKind.TargetAsset:
                    if (!IsDocumentTarget(document, selection.TargetAsset))
                        Select(StageMapSelection.None);
                    return;
            }
        }

        public void Dispose()
        {
            if (_undoState == null)
                return;
            UnityEngine.Object.DestroyImmediate(_undoState);
            _undoState = null;
        }

        private StageMapEditingSessionUndoState GetUndoState()
        {
            if (_undoState != null)
                return _undoState;
            _undoState = ScriptableObject.CreateInstance<StageMapEditingSessionUndoState>();
            _undoState.hideFlags = HideFlags.HideAndDontSave;
            return _undoState;
        }

        private static bool IsDocumentTarget(StageMapDocument document, UnityEngine.Object target)
        {
            return target != null
                && (target == document.TargetLayout
                    || target == document.TargetDefinition
                    || target == document.TargetCatalog
                    || target == document.PresentationCatalog);
        }
    }
}
