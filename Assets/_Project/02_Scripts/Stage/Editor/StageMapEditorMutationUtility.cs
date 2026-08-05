using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    /// <summary>
    /// Central mutation gate for StageMapEditorWindow and Scene View tools.
    /// </summary>
    public static class StageMapEditorMutationUtility
    {
        public static bool TryPaintMovement(
            StageMapEditingSession session,
            Vector2Int cell,
            StageCellMovementFlags flags,
            out ContentValidationIssue issue)
        {
            if (!CanMutate(session, StageMapEditorLayer.Movement, out issue))
                return false;
            return StageMapDocumentCommandUtility.TryPaintMovement(session.Document, cell, flags, out issue);
        }

        public static bool TryPaintRegion(
            StageMapEditingSession session,
            Vector2Int cell,
            StageRegionKind kind,
            uint stableId,
            out ContentValidationIssue issue)
        {
            var layer = kind == StageRegionKind.Source ? StageMapEditorLayer.Source : StageMapEditorLayer.Deposit;
            if (!CanMutate(session, layer, out issue))
                return false;
            return StageMapDocumentCommandUtility.TryPaintRegion(session.Document, cell, kind, stableId, out issue);
        }

        public static bool TryPlaceAnchor(
            StageMapEditingSession session,
            StageRegionKind kind,
            uint stableId,
            Vector3 worldPosition,
            out ContentValidationIssue issue)
        {
            if (!CanMutateAnchor(session, kind, out issue)
                || !TryResolveSnappedPose(
                    session.Document,
                    worldPosition,
                    session.CenterRegionAnchors,
                    out var cell,
                    out var offset,
                    out issue))
            {
                return false;
            }

            return StageMapDocumentCommandUtility.PlaceAnchor(session.Document, kind, stableId, cell, offset);
        }

        public static bool TryPlacePlayerStart(
            StageMapEditingSession session,
            Vector3 worldPosition,
            float yawDeg,
            out ContentValidationIssue issue)
        {
            if (!CanMutate(session, StageMapEditorLayer.PlayerStart, out issue)
                || !TryResolveSnappedPose(
                    session.Document,
                    worldPosition,
                    session.CenterPlayerStart,
                    out var cell,
                    out var offset,
                    out issue))
            {
                return false;
            }

            return StageMapDocumentCommandUtility.PlacePlayerStart(session.Document, cell, offset, yawDeg);
        }

        public static bool TryPlaceHazardActor(
            StageMapEditingSession session,
            uint owningSourceStableId,
            GameObject prefab,
            Vector3 worldPosition,
            float yawDeg,
            out int placementInstanceId,
            out ContentValidationIssue issue)
        {
            placementInstanceId = 0;
            if (!CanMutate(session, StageMapEditorLayer.HazardActors, out issue))
                return false;
            return StageMapDocumentCommandUtility.PlaceHazardActor(
                session.Document,
                owningSourceStableId,
                prefab,
                worldPosition,
                yawDeg,
                out placementInstanceId);
        }

        public static bool TryPlacePresentation(
            StageMapEditingSession session,
            uint stableId,
            string presentationKey,
            StagePresentationPlacementMode placementMode,
            StagePresentationLinkKind linkKind,
            uint linkedStableId,
            Vector3 worldPosition,
            Vector3 euler,
            Vector3 scale,
            out ContentValidationIssue issue)
        {
            if (!CanMutate(session, StageMapEditorLayer.Presentations, out issue))
                return false;
            return StageMapDocumentCommandUtility.PlacePresentationLink(
                session.Document,
                stableId,
                presentationKey,
                placementMode,
                linkKind,
                linkedStableId,
                worldPosition,
                euler,
                scale);
        }

        public static bool TrySetCenterRegionAnchors(
            StageMapEditingSession session,
            bool enabled,
            out ContentValidationIssue issue)
        {
            issue = default;
            if (!HasDocument(session, out issue))
                return false;

            bool preferenceChanged = session.CenterRegionAnchors != enabled;
            if (!enabled)
            {
                session.CenterRegionAnchors = false;
                return preferenceChanged;
            }

            StageMapSelection selection = session.Selection;
            bool compatible = TryGetRegionKind(selection.Kind, out StageRegionKind kind);
            if (compatible && !CanMutateAnchor(session, kind, out issue))
                return false;

            bool documentChanged = compatible
                && TrySetRegionOffsetZero(session.Document, kind, selection.StableId);
            session.CenterRegionAnchors = true;
            return preferenceChanged || documentChanged;
        }

        public static bool TrySetCenterPlayerStart(
            StageMapEditingSession session,
            bool enabled,
            out ContentValidationIssue issue)
        {
            issue = default;
            if (!HasDocument(session, out issue))
                return false;

            bool preferenceChanged = session.CenterPlayerStart != enabled;
            if (!enabled)
            {
                session.CenterPlayerStart = false;
                return preferenceChanged;
            }

            bool selected = session.Selection.Kind == StageMapSelectionKind.PlayerStart;
            if (selected && !CanMutate(session, StageMapEditorLayer.PlayerStart, out issue))
                return false;

            bool documentChanged = selected
                && session.Document.PlayerStart.Active
                && session.Document.PlayerStart.AnchorOffset != Vector2.zero;
            if (documentChanged)
            {
                var player = session.Document.PlayerStart;
                player.AnchorOffset = Vector2.zero;
                session.Document.PlayerStart = player;
            }

            session.CenterPlayerStart = true;
            return preferenceChanged || documentChanged;
        }

        public static bool TrySetCellMovement(
            StageMapEditingSession session,
            Vector2Int cell,
            StageCellMovementFlags flags,
            out ContentValidationIssue issue)
        {
            return TryPaintMovement(session, cell, flags, out issue);
        }

        public static bool TrySetCellRegion(
            StageMapEditingSession session,
            Vector2Int cell,
            StageRegionKind kind,
            uint stableId,
            out ContentValidationIssue issue)
        {
            return TryPaintRegion(session, cell, kind, stableId, out issue);
        }

        public static bool TryUpdateRegion(
            StageMapEditingSession session,
            StageRegionKind kind,
            uint selectedStableId,
            StageMapRegionData value,
            out ContentValidationIssue issue)
        {
            if (!CanMutateAnchor(session, kind, out issue))
                return false;

            var regions = kind == StageRegionKind.Source
                ? session.Document.SourceRegions
                : session.Document.DepositRegions;
            if (!StageMapSelectionUtility.TryFindUniqueRegionIndex(regions, selectedStableId, out int index))
            {
                issue = BuildIssue("Selected region identity is missing or ambiguous.");
                return false;
            }

            value.StableId = selectedStableId;
            if (session.CenterRegionAnchors)
                value.AnchorOffset = Vector2.zero;
            if (RegionEquals(regions[index], value))
                return false;

            regions[index] = value;
            if (kind == StageRegionKind.Source)
                session.Document.SourceRegions = regions;
            else
                session.Document.DepositRegions = regions;
            return true;
        }

        public static bool TryUpdatePlayerStart(
            StageMapEditingSession session,
            StagePlayerStartLayoutData value,
            out ContentValidationIssue issue)
        {
            if (!CanMutate(session, StageMapEditorLayer.PlayerStart, out issue))
                return false;
            if (session.CenterPlayerStart)
                value.AnchorOffset = Vector2.zero;
            if (PlayerEquals(session.Document.PlayerStart, value))
                return false;
            session.Document.PlayerStart = value;
            return true;
        }

        public static bool TryUpdateHazard(
            StageMapEditingSession session,
            StageMapSelection identity,
            StageMapHazardActorPlacementData value,
            out ContentValidationIssue issue)
        {
            if (!CanMutate(session, StageMapEditorLayer.HazardActors, out issue))
                return false;
            if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(
                    session.Document.HazardActorPlacements,
                    identity.OwningSourceStableId,
                    identity.PlacementInstanceId,
                    out int index))
            {
                issue = BuildIssue("Selected HazardActor identity is missing or ambiguous.");
                return false;
            }

            var placements = session.Document.HazardActorPlacements;
            if (HazardEquals(placements[index], value))
                return false;
            placements[index] = value;
            session.Document.HazardActorPlacements = placements;
            session.Select(StageMapSelection.ForHazard(value.OwningSourceStableId, value.PlacementInstanceId));
            return true;
        }

        public static bool TryUpdatePresentation(
            StageMapEditingSession session,
            StageMapSelection identity,
            StageMapPresentationLinkData value,
            out ContentValidationIssue issue)
        {
            if (!CanMutate(session, StageMapEditorLayer.Presentations, out issue))
                return false;
            if (!StageMapSelectionUtility.TryFindUniquePresentationIndex(
                    session.Document.PresentationLinks,
                    identity.StableId,
                    out int index))
            {
                issue = BuildIssue("Selected Presentation identity is missing or ambiguous.");
                return false;
            }

            var links = session.Document.PresentationLinks;
            if (PresentationEquals(links[index], value))
                return false;
            links[index] = value;
            session.Document.PresentationLinks = links;
            session.Select(StageMapSelection.ForPresentation(value.StableId));
            return true;
        }

        public static bool TryMoveSelection(
            StageMapEditingSession session,
            Vector3 worldPosition,
            float yawDeg,
            Vector3 euler,
            out ContentValidationIssue issue)
        {
            issue = default;
            if (!HasDocument(session, out issue))
                return false;

            StageMapSelection selection = session.Selection;
            if (!CanMutateSelection(session, selection, out issue))
                return false;

            switch (selection.Kind)
            {
                case StageMapSelectionKind.SourceAnchor:
                    return TryMoveAnchor(session, StageRegionKind.Source, selection.StableId, worldPosition, out issue);
                case StageMapSelectionKind.DepositAnchor:
                    return TryMoveAnchor(session, StageRegionKind.Deposit, selection.StableId, worldPosition, out issue);
                case StageMapSelectionKind.PlayerStart:
                    return TryMovePlayer(session, worldPosition, yawDeg, out issue);
                case StageMapSelectionKind.HazardActor:
                    return TryMoveHazard(session.Document, selection, worldPosition, yawDeg);
                case StageMapSelectionKind.Presentation:
                    return TryMovePresentation(session.Document, selection, worldPosition, euler);
                default:
                    issue = BuildIssue("Selected element cannot be moved by the Scene View tool.");
                    return false;
            }
        }

        public static bool TryDeleteSelection(StageMapEditingSession session, out ContentValidationIssue issue)
        {
            issue = default;
            if (!HasDocument(session, out issue))
                return false;

            StageMapSelection selection = session.Selection;
            if (!CanMutateSelection(session, selection, out issue))
                return false;

            bool changed;
            if (selection.Kind == StageMapSelectionKind.HazardActor
                && StageMapSelectionUtility.TryFindUniqueHazardIndex(
                    session.Document.HazardActorPlacements,
                    selection.OwningSourceStableId,
                    selection.PlacementInstanceId,
                    out int hazardIndex))
            {
                var placements = session.Document.HazardActorPlacements;
                changed = RemoveAt(ref placements, hazardIndex);
                if (changed)
                    session.Document.HazardActorPlacements = placements;
            }
            else if (selection.Kind == StageMapSelectionKind.Presentation
                && StageMapSelectionUtility.TryFindUniquePresentationIndex(
                    session.Document.PresentationLinks,
                    selection.StableId,
                    out int presentationIndex))
            {
                var links = session.Document.PresentationLinks;
                changed = RemoveAt(ref links, presentationIndex);
                if (changed)
                    session.Document.PresentationLinks = links;
            }
            else
            {
                changed = false;
            }

            if (changed)
                session.Select(StageMapSelection.None);
            else if (issue.Code == null)
                issue = BuildIssue("Selected element cannot be deleted by the Scene View tool.");
            return changed;
        }

        public static bool CanApplyFix(
            StageMapEditingSession session,
            ContentValidationIssue sourceIssue,
            StageMapIssueTarget target,
            out ContentValidationIssue issue)
        {
            switch (sourceIssue.Code)
            {
                case "STG003":
                case "SMD010":
                    return CanMutate(session, StageMapEditorLayer.Grid, out issue);
                case "STG009":
                    return CanMutateAnchor(session, StageRegionKind.Source, out issue);
                case "STG010":
                    return CanMutateAnchor(session, StageRegionKind.Deposit, out issue);
                case "STG012":
                    if (target.Kind == StageMapIssueTargetKind.SourceAnchor
                        || target.Kind == StageMapIssueTargetKind.SourceRegion)
                    {
                        return CanMutateAnchor(session, StageRegionKind.Source, out issue);
                    }
                    if (target.Kind == StageMapIssueTargetKind.DepositAnchor
                        || target.Kind == StageMapIssueTargetKind.DepositRegion)
                    {
                        return CanMutateAnchor(session, StageRegionKind.Deposit, out issue);
                    }
                    issue = BuildIssue("Quick-fix target cannot be resolved.");
                    return false;
                case "STG015":
                case "STG016":
                case "STG017":
                    return CanMutate(session, StageMapEditorLayer.PlayerStart, out issue);
                default:
                    issue = BuildIssue("Quick-fix is not supported by the Stage Map Editor mutation policy.");
                    return false;
            }
        }

        public static bool TryApplyFix(
            StageMapEditingSession session,
            ContentValidationIssue sourceIssue,
            StageMapIssueTarget target,
            out ContentValidationIssue issue)
        {
            if (!CanApplyFix(session, sourceIssue, target, out issue))
                return false;
            return StageMapDocumentFixUtility.ApplyFix(session.Document, sourceIssue);
        }

        private static bool TryMoveAnchor(
            StageMapEditingSession session,
            StageRegionKind kind,
            uint stableId,
            Vector3 worldPosition,
            out ContentValidationIssue issue)
        {
            if (!TryResolveSnappedPose(
                    session.Document,
                    worldPosition,
                    session.CenterRegionAnchors,
                    out var cell,
                    out var offset,
                    out issue))
            {
                return false;
            }
            return StageMapDocumentCommandUtility.PlaceAnchor(session.Document, kind, stableId, cell, offset);
        }

        private static bool TryMovePlayer(
            StageMapEditingSession session,
            Vector3 worldPosition,
            float yawDeg,
            out ContentValidationIssue issue)
        {
            if (!TryResolveSnappedPose(
                    session.Document,
                    worldPosition,
                    session.CenterPlayerStart,
                    out var cell,
                    out var offset,
                    out issue))
            {
                return false;
            }
            return StageMapDocumentCommandUtility.PlacePlayerStart(session.Document, cell, offset, yawDeg);
        }

        private static bool TryMoveHazard(
            StageMapDocument document,
            StageMapSelection selection,
            Vector3 worldPosition,
            float yawDeg)
        {
            var placements = document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
            if (!StageMapSelectionUtility.TryFindUniqueHazardIndex(
                    placements,
                    selection.OwningSourceStableId,
                    selection.PlacementInstanceId,
                    out int index))
            {
                return false;
            }

            var placement = placements[index];
            Vector3 offset = StageMapSelectionUtility.ToSourceLocal(document, placement.OwningSourceStableId, worldPosition);
            float yaw = NormalizeYaw(yawDeg);
            if (placement.SourceLocalOffset == offset && Mathf.Approximately(NormalizeYaw(placement.LocalYawDeg), yaw))
                return false;

            placement.SourceLocalOffset = offset;
            placement.LocalYawDeg = yaw;
            placements[index] = placement;
            document.HazardActorPlacements = placements;
            return true;
        }

        private static bool TryMovePresentation(
            StageMapDocument document,
            StageMapSelection selection,
            Vector3 worldPosition,
            Vector3 euler)
        {
            var links = document.PresentationLinks ?? Array.Empty<StageMapPresentationLinkData>();
            if (!StageMapSelectionUtility.TryFindUniquePresentationIndex(links, selection.StableId, out int index))
                return false;

            var link = links[index];
            Vector3 local = StageMapSelectionUtility.ToPresentationLocal(document, link, worldPosition);
            if (link.Position == local && link.Euler == euler)
                return false;

            link.Position = local;
            link.Euler = euler;
            links[index] = link;
            document.PresentationLinks = links;
            return true;
        }

        private static bool TryResolveSnappedPose(
            StageMapDocument document,
            Vector3 worldPosition,
            bool lockToCenter,
            out Vector2Int cell,
            out Vector2 offset,
            out ContentValidationIssue issue)
        {
            offset = default;
            if (!StageMapDocumentCommandUtility.TryWorldToCell(document, worldPosition, out cell))
            {
                issue = BuildIssue("World position is outside the document grid.");
                return false;
            }

            offset = lockToCenter
                ? Vector2.zero
                : StageMapDocumentCommandUtility.ComputeAnchorOffset(document, cell, worldPosition);
            issue = default;
            return true;
        }

        private static bool CanMutateSelection(
            StageMapEditingSession session,
            StageMapSelection selection,
            out ContentValidationIssue issue)
        {
            if (StageMapEditingPolicy.CanMutateSelection(session, selection, out StageMapEditorLayer lockedLayer))
            {
                issue = default;
                return true;
            }

            issue = BuildIssue($"{lockedLayer} layer is locked or the selection is not mutable.");
            return false;
        }

        private static bool CanMutateAnchor(
            StageMapEditingSession session,
            StageRegionKind kind,
            out ContentValidationIssue issue)
        {
            if (!HasDocument(session, out issue))
                return false;
            if (StageMapEditingPolicy.CanMutateAnchor(session, kind, out StageMapEditorLayer lockedLayer))
                return true;
            issue = BuildIssue($"{lockedLayer} layer is locked.");
            return false;
        }

        private static bool CanMutate(
            StageMapEditingSession session,
            StageMapEditorLayer layer,
            out ContentValidationIssue issue)
        {
            if (!HasDocument(session, out issue))
                return false;
            if (!StageMapEditingPolicy.IsLayerLocked(session, layer))
                return true;
            issue = BuildIssue($"{layer} layer is locked.");
            return false;
        }

        private static bool HasDocument(StageMapEditingSession session, out ContentValidationIssue issue)
        {
            issue = default;
            if (session != null && session.Document != null)
                return true;
            issue = BuildIssue("No StageMapDocument is loaded.");
            return false;
        }

        private static bool TryGetRegionKind(StageMapSelectionKind kind, out StageRegionKind regionKind)
        {
            if (kind == StageMapSelectionKind.SourceRegion || kind == StageMapSelectionKind.SourceAnchor)
            {
                regionKind = StageRegionKind.Source;
                return true;
            }
            if (kind == StageMapSelectionKind.DepositRegion || kind == StageMapSelectionKind.DepositAnchor)
            {
                regionKind = StageRegionKind.Deposit;
                return true;
            }
            regionKind = default;
            return false;
        }

        private static bool TrySetRegionOffsetZero(
            StageMapDocument document,
            StageRegionKind kind,
            uint stableId)
        {
            var regions = kind == StageRegionKind.Source ? document.SourceRegions : document.DepositRegions;
            if (!StageMapSelectionUtility.TryFindUniqueRegionIndex(regions, stableId, out int index)
                || regions[index].AnchorOffset == Vector2.zero)
            {
                return false;
            }

            regions[index].AnchorOffset = Vector2.zero;
            if (kind == StageRegionKind.Source)
                document.SourceRegions = regions;
            else
                document.DepositRegions = regions;
            return true;
        }

        private static ContentValidationIssue BuildIssue(string message)
        {
            return new ContentValidationIssue(ContentValidationSeverity.Error, "SML001", "StageMapEditor", message);
        }

        private static bool RemoveAt<T>(ref T[] values, int index)
        {
            values = values ?? Array.Empty<T>();
            if (index < 0 || index >= values.Length)
                return false;
            for (int i = index + 1; i < values.Length; i++)
                values[i - 1] = values[i];
            Array.Resize(ref values, values.Length - 1);
            return true;
        }

        private static bool RegionEquals(StageMapRegionData left, StageMapRegionData right)
        {
            return left.StableId == right.StableId
                && left.Active == right.Active
                && left.AnchorCell == right.AnchorCell
                && left.AnchorOffset == right.AnchorOffset;
        }

        private static bool PlayerEquals(StagePlayerStartLayoutData left, StagePlayerStartLayoutData right)
        {
            return left.Active == right.Active
                && left.AnchorCell == right.AnchorCell
                && left.AnchorOffset == right.AnchorOffset
                && Mathf.Approximately(NormalizeYaw(left.YawDeg), NormalizeYaw(right.YawDeg));
        }

        private static bool HazardEquals(
            StageMapHazardActorPlacementData left,
            StageMapHazardActorPlacementData right)
        {
            return left.OwningSourceStableId == right.OwningSourceStableId
                && left.PlacementInstanceId == right.PlacementInstanceId
                && left.ActorArchetypePrefab == right.ActorArchetypePrefab
                && left.SourceLocalOffset == right.SourceLocalOffset
                && Mathf.Approximately(NormalizeYaw(left.LocalYawDeg), NormalizeYaw(right.LocalYawDeg));
        }

        private static bool PresentationEquals(
            StageMapPresentationLinkData left,
            StageMapPresentationLinkData right)
        {
            return left.StableId == right.StableId
                && left.Active == right.Active
                && left.PresentationKey == right.PresentationKey
                && left.PlacementMode == right.PlacementMode
                && left.LinkKind == right.LinkKind
                && left.LinkedStableId == right.LinkedStableId
                && left.Position == right.Position
                && left.Euler == right.Euler
                && left.Scale == right.Scale;
        }

        private static float NormalizeYaw(float yawDeg)
        {
            return Mathf.Repeat(yawDeg, 360f);
        }
    }
}
