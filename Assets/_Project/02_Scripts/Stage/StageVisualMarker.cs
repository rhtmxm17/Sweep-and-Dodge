using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageVisualMarker : MonoBehaviour
    {
        [Min(1)] public uint StableId = 1;
        public bool Active = true;
        public string VisualKey = string.Empty;

        [Header("Debug")]
        public bool DrawGizmo = true;

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo)
                return;

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = Active ? new Color(0.8f, 0.25f, 1f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f));
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
