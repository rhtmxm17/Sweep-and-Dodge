using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class ContentValidationRunner
    {
        private static readonly string[] SearchRoots = { "Assets/_Project" };
        public const int DefaultWarningLogCap = 100;
        public const int DefaultErrorSummaryLimit = 10;

        [MenuItem("Tools/Project/Validate Content")]
        private static void ValidateContentMenu()
        {
            var issues = ValidateProjectAssets();
            ReportToConsole(issues);
        }

        public static List<ContentValidationIssue> ValidateProjectAssets()
        {
            var definitions = CollectScriptableObjects<BulletDefinitionSO>();
            var waveClips = CollectScriptableObjects<WaveClipSO>();
            var stageMapCatalogs = CollectScriptableObjects<StageMapCatalogSO>();
            var stageCatalogs = CollectScriptableObjects<StageCatalogSO>();
            var visuals = new List<ContentValidationRecord<BulletVisualPrefabAuthoring>>();
            var sources = new List<ContentValidationRecord<BulletSourceAuthoring>>();
            var bullets = new List<ContentValidationRecord<BulletAuthoring>>();

            CollectAuthoringsFromPrefabs(visuals, sources, bullets);
            CollectAuthoringsFromScenes(visuals, sources, bullets);

            SortRecordsByLocation(definitions);
            SortRecordsByLocation(waveClips);
            SortRecordsByLocation(stageMapCatalogs);
            SortRecordsByLocation(stageCatalogs);
            SortRecordsByLocation(visuals);
            SortRecordsByLocation(sources);
            SortRecordsByLocation(bullets);

            var input = new ContentValidationInput(definitions, waveClips, visuals, sources, bullets);
            var issues = ContentValidationRules.Validate(input);
            StageLayoutValidationRules.ValidateCatalogRecords(stageMapCatalogs, issues);
            StageCatalogValidationRules.ValidateCatalogRecords(stageCatalogs, issues);
            SortIssuesInPlace(issues);
            return issues;
        }

        public static void SortIssuesInPlace(List<ContentValidationIssue> issues)
        {
            if (issues == null || issues.Count <= 1)
                return;

            issues.Sort(CompareIssues);
        }

        public static string BuildErrorSummary(IReadOnlyList<ContentValidationIssue> issues, int maxEntries = DefaultErrorSummaryLimit)
        {
            if (issues == null || issues.Count <= 0)
                return "errors=0, shown=0";

            var errors = new List<ContentValidationIssue>();
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    errors.Add(issues[i]);
            }

            SortIssuesInPlace(errors);
            if (errors.Count <= 0)
                return "errors=0, shown=0";

            int capped = maxEntries < 0 ? 0 : maxEntries;
            int showCount = Mathf.Min(capped, errors.Count);
            if (showCount <= 0)
                return $"errors={errors.Count}, shown=0";

            var lines = new List<string>(showCount);
            for (int i = 0; i < showCount; i++)
            {
                var error = errors[i];
                lines.Add($"[{i + 1}] {error.Code} {error.Location} - {error.Message}");
            }

            return $"errors={errors.Count}, shown={showCount}\n{string.Join("\n", lines)}";
        }

        public static (int ErrorCount, int WarningCount, int WarningLogsToEmit, int SuppressedWarningCount) CalculateIssueReportCounts(
            IReadOnlyList<ContentValidationIssue> issues,
            int warningLogCap = DefaultWarningLogCap)
        {
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < (issues?.Count ?? 0); i++)
            {
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    errors++;
                else
                    warnings++;
            }

            int cap = warningLogCap < 0 ? 0 : warningLogCap;
            int emittedWarnings = Mathf.Min(cap, warnings);
            int suppressedWarnings = Mathf.Max(0, warnings - cap);
            return (errors, warnings, emittedWarnings, suppressedWarnings);
        }

        private static List<ContentValidationRecord<T>> CollectScriptableObjects<T>() where T : ScriptableObject
        {
            var list = new List<ContentValidationRecord<T>>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", SearchRoots);
            System.Array.Sort(guids, System.StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                    continue;

                list.Add(new ContentValidationRecord<T>(asset, path));
            }

            return list;
        }

        private static void CollectAuthoringsFromPrefabs(
            List<ContentValidationRecord<BulletVisualPrefabAuthoring>> visuals,
            List<ContentValidationRecord<BulletSourceAuthoring>> sources,
            List<ContentValidationRecord<BulletAuthoring>> bullets)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchRoots);
            System.Array.Sort(guids, System.StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    continue;

                CollectAuthoringsInHierarchy(path, root, visuals, sources, bullets);
            }
        }

        private static void CollectAuthoringsFromScenes(
            List<ContentValidationRecord<BulletVisualPrefabAuthoring>> visuals,
            List<ContentValidationRecord<BulletSourceAuthoring>> sources,
            List<ContentValidationRecord<BulletAuthoring>> bullets)
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", SearchRoots);
            System.Array.Sort(guids, System.StringComparer.Ordinal);
            var previous = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (!scene.IsValid())
                        continue;

                    var roots = scene.GetRootGameObjects();
                    for (int r = 0; r < roots.Length; r++)
                    {
                        CollectAuthoringsInHierarchy(path, roots[r], visuals, sources, bullets);
                    }

                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
            finally
            {
                if (previous != null && previous.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                }
                else if (SceneManager.sceneCount <= 0)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        private static void CollectAuthoringsInHierarchy(
            string assetPath,
            GameObject root,
            List<ContentValidationRecord<BulletVisualPrefabAuthoring>> visuals,
            List<ContentValidationRecord<BulletSourceAuthoring>> sources,
            List<ContentValidationRecord<BulletAuthoring>> bullets)
        {
            var visualComponents = root.GetComponentsInChildren<BulletVisualPrefabAuthoring>(includeInactive: true);
            for (int i = 0; i < visualComponents.Length; i++)
            {
                string location = $"{assetPath}::{BuildHierarchyPath(visualComponents[i].transform)}";
                visuals.Add(new ContentValidationRecord<BulletVisualPrefabAuthoring>(visualComponents[i], location));
            }

            var sourceComponents = root.GetComponentsInChildren<BulletSourceAuthoring>(includeInactive: true);
            for (int i = 0; i < sourceComponents.Length; i++)
            {
                string location = $"{assetPath}::{BuildHierarchyPath(sourceComponents[i].transform)}";
                sources.Add(new ContentValidationRecord<BulletSourceAuthoring>(sourceComponents[i], location));
            }

            var bulletComponents = root.GetComponentsInChildren<BulletAuthoring>(includeInactive: true);
            for (int i = 0; i < bulletComponents.Length; i++)
            {
                string location = $"{assetPath}::{BuildHierarchyPath(bulletComponents[i].transform)}";
                bullets.Add(new ContentValidationRecord<BulletAuthoring>(bulletComponents[i], location));
            }
        }

        private static void ReportToConsole(List<ContentValidationIssue> issues)
        {
            var counts = CalculateIssueReportCounts(issues, DefaultWarningLogCap);
            int warningLogsEmitted = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                string line = $"[ContentValidation][{issue.Severity}] {issue.Code} {issue.Location} - {issue.Message}";
                if (issue.Severity == ContentValidationSeverity.Error)
                {
                    Debug.LogError(line);
                }
                else
                {
                    if (warningLogsEmitted < counts.WarningLogsToEmit)
                    {
                        warningLogsEmitted++;
                        Debug.LogWarning(line);
                    }
                }
            }

            if (counts.SuppressedWarningCount > 0)
            {
                Debug.LogWarning($"[ContentValidation] Warning log cap reached ({DefaultWarningLogCap}). Suppressed warnings={counts.SuppressedWarningCount}");
            }

            Debug.Log($"[ContentValidation] Done. errors={counts.ErrorCount}, warnings={counts.WarningCount}, total={issues.Count}");
        }

        private static void SortRecordsByLocation<T>(List<ContentValidationRecord<T>> records) where T : Object
        {
            if (records == null || records.Count <= 1)
                return;

            records.Sort((a, b) => string.CompareOrdinal(a.Location, b.Location));
        }

        private static int CompareIssues(ContentValidationIssue a, ContentValidationIssue b)
        {
            int severity = b.Severity.CompareTo(a.Severity);
            if (severity != 0)
                return severity;

            int code = string.CompareOrdinal(a.Code, b.Code);
            if (code != 0)
                return code;

            int location = string.CompareOrdinal(a.Location, b.Location);
            if (location != 0)
                return location;

            return string.CompareOrdinal(a.Message, b.Message);
        }

        private static string BuildHierarchyPath(Transform transform)
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
    }
}



