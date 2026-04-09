using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public enum HazardActorPresenceStateId : byte
    {
        Hidden = 0,
        Activating = 1,
        Active = 2,
        Retiring = 3,
    }

    public struct HazardActorComponent : IComponentData
    {
        public int ActorId;
        public Entity SourceEntity;
    }

    public struct HazardActorAppliedConfigBaselineComponent : IComponentData
    {
        public byte IsEnabled;
        public byte IsSuppressed;
    }

    public struct HazardActorAppliedConfigComponent : IComponentData
    {
        public byte IsEnabled;
        public byte IsSuppressed;
    }

    public struct HazardActorRuntimeBaselineComponent : IComponentData
    {
        public HazardActorPresenceStateId InitialPresenceState;
    }

    public struct HazardActorRuntimeStateComponent : IComponentData
    {
        public HazardActorPresenceStateId PresenceState;
        public float StateElapsedSec;
    }

    public struct HazardActorPatternSelectorStateComponent : IComponentData
    {
        public int TargetEmitterId;
        public int CurrentPatternSlotId;
        public int LastPatternSlotId;
        public uint SelectionSequence;
    }

    [InternalBufferCapacity(4)]
    public struct SourceHazardActorRefBuffer : IBufferElementData
    {
        public Entity ActorEntity;
        public int ActorId;
    }

    [InternalBufferCapacity(4)]
    public struct HazardActorEmitterRefBuffer : IBufferElementData
    {
        public Entity EmitterEntity;
        public int EmitterId;
    }
}
