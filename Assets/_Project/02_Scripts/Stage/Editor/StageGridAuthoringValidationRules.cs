using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageGridAuthoringValidationRules
    {
        public static void Validate(StageLayoutStageMarker stageNode, List<ContentValidationIssue> issues)
        {
            if (stageNode == null || issues == null)
                return;

            string stageLocation = BuildHierarchyPath(stageNode.transform);
            if (!stageNode.TryGetComponent(out StageGridAuthoring authoring) || authoring == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA001", stageLocation, "StageGridAuthoring is required on the StageLayoutStageMarker GameObject."));
                return;
            }

            if (!TryValidateAndResolve(authoring, stageLocation, issues, out _, out _, out _))
                return;

            ValidatePresentationMarkers(stageNode, issues);
        }

        public static bool TryValidateAndResolve(
            StageGridAuthoring authoring,
            string location,
            List<ContentValidationIssue> issues,
            out Vector3Int boundsMin,
            out Vector3Int boundsSize,
            out Dictionary<(StageRegionKind Kind, uint StableId), StageRegionAnchorMarker> anchorByRegion)
        {
            boundsMin = default;
            boundsSize = default;
            anchorByRegion = new Dictionary<(StageRegionKind Kind, uint StableId), StageRegionAnchorMarker>();

            if (authoring == null)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA002", location, "StageGridAuthoring is null."));
                return false;
            }

            bool valid = true;
            if (authoring.Grid == null)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA003", location, "Grid reference is missing."));
                valid = false;
            }

            if (authoring.MovementTilemap == null)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA004", location, "MovementTilemap reference is missing."));
                valid = false;
            }

            if (authoring.SourceRegionPaint == null || authoring.DepositRegionPaint == null)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA005", location, "SourceRegionPaint and DepositRegionPaint must both be assigned."));
                valid = false;
            }

            if (!valid)
                return false;

            if (authoring.SourceRegionPaint.RegionKind != StageRegionKind.Source)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA006", location, "SourceRegionPaint must have RegionKind=Source."));
                valid = false;
            }

            if (authoring.DepositRegionPaint.RegionKind != StageRegionKind.Deposit)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA007", location, "DepositRegionPaint must have RegionKind=Deposit."));
                valid = false;
            }

            authoring.SourceRegionPaint.EnsureShape();
            authoring.DepositRegionPaint.EnsureShape();

            if (authoring.SourceRegionPaint.Width != authoring.DepositRegionPaint.Width
                || authoring.SourceRegionPaint.Height != authoring.DepositRegionPaint.Height)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA008", location, "Source/Deposit region paints must have identical dimensions."));
                valid = false;
            }

            if (authoring.Grid.cellSize.x <= 0f || !Mathf.Approximately(authoring.Grid.cellSize.x, authoring.Grid.cellSize.y))
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA009", location, "Grid cell size must be square and positive."));
                valid = false;
            }

            boundsMin = authoring.MovementTilemap.cellBounds.min;
            boundsSize = authoring.MovementTilemap.cellBounds.size;
            if (boundsMin.x != 0 || boundsMin.y != 0 || boundsMin.z != 0)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA010", location, $"MovementTilemap bounds min must be (0,0,0). current={boundsMin}"));
                valid = false;
            }

            if (boundsSize.x != authoring.SourceRegionPaint.Width || boundsSize.y != authoring.SourceRegionPaint.Height)
            {
                issues?.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STA011",
                    location,
                    $"MovementTilemap bounds size must match paint asset dimensions. tilemap=({boundsSize.x}, {boundsSize.y}), paint=({authoring.SourceRegionPaint.Width}, {authoring.SourceRegionPaint.Height})"));
                valid = false;
            }

            if (authoring.MovementTilemap.layoutGrid != authoring.Grid)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA012", location, "MovementTilemap must be authored under the assigned Grid."));
                valid = false;
            }

            if (!ValidateMovementTiles(authoring.MovementTilemap, boundsMin, boundsSize, location, issues))
                valid = false;

            var stageNode = authoring.GetComponent<StageLayoutStageMarker>();
            var anchors = stageNode != null
                ? stageNode.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true)
                : authoring.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                var key = (anchor.RegionKind, anchor.StableId);
                string anchorLocation = $"{location}/{BuildHierarchyPath(anchor.transform)}";
                if (anchor.StableId == 0)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA013", anchorLocation, "Anchor StableId must be >= 1."));
                    valid = false;
                    continue;
                }

                if (anchorByRegion.ContainsKey(key))
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA014", anchorLocation, $"Duplicate anchor marker for {anchor.RegionKind} stableId={anchor.StableId}."));
                    valid = false;
                    continue;
                }

                anchorByRegion.Add(key, anchor);
            }

            if (!ValidatePaintAndAnchors(authoring.SourceRegionPaint, StageRegionKind.Source, anchorByRegion, location, issues))
                valid = false;
            if (!ValidatePaintAndAnchors(authoring.DepositRegionPaint, StageRegionKind.Deposit, anchorByRegion, location, issues))
                valid = false;
            if (!ValidateOverlap(authoring.SourceRegionPaint, authoring.DepositRegionPaint, location, issues))
                valid = false;

            return valid;
        }

        private static bool ValidateMovementTiles(Tilemap tilemap, Vector3Int boundsMin, Vector3Int boundsSize, string location, List<ContentValidationIssue> issues)
        {
            bool valid = true;
            for (int y = 0; y < boundsSize.y; y++)
            {
                for (int x = 0; x < boundsSize.x; x++)
                {
                    var tile = tilemap.GetTile(boundsMin + new Vector3Int(x, y, 0));
                    if (tile == null || tile is StageMovementTile)
                        continue;

                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA015", location, $"MovementTilemap contains non-StageMovementTile at cell ({x}, {y})."));
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidatePaintAndAnchors(
            StageRegionPaintAsset paintAsset,
            StageRegionKind kind,
            Dictionary<(StageRegionKind Kind, uint StableId), StageRegionAnchorMarker> anchorByRegion,
            string location,
            List<ContentValidationIssue> issues)
        {
            bool valid = true;
            var painted = new HashSet<uint>();
            for (int y = 0; y < paintAsset.Height; y++)
            {
                for (int x = 0; x < paintAsset.Width; x++)
                {
                    uint stableId = paintAsset.GetCell(x, y);
                    if (stableId == 0)
                        continue;

                    painted.Add(stableId);
                    if (!anchorByRegion.TryGetValue((kind, stableId), out var anchor) || anchor == null)
                    {
                        issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA016", location, $"Painted {kind} stableId={stableId} is missing an anchor marker."));
                        valid = false;
                    }
                }
            }

            foreach (var pair in anchorByRegion)
            {
                if (pair.Key.Kind != kind)
                    continue;

                var anchor = pair.Value;
                string anchorLocation = $"{location}/{BuildHierarchyPath(anchor.transform)}";
                bool inBounds = anchor.AnchorCell.x >= 0
                    && anchor.AnchorCell.y >= 0
                    && anchor.AnchorCell.x < paintAsset.Width
                    && anchor.AnchorCell.y < paintAsset.Height;
                if (!inBounds)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA017", anchorLocation, $"AnchorCell is out of bounds for {kind} paint. anchor={anchor.AnchorCell}"));
                    valid = false;
                    continue;
                }

                if (paintAsset.GetCell(anchor.AnchorCell.x, anchor.AnchorCell.y) != pair.Key.StableId)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA018", anchorLocation, $"AnchorCell must point to a painted {kind} cell with the same stableId={pair.Key.StableId}."));
                    valid = false;
                }

                if (!painted.Contains(pair.Key.StableId))
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA019", anchorLocation, $"Anchor marker exists for {kind} stableId={pair.Key.StableId} but no cells are painted."));
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidateOverlap(StageRegionPaintAsset source, StageRegionPaintAsset deposit, string location, List<ContentValidationIssue> issues)
        {
            bool valid = true;
            int width = Math.Min(source.Width, deposit.Width);
            int height = Math.Min(source.Height, deposit.Height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (source.GetCell(x, y) == 0 || deposit.GetCell(x, y) == 0)
                        continue;

                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA020", location, $"Source/Deposit overlap is not allowed. cell=({x}, {y})"));
                    valid = false;
                }
            }

            return valid;
        }

        private static void ValidatePresentationMarkers(StageLayoutStageMarker stageNode, List<ContentValidationIssue> issues)
        {
            var markers = stageNode.GetComponentsInChildren<StagePresentationMarker>(includeInactive: true);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                string location = BuildHierarchyPath(marker.transform);
                bool hasTopologyOnSelf = StagePresentationEditorUtility.HasTopologyOnSelf(marker);
                if (hasTopologyOnSelf)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL010", location, "StagePresentationMarker must not share a GameObject with Source/Deposit anchor marker."));

                bool hasParentTopology = StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out var linkKind, out _, out _);
                if (marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent && !hasParentTopology)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL011", location, "LinkedToParent presentation requires a parent Source/Deposit anchor marker."));

                if (marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent && linkKind == StagePresentationLinkKind.Obstacle)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG014", location, "Obstacle-linked presentation is not supported for grid schema layouts."));

                if (marker.PlacementMode == StagePresentationPlacementMode.Standalone && hasParentTopology)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL012", location, "Standalone presentation must not be authored under a topology marker parent."));
            }
        }

        public static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "(null)";

            string path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
