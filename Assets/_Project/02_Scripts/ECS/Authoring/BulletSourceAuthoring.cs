using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletSourceAuthoring : MonoBehaviour
    {
        [Header("Source Field")]
        public BulletFieldShapeId FieldShape = BulletFieldShapeId.Circle;
        public float FieldRadius = 8f;
        public Vector2 FieldSize = new Vector2(12f, 8f);
        public BulletSourceProfileSO SpawnProfile;

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
                    ThresholdWeakened = thresholdWeakened,
                    ThresholdDepleted = thresholdDepleted,
                    CollectedCount = Mathf.Max(0, authoring.InitialCollectedCount),
                    State = authoring.InitialState
                });

                AddComponent(e, new BulletFieldAreaComponent
                {
                    Shape = authoring.FieldShape,
                    Radius = Mathf.Max(0f, authoring.FieldRadius),
                    Size = new float2(Mathf.Max(0f, authoring.FieldSize.x), Mathf.Max(0f, authoring.FieldSize.y)),
                    ComputedArea = ComputeArea(authoring.FieldShape, authoring.FieldRadius, authoring.FieldSize)
                });

                AddComponent(e, new SourceSpawnRuntimeComponent
                {
                    SpawnSequence = 1u
                });

                var patternBuffer = AddBuffer<SourceSpawnPatternBuffer>(e);
                var activeCountBuffer = AddBuffer<SourceActiveBulletCountBuffer>(e);
                BakeSpawnProfile(authoring.SpawnProfile, patternBuffer, activeCountBuffer);

                AddComponent(e, new SourceAnchorComponent
                {
                    Position = (float3)authoring.transform.position
                });
            }

            private void BakeSpawnProfile(
                BulletSourceProfileSO profile,
                DynamicBuffer<SourceSpawnPatternBuffer> patternBuffer,
                DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer)
            {
                if (profile == null || profile.States == null)
                    return;

                var activeCountKeys = new System.Collections.Generic.HashSet<int>();

                for (int i = 0; i < profile.States.Length; i++)
                {
                    var stateConfig = profile.States[i];
                    var entries = stateConfig.Entries;
                    if (entries == null)
                        continue;

                    for (int j = 0; j < entries.Length; j++)
                    {
                        var entry = entries[j];
                        if (entry.Bullet == null)
                            continue;

                        int typeKey = entry.Bullet.DefinitionId;
                        patternBuffer.Add(new SourceSpawnPatternBuffer
                        {
                            State = stateConfig.State,
                            BulletTypeKey = typeKey,
                            SpawnMode = entry.SpawnMode,
                            SpawnDensityPerSecPerArea = Mathf.Max(0f, entry.SpawnDensityPerSecPerArea),
                            MaxActiveDensityPerArea = Mathf.Max(0f, entry.MaxActiveDensityPerArea),
                            SpawnAccumulator = 0f
                        });

                        if (activeCountKeys.Add(typeKey))
                        {
                            activeCountBuffer.Add(new SourceActiveBulletCountBuffer
                            {
                                BulletTypeKey = typeKey,
                                ActiveCount = 0
                            });
                        }
                    }
                }
            }
        }

        private static float ComputeArea(BulletFieldShapeId shape, float radius, Vector2 size)
        {
            if (shape == BulletFieldShapeId.Rectangle)
                return Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y);

            float r = Mathf.Max(0f, radius);
            return Mathf.PI * r * r;
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
            var prevMatrix = Gizmos.matrix;
            var prev = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            if (FieldShape == BulletFieldShapeId.Rectangle)
            {
                var size = new Vector3(Mathf.Max(0f, FieldSize.x), 0f, Mathf.Max(0f, FieldSize.y));
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = prevMatrix;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, FieldRadius));
            }
            Gizmos.color = prev;
            Gizmos.matrix = prevMatrix;
        }
    }
}
