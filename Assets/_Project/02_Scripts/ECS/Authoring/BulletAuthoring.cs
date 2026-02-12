using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletAuthoring : MonoBehaviour
    {
        [Header("Default Bullet Data (spawn 시 덮어씌워짐)")]
        public float Radius = 0.05f;
        public int TypeKey = 0;
        public BulletCaptureRuleId CaptureRule = BulletCaptureRuleId.StandardCollectible;

        private class Baker : Baker<BulletAuthoring>
        {
            public override void Bake(BulletAuthoring authoring)
            {
                // 로직/이동 기준 루트 엔티티는 Dynamic로 베이크하고,
                // 출력(렌더) 담당 자식 엔티티들은 Renderable로 베이크해 RenderParts 버퍼에 기록한다.
                // 로직/이동 기준 루트는 Dynamic
                var root = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(root, new BulletVelocityComponent { Value = Unity.Mathematics.float2.zero });
                AddComponent(root, new BulletRadiusComponent { Value = authoring.Radius });
                AddComponent(root, new BulletTypeKeyComponent { Value = authoring.TypeKey });
                AddComponent(root, new BulletCaptureRuleComponent { Value = authoring.CaptureRule });
                AddComponent(root, new BulletLifetimeComponent { Value = 0f });
                AddComponent(root, new BulletSourceRefComponent { Value = Entity.Null });

                // 풀에서 enable/disable로 운용
                AddComponent<BulletActiveTag>(root);
                SetComponentEnabled<BulletActiveTag>(root, false);

                // 제거 요청 태그 (항상 존재, 기본 disabled)
                AddComponent<BulletDespawnRequestTag>(root);
                SetComponentEnabled<BulletDespawnRequestTag>(root, false);

                // 위험탄 분류 태그(항상 존재). CaptureRule 기준으로 enable 상태를 설정한다.
                AddComponent<BulletHazardTag>(root);
                SetComponentEnabled<BulletHazardTag>(root, authoring.CaptureRule == BulletCaptureRuleId.RiskTimedResolve);

                // 다중 렌더 파츠 목록(스폰/디스폰 시 MaterialMeshInfo enable 토글 용도)
                // - 버퍼에는 렌더 파츠 엔티티만 포함됨(외형 이외 사용 없음 전제)
                var renderTargets = AddBuffer<EntityRenderElementBuffer>(root);

                // MeshRenderer/SkinnedMeshRenderer 기반으로 렌더 엔티티 수집
                // '*수정'에서 분리된 렌더 엔티티
                var unique = new HashSet<Entity>();

                foreach (var renderer in authoring.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var e = GetEntity(renderer, TransformUsageFlags.Renderable);
                    if (unique.Add(e))
                        renderTargets.Add(new EntityRenderElementBuffer { Value = e });
                }

                foreach (var renderer in authoring.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var e = GetEntity(renderer, TransformUsageFlags.Renderable);
                    if (unique.Add(e))
                        renderTargets.Add(new EntityRenderElementBuffer { Value = e });
                }

            }
        }
    }
}
