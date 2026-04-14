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

    public enum HazardActorEnabledOverrideMode : byte
    {
        Inherit = 0,
        ForceEnabled = 1,
        ForceDisabled = 2,
    }

    public enum HazardActorSuppressionOverrideMode : byte
    {
        Inherit = 0,
        ForceUnsuppressed = 1,
        ForceSuppressed = 2,
    }

    [Serializable]
    public struct HazardActorBinding
    {
        [Min(1)] public int ActorId;
        public HazardActorEnabledOverrideMode EnabledMode;
        public HazardActorSuppressionOverrideMode StartSuppressedMode;
        public HazardEmitterBinding[] Emitters;
    }

    [Serializable]
    public struct HazardEmitterBinding
    {
        [Min(1)] public int EmitterId;
        public bool OverrideLocalOffset;
        public Vector3 LocalOffset;
        public HazardEmitterTelegraphProfileSO TelegraphProfileOverride;
        public HazardEmitterEmissionProfileSO EmissionProfileOverride;
    }

    [Serializable]
    public struct HazardActorPlacementBinding
    {
        [Min(1)] public int PlacementInstanceId;
        public GameObject ActorArchetypePrefab;
        public Vector3 LocalOffset;
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
        public HazardActorBinding[] HazardActors;
        public HazardActorPlacementBinding[] HazardActorPlacements;
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
