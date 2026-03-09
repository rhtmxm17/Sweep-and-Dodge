using System;
using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    [Flags]
    public enum ObstacleCollisionMask : byte
    {
        None = 0,
        BlockPlayer = 1 << 0,
        BlockBullet = 1 << 1,
    }

    public struct StageTopologyObstacleTag : IComponentData
    {
    }

    public struct ObstacleStableIdComponent : IComponentData
    {
        public uint Value;
    }

    public struct ObstacleCollisionMaskComponent : IComponentData
    {
        public ObstacleCollisionMask Value;
    }

    public struct ObstacleGeometryComponent : IComponentData
    {
    }
}
