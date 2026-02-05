using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletAuthoring : MonoBehaviour
    {
        [Header("Default Bullet Data (spawn 시 덮어씌워짐)")]
        public float Radius = 0.05f;
        public BulletKindId Kind = BulletKindId.Trash;

        private class Baker : Baker<BulletAuthoring>
        {
            public override void Bake(BulletAuthoring authoring)
            {
                // Renderable 프리펩이어야 MaterialMeshInfo 등이 함께 베이크됨
                // *수정 예정: 로직/이동 기준 루트 엔티티와 출력 담당 자식 엔티티 분리
                //             (TransformUsageFlags Renderable → Dynamic)
                var root = GetEntity(TransformUsageFlags.Renderable);

                AddComponent(root, new BulletVelocityComponent { Value = Unity.Mathematics.float2.zero });
                AddComponent(root, new BulletRadiusComponent { Value = authoring.Radius });
                AddComponent(root, new BulletKindComponent { Value = authoring.Kind });
                AddComponent(root, new BulletLifetimeComponent { Value = 0f });

                // 풀에서 enable/disable로 운용
                AddComponent<BulletActiveTag>(root);

                // ------ 이 이후는 아직 실제로 사용되지 않음 ------
                // (Bullet 활성/비활성 사이클 조정 후 사용 예정)

                // 다중 렌더 파츠 목록
                var renderTargets = AddBuffer<BulletRenderEntityBufferElement>(root);

                // MeshRenderer/SkinnedMeshRenderer 기반으로 렌더 엔티티 수집
                // '*수정'에서 분리된 렌더 엔티티
                var unique = new HashSet<Entity>();

                foreach (var renderer in authoring.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var e = GetEntity(renderer, TransformUsageFlags.Renderable);
                    if (unique.Add(e))
                        renderTargets.Add(new BulletRenderEntityBufferElement { Value = e });
                }

                foreach (var renderer in authoring.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var e = GetEntity(renderer, TransformUsageFlags.Renderable);
                    if (unique.Add(e))
                        renderTargets.Add(new BulletRenderEntityBufferElement { Value = e });
                }

            }
        }
    }
}
