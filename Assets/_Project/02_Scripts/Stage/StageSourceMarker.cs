using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageSourceMarker : MonoBehaviour
    {
        [Min(1)] public uint StableId = 1;
        public bool Active = true;

        [Header("Field")]
        public BulletFieldShapeId FieldShape = BulletFieldShapeId.Circle;
        [Min(0f)] public float FieldRadius = 8f;
        public Vector2 FieldSize = new Vector2(12f, 8f);

        [Header("Debug")]
        public bool DrawGizmo = true;

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo)
                return;

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = Active ? new Color(0.15f, 0.9f, 0.35f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);

            if (FieldShape == BulletFieldShapeId.Rectangle)
            {
                var size = new Vector3(Mathf.Max(0f, FieldSize.x), 0f, Mathf.Max(0f, FieldSize.y));
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, FieldRadius));
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
