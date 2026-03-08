using System.Text;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public abstract class DepositRuntimeTemplateAuthoringBase : MonoBehaviour
    {
        [Header("Identity")]
        [Min(0)] public uint StableIdOverride = 0;

        [Min(0f)] public float Radius = 1.2f;

        [Header("Debug")]
        public bool DrawGizmo = true;
        public bool DrawGizmoWhenNotSelected = false;

        protected static void BakeRuntimeTemplate<TAuthoring>(Baker<TAuthoring> baker, DepositRuntimeTemplateAuthoringBase authoring)
            where TAuthoring : DepositRuntimeTemplateAuthoringBase
        {
            var e = baker.GetEntity(TransformUsageFlags.Dynamic);
            uint stableId = authoring.StableIdOverride > 0
                ? authoring.StableIdOverride
                : ComputeStableDepositId(authoring.transform, authoring.transform.position);

            baker.AddComponent(e, new DepositPointComponent
            {
                Radius = Mathf.Max(0f, authoring.Radius)
            });

            baker.AddComponent(e, new DepositStableIdComponent
            {
                Value = stableId == 0 ? 1u : stableId,
            });
        }

        private static uint ComputeStableDepositId(Transform depositTransform, Vector3 depositPosition)
        {
            if (depositTransform == null)
                return 1u;

            var sb = new StringBuilder(128);
            AppendHierarchyPath(sb, depositTransform);

            int px = Mathf.RoundToInt(depositPosition.x * 100f);
            int py = Mathf.RoundToInt(depositPosition.y * 100f);
            int pz = Mathf.RoundToInt(depositPosition.z * 100f);
            sb.Append('|').Append(px).Append(',').Append(py).Append(',').Append(pz);

            uint hash = 2166136261u;
            for (int i = 0; i < sb.Length; i++)
            {
                hash ^= sb[i];
                hash *= 16777619u;
            }

            return hash == 0 ? 1u : hash;
        }

        private static void AppendHierarchyPath(StringBuilder sb, Transform t)
        {
            if (t.parent != null)
            {
                AppendHierarchyPath(sb, t.parent);
                sb.Append('/');
            }

            sb.Append(t.name);
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
