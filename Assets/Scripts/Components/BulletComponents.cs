using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum BulletKindId : byte
    {
        Trash = 0,
        Hazard = 1
    }

    public struct BulletVelocityComponent : IComponentData
    {
        public float2 Value;
    }

    public struct BulletRadiusComponent : IComponentData
    {
        public float Value;
    }

    public struct BulletKindComponent : IComponentData
    {
        public BulletKindId Value;
    }

    public struct BulletLifetimeComponent : IComponentData
    {
        public float Value;
    }

    // Enableable Tag (활성/비활성 토글용)
    public struct BulletActiveTag : IComponentData, IEnableableComponent { }

    // 제거 행동 시스템들이 디스폰을 직접 하지 않고, BulletExecutionGroup에 위임하기 위해 남기는 요청 태그.
    public struct BulletDespawnRequestTag : IComponentData, IEnableableComponent { }

    // Render 관련 버퍼 (엔티티 활성/비활성 시 참조용)
    public struct EntityRenderElementBuffer : IBufferElementData
    {
        public Entity Value;
    }

    public struct BulletVisualPrefabComponent : IComponentData
    {
        public Entity Value; // baked entity prefab
    }

    // Singleton Config
    public struct BulletFieldConfigComponent : IComponentData
    {
        public int PoolSize;
        public int MaxActiveTarget;

        public float CellSize;
        public float InvCellSize;

        public float BulletLifetime;
        public float SpawnRate; // bullets/sec (평균)
    }

    public struct ScoreComponent : IComponentData
    {
        public long Value;
    }
}
