using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.ECS
{
    public struct BulletTag : IComponentData { }

    public struct Velocity2DComponent : IComponentData
    {
        public float2 Value;
    }

    public struct LifetimeComponent : IComponentData
    {
        public float Value;
    }

    public struct RadiusComponent : IComponentData
    {
        public float Value;
    }

    public struct DamageComponent : IComponentData
    {
        public float Value;
    }
}