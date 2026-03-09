using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageSourceMarker : MonoBehaviour
    {
        [Min(1)] public uint StableId = 1;
        public bool Active = true;

        [Header("Field")]
        public Shape2DKind Shape = Shape2DKind.Circle;
        [Min(0f)] public float Radius = 8f;
        public Vector2 Size = new Vector2(12f, 8f);

        [Header("Debug")]
        public bool DrawGizmo = true;

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo)
                return;

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = Active ? new Color(0.15f, 0.9f, 0.35f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);

            if (Shape == Shape2DKind.Rectangle)
            {
                var size = new Vector3(Mathf.Max(0f, Size.x), 0f, Mathf.Max(0f, Size.y));
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, Radius));
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void OnValidate()
        {
            var euler = transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) > 0.001f || Mathf.Abs(Mathf.DeltaAngle(0f, euler.z)) > 0.001f)
            {
                transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
            }
        }
    }
}
