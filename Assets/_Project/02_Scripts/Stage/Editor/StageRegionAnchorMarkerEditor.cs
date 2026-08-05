using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StageRegionAnchorMarker))]
    public sealed class StageRegionAnchorMarkerEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            var marker = (StageRegionAnchorMarker)target;
            if (marker != null)
                StageAnchorTransformEditorUtility.RememberTransformPose(marker.transform);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Legacy import/debug/backend path. New user-facing stage editing should use StageMapDocument in the Stage Map Editor.", MessageType.Info);

            bool dataChanged = DrawDefaultInspector();

            var marker = (StageRegionAnchorMarker)target;
            if (marker == null)
                return;

            if (dataChanged)
                StageAnchorTransformEditorUtility.SyncRegionTransformFromData(marker, recordUndo: true);

            if (TryComputeWorldPosition(marker, out var world))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Vector3Field("Preview World Position", world);
                }

                if (GUILayout.Button("Snap Transform To Anchor Preview"))
                {
                    StageAnchorTransformEditorUtility.SyncRegionTransformFromData(marker, recordUndo: true);
                }

                if (GUILayout.Button("Snap To Cell Center"))
                {
                    StageAnchorTransformEditorUtility.SyncRegionDataFromTransform(marker, recordUndo: true);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Nearest StageGridAuthoring not found or incomplete.", MessageType.Warning);
            }
        }

        private void OnSceneGUI()
        {
            var marker = (StageRegionAnchorMarker)target;
            if (StageAnchorTransformEditorUtility.SyncRegionDataFromTransformIfChanged(marker, recordUndo: true))
                SceneView.RepaintAll();
        }

        private static bool TryComputeWorldPosition(StageRegionAnchorMarker marker, out Vector3 world)
        {
            world = default;
            if (marker == null)
                return false;

            var stageNode = marker.GetComponentInParent<StageLayoutStageMarker>();
            if (stageNode == null || !stageNode.TryGetComponent(out StageGridAuthoring authoring) || authoring == null || authoring.Grid == null)
                return false;

            return StageAnchorTransformEditorUtility.TryGetWorldPosition(
                authoring,
                marker.AnchorCell,
                marker.AnchorOffset,
                out world);
        }
    }
}
