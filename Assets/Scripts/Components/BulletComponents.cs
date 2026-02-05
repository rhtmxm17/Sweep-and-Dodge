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

    // Render 관련 버퍼 (엔티티 활성/비활성 시 참조용)
    public struct BulletRenderEntityBufferElement : IBufferElementData
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
