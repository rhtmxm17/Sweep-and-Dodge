using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 키 기반 Bullet 풀 정의 목록을 엔티티 버퍼로 베이크한다.
    /// </summary>
    public class BulletVisualPrefabAuthoring : MonoBehaviour
    {
        [Header("Bullet Definitions")]
        public BulletDefinitionSO[] Definitions;

        private class BulletVisualPrefabBaker : Baker<BulletVisualPrefabAuthoring>
        {
            public override void Bake(BulletVisualPrefabAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);
                AddComponent<BulletPoolRegistryTag>(e);
                var buffer = AddBuffer<BulletPoolDefinitionBuffer>(e);

                if (authoring.Definitions == null)
                    return;

                var uniqueKeys = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < authoring.Definitions.Length; i++)
                {
                    var def = authoring.Definitions[i];
                    if (def == null || def.Prefab == null)
                        continue;
                    if (!uniqueKeys.Add(def.DefinitionId))
                    {
                        Debug.LogWarning($"[BulletVisualPrefabAuthoring] Duplicate DefinitionId detected: {def.DefinitionId}. Skipping.");
                        continue;
                    }

                    var prefabEntity = GetEntity(def.Prefab, TransformUsageFlags.Renderable);
                    buffer.Add(new BulletPoolDefinitionBuffer
                    {
                        TypeKey = def.DefinitionId,
                        Prefab = prefabEntity,
                        PoolSize = Mathf.Max(0, def.PoolSize),
                        CaptureRule = def.CaptureRule,
                        Speed = Mathf.Max(0f, def.Speed),
                        Lifetime = Mathf.Max(0f, def.Lifetime),
                        Radius = Mathf.Max(0f, def.Radius),
                        ScoreValue = Mathf.Max(0, def.ScoreValue),
                        MovementFamily = def.MovementFamily,
                        DampedLinear = def.DampedLinear,
                        HomingLite = def.HomingLite,
                        OnMotionCompletedExplode = BulletDefinitionBakeUtility.CreateRuntimeReactionDefinition(def.OnMotionCompletedExplode),
                        OnCleanupRemovedSpawnSecondary = BulletDefinitionBakeUtility.CreateRuntimeReactionDefinition(def.OnCleanupRemovedSpawnSecondary),
                    });
                }
            }
        }
    }
}
