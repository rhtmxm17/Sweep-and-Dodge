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
        public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public BulletFieldShapeId FieldShape;
        public float FieldRadius;
        public Vector2 FieldSize;
    }

    [Serializable]
    public struct StageDepositLayoutData
    {
        public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float Radius;
    }

    [Serializable]
    public struct StageObstacleLayoutData
    {
        public uint StableId;
        public bool Active;
        public Vector3 Position;
        public float YawDeg;
        public StageMapElementShape Shape;
        public float Radius;
        public Vector2 Size;
    }

    [Serializable]
    public struct StageVisualLayoutData
    {
        public uint StableId;
        public bool Active;
        public Vector3 Position;
        public Vector3 Euler;
        public Vector3 Scale;
        public string VisualKey;
    }

    [Serializable]
    public struct StageMapDefinition
    {
        [Min(1)] public int StageId;
        public StageSourceLayoutData[] Sources;
        public StageDepositLayoutData[] Deposits;
        public StageObstacleLayoutData[] Obstacles;
        public StageVisualLayoutData[] Visuals;
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Map Catalog", fileName = "smc_")]
    public class StageMapCatalogSO : ScriptableObject
    {
        public StageMapDefinition[] Stages;
    }
}
