using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapDocumentCommandUtility
    {
        public static bool TryGetCellIndex(StageMapDocument document, Vector2Int cell, out int index)
        {
            index = -1;
            if (document == null || document.Grid.Width <= 0 || document.Grid.Height <= 0)
                return false;

            if (cell.x < 0 || cell.y < 0 || cell.x >= document.Grid.Width || cell.y >= document.Grid.Height)
                return false;

            index = (cell.y * document.Grid.Width) + cell.x;
            return true;
        }

        public static Vector3 GetCellCenterWorld(StageMapDocument document, Vector2Int cell)
        {
            if (document == null)
                return Vector3.zero;

            float cellSize = Mathf.Max(0.0001f, document.Grid.CellSize);
            return document.Grid.Origin + new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
        }

        public static Vector2 ComputeAnchorOffset(StageMapDocument document, Vector2Int cell, Vector3 worldPosition)
        {
            if (document == null || document.Grid.CellSize <= 0f)
                return Vector2.zero;

            Vector3 center = GetCellCenterWorld(document, cell);
            float cellSize = Mathf.Max(0.0001f, document.Grid.CellSize);
            return new Vector2(
                Mathf.Clamp((worldPosition.x - center.x) / cellSize, -0.5f, 0.5f),
                Mathf.Clamp((worldPosition.z - center.z) / cellSize, -0.5f, 0.5f));
        }

        public static bool TryWorldToCell(StageMapDocument document, Vector3 worldPosition, out Vector2Int cell)
        {
            cell = default;
            if (document == null || document.Grid.CellSize <= 0f)
                return false;

            float cellSize = Mathf.Max(0.0001f, document.Grid.CellSize);
            int x = Mathf.FloorToInt((worldPosition.x - document.Grid.Origin.x) / cellSize);
            int y = Mathf.FloorToInt((worldPosition.z - document.Grid.Origin.z) / cellSize);
            cell = new Vector2Int(x, y);
            return TryGetCellIndex(document, cell, out _);
        }

        public static bool PaintMovement(StageMapDocument document, Vector2Int cell, StageCellMovementFlags movementFlags)
        {
            return TryPaintMovement(document, cell, movementFlags, out _);
        }

        public static bool TryPaintMovement(
            StageMapDocument document,
            Vector2Int cell,
            StageCellMovementFlags movementFlags,
            out ContentValidationIssue issue)
        {
            if (!TryValidateDenseCellsForWrite(document, out issue)
                || !TryGetCellIndex(document, cell, out int index))
            {
                return false;
            }

            var data = document.Cells[index];
            if (data.MovementFlags == movementFlags)
                return false;

            data.MovementFlags = movementFlags;
            document.Cells[index] = data;
            return true;
        }

        public static bool PaintRegion(StageMapDocument document, Vector2Int cell, StageRegionKind kind, uint stableId)
        {
            return TryPaintRegion(document, cell, kind, stableId, out _);
        }

        public static bool TryPaintRegion(
            StageMapDocument document,
            Vector2Int cell,
            StageRegionKind kind,
            uint stableId,
            out ContentValidationIssue issue)
        {
            if (!TryValidateDenseCellsForWrite(document, out issue)
                || !TryGetCellIndex(document, cell, out int index))
            {
                return false;
            }

            var data = document.Cells[index];
            if (kind == StageRegionKind.Source)
            {
                if (data.SourceRegionId == stableId)
                    return false;

                data.SourceRegionId = stableId;
            }
            else
            {
                if (data.DepositRegionId == stableId)
                    return false;

                data.DepositRegionId = stableId;
            }

            document.Cells[index] = data;
            return true;
        }

        public static bool PlaceAnchor(StageMapDocument document, StageRegionKind kind, uint stableId, Vector2Int cell, Vector2 anchorOffset)
        {
            if (document == null || stableId == 0u || !TryGetCellIndex(document, cell, out _))
                return false;

            var regions = kind == StageRegionKind.Source
                ? document.SourceRegions ?? Array.Empty<StageMapRegionData>()
                : document.DepositRegions ?? Array.Empty<StageMapRegionData>();
            var next = (StageMapRegionData[])regions.Clone();
            int index = FindRegion(next, stableId);
            var data = new StageMapRegionData
            {
                StableId = stableId,
                Active = true,
                AnchorCell = cell,
                AnchorOffset = anchorOffset,
            };

            if (index >= 0)
            {
                if (RegionEquals(next[index], data))
                    return false;

                next[index] = data;
            }
            else
            {
                Array.Resize(ref next, next.Length + 1);
                next[next.Length - 1] = data;
            }

            if (kind == StageRegionKind.Source)
                document.SourceRegions = next;
            else
                document.DepositRegions = next;
            return true;
        }

        public static bool PlacePlayerStart(StageMapDocument document, Vector2Int cell, Vector2 anchorOffset, float yawDeg)
        {
            if (document == null || !TryGetCellIndex(document, cell, out _))
                return false;

            var next = new StagePlayerStartLayoutData
            {
                Active = true,
                AnchorCell = cell,
                AnchorOffset = anchorOffset,
                YawDeg = NormalizeYaw(yawDeg),
            };

            if (PlayerStartEquals(document.PlayerStart, next))
                return false;

            document.PlayerStart = next;
            return true;
        }

        public static bool PlaceHazardActor(
            StageMapDocument document,
            uint owningSourceStableId,
            GameObject actorArchetypePrefab,
            Vector3 worldPosition,
            float localYawDeg,
            out int placementInstanceId)
        {
            placementInstanceId = 0;
            if (document == null || owningSourceStableId == 0u || actorArchetypePrefab == null)
                return false;

            var placements = document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
            placementInstanceId = GetNextHazardPlacementId(placements);
            var next = (StageMapHazardActorPlacementData[])placements.Clone();
            Array.Resize(ref next, next.Length + 1);
            next[next.Length - 1] = new StageMapHazardActorPlacementData
            {
                OwningSourceStableId = owningSourceStableId,
                PlacementInstanceId = placementInstanceId,
                ActorArchetypePrefab = actorArchetypePrefab,
                SourceLocalOffset = ComputeSourceLocalOffset(document, owningSourceStableId, worldPosition),
                LocalYawDeg = NormalizeYaw(localYawDeg),
            };
            document.HazardActorPlacements = next;
            return true;
        }

        public static bool PlacePresentationLink(
            StageMapDocument document,
            uint stableId,
            string presentationKey,
            StagePresentationPlacementMode placementMode,
            StagePresentationLinkKind linkKind,
            uint linkedStableId,
            Vector3 worldPosition,
            Vector3 euler,
            Vector3 scale)
        {
            if (document == null || stableId == 0u)
                return false;

            bool linked = placementMode == StagePresentationPlacementMode.LinkedToParent;
            var links = document.PresentationLinks ?? Array.Empty<StageMapPresentationLinkData>();
            var next = (StageMapPresentationLinkData[])links.Clone();
            int index = FindPresentation(next, stableId);
            var data = new StageMapPresentationLinkData
            {
                StableId = stableId,
                Active = true,
                PresentationKey = presentationKey != null ? presentationKey.Trim() : string.Empty,
                PlacementMode = placementMode,
                LinkKind = linked ? linkKind : StagePresentationLinkKind.None,
                LinkedStableId = linked ? linkedStableId : 0u,
                Position = linked
                    ? ComputeLinkedLocalPosition(document, linkKind, linkedStableId, worldPosition)
                    : worldPosition - document.Grid.Origin,
                Euler = euler,
                Scale = scale == Vector3.zero ? Vector3.one : scale,
            };

            if (index >= 0)
            {
                next[index] = data;
            }
            else
            {
                Array.Resize(ref next, next.Length + 1);
                next[next.Length - 1] = data;
            }

            document.PresentationLinks = next;
            return true;
        }

        public static uint GetNextPresentationStableId(StageMapDocument document)
        {
            uint max = 0u;
            var links = document != null ? document.PresentationLinks : null;
            if (links != null)
            {
                for (int i = 0; i < links.Length; i++)
                    max = Math.Max(max, links[i].StableId);
            }

            return Math.Max(1u, max + 1u);
        }

        public static bool TryValidateDenseCellsForWrite(StageMapDocument document, out ContentValidationIssue issue)
        {
            issue = default;
            if (document == null || document.Grid.Width <= 0 || document.Grid.Height <= 0)
            {
                issue = new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMC001",
                    document != null ? document.name : "(null)",
                    "Paint requires a document with positive grid dimensions.");
                return false;
            }

            long expectedLong = (long)document.Grid.Width * document.Grid.Height;
            if (expectedLong > int.MaxValue)
            {
                issue = new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMC002",
                    document.name,
                    "Paint requires a dense cell count that fits Int32.");
                return false;
            }

            int expected = (int)expectedLong;
            int actual = document.Cells != null ? document.Cells.Length : 0;
            if (actual != expected)
            {
                issue = new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMC003",
                    document.name,
                    $"Paint refused because Cells is not dense. cells={actual}, expected={expected}. Run the explicit array repair preview/apply command.");
                return false;
            }

            return true;
        }

        private static Vector3 ComputeSourceLocalOffset(StageMapDocument document, uint stableId, Vector3 worldPosition)
        {
            if (!TryGetRegionAnchorWorld(document, StageRegionKind.Source, stableId, out var anchorWorld))
                return worldPosition - document.Grid.Origin;

            return worldPosition - anchorWorld;
        }

        private static Vector3 ComputeLinkedLocalPosition(StageMapDocument document, StagePresentationLinkKind linkKind, uint linkedStableId, Vector3 worldPosition)
        {
            if (linkKind == StagePresentationLinkKind.Source
                && TryGetRegionAnchorWorld(document, StageRegionKind.Source, linkedStableId, out var sourceWorld))
            {
                return worldPosition - sourceWorld;
            }

            if (linkKind == StagePresentationLinkKind.Deposit
                && TryGetRegionAnchorWorld(document, StageRegionKind.Deposit, linkedStableId, out var depositWorld))
            {
                return worldPosition - depositWorld;
            }

            return worldPosition - document.Grid.Origin;
        }

        private static bool TryGetRegionAnchorWorld(StageMapDocument document, StageRegionKind kind, uint stableId, out Vector3 worldPosition)
        {
            worldPosition = default;
            var regions = kind == StageRegionKind.Source
                ? document.SourceRegions
                : document.DepositRegions;
            int index = FindRegion(regions, stableId);
            if (index < 0)
                return false;

            Vector3 center = GetCellCenterWorld(document, regions[index].AnchorCell);
            float cellSize = Mathf.Max(0.0001f, document.Grid.CellSize);
            worldPosition = center + new Vector3(regions[index].AnchorOffset.x * cellSize, 0f, regions[index].AnchorOffset.y * cellSize);
            return true;
        }

        private static int FindRegion(StageMapRegionData[] regions, uint stableId)
        {
            if (regions == null)
                return -1;

            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i].StableId == stableId)
                    return i;
            }

            return -1;
        }

        private static int FindPresentation(StageMapPresentationLinkData[] links, uint stableId)
        {
            if (links == null)
                return -1;

            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].StableId == stableId)
                    return i;
            }

            return -1;
        }

        private static int GetNextHazardPlacementId(StageMapHazardActorPlacementData[] placements)
        {
            int max = 0;
            if (placements != null)
            {
                for (int i = 0; i < placements.Length; i++)
                    max = Mathf.Max(max, placements[i].PlacementInstanceId);
            }

            return Mathf.Max(1, max + 1);
        }

        private static bool RegionEquals(StageMapRegionData left, StageMapRegionData right)
        {
            return left.StableId == right.StableId
                && left.Active == right.Active
                && left.AnchorCell == right.AnchorCell
                && left.AnchorOffset == right.AnchorOffset;
        }

        private static bool PlayerStartEquals(StagePlayerStartLayoutData left, StagePlayerStartLayoutData right)
        {
            return left.Active == right.Active
                && left.AnchorCell == right.AnchorCell
                && left.AnchorOffset == right.AnchorOffset
                && Mathf.Approximately(NormalizeYaw(left.YawDeg), NormalizeYaw(right.YawDeg));
        }

        private static float NormalizeYaw(float yawDeg)
        {
            float normalized = Mathf.Repeat(yawDeg, 360f);
            return Mathf.Approximately(normalized, 360f) ? 0f : normalized;
        }
    }
}
