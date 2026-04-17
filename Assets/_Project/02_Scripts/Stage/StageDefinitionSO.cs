using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [Serializable]
    public struct SustainSlotBinding
    {
        public SourceStateId State;
        public SourceSpawnLaneId Lane;
        public WaveClipSO[] Clips;
        public float[] Weights;
    }

    [Serializable]
    public struct EventSlotBinding
    {
        public SourceStateId TriggerState;
        public WaveClipSO[] EventClips;
    }

    [Serializable]
    public struct HazardActorPlacementBinding
    {
        [Min(1)] public int PlacementInstanceId;
        public GameObject ActorArchetypePrefab;
        public Vector3 LocalOffset;
        public float LocalYawDeg;
    }

    public enum HazardActorOrchestrationActionId : byte
    {
        None = 0,
        Spawn = 1,
        PhaseSet = 2,
        Retire = 3,
    }

    public enum HazardActorOrchestrationTriggerId : byte
    {
        None = 0,
        OnStageStart = 1,
        OnSourceProgressAtOrAbove = 2,
    }

    [Serializable]
    public struct HazardActorOrchestrationRuleBinding
    {
        [Min(1)] public int RuleId;
        public int[] TargetPlacementInstanceIds;
        public HazardActorOrchestrationActionId ActionType;
        public HazardActorOrchestrationTriggerId TriggerType;
        [Range(0f, 1f)] public float TriggerThresholdNormalized;
        [Min(1)] public int TargetPhaseId;

        public int TargetPlacementInstanceId
        {
            get => TargetPlacementInstanceIds != null && TargetPlacementInstanceIds.Length > 0
                ? TargetPlacementInstanceIds[0]
                : 0;
            set => TargetPlacementInstanceIds = value > 0
                ? new[] { value }
                : Array.Empty<int>();
        }
    }

    [Serializable]
    public struct StageSourceBinding
    {
        [Min(1)] public uint SourceStableId;
        public SourceStateId InitialSourceState;
        [Min(0)] public int ThresholdWeakened;
        [Min(0)] public int ThresholdDepleted;
        public SustainSlotBinding[] SustainSlots;
        public EventSlotBinding[] EventSlots;
        public HazardActorPlacementBinding[] HazardActorPlacements;
        public HazardActorOrchestrationRuleBinding[] HazardActorOrchestrationRules;
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Definition", fileName = "sd_")]
    public class StageDefinitionSO : ScriptableObject
    {
        [Min(1)] public int StageId = 1;
        public string DisplayName;
        public bool IsFinalStage;
        [Min(0.01f)] public float StageTimeLimitSec = 150f;
        public StageSourceBinding[] SourceBindings;
    }
}
