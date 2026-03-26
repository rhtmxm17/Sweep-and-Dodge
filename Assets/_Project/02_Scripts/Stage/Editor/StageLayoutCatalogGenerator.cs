using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageLayoutCatalogGenerator
    {
        [MenuItem("Tools/Project/Stage Layout/Generate Stage Layout Assets From Open Scenes")]
        private static void GenerateStageLayoutsFromOpenScenesMenu()
        {
            int generated = GenerateStageLayoutsFromOpenScenes(saveAssets: true);
            Debug.Log($"[StageLayout] Grid StageLayoutSO generation complete. layouts={generated}");
        }

        public static int GenerateStageLayoutsFromOpenScenes(bool saveAssets)
        {
            var roots = UnityEngine.Object.FindObjectsByType<StageLayoutRootMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(roots, CompareRoots);

            int generatedCount = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                if (TryGenerateLayoutsForRoot(root, out var issues, saveAssets))
                    generatedCount += CountGeneratedLayoutTargets(root);

                ReportIssues(root, issues);
            }

            return generatedCount;
        }

        public static bool TryGenerateLayoutsForRoot(StageLayoutRootMarker root, out List<ContentValidationIssue> issues, bool saveAssets = false)
        {
            issues = new List<ContentValidationIssue>(16);
            if (root == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL900", "(null)", "StageLayoutRootMarker is null."));
                return false;
            }

            var stageNodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            if (stageNodes == null || stageNodes.Length <= 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STL902", BuildHierarchyPath(root.transform), "No StageLayoutStageMarker was found under root."));
                return true;
            }

            Array.Sort(stageNodes, (a, b) =>
            {
                int stageOrder = a.StageId.CompareTo(b.StageId);
                if (stageOrder != 0)
                    return stageOrder;
                return string.CompareOrdinal(BuildHierarchyPath(a.transform), BuildHierarchyPath(b.transform));
            });

            for (int i = 0; i < stageNodes.Length; i++)
                TryGenerateLayoutForStage(stageNodes[i], issues, saveAssets);

            return !HasError(issues);
        }

        private static bool TryGenerateLayoutForStage(StageLayoutStageMarker stageNode, List<ContentValidationIssue> issues, bool saveAssets)
        {
            if (stageNode == null)
                return false;

            string location = BuildHierarchyPath(stageNode.transform);
            if (stageNode.TargetLayout == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL901", location, "TargetLayout is not assigned."));
                return false;
            }

            StageGridAuthoringValidationRules.Validate(stageNode, issues);
            if (HasError(issues))
                return false;

            if (!stageNode.TryGetComponent(out StageGridAuthoring authoring) || authoring == null)
                return false;

            var layout = BuildStageLayout(stageNode, authoring);
            var validation = new List<ContentValidationIssue>(8);
            StageGridLayoutValidationRules.ValidateLayout(layout, location, validation);
            issues.AddRange(validation);
            if (HasError(validation))
            {
                UnityEngine.Object.DestroyImmediate(layout);
                return false;
            }

            Undo.RecordObject(stageNode.TargetLayout, "Generate Stage Layout");
            stageNode.TargetLayout.SchemaVersion = 2;
            stageNode.TargetLayout.StageId = layout.StageId;
            stageNode.TargetLayout.Grid = layout.Grid;
            stageNode.TargetLayout.Cells = layout.Cells;
            stageNode.TargetLayout.SourceRegions = layout.SourceRegions;
            stageNode.TargetLayout.DepositRegions = layout.DepositRegions;
            stageNode.TargetLayout.Presentations = layout.Presentations;
            stageNode.TargetLayout.Sources = Array.Empty<StageSourceLayoutData>();
            stageNode.TargetLayout.Deposits = Array.Empty<StageDepositLayoutData>();
            stageNode.TargetLayout.Obstacles = Array.Empty<StageObstacleLayoutData>();
            EditorUtility.SetDirty(stageNode.TargetLayout);
            UnityEngine.Object.DestroyImmediate(layout);
            if (saveAssets)
                AssetDatabase.SaveAssets();
            return true;
        }

        private static StageLayoutSO BuildStageLayout(StageLayoutStageMarker stageNode, StageGridAuthoring authoring)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var gridSpec = authoring.BuildRuntimeGridSpec();
            int width = gridSpec.Width;
            int height = gridSpec.Height;
            bool sourceUsesTilemap = authoring.GetRegionTilemap(StageRegionKind.Source) != null;
            bool depositUsesTilemap = authoring.GetRegionTilemap(StageRegionKind.Deposit) != null;

            layout.SchemaVersion = 2;
            layout.StageId = stageNode.StageId;
            layout.Grid = gridSpec;

            layout.Cells = new StageCellLayoutData[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    var tile = authoring.MovementTilemap.GetTile(authoring.GetTilemapCell(x, y)) as StageMovementTile;
                    layout.Cells[index] = new StageCellLayoutData
                    {
                        MovementFlags = tile != null ? tile.MovementFlags : StageCellMovementFlags.None,
                        SourceRegionId = ResolveRegionStableId(authoring, StageRegionKind.Source, x, y, sourceUsesTilemap),
                        DepositRegionId = ResolveRegionStableId(authoring, StageRegionKind.Deposit, x, y, depositUsesTilemap),
                    };
                }
            }

            var anchors = stageNode.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true)
                .OrderBy(x => x.RegionKind)
                .ThenBy(x => ResolveAnchorStableId(authoring, x))
                .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .ToArray();

            layout.SourceRegions = anchors
                .Where(x => x.RegionKind == StageRegionKind.Source)
                .Select(x => ToSourceRegionData(authoring, x))
                .Where(x => x.StableId > 0u)
                .ToArray();
            layout.DepositRegions = anchors
                .Where(x => x.RegionKind == StageRegionKind.Deposit)
                .Select(x => ToDepositRegionData(authoring, x))
                .Where(x => x.StableId > 0u)
                .ToArray();
            layout.Presentations = stageNode.GetComponentsInChildren<StagePresentationMarker>(includeInactive: true)
                .OrderBy(x => x.StableId)
                .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .Select(ToPresentationData)
                .ToArray();
            return layout;
        }

        private static int CountGeneratedLayoutTargets(StageLayoutRootMarker root)
        {
            if (root == null)
                return 0;

            int count = 0;
            var nodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].TargetLayout != null)
                    count++;
            }

            return count;
        }

        private static uint ResolveRegionStableId(StageGridAuthoring authoring, StageRegionKind kind, int localX, int localY, bool useTilemap)
        {
            if (authoring == null)
                return 0u;

            if (useTilemap)
            {
                var tilemap = authoring.GetRegionTilemap(kind);
                var tile = tilemap != null ? tilemap.GetTile(authoring.GetTilemapCell(localX, localY)) as StageRegionTile : null;
                if (tile == null || tile.RegionKind != kind || tile.RegionSlotIndex <= 0)
                    return 0u;

                return authoring.TryResolveStableId(kind, tile.RegionSlotIndex, out uint stableId) ? stableId : 0u;
            }

            var paint = kind == StageRegionKind.Source ? authoring.SourceRegionPaint : authoring.DepositRegionPaint;
            return paint != null ? paint.GetCell(localX, localY) : 0u;
        }

        private static uint ResolveAnchorStableId(StageGridAuthoring authoring, StageRegionAnchorMarker marker)
        {
            if (authoring != null && marker != null && authoring.TryResolveStableId(marker.RegionKind, marker.RegionSlotIndex, out uint stableId))
                return stableId;

            return marker != null ? marker.StableId : 0u;
        }

        private static StageSourceRegionLayoutData ToSourceRegionData(StageGridAuthoring authoring, StageRegionAnchorMarker marker)
        {
            return new StageSourceRegionLayoutData
            {
                StableId = ResolveAnchorStableId(authoring, marker),
                Active = marker.Active,
                AnchorCell = marker.AnchorCell,
                AnchorOffset = marker.AnchorOffset,
            };
        }

        private static StageDepositRegionLayoutData ToDepositRegionData(StageGridAuthoring authoring, StageRegionAnchorMarker marker)
        {
            return new StageDepositRegionLayoutData
            {
                StableId = ResolveAnchorStableId(authoring, marker),
                Active = marker.Active,
                AnchorCell = marker.AnchorCell,
                AnchorOffset = marker.AnchorOffset,
            };
        }

        private static StagePresentationLayoutData ToPresentationData(StagePresentationMarker marker)
        {
            var transform = marker.transform;
            ResolvePresentationLink(marker, out var linkKind, out var linkedStableId);
            bool linked = marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent;
            return new StagePresentationLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                PlacementMode = marker.PlacementMode,
                LinkKind = linked ? linkKind : StagePresentationLinkKind.None,
                LinkedStableId = linked ? linkedStableId : 0u,
                PresentationKey = marker.PresentationKey != null ? marker.PresentationKey.Trim() : string.Empty,
                Position = linked ? transform.localPosition : transform.position,
                Euler = linked ? transform.localEulerAngles : transform.eulerAngles,
                Scale = transform.localScale,
            };
        }

        private static void ResolvePresentationLink(StagePresentationMarker marker, out StagePresentationLinkKind linkKind, out uint linkedStableId)
        {
            linkKind = StagePresentationLinkKind.None;
            linkedStableId = 0u;

            if (marker == null || marker.PlacementMode != StagePresentationPlacementMode.LinkedToParent)
                return;

            StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out linkKind, out linkedStableId, out _);
        }

        private static int CompareRoots(StageLayoutRootMarker a, StageLayoutRootMarker b)
        {
            return string.CompareOrdinal(BuildHierarchyPath(a != null ? a.transform : null), BuildHierarchyPath(b != null ? b.transform : null));
        }

        private static bool HasError(IReadOnlyList<ContentValidationIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    return true;
            }

            return false;
        }

        private static void ReportIssues(StageLayoutRootMarker root, IReadOnlyList<ContentValidationIssue> issues)
        {
            if (issues == null || issues.Count <= 0)
                return;

            string rootName = root != null ? BuildHierarchyPath(root.transform) : "(null-root)";
            for (int i = 0; i < issues.Count; i++)
            {
                string line = $"[StageLayout][{rootName}] {issues[i].Code} {issues[i].Location} - {issues[i].Message}";
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    Debug.LogError(line);
                else
                    Debug.LogWarning(line);
            }
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "(null)";

            string path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }
}
