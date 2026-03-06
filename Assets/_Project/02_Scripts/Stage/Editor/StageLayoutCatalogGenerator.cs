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
        [MenuItem("Tools/Project/Stage Layout/Generate Stage Layout Assets From Open Scenes")]
        private static void GenerateStageLayoutsFromOpenScenesMenu()
        {
            int generated = GenerateStageLayoutsFromOpenScenes(saveAssets: true);
            Debug.Log($"[StageLayout] StageLayoutSO generation complete. layouts={generated}");
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
                {
                    generatedCount += CountGeneratedLayoutTargets(root);
                }

                ReportIssues(root, issues);
            }

            return generatedCount;
        }

        public static bool TryGenerateLayoutsForRoot(
            StageLayoutRootMarker root,
            out List<ContentValidationIssue> issues,
            bool saveAssets = false)
        {
            issues = new List<ContentValidationIssue>(16);
            if (root == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STL900",
                    "(null)",
                    "StageLayoutRootMarker is null."));
                return false;
            }

            var stageNodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            if (stageNodes == null || stageNodes.Length <= 0)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Warning,
                    "STL902",
                    BuildHierarchyPath(root.transform),
                    "No StageLayoutStageMarker was found under root."));
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
            {
                TryGenerateLayoutForStage(stageNodes[i], issues, saveAssets);
            }

            return !HasError(issues);
        }

        private static bool TryGenerateLayoutForStage(
            StageLayoutStageMarker stageNode,
            List<ContentValidationIssue> issues,
            bool saveAssets)
        {
            if (stageNode == null)
                return false;

            string location = BuildHierarchyPath(stageNode.transform);
            if (stageNode.TargetLayout == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STL901",
                    location,
                    "TargetLayout is not assigned."));
                return false;
            }

            var definition = BuildLegacyStageDefinition(stageNode);
            var validation = new List<ContentValidationIssue>(8);
            StageLayoutValidationRules.ValidateDefinitions(new[] { definition }, location, validation);
            for (int i = 0; i < validation.Count; i++)
                issues.Add(validation[i]);
            if (HasError(validation))
                return false;

            Undo.RecordObject(stageNode.TargetLayout, "Generate Stage Layout");
            stageNode.TargetLayout.StageId = definition.StageId;
            stageNode.TargetLayout.Sources = definition.Sources;
            stageNode.TargetLayout.Deposits = definition.Deposits;
            stageNode.TargetLayout.Obstacles = definition.Obstacles;
            stageNode.TargetLayout.Visuals = definition.Visuals;
            EditorUtility.SetDirty(stageNode.TargetLayout);
            if (saveAssets)
                AssetDatabase.SaveAssets();
            return true;
        }

        [MenuItem("Tools/Project/Stage Layout/Generate Catalogs From Open Scenes")]
        private static void GenerateCatalogsFromOpenScenesMenu()
        {
            Debug.LogWarning("[StageLayout] GenerateCatalogsFromOpenScenes is deprecated in Dual Catalog mode. Use StageLayoutSO + StageCatalog composer.");
            int generated = GenerateCatalogsFromOpenScenes(saveAssets: true);
            Debug.Log($"[StageLayout] Legacy StageMapCatalog generation complete. catalogs={generated}");
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

                definitions.Add(BuildLegacyStageDefinition(stageNode));
            }

            if (root.SortByStageId)
            {
                definitions.Sort((a, b) => a.StageId.CompareTo(b.StageId));
            }

            return definitions;
        }

        private static StageMapDefinition BuildLegacyStageDefinition(StageLayoutStageMarker stageNode)
        {
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

            return new StageMapDefinition
            {
                StageId = stageNode.StageId,
                Sources = sources,
                Deposits = deposits,
                Obstacles = obstacles,
                Visuals = visuals,
            };
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
