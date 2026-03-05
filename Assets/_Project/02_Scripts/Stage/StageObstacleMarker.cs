using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageObstacleMarker : MonoBehaviour
    {
        [Min(1)] public uint StableId = 1;
        public bool Active = true;
        public StageMapElementShape Shape = StageMapElementShape.Rectangle;
        [Min(0f)] public float Radius = 1f;
        public Vector2 Size = new Vector2(2f, 2f);

        [Header("Debug")]
        public bool DrawGizmo = true;

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo)
                return;

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = Active ? new Color(1f, 0.55f, 0.2f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);

            if (Shape == StageMapElementShape.Circle)
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, Radius));
            }
            else
            {
                var boxSize = new Vector3(Mathf.Max(0f, Size.x), 0f, Mathf.Max(0f, Size.y));
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, boxSize);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
