using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StageLayoutRootMarker))]
    public sealed class StageLayoutRootMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var root = target as StageLayoutRootMarker;
            if (root == null)
                return;

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Generate StageLayoutSO Assets"))
            {
                bool generated = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(root, out var issues, saveAssets: true);
                if (generated)
                    Debug.Log($"[StageLayout] StageLayoutSO assets generated for {root.name}");

                ReportIssues(issues);
            }

            if (GUILayout.Button("Sync StageDefinitionSO Assets"))
            {
                bool synced = StageDefinitionGenerator.TrySyncDefinitionsForRoot(root, out var issues, saveAssets: true);
                if (synced)
                    Debug.Log($"[StageDefinition] StageDefinitionSO assets synced for {root.name}");

                ReportIssues(issues);
            }

            if (GUILayout.Button("Compose StageCatalogSO"))
            {
                bool composed = StageCatalogComposer.TryComposeForRoot(root, out var issues, saveAssets: true);
                if (composed)
                    Debug.Log($"[StageCatalog] StageCatalogSO composed for {root.name}");

                ReportIssues(issues);
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Generate Legacy StageMapCatalog (Deprecated)"))
            {
                bool generated = StageLayoutCatalogGenerator.TryGenerateForRoot(root, out var issues, saveAssets: true);
                if (generated)
                    Debug.Log($"[StageLayout] Legacy StageMapCatalog generated for {root.name}");

                ReportIssues(issues);
            }
        }

        private static void ReportIssues(System.Collections.Generic.IReadOnlyList<ContentValidationIssue> issues)
        {
            if (issues == null)
                return;

            for (int i = 0; i < issues.Count; i++)
            {
                string line = $"[StageLayout] {issues[i].Code} {issues[i].Location} - {issues[i].Message}";
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    Debug.LogError(line);
                else
                    Debug.LogWarning(line);
            }
        }
    }
}
