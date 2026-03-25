using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StageRegionAnchorMarker : MonoBehaviour
    {
        public StageRegionKind RegionKind;
        [Min(1)] public uint StableId = 1;
        public bool Active = true;
        public Vector2Int AnchorCell;
        public Vector2 AnchorOffset;

        [Header("Debug")]
        public bool DrawGizmo = true;

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo)
                return;

            var previousColor = Gizmos.color;
            Gizmos.color = RegionKind == StageRegionKind.Source
                ? new Color(0.15f, 0.9f, 0.35f, 1f)
                : new Color(0.2f, 0.7f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.color = previousColor;
        }
    }
}
