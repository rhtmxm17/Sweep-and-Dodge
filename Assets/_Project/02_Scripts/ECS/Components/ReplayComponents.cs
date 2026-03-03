using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum ReplayInputModeId : byte
    {
        Off = 0,
        Record = 1,
        Playback = 2,
    }

    public struct ReplayInputControlComponent : IComponentData
    {
        public ReplayInputModeId Mode;
        public uint LastRecordedFrame;
        public uint LastPlaybackFrame;
        public int MissingFrameCount;
    }

    public struct ReplayInputCursorComponent : IComponentData
    {
        public int NextFrameIndex;
    }

    [InternalBufferCapacity(64)]
    public struct ReplayInputFrameBufferElement : IBufferElementData
    {
        public uint Frame;
        public float3 Position;
        public quaternion Rotation;
        public byte SyncRotation;
        public byte VacuumRequested;
        public byte CleanupActionRequested;
        public byte RequestedCleanupActionSlot;
    }
}
