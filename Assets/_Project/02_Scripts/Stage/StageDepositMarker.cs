using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageDepositMarker : MonoBehaviour
    {
        [Min(1)] public uint StableId = 1;
        public bool Active = true;
        [Min(0f)] public float Radius = 1.2f;

        [Header("Debug")]
        public bool DrawGizmo = true;

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo)
                return;

            var previousColor = Gizmos.color;
            Gizmos.color = Active ? new Color(0.2f, 0.7f, 1f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, Radius));
            Gizmos.color = previousColor;
        }
    }
}
