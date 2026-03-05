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
            if (GUILayout.Button("Generate Target Catalog"))
            {
                bool generated = StageLayoutCatalogGenerator.TryGenerateForRoot(root, out var issues, saveAssets: true);
                if (generated)
                    Debug.Log($"[StageLayout] Catalog generated for {root.name}");

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
}
