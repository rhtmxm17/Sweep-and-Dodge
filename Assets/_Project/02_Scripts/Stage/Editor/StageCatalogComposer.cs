using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageCatalogComposer
    {
        [MenuItem("Tools/Project/Stage Layout/Compose Stage Catalogs From Open Scenes")]
        private static void ComposeCatalogsFromOpenScenesMenu()
        {
            int composed = ComposeCatalogsFromOpenScenes(saveAssets: true);
            Debug.Log($"[StageCatalog] Compose complete. catalogs={composed}");
        }

        public static int ComposeCatalogsFromOpenScenes(bool saveAssets)
        {
            var roots = UnityEngine.Object.FindObjectsByType<StageLayoutRootMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(roots, CompareRoots);

            int composed = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                if (TryComposeForRoot(root, out var issues, saveAssets))
                    composed++;

                ReportIssues(root, issues);
            }

            return composed;
        }

        public static bool TryComposeForRoot(
            StageLayoutRootMarker root,
            out List<ContentValidationIssue> issues,
            bool saveAssets = false)
        {
            issues = new List<ContentValidationIssue>(16);
            if (root == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STC900", "(null)", "StageLayoutRootMarker is null."));
                return false;
            }

            if (root.TargetStageCatalog == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STC901", BuildHierarchyPath(root.transform), "TargetStageCatalog is not assigned."));
                return false;
            }

            var stageNodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            if (stageNodes == null || stageNodes.Length <= 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STC902", BuildHierarchyPath(root.transform), "No StageLayoutStageMarker was found under root."));
                return true;
            }

            Array.Sort(stageNodes, (a, b) =>
            {
                int stageOrder = a.StageId.CompareTo(b.StageId);
                if (stageOrder != 0)
                    return stageOrder;
                return string.CompareOrdinal(BuildHierarchyPath(a.transform), BuildHierarchyPath(b.transform));
            });

            var entries = new List<StageCatalogEntry>(stageNodes.Length);
            for (int i = 0; i < stageNodes.Length; i++)
            {
                var node = stageNodes[i];
                if (node == null || !node.IncludeInCatalog)
                    continue;

                string location = BuildHierarchyPath(node.transform);
                if (node.TargetDefinition == null)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STC903", location, "TargetDefinition is not assigned."));
                    continue;
                }

                if (node.TargetLayout == null)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STC904", location, "TargetLayout is not assigned."));
                    continue;
                }

                string entryKey = string.IsNullOrWhiteSpace(node.EntryKey)
                    ? $"stage_{Mathf.Max(1, node.StageId):00}"
                    : node.EntryKey.Trim();

                entries.Add(new StageCatalogEntry
                {
                    Enabled = node.EnabledInCatalog,
                    EntryKey = entryKey,
                    Definition = node.TargetDefinition,
                    Layout = node.TargetLayout,
                });
            }

            Undo.RecordObject(root.TargetStageCatalog, "Compose Stage Catalog");
            root.TargetStageCatalog.SchemaVersion = Mathf.Max(1, root.TargetStageCatalog.SchemaVersion);
            root.TargetStageCatalog.Entries = entries.ToArray();
            EditorUtility.SetDirty(root.TargetStageCatalog);
            if (saveAssets)
                AssetDatabase.SaveAssets();

            return !HasError(issues);
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
                string line = $"[StageCatalog][{rootName}] {issues[i].Code} {issues[i].Location} - {issues[i].Message}";
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
