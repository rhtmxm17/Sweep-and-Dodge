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

            float cellSize = authoring.Grid.cellSize.x;
            world = authoring.Grid.transform.position
                + new Vector3((marker.AnchorCell.x + marker.AnchorOffset.x + 0.5f) * cellSize, 0f, (marker.AnchorCell.y + marker.AnchorOffset.y + 0.5f) * cellSize);
            return true;
        }
    }
}
