using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class DemoShellDialogueBridge : MonoBehaviour
    {
        [Header("References")]
        public DemoShellFlowController DemoShell;
        public RuntimeUiRoot RuntimeUiRoot;
        public DemoShellGameplayPauseController PauseController;
        public InWorldDialogueCatalogSO DialogueCatalog;
        public InWorldDialogueSpeakerCatalogSO SpeakerCatalog;

        [Header("Policy")]
        public bool LogBindWarnings = true;
        public bool EnableMouseAdvanceFallback = true;

        private readonly Dictionary<string, InWorldDialogueSpeakerProfile> _speakerProfilesByKey =
            new(StringComparer.Ordinal);

        private DialoguePresentationState _currentPresentation = DialoguePresentationState.Hidden;
        private InWorldDialogueCatalogEntry _activeEntry;
        private InWorldDialogueSequenceVariant _activeVariant;
        private InWorldDialogueTriggerId _activeTrigger;
        private DemoShellStageResultMetrics _pendingClearResultContext;
        private int _activeLineIndex = -1;
        private float _lineElapsedSec;
        private bool _completionNotified;
        private bool _startTriggerIssuedForCurrentStage;
        private int _startTriggerObservedStageId;
        private bool _wasRunningLastFrame;
        private GameplayPauseHandle _gatePauseHandle;
        private bool _warnedBindFailure;

        public bool IsDialogueActive => _activeLineIndex >= 0 && _activeVariant.HasLines;
        public DialoguePresentationState CurrentPresentation => _currentPresentation;

        private void Reset()
        {
            DemoShell = GetComponent<DemoShellFlowController>();
            RuntimeUiRoot = FindFirst<RuntimeUiRoot>();
        }

        private void OnEnable()
        {
            EnsureReferences();
            BuildSpeakerLookup();
            if (DemoShell != null)
                DemoShell.PreResultClearPresentationRequested += HandlePreResultClearPresentationRequested;
            ResetActiveSequence();
            ResetStartTriggerLatch();
        }

        private void OnDisable()
        {
            if (DemoShell != null)
                DemoShell.PreResultClearPresentationRequested -= HandlePreResultClearPresentationRequested;
            ResetActiveSequence();
        }

        private void Update()
        {
            EnsureReferences();
            BuildSpeakerLookup();
            UpdateStartTriggerState();
            Tick(Time.unscaledDeltaTime);
            ProcessInput();
        }

        public bool Advance()
        {
            if (!IsDialogueActive)
                return false;

            ref readonly var line = ref GetActiveLine();
            if (_lineElapsedSec < Mathf.Max(0f, line.MinHoldSec))
                return false;

            if (_activeLineIndex + 1 < _activeVariant.Lines.Length)
            {
                _activeLineIndex += 1;
                _lineElapsedSec = 0f;
                RefreshPresentation();
                return true;
            }

            CompleteActiveSequence(skipped: false);
            return true;
        }

        public bool Skip()
        {
            if (!IsDialogueActive)
                return false;

            CompleteActiveSequence(skipped: true);
            return true;
        }

        public bool TryStartThemeTransition(string themeKey)
        {
            if (string.IsNullOrWhiteSpace(themeKey))
                return false;

            return TryStartSequence(
                InWorldDialogueTriggerId.ThemeTransition,
                InWorldDialogueTargetKind.Theme,
                stageId: 0,
                themeKey.Trim(),
                clearResultContext: default);
        }

        public bool TryStartStagePlayIntervention(InWorldDialogueTriggerId trigger, int stageId)
        {
            if (trigger != InWorldDialogueTriggerId.InterventionCarryFull
                && trigger != InWorldDialogueTriggerId.InterventionFirstHit)
            {
                return false;
            }

            if (stageId <= 0 || IsDialogueActive)
                return false;

            return TryStartSequence(
                trigger,
                InWorldDialogueTargetKind.Stage,
                stageId,
                themeKey: null,
                clearResultContext: default);
        }

        private void Tick(float deltaSec)
        {
            if (!IsDialogueActive)
                return;

            _lineElapsedSec = Mathf.Max(0f, _lineElapsedSec + Mathf.Max(0f, deltaSec));
            RefreshPresentation();

            ref readonly var line = ref GetActiveLine();
            float autoAdvanceSec = Mathf.Max(0f, line.AutoAdvanceSec);
            if (autoAdvanceSec <= 0f || _lineElapsedSec < autoAdvanceSec)
                return;

            Advance();
        }

        private void ProcessInput()
        {
            if (!IsDialogueActive)
                return;

            if (WasSkipPressedThisFrame())
            {
                Skip();
                return;
            }

            if (WasAdvancePressedThisFrame())
                Advance();
        }

        private void UpdateStartTriggerState()
        {
            bool shellReady = DemoShell != null && DemoShell.CurrentScreen == DemoShellScreenId.StagePlay;
            int stageId = shellReady ? Mathf.Max(0, DemoShell.CurrentStageId) : 0;
            bool runningNow = shellReady && DemoShell.CurrentStagePlayPhase == DemoShellStagePlayPhaseId.Running;

            if (!shellReady || stageId <= 0)
            {
                ResetStartTriggerLatch();
                _wasRunningLastFrame = false;
                return;
            }

            if (_startTriggerObservedStageId != stageId)
            {
                _startTriggerObservedStageId = stageId;
                _startTriggerIssuedForCurrentStage = false;
            }

            bool runningEdge = runningNow && !_wasRunningLastFrame;
            _wasRunningLastFrame = runningNow;
            if (!runningEdge || _startTriggerIssuedForCurrentStage)
                return;

            _startTriggerIssuedForCurrentStage = true;
            TryStartSequence(
                InWorldDialogueTriggerId.StageStart,
                InWorldDialogueTargetKind.Stage,
                stageId,
                themeKey: null,
                clearResultContext: default);
        }

        private void HandlePreResultClearPresentationRequested(DemoShellStageResultMetrics result)
        {
            EnsureReferences();
            BuildSpeakerLookup();

            int stageId = DemoShell != null ? Mathf.Max(0, DemoShell.CurrentStageId) : result.StageId;
            bool started = TryStartSequence(
                InWorldDialogueTriggerId.StageClear,
                InWorldDialogueTargetKind.Stage,
                stageId,
                themeKey: null,
                clearResultContext: result);
            if (!started && DemoShell != null)
                DemoShell.NotifyPreResultClearPresentationCompleted();
        }

        private bool TryStartSequence(
            InWorldDialogueTriggerId trigger,
            InWorldDialogueTargetKind targetKind,
            int stageId,
            string themeKey,
            DemoShellStageResultMetrics clearResultContext)
        {
            if (DialogueCatalog == null)
                return false;

            if (!TryResolveEntry(trigger, targetKind, stageId, themeKey, out var entry))
                return false;

            if (!TryResolveVariant(entry, stageId, out var variant))
                return false;

            StartSequence(entry, variant, trigger, clearResultContext);
            return true;
        }

        private bool TryResolveEntry(
            InWorldDialogueTriggerId trigger,
            InWorldDialogueTargetKind requestedTargetKind,
            int stageId,
            string themeKey,
            out InWorldDialogueCatalogEntry entry)
        {
            entry = default;
            var entries = DialogueCatalog.Entries ?? Array.Empty<InWorldDialogueCatalogEntry>();
            bool found = false;
            int bestPriority = int.MinValue;

            for (int i = 0; i < entries.Length; i++)
            {
                var candidate = entries[i];
                if (!candidate.Enabled || candidate.Trigger != trigger)
                    continue;
                if (!MatchesTarget(candidate, requestedTargetKind, stageId, themeKey))
                    continue;
                if (candidate.Priority < bestPriority)
                    continue;

                bestPriority = candidate.Priority;
                entry = candidate;
                found = true;
            }

            return found;
        }

        private static bool MatchesTarget(
            in InWorldDialogueCatalogEntry entry,
            InWorldDialogueTargetKind requestedTargetKind,
            int stageId,
            string themeKey)
        {
            if (entry.TargetKind != requestedTargetKind)
                return false;

            return requestedTargetKind switch
            {
                InWorldDialogueTargetKind.Stage => entry.StageId == stageId,
                InWorldDialogueTargetKind.Theme => string.Equals(entry.ThemeKey?.Trim(), themeKey, StringComparison.Ordinal),
                InWorldDialogueTargetKind.Global => true,
                _ => false,
            };
        }

        private bool TryResolveVariant(in InWorldDialogueCatalogEntry entry, int stageId, out InWorldDialogueSequenceVariant variant)
        {
            variant = default;
            if (!entry.FullVariant.HasLines)
                return false;

            int attemptCount = stageId > 0 ? DemoShellSessionStaging.GetDialogueStageAttemptCount(stageId) : 0;
            bool retry = attemptCount > 1;

            switch (entry.RetryPolicy)
            {
                case InWorldDialogueRetryPolicy.ShortOnRetry:
                    variant = retry && entry.RetryVariant.HasLines ? entry.RetryVariant : entry.FullVariant;
                    break;
                case InWorldDialogueRetryPolicy.SkipOnRetry:
                    if (retry)
                        return false;
                    variant = entry.FullVariant;
                    break;
                case InWorldDialogueRetryPolicy.OncePerSession:
                    if (DemoShellSessionStaging.HasSeenDialogueEntry(entry.EntryKey))
                        return false;
                    variant = entry.FullVariant;
                    break;
                default:
                    variant = entry.FullVariant;
                    break;
            }

            return variant.HasLines;
        }

        private void StartSequence(
            in InWorldDialogueCatalogEntry entry,
            in InWorldDialogueSequenceVariant variant,
            InWorldDialogueTriggerId trigger,
            DemoShellStageResultMetrics clearResultContext)
        {
            ReleaseStagePlayDialoguePauseHandle();
            _activeEntry = entry;
            _activeVariant = variant;
            _activeTrigger = trigger;
            _pendingClearResultContext = clearResultContext;
            _activeLineIndex = 0;
            _lineElapsedSec = 0f;
            _completionNotified = false;
            AcquireStagePlayDialoguePauseIfNeeded(entry, trigger);
            RefreshPresentation();
        }

        private void CompleteActiveSequence(bool skipped)
        {
            if (!IsDialogueActive || _completionNotified)
                return;

            _completionNotified = true;
            if (!string.IsNullOrWhiteSpace(_activeEntry.EntryKey))
                DemoShellSessionStaging.MarkSeenDialogueEntry(_activeEntry.EntryKey);
            if (_activeTrigger == InWorldDialogueTriggerId.InterventionCarryFull)
            {
                int stageId = _activeEntry.StageId > 0
                    ? _activeEntry.StageId
                    : (DemoShell != null ? Mathf.Max(0, DemoShell.CurrentStageId) : 0);
                DemoShellSessionStaging.MarkSeenDialogueTriggerThisRun(stageId, _activeTrigger);
            }

            bool isClearGate = _activeTrigger == InWorldDialogueTriggerId.StageClear
                && _activeEntry.BlockingMode == InWorldDialogueBlockingMode.GateClear;

            ResetActiveSequence();

            if (isClearGate && DemoShell != null)
                DemoShell.NotifyPreResultClearPresentationCompleted(skipped);
        }

        private void ResetActiveSequence()
        {
            ReleaseStagePlayDialoguePauseHandle();
            _activeEntry = default;
            _activeVariant = default;
            _activeTrigger = InWorldDialogueTriggerId.None;
            _pendingClearResultContext = default;
            _activeLineIndex = -1;
            _lineElapsedSec = 0f;
            _completionNotified = false;
            _currentPresentation = DialoguePresentationState.Hidden;
        }

        private void ResetStartTriggerLatch()
        {
            _startTriggerObservedStageId = 0;
            _startTriggerIssuedForCurrentStage = false;
        }

        private void RefreshPresentation()
        {
            if (!IsDialogueActive)
            {
                _currentPresentation = DialoguePresentationState.Hidden;
                return;
            }

            ref readonly var line = ref GetActiveLine();
            ResolveSpeakerProfile(line.SpeakerKey, out var profile);

            float minHoldSec = Mathf.Max(0f, line.MinHoldSec);
            float autoAdvanceSec = Mathf.Max(0f, line.AutoAdvanceSec);
            _currentPresentation = new DialoguePresentationState(
                visible: true,
                trigger: _activeTrigger,
                blockingMode: _activeEntry.BlockingMode,
                entryKey: _activeEntry.EntryKey,
                lineIndex: _activeLineIndex,
                lineCount: _activeVariant.Lines?.Length ?? 0,
                speakerKey: line.SpeakerKey,
                speakerDisplayName: profile.DisplayName,
                speakerPortrait: profile.Portrait,
                portraitSide: profile.PortraitSide,
                bodyText: line.Text,
                anchor: line.Anchor,
                canAdvance: _lineElapsedSec >= minHoldSec,
                canSkip: true,
                autoAdvanceEnabled: autoAdvanceSec > 0f,
                lineElapsedSec: _lineElapsedSec,
                minHoldSec: minHoldSec,
                autoAdvanceSec: autoAdvanceSec);
        }

        private void ResolveSpeakerProfile(string speakerKey, out InWorldDialogueSpeakerProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(speakerKey)
                && _speakerProfilesByKey.TryGetValue(speakerKey.Trim(), out profile))
            {
                return;
            }

            profile = new InWorldDialogueSpeakerProfile
            {
                SpeakerKey = speakerKey ?? string.Empty,
                DisplayName = speakerKey ?? string.Empty,
                PortraitSide = DialoguePortraitSide.Auto,
            };
        }

        private ref readonly InWorldDialogueLine GetActiveLine()
        {
            return ref _activeVariant.Lines[_activeLineIndex];
        }

        private bool WasAdvancePressedThisFrame()
        {
            if (RuntimeUiRoot != null)
            {
                var uiInputModule = RuntimeUiRoot.UiInputModule;
                if (uiInputModule != null
                    && uiInputModule.submit != null
                    && uiInputModule.submit.action != null
                    && uiInputModule.submit.action.WasPerformedThisFrame())
                {
                    return true;
                }
            }

            return EnableMouseAdvanceFallback && Input.GetMouseButtonDown(0);
        }

        private bool WasSkipPressedThisFrame()
        {
            if (RuntimeUiRoot == null)
                return false;

            var uiInputModule = RuntimeUiRoot.UiInputModule;
            return uiInputModule != null
                && uiInputModule.cancel != null
                && uiInputModule.cancel.action != null
                && uiInputModule.cancel.action.WasPerformedThisFrame();
        }

        private void EnsureReferences()
        {
            DemoShell ??= GetComponent<DemoShellFlowController>() ?? FindFirst<DemoShellFlowController>();
            RuntimeUiRoot ??= FindFirst<RuntimeUiRoot>();
            PauseController ??= GetComponent<DemoShellGameplayPauseController>() ?? FindFirst<DemoShellGameplayPauseController>();

            if ((DemoShell == null || DialogueCatalog == null || SpeakerCatalog == null || PauseController == null) && !_warnedBindFailure && LogBindWarnings)
            {
                _warnedBindFailure = true;
                Debug.LogWarning("[DemoShellDialogueBridge] Required references were not found. Assign DialogueCatalog, SpeakerCatalog, and DemoShellGameplayPauseController explicitly.");
            }
            else if (DemoShell != null && DialogueCatalog != null && SpeakerCatalog != null && PauseController != null)
            {
                _warnedBindFailure = false;
            }
        }

        private void AcquireStagePlayDialoguePauseIfNeeded(
            in InWorldDialogueCatalogEntry entry,
            InWorldDialogueTriggerId trigger)
        {
            if (!ShouldAcquireStagePlayDialoguePause(entry, trigger)
                || PauseController == null
                || _gatePauseHandle.IsValid)
                return;

            _gatePauseHandle = PauseController.Acquire(
                GameplayPauseReasonId.DialogueGate,
                GameplayPauseFlags.PauseSimulation
                | GameplayPauseFlags.BlockGameplayInput
                | GameplayPauseFlags.ExclusivePresentationInput
                | GameplayPauseFlags.BlockPauseMenuOpen);
        }

        private static bool ShouldAcquireStagePlayDialoguePause(
            in InWorldDialogueCatalogEntry entry,
            InWorldDialogueTriggerId trigger)
        {
            if (entry.TargetKind != InWorldDialogueTargetKind.Stage)
                return false;

            return trigger == InWorldDialogueTriggerId.StageStart
                   || trigger == InWorldDialogueTriggerId.StageClear
                   || trigger == InWorldDialogueTriggerId.InterventionCarryFull
                   || trigger == InWorldDialogueTriggerId.InterventionFirstHit;
        }

        private void ReleaseStagePlayDialoguePauseHandle()
        {
            if (PauseController != null && _gatePauseHandle.IsValid)
                PauseController.Release(_gatePauseHandle);

            _gatePauseHandle = GameplayPauseHandle.Invalid;
        }

        private void BuildSpeakerLookup()
        {
            _speakerProfilesByKey.Clear();
            var profiles = SpeakerCatalog != null ? SpeakerCatalog.Profiles : null;
            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = profiles[i];
                if (string.IsNullOrWhiteSpace(profile.SpeakerKey))
                    continue;

                _speakerProfilesByKey[profile.SpeakerKey.Trim()] = profile;
            }
        }

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}
