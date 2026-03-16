using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public readonly struct DialoguePresentationState
    {
        public DialoguePresentationState(
            bool visible,
            InWorldDialogueTriggerId trigger,
            InWorldDialogueBlockingMode blockingMode,
            string entryKey,
            int lineIndex,
            int lineCount,
            string speakerKey,
            string speakerDisplayName,
            Sprite speakerPortrait,
            DialoguePortraitSide portraitSide,
            string bodyText,
            InWorldDialogueAnchorRef anchor,
            bool canAdvance,
            bool canSkip,
            bool autoAdvanceEnabled,
            float lineElapsedSec,
            float minHoldSec,
            float autoAdvanceSec)
        {
            Visible = visible;
            Trigger = trigger;
            BlockingMode = blockingMode;
            EntryKey = entryKey ?? string.Empty;
            LineIndex = lineIndex;
            LineCount = lineCount;
            SpeakerKey = speakerKey ?? string.Empty;
            SpeakerDisplayName = speakerDisplayName ?? string.Empty;
            SpeakerPortrait = speakerPortrait;
            PortraitSide = portraitSide;
            BodyText = bodyText ?? string.Empty;
            Anchor = anchor;
            CanAdvance = canAdvance;
            CanSkip = canSkip;
            AutoAdvanceEnabled = autoAdvanceEnabled;
            LineElapsedSec = lineElapsedSec;
            MinHoldSec = minHoldSec;
            AutoAdvanceSec = autoAdvanceSec;
        }

        public static DialoguePresentationState Hidden => new(
            visible: false,
            trigger: InWorldDialogueTriggerId.None,
            blockingMode: InWorldDialogueBlockingMode.OverlayOnly,
            entryKey: string.Empty,
            lineIndex: 0,
            lineCount: 0,
            speakerKey: string.Empty,
            speakerDisplayName: string.Empty,
            speakerPortrait: null,
            portraitSide: DialoguePortraitSide.Auto,
            bodyText: string.Empty,
            anchor: default,
            canAdvance: false,
            canSkip: false,
            autoAdvanceEnabled: false,
            lineElapsedSec: 0f,
            minHoldSec: 0f,
            autoAdvanceSec: 0f);

        public bool Visible { get; }
        public InWorldDialogueTriggerId Trigger { get; }
        public InWorldDialogueBlockingMode BlockingMode { get; }
        public string EntryKey { get; }
        public int LineIndex { get; }
        public int LineCount { get; }
        public string SpeakerKey { get; }
        public string SpeakerDisplayName { get; }
        public Sprite SpeakerPortrait { get; }
        public DialoguePortraitSide PortraitSide { get; }
        public string BodyText { get; }
        public InWorldDialogueAnchorRef Anchor { get; }
        public bool CanAdvance { get; }
        public bool CanSkip { get; }
        public bool AutoAdvanceEnabled { get; }
        public float LineElapsedSec { get; }
        public float MinHoldSec { get; }
        public float AutoAdvanceSec { get; }
    }
}
