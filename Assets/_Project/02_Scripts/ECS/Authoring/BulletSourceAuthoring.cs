using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletSourceAuthoring : MonoBehaviour
    {
        [Header("Source Spawn")]
        public float Radius = 8f;
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
                    Radius = Mathf.Max(0f, authoring.Radius),
                    ThresholdWeakened = thresholdWeakened,
                    ThresholdDepleted = thresholdDepleted,
                    CollectedCount = Mathf.Max(0, authoring.InitialCollectedCount),
                    State = authoring.InitialState
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
                            SpawnRatePerSec = Mathf.Max(0f, entry.SpawnRatePerSec),
                            MaxActive = Mathf.Max(0, entry.MaxActive),
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
