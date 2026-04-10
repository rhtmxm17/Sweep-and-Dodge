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

    public enum HazardActorPresenceTriggerMode : byte
    {
        None = 0,
        Immediate = 1,
        SourceAvailable = 2,
        SourceDepleted = 3,
        SourceOccupied = 4,
    }

    public struct HazardActorPresencePolicyComponent : IComponentData
    {
        public HazardActorPresenceTriggerMode ActivationTrigger;
        public float ActivationDurationSec;
        public HazardActorPresenceTriggerMode RetireTrigger;
        public float RetireDurationSec;
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

    public enum HazardActorSelectionModeId : byte
    {
        OrderedPriority = 0,
        OrderedCycle = 1,
    }

    public struct HazardActorBehaviorPhaseBaselineComponent : IComponentData
    {
        public int InitialPhaseId;
    }

    public struct HazardActorBehaviorPhaseStateComponent : IComponentData
    {
        public int CurrentPhaseId;
        public int PreviousPhaseId;
        public uint PhaseVersion;
    }

    [InternalBufferCapacity(2)]
    public struct HazardActorPhaseSelectorPolicyBuffer : IBufferElementData
    {
        public int PhaseId;
        public HazardActorSelectionModeId SelectionMode;
    }

    [InternalBufferCapacity(4)]
    public struct HazardActorPhaseSelectorCandidateBuffer : IBufferElementData
    {
        public int PhaseId;
        public int OrderIndex;
        public int EmitterId;
        public int PatternSlotId;
    }

    public struct HazardActorPatternSelectorStateComponent : IComponentData
    {
        public int TargetEmitterId;
        public int CurrentPatternSlotId;
        public int LastPatternSlotId;
        public uint SelectionSequence;
        public int CurrentCandidateOrder;
        public uint LastResolvedPhaseVersion;
        public uint LastConsumedCycleVersion;
    }

    public enum HazardActorPresencePresentationCueId : byte
    {
        None = 0,
        ActivationStarted = 1,
        RetireStarted = 2,
    }

    public struct HazardActorPresencePresentationSignalComponent : IComponentData
    {
        public uint Version;
        public HazardActorPresencePresentationCueId Cue;
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
