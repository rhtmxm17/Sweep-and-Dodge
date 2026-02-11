using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [System.Serializable]
    public struct BulletPoolEntryAuthoring
    {
        public int TypeKey;
        public GameObject Prefab;
        public int PoolSize;
        public BulletCaptureRuleId CaptureRule;
    }

    /// <summary>
    /// 키 기반 Bullet 풀 정의 목록을 엔티티 버퍼로 베이크한다.
    /// </summary>
    public class BulletVisualPrefabAuthoring : MonoBehaviour
    {
        [Header("Key-Pool Definitions")]
        public BulletPoolEntryAuthoring[] Entries;

        private class BulletVisualPrefabBaker : Baker<BulletVisualPrefabAuthoring>
        {
            public override void Bake(BulletVisualPrefabAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);
                AddComponent<BulletPoolRegistryTag>(e);
                var buffer = AddBuffer<BulletPoolDefinitionBuffer>(e);

                if (authoring.Entries == null)
                    return;

                for (int i = 0; i < authoring.Entries.Length; i++)
                {
                    var entry = authoring.Entries[i];
                    if (entry.Prefab == null)
                        continue;

                    var prefabEntity = GetEntity(entry.Prefab, TransformUsageFlags.Renderable);
                    buffer.Add(new BulletPoolDefinitionBuffer
                    {
                        TypeKey = entry.TypeKey,
                        Prefab = prefabEntity,
                        PoolSize = Mathf.Max(0, entry.PoolSize),
                        CaptureRule = entry.CaptureRule
                    });
                }
            }
        }
    }
}
