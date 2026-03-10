using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SweepNDodge.DotsBullets
{
    public enum StagePresentationPlacementMode : byte
    {
        Standalone = 0,
        LinkedToParent = 1,
    }

    public enum StagePresentationLinkKind : byte
    {
        None = 0,
        Source = 1,
        Deposit = 2,
        Obstacle = 3,
    }

    [Serializable]
    public struct StageSourceLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public Shape2DKind Shape;
        [Min(0f)] public float Radius;
        public Vector2 Size;
    }

    [Serializable]
    public struct StageDepositLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public Shape2DKind Shape;
        [Min(0f)] public float Radius;
        public Vector2 Size;
    }

    [Serializable]
    public struct StageObstacleLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public Shape2DKind Shape;
        [Min(0f)] public float Radius;
        public Vector2 Size;
        public ObstacleCollisionMask CollisionMask;
    }

    [Serializable]
    public struct StagePresentationLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public StagePresentationPlacementMode PlacementMode;
        public StagePresentationLinkKind LinkKind;
        [Min(0)] public uint LinkedStableId;
        public string PresentationKey;
        public Vector3 Position;
        [FormerlySerializedAs("YawDeg")] public Vector3 Euler;
        public Vector3 Scale;
    }
}
