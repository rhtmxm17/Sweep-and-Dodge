using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletSourceAuthoring : MonoBehaviour
    {
        [Header("Source Spawn")]
        public float Radius = 8f;
        public float SpawnRateNormal = 5000f;
        [Range(0f, 1f)] public float WeakenedMultiplier = 0.5f;
        [Range(0f, 1f)] public float HazardRatioNormal = 0.04f;
        [Range(0f, 1f)] public float HazardRatioWeakened = 0.10f;
        [Range(0f, 1f)] public float HazardRatioNearDepleted = 0.18f;

        [Header("Depletion Threshold (externally injectable)")]
        public int ThresholdWeakened = 2000;
        public int ThresholdDepleted = 4000;
        public int InitialCollectedCount = 0;
        public SourceStateId InitialState = SourceStateId.Normal;

        [Header("Debug")]
        public bool DrawGizmo = true;
        public bool DrawGizmoWhenNotSelected = false;

        private class Baker : Baker<BulletSourceAuthoring>
        {
            public override void Bake(BulletSourceAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);

                int thresholdWeakened = Mathf.Max(0, authoring.ThresholdWeakened);
                int thresholdDepleted = Mathf.Max(thresholdWeakened, authoring.ThresholdDepleted);

                AddComponent(e, new SourceSpawnComponent
                {
                    Radius = Mathf.Max(0f, authoring.Radius),
                    SpawnRateNormal = Mathf.Max(0f, authoring.SpawnRateNormal),
                    WeakenedMultiplier = Mathf.Clamp01(authoring.WeakenedMultiplier),
                    HazardRatioNormal = Mathf.Clamp01(authoring.HazardRatioNormal),
                    HazardRatioWeakened = Mathf.Clamp01(authoring.HazardRatioWeakened),
                    HazardRatioNearDepleted = Mathf.Clamp01(authoring.HazardRatioNearDepleted),
                    ThresholdWeakened = thresholdWeakened,
                    ThresholdDepleted = thresholdDepleted,
                    CollectedCount = Mathf.Max(0, authoring.InitialCollectedCount),
                    State = authoring.InitialState
                });

                AddComponent(e, new SourceSpawnRuntimeComponent
                {
                    SpawnAccumulator = 0f,
                    SpawnSequence = 1u
                });

                AddComponent(e, new SourceAnchorComponent
                {
                    Position = (float3)authoring.transform.position
                });
            }
        }

        private void OnDrawGizmos()
        {
            if (!DrawGizmo || !DrawGizmoWhenNotSelected)
                return;
            DrawSourceGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo || DrawGizmoWhenNotSelected)
                return;
            DrawSourceGizmo();
        }

        private void DrawSourceGizmo()
        {
            var prev = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, Radius));
            Gizmos.color = prev;
        }
    }
}
