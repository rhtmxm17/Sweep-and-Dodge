using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StageRegionPaintAsset))]
    public sealed class StageRegionPaintAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty _regionKind;
        private SerializedProperty _width;
        private SerializedProperty _height;

        private void OnEnable()
        {
            _regionKind = serializedObject.FindProperty("RegionKind");
            _width = serializedObject.FindProperty("Width");
            _height = serializedObject.FindProperty("Height");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_regionKind);
            EditorGUILayout.PropertyField(_width);
            EditorGUILayout.PropertyField(_height);

            serializedObject.ApplyModifiedProperties();

            var asset = (StageRegionPaintAsset)target;
            if (asset == null)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Cells", $"{asset.Width} x {asset.Height} ({asset.CellCount})");

            if (GUILayout.Button("Open Editor"))
                StageRegionPaintEditorWindow.Open(asset);

            if (GUILayout.Button("Resize"))
            {
                Undo.RecordObject(asset, "Resize Stage Region Paint");
                asset.Resize(asset.Width, asset.Height);
                EditorUtility.SetDirty(asset);
            }

            if (GUILayout.Button("Clear"))
            {
                Undo.RecordObject(asset, "Clear Stage Region Paint");
                asset.ClearAll();
                EditorUtility.SetDirty(asset);
            }

            if (GUILayout.Button("Validate Shape"))
            {
                Undo.RecordObject(asset, "Validate Stage Region Paint");
                asset.EnsureShape();
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
