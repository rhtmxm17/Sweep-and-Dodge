using Unity.Entities;
using Unity.Mathematics;

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

    public struct HazardActorPresencePolicyComponent : IComponentData
    {
        public float ActivationDurationSec;
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
        public int PatternSlotId;
    }

    public struct HazardActorPatternSelectorStateComponent : IComponentData
    {
        public int CurrentPatternSlotId;
        public int LastPatternSlotId;
        public uint SelectionSequence;
        public int CurrentCandidateOrder;
        public uint LastResolvedPhaseVersion;
        public uint LastConsumedCycleVersion;
    }

    [InternalBufferCapacity(1)]
    public struct HazardActorPatternSlotBuffer : IBufferElementData
    {
        public int PatternSlotId;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
        public float BaseWeight;
        public uint AvailabilityFlags;
    }

    [InternalBufferCapacity(1)]
    public struct HazardActorPatternExecutionSlotBuffer : IBufferElementData
    {
        public int PatternSlotId;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
        public float TelegraphDurationSec;
        public float3 LocalOffset;

        public int BulletTypeKey;

        public WavePositionPatternModeId PositionPatternMode;
        public float2 SpawnOffset;
        public float2 LineStart;
        public float2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public float2 Point0;
        public float2 Point1;
        public float2 Point2;
        public float2 Point3;

        public WaveAimModeId AimMode;
        public WaveAimSnapshotTimingId AimSnapshotTiming;
        public float BaseAngleDeg;
        public float AimAngleOffsetDeg;
        public WaveLineNormalSideId LineNormalSide;
        public float LineNormalAngleOffsetDeg;
        public float SpiralStepDeg;

        public WaveShotPatternModeId ShotPatternMode;
        public int ShotCount;
        public float NWayAngleSpacingDeg;

        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;
        public int EventRepeatCount;
        public float CooldownSec;
    }

    public enum HazardActorPhaseTransitionStateId : byte
    {
        Idle = 0,
        Preparing = 1,
    }

    public enum HazardActorPhaseTransitionReasonId : byte
    {
        ProgressThreshold = 0,
    }

    public enum HazardActorPhaseTransitionSignalCueId : byte
    {
        None = 0,
        PreparingStarted = 1,
        PhaseCommitted = 2,
    }

    [InternalBufferCapacity(1)]
    public struct HazardActorPhaseProgressTransitionBuffer : IBufferElementData
    {
        public int FromPhaseId;
        public int ToPhaseId;
        public float ProgressThresholdNormalized;
        public float TransitionLeadInSec;
    }

    public struct HazardActorPhaseTransitionRuntimeComponent : IComponentData
    {
        public HazardActorPhaseTransitionStateId State;
        public int PendingFromPhaseId;
        public int PendingToPhaseId;
        public float ElapsedSec;
        public float DurationSec;
        public uint TransitionVersion;
    }

    public struct HazardActorPhaseTransitionSignalComponent : IComponentData
    {
        public uint Version;
        public HazardActorPhaseTransitionSignalCueId Cue;
        public HazardActorPhaseTransitionReasonId Reason;
        public int PreviousPhaseId;
        public int CurrentPhaseId;
        public int PendingToPhaseId;
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

    public struct HazardActorPlacementComponent : IComponentData
    {
        public int PlacementInstanceId;
        public float3 LocalOffset;
        public float LocalYawDeg;
    }

    public struct HazardActorOrchestrationRequestSignalComponent : IComponentData
    {
        public uint Version;
        public HazardActorOrchestrationActionId ActionType;
        public int TargetPhaseId;
    }

    public struct HazardActorOrchestrationRequestConsumptionComponent : IComponentData
    {
        public uint LastPresenceRequestVersion;
        public uint LastPhaseRequestVersion;
    }

    [InternalBufferCapacity(4)]
    public struct SourceHazardActorRefBuffer : IBufferElementData
    {
        public Entity ActorEntity;
        public int ActorId;
    }

    [InternalBufferCapacity(4)]
    public struct SourceHazardActorPlacementRefBuffer : IBufferElementData
    {
        public int PlacementInstanceId;
        public Entity ActorEntity;
    }

    [InternalBufferCapacity(4)]
    public struct SourceHazardActorOrchestrationRuleBuffer : IBufferElementData
    {
        public int RuleId;
        public int TargetPlacementInstanceId;
        public HazardActorOrchestrationActionId ActionType;
        public HazardActorOrchestrationTriggerId TriggerType;
        public float TriggerThresholdNormalized;
        public int TargetPhaseId;
    }

    [InternalBufferCapacity(4)]
    public struct SourceHazardActorOrchestrationRuleStateBuffer : IBufferElementData
    {
        public int RuleId;
        public byte HasFired;
    }
}
