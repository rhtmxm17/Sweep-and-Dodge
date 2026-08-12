using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum StageMapRightPanelTab : byte
    {
        Selection = 0,
        Issues = 1,
        Diff = 2,
    }

    public enum StageMapSelectionCategory : byte
    {
        Regions = 0,
        Hazards = 1,
        Rules = 2,
        Links = 3,
    }

    public enum StageMapTableRowAction : byte
    {
        None = 0,
        Select = 1,
        SelectAndFrame = 2,
    }

    public readonly struct StageMapLayerUiState
    {
        public StageMapLayerUiState(bool active, bool visible, bool locked)
        {
            Active = active;
            Visible = visible;
            Locked = locked;
        }

        public bool Active { get; }
        public bool Visible { get; }
        public bool Locked { get; }
    }

    public static class StageMapEditorUiPolicy
    {
        public const float DefaultRightPanelWidth = 520f;
        public const float MinRightPanelWidth = 400f;
        public const float MaxRightPanelWidth = 720f;
        public const float MinDocumentPanelWidth = 360f;
        public const float SplitterWidth = 4f;

        public static StageCellMovementFlags GetMovementPreset(int index)
        {
            switch (index)
            {
                case 0: return StageCellMovementFlags.None;
                case 1: return StageCellMovementFlags.BlockPlayer;
                case 2: return StageCellMovementFlags.BlockBullet;
                case 3: return StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet;
                default: return StageCellMovementFlags.None;
            }
        }

        public static int GetMovementPresetIndex(StageCellMovementFlags flags)
        {
            StageCellMovementFlags supported = StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet;
            switch (flags & supported)
            {
                case StageCellMovementFlags.BlockPlayer: return 1;
                case StageCellMovementFlags.BlockBullet: return 2;
                case StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet: return 3;
                default: return 0;
            }
        }

        public static float ClampRightPanelWidth(float requestedWidth, float windowWidth)
        {
            float available = Mathf.Max(0f, windowWidth - MinDocumentPanelWidth - SplitterWidth);
            float upper = Mathf.Min(MaxRightPanelWidth, available);
            if (upper < MinRightPanelWidth)
                return upper;
            return Mathf.Clamp(requestedWidth, MinRightPanelWidth, upper);
        }

        public static StageMapTableRowAction GetRowAction(int mouseButton, int clickCount)
        {
            if (mouseButton != 0 || clickCount <= 0)
                return StageMapTableRowAction.None;
            return clickCount >= 2
                ? StageMapTableRowAction.SelectAndFrame
                : StageMapTableRowAction.Select;
        }

        public static bool CanEraseStableId(StageMapEditorToolMode tool)
        {
            return tool == StageMapEditorToolMode.PaintRegion;
        }

        public static StageMapLayerUiState GetLayerState(StageMapEditingSession session, StageMapEditorLayer layer)
        {
            if (session == null)
                return default;
            return new StageMapLayerUiState(
                session.SelectedLayer == layer,
                GetLayerVisible(session, layer),
                StageMapEditingPolicy.IsLayerLocked(session, layer));
        }

        public static void SetLayerState(
            StageMapEditingSession session,
            StageMapEditorLayer layer,
            bool active,
            bool visible,
            bool locked)
        {
            if (session == null)
                return;
            if (active)
                session.SelectedLayer = layer;
            SetLayerVisible(session, layer, visible);
            SetLayerLocked(session, layer, locked);
        }

        private static bool GetLayerVisible(StageMapEditingSession session, StageMapEditorLayer layer)
        {
            switch (layer)
            {
                case StageMapEditorLayer.Grid: return session.ShowGridLayer;
                case StageMapEditorLayer.Movement: return session.ShowMovementLayer;
                case StageMapEditorLayer.Source: return session.ShowSourceLayer;
                case StageMapEditorLayer.Deposit: return session.ShowDepositLayer;
                case StageMapEditorLayer.Anchors: return session.ShowAnchorLayer;
                case StageMapEditorLayer.PlayerStart: return session.ShowPlayerStartLayer;
                case StageMapEditorLayer.HazardActors: return session.ShowHazardActorLayer;
                case StageMapEditorLayer.Presentations: return session.ShowPresentationLayer;
                default: return false;
            }
        }

        private static void SetLayerVisible(StageMapEditingSession session, StageMapEditorLayer layer, bool value)
        {
            switch (layer)
            {
                case StageMapEditorLayer.Grid: session.ShowGridLayer = value; break;
                case StageMapEditorLayer.Movement: session.ShowMovementLayer = value; break;
                case StageMapEditorLayer.Source: session.ShowSourceLayer = value; break;
                case StageMapEditorLayer.Deposit: session.ShowDepositLayer = value; break;
                case StageMapEditorLayer.Anchors: session.ShowAnchorLayer = value; break;
                case StageMapEditorLayer.PlayerStart: session.ShowPlayerStartLayer = value; break;
                case StageMapEditorLayer.HazardActors: session.ShowHazardActorLayer = value; break;
                case StageMapEditorLayer.Presentations: session.ShowPresentationLayer = value; break;
            }
        }

        private static void SetLayerLocked(StageMapEditingSession session, StageMapEditorLayer layer, bool value)
        {
            switch (layer)
            {
                case StageMapEditorLayer.Grid: session.LockGridLayer = value; break;
                case StageMapEditorLayer.Movement: session.LockMovementLayer = value; break;
                case StageMapEditorLayer.Source: session.LockSourceLayer = value; break;
                case StageMapEditorLayer.Deposit: session.LockDepositLayer = value; break;
                case StageMapEditorLayer.Anchors: session.LockAnchorLayer = value; break;
                case StageMapEditorLayer.PlayerStart: session.LockPlayerStartLayer = value; break;
                case StageMapEditorLayer.HazardActors: session.LockHazardActorLayer = value; break;
                case StageMapEditorLayer.Presentations: session.LockPresentationLayer = value; break;
            }
        }
    }

    public readonly struct StageMapRegionTableRow
    {
        public StageMapRegionTableRow(StageRegionKind kind, StageMapRegionData region, int cellCount, int issueCount)
        {
            Kind = kind;
            StableId = region.StableId;
            KindLabel = kind.ToString();
            IdLabel = region.StableId.ToString();
            ActiveLabel = region.Active ? "Yes" : "No";
            AnchorCellLabel = $"{region.AnchorCell.x}, {region.AnchorCell.y}";
            CellCountLabel = cellCount.ToString();
            IssueLabel = issueCount > 0 ? issueCount.ToString() : "-";
            RegionSelection = StageMapSelection.ForRegion(kind, region.StableId);
            AnchorSelection = StageMapSelection.ForAnchor(kind, region.StableId);
        }

        public StageRegionKind Kind { get; }
        public uint StableId { get; }
        public string KindLabel { get; }
        public string IdLabel { get; }
        public string ActiveLabel { get; }
        public string AnchorCellLabel { get; }
        public string CellCountLabel { get; }
        public string IssueLabel { get; }
        public StageMapSelection RegionSelection { get; }
        public StageMapSelection AnchorSelection { get; }
    }

    public readonly struct StageMapHazardTableRow
    {
        public StageMapHazardTableRow(StageMapHazardActorPlacementData placement, int issueCount)
        {
            SourceLabel = placement.OwningSourceStableId.ToString();
            PlacementLabel = placement.PlacementInstanceId.ToString();
            ArchetypeLabel = placement.ActorArchetypePrefab != null ? placement.ActorArchetypePrefab.name : "(missing)";
            YawLabel = $"{placement.LocalYawDeg:0.##}°";
            IssueLabel = issueCount > 0 ? issueCount.ToString() : "-";
            Selection = StageMapSelection.ForHazard(placement.OwningSourceStableId, placement.PlacementInstanceId);
        }

        public string SourceLabel { get; }
        public string PlacementLabel { get; }
        public string ArchetypeLabel { get; }
        public string YawLabel { get; }
        public string IssueLabel { get; }
        public StageMapSelection Selection { get; }
    }

    public readonly struct StageMapRuleTableRow
    {
        public StageMapRuleTableRow(StageMapHazardActorOrchestrationRuleData rule, int issueCount)
        {
            SourceLabel = rule.OwningSourceStableId.ToString();
            RuleLabel = rule.RuleId.ToString();
            ActionLabel = rule.ActionType.ToString();
            TriggerLabel = rule.TriggerType == HazardActorOrchestrationTriggerId.OnStageStart
                ? "Stage Start"
                : $"{rule.TriggerType} {rule.TriggerThresholdNormalized:0.###}";
            TargetLabel = rule.TargetPlacementInstanceIds == null || rule.TargetPlacementInstanceIds.Length == 0
                ? "-"
                : string.Join(",", rule.TargetPlacementInstanceIds);
            IssueLabel = issueCount > 0 ? issueCount.ToString() : "-";
            Selection = StageMapSelection.ForHazardRule(rule.OwningSourceStableId, rule.RuleId);
        }

        public string SourceLabel { get; }
        public string RuleLabel { get; }
        public string ActionLabel { get; }
        public string TriggerLabel { get; }
        public string TargetLabel { get; }
        public string IssueLabel { get; }
        public StageMapSelection Selection { get; }
    }

    public readonly struct StageMapLinkTableRow
    {
        public StageMapLinkTableRow(StageMapPresentationLinkData link, int issueCount)
        {
            IdLabel = link.StableId.ToString();
            KeyLabel = string.IsNullOrWhiteSpace(link.PresentationKey) ? "(empty)" : link.PresentationKey;
            ModeLabel = link.PlacementMode.ToString();
            LinkedTargetLabel = link.PlacementMode == StagePresentationPlacementMode.LinkedToParent
                ? $"{link.LinkKind} {link.LinkedStableId}"
                : "-";
            ActiveLabel = link.Active ? "Yes" : "No";
            IssueLabel = issueCount > 0 ? issueCount.ToString() : "-";
            Selection = StageMapSelection.ForPresentation(link.StableId);
        }

        public string IdLabel { get; }
        public string KeyLabel { get; }
        public string ModeLabel { get; }
        public string LinkedTargetLabel { get; }
        public string ActiveLabel { get; }
        public string IssueLabel { get; }
        public StageMapSelection Selection { get; }
    }

    public readonly struct StageMapIssueTableRow
    {
        public StageMapIssueTableRow(int index, ContentValidationIssue issue, StageMapIssueTarget target)
        {
            Index = index;
            SeverityLabel = issue.Severity.ToString();
            CodeLabel = issue.Code;
            TargetLabel = BuildTargetLabel(target);
            MessageLabel = Shorten(issue.Message, 72);
            HasFix = !string.IsNullOrEmpty(target.FixId);
        }

        public int Index { get; }
        public string SeverityLabel { get; }
        public string CodeLabel { get; }
        public string TargetLabel { get; }
        public string MessageLabel { get; }
        public bool HasFix { get; }

        private static string BuildTargetLabel(StageMapIssueTarget target)
        {
            switch (target.Kind)
            {
                case StageMapIssueTargetKind.Cell: return $"Cell {target.Cell.x},{target.Cell.y}";
                case StageMapIssueTargetKind.SourceRegion:
                case StageMapIssueTargetKind.DepositRegion:
                case StageMapIssueTargetKind.SourceAnchor:
                case StageMapIssueTargetKind.DepositAnchor: return $"{target.Kind} {target.StableId}";
                case StageMapIssueTargetKind.HazardActor: return $"S{target.OwningSourceStableId}/P{target.PlacementInstanceId}";
                case StageMapIssueTargetKind.HazardActorRule: return $"S{target.OwningSourceStableId}/R{target.RuleId}";
                case StageMapIssueTargetKind.Presentation: return $"Link {target.StableId}";
                default: return target.Kind.ToString();
            }
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value ?? string.Empty;
            return value.Substring(0, maxLength - 1) + "…";
        }
    }

    public readonly struct StageMapDiffTableRow
    {
        public StageMapDiffTableRow(int index, StageMapApplyPlanChange change)
        {
            Index = index;
            KindLabel = change.Kind.ToString();
            TargetLabel = change.Target;
            FieldLabel = change.Field;
            SummaryLabel = string.IsNullOrEmpty(change.Description) || change.Description.Length <= 72
                ? change.Description
                : change.Description.Substring(0, 71) + "…";
        }

        public int Index { get; }
        public string KindLabel { get; }
        public string TargetLabel { get; }
        public string FieldLabel { get; }
        public string SummaryLabel { get; }
    }

    public sealed class StageMapEditorTableCache
    {
        private readonly List<StageMapRegionTableRow> _regions = new List<StageMapRegionTableRow>();
        private readonly List<StageMapHazardTableRow> _hazards = new List<StageMapHazardTableRow>();
        private readonly List<StageMapRuleTableRow> _rules = new List<StageMapRuleTableRow>();
        private readonly List<StageMapLinkTableRow> _links = new List<StageMapLinkTableRow>();
        private readonly List<StageMapIssueTableRow> _issues = new List<StageMapIssueTableRow>();
        private readonly List<StageMapDiffTableRow> _diffs = new List<StageMapDiffTableRow>();
        private readonly List<uint> _sourceIds = new List<uint>();
        private readonly List<uint> _depositIds = new List<uint>();
        private bool _dirty = true;

        public IReadOnlyList<StageMapRegionTableRow> Regions => _regions;
        public IReadOnlyList<StageMapHazardTableRow> Hazards => _hazards;
        public IReadOnlyList<StageMapRuleTableRow> Rules => _rules;
        public IReadOnlyList<StageMapLinkTableRow> Links => _links;
        public IReadOnlyList<StageMapIssueTableRow> Issues => _issues;
        public IReadOnlyList<StageMapDiffTableRow> Diffs => _diffs;
        public IReadOnlyList<uint> SourceIds => _sourceIds;
        public IReadOnlyList<uint> DepositIds => _depositIds;
        public int BuildCount { get; private set; }

        public void Invalidate()
        {
            _dirty = true;
        }

        public void EnsureBuilt(
            StageMapDocument document,
            IReadOnlyList<StageMapDocumentIssue> documentIssues,
            StageMapApplyPlan applyPlan)
        {
            if (!_dirty)
                return;
            Rebuild(document, documentIssues, applyPlan);
        }

        private void Rebuild(
            StageMapDocument document,
            IReadOnlyList<StageMapDocumentIssue> documentIssues,
            StageMapApplyPlan applyPlan)
        {
            _regions.Clear();
            _hazards.Clear();
            _rules.Clear();
            _links.Clear();
            _issues.Clear();
            _diffs.Clear();
            _sourceIds.Clear();
            _depositIds.Clear();
            BuildCount++;
            _dirty = false;
            if (document == null)
                return;

            AddRegions(document, StageRegionKind.Source, document.SourceRegions, documentIssues, _sourceIds);
            AddRegions(document, StageRegionKind.Deposit, document.DepositRegions, documentIssues, _depositIds);

            var hazards = document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
            for (int i = 0; i < hazards.Length; i++)
            {
                var hazard = hazards[i];
                _hazards.Add(new StageMapHazardTableRow(
                    hazard,
                    CountIssues(documentIssues, StageMapIssueTargetKind.HazardActor, 0u, hazard.OwningSourceStableId, hazard.PlacementInstanceId, 0)));
            }

            var rules = document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                _rules.Add(new StageMapRuleTableRow(
                    rule,
                    CountIssues(documentIssues, StageMapIssueTargetKind.HazardActorRule, 0u, rule.OwningSourceStableId, 0, rule.RuleId)));
            }

            var links = document.PresentationLinks ?? Array.Empty<StageMapPresentationLinkData>();
            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                _links.Add(new StageMapLinkTableRow(
                    link,
                    CountIssues(documentIssues, StageMapIssueTargetKind.Presentation, link.StableId, 0u, 0, 0)));
            }

            if (documentIssues != null)
            {
                for (int i = 0; i < documentIssues.Count; i++)
                    _issues.Add(new StageMapIssueTableRow(i, documentIssues[i].Issue, documentIssues[i].Target));
            }

            if (applyPlan != null)
            {
                for (int i = 0; i < applyPlan.Changes.Count; i++)
                    _diffs.Add(new StageMapDiffTableRow(i, applyPlan.Changes[i]));
            }
        }

        private void AddRegions(
            StageMapDocument document,
            StageRegionKind kind,
            StageMapRegionData[] regions,
            IReadOnlyList<StageMapDocumentIssue> documentIssues,
            List<uint> ids)
        {
            regions = regions ?? Array.Empty<StageMapRegionData>();
            for (int i = 0; i < regions.Length; i++)
            {
                var region = regions[i];
                if (!ids.Contains(region.StableId))
                    ids.Add(region.StableId);
                int cells = CountRegionCells(document, kind, region.StableId);
                int issues = CountRegionIssues(documentIssues, kind, region.StableId);
                _regions.Add(new StageMapRegionTableRow(kind, region, cells, issues));
            }
        }

        private static int CountRegionCells(StageMapDocument document, StageRegionKind kind, uint stableId)
        {
            int count = 0;
            var cells = document.Cells ?? Array.Empty<StageCellLayoutData>();
            for (int i = 0; i < cells.Length; i++)
            {
                uint value = kind == StageRegionKind.Source ? cells[i].SourceRegionId : cells[i].DepositRegionId;
                if (value == stableId)
                    count++;
            }
            return count;
        }

        private static int CountRegionIssues(
            IReadOnlyList<StageMapDocumentIssue> issues,
            StageRegionKind kind,
            uint stableId)
        {
            if (issues == null)
                return 0;
            StageMapIssueTargetKind regionKind = kind == StageRegionKind.Source
                ? StageMapIssueTargetKind.SourceRegion
                : StageMapIssueTargetKind.DepositRegion;
            StageMapIssueTargetKind anchorKind = kind == StageRegionKind.Source
                ? StageMapIssueTargetKind.SourceAnchor
                : StageMapIssueTargetKind.DepositAnchor;
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                var target = issues[i].Target;
                if (target.StableId == stableId && (target.Kind == regionKind || target.Kind == anchorKind))
                    count++;
            }
            return count;
        }

        private static int CountIssues(
            IReadOnlyList<StageMapDocumentIssue> issues,
            StageMapIssueTargetKind kind,
            uint stableId,
            uint owningSourceStableId,
            int placementId,
            int ruleId)
        {
            if (issues == null)
                return 0;
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                var target = issues[i].Target;
                if (target.Kind != kind)
                    continue;
                if (stableId != 0u && target.StableId != stableId)
                    continue;
                if (owningSourceStableId != 0u && target.OwningSourceStableId != owningSourceStableId)
                    continue;
                if (placementId != 0 && target.PlacementInstanceId != placementId)
                    continue;
                if (ruleId != 0 && target.RuleId != ruleId)
                    continue;
                count++;
            }
            return count;
        }
    }
}
