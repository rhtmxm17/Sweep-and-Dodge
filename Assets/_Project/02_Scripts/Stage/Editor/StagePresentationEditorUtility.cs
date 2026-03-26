using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StagePresentationEditorUtility
    {
        private static readonly string[] EmptyKeys = Array.Empty<string>();

        public static StageLayoutRootMarker FindRoot(StagePresentationMarker marker)
        {
            return marker != null ? marker.GetComponentInParent<StageLayoutRootMarker>() : null;
        }

        public static StageLayoutStageMarker FindOwningStage(StagePresentationMarker marker)
        {
            return marker != null ? marker.GetComponentInParent<StageLayoutStageMarker>() : null;
        }

        public static bool TryResolveCatalog(StagePresentationMarker marker, out StagePresentationCatalogSO catalog)
        {
            catalog = FindRoot(marker)?.TargetPresentationCatalog;
            return catalog != null;
        }

        public static string[] GetPresentationKeys(StagePresentationCatalogSO catalog)
        {
            if (catalog == null || catalog.Entries == null || catalog.Entries.Length == 0)
                return EmptyKeys;

            return catalog.Entries
                .Where(x => !string.IsNullOrWhiteSpace(x.PresentationKey))
                .Select(x => x.PresentationKey.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool TryResolveEntry(StagePresentationCatalogSO catalog, string presentationKey, out StagePresentationCatalogEntry entry)
        {
            entry = default;
            if (catalog == null || catalog.Entries == null || string.IsNullOrWhiteSpace(presentationKey))
                return false;

            string trimmed = presentationKey.Trim();
            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                var candidate = catalog.Entries[i];
                if (!string.Equals(candidate.PresentationKey?.Trim(), trimmed, StringComparison.Ordinal))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        public static bool TryFindLinkedParent(Transform transform, out StagePresentationLinkKind linkKind, out uint linkedStableId, out Transform linkedParent)
        {
            linkKind = StagePresentationLinkKind.None;
            linkedStableId = 0u;
            linkedParent = null;

            var current = transform != null ? transform.parent : null;
            while (current != null)
            {
                if (current.TryGetComponent<StageRegionAnchorMarker>(out var anchor))
                {
                    if (!TryResolveAnchorStableId(anchor, out uint resolvedStableId))
                    {
                        current = current.parent;
                        continue;
                    }

                    if (anchor.RegionKind == StageRegionKind.Source)
                    {
                        linkKind = StagePresentationLinkKind.Source;
                        linkedStableId = resolvedStableId;
                        linkedParent = current;
                        return true;
                    }

                    if (anchor.RegionKind == StageRegionKind.Deposit)
                    {
                        linkKind = StagePresentationLinkKind.Deposit;
                        linkedStableId = resolvedStableId;
                        linkedParent = current;
                        return true;
                    }
                }

                current = current.parent;
            }

            return false;
        }

        public static Matrix4x4 GetPreviewMatrix(StagePresentationMarker marker)
        {
            return marker != null ? marker.transform.localToWorldMatrix : Matrix4x4.identity;
        }

        public static StagePresentationMarker[] GetPresentationMarkers(StageLayoutStageMarker stageMarker)
        {
            if (stageMarker == null)
                return Array.Empty<StagePresentationMarker>();

            return stageMarker.GetComponentsInChildren<StagePresentationMarker>(includeInactive: true);
        }

        public static bool TryComputePrefabBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            if (prefab == null)
                return false;

            bool hasAny = false;
            var rootInv = prefab.transform.worldToLocalMatrix;

            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;
                if (meshFilter.GetComponent<Renderer>() == null)
                    continue;

                EncapsulateMeshBounds(ref bounds, ref hasAny, rootInv * meshFilter.transform.localToWorldMatrix, meshFilter.sharedMesh.bounds);
            }

            var skinnedMeshes = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < skinnedMeshes.Length; i++)
            {
                var skinned = skinnedMeshes[i];
                if (skinned == null)
                    continue;

                EncapsulateMeshBounds(ref bounds, ref hasAny, rootInv * skinned.transform.localToWorldMatrix, skinned.localBounds);
            }

            return hasAny;
        }

        public static bool HasTopologyOnSelf(StagePresentationMarker marker)
        {
            if (marker == null)
                return false;

            return marker.TryGetComponent<StageRegionAnchorMarker>(out _);
        }

        private static bool TryResolveAnchorStableId(StageRegionAnchorMarker anchor, out uint stableId)
        {
            stableId = 0u;
            if (anchor == null || anchor.RegionSlotIndex <= 0)
                return false;

            var stage = anchor.GetComponentInParent<StageLayoutStageMarker>();
            if (stage == null || !stage.TryGetComponent(out StageGridAuthoring authoring) || authoring == null)
                return false;

            return authoring.TryResolveStableId(anchor.RegionKind, anchor.RegionSlotIndex, out stableId);
        }

        private static void EncapsulateMeshBounds(ref Bounds aggregate, ref bool hasAny, Matrix4x4 localToRoot, Bounds localBounds)
        {
            var min = localBounds.min;
            var max = localBounds.max;
            for (int xi = 0; xi <= 1; xi++)
            {
                for (int yi = 0; yi <= 1; yi++)
                {
                    for (int zi = 0; zi <= 1; zi++)
                    {
                        var point = new Vector3(
                            xi == 0 ? min.x : max.x,
                            yi == 0 ? min.y : max.y,
                            zi == 0 ? min.z : max.z);
                        var rootPoint = localToRoot.MultiplyPoint3x4(point);
                        if (!hasAny)
                        {
                            aggregate = new Bounds(rootPoint, Vector3.zero);
                            hasAny = true;
                        }
                        else
                        {
                            aggregate.Encapsulate(rootPoint);
                        }
                    }
                }
            }
        }
    }
}
