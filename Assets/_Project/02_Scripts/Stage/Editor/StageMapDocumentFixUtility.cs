using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public readonly struct StageMapDocumentFixPreview
    {
        public readonly string FixId;
        public readonly string Summary;
        public readonly string Details;

        public StageMapDocumentFixPreview(string fixId, string summary, string details)
        {
            FixId = fixId ?? string.Empty;
            Summary = summary ?? string.Empty;
            Details = details ?? string.Empty;
        }
    }

    public static class StageMapDocumentFixUtility
    {
        public static bool TryBuildFixPreview(StageMapDocument document, ContentValidationIssue issue, out StageMapDocumentFixPreview preview)
        {
            preview = default;
            if (document == null)
                return false;

            switch (issue.Code)
            {
                case "STG003":
                    if (!HasPositiveGrid(document, out int cellCount))
                        return false;
                    preview = new StageMapDocumentFixPreview(
                        "resize-cells",
                        "Resize document cells to grid dimensions.",
                        $"Cells will be resized to {cellCount}. Existing overlapping cell data is preserved.");
                    return true;

                case "SMD010":
                    if (!HasPositiveGrid(document, out int keyCount))
                        return false;
                    preview = new StageMapDocumentFixPreview(
                        "resize-visual-tile-keys",
                        "Resize visual tile keys to grid dimensions.",
                        $"VisualTileKeys will be resized to {keyCount}. Existing overlapping keys are preserved.");
                    return true;

                case "STG009":
                    if (!TryResolveRegionIssue(document, issue, StageRegionKind.Source, out var sourceRegion, out _))
                        return false;
                    preview = new StageMapDocumentFixPreview(
                        "paint-source-anchor-cell",
                        $"Paint source region {sourceRegion.StableId} on its anchor cell.",
                        $"Cell {sourceRegion.AnchorCell} will reference SourceRegionId {sourceRegion.StableId}.");
                    return true;

                case "STG010":
                    if (!TryResolveRegionIssue(document, issue, StageRegionKind.Deposit, out var depositRegion, out _)
                        || !CanPaintDepositCell(document, depositRegion.AnchorCell))
                    {
                        return false;
                    }

                    preview = new StageMapDocumentFixPreview(
                        "paint-deposit-anchor-cell",
                        $"Paint deposit region {depositRegion.StableId} on its anchor cell.",
                        $"Cell {depositRegion.AnchorCell} will reference DepositRegionId {depositRegion.StableId}.");
                    return true;

                case "STG012":
                    if (!TryResolveAnchorMismatchPreview(document, issue, out preview))
                        return false;
                    return true;

                case "STG015":
                case "STG016":
                case "STG017":
                    if (!TryFindFirstPlayerStartCell(document, out var playerCell))
                        return false;
                    preview = new StageMapDocumentFixPreview(
                        "move-player-start",
                        "Move PlayerStart to the first walkable cell.",
                        $"PlayerStart will be activated at {playerCell} with zero offset.");
                    return true;
            }

            return false;
        }

        public static bool ApplyFix(StageMapDocument document, ContentValidationIssue issue)
        {
            if (!TryBuildFixPreview(document, issue, out _))
                return false;

            switch (issue.Code)
            {
                case "STG003":
                    return ResizeCellsPreservingData(document);
                case "SMD010":
                    return ResizeVisualTileKeysPreservingData(document);
                case "STG009":
                    return TryResolveRegionIssue(document, issue, StageRegionKind.Source, out var sourceRegion, out _)
                        && StageMapDocumentCommandUtility.PaintRegion(document, sourceRegion.AnchorCell, StageRegionKind.Source, sourceRegion.StableId);
                case "STG010":
                    return TryResolveRegionIssue(document, issue, StageRegionKind.Deposit, out var depositRegion, out _)
                        && CanPaintDepositCell(document, depositRegion.AnchorCell)
                        && StageMapDocumentCommandUtility.PaintRegion(document, depositRegion.AnchorCell, StageRegionKind.Deposit, depositRegion.StableId);
                case "STG012":
                    return ApplyAnchorMismatchFix(document, issue);
                case "STG015":
                case "STG016":
                case "STG017":
                    return TryFindFirstPlayerStartCell(document, out var playerCell)
                        && StageMapDocumentCommandUtility.PlacePlayerStart(document, playerCell, Vector2.zero, document.PlayerStart.YawDeg);
            }

            return false;
        }

        private static bool ResizeCellsPreservingData(StageMapDocument document)
        {
            if (!HasPositiveGrid(document, out int count))
                return false;

            var previous = document.Cells ?? Array.Empty<StageCellLayoutData>();
            if (previous.Length == count)
                return false;

            var next = new StageCellLayoutData[count];
            Array.Copy(previous, next, Mathf.Min(previous.Length, next.Length));
            document.Cells = next;
            return true;
        }

        private static bool ResizeVisualTileKeysPreservingData(StageMapDocument document)
        {
            if (!HasPositiveGrid(document, out int count))
                return false;

            var previous = document.VisualTileKeys ?? Array.Empty<string>();
            if (previous.Length == count)
                return false;

            var next = new string[count];
            Array.Copy(previous, next, Mathf.Min(previous.Length, next.Length));
            document.VisualTileKeys = next;
            return true;
        }

        private static bool TryResolveAnchorMismatchPreview(StageMapDocument document, ContentValidationIssue issue, out StageMapDocumentFixPreview preview)
        {
            preview = default;
            if (TryResolveRegionIssue(document, issue, StageRegionKind.Source, out var sourceRegion, out _))
            {
                if (TryFindRegionCell(document, StageRegionKind.Source, sourceRegion.StableId, requireWalkable: false, out var sourceCell))
                {
                    preview = new StageMapDocumentFixPreview(
                        "move-source-anchor-to-region-cell",
                        $"Move source region {sourceRegion.StableId} anchor to a painted cell.",
                        $"AnchorCell will move from {sourceRegion.AnchorCell} to {sourceCell}.");
                    return true;
                }

                if (StageMapDocumentCommandUtility.TryGetCellIndex(document, sourceRegion.AnchorCell, out _))
                {
                    preview = new StageMapDocumentFixPreview(
                        "paint-source-anchor-cell",
                        $"Paint source region {sourceRegion.StableId} on its anchor cell.",
                        $"Cell {sourceRegion.AnchorCell} will reference SourceRegionId {sourceRegion.StableId}.");
                    return true;
                }
            }

            if (TryResolveRegionIssue(document, issue, StageRegionKind.Deposit, out var depositRegion, out _))
            {
                if (TryFindRegionCell(document, StageRegionKind.Deposit, depositRegion.StableId, requireWalkable: true, out var depositCell))
                {
                    preview = new StageMapDocumentFixPreview(
                        "move-deposit-anchor-to-region-cell",
                        $"Move deposit region {depositRegion.StableId} anchor to a painted cell.",
                        $"AnchorCell will move from {depositRegion.AnchorCell} to {depositCell}.");
                    return true;
                }

                if (CanPaintDepositCell(document, depositRegion.AnchorCell))
                {
                    preview = new StageMapDocumentFixPreview(
                        "paint-deposit-anchor-cell",
                        $"Paint deposit region {depositRegion.StableId} on its anchor cell.",
                        $"Cell {depositRegion.AnchorCell} will reference DepositRegionId {depositRegion.StableId}.");
                    return true;
                }
            }

            return false;
        }

        private static bool ApplyAnchorMismatchFix(StageMapDocument document, ContentValidationIssue issue)
        {
            if (TryResolveRegionIssue(document, issue, StageRegionKind.Source, out var sourceRegion, out int sourceIndex))
            {
                if (TryFindRegionCell(document, StageRegionKind.Source, sourceRegion.StableId, requireWalkable: false, out var sourceCell))
                    return MoveRegionAnchor(document, StageRegionKind.Source, sourceIndex, sourceCell);

                return StageMapDocumentCommandUtility.TryGetCellIndex(document, sourceRegion.AnchorCell, out _)
                    && StageMapDocumentCommandUtility.PaintRegion(document, sourceRegion.AnchorCell, StageRegionKind.Source, sourceRegion.StableId);
            }

            if (TryResolveRegionIssue(document, issue, StageRegionKind.Deposit, out var depositRegion, out int depositIndex))
            {
                if (TryFindRegionCell(document, StageRegionKind.Deposit, depositRegion.StableId, requireWalkable: true, out var depositCell))
                    return MoveRegionAnchor(document, StageRegionKind.Deposit, depositIndex, depositCell);

                return CanPaintDepositCell(document, depositRegion.AnchorCell)
                    && StageMapDocumentCommandUtility.PaintRegion(document, depositRegion.AnchorCell, StageRegionKind.Deposit, depositRegion.StableId);
            }

            return false;
        }

        private static bool MoveRegionAnchor(StageMapDocument document, StageRegionKind kind, int index, Vector2Int cell)
        {
            var regions = kind == StageRegionKind.Source ? document.SourceRegions : document.DepositRegions;
            if (regions == null || index < 0 || index >= regions.Length)
                return false;

            if (regions[index].AnchorCell == cell && regions[index].AnchorOffset == Vector2.zero)
                return false;

            regions[index].AnchorCell = cell;
            regions[index].AnchorOffset = Vector2.zero;
            if (kind == StageRegionKind.Source)
                document.SourceRegions = regions;
            else
                document.DepositRegions = regions;
            return true;
        }

        private static bool TryResolveRegionIssue(
            StageMapDocument document,
            ContentValidationIssue issue,
            StageRegionKind kind,
            out StageMapRegionData region,
            out int index)
        {
            region = default;
            index = -1;
            var regions = kind == StageRegionKind.Source ? document.SourceRegions : document.DepositRegions;
            string prefix = kind == StageRegionKind.Source ? "/SourceRegions[" : "/DepositRegions[";
            if (regions == null || !TryParseArrayIndex(issue.Location, prefix, out index) || index < 0 || index >= regions.Length)
                return false;

            region = regions[index];
            return region.Active && region.StableId > 0u;
        }

        private static bool TryFindRegionCell(
            StageMapDocument document,
            StageRegionKind kind,
            uint stableId,
            bool requireWalkable,
            out Vector2Int cell)
        {
            cell = default;
            if (document == null || document.Cells == null || document.Grid.Width <= 0)
                return false;

            for (int i = 0; i < document.Cells.Length; i++)
            {
                var data = document.Cells[i];
                bool matches = kind == StageRegionKind.Source
                    ? data.SourceRegionId == stableId
                    : data.DepositRegionId == stableId;
                if (!matches)
                    continue;

                if (requireWalkable && (data.MovementFlags & StageCellMovementFlags.BlockPlayer) != 0)
                    continue;

                cell = new Vector2Int(i % document.Grid.Width, i / document.Grid.Width);
                return true;
            }

            return false;
        }

        private static bool TryFindFirstPlayerStartCell(StageMapDocument document, out Vector2Int cell)
        {
            cell = default;
            if (document == null || document.Cells == null || document.Grid.Width <= 0)
                return false;

            for (int i = 0; i < document.Cells.Length; i++)
            {
                if ((document.Cells[i].MovementFlags & StageCellMovementFlags.BlockPlayer) != 0)
                    continue;

                cell = new Vector2Int(i % document.Grid.Width, i / document.Grid.Width);
                return StageMapDocumentCommandUtility.TryGetCellIndex(document, cell, out _);
            }

            return false;
        }

        private static bool CanPaintDepositCell(StageMapDocument document, Vector2Int cell)
        {
            if (!StageMapDocumentCommandUtility.TryGetCellIndex(document, cell, out int index))
                return false;

            var cells = document.Cells;
            if (cells == null || index >= cells.Length)
                return false;

            return (cells[index].MovementFlags & StageCellMovementFlags.BlockPlayer) == 0;
        }

        private static bool HasPositiveGrid(StageMapDocument document, out int count)
        {
            count = 0;
            if (document == null || document.Grid.Width <= 0 || document.Grid.Height <= 0)
                return false;

            count = document.Grid.Width * document.Grid.Height;
            return count > 0;
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
            if (end < 0)
                return false;

            return int.TryParse(text.Substring(start, end - start), out index);
        }
    }
}
