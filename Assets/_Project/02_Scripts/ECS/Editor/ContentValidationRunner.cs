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

        [MenuItem("Tools/Project/Validate Content")]
        private static void ValidateContentMenu()
        {
            var issues = ValidateProjectAssets();
            ReportToConsole(issues);
        }

        public static List<ContentValidationIssue> ValidateProjectAssets()
        {
            var definitions = CollectScriptableObjects<BulletDefinitionSO>();
            var profiles = CollectScriptableObjects<BulletSourceProfileSO>();
            var visuals = new List<ContentValidationRecord<BulletVisualPrefabAuthoring>>();
            var sources = new List<ContentValidationRecord<BulletSourceAuthoring>>();

            CollectAuthoringsFromPrefabs(visuals, sources);
            CollectAuthoringsFromScenes(visuals, sources);

            var input = new ContentValidationInput(definitions, profiles, visuals, sources);
            return ContentValidationRules.Validate(input);
        }

        private static List<ContentValidationRecord<T>> CollectScriptableObjects<T>() where T : ScriptableObject
        {
            var list = new List<ContentValidationRecord<T>>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", SearchRoots);
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
            List<ContentValidationRecord<BulletSourceAuthoring>> sources)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", SearchRoots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                    continue;

                CollectAuthoringsInHierarchy(path, root, visuals, sources);
            }
        }

        private static void CollectAuthoringsFromScenes(
            List<ContentValidationRecord<BulletVisualPrefabAuthoring>> visuals,
            List<ContentValidationRecord<BulletSourceAuthoring>> sources)
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", SearchRoots);
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
                        CollectAuthoringsInHierarchy(path, roots[r], visuals, sources);
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
            List<ContentValidationRecord<BulletSourceAuthoring>> sources)
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
        }

        private static void ReportToConsole(List<ContentValidationIssue> issues)
        {
            int errorCount = 0;
            int warningCount = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                string line = $"[ContentValidation][{issue.Severity}] {issue.Code} {issue.Location} - {issue.Message}";
                if (issue.Severity == ContentValidationSeverity.Error)
                {
                    errorCount++;
                    Debug.LogError(line);
                }
                else
                {
                    warningCount++;
                    Debug.LogWarning(line);
                }
            }

            Debug.Log($"[ContentValidation] Done. errors={errorCount}, warnings={warningCount}, total={issues.Count}");
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
