using System;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum ObstacleShape : byte
    {
        Circle = 0,
        Box = 1,
    }

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
        public ObstacleShape Shape;
        public float Radius;
        public float2 Size;
    }
}
