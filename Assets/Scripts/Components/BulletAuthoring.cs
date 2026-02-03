using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.ECS
{
    // Bullet 프리팹에 붙여서 ECS 변환 시 BulletTag 등 컴포넌트 추가
    public class BulletAuthoring : MonoBehaviour
    {
        public float Lifetime = 3f;
        public float Radius = 0.1f;
        public float Damage = 1f;

        public class Baker : Baker<BulletAuthoring>
        {
            public override void Bake(BulletAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<BulletTag>(entity);
                AddComponent(entity, new LifetimeComponent { Value = authoring.Lifetime });
                AddComponent(entity, new RadiusComponent { Value = authoring.Radius });
                AddComponent(entity, new DamageComponent { Value = authoring.Damage });

                // 초기 속도는 0으로 설정(스포너에 의해 설정될 것)
                AddComponent(entity, new Velocity2DComponent { Value = float2.zero });
            }
        }
    }
}