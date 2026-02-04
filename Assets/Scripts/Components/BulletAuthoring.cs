using Unity.Entities;
using UnityEngine;

namespace SweepnDodge.DotsBullets
{
    public class BulletAuthoring : MonoBehaviour
    {
        [Header("Default Bullet Data (spawn 시 덮어씌워짐)")]
        public float Radius = 0.05f;
        public BulletKindId Kind = BulletKindId.Trash;

        private class BulletBaker : Baker<BulletAuthoring>
        {
            public override void Bake(BulletAuthoring authoring)
            {
                // Renderable 프리펩이어야 MaterialMeshInfo 등이 함께 베이크됨
                var e = GetEntity(TransformUsageFlags.Renderable);

                AddComponent(e, new BulletVelocityComponent { Value = Unity.Mathematics.float2.zero });
                AddComponent(e, new BulletRadiusComponent { Value = authoring.Radius });
                AddComponent(e, new BulletKindComponent { Value = authoring.Kind });
                AddComponent(e, new BulletLifetimeComponent { Value = 0f });

                // 풀에서 enable/disable로 운용
                AddComponent<BulletActiveTag>(e);
            }
        }
    }
}
