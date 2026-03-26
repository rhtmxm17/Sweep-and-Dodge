using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StageRegionAnchorMarker))]
    public sealed class StageRegionAnchorMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var marker = (StageRegionAnchorMarker)target;
            if (marker == null)
                return;

            if (TryComputeWorldPosition(marker, out var world))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Vector3Field("Preview World Position", world);
                }

                if (GUILayout.Button("Snap Transform To Anchor Preview"))
                {
                    Undo.RecordObject(marker.transform, "Snap Anchor Transform");
                    marker.transform.position = world;
                    EditorUtility.SetDirty(marker.transform);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Nearest StageGridAuthoring not found or incomplete.", MessageType.Warning);
            }
        }

        private static bool TryComputeWorldPosition(StageRegionAnchorMarker marker, out Vector3 world)
        {
            world = default;
            if (marker == null)
                return false;

            var stageNode = marker.GetComponentInParent<StageLayoutStageMarker>();
            if (stageNode == null || !stageNode.TryGetComponent(out StageGridAuthoring authoring) || authoring == null || authoring.Grid == null)
                return false;

            var grid = authoring.BuildEditorPreviewGridSpec();
            world = StageRuntimeGridUtility.GetAnchorWorldPosition(
                in grid,
                new Unity.Mathematics.int2(marker.AnchorCell.x, marker.AnchorCell.y),
                new Unity.Mathematics.float2(marker.AnchorOffset.x, marker.AnchorOffset.y),
                authoring.GetEditorPreviewPlaneY());
            return true;
        }
    }
}
