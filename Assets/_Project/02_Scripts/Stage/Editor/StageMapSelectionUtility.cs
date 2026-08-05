using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapEditingPolicy
    {
        public static bool IsLayerLocked(StageMapEditingSession session, StageMapEditorLayer layer)
        {
            if (session == null)
                return true;

            switch (layer)
            {
                case StageMapEditorLayer.Grid: return session.LockGridLayer;
                case StageMapEditorLayer.Movement: return session.LockMovementLayer;
                case StageMapEditorLayer.Source: return session.LockSourceLayer;
                case StageMapEditorLayer.Deposit: return session.LockDepositLayer;
                case StageMapEditorLayer.Anchors: return session.LockAnchorLayer;
                case StageMapEditorLayer.PlayerStart: return session.LockPlayerStartLayer;
                case StageMapEditorLayer.HazardActors: return session.LockHazardActorLayer;
                case StageMapEditorLayer.Presentations: return session.LockPresentationLayer;
                default: return true;
            }
        }

        public static bool IsLayerVisible(StageMapEditingSession session, StageMapEditorLayer layer)
        {
            if (session == null)
                return false;

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

        public static bool CanMutateAnchor(StageMapEditingSession session, StageRegionKind kind, out StageMapEditorLayer lockedLayer)
        {
            StageMapEditorLayer owner = kind == StageRegionKind.Source
                ? StageMapEditorLayer.Source
                : StageMapEditorLayer.Deposit;
            if (IsLayerLocked(session, owner))
            {
                lockedLayer = owner;
                return false;
            }

            if (IsLayerLocked(session, StageMapEditorLayer.Anchors))
            {
                lockedLayer = StageMapEditorLayer.Anchors;
                return false;
            }

            lockedLayer = default;
            return true;
        }

        public static bool CanMutateSelection(
            StageMapEditingSession session,
            StageMapSelection selection,
            out StageMapEditorLayer lockedLayer)
        {
            switch (selection.Kind)
            {
                case StageMapSelectionKind.SourceAnchor:
                case StageMapSelectionKind.SourceRegion:
                    return CanMutateAnchor(session, StageRegionKind.Source, out lockedLayer);
                case StageMapSelectionKind.DepositAnchor:
                case StageMapSelectionKind.DepositRegion:
                    return CanMutateAnchor(session, StageRegionKind.Deposit, out lockedLayer);
                case StageMapSelectionKind.PlayerStart:
                    return IsUnlocked(session, StageMapEditorLayer.PlayerStart, out lockedLayer);
                case StageMapSelectionKind.HazardActor:
                    return IsUnlocked(session, StageMapEditorLayer.HazardActors, out lockedLayer);
                case StageMapSelectionKind.Presentation:
                    return IsUnlocked(session, StageMapEditorLayer.Presentations, out lockedLayer);
                case StageMapSelectionKind.Cell:
                    return IsUnlocked(session, StageMapEditorLayer.Grid, out lockedLayer);
                default:
                    lockedLayer = StageMapEditorLayer.Grid;
                    return false;
            }
        }

        public static bool IsSelectionVisible(StageMapEditingSession session, StageMapSelection selection)
        {
            switch (selection.Kind)
            {
                case StageMapSelectionKind.Cell:
                    return IsLayerVisible(session, StageMapEditorLayer.Grid);
                case StageMapSelectionKind.SourceRegion:
                    return IsLayerVisible(session, StageMapEditorLayer.Source);
                case StageMapSelectionKind.DepositRegion:
                    return IsLayerVisible(session, StageMapEditorLayer.Deposit);
                case StageMapSelectionKind.SourceAnchor:
                    return IsLayerVisible(session, StageMapEditorLayer.Source)
                        && IsLayerVisible(session, StageMapEditorLayer.Anchors);
                case StageMapSelectionKind.DepositAnchor:
                    return IsLayerVisible(session, StageMapEditorLayer.Deposit)
                        && IsLayerVisible(session, StageMapEditorLayer.Anchors);
                case StageMapSelectionKind.PlayerStart:
                    return IsLayerVisible(session, StageMapEditorLayer.PlayerStart);
                case StageMapSelectionKind.HazardActor:
                    return IsLayerVisible(session, StageMapEditorLayer.HazardActors);
                case StageMapSelectionKind.Presentation:
                    return IsLayerVisible(session, StageMapEditorLayer.Presentations);
                default:
                    return false;
            }
        }

        public static StageMapEditorLayer GetMutationLayer(StageMapSelectionKind kind)
        {
            switch (kind)
            {
                case StageMapSelectionKind.Cell: return StageMapEditorLayer.Grid;
                case StageMapSelectionKind.SourceRegion: return StageMapEditorLayer.Source;
                case StageMapSelectionKind.DepositRegion: return StageMapEditorLayer.Deposit;
                case StageMapSelectionKind.SourceAnchor:
                case StageMapSelectionKind.DepositAnchor: return StageMapEditorLayer.Anchors;
                case StageMapSelectionKind.PlayerStart: return StageMapEditorLayer.PlayerStart;
                case StageMapSelectionKind.HazardActor: return StageMapEditorLayer.HazardActors;
                case StageMapSelectionKind.Presentation: return StageMapEditorLayer.Presentations;
                default: return StageMapEditorLayer.Grid;
            }
        }

        private static bool IsUnlocked(
            StageMapEditingSession session,
            StageMapEditorLayer layer,
            out StageMapEditorLayer lockedLayer)
        {
            lockedLayer = layer;
            return !IsLayerLocked(session, layer);
        }
    }

    public static class StageMapSelectionUtility
    {
        public static bool TryHitTest(
            StageMapDocument document,
            StageMapEditingSession session,
            Vector3 worldPosition,
            float maxDistance,
            out StageMapSelection selection)
        {
            selection = StageMapSelection.None;
            if (document == null || session == null)
                return false;

            float maxDistanceSq = maxDistance * maxDistance;
            if (StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.HazardActors)
                && TryFindNearestHazard(document, worldPosition, maxDistanceSq, out int hazardIndex))
            {
                var placement = document.HazardActorPlacements[hazardIndex];
                selection = StageMapSelection.ForHazard(placement.OwningSourceStableId, placement.PlacementInstanceId);
                return true;
            }

            if (StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.Presentations)
                && TryFindNearestPresentation(document, worldPosition, maxDistanceSq, out int presentationIndex))
            {
                selection = StageMapSelection.ForPresentation(document.PresentationLinks[presentationIndex].StableId);
                return true;
            }

            if (StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.PlayerStart)
                && document.PlayerStart.Active
                && DistanceSquaredXZ(GetPlayerStartWorld(document), worldPosition) <= maxDistanceSq)
            {
                selection = StageMapSelection.ForPlayerStart();
                return true;
            }

            if (StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.Anchors)
                && TryFindNearestAnchor(document, session, worldPosition, maxDistanceSq, out selection))
            {
                return true;
            }

            if (!StageMapDocumentCommandUtility.TryWorldToCell(document, worldPosition, out Vector2Int cell)
                || !StageMapDocumentCommandUtility.TryGetCellIndex(document, cell, out int cellIndex))
            {
                return false;
            }

            var cellData = document.Cells != null && cellIndex < document.Cells.Length
                ? document.Cells[cellIndex]
                : default;
            if (session.SelectedLayer == StageMapEditorLayer.Source
                && StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.Source)
                && cellData.SourceRegionId != 0u)
            {
                selection = StageMapSelection.ForRegion(StageRegionKind.Source, cellData.SourceRegionId);
                return true;
            }

            if (session.SelectedLayer == StageMapEditorLayer.Deposit
                && StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.Deposit)
                && cellData.DepositRegionId != 0u)
            {
                selection = StageMapSelection.ForRegion(StageRegionKind.Deposit, cellData.DepositRegionId);
                return true;
            }

            if (!StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.Grid))
                return false;

            selection = StageMapSelection.ForCell(cell);
            return true;
        }

        public static bool TryGetSelectionWorld(
            StageMapDocument document,
            StageMapSelection selection,
            out Vector3 worldPosition)
        {
            worldPosition = document != null ? document.Grid.Origin : Vector3.zero;
            if (document == null)
                return false;

            switch (selection.Kind)
            {
                case StageMapSelectionKind.Cell:
                    if (!StageMapDocumentCommandUtility.TryGetCellIndex(document, selection.Cell, out _))
                        return false;
                    worldPosition = StageMapDocumentCommandUtility.GetCellCenterWorld(document, selection.Cell);
                    return true;
                case StageMapSelectionKind.SourceRegion:
                case StageMapSelectionKind.SourceAnchor:
                    if (!TryFindUniqueRegionIndex(document.SourceRegions, selection.StableId, out _))
                        return false;
                    worldPosition = GetRegionAnchorWorld(document, StageRegionKind.Source, selection.StableId);
                    return true;
                case StageMapSelectionKind.DepositRegion:
                case StageMapSelectionKind.DepositAnchor:
                    if (!TryFindUniqueRegionIndex(document.DepositRegions, selection.StableId, out _))
                        return false;
                    worldPosition = GetRegionAnchorWorld(document, StageRegionKind.Deposit, selection.StableId);
                    return true;
                case StageMapSelectionKind.PlayerStart:
                    if (!document.PlayerStart.Active)
                        return false;
                    worldPosition = GetPlayerStartWorld(document);
                    return true;
                case StageMapSelectionKind.HazardActor:
                    if (!TryFindUniqueHazardIndex(
                            document.HazardActorPlacements,
                            selection.OwningSourceStableId,
                            selection.PlacementInstanceId,
                            out int hazardIndex))
                    {
                        return false;
                    }
                    worldPosition = GetHazardActorWorld(document, hazardIndex);
                    return true;
                case StageMapSelectionKind.Presentation:
                    if (!TryFindUniquePresentationIndex(document.PresentationLinks, selection.StableId, out int presentationIndex))
                        return false;
                    worldPosition = GetPresentationWorld(document, presentationIndex);
                    return true;
                case StageMapSelectionKind.Document:
                    worldPosition = document.Grid.Origin + new Vector3(
                        document.Grid.Width * document.Grid.CellSize * 0.5f,
                        0f,
                        document.Grid.Height * document.Grid.CellSize * 0.5f);
                    return true;
                default:
                    return false;
            }
        }

        public static string GetSelectionSummary(StageMapDocument document, StageMapSelection selection)
        {
            switch (selection.Kind)
            {
                case StageMapSelectionKind.None: return "No selection";
                case StageMapSelectionKind.Cell: return $"Cell ({selection.Cell.x}, {selection.Cell.y})";
                case StageMapSelectionKind.SourceRegion: return $"Source {selection.StableId}";
                case StageMapSelectionKind.DepositRegion: return $"Deposit {selection.StableId}";
                case StageMapSelectionKind.SourceAnchor: return $"Source {selection.StableId} Anchor";
                case StageMapSelectionKind.DepositAnchor: return $"Deposit {selection.StableId} Anchor";
                case StageMapSelectionKind.PlayerStart: return "PlayerStart";
                case StageMapSelectionKind.HazardActor:
                    return $"Source {selection.OwningSourceStableId} / Placement {selection.PlacementInstanceId}";
                case StageMapSelectionKind.Presentation:
                    if (document != null
                        && TryFindUniquePresentationIndex(document.PresentationLinks, selection.StableId, out int index))
                    {
                        return $"{selection.StableId} / {document.PresentationLinks[index].PresentationKey}";
                    }
                    return $"Presentation {selection.StableId}";
                case StageMapSelectionKind.Document: return document != null ? document.name : "Document";
                case StageMapSelectionKind.TargetAsset:
                    return selection.TargetAsset != null ? selection.TargetAsset.name : "Target Asset";
                default: return selection.Kind.ToString();
            }
        }

        public static Vector3 GetRegionAnchorWorld(StageMapDocument document, StageRegionKind kind, uint stableId)
        {
            var regions = kind == StageRegionKind.Source ? document?.SourceRegions : document?.DepositRegions;
            if (document == null || !TryFindUniqueRegionIndex(regions, stableId, out int index))
                return document != null ? document.Grid.Origin : Vector3.zero;

            var region = regions[index];
            Vector3 center = StageMapDocumentCommandUtility.GetCellCenterWorld(document, region.AnchorCell);
            return center + new Vector3(
                region.AnchorOffset.x * document.Grid.CellSize,
                0f,
                region.AnchorOffset.y * document.Grid.CellSize);
        }

        public static Vector3 GetPlayerStartWorld(StageMapDocument document)
        {
            if (document == null)
                return Vector3.zero;

            Vector3 center = StageMapDocumentCommandUtility.GetCellCenterWorld(document, document.PlayerStart.AnchorCell);
            return center + new Vector3(
                document.PlayerStart.AnchorOffset.x * document.Grid.CellSize,
                0f,
                document.PlayerStart.AnchorOffset.y * document.Grid.CellSize);
        }

        public static Vector3 GetHazardActorWorld(StageMapDocument document, int index)
        {
            if (document == null
                || document.HazardActorPlacements == null
                || index < 0
                || index >= document.HazardActorPlacements.Length)
            {
                return Vector3.zero;
            }

            var placement = document.HazardActorPlacements[index];
            return GetRegionAnchorWorld(document, StageRegionKind.Source, placement.OwningSourceStableId)
                + placement.SourceLocalOffset;
        }

        public static Vector3 GetPresentationWorld(StageMapDocument document, int index)
        {
            if (document == null
                || document.PresentationLinks == null
                || index < 0
                || index >= document.PresentationLinks.Length)
            {
                return Vector3.zero;
            }

            var link = document.PresentationLinks[index];
            Vector3 origin = document.Grid.Origin;
            if (link.PlacementMode == StagePresentationPlacementMode.LinkedToParent)
            {
                if (link.LinkKind == StagePresentationLinkKind.Source)
                    origin = GetRegionAnchorWorld(document, StageRegionKind.Source, link.LinkedStableId);
                else if (link.LinkKind == StagePresentationLinkKind.Deposit)
                    origin = GetRegionAnchorWorld(document, StageRegionKind.Deposit, link.LinkedStableId);
            }

            return origin + link.Position;
        }

        public static Vector3 ToSourceLocal(StageMapDocument document, uint sourceStableId, Vector3 worldPosition)
        {
            return worldPosition - GetRegionAnchorWorld(document, StageRegionKind.Source, sourceStableId);
        }

        public static Vector3 ToPresentationLocal(StageMapDocument document, StageMapPresentationLinkData link, Vector3 worldPosition)
        {
            Vector3 origin = document != null ? document.Grid.Origin : Vector3.zero;
            if (document != null && link.PlacementMode == StagePresentationPlacementMode.LinkedToParent)
            {
                if (link.LinkKind == StagePresentationLinkKind.Source)
                    origin = GetRegionAnchorWorld(document, StageRegionKind.Source, link.LinkedStableId);
                else if (link.LinkKind == StagePresentationLinkKind.Deposit)
                    origin = GetRegionAnchorWorld(document, StageRegionKind.Deposit, link.LinkedStableId);
            }

            return worldPosition - origin;
        }

        public static int FindRegionIndex(StageMapRegionData[] regions, uint stableId)
        {
            return TryFindUniqueRegionIndex(regions, stableId, out int index) ? index : -1;
        }

        public static bool TryFindUniqueRegionIndex(StageMapRegionData[] regions, uint stableId, out int index)
        {
            index = -1;
            if (regions == null || stableId == 0u)
                return false;

            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i].StableId != stableId)
                    continue;
                if (index >= 0)
                {
                    index = -1;
                    return false;
                }
                index = i;
            }
            return index >= 0;
        }

        public static bool TryFindUniqueHazardIndex(
            StageMapHazardActorPlacementData[] placements,
            uint owningSourceStableId,
            int placementInstanceId,
            out int index)
        {
            index = -1;
            if (placements == null || owningSourceStableId == 0u || placementInstanceId <= 0)
                return false;

            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].OwningSourceStableId != owningSourceStableId
                    || placements[i].PlacementInstanceId != placementInstanceId)
                {
                    continue;
                }

                if (index >= 0)
                {
                    index = -1;
                    return false;
                }
                index = i;
            }
            return index >= 0;
        }

        public static bool TryFindUniquePresentationIndex(
            StageMapPresentationLinkData[] links,
            uint stableId,
            out int index)
        {
            index = -1;
            if (links == null || stableId == 0u)
                return false;

            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].StableId != stableId)
                    continue;
                if (index >= 0)
                {
                    index = -1;
                    return false;
                }
                index = i;
            }
            return index >= 0;
        }

        private static bool TryFindNearestHazard(StageMapDocument document, Vector3 world, float maxDistanceSq, out int index)
        {
            index = -1;
            var placements = document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
            float best = maxDistanceSq;
            for (int i = 0; i < placements.Length; i++)
            {
                float distance = DistanceSquaredXZ(GetHazardActorWorld(document, i), world);
                if (distance > best)
                    continue;
                best = distance;
                index = i;
            }
            return index >= 0;
        }

        private static bool TryFindNearestPresentation(StageMapDocument document, Vector3 world, float maxDistanceSq, out int index)
        {
            index = -1;
            var links = document.PresentationLinks ?? Array.Empty<StageMapPresentationLinkData>();
            float best = maxDistanceSq;
            for (int i = 0; i < links.Length; i++)
            {
                if (!links[i].Active)
                    continue;
                float distance = DistanceSquaredXZ(GetPresentationWorld(document, i), world);
                if (distance > best)
                    continue;
                best = distance;
                index = i;
            }
            return index >= 0;
        }

        private static bool TryFindNearestAnchor(
            StageMapDocument document,
            StageMapEditingSession session,
            Vector3 world,
            float maxDistanceSq,
            out StageMapSelection selection)
        {
            selection = StageMapSelection.None;
            float best = maxDistanceSq;
            if (StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.Source))
                FindNearestAnchor(document, document.SourceRegions, StageRegionKind.Source, world, ref best, ref selection);
            if (StageMapEditingPolicy.IsLayerVisible(session, StageMapEditorLayer.Deposit))
                FindNearestAnchor(document, document.DepositRegions, StageRegionKind.Deposit, world, ref best, ref selection);
            return selection.Kind != StageMapSelectionKind.None;
        }

        private static void FindNearestAnchor(
            StageMapDocument document,
            StageMapRegionData[] regions,
            StageRegionKind kind,
            Vector3 world,
            ref float best,
            ref StageMapSelection selection)
        {
            if (regions == null)
                return;
            for (int i = 0; i < regions.Length; i++)
            {
                if (!regions[i].Active)
                    continue;
                float distance = DistanceSquaredXZ(GetRegionAnchorWorld(document, kind, regions[i].StableId), world);
                if (distance > best)
                    continue;
                best = distance;
                selection = StageMapSelection.ForAnchor(kind, regions[i].StableId);
            }
        }

        private static float DistanceSquaredXZ(Vector3 left, Vector3 right)
        {
            float x = left.x - right.x;
            float z = left.z - right.z;
            return (x * x) + (z * z);
        }
    }
}
