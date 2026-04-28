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
            stageNode.TargetLayout.PlayerStart = layout.PlayerStart;
            stageNode.TargetLayout.Presentations = layout.Presentations;
            GenerateGridVisualPrefab(authoring, stageNode.TargetLayout, layout.StageId, saveAssets);
            EditorUtility.SetDirty(stageNode.TargetLayout);
            UnityEngine.Object.DestroyImmediate(layout);
            if (saveAssets)
                AssetDatabase.SaveAssets();
            return true;
        }

        private static void GenerateGridVisualPrefab(StageGridAuthoring authoring, StageLayoutSO targetLayout, int stageId, bool saveAssets)
        {
            if (targetLayout == null)
                return;

            if (authoring == null
                || authoring.Grid == null
                || (authoring.GroundVisualTilemap == null && authoring.WallVisualTilemap == null)
                || !saveAssets)
            {
                targetLayout.GridVisualPrefab = null;
                return;
            }

            var root = new GameObject($"GridVisual_Stage{stageId}");

            try
            {
                root.transform.position = Vector3.zero;
                root.transform.rotation = authoring.Grid.transform.rotation;
                root.transform.localScale = Vector3.one;

                var grid = root.AddComponent<Grid>();
                grid.cellSize = authoring.Grid.cellSize;
                grid.cellGap = authoring.Grid.cellGap;
                grid.cellLayout = authoring.Grid.cellLayout;
                grid.cellSwizzle = authoring.Grid.cellSwizzle;

                CopyVisualTilemap(authoring.GroundVisualTilemap, root.transform);
                CopyVisualTilemap(authoring.WallVisualTilemap, root.transform);

                const string folderPath = "Assets/_Project/04_Prefabs/StageVisual";
                EnsureAssetFolder(folderPath);

                string prefabPath = $"{folderPath}/GridVisual_Stage{stageId}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                targetLayout.GridVisualPrefab = prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CopyVisualTilemap(Tilemap tilemap, Transform parent)
        {
            if (tilemap == null)
                return;

            var copy = UnityEngine.Object.Instantiate(tilemap.gameObject, parent, false);
            copy.name = tilemap.gameObject.name;
            copy.hideFlags = HideFlags.None;
            var transforms = copy.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; i++)
                transforms[i].gameObject.hideFlags = HideFlags.None;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static StageLayoutSO BuildStageLayout(StageLayoutStageMarker stageNode, StageGridAuthoring authoring)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var gridSpec = authoring.BuildRuntimeGridSpec();
            int width = gridSpec.Width;
            int height = gridSpec.Height;

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
                        SourceRegionId = ResolveRegionStableId(authoring, StageRegionKind.Source, x, y),
                        DepositRegionId = ResolveRegionStableId(authoring, StageRegionKind.Deposit, x, y),
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
            layout.PlayerStart = ToPlayerStartData(stageNode);
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

        private static uint ResolveRegionStableId(StageGridAuthoring authoring, StageRegionKind kind, int localX, int localY)
        {
            if (authoring == null || authoring.RegionTilemap == null)
                return 0u;

            var tile = authoring.RegionTilemap.GetTile(authoring.GetTilemapCell(localX, localY)) as StageRegionTile;
            if (tile == null || tile.RegionKind != kind || tile.RegionSlotIndex <= 0)
                return 0u;

            return authoring.TryResolveStableId(kind, tile.RegionSlotIndex, out uint stableId) ? stableId : 0u;
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
                AnchorCell = NormalizeAnchorCell(authoring, marker.AnchorCell),
                AnchorOffset = marker.AnchorOffset,
            };
        }

        private static StageDepositRegionLayoutData ToDepositRegionData(StageGridAuthoring authoring, StageRegionAnchorMarker marker)
        {
            return new StageDepositRegionLayoutData
            {
                StableId = ResolveAnchorStableId(authoring, marker),
                Active = marker.Active,
                AnchorCell = NormalizeAnchorCell(authoring, marker.AnchorCell),
                AnchorOffset = marker.AnchorOffset,
            };
        }

        private static StagePlayerStartLayoutData ToPlayerStartData(StageLayoutStageMarker stageNode)
        {
            if (stageNode == null)
                return default;

            var markers = stageNode.GetComponentsInChildren<StagePlayerStartMarker>(includeInactive: true)
                .OrderBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .ToArray();
            if (markers.Length <= 0 || markers[0] == null)
                return default;

            var marker = markers[0];
            return new StagePlayerStartLayoutData
            {
                Active = marker.Active,
                AnchorCell = NormalizeAnchorCell(stageNode.TryGetComponent(out StageGridAuthoring authoring) ? authoring : null, marker.AnchorCell),
                AnchorOffset = marker.AnchorOffset,
                YawDeg = marker.YawDeg,
            };
        }

        private static Vector2Int NormalizeAnchorCell(StageGridAuthoring authoring, Vector2Int tileCell)
        {
            return authoring != null ? authoring.GetLocalCell(tileCell) : tileCell;
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
