using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StagePlayerStartMarker : MonoBehaviour
    {
        public bool Active = true;
        public Vector2Int AnchorCell;
        public Vector2 AnchorOffset;
        public float YawDeg;

        [Header("Debug")]
        public bool DrawGizmo = true;

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo)
                return;

            if (!TryComputePreviewWorldPose(out var position, out var rotation))
            {
                position = transform.position;
                rotation = Quaternion.Euler(0f, YawDeg, 0f);
            }

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 1f);
            Gizmos.DrawWireSphere(position, 0.3f);
            Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.9f);
            Gizmos.DrawLine(Vector3.forward * 0.9f, Vector3.forward * 0.6f + Vector3.left * 0.18f);
            Gizmos.DrawLine(Vector3.forward * 0.9f, Vector3.forward * 0.6f + Vector3.right * 0.18f);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private bool TryComputePreviewWorldPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            var stageNode = GetComponentInParent<StageLayoutStageMarker>();
            if (stageNode == null || !stageNode.TryGetComponent(out StageGridAuthoring authoring) || authoring == null)
                return false;

            var grid = authoring.BuildEditorPreviewGridSpec();
            position = StageRuntimeGridUtility.GetAnchorWorldPosition(
                in grid,
                new int2(AnchorCell.x, AnchorCell.y),
                new float2(AnchorOffset.x, AnchorOffset.y),
                authoring.GetEditorPreviewPlaneY());
            rotation = Quaternion.Euler(0f, YawDeg, 0f);
            return true;
        }
    }
}
