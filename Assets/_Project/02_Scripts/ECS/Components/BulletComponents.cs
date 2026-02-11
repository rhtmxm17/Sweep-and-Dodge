using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum BulletCaptureRuleId : byte
    {
        StandardCollectible = 0,
        RiskTimedResolve = 1
    }

    public struct BulletVelocityComponent : IComponentData
    {
        public float2 Value;
    }

    public struct BulletRadiusComponent : IComponentData
    {
        public float Value;
    }

    public struct BulletTypeKeyComponent : IComponentData
    {
        public int Value;
    }

    public struct BulletCaptureRuleComponent : IComponentData
    {
        public BulletCaptureRuleId Value;
    }

    public struct BulletLifetimeComponent : IComponentData
    {
        public float Value;
    }

    // Source 기반 스폰/고갈 추적을 위한 출처 참조
    public struct BulletSourceRefComponent : IComponentData
    {
        public Entity Value;
    }

    // Enableable Tag (활성/비활성 토글용)
    public struct BulletActiveTag : IComponentData, IEnableableComponent { }

    // 제거 행동 시스템들이 디스폰을 직접 하지 않고, BulletExecutionGroup에 위임하기 위해 남기는 요청 태그.
    public struct BulletDespawnRequestTag : IComponentData, IEnableableComponent { }

    // Render 관련 버퍼 (엔티티 활성/비활성 시 참조용)
    [InternalBufferCapacity(4)]
    public struct EntityRenderElementBuffer : IBufferElementData
    {
        public Entity Value;
    }

    public struct BulletPoolRegistryTag : IComponentData { }

    [InternalBufferCapacity(8)]
    public struct BulletPoolDefinitionBuffer : IBufferElementData
    {
        public int TypeKey;
        public Entity Prefab;
        public int PoolSize;
        public BulletCaptureRuleId CaptureRule;
    }
}
