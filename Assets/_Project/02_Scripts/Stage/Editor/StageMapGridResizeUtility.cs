using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public sealed class StageMapGridResizePlan
    {
        internal StageMapGridResizePlan(
            StageMapDocument document,
            StageGridSpec sourceGrid,
            StageGridSpec targetGrid,
            string documentSignature,
            StageCellLayoutData[] cells,
            string[] visualTileKeys,
            IReadOnlyList<ContentValidationIssue> issues,
            IReadOnlyList<StageMapApplyPlanChange> changes,
            int croppedNonDefaultCellCount,
            int croppedVisualKeyCount)
        {
            Document = document;
            SourceGrid = sourceGrid;
            TargetGrid = targetGrid;
            DocumentSignature = documentSignature ?? string.Empty;
            Cells = cells ?? Array.Empty<StageCellLayoutData>();
            VisualTileKeys = visualTileKeys ?? Array.Empty<string>();
            Issues = issues ?? Array.Empty<ContentValidationIssue>();
            Changes = changes ?? Array.Empty<StageMapApplyPlanChange>();
            CroppedNonDefaultCellCount = croppedNonDefaultCellCount;
            CroppedVisualKeyCount = croppedVisualKeyCount;
        }

        public StageMapDocument Document { get; }
        public StageGridSpec SourceGrid { get; }
        public StageGridSpec TargetGrid { get; }
        public string DocumentSignature { get; }
        public StageCellLayoutData[] Cells { get; }
        public string[] VisualTileKeys { get; }
        public IReadOnlyList<ContentValidationIssue> Issues { get; }
        public IReadOnlyList<StageMapApplyPlanChange> Changes { get; }
        public int CroppedNonDefaultCellCount { get; }
        public int CroppedVisualKeyCount { get; }
        public bool HasErrors => Issues.Any(x => x.Severity == ContentValidationSeverity.Error);
        public bool HasChanges => Changes.Count > 0;
        public bool RequiresConfirmation => CroppedNonDefaultCellCount > 0 || CroppedVisualKeyCount > 0;
    }

    /// <summary>
    /// Owns grid metadata and dense-array resize preview/apply.
    /// </summary>
    public static class StageMapGridResizeUtility
    {
        public static StageMapGridResizePlan BuildPreview(StageMapDocument document, StageGridSpec targetGrid)
        {
            var issues = new List<ContentValidationIssue>(2);
            var changes = new List<StageMapApplyPlanChange>(3);
            if (document == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMR900", "(null)", "StageMapDocument is null."));
                return new StageMapGridResizePlan(null, default, targetGrid, string.Empty, null, null, issues, changes, 0, 0);
            }

            string location = AssetDatabase.GetAssetPath(document);
            if (!TryGetCellCount(targetGrid, out int targetCount))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMR001",
                    location,
                    "Target grid width, height, and cell size must be positive and the dense cell count must fit Int32."));
                return EmptyPlan(document, targetGrid, issues, changes);
            }

            if (!TryGetCellCount(document.Grid, out int sourceCount)
                || document.Cells == null
                || document.Cells.Length != sourceCount)
            {
                int actual = document.Cells != null ? document.Cells.Length : 0;
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMR002",
                    location,
                    $"Grid resize requires a valid dense Cells array. Repair it first. cells={actual}, expected={sourceCount}"));
                return EmptyPlan(document, targetGrid, issues, changes);
            }

            var sourceKeys = document.VisualTileKeys ?? Array.Empty<string>();
            if (sourceKeys.Length != 0 && sourceKeys.Length != sourceCount)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMR003",
                    location,
                    $"Grid resize requires VisualTileKeys to be empty or dense. Repair it first. keys={sourceKeys.Length}, expected={sourceCount}"));
                return EmptyPlan(document, targetGrid, issues, changes);
            }

            var cells = new StageCellLayoutData[targetCount];
            var keys = sourceKeys.Length == 0 ? Array.Empty<string>() : new string[targetCount];
            int overlapWidth = Mathf.Min(document.Grid.Width, targetGrid.Width);
            int overlapHeight = Mathf.Min(document.Grid.Height, targetGrid.Height);
            for (int y = 0; y < overlapHeight; y++)
            {
                for (int x = 0; x < overlapWidth; x++)
                {
                    int sourceIndex = (y * document.Grid.Width) + x;
                    int targetIndex = (y * targetGrid.Width) + x;
                    cells[targetIndex] = document.Cells[sourceIndex];
                    if (keys.Length != 0)
                        keys[targetIndex] = sourceKeys[sourceIndex];
                }
            }

            int croppedCells = 0;
            int croppedKeys = 0;
            for (int y = 0; y < document.Grid.Height; y++)
            {
                for (int x = 0; x < document.Grid.Width; x++)
                {
                    if (x < targetGrid.Width && y < targetGrid.Height)
                        continue;

                    int index = (y * document.Grid.Width) + x;
                    if (!IsDefault(document.Cells[index]))
                        croppedCells++;
                    if (sourceKeys.Length == sourceCount && !string.IsNullOrEmpty(sourceKeys[index]))
                        croppedKeys++;
                }
            }

            if (!GridEquals(document.Grid, targetGrid))
            {
                changes.Add(new StageMapApplyPlanChange(
                    StageMapApplyChangeKind.Update,
                    "StageMapDocument",
                    nameof(StageMapDocument.Grid),
                    $"Resize grid {document.Grid.Width}x{document.Grid.Height} to {targetGrid.Width}x{targetGrid.Height}."));
            }

            if (sourceCount != targetCount || document.Grid.Width != targetGrid.Width)
            {
                changes.Add(new StageMapApplyPlanChange(
                    croppedCells > 0 ? StageMapApplyChangeKind.Remove : StageMapApplyChangeKind.Update,
                    "StageMapDocument",
                    nameof(StageMapDocument.Cells),
                    $"Rebuild dense cells by coordinate. croppedNonDefault={croppedCells}"));
                changes.Add(new StageMapApplyPlanChange(
                    croppedKeys > 0 ? StageMapApplyChangeKind.Remove : StageMapApplyChangeKind.Update,
                    "StageMapDocument",
                    nameof(StageMapDocument.VisualTileKeys),
                    $"Rebuild visual keys by coordinate. croppedNonDefault={croppedKeys}"));
            }

            return new StageMapGridResizePlan(
                document,
                document.Grid,
                targetGrid,
                StageMapApplyPlanner.ComputeSignature(document),
                cells,
                keys,
                issues,
                changes,
                croppedCells,
                croppedKeys);
        }

        public static bool TryApply(StageMapGridResizePlan plan, bool confirmed, out string error)
        {
            error = null;
            if (plan == null || plan.Document == null)
            {
                error = "Stage map grid resize plan is invalid.";
                return false;
            }

            if (StageMapApplyPlanner.ComputeSignature(plan.Document) != plan.DocumentSignature)
            {
                error = "StageMapDocument changed after grid resize preview. Rebuild the resize plan.";
                return false;
            }

            if (plan.HasErrors)
            {
                error = "Stage map grid resize validation failed.";
                return false;
            }

            if (plan.RequiresConfirmation && !confirmed)
            {
                error = "Grid resize removes non-default data and requires confirmation.";
                return false;
            }

            if (!plan.HasChanges)
                return true;

            Undo.RecordObject(plan.Document, "Resize Stage Map Grid");
            plan.Document.Grid = plan.TargetGrid;
            plan.Document.Cells = (StageCellLayoutData[])plan.Cells.Clone();
            plan.Document.VisualTileKeys = (string[])plan.VisualTileKeys.Clone();
            EditorUtility.SetDirty(plan.Document);
            return true;
        }

        private static StageMapGridResizePlan EmptyPlan(
            StageMapDocument document,
            StageGridSpec targetGrid,
            IReadOnlyList<ContentValidationIssue> issues,
            IReadOnlyList<StageMapApplyPlanChange> changes)
        {
            return new StageMapGridResizePlan(
                document,
                document != null ? document.Grid : default,
                targetGrid,
                document != null ? StageMapApplyPlanner.ComputeSignature(document) : string.Empty,
                null,
                null,
                issues,
                changes,
                0,
                0);
        }

        private static bool TryGetCellCount(StageGridSpec grid, out int count)
        {
            count = 0;
            if (grid.Width <= 0 || grid.Height <= 0 || grid.CellSize <= 0f)
                return false;

            long value = (long)grid.Width * grid.Height;
            if (value <= 0L || value > int.MaxValue)
                return false;

            count = (int)value;
            return true;
        }

        private static bool IsDefault(StageCellLayoutData cell)
        {
            return cell.MovementFlags == StageCellMovementFlags.None
                && cell.SourceRegionId == 0u
                && cell.DepositRegionId == 0u;
        }

        private static bool GridEquals(StageGridSpec left, StageGridSpec right)
        {
            return left.Width == right.Width
                && left.Height == right.Height
                && Mathf.Approximately(left.CellSize, right.CellSize)
                && left.Origin == right.Origin;
        }
    }
}
