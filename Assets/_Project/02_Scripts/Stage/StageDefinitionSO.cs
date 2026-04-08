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

    public enum HazardEmitterEnabledOverrideMode : byte
    {
        Inherit = 0,
        ForceEnabled = 1,
        ForceDisabled = 2,
    }

    public enum HazardEmitterSuppressionOverrideMode : byte
    {
        Inherit = 0,
        ForceUnsuppressed = 1,
        ForceSuppressed = 2,
    }

    [Serializable]
    public struct HazardEmitterBinding
    {
        [Min(1)] public int EmitterId;
        public HazardEmitterEnabledOverrideMode EnabledMode;
        public HazardEmitterSuppressionOverrideMode StartSuppressedMode;
        public bool OverrideLocalOffset;
        public Vector3 LocalOffset;
        public HazardEmitterTelegraphProfileSO TelegraphProfileOverride;
        public HazardEmitterEmissionProfileSO EmissionProfileOverride;
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
        public HazardEmitterBinding[] HazardEmitterBindings;
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
