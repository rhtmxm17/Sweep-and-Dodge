using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum StageMapElementShape : byte
    {
        Circle = 0,
        Rectangle = 1,
    }

    [Serializable]
    public struct StageSourceLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public BulletFieldShapeId FieldShape;
        [Min(0f)] public float FieldRadius;
        public Vector2 FieldSize;
    }

    [Serializable]
    public struct StageDepositLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector3 Position;
        [Min(0f)] public float Radius;
    }

    [Serializable]
    public struct StageObstacleLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public StageMapElementShape Shape;
        [Min(0f)] public float Radius;
        public Vector2 Size;
    }

    [Serializable]
    public struct StageVisualLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public string VisualKey;
        public Vector3 Scale;
    }
}
