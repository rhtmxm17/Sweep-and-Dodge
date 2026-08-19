using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum StageMapIssueTargetKind : byte
    {
        Document = 0,
        Cell = 1,
        SourceRegion = 2,
        DepositRegion = 3,
        SourceAnchor = 4,
        DepositAnchor = 5,
        PlayerStart = 6,
        HazardActor = 7,
        Presentation = 8,
        TargetLayout = 9,
        TargetDefinition = 10,
        TargetCatalog = 11,
        PresentationCatalog = 12,
        HazardActorRule = 13,
    }

    public readonly struct StageMapIssueTarget
    {
        public StageMapIssueTarget(
            StageMapIssueTargetKind kind,
            int arrayIndex,
            uint stableId,
            uint owningSourceStableId,
            int placementInstanceId,
            int ruleId,
            bool hasCell,
            Vector2Int cell,
            string fixId,
            UnityEngine.Object asset)
        {
            Kind = kind;
            ArrayIndex = arrayIndex;
            StableId = stableId;
            OwningSourceStableId = owningSourceStableId;
            PlacementInstanceId = placementInstanceId;
            RuleId = ruleId;
            HasCell = hasCell;
            Cell = cell;
            FixId = fixId ?? string.Empty;
            Asset = asset;
        }

        public StageMapIssueTargetKind Kind { get; }
        public int ArrayIndex { get; }
        public uint StableId { get; }
        public uint OwningSourceStableId { get; }
        public int PlacementInstanceId { get; }
        public int RuleId { get; }
        public bool HasCell { get; }
        public Vector2Int Cell { get; }
        public string FixId { get; }
        public UnityEngine.Object Asset { get; }
    }

    public readonly struct StageMapDocumentIssue
    {
        public StageMapDocumentIssue(ContentValidationIssue issue, StageMapIssueTarget target)
        {
            Issue = issue;
            Target = target;
        }

        public ContentValidationIssue Issue { get; }
        public StageMapIssueTarget Target { get; }
    }

    public static class StageMapDocumentIssueMapper
    {
        public static void Map(
            StageMapDocument document,
            IReadOnlyList<ContentValidationIssue> issues,
            List<StageMapDocumentIssue> results)
        {
            if (results == null)
                return;
            results.Clear();
            if (document == null || issues == null)
                return;

            for (int i = 0; i < issues.Count; i++)
                results.Add(new StageMapDocumentIssue(issues[i], ResolveTarget(document, issues[i])));
        }

        public static StageMapIssueTarget ResolveTarget(StageMapDocument document, ContentValidationIssue issue)
        {
            string fixId = StageMapDocumentFixUtility.TryBuildFixPreview(document, issue, out var preview)
                ? preview.FixId
                : string.Empty;

            if (TryParseCellCoordinates(issue.Location, out Vector2Int cell))
                return Target(StageMapIssueTargetKind.Cell, -1, 0u, true, cell, fixId, null);

            if (TryParseArrayIndex(issue.Location, "/HazardActorPlacements[", out int hazardIndex))
            {
                var placement = document.HazardActorPlacements != null
                    && hazardIndex >= 0
                    && hazardIndex < document.HazardActorPlacements.Length
                    ? document.HazardActorPlacements[hazardIndex]
                    : default;
                return Target(
                    StageMapIssueTargetKind.HazardActor,
                    hazardIndex,
                    0u,
                    placement.OwningSourceStableId,
                    placement.PlacementInstanceId,
                    false,
                    default,
                    fixId,
                    null);
            }

            if (TryParseArrayIndex(issue.Location, "/HazardActorOrchestrationRules[", out int ruleIndex))
            {
                var rule = document.HazardActorOrchestrationRules != null
                    && ruleIndex >= 0
                    && ruleIndex < document.HazardActorOrchestrationRules.Length
                    ? document.HazardActorOrchestrationRules[ruleIndex]
                    : default;
                return Target(
                    StageMapIssueTargetKind.HazardActorRule,
                    ruleIndex,
                    0u,
                    rule.OwningSourceStableId,
                    0,
                    rule.RuleId,
                    false,
                    default,
                    fixId,
                    null);
            }

            if (TryParseArrayIndex(issue.Location, "/PresentationLinks[", out int presentationIndex))
            {
                uint stableId = document.PresentationLinks != null
                    && presentationIndex >= 0
                    && presentationIndex < document.PresentationLinks.Length
                    ? document.PresentationLinks[presentationIndex].StableId
                    : 0u;
                return Target(StageMapIssueTargetKind.Presentation, presentationIndex, stableId, false, default, fixId, null);
            }

            if (TryParseArrayIndex(issue.Location, "/SourceRegions[", out int sourceIndex))
            {
                var region = GetRegion(document.SourceRegions, sourceIndex);
                bool anchorIssue = issue.Code == "STG009" || issue.Code == "STG012";
                return Target(
                    anchorIssue ? StageMapIssueTargetKind.SourceAnchor : StageMapIssueTargetKind.SourceRegion,
                    sourceIndex,
                    region.StableId,
                    true,
                    region.AnchorCell,
                    fixId,
                    null);
            }

            if (TryParseArrayIndex(issue.Location, "/DepositRegions[", out int depositIndex))
            {
                var region = GetRegion(document.DepositRegions, depositIndex);
                bool anchorIssue = issue.Code == "STG010" || issue.Code == "STG012";
                return Target(
                    anchorIssue ? StageMapIssueTargetKind.DepositAnchor : StageMapIssueTargetKind.DepositRegion,
                    depositIndex,
                    region.StableId,
                    true,
                    region.AnchorCell,
                    fixId,
                    null);
            }

            if (!string.IsNullOrEmpty(issue.Location) && issue.Location.Contains("/PlayerStart"))
                return Target(StageMapIssueTargetKind.PlayerStart, 0, 0u, true, document.PlayerStart.AnchorCell, fixId, null);

            switch (issue.Code)
            {
                case "SMD900":
                    return Target(StageMapIssueTargetKind.TargetLayout, -1, 0u, false, default, fixId, document.TargetLayout);
                case "SMD901":
                    return Target(StageMapIssueTargetKind.TargetDefinition, -1, 0u, false, default, fixId, document.TargetDefinition);
                case "SMD902":
                    return Target(StageMapIssueTargetKind.TargetCatalog, -1, 0u, false, default, fixId, document.TargetCatalog);
                case "SMD903":
                case "SMD033":
                case "SMD034":
                    return Target(StageMapIssueTargetKind.PresentationCatalog, -1, 0u, false, default, fixId, document.PresentationCatalog);
                default:
                    return Target(StageMapIssueTargetKind.Document, -1, 0u, false, default, fixId, document);
            }
        }

        private static StageMapIssueTarget Target(
            StageMapIssueTargetKind kind,
            int index,
            uint stableId,
            uint owningSourceStableId,
            int placementInstanceId,
            bool hasCell,
            Vector2Int cell,
            string fixId,
            UnityEngine.Object asset)
        {
            return new StageMapIssueTarget(
                kind,
                index,
                stableId,
                owningSourceStableId,
                placementInstanceId,
                0,
                hasCell,
                cell,
                fixId,
                asset);
        }

        private static StageMapIssueTarget Target(
            StageMapIssueTargetKind kind,
            int index,
            uint stableId,
            bool hasCell,
            Vector2Int cell,
            string fixId,
            UnityEngine.Object asset)
        {
            return Target(kind, index, stableId, 0u, 0, hasCell, cell, fixId, asset);
        }

        private static StageMapIssueTarget Target(
            StageMapIssueTargetKind kind,
            int index,
            uint stableId,
            uint owningSourceStableId,
            int placementInstanceId,
            int ruleId,
            bool hasCell,
            Vector2Int cell,
            string fixId,
            UnityEngine.Object asset)
        {
            return new StageMapIssueTarget(
                kind,
                index,
                stableId,
                owningSourceStableId,
                placementInstanceId,
                ruleId,
                hasCell,
                cell,
                fixId,
                asset);
        }

        private static StageMapRegionData GetRegion(StageMapRegionData[] regions, int index)
        {
            return regions != null && index >= 0 && index < regions.Length ? regions[index] : default;
        }

        private static bool TryParseArrayIndex(string text, string prefix, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(text))
                return false;
            int start = text.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
                return false;
            start += prefix.Length;
            int end = text.IndexOf("]", start, StringComparison.Ordinal);
            return end > start && int.TryParse(text.Substring(start, end - start), out index);
        }

        private static bool TryParseCellCoordinates(string text, out Vector2Int cell)
        {
            cell = default;
            if (string.IsNullOrEmpty(text))
                return false;
            int xStart = text.IndexOf("(x=", StringComparison.Ordinal);
            int yStart = text.IndexOf(", y=", StringComparison.Ordinal);
            int end = text.IndexOf(")", yStart >= 0 ? yStart : 0, StringComparison.Ordinal);
            if (xStart < 0 || yStart < 0 || end < 0)
                return false;
            return int.TryParse(text.Substring(xStart + 3, yStart - xStart - 3), out int x)
                && int.TryParse(text.Substring(yStart + 4, end - yStart - 4), out int y)
                && AssignCell(x, y, out cell);
        }

        private static bool AssignCell(int x, int y, out Vector2Int cell)
        {
            cell = new Vector2Int(x, y);
            return true;
        }
    }

    public static class StageMapIssueNavigationUtility
    {
        public static bool TryResolve(
            StageMapDocument document,
            StageMapIssueTarget target,
            out StageMapSelection selection,
            out Vector3 worldPosition,
            out UnityEngine.Object asset)
        {
            selection = StageMapSelection.None;
            worldPosition = document != null ? document.Grid.Origin : Vector3.zero;
            asset = target.Asset;
            if (document == null)
                return asset != null;

            switch (target.Kind)
            {
                case StageMapIssueTargetKind.Cell:
                    if (!StageMapDocumentCommandUtility.TryGetCellIndex(document, target.Cell, out _))
                        return false;
                    selection = StageMapSelection.ForCell(target.Cell);
                    worldPosition = StageMapDocumentCommandUtility.GetCellCenterWorld(document, target.Cell);
                    return true;
                case StageMapIssueTargetKind.SourceRegion:
                    return ResolveRegion(document, target, StageMapSelectionKind.SourceRegion, StageRegionKind.Source, out selection, out worldPosition);
                case StageMapIssueTargetKind.DepositRegion:
                    return ResolveRegion(document, target, StageMapSelectionKind.DepositRegion, StageRegionKind.Deposit, out selection, out worldPosition);
                case StageMapIssueTargetKind.SourceAnchor:
                    return ResolveRegion(document, target, StageMapSelectionKind.SourceAnchor, StageRegionKind.Source, out selection, out worldPosition);
                case StageMapIssueTargetKind.DepositAnchor:
                    return ResolveRegion(document, target, StageMapSelectionKind.DepositAnchor, StageRegionKind.Deposit, out selection, out worldPosition);
                case StageMapIssueTargetKind.PlayerStart:
                    if (!document.PlayerStart.Active)
                        return false;
                    selection = StageMapSelection.ForPlayerStart();
                    worldPosition = StageMapSelectionUtility.GetPlayerStartWorld(document);
                    return true;
                case StageMapIssueTargetKind.HazardActor:
                    uint ownerStableId = target.OwningSourceStableId;
                    int placementInstanceId = target.PlacementInstanceId;
                    if ((ownerStableId == 0u || placementInstanceId <= 0)
                        && document.HazardActorPlacements != null
                        && target.ArrayIndex >= 0
                        && target.ArrayIndex < document.HazardActorPlacements.Length)
                    {
                        var fallback = document.HazardActorPlacements[target.ArrayIndex];
                        ownerStableId = fallback.OwningSourceStableId;
                        placementInstanceId = fallback.PlacementInstanceId;
                    }
                    if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(
                            document.HazardActorPlacements,
                            ownerStableId,
                            placementInstanceId,
                            out int hazardIndex))
                        return false;
                    selection = StageMapSelection.ForHazard(ownerStableId, placementInstanceId);
                    worldPosition = StageMapSelectionUtility.GetHazardActorWorld(document, hazardIndex);
                    return true;
                case StageMapIssueTargetKind.HazardActorRule:
                    uint ruleOwnerStableId = target.OwningSourceStableId;
                    int ruleId = target.RuleId;
                    if ((ruleOwnerStableId == 0u || ruleId <= 0)
                        && document.HazardActorOrchestrationRules != null
                        && target.ArrayIndex >= 0
                        && target.ArrayIndex < document.HazardActorOrchestrationRules.Length)
                    {
                        var fallback = document.HazardActorOrchestrationRules[target.ArrayIndex];
                        ruleOwnerStableId = fallback.OwningSourceStableId;
                        ruleId = fallback.RuleId;
                    }
                    if (!StageMapHazardActorOrchestrationUtility.TryFindRuleIndex(
                            document,
                            ruleOwnerStableId,
                            ruleId,
                            out int ruleIndex))
                    {
                        return false;
                    }
                    selection = StageMapSelection.ForHazardRule(ruleOwnerStableId, ruleId);
                    worldPosition = StageMapSelectionUtility.GetHazardRuleWorld(document, document.HazardActorOrchestrationRules[ruleIndex]);
                    return true;
                case StageMapIssueTargetKind.Presentation:
                    if (!StageMapSelectionUtility.TryFindUniquePresentationIndex(
                            document.PresentationLinks,
                            target.StableId,
                            out int presentationIndex))
                        return false;
                    selection = StageMapSelection.ForPresentation(target.StableId);
                    worldPosition = StageMapSelectionUtility.GetPresentationWorld(document, presentationIndex);
                    return true;
                case StageMapIssueTargetKind.Document:
                    selection = StageMapSelection.ForDocument(document);
                    asset = document;
                    return true;
                default:
                    selection = StageMapSelection.ForTargetAsset(asset);
                    return asset != null;
            }
        }

        private static bool ResolveRegion(
            StageMapDocument document,
            StageMapIssueTarget target,
            StageMapSelectionKind selectionKind,
            StageRegionKind regionKind,
            out StageMapSelection selection,
            out Vector3 worldPosition)
        {
            var regions = regionKind == StageRegionKind.Source ? document.SourceRegions : document.DepositRegions;
            if (!StageMapSelectionUtility.TryFindUniqueRegionIndex(regions, target.StableId, out int regionIndex))
            {
                selection = StageMapSelection.None;
                worldPosition = document.Grid.Origin;
                return false;
            }

            var region = regions[regionIndex];
            selection = selectionKind == StageMapSelectionKind.SourceAnchor
                || selectionKind == StageMapSelectionKind.DepositAnchor
                ? StageMapSelection.ForAnchor(regionKind, region.StableId)
                : StageMapSelection.ForRegion(regionKind, region.StableId);
            worldPosition = StageMapSelectionUtility.GetRegionAnchorWorld(document, regionKind, region.StableId);
            return true;
        }
    }
}
