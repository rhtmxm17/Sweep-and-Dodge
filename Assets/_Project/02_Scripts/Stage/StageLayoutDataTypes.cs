using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SweepNDodge.DotsBullets
{
    [Flags]
    public enum StageCellMovementFlags : byte
    {
        None = 0,
        BlockPlayer = 1 << 0,
        BlockBullet = 1 << 1,
    }

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
    }

    public enum StageRegionKind : byte
    {
        Source = 0,
        Deposit = 1,
    }

    [Serializable]
    public struct StageGridSpec
    {
        [Min(1)] public int Width;
        [Min(1)] public int Height;
        [Min(0.0001f)] public float CellSize;
        public Vector3 Origin;
    }

    [Serializable]
    public struct StageCellLayoutData
    {
        public StageCellMovementFlags MovementFlags;
        [Min(0)] public uint SourceRegionId;
        [Min(0)] public uint DepositRegionId;
    }

    [Serializable]
    public struct StageSourceRegionLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector2Int AnchorCell;
        public Vector2 AnchorOffset;
    }

    [Serializable]
    public struct StageDepositRegionLayoutData
    {
        [Min(1)] public uint StableId;
        public bool Active;
        public Vector2Int AnchorCell;
        public Vector2 AnchorOffset;
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
