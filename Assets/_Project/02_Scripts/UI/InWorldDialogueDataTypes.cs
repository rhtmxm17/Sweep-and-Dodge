using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum InWorldDialogueTriggerId : byte
    {
        None = 0,
        StageStart = 1,
        StageClear = 2,
        ThemeTransition = 3,
        InterventionCarryFull = 4,
        InterventionFirstHit = 5,
    }

    public enum InWorldDialogueTargetKind : byte
    {
        Stage = 0,
        Theme = 1,
        Global = 2,
    }

    public enum InWorldDialogueBlockingMode : byte
    {
        OverlayOnly = 0,
        GateIntro = 1,
        GateClear = 2,
        ShellOverlay = 3,
    }

    public enum InWorldDialogueRetryPolicy : byte
    {
        AlwaysFull = 0,
        ShortOnRetry = 1,
        SkipOnRetry = 2,
        OncePerSession = 3,
    }

    public enum InWorldDialogueAnchorKind : byte
    {
        None = 0,
        StagePresentationStableId = 1,
        ScreenAnchor = 2,
    }

    public enum InWorldDialogueScreenAnchorId : byte
    {
        None = 0,
        Center = 1,
        LowerCenter = 2,
        LeftActor = 3,
        RightActor = 4,
    }

    public enum DialoguePortraitSide : byte
    {
        Auto = 0,
        Left = 1,
        Right = 2,
    }

    [Serializable]
    public struct InWorldDialogueAnchorRef
    {
        public InWorldDialogueAnchorKind Kind;
        [Min(0)] public uint StagePresentationStableId;
        public InWorldDialogueScreenAnchorId ScreenAnchor;
    }

    [Serializable]
    public struct InWorldDialogueLine
    {
        public string SpeakerKey;
        [TextArea(2, 4)] public string Text;
        public InWorldDialogueAnchorRef Anchor;
        [Min(0f)] public float MinHoldSec;
        [Min(0f)] public float AutoAdvanceSec;
    }

    [Serializable]
    public struct InWorldDialogueSequenceVariant
    {
        public InWorldDialogueLine[] Lines;

        public bool HasLines => Lines != null && Lines.Length > 0;
    }

    [Serializable]
    public struct InWorldDialogueCatalogEntry
    {
        public string EntryKey;
        public bool Enabled;
        public InWorldDialogueTriggerId Trigger;
        public InWorldDialogueTargetKind TargetKind;
        [Min(0)] public int StageId;
        public string ThemeKey;
        public int Priority;
        public InWorldDialogueBlockingMode BlockingMode;
        public InWorldDialogueRetryPolicy RetryPolicy;
        public InWorldDialogueSequenceVariant FullVariant;
        public InWorldDialogueSequenceVariant RetryVariant;
    }

    [Serializable]
    public struct InWorldDialogueSpeakerProfile
    {
        public string SpeakerKey;
        public string DisplayName;
        public Sprite Portrait;
        public DialoguePortraitSide PortraitSide;
    }
}
