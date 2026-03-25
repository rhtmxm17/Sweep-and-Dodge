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

            var grid = new StageGridSpec
            {
                Width = authoring.SourceRegionPaint != null ? authoring.SourceRegionPaint.Width : 1,
                Height = authoring.SourceRegionPaint != null ? authoring.SourceRegionPaint.Height : 1,
                CellSize = authoring.Grid.cellSize.x,
                Origin = new Vector3(authoring.Grid.transform.position.x, authoring.Grid.transform.position.y, authoring.Grid.transform.position.z),
            };
            world = StageRuntimeGridUtility.GetAnchorWorldPosition(
                in grid,
                new Unity.Mathematics.int2(marker.AnchorCell.x, marker.AnchorCell.y),
                new Unity.Mathematics.float2(marker.AnchorOffset.x, marker.AnchorOffset.y),
                authoring.Grid.transform.position.y);
            return true;
        }
    }
}
