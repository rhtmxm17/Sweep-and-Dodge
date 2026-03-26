using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageGridLayoutValidationRules
    {
        public static void ValidateLayoutRecords(IReadOnlyList<ContentValidationRecord<StageLayoutSO>> layouts, List<ContentValidationIssue> issues)
        {
            if (layouts == null || issues == null)
                return;

            for (int i = 0; i < layouts.Count; i++)
            {
                var record = layouts[i];
                if (record.Value == null)
                    continue;

                ValidateLayout(record.Value, record.Location, issues);
            }
        }

        public static void ValidateLayout(StageLayoutSO layout, string locationPrefix, List<ContentValidationIssue> issues)
        {
            if (layout == null || issues == null)
                return;

            string stageLocation = BuildStageLocation(locationPrefix, layout.StageId);
            if (layout.StageId <= 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG001", stageLocation, $"StageId must be >= 1. current={layout.StageId}"));
            }

            int width = Mathf.Max(0, layout.Grid.Width);
            int height = Mathf.Max(0, layout.Grid.Height);
            bool validGrid = layout.Grid.Width > 0 && layout.Grid.Height > 0 && layout.Grid.CellSize > 0f;
            if (!validGrid)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STG002",
                    stageLocation,
                    $"Grid must have Width/Height > 0 and CellSize > 0. current=({layout.Grid.Width}, {layout.Grid.Height}, {layout.Grid.CellSize})"));
            }

            int expectedCellCount = width > 0 && height > 0 ? width * height : 0;
            var cells = layout.Cells ?? Array.Empty<StageCellLayoutData>();
            if (cells.Length != expectedCellCount)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STG003",
                    stageLocation,
                    $"Cells length must equal Grid.Width * Grid.Height. cells={cells.Length}, expected={expectedCellCount}"));
            }

            var sourceRegions = BuildSourceRegionMap(layout.SourceRegions, stageLocation, issues);
            var depositRegions = BuildDepositRegionMap(layout.DepositRegions, stageLocation, issues);

            var sourceRefCounts = new Dictionary<uint, int>();
            var depositAccessibleCounts = new Dictionary<uint, int>();
            bool hasAnySourceRegion = false;
            bool hasAnyDepositRegion = false;

            int inspectCellCount = Math.Min(cells.Length, expectedCellCount);
            for (int i = 0; i < inspectCellCount; i++)
            {
                var cell = cells[i];
                string location = BuildCellLocation(stageLocation, i, width);

                if (cell.SourceRegionId != 0 && cell.DepositRegionId != 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG008",
                        location,
                        "Cell must not reference SourceRegionId and DepositRegionId at the same time."));
                }

                if (cell.SourceRegionId != 0)
                {
                    hasAnySourceRegion = true;
                    if (!sourceRegions.ContainsKey(cell.SourceRegionId))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "STG006",
                            location,
                            $"Cell references missing SourceRegionId. stableId={cell.SourceRegionId}"));
                    }
                    else
                    {
                        sourceRefCounts[cell.SourceRegionId] = sourceRefCounts.TryGetValue(cell.SourceRegionId, out int count)
                            ? count + 1
                            : 1;
                    }
                }

                if (cell.DepositRegionId != 0)
                {
                    hasAnyDepositRegion = true;
                    if (!depositRegions.ContainsKey(cell.DepositRegionId))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "STG007",
                            location,
                            $"Cell references missing DepositRegionId. stableId={cell.DepositRegionId}"));
                    }
                    else if ((cell.MovementFlags & StageCellMovementFlags.BlockPlayer) == 0)
                    {
                        depositAccessibleCounts[cell.DepositRegionId] = depositAccessibleCounts.TryGetValue(cell.DepositRegionId, out int count)
                            ? count + 1
                            : 1;
                    }
                }
            }

            ValidateSourceRegions(layout.SourceRegions, sourceRefCounts, cells, width, height, stageLocation, issues);
            ValidateDepositRegions(layout.DepositRegions, depositAccessibleCounts, cells, width, height, stageLocation, issues);

            if (!hasAnySourceRegion || !hasAnyDepositRegion)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STG013", stageLocation, "Stage should include at least one SourceRegion and one DepositRegion."));
            }

            ValidatePresentationEntries(layout.Presentations, stageLocation, issues);
        }

        public static bool UsesGridSchema(StageLayoutSO layout)
        {
            if (layout == null)
                return false;

            if (layout.SchemaVersion < 2)
                return false;

            return layout.Cells != null
                || layout.SourceRegions != null
                || layout.DepositRegions != null;
        }

        private static Dictionary<uint, StageSourceRegionLayoutData> BuildSourceRegionMap(
            StageSourceRegionLayoutData[] entries,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            var map = new Dictionary<uint, StageSourceRegionLayoutData>();
            if (entries == null)
                return map;

            var ownersById = new Dictionary<uint, List<string>>();
            for (int i = 0; i < entries.Length; i++)
            {
                uint stableId = entries[i].StableId;
                string location = $"{stageLocation}/SourceRegions[{i}]";
                if (!ownersById.TryGetValue(stableId, out var owners))
                {
                    owners = new List<string>(2);
                    ownersById.Add(stableId, owners);
                }

                owners.Add(location);

                if (stableId > 0 && !map.ContainsKey(stableId))
                    map.Add(stableId, entries[i]);
            }

            ReportDuplicates(ownersById, "STG004", "SourceRegion", issues);
            return map;
        }

        private static Dictionary<uint, StageDepositRegionLayoutData> BuildDepositRegionMap(
            StageDepositRegionLayoutData[] entries,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            var map = new Dictionary<uint, StageDepositRegionLayoutData>();
            if (entries == null)
                return map;

            var ownersById = new Dictionary<uint, List<string>>();
            for (int i = 0; i < entries.Length; i++)
            {
                uint stableId = entries[i].StableId;
                string location = $"{stageLocation}/DepositRegions[{i}]";
                if (!ownersById.TryGetValue(stableId, out var owners))
                {
                    owners = new List<string>(2);
                    ownersById.Add(stableId, owners);
                }

                owners.Add(location);

                if (stableId > 0 && !map.ContainsKey(stableId))
                    map.Add(stableId, entries[i]);
            }

            ReportDuplicates(ownersById, "STG005", "DepositRegion", issues);
            return map;
        }

        private static void ReportDuplicates(
            Dictionary<uint, List<string>> ownersById,
            string code,
            string category,
            List<ContentValidationIssue> issues)
        {
            foreach (var pair in ownersById)
            {
                if (pair.Key == 0)
                {
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, code, pair.Value[i], $"{category} StableId must be >= 1."));
                    }

                    continue;
                }

                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        code,
                        pair.Value[i],
                        $"Duplicate {category} StableId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateSourceRegions(
            StageSourceRegionLayoutData[] entries,
            Dictionary<uint, int> referenceCounts,
            StageCellLayoutData[] cells,
            int width,
            int height,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!entry.Active || entry.StableId == 0)
                    continue;

                string location = $"{stageLocation}/SourceRegions[{i}]";
                if (!referenceCounts.TryGetValue(entry.StableId, out int count) || count <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG009",
                        location,
                        $"Active SourceRegion must be referenced by at least one cell. stableId={entry.StableId}"));
                }

                ValidateAnchorCell(
                    location,
                    entry.StableId,
                    entry.AnchorCell,
                    width,
                    height,
                    cells,
                    cell => cell.SourceRegionId,
                    issues);
            }
        }

        private static void ValidateDepositRegions(
            StageDepositRegionLayoutData[] entries,
            Dictionary<uint, int> accessibleCounts,
            StageCellLayoutData[] cells,
            int width,
            int height,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!entry.Active || entry.StableId == 0)
                    continue;

                string location = $"{stageLocation}/DepositRegions[{i}]";
                if (!accessibleCounts.TryGetValue(entry.StableId, out int count) || count <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG010",
                        location,
                        $"Active DepositRegion must be referenced by at least one cell without BlockPlayer. stableId={entry.StableId}"));
                }

                ValidateAnchorCell(
                    location,
                    entry.StableId,
                    entry.AnchorCell,
                    width,
                    height,
                    cells,
                    cell => cell.DepositRegionId,
                    issues);
            }
        }

        private static void ValidateAnchorCell(
            string location,
            uint stableId,
            Vector2Int anchorCell,
            int width,
            int height,
            StageCellLayoutData[] cells,
            Func<StageCellLayoutData, uint> stableIdSelector,
            List<ContentValidationIssue> issues)
        {
            if (anchorCell.x < 0 || anchorCell.y < 0 || anchorCell.x >= width || anchorCell.y >= height)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STG011",
                    location,
                    $"AnchorCell must be within grid bounds. anchor=({anchorCell.x}, {anchorCell.y}), bounds=({width}, {height})"));
                return;
            }

            int index = anchorCell.y * width + anchorCell.x;
            if (index < 0 || index >= cells.Length || stableIdSelector(cells[index]) != stableId)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STG012",
                    location,
                    $"AnchorCell must lie on a cell that references the same region StableId. stableId={stableId}, anchor=({anchorCell.x}, {anchorCell.y})"));
            }
        }

        private static void ValidatePresentationEntries(StagePresentationLayoutData[] entries, string stageLocation, List<ContentValidationIssue> issues)
        {
            ValidatePresentationStableIdUniqueness(entries, stageLocation, issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Presentations[{i}]";
                if (entry.StableId == 0)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL004", location, "StableId must be >= 1."));
                if (string.IsNullOrWhiteSpace(entry.PresentationKey))
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STL007", location, "PresentationKey is empty."));
                if (entry.PlacementMode == StagePresentationPlacementMode.LinkedToParent)
                {
                    if (entry.LinkKind == StagePresentationLinkKind.None || entry.LinkedStableId == 0)
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL013", location, "LinkedToParent presentation requires LinkKind and LinkedStableId."));
                    if (!IsSupportedLinkedParentKind(entry.LinkKind))
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG014", location, $"Unsupported linked presentation kind for grid schema layouts. linkKind={(int)entry.LinkKind}"));
                }
                else if (entry.LinkKind != StagePresentationLinkKind.None || entry.LinkedStableId != 0)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL014", location, "Standalone presentation must not carry link target data."));
                }
            }
        }

        private static void ValidatePresentationStableIdUniqueness(StagePresentationLayoutData[] entries, string stageLocation, List<ContentValidationIssue> issues)
        {
            if (entries == null || entries.Length <= 1)
                return;

            var ownersById = new Dictionary<uint, List<string>>();
            for (int i = 0; i < entries.Length; i++)
            {
                uint stableId = entries[i].StableId;
                string location = $"{stageLocation}/Presentations[{i}]";
                if (!ownersById.TryGetValue(stableId, out var owners))
                {
                    owners = new List<string>(2);
                    ownersById.Add(stableId, owners);
                }

                owners.Add(location);
            }

            foreach (var pair in ownersById)
            {
                if (pair.Key == 0 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL003", pair.Value[i], $"Duplicate Presentation StableId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static string BuildCellLocation(string stageLocation, int index, int width)
        {
            int x = width > 0 ? index % width : 0;
            int y = width > 0 ? index / width : 0;
            return $"{stageLocation}/Cells[{index}] (x={x}, y={y})";
        }

        private static string BuildStageLocation(string prefix, int stageId)
        {
            return $"{prefix}::StageLayout(StageId={stageId})";
        }

        private static bool IsSupportedLinkedParentKind(StagePresentationLinkKind linkKind)
        {
            return linkKind == StagePresentationLinkKind.Source
                || linkKind == StagePresentationLinkKind.Deposit;
        }
    }
}
