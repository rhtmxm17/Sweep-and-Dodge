using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class DepositPointAuthoring : MonoBehaviour
    {
        [Min(0f)] public float Radius = 1.2f;

        [Header("Debug")]
        public bool DrawGizmo = true;
        public bool DrawGizmoWhenNotSelected = false;

        private class Baker : Baker<DepositPointAuthoring>
        {
            public override void Bake(DepositPointAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(e, new DepositPointComponent
                {
                    Radius = Mathf.Max(0f, authoring.Radius)
                });
            }
        }

        private void OnDrawGizmos()
        {
            if (!DrawGizmo || !DrawGizmoWhenNotSelected)
                return;
            DrawDepositGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo || DrawGizmoWhenNotSelected)
                return;
            DrawDepositGizmo();
        }

        private void DrawDepositGizmo()
        {
            var prev = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, Radius));
            Gizmos.color = prev;
        }
    }
}
