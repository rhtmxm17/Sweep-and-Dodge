using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageGridAuthoringValidationRules
    {
        private readonly struct ResolvedRegionData
        {
            public ResolvedRegionData(bool useTilemap, uint[] cells)
            {
                UseTilemap = useTilemap;
                Cells = cells;
            }

            public bool UseTilemap { get; }
            public uint[] Cells { get; }
        }

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

            TryValidateAndResolve(authoring, stageLocation, issues, out _, out _, out _);
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

            if (!valid)
                return false;

            if (authoring.Grid.cellSize.x <= 0f || !Mathf.Approximately(authoring.Grid.cellSize.x, authoring.Grid.cellSize.y))
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA009", location, "Grid cell size must be square and positive."));
                valid = false;
            }

            if (!IsCanonicalGridRotation(authoring.Grid.transform))
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA021", location, "Grid rotation must be canonical identity or +90deg X only."));
                valid = false;
            }

            if (authoring.BoundsSize.x <= 0 || authoring.BoundsSize.y <= 0)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA010", location, $"Authoring bounds size must be positive. current=({authoring.BoundsSize.x}, {authoring.BoundsSize.y})"));
                valid = false;
            }

            var authoringBounds = authoring.GetAuthoringBounds();
            boundsMin = authoringBounds.min;
            boundsSize = authoringBounds.size;

            if (authoring.MovementTilemap.layoutGrid != authoring.Grid)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA012", location, "MovementTilemap must be authored under the assigned Grid."));
                valid = false;
            }

            valid &= ValidateVisualTilemap(authoring.GroundVisualTilemap, authoring.Grid, "GroundVisualTilemap", location, issues);
            valid &= ValidateVisualTilemap(authoring.WallVisualTilemap, authoring.Grid, "WallVisualTilemap", location, issues);

            WarnUsedTilesOutsideBounds(authoring.MovementTilemap, authoringBounds, location, "MovementTilemap", issues);
            WarnUsedTilesOutsideBounds(authoring.GroundVisualTilemap, authoringBounds, location, "GroundVisualTilemap", issues);
            WarnUsedTilesOutsideBounds(authoring.WallVisualTilemap, authoringBounds, location, "WallVisualTilemap", issues);

            if (!ValidateMovementTiles(authoring.MovementTilemap, boundsMin, boundsSize, location, issues))
                valid = false;

            valid &= ValidateMappingTable(authoring, StageRegionKind.Source, location, issues);
            valid &= ValidateMappingTable(authoring, StageRegionKind.Deposit, location, issues);

            var sourceData = ResolveRegionData(authoring, StageRegionKind.Source, boundsSize, location, issues, ref valid);
            var depositData = ResolveRegionData(authoring, StageRegionKind.Deposit, boundsSize, location, issues, ref valid);

            var stageNode = authoring.GetComponent<StageLayoutStageMarker>();
            var anchors = stageNode != null
                ? stageNode.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true)
                : authoring.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                string anchorLocation = $"{location}/{BuildHierarchyPath(anchor.transform)}";
                if (anchor.RegionSlotIndex <= 0)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA013", anchorLocation, "Anchor RegionSlotIndex must be >= 1."));
                    valid = false;
                    continue;
                }

                if (!authoring.TryResolveStableId(anchor.RegionKind, anchor.RegionSlotIndex, out uint stableId))
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA024", anchorLocation, $"Anchor slot is missing a {anchor.RegionKind} mapping. slot={anchor.RegionSlotIndex}"));
                    valid = false;
                    continue;
                }

                anchor.StableId = stableId;
                var key = (anchor.RegionKind, stableId);
                if (anchorByRegion.ContainsKey(key))
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA014", anchorLocation, $"Duplicate anchor marker for {anchor.RegionKind} stableId={stableId}."));
                    valid = false;
                    continue;
                }

                anchorByRegion.Add(key, anchor);
            }

            valid &= ValidateAnchors(authoring, StageRegionKind.Source, sourceData.Cells, boundsSize, anchorByRegion, location, issues);
            valid &= ValidateAnchors(authoring, StageRegionKind.Deposit, depositData.Cells, boundsSize, anchorByRegion, location, issues);
            valid &= ValidateOverlap(sourceData.Cells, depositData.Cells, boundsSize, location, issues);
            return valid;
        }

        private static bool ValidateMappingTable(StageGridAuthoring authoring, StageRegionKind kind, string location, List<ContentValidationIssue> issues)
        {
            bool valid = true;
            var mappings = authoring.GetMappings(kind);
            if (mappings == null || mappings.Length == 0)
                return true;

            var slotOwners = new Dictionary<int, int>();
            var stableIdOwners = new Dictionary<uint, int>();
            for (int i = 0; i < mappings.Length; i++)
            {
                var entry = mappings[i];
                string entryLocation = $"{location}/{kind}RegionMappings[{i}]";
                if (entry.RegionSlotIndex <= 0)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA023", entryLocation, "RegionSlotIndex must be >= 1."));
                    valid = false;
                    continue;
                }

                if (entry.StableId == 0u)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA025", entryLocation, "StableId must be >= 1."));
                    valid = false;
                }

                if (!slotOwners.TryAdd(entry.RegionSlotIndex, i))
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA026", entryLocation, $"Duplicate {kind} RegionSlotIndex detected. slot={entry.RegionSlotIndex}"));
                    valid = false;
                }

                if (!stableIdOwners.TryAdd(entry.StableId, i))
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA027", entryLocation, $"Duplicate {kind} StableId detected. stableId={entry.StableId}"));
                    valid = false;
                }
            }

            return valid;
        }

        private static ResolvedRegionData ResolveRegionData(
            StageGridAuthoring authoring,
            StageRegionKind kind,
            Vector3Int boundsSize,
            string location,
            List<ContentValidationIssue> issues,
            ref bool valid)
        {
            int cellCount = boundsSize.x * boundsSize.y;
            var tilemap = authoring.GetRegionTilemap(kind);
            var paint = kind == StageRegionKind.Source ? authoring.SourceRegionPaint : authoring.DepositRegionPaint;
            bool usesUnifiedTilemap = authoring.RegionTilemap != null;

            if (tilemap != null)
            {
                if (tilemap.layoutGrid != authoring.Grid)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, kind == StageRegionKind.Source ? "STA028" : "STA029", location, $"{kind}Tilemap must be authored under the assigned Grid."));
                    valid = false;
                }

                WarnUsedTilesOutsideBounds(tilemap, authoring.GetAuthoringBounds(), location, usesUnifiedTilemap ? "RegionTilemap" : $"{kind}Tilemap", issues);

                if (paint != null)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STA030", location, $"{(usesUnifiedTilemap ? "RegionTilemap" : $"{kind}Tilemap")} is authoritative. {kind}RegionPaint is treated as deprecated fallback only."));
                }

                if (usesUnifiedTilemap)
                {
                    if ((kind == StageRegionKind.Source && authoring.SourceTilemap != null)
                        || (kind == StageRegionKind.Deposit && authoring.DepositTilemap != null))
                    {
                        issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STA033", location, $"RegionTilemap is authoritative. Legacy split {(kind == StageRegionKind.Source ? "SourceTilemap" : "DepositTilemap")} is ignored."));
                    }
                }

                var cells = new uint[cellCount];
                for (int y = 0; y < boundsSize.y; y++)
                {
                    for (int x = 0; x < boundsSize.x; x++)
                    {
                        int index = (y * boundsSize.x) + x;
                        var tile = tilemap.GetTile(authoring.GetTilemapCell(x, y));
                        if (tile == null)
                        {
                            cells[index] = 0u;
                            continue;
                        }

                        if (tile is not StageRegionTile regionTile)
                        {
                            issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA015", location, $"{kind}Tilemap contains non-StageRegionTile at local cell ({x}, {y})."));
                            valid = false;
                            cells[index] = 0u;
                            continue;
                        }

                        if (usesUnifiedTilemap && regionTile.RegionKind != kind)
                        {
                            cells[index] = 0u;
                            continue;
                        }

                        if (!usesUnifiedTilemap && regionTile.RegionKind != kind)
                        {
                            issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA006", location, $"{kind}Tilemap contains tile with RegionKind={regionTile.RegionKind} at local cell ({x}, {y})."));
                            valid = false;
                            cells[index] = 0u;
                            continue;
                        }

                        if (regionTile.RegionSlotIndex <= 0)
                        {
                            cells[index] = 0u;
                            continue;
                        }

                        if (!authoring.TryResolveStableId(kind, regionTile.RegionSlotIndex, out uint stableId))
                        {
                            issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA024", location, $"{kind}Tilemap uses an unmapped slot at local cell ({x}, {y}). slot={regionTile.RegionSlotIndex}"));
                            valid = false;
                            cells[index] = 0u;
                            continue;
                        }

                        cells[index] = stableId;
                    }
                }

                return new ResolvedRegionData(true, cells);
            }

            if (paint == null)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA005", location, $"{kind}Tilemap is missing and deprecated fallback paint is not assigned."));
                valid = false;
                return new ResolvedRegionData(false, new uint[cellCount]);
            }

            issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STA031", location, $"{kind}Tilemap is not assigned. Deprecated {kind}RegionPaint fallback is being used."));
            if (paint.RegionKind != kind)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA006", location, $"{kind}RegionPaint must have RegionKind={kind}."));
                valid = false;
            }

            paint.EnsureShape();
            if (paint.Width != boundsSize.x || paint.Height != boundsSize.y)
            {
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA011", location, $"{kind}RegionPaint dimensions must match authoring bounds. bounds=({boundsSize.x}, {boundsSize.y}), paint=({paint.Width}, {paint.Height})"));
                valid = false;
            }

            var paintCells = new uint[cellCount];
            Array.Copy(paint.Cells ?? Array.Empty<uint>(), paintCells, Math.Min(paintCells.Length, paint.Cells?.Length ?? 0));
            return new ResolvedRegionData(false, paintCells);
        }

        private static bool ValidateAnchors(
            StageGridAuthoring authoring,
            StageRegionKind kind,
            uint[] cells,
            Vector3Int boundsSize,
            Dictionary<(StageRegionKind Kind, uint StableId), StageRegionAnchorMarker> anchorByRegion,
            string location,
            List<ContentValidationIssue> issues)
        {
            bool valid = true;
            var painted = new HashSet<uint>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != 0u)
                    painted.Add(cells[i]);
            }

            foreach (uint stableId in painted)
            {
                if (!anchorByRegion.ContainsKey((kind, stableId)))
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA016", location, $"Painted {kind} stableId={stableId} is missing an anchor marker."));
                    valid = false;
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
                    && anchor.AnchorCell.x < boundsSize.x
                    && anchor.AnchorCell.y < boundsSize.y;
                if (!inBounds)
                {
                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA017", anchorLocation, $"AnchorCell is out of bounds for {kind}. anchor={anchor.AnchorCell}"));
                    valid = false;
                    continue;
                }

                int index = (anchor.AnchorCell.y * boundsSize.x) + anchor.AnchorCell.x;
                if (index < 0 || index >= cells.Length || cells[index] != pair.Key.StableId)
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

        private static bool ValidateOverlap(uint[] sourceCells, uint[] depositCells, Vector3Int boundsSize, string location, List<ContentValidationIssue> issues)
        {
            bool valid = true;
            int width = boundsSize.x;
            int height = boundsSize.y;
            int count = Math.Min(sourceCells.Length, depositCells.Length);
            for (int index = 0; index < count; index++)
            {
                if (sourceCells[index] == 0u || depositCells[index] == 0u)
                    continue;

                int x = index % width;
                int y = index / width;
                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA020", location, $"Source/Deposit overlap is not allowed. cell=({x}, {y})"));
                valid = false;
            }

            return valid;
        }

        private static bool ValidateVisualTilemap(Tilemap tilemap, Grid grid, string name, string location, List<ContentValidationIssue> issues)
        {
            if (tilemap == null)
                return true;
            if (tilemap.layoutGrid == grid)
                return true;

            issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA032", location, $"{name} must be authored under the assigned Grid."));
            return false;
        }

        private static void WarnUsedTilesOutsideBounds(Tilemap tilemap, BoundsInt authoringBounds, string location, string tilemapName, List<ContentValidationIssue> issues)
        {
            if (tilemap == null)
                return;

            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) == null)
                    continue;
                if (authoringBounds.Contains(cell))
                    continue;

                issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STA022", location, $"{tilemapName} has a used tile outside authoring bounds at tile cell ({cell.x}, {cell.y})."));
                return;
            }
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

                    issues?.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STA015", location, $"MovementTilemap contains non-StageMovementTile at local cell ({x}, {y})."));
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

                bool hasParentTopology = StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out _, out _, out _);
                bool hasObstacleParent = marker.GetComponentInParent<StageObstacleMarker>() != null;
                if (hasObstacleParent)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG014", location, "Obstacle-linked presentation is not supported for grid schema layouts."));

                if (marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent && !hasParentTopology)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL011", location, "LinkedToParent presentation requires a parent Source/Deposit anchor marker."));

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

        public static bool IsCanonicalGridRotation(Transform transform)
        {
            if (transform == null)
                return false;

            Quaternion rotation = transform.rotation;
            return Quaternion.Angle(rotation, Quaternion.identity) <= 0.1f
                || Quaternion.Angle(rotation, Quaternion.Euler(90f, 0f, 0f)) <= 0.1f;
        }
    }
}
