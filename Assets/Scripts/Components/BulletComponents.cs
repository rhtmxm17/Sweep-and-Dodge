using Unity.Entities;
using Unity.Mathematics;

namespace SweepnDodge.DotsBullets
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

    public struct BulletVisualPrefabComponent : IComponentData
    {
        public Entity Value; // baked entity prefab
    }

    // Player
    public struct PlayerTag : IComponentData { }

    public struct PlayerRadiusComponent : IComponentData
    {
        public float Value;
    }

    public struct VacuumBurstComponent : IComponentData
    {
        public float Range;
        public float Strength;
        public float CollectRadius;

        public float ActiveTime;
        public float ActiveTimer;

        public float Cooldown;
        public float CooldownTimer;

        public byte IsActive;           // 0/1
        public byte ActivateRequested;  // 0/1 (입력 시스템이 1로 세팅 → 이 시스템이 소모)
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
