using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StagePlayerStartMarker))]
    public sealed class StagePlayerStartMarkerEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            var marker = (StagePlayerStartMarker)target;
            if (marker != null)
                StageAnchorTransformEditorUtility.RememberTransformPose(marker.transform);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Legacy import/debug/backend path. New user-facing stage editing should use StageMapDocument in the Stage Map Editor.", MessageType.Info);

            bool dataChanged = DrawDefaultInspector();

            var marker = (StagePlayerStartMarker)target;
            if (marker == null)
                return;

            if (dataChanged)
                StageAnchorTransformEditorUtility.SyncPlayerTransformFromData(marker, recordUndo: true);

            if (TryComputeWorldPosition(marker, out var worldPosition))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Vector3Field("Preview World Position", worldPosition);
                }

                if (GUILayout.Button("Apply Player Start Data To Transform"))
                    StageAnchorTransformEditorUtility.SyncPlayerTransformFromData(marker, recordUndo: true);

                if (GUILayout.Button("Snap To Cell Center"))
                    StageAnchorTransformEditorUtility.SyncPlayerDataFromTransform(marker, recordUndo: true);
            }
            else
            {
                EditorGUILayout.HelpBox("Nearest StageGridAuthoring not found or incomplete.", MessageType.Warning);
            }
        }

        private void OnSceneGUI()
        {
            var marker = (StagePlayerStartMarker)target;
            if (StageAnchorTransformEditorUtility.SyncPlayerDataFromTransformIfChanged(marker, recordUndo: true))
                SceneView.RepaintAll();
        }

        private static bool TryComputeWorldPosition(StagePlayerStartMarker marker, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (marker == null)
                return false;

            var stageNode = marker.GetComponentInParent<StageLayoutStageMarker>();
            if (stageNode == null
                || !stageNode.TryGetComponent(out StageGridAuthoring authoring)
                || authoring == null
                || authoring.Grid == null)
            {
                return false;
            }

            return StageAnchorTransformEditorUtility.TryGetWorldPosition(
                authoring,
                marker.AnchorCell,
                marker.AnchorOffset,
                out worldPosition);
        }
    }
}
