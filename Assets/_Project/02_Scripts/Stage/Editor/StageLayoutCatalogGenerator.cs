using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageLayoutCatalogGenerator
    {
        [MenuItem("Tools/Project/Stage Layout/Generate Catalogs From Open Scenes")]
        private static void GenerateCatalogsFromOpenScenesMenu()
        {
            int generated = GenerateCatalogsFromOpenScenes(saveAssets: true);
            Debug.Log($"[StageLayout] Generation complete. catalogs={generated}");
        }

        public static int GenerateCatalogsFromOpenScenes(bool saveAssets)
        {
            var roots = UnityEngine.Object.FindObjectsByType<StageLayoutRootMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(roots, CompareRoots);

            int generatedCount = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                if (TryGenerateForRoot(root, out var issues, saveAssets))
                {
                    generatedCount++;
                    ReportIssues(root, issues);
                    continue;
                }

                ReportIssues(root, issues);
            }

            return generatedCount;
        }

        public static bool TryGenerateForRoot(
            StageLayoutRootMarker root,
            out List<ContentValidationIssue> issues,
            bool saveAssets = false)
        {
            issues = new List<ContentValidationIssue>(16);
            if (root == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STG900",
                    "(null)",
                    "StageLayoutRootMarker is null."));
                return false;
            }

            string rootLocation = BuildHierarchyPath(root.transform);
            if (root.TargetCatalog == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STG901",
                    rootLocation,
                    "TargetCatalog is not assigned."));
                return false;
            }

            var definitions = BuildDefinitions(root);
            StageLayoutValidationRules.ValidateDefinitions(definitions, rootLocation, issues);
            if (HasError(issues))
                return false;

            Undo.RecordObject(root.TargetCatalog, "Generate Stage Map Catalog");
            root.TargetCatalog.Stages = definitions.ToArray();
            EditorUtility.SetDirty(root.TargetCatalog);
            if (saveAssets)
                AssetDatabase.SaveAssets();
            return true;
        }

        private static List<StageMapDefinition> BuildDefinitions(StageLayoutRootMarker root)
        {
            var definitions = new List<StageMapDefinition>(8);
            if (root == null)
                return definitions;

            var stageNodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            if (stageNodes == null || stageNodes.Length <= 0)
                return definitions;

            Array.Sort(stageNodes, (a, b) =>
            {
                int stageOrder = a.StageId.CompareTo(b.StageId);
                if (stageOrder != 0)
                    return stageOrder;

                return string.CompareOrdinal(BuildHierarchyPath(a.transform), BuildHierarchyPath(b.transform));
            });

            for (int i = 0; i < stageNodes.Length; i++)
            {
                var stageNode = stageNodes[i];
                if (stageNode == null)
                    continue;

                var sources = stageNode.GetComponentsInChildren<StageSourceMarker>(includeInactive: true)
                    .OrderBy(x => x.StableId)
                    .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                    .Select(ToSourceData)
                    .ToArray();
                var deposits = stageNode.GetComponentsInChildren<StageDepositMarker>(includeInactive: true)
                    .OrderBy(x => x.StableId)
                    .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                    .Select(ToDepositData)
                    .ToArray();
                var obstacles = stageNode.GetComponentsInChildren<StageObstacleMarker>(includeInactive: true)
                    .OrderBy(x => x.StableId)
                    .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                    .Select(ToObstacleData)
                    .ToArray();
                var visuals = stageNode.GetComponentsInChildren<StageVisualMarker>(includeInactive: true)
                    .OrderBy(x => x.StableId)
                    .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                    .Select(ToVisualData)
                    .ToArray();

                definitions.Add(new StageMapDefinition
                {
                    StageId = stageNode.StageId,
                    Sources = sources,
                    Deposits = deposits,
                    Obstacles = obstacles,
                    Visuals = visuals,
                });
            }

            if (root.SortByStageId)
            {
                definitions.Sort((a, b) => a.StageId.CompareTo(b.StageId));
            }

            return definitions;
        }

        private static StageSourceLayoutData ToSourceData(StageSourceMarker marker)
        {
            var transform = marker.transform;
            return new StageSourceLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                Position = transform.position,
                YawDeg = transform.eulerAngles.y,
                FieldShape = marker.FieldShape,
                FieldRadius = Mathf.Max(0f, marker.FieldRadius),
                FieldSize = new Vector2(Mathf.Max(0f, marker.FieldSize.x), Mathf.Max(0f, marker.FieldSize.y)),
            };
        }

        private static StageDepositLayoutData ToDepositData(StageDepositMarker marker)
        {
            var transform = marker.transform;
            return new StageDepositLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                Position = transform.position,
                Radius = Mathf.Max(0f, marker.Radius),
            };
        }

        private static StageObstacleLayoutData ToObstacleData(StageObstacleMarker marker)
        {
            var transform = marker.transform;
            return new StageObstacleLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                Position = transform.position,
                YawDeg = transform.eulerAngles.y,
                Shape = marker.Shape,
                Radius = Mathf.Max(0f, marker.Radius),
                Size = new Vector2(Mathf.Max(0f, marker.Size.x), Mathf.Max(0f, marker.Size.y)),
            };
        }

        private static StageVisualLayoutData ToVisualData(StageVisualMarker marker)
        {
            var transform = marker.transform;
            return new StageVisualLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                Position = transform.position,
                Euler = transform.eulerAngles,
                Scale = transform.localScale,
                VisualKey = marker.VisualKey,
            };
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

        private static int CompareRoots(StageLayoutRootMarker a, StageLayoutRootMarker b)
        {
            if (a == b)
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            Scene sa = a.gameObject.scene;
            Scene sb = b.gameObject.scene;
            int sceneCompare = string.CompareOrdinal(sa.path, sb.path);
            if (sceneCompare != 0)
                return sceneCompare;

            return string.CompareOrdinal(BuildHierarchyPath(a.transform), BuildHierarchyPath(b.transform));
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "(null)";

            string path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}

