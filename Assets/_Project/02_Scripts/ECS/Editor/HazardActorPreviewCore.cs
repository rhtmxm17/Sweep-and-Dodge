using System;
using System.Collections.Generic;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum HazardActorPreviewScope : byte
    {
        Pattern = 0,
        Actor = 1,
        Encounter = 2,
    }

    public enum HazardActorWorkbenchSelectionKind : byte
    {
        None = 0,
        Actor = 1,
        Phase = 2,
        Transition = 3,
        PatternSlot = 4,
        EmissionProfile = 5,
        TelegraphProfile = 6,
    }

    public readonly struct HazardActorWorkbenchSelection : IEquatable<HazardActorWorkbenchSelection>
    {
        private HazardActorWorkbenchSelection(
            HazardActorWorkbenchSelectionKind kind,
            GameObject actorPrefab,
            int phaseId,
            int transitionFromPhaseId,
            int patternSlotId,
            UnityEngine.Object profileAsset)
        {
            Kind = kind;
            ActorPrefab = actorPrefab;
            PhaseId = phaseId;
            TransitionFromPhaseId = transitionFromPhaseId;
            PatternSlotId = patternSlotId;
            ProfileAsset = profileAsset;
        }

        public HazardActorWorkbenchSelectionKind Kind { get; }
        public GameObject ActorPrefab { get; }
        public int PhaseId { get; }
        public int TransitionFromPhaseId { get; }
        public int PatternSlotId { get; }
        public UnityEngine.Object ProfileAsset { get; }

        public static HazardActorWorkbenchSelection None => default;
        public static HazardActorWorkbenchSelection ForActor(GameObject prefab) =>
            new HazardActorWorkbenchSelection(HazardActorWorkbenchSelectionKind.Actor, prefab, 0, 0, 0, null);
        public static HazardActorWorkbenchSelection ForPhase(GameObject prefab, int phaseId) =>
            new HazardActorWorkbenchSelection(HazardActorWorkbenchSelectionKind.Phase, prefab, phaseId, 0, 0, null);
        public static HazardActorWorkbenchSelection ForTransition(GameObject prefab, int fromPhaseId) =>
            new HazardActorWorkbenchSelection(HazardActorWorkbenchSelectionKind.Transition, prefab, 0, fromPhaseId, 0, null);
        public static HazardActorWorkbenchSelection ForPattern(GameObject prefab, int patternSlotId) =>
            new HazardActorWorkbenchSelection(HazardActorWorkbenchSelectionKind.PatternSlot, prefab, 0, 0, patternSlotId, null);
        public static HazardActorWorkbenchSelection ForEmissionProfile(GameObject prefab, int patternSlotId, EmissionProfileSO profile) =>
            new HazardActorWorkbenchSelection(HazardActorWorkbenchSelectionKind.EmissionProfile, prefab, 0, 0, patternSlotId, profile);
        public static HazardActorWorkbenchSelection ForTelegraphProfile(GameObject prefab, int patternSlotId, HazardEmitterTelegraphProfileSO profile) =>
            new HazardActorWorkbenchSelection(HazardActorWorkbenchSelectionKind.TelegraphProfile, prefab, 0, 0, patternSlotId, profile);

        public bool Equals(HazardActorWorkbenchSelection other)
        {
            return Kind == other.Kind
                && ActorPrefab == other.ActorPrefab
                && PhaseId == other.PhaseId
                && TransitionFromPhaseId == other.TransitionFromPhaseId
                && PatternSlotId == other.PatternSlotId
                && ProfileAsset == other.ProfileAsset;
        }

        public override bool Equals(object obj) => obj is HazardActorWorkbenchSelection other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (ActorPrefab != null ? ActorPrefab.GetInstanceID() : 0);
                hash = (hash * 397) ^ PhaseId;
                hash = (hash * 397) ^ TransitionFromPhaseId;
                hash = (hash * 397) ^ PatternSlotId;
                hash = (hash * 397) ^ (ProfileAsset != null ? ProfileAsset.GetInstanceID() : 0);
                return hash;
            }
        }
    }

    public readonly struct HazardActorWorkbenchIssue
    {
        public HazardActorWorkbenchIssue(
            ContentValidationSeverity severity,
            string code,
            string message,
            HazardActorWorkbenchSelection target)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Target = target;
        }

        public ContentValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public HazardActorWorkbenchSelection Target { get; }
    }

    public sealed class HazardActorPreviewSnapshot
    {
        public GameObject ActorPrefab;
        public HazardActorAuthoring Actor;
        public int ActorId;
        public bool Enabled;
        public bool StartSuppressed;
        public HazardActorPresenceStateId InitialPresenceState;
        public float ActivationDurationSec;
        public float RetireDurationSec;
        public int InitialPhaseId;
        public HazardActorPhaseSelectorPolicyBuffer[] Policies = Array.Empty<HazardActorPhaseSelectorPolicyBuffer>();
        public HazardActorPhaseSelectorCandidateBuffer[] Candidates = Array.Empty<HazardActorPhaseSelectorCandidateBuffer>();
        public HazardActorPhaseProgressTransitionBuffer[] Transitions = Array.Empty<HazardActorPhaseProgressTransitionBuffer>();
        public HazardActorPreviewPatternSlot[] PatternSlots = Array.Empty<HazardActorPreviewPatternSlot>();
        public HazardActorWorkbenchIssue[] Issues = Array.Empty<HazardActorWorkbenchIssue>();

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Length; i++)
                {
                    if (Issues[i].Severity == ContentValidationSeverity.Error)
                        return true;
                }
                return false;
            }
        }
    }

    public sealed class HazardActorPreviewPatternSlot
    {
        public int PatternSlotId;
        public HazardEmitterTelegraphProfileSO TelegraphProfile;
        public EmissionProfileSO EmissionProfile;
        public HazardActorPatternExecutionSlotBuffer Execution;
        public ResolvedEmissionCore EmissionCore;
    }

    public struct HazardActorPreviewInput
    {
        public HazardActorPreviewScope Scope;
        public int ForcedPhaseId;
        public int ForcedPatternSlotId;
        public int GhostCapOverride;
        public float SourceProgress01;
        public Vector3 ActorWorldPosition;
        public float ActorYawDeg;
        public Vector3 TargetWorldPosition;
        public bool SpawnAtStart;
    }

    public struct HazardActorPreviewGhost
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float AgeSec;
        public float LifetimeSec;
        public int PatternSlotId;
        public int Depth;
        public BulletMovementFamilyId MovementFamily;
        public BulletDampedLinearDefinition DampedLinear;
        public BulletHomingLiteDefinition HomingLite;
    }

    public readonly struct HazardActorEncounterRulePreview
    {
        public HazardActorEncounterRulePreview(
            int ruleId,
            int placementInstanceId,
            HazardActorOrchestrationActionId actionType,
            HazardActorOrchestrationTriggerId triggerType,
            float triggerThresholdNormalized,
            int targetPhaseId)
        {
            RuleId = ruleId;
            PlacementInstanceId = placementInstanceId;
            ActionType = actionType;
            TriggerType = triggerType;
            TriggerThresholdNormalized = Mathf.Clamp01(triggerThresholdNormalized);
            TargetPhaseId = targetPhaseId;
        }

        public int RuleId { get; }
        public int PlacementInstanceId { get; }
        public HazardActorOrchestrationActionId ActionType { get; }
        public HazardActorOrchestrationTriggerId TriggerType { get; }
        public float TriggerThresholdNormalized { get; }
        public int TargetPhaseId { get; }
    }

    public sealed class HazardActorPreviewFrame
    {
        public HazardActorPresenceStateId Presence;
        public int PhaseId;
        public int PatternSlotId;
        public HazardActorEmitLifecycleStateId Lifecycle;
        public float LifecycleElapsedSec;
        public int ActiveGhostCount;
        public int SuppressedGhostCount;
        public string Warning;
    }

    public static class HazardActorPreviewSnapshotBuilder
    {
        public static bool TryBuild(GameObject actorPrefab, out HazardActorPreviewSnapshot snapshot)
        {
            var issues = new List<HazardActorWorkbenchIssue>(8);
            snapshot = new HazardActorPreviewSnapshot { ActorPrefab = actorPrefab };
            if (actorPrefab == null)
            {
                issues.Add(Issue(ContentValidationSeverity.Error, "HAW001", "Actor prefab is not assigned.", HazardActorWorkbenchSelection.None));
                snapshot.Issues = issues.ToArray();
                return false;
            }

            var actors = actorPrefab.GetComponentsInChildren<HazardActorAuthoring>(true);
            if (actors == null || actors.Length != 1)
            {
                issues.Add(Issue(
                    ContentValidationSeverity.Error,
                    "HAW002",
                    $"Actor prefab must contain exactly one HazardActorAuthoring. found={actors?.Length ?? 0}",
                    HazardActorWorkbenchSelection.ForActor(actorPrefab)));
                snapshot.Issues = issues.ToArray();
                return false;
            }

            var actor = actors[0];
            snapshot.Actor = actor;
            snapshot.ActorId = Math.Max(1, actor.ActorId);
            snapshot.Enabled = actor.Enabled;
            snapshot.StartSuppressed = actor.StartSuppressed;
            snapshot.InitialPresenceState = actor.InitialPresenceState;
            snapshot.ActivationDurationSec = Mathf.Max(0f, actor.ActivationDurationSec);
            snapshot.RetireDurationSec = Mathf.Max(0f, actor.RetireDurationSec);

            if (!HazardActorAuthoringValidationUtility.TryValidateStandalone(
                    actor,
                    out var seed,
                    out var transitions,
                    out _,
                    out string validationError))
            {
                issues.Add(Issue(
                    ContentValidationSeverity.Error,
                    "HAW010",
                    validationError,
                    HazardActorWorkbenchSelection.ForActor(actorPrefab)));
            }

            snapshot.InitialPhaseId = seed.InitialPhaseId > 0 ? seed.InitialPhaseId : Math.Max(1, actor.InitialPhaseId);
            snapshot.Policies = seed.Policies ?? Array.Empty<HazardActorPhaseSelectorPolicyBuffer>();
            snapshot.Candidates = seed.Candidates ?? Array.Empty<HazardActorPhaseSelectorCandidateBuffer>();
            snapshot.Transitions = transitions ?? Array.Empty<HazardActorPhaseProgressTransitionBuffer>();

            if (HazardActorPatternSlotAuthoringUtility.TryResolveSlots(actor.PatternSlots, out var resolved, out string slotError))
                snapshot.PatternSlots = BuildPatternSlots(actor, resolved, actorPrefab, issues);
            else
                issues.Add(Issue(ContentValidationSeverity.Error, "HAW011", slotError, HazardActorWorkbenchSelection.ForActor(actorPrefab)));

            snapshot.Issues = issues.ToArray();
            return !snapshot.HasErrors;
        }

        public static HazardActorWorkbenchIssue[] Validate(GameObject actorPrefab)
        {
            TryBuild(actorPrefab, out var snapshot);
            return snapshot.Issues;
        }

        private static HazardActorPreviewPatternSlot[] BuildPatternSlots(
            HazardActorAuthoring actor,
            ResolvedHazardActorPatternSlotAuthoring[] resolved,
            GameObject prefab,
            List<HazardActorWorkbenchIssue> issues)
        {
            var result = new HazardActorPreviewPatternSlot[resolved.Length];
            for (int i = 0; i < resolved.Length; i++)
            {
                int slotId = resolved[i].Metadata.PatternSlotId;
                HazardActorPatternSlotAuthoring source = FindAuthoringSlot(actor.PatternSlots, slotId);
                var previewSlot = new HazardActorPreviewPatternSlot
                {
                    PatternSlotId = slotId,
                    TelegraphProfile = source.TelegraphProfile,
                    EmissionProfile = source.Emission.Profile,
                    Execution = resolved[i].Execution,
                };
                if (source.Emission.Profile != null
                    && !EmissionProfileResolver.TryResolve(source.Emission.Profile, out previewSlot.EmissionCore, out string emissionError))
                {
                    issues.Add(Issue(
                        ContentValidationSeverity.Error,
                        "HAW012",
                        emissionError,
                        HazardActorWorkbenchSelection.ForEmissionProfile(prefab, slotId, source.Emission.Profile)));
                }
                result[i] = previewSlot;
            }
            return result;
        }

        private static HazardActorPatternSlotAuthoring FindAuthoringSlot(HazardActorPatternSlotAuthoring[] slots, int slotId)
        {
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].PatternSlotId == slotId)
                        return slots[i];
                }
            }
            return default;
        }

        private static HazardActorWorkbenchIssue Issue(
            ContentValidationSeverity severity,
            string code,
            string message,
            HazardActorWorkbenchSelection target)
        {
            return new HazardActorWorkbenchIssue(severity, code, message, target);
        }
    }

    public sealed class HazardActorPreviewSession : IDisposable
    {
        public const float FixedDeltaTime = 1f / 30f;
        public const int ActorGhostCap = 1024;
        public const int EncounterGhostCap = 4096;
        public const int TrajectorySamplesPerBranch = 16;
        public const int MotionCompletedDepthCap = 3;

        private readonly List<HazardActorPreviewGhost> _ghosts = new List<HazardActorPreviewGhost>(ActorGhostCap);
        private HazardActorPreviewSnapshot _snapshot;
        private HazardActorPreviewInput _input;
        private HazardActorPresenceStateId _presence;
        private float _presenceElapsed;
        private int _phaseId;
        private int _previousPhaseId;
        private uint _phaseVersion;
        private HazardActorPhaseTransitionStateId _transitionState;
        private int _pendingPhaseId;
        private float _transitionElapsed;
        private float _transitionDuration;
        private int _currentPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
        private int _currentCandidateOrder = -1;
        private uint _lastResolvedPhaseVersion;
        private uint _completedCycleVersion;
        private uint _lastConsumedCycleVersion;
        private HazardActorEmitLifecycleStateId _lifecycle;
        private float _lifecycleElapsed;
        private int _appliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
        private int _remainingRepeats;
        private float _repeatElapsed;
        private int _suppressedGhostCount;
        private string _warning = string.Empty;

        public HazardActorPreviewSession()
        {
            Frame = new HazardActorPreviewFrame();
        }

        public HazardActorPreviewFrame Frame { get; }
        public bool Playing { get; private set; }
        public float TimeSec { get; private set; }
        public int ActiveCallbackRefCount { get; private set; }
        public IReadOnlyList<HazardActorPreviewGhost> Ghosts => _ghosts;
        public HazardActorPreviewSnapshot Snapshot => _snapshot;
        public HazardActorPreviewInput Input => _input;
        public int GhostCap => _input.GhostCapOverride > 0
            ? _input.GhostCapOverride
            : _input.Scope == HazardActorPreviewScope.Encounter ? EncounterGhostCap : ActorGhostCap;
        public bool IsDisposed { get; private set; }

        public void Load(HazardActorPreviewSnapshot snapshot, HazardActorPreviewInput input)
        {
            _snapshot = snapshot;
            _input = input;
            Restart();
        }

        public void Play() => Playing = true;
        public void Pause() => Playing = false;

        public void Restart()
        {
            Playing = false;
            TimeSec = 0f;
            _ghosts.Clear();
            _suppressedGhostCount = 0;
            _warning = string.Empty;
            _presence = ResolveInitialPresence();
            _presenceElapsed = 0f;
            _phaseId = _input.ForcedPhaseId > 0 ? _input.ForcedPhaseId : Math.Max(1, _snapshot?.InitialPhaseId ?? 1);
            _previousPhaseId = _phaseId;
            _phaseVersion = 0u;
            _transitionState = HazardActorPhaseTransitionStateId.Idle;
            _pendingPhaseId = -1;
            _transitionElapsed = 0f;
            _transitionDuration = 0f;
            _currentPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
            _currentCandidateOrder = -1;
            _lastResolvedPhaseVersion = 0u;
            _completedCycleVersion = 0u;
            _lastConsumedCycleVersion = 0u;
            _lifecycle = HazardActorEmitLifecycleStateId.Dormant;
            _lifecycleElapsed = 0f;
            _appliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
            _remainingRepeats = 0;
            _repeatElapsed = 0f;
            PublishFrame();
        }

        public void EvaluateAt(float timeSec)
        {
            float target = Mathf.Max(0f, timeSec);
            int targetStep = Mathf.FloorToInt(target / FixedDeltaTime);
            int currentStep = Mathf.RoundToInt(TimeSec / FixedDeltaTime);
            if (targetStep < currentStep)
            {
                Restart();
                currentStep = 0;
            }

            while (currentStep < targetStep)
            {
                Step();
                currentStep++;
            }
        }

        public void ScrubTo(float timeSec)
        {
            EvaluateAt(timeSec);
        }

        public void Step()
        {
            if (_snapshot == null || _snapshot.HasErrors)
            {
                _warning = "Preview snapshot has validation errors.";
                PublishFrame();
                return;
            }

            TimeSec += FixedDeltaTime;
            StepPresence();
            StepPhase();
            StepSelector();
            StepEmit();
            StepGhosts();
            PublishFrame();
        }

        public bool CleanupOldestGhost()
        {
            if (_ghosts.Count == 0)
                return false;

            var removed = _ghosts[0];
            _ghosts.RemoveAt(0);
            if (TryFindPatternSlot(removed.PatternSlotId, out var slot)
                && slot.EmissionCore.HasCleanupRemovedTrigger
                && slot.EmissionCore.CleanupRemovedTargetProfile != null
                && removed.Depth < MotionCompletedDepthCap)
            {
                EmitProfileAt(slot.EmissionCore.CleanupRemovedTargetProfile, removed.Position, removed.Velocity, removed.Depth + 1);
            }
            PublishFrame();
            return true;
        }

        public long MeasureSteadyStepManagedAllocation()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Step();
            long after = GC.GetAllocatedBytesForCurrentThread();
            return Math.Max(0L, after - before);
        }

        public void RetainCallbackOwner()
        {
            ActiveCallbackRefCount++;
        }

        public void ReleaseCallbackOwner()
        {
            ActiveCallbackRefCount = Math.Max(0, ActiveCallbackRefCount - 1);
        }

        public void Dispose()
        {
            IsDisposed = true;
            Playing = false;
            _ghosts.Clear();
            ReleaseAllCallbackOwners();
        }

        public void ReleaseAllCallbackOwners()
        {
            ActiveCallbackRefCount = 0;
        }

        private HazardActorPresenceStateId ResolveInitialPresence()
        {
            if (_snapshot == null || !_snapshot.Enabled || _snapshot.StartSuppressed)
                return HazardActorPresenceStateId.Hidden;
            if (_input.Scope == HazardActorPreviewScope.Pattern)
                return HazardActorPresenceStateId.Active;
            if (_input.SpawnAtStart)
                return _snapshot.ActivationDurationSec > 0f ? HazardActorPresenceStateId.Activating : HazardActorPresenceStateId.Active;
            return _snapshot.InitialPresenceState;
        }

        private void StepPresence()
        {
            if (_input.Scope == HazardActorPreviewScope.Pattern)
            {
                _presence = HazardActorPresenceStateId.Active;
                return;
            }

            if (_snapshot == null || !_snapshot.Enabled || _snapshot.StartSuppressed)
            {
                _presence = HazardActorPresenceStateId.Hidden;
                ResetSelectionAndEmit();
                return;
            }

            switch (_presence)
            {
                case HazardActorPresenceStateId.Activating:
                    _presenceElapsed += FixedDeltaTime;
                    ResetSelectionAndEmit();
                    if (_presenceElapsed >= Mathf.Max(0f, _snapshot.ActivationDurationSec))
                    {
                        _presence = HazardActorPresenceStateId.Active;
                        _presenceElapsed = 0f;
                    }
                    break;
                case HazardActorPresenceStateId.Retiring:
                    _presenceElapsed += FixedDeltaTime;
                    ResetSelectionAndEmit();
                    if (_presenceElapsed >= Mathf.Max(0f, _snapshot.RetireDurationSec))
                    {
                        _presence = HazardActorPresenceStateId.Hidden;
                        _presenceElapsed = 0f;
                    }
                    break;
            }
        }

        private void StepPhase()
        {
            if (_presence != HazardActorPresenceStateId.Active || _input.Scope == HazardActorPreviewScope.Pattern)
                return;

            if (_input.ForcedPhaseId > 0 && _phaseId != _input.ForcedPhaseId)
            {
                CommitPhase(_input.ForcedPhaseId);
                return;
            }

            if (_transitionState == HazardActorPhaseTransitionStateId.Preparing)
            {
                _transitionElapsed += FixedDeltaTime;
                if (_transitionElapsed >= _transitionDuration)
                    CommitPhase(_pendingPhaseId);
                return;
            }

            if (!TryFindProgressTransition(_phaseId, out var transition))
                return;
            if (_input.SourceProgress01 < transition.ProgressThresholdNormalized)
                return;

            _transitionState = HazardActorPhaseTransitionStateId.Preparing;
            _pendingPhaseId = transition.ToPhaseId;
            _transitionElapsed = 0f;
            _transitionDuration = Mathf.Max(0f, transition.TransitionLeadInSec);
            if (_transitionDuration <= 0f)
                CommitPhase(_pendingPhaseId);
        }

        private void StepSelector()
        {
            if (_presence != HazardActorPresenceStateId.Active
                || _transitionState == HazardActorPhaseTransitionStateId.Preparing)
            {
                return;
            }

            if (_input.Scope == HazardActorPreviewScope.Pattern)
            {
                ApplyPatternSelection(_input.ForcedPatternSlotId > 0 ? _input.ForcedPatternSlotId : FirstPatternSlotId(), 0);
                return;
            }

            if (_input.ForcedPatternSlotId > 0)
            {
                ApplyPatternSelection(_input.ForcedPatternSlotId, 0);
                return;
            }

            if (!TryFindPolicy(_phaseId, out var policy))
            {
                _currentPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
                _currentCandidateOrder = -1;
                _lastResolvedPhaseVersion = _phaseVersion;
                return;
            }

            bool phaseChanged = _lastResolvedPhaseVersion != _phaseVersion;
            bool currentEligible = CurrentSelectionEligible();
            if (policy.SelectionMode == HazardActorSelectionModeId.OrderedPriority)
            {
                if (TryFindFirstCandidate(_phaseId, -1, out var candidate))
                    ApplyPatternSelection(candidate.PatternSlotId, candidate.OrderIndex);
            }
            else if (phaseChanged || !currentEligible || _completedCycleVersion > _lastConsumedCycleVersion)
            {
                int minOrder = phaseChanged || !currentEligible ? -1 : _currentCandidateOrder;
                if (TryFindFirstCandidate(_phaseId, minOrder, out var candidate))
                {
                    ApplyPatternSelection(candidate.PatternSlotId, candidate.OrderIndex);
                    _lastConsumedCycleVersion = _completedCycleVersion;
                }
            }
            _lastResolvedPhaseVersion = _phaseVersion;
        }

        private void StepEmit()
        {
            if (_presence != HazardActorPresenceStateId.Active
                || _transitionState == HazardActorPhaseTransitionStateId.Preparing
                || _currentPatternSlotId < 0
                || !TryFindPatternSlot(_currentPatternSlotId, out var slot))
            {
                ResetEmit();
                return;
            }

            if (_appliedPatternSlotId != _currentPatternSlotId)
            {
                _appliedPatternSlotId = _currentPatternSlotId;
                _lifecycle = HazardActorEmitLifecycleStateId.Dormant;
                _lifecycleElapsed = 0f;
                return;
            }

            if (_lifecycle != HazardActorEmitLifecycleStateId.Dormant)
                _lifecycleElapsed += FixedDeltaTime;
            if (_lifecycle == HazardActorEmitLifecycleStateId.Emit)
                _repeatElapsed += FixedDeltaTime;

            bool emittedThisStep = false;
            for (int guard = 0; guard < 4; guard++)
            {
                switch (_lifecycle)
                {
                    case HazardActorEmitLifecycleStateId.Dormant:
                        if (emittedThisStep)
                            return;
                        _lifecycle = HazardActorEmitLifecycleStateId.Telegraph;
                        _lifecycleElapsed = 0f;
                        break;
                    case HazardActorEmitLifecycleStateId.Telegraph:
                        if (_lifecycleElapsed < Mathf.Max(0f, slot.Execution.TelegraphDurationSec))
                            return;
                        BeginEmit(slot);
                        emittedThisStep = true;
                        if (_lifecycle == HazardActorEmitLifecycleStateId.Cooldown)
                            return;
                        break;
                    case HazardActorEmitLifecycleStateId.Emit:
                        if (!TryEmitTimedRepeat(slot, ref emittedThisStep))
                            return;
                        break;
                    case HazardActorEmitLifecycleStateId.Cooldown:
                        if (_lifecycleElapsed < Mathf.Max(0f, slot.Execution.CooldownSec))
                            return;
                        CompleteCycle();
                        if (emittedThisStep)
                            return;
                        break;
                    default:
                        ResetEmit();
                        return;
                }
            }
        }

        private void BeginEmit(HazardActorPreviewPatternSlot slot)
        {
            _remainingRepeats = Mathf.Max(1, slot.Execution.EventRepeatCount);
            _repeatElapsed = 0f;
            if (slot.Execution.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed)
            {
                EmitSlot(slot, 0);
                _remainingRepeats--;
                if (_remainingRepeats > 0)
                {
                    _lifecycle = HazardActorEmitLifecycleStateId.Emit;
                    _lifecycleElapsed = 0f;
                    return;
                }
            }
            else
            {
                while (_remainingRepeats > 0)
                {
                    EmitSlot(slot, 0);
                    _remainingRepeats--;
                }
            }

            BeginCooldown(slot);
        }

        private bool TryEmitTimedRepeat(HazardActorPreviewPatternSlot slot, ref bool emittedThisStep)
        {
            float interval = Mathf.Max(0.001f, slot.Execution.EventShotIntervalSec);
            while (_remainingRepeats > 0 && _repeatElapsed >= interval)
            {
                _repeatElapsed = Mathf.Max(0f, _repeatElapsed - interval);
                EmitSlot(slot, 0);
                _remainingRepeats--;
                emittedThisStep = true;
                if (_remainingRepeats > 0)
                    return true;
            }

            if (_remainingRepeats > 0)
                return false;

            BeginCooldown(slot);
            return true;
        }

        private void BeginCooldown(HazardActorPreviewPatternSlot slot)
        {
            _lifecycle = HazardActorEmitLifecycleStateId.Cooldown;
            _lifecycleElapsed = 0f;
            _remainingRepeats = 0;
            _repeatElapsed = 0f;
            if (slot.Execution.CooldownSec <= 0f)
                CompleteCycle();
        }

        private void StepGhosts()
        {
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = _ghosts[i];
                ghost.AgeSec += FixedDeltaTime;
                if (ghost.AgeSec >= ghost.LifetimeSec)
                {
                    if (TryFindPatternSlot(ghost.PatternSlotId, out var ownerSlot)
                        && ownerSlot.EmissionCore.HasMotionCompletedTrigger
                        && ownerSlot.EmissionCore.MotionCompletedTargetProfile != null
                        && ghost.Depth < MotionCompletedDepthCap)
                    {
                        EmitProfileAt(ownerSlot.EmissionCore.MotionCompletedTargetProfile, ghost.Position, ghost.Velocity, ghost.Depth + 1);
                    }
                    _ghosts.RemoveAt(i);
                    continue;
                }

                switch (ghost.MovementFamily)
                {
                    case BulletMovementFamilyId.DampedLinear:
                        ghost.Velocity *= Mathf.Exp(-Mathf.Max(0f, ghost.DampedLinear.DampingPerSec) * FixedDeltaTime);
                        if (ghost.Velocity.magnitude < Mathf.Max(0f, ghost.DampedLinear.StopSpeedThreshold))
                            ghost.Velocity = Vector3.zero;
                        break;
                    case BulletMovementFamilyId.HomingLite:
                        Vector3 desired = _input.TargetWorldPosition - ghost.Position;
                        desired.y = 0f;
                        if (desired.sqrMagnitude > 0.0001f)
                        {
                            float speed = ghost.Velocity.magnitude;
                            var nextDir = Vector3.RotateTowards(
                                ghost.Velocity.normalized,
                                desired.normalized,
                                Mathf.Deg2Rad * Mathf.Max(0f, ghost.HomingLite.TurnRateDegPerSec) * FixedDeltaTime,
                                0f);
                            ghost.Velocity = nextDir * speed;
                        }
                        break;
                }

                ghost.Position += ghost.Velocity * FixedDeltaTime;
                _ghosts[i] = ghost;
            }
        }

        private void EmitSlot(HazardActorPreviewPatternSlot slot, int depth)
        {
            Vector3 actorOrigin = _input.ActorWorldPosition;
            Quaternion yaw = Quaternion.Euler(0f, _input.ActorYawDeg, 0f);
            Vector3 slotOrigin = actorOrigin + yaw * ToVector3(slot.Execution.LocalOffset);
            EmitExecution(slot, slot.Execution, slot.EmissionCore, slotOrigin, yaw, depth);
        }

        private void EmitProfileAt(EmissionProfileSO profile, Vector3 origin, Vector3 inheritedVelocity, int depth)
        {
            if (!EmissionProfileResolver.TryResolve(profile, out var core, out _))
                return;

            var execution = new HazardActorPatternExecutionSlotBuffer
            {
                PatternSlotId = _currentPatternSlotId,
                BulletTypeKey = core.BulletTypeKey,
                HasSpeedOverride = core.HasSpeedOverride ? (byte)1 : (byte)0,
                SpeedOverride = core.SpeedOverride,
                HasLifetimeOverride = core.HasLifetimeOverride ? (byte)1 : (byte)0,
                LifetimeOverride = core.LifetimeOverride,
                HasMovementOverride = core.HasMovementOverride ? (byte)1 : (byte)0,
                MovementFamily = core.MovementFamily,
                DampedLinear = core.DampedLinear,
                HomingLite = core.HomingLite,
                PositionPatternMode = core.PositionPatternMode,
                SpawnOffset = core.SpawnOffset,
                LineStart = core.LineStart,
                LineEnd = core.LineEnd,
                SampleSpacing = core.SampleSpacing,
                PointSetCount = core.PointSetCount,
                Point0 = core.Point0,
                Point1 = core.Point1,
                Point2 = core.Point2,
                Point3 = core.Point3,
                AimMode = inheritedVelocity.sqrMagnitude > 0.0001f ? WaveAimModeId.Fixed : core.AimMode,
                BaseAngleDeg = inheritedVelocity.sqrMagnitude > 0.0001f ? VectorToYawDeg(inheritedVelocity) : core.BaseAngleDeg,
                AimAngleOffsetDeg = core.AimAngleOffsetDeg,
                LineNormalSide = core.LineNormalSide,
                LineNormalAngleOffsetDeg = core.LineNormalAngleOffsetDeg,
                SpiralStepDeg = core.SpiralStepDeg,
                ShotPatternMode = core.ShotPatternMode,
                ShotCount = core.ShotCount,
                NWayAngleSpacingDeg = core.NWayAngleSpacingDeg,
            };
            EmitExecution(null, execution, core, origin, Quaternion.identity, depth);
        }

        private void EmitExecution(
            HazardActorPreviewPatternSlot ownerSlot,
            HazardActorPatternExecutionSlotBuffer execution,
            ResolvedEmissionCore core,
            Vector3 origin,
            Quaternion yaw,
            int depth)
        {
            int positionCount = ResolvePositionCount(execution);
            int shotCount = ResolveShotCount(execution);
            for (int positionIndex = 0; positionIndex < positionCount; positionIndex++)
            {
                Vector3 spawn = origin + yaw * ToWorldOffset(ResolvePosition(execution, positionIndex));
                float baseAngle = ResolveAimDeg(execution, spawn, positionIndex);
                for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
                {
                    float angle = baseAngle + ResolveShotOffsetDeg(execution, shotIndex, shotCount);
                    AddGhost(ownerSlot != null ? ownerSlot.PatternSlotId : _currentPatternSlotId, spawn, angle, execution, core, depth);
                }
            }
        }

        private void AddGhost(
            int patternSlotId,
            Vector3 position,
            float angleDeg,
            HazardActorPatternExecutionSlotBuffer execution,
            ResolvedEmissionCore core,
            int depth)
        {
            if (_ghosts.Count >= GhostCap)
            {
                _suppressedGhostCount++;
                _warning = $"Ghost cap reached. cap={GhostCap}, suppressed={_suppressedGhostCount}";
                return;
            }

            float speed = execution.HasSpeedOverride != 0
                ? Mathf.Max(0f, execution.SpeedOverride)
                : core.Bullet != null ? Mathf.Max(0f, core.Bullet.Speed) : 0.5f;
            float lifetime = execution.HasLifetimeOverride != 0
                ? Mathf.Max(FixedDeltaTime, execution.LifetimeOverride)
                : core.Bullet != null ? Mathf.Max(FixedDeltaTime, core.Bullet.Lifetime) : 4f;
            BulletMovementFamilyId family = execution.HasMovementOverride != 0
                ? execution.MovementFamily
                : core.Bullet != null ? core.Bullet.MovementFamily : BulletMovementFamilyId.Linear;

            Vector3 forward = Quaternion.Euler(0f, angleDeg, 0f) * Vector3.forward;
            _ghosts.Add(new HazardActorPreviewGhost
            {
                Position = position,
                Velocity = forward * speed,
                AgeSec = 0f,
                LifetimeSec = lifetime,
                PatternSlotId = patternSlotId,
                Depth = depth,
                MovementFamily = family,
                DampedLinear = execution.HasMovementOverride != 0 ? execution.DampedLinear : core.Bullet != null ? core.Bullet.DampedLinear : default,
                HomingLite = execution.HasMovementOverride != 0 ? execution.HomingLite : core.Bullet != null ? core.Bullet.HomingLite : default,
            });
        }

        private int ResolvePositionCount(HazardActorPatternExecutionSlotBuffer execution)
        {
            switch (execution.PositionPatternMode)
            {
                case WavePositionPatternModeId.LineEven:
                    float distance = math.distance(execution.LineStart, execution.LineEnd);
                    return Mathf.Max(1, Mathf.FloorToInt(distance / Mathf.Max(0.001f, execution.SampleSpacing)) + 1);
                case WavePositionPatternModeId.PointSet:
                    return Mathf.Clamp(execution.PointSetCount, 1, PointSetPositionPatternAuthoring.MaxPointCount);
                default:
                    return 1;
            }
        }

        private Vector2 ResolvePosition(HazardActorPatternExecutionSlotBuffer execution, int index)
        {
            switch (execution.PositionPatternMode)
            {
                case WavePositionPatternModeId.LineEven:
                    int count = ResolvePositionCount(execution);
                    float t = count <= 1 ? 0f : (float)index / (count - 1);
                    return Vector2.Lerp(ToVector2(execution.LineStart), ToVector2(execution.LineEnd), t);
                case WavePositionPatternModeId.PointSet:
                    switch (index)
                    {
                        case 0: return ToVector2(execution.Point0);
                        case 1: return ToVector2(execution.Point1);
                        case 2: return ToVector2(execution.Point2);
                        case 3: return ToVector2(execution.Point3);
                        default: return Vector2.zero;
                    }
                default:
                    return ToVector2(execution.SpawnOffset);
            }
        }

        private int ResolveShotCount(HazardActorPatternExecutionSlotBuffer execution)
        {
            switch (execution.ShotPatternMode)
            {
                case WaveShotPatternModeId.NWay:
                case WaveShotPatternModeId.Radial:
                    return Mathf.Max(1, execution.ShotCount);
                default:
                    return 1;
            }
        }

        private float ResolveShotOffsetDeg(HazardActorPatternExecutionSlotBuffer execution, int shotIndex, int shotCount)
        {
            switch (execution.ShotPatternMode)
            {
                case WaveShotPatternModeId.NWay:
                    return execution.NWayAngleSpacingDeg * (shotIndex - ((shotCount - 1) * 0.5f));
                case WaveShotPatternModeId.Radial:
                    return 360f * shotIndex / Mathf.Max(1, shotCount);
                default:
                    return 0f;
            }
        }

        private float ResolveAimDeg(HazardActorPatternExecutionSlotBuffer execution, Vector3 spawn, int order)
        {
            switch (execution.AimMode)
            {
                case WaveAimModeId.Fixed:
                    return execution.BaseAngleDeg;
                case WaveAimModeId.Spiral:
                    return execution.BaseAngleDeg + (execution.SpiralStepDeg * order);
                case WaveAimModeId.PlayerPosition:
                    Vector3 toTarget = _input.TargetWorldPosition - spawn;
                    toTarget.y = 0f;
                    return VectorToYawDeg(toTarget) + execution.AimAngleOffsetDeg;
                case WaveAimModeId.LineNormal:
                    float side = execution.LineNormalSide == WaveLineNormalSideId.Left ? -90f : 90f;
                    Vector2 line = ToVector2(execution.LineEnd - execution.LineStart);
                    return Mathf.Atan2(line.x, line.y) * Mathf.Rad2Deg + side + execution.LineNormalAngleOffsetDeg;
                default:
                    return 0f;
            }
        }

        private void CompleteCycle()
        {
            _lifecycle = HazardActorEmitLifecycleStateId.Dormant;
            _lifecycleElapsed = 0f;
            _completedCycleVersion = _completedCycleVersion >= uint.MaxValue ? 1u : _completedCycleVersion + 1u;
        }

        private void ResetSelectionAndEmit()
        {
            _currentPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
            _currentCandidateOrder = -1;
            ResetEmit();
        }

        private void ResetEmit()
        {
            _lifecycle = HazardActorEmitLifecycleStateId.Dormant;
            _lifecycleElapsed = 0f;
            _appliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
        }

        private void CommitPhase(int phaseId)
        {
            _previousPhaseId = _phaseId;
            _phaseId = Math.Max(1, phaseId);
            _phaseVersion = _phaseVersion >= uint.MaxValue ? 1u : _phaseVersion + 1u;
            _transitionState = HazardActorPhaseTransitionStateId.Idle;
            _pendingPhaseId = -1;
            _transitionElapsed = 0f;
            _transitionDuration = 0f;
            ResetEmit();
        }

        private bool TryFindProgressTransition(int fromPhaseId, out HazardActorPhaseProgressTransitionBuffer transition)
        {
            var transitions = _snapshot.Transitions ?? Array.Empty<HazardActorPhaseProgressTransitionBuffer>();
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].FromPhaseId == fromPhaseId)
                {
                    transition = transitions[i];
                    return true;
                }
            }
            transition = default;
            return false;
        }

        private bool TryFindPolicy(int phaseId, out HazardActorPhaseSelectorPolicyBuffer policy)
        {
            var policies = _snapshot.Policies ?? Array.Empty<HazardActorPhaseSelectorPolicyBuffer>();
            for (int i = 0; i < policies.Length; i++)
            {
                if (policies[i].PhaseId == phaseId)
                {
                    policy = policies[i];
                    return true;
                }
            }
            policy = default;
            return false;
        }

        private bool TryFindFirstCandidate(int phaseId, int minOrderExclusive, out HazardActorPhaseSelectorCandidateBuffer candidate)
        {
            candidate = default;
            int bestOrder = int.MaxValue;
            bool found = false;
            for (int pass = 0; pass < 2; pass++)
            {
                int minOrder = pass == 0 ? minOrderExclusive : -1;
                var candidates = _snapshot.Candidates ?? Array.Empty<HazardActorPhaseSelectorCandidateBuffer>();
                for (int i = 0; i < candidates.Length; i++)
                {
                    var current = candidates[i];
                    if (current.PhaseId != phaseId || current.OrderIndex <= minOrder || current.OrderIndex >= bestOrder)
                        continue;
                    if (!TryFindPatternSlot(current.PatternSlotId, out _))
                        continue;
                    candidate = current;
                    bestOrder = current.OrderIndex;
                    found = true;
                }
                if (found || minOrderExclusive < 0)
                    break;
            }
            return found;
        }

        private bool CurrentSelectionEligible()
        {
            if (_currentPatternSlotId < 0 || !TryFindPatternSlot(_currentPatternSlotId, out _))
                return false;
            var candidates = _snapshot.Candidates ?? Array.Empty<HazardActorPhaseSelectorCandidateBuffer>();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].PhaseId == _phaseId && candidates[i].PatternSlotId == _currentPatternSlotId)
                    return true;
            }
            return false;
        }

        private void ApplyPatternSelection(int patternSlotId, int orderIndex)
        {
            if (patternSlotId <= 0 || !TryFindPatternSlot(patternSlotId, out _))
                return;
            if (_currentPatternSlotId != patternSlotId)
                ResetEmit();
            _currentPatternSlotId = patternSlotId;
            _currentCandidateOrder = orderIndex;
        }

        private int FirstPatternSlotId()
        {
            return _snapshot != null && _snapshot.PatternSlots != null && _snapshot.PatternSlots.Length > 0
                ? _snapshot.PatternSlots[0].PatternSlotId
                : HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
        }

        private bool TryFindPatternSlot(int patternSlotId, out HazardActorPreviewPatternSlot slot)
        {
            var slots = _snapshot?.PatternSlots ?? Array.Empty<HazardActorPreviewPatternSlot>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].PatternSlotId == patternSlotId)
                {
                    slot = slots[i];
                    return true;
                }
            }
            slot = null;
            return false;
        }

        private void PublishFrame()
        {
            Frame.Presence = _presence;
            Frame.PhaseId = _phaseId;
            Frame.PatternSlotId = _currentPatternSlotId;
            Frame.Lifecycle = _lifecycle;
            Frame.LifecycleElapsedSec = _lifecycleElapsed;
            Frame.ActiveGhostCount = _ghosts.Count;
            Frame.SuppressedGhostCount = _suppressedGhostCount;
            Frame.Warning = _warning;
        }

        private static Vector3 ToWorldOffset(Vector2 value)
        {
            return new Vector3(value.x, 0f, value.y);
        }

        private static Vector2 ToVector2(float2 value)
        {
            return new Vector2(value.x, value.y);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static float VectorToYawDeg(Vector3 vector)
        {
            vector.y = 0f;
            if (vector.sqrMagnitude <= 0.0001f)
                return 0f;
            return Mathf.Atan2(vector.x, vector.z) * Mathf.Rad2Deg;
        }
    }

    public sealed class HazardActorEncounterPreviewSession : IDisposable
    {
        private sealed class ActorPlan
        {
            public int PlacementInstanceId;
            public HazardActorPreviewSnapshot Snapshot;
            public HazardActorPreviewInput Input;
            public HazardActorEncounterRulePreview[] Rules = Array.Empty<HazardActorEncounterRulePreview>();
        }

        private readonly List<ActorPlan> _plans = new List<ActorPlan>(16);
        private readonly List<HazardActorPreviewSession> _actors = new List<HazardActorPreviewSession>(16);
        private readonly HazardActorPreviewFrame _frame = new HazardActorPreviewFrame();
        private float _sourceProgress01;

        public HazardActorPreviewFrame Frame => _frame;
        public IReadOnlyList<HazardActorPreviewSession> Actors => _actors;
        public bool Playing { get; private set; }
        public bool IsDisposed { get; private set; }
        public int ActiveActorCount => _actors.Count;
        public float TimeSec
        {
            get
            {
                float time = 0f;
                for (int i = 0; i < _actors.Count; i++)
                    time = Mathf.Max(time, _actors[i].TimeSec);
                return time;
            }
        }

        public void AddActor(HazardActorPreviewSnapshot snapshot, HazardActorPreviewInput input)
        {
            AddActorPlan(0, snapshot, input, Array.Empty<HazardActorEncounterRulePreview>());
        }

        public void AddActorPlan(
            int placementInstanceId,
            HazardActorPreviewSnapshot snapshot,
            HazardActorPreviewInput input,
            HazardActorEncounterRulePreview[] rules)
        {
            if (snapshot == null || snapshot.HasErrors)
                return;

            input.Scope = HazardActorPreviewScope.Encounter;
            input.GhostCapOverride = Mathf.Max(1, input.GhostCapOverride);
            _plans.Add(new ActorPlan
            {
                PlacementInstanceId = placementInstanceId,
                Snapshot = snapshot,
                Input = input,
                Rules = rules ?? Array.Empty<HazardActorEncounterRulePreview>(),
            });
            RebuildActorsForProgress();
        }

        public void Play()
        {
            Playing = true;
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].Play();
        }

        public void Pause()
        {
            Playing = false;
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].Pause();
        }

        public void Restart()
        {
            Playing = false;
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].Restart();
            PublishFrame();
        }

        public void SetSourceProgress(float sourceProgress01)
        {
            float next = Mathf.Clamp01(sourceProgress01);
            if (Mathf.Approximately(_sourceProgress01, next))
                return;
            _sourceProgress01 = next;
            float time = TimeSec;
            bool wasPlaying = Playing;
            RebuildActorsForProgress();
            EvaluateAt(time);
            if (wasPlaying)
                Play();
        }

        public void Step()
        {
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].Step();
            PublishFrame();
        }

        public void EvaluateAt(float timeSec)
        {
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].EvaluateAt(timeSec);
            PublishFrame();
        }

        public bool CleanupOldestGhost()
        {
            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i].CleanupOldestGhost())
                {
                    PublishFrame();
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            IsDisposed = true;
            Playing = false;
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].Dispose();
            _actors.Clear();
            _plans.Clear();
            PublishFrame();
        }

        private void RebuildActorsForProgress()
        {
            for (int i = 0; i < _actors.Count; i++)
                _actors[i].Dispose();
            _actors.Clear();

            for (int i = 0; i < _plans.Count; i++)
            {
                if (!TryResolveInputAtProgress(_plans[i], out var input))
                    continue;

                var actor = new HazardActorPreviewSession();
                actor.Load(_plans[i].Snapshot, input);
                if (Playing)
                    actor.Play();
                _actors.Add(actor);
            }

            PublishFrame();
        }

        private bool TryResolveInputAtProgress(ActorPlan plan, out HazardActorPreviewInput input)
        {
            input = plan.Input;
            input.SourceProgress01 = _sourceProgress01;
            bool hasRule = false;
            bool spawned = false;
            bool retired = false;
            int forcedPhaseId = 0;
            var rules = plan.Rules ?? Array.Empty<HazardActorEncounterRulePreview>();
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (plan.PlacementInstanceId > 0 && rule.PlacementInstanceId != plan.PlacementInstanceId)
                    continue;
                hasRule = true;
                if (!IsRuleTriggered(rule, _sourceProgress01))
                    continue;

                switch (rule.ActionType)
                {
                    case HazardActorOrchestrationActionId.Spawn:
                        spawned = true;
                        retired = false;
                        break;
                    case HazardActorOrchestrationActionId.PhaseSet:
                        forcedPhaseId = Math.Max(0, rule.TargetPhaseId);
                        break;
                    case HazardActorOrchestrationActionId.Retire:
                        retired = true;
                        break;
                }
            }

            if (hasRule && (!spawned || retired))
                return false;
            input.SpawnAtStart = !hasRule || spawned;
            input.ForcedPhaseId = forcedPhaseId;
            return true;
        }

        private static bool IsRuleTriggered(HazardActorEncounterRulePreview rule, float progress01)
        {
            return rule.TriggerType == HazardActorOrchestrationTriggerId.OnStageStart
                || progress01 >= rule.TriggerThresholdNormalized;
        }

        private void PublishFrame()
        {
            int activeGhosts = 0;
            int suppressedGhosts = 0;
            string warning = string.Empty;
            for (int i = 0; i < _actors.Count; i++)
            {
                activeGhosts += _actors[i].Frame.ActiveGhostCount;
                suppressedGhosts += _actors[i].Frame.SuppressedGhostCount;
                if (string.IsNullOrEmpty(warning) && !string.IsNullOrEmpty(_actors[i].Frame.Warning))
                    warning = _actors[i].Frame.Warning;
            }

            if (activeGhosts >= HazardActorPreviewSession.EncounterGhostCap || suppressedGhosts > 0)
                warning = $"Encounter ghost cap reached. cap={HazardActorPreviewSession.EncounterGhostCap}, active={activeGhosts}, suppressed={suppressedGhosts}";

            _frame.Presence = _actors.Count > 0 ? HazardActorPresenceStateId.Active : HazardActorPresenceStateId.Hidden;
            _frame.ActiveGhostCount = activeGhosts;
            _frame.SuppressedGhostCount = suppressedGhosts;
            _frame.Warning = warning;
        }
    }

    public static class HazardActorPreviewCoordinator
    {
        private static HazardActorPreviewSession _activeSession;
        private static HazardActorEncounterPreviewSession _activeEncounterSession;
        private static double _lastUpdateTime;
        private static double _playWallAnchorTime;
        private static float _playPreviewAnchorTime;
        private static bool _wasPlaying;
        private static bool _hooksRegistered;

        public static event Action PreviewRepaintRequested;

        public static HazardActorPreviewSession ActiveSession => _activeSession;
        public static HazardActorEncounterPreviewSession ActiveEncounterSession => _activeEncounterSession;
        public static int ActiveCallbackCount => _hooksRegistered ? 2 : 0;

        public static void SetActiveSession(HazardActorPreviewSession session)
        {
            if (_activeSession == session)
                return;
            _activeSession?.Dispose();
            _activeEncounterSession?.Dispose();
            _activeSession = session;
            _activeEncounterSession = null;
            ResetPlaybackClock(EditorApplication.timeSinceStartup);
            if (_activeSession != null)
                EnsureHooks();
        }

        public static void SetActiveEncounterSession(HazardActorEncounterPreviewSession session)
        {
            if (_activeEncounterSession == session)
                return;
            _activeSession?.Dispose();
            _activeEncounterSession?.Dispose();
            _activeSession = null;
            _activeEncounterSession = session;
            ResetPlaybackClock(EditorApplication.timeSinceStartup);
            if (_activeEncounterSession != null)
                EnsureHooks();
        }

        public static void ClearActiveSession(HazardActorPreviewSession session)
        {
            if (_activeSession != session)
                return;
            _activeSession?.Dispose();
            _activeSession = null;
            ResetPlaybackClock(EditorApplication.timeSinceStartup);
            RemoveHooksIfIdle();
        }

        public static void Shutdown()
        {
            _activeSession?.Dispose();
            _activeEncounterSession?.Dispose();
            _activeSession = null;
            _activeEncounterSession = null;
            ResetPlaybackClock(EditorApplication.timeSinceStartup);
            HazardActorPreviewRendererUtility.Dispose();
            RemoveHooks();
        }

        public static void StepActiveSession()
        {
            if (_activeSession != null)
                _activeSession.Step();
            else
                _activeEncounterSession?.Step();
            ResetPlaybackClock(EditorApplication.timeSinceStartup);
            RequestPreviewRepaint();
        }

        public static void EvaluatePlaybackForTests(double editorTimeSinceStartup)
        {
            UpdateAt(editorTimeSinceStartup);
        }

        public static void DrawScenePreview()
        {
            if (_activeEncounterSession != null)
            {
                DrawEncounterScenePreview(_activeEncounterSession);
                return;
            }

            if (_activeSession == null || _activeSession.Snapshot == null)
                return;

            var input = _activeSession.Input;
            float radius = 0.25f;
            Handles.color = Color.magenta;
            Handles.DrawWireDisc(input.ActorWorldPosition, Vector3.up, radius);
            Vector3 forward = Quaternion.Euler(0f, input.ActorYawDeg, 0f) * Vector3.forward;
            Handles.DrawLine(input.ActorWorldPosition, input.ActorWorldPosition + forward * 0.7f);

            var ghosts = _activeSession.Ghosts;
            HazardActorPreviewRendererUtility.DrawGhostInstances(
                ghosts,
                0.08f,
                _activeSession.Frame.SuppressedGhostCount > 0);
            if (!string.IsNullOrEmpty(_activeSession.Frame.Warning))
                Handles.Label(input.ActorWorldPosition + Vector3.up * 0.5f, _activeSession.Frame.Warning);
        }

        private static void DrawEncounterScenePreview(HazardActorEncounterPreviewSession encounter)
        {
            var actors = encounter.Actors;
            for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
            {
                var actor = actors[actorIndex];
                var input = actor.Input;
                Handles.color = Color.magenta;
                Handles.DrawWireDisc(input.ActorWorldPosition, Vector3.up, 0.2f);
                Vector3 forward = Quaternion.Euler(0f, input.ActorYawDeg, 0f) * Vector3.forward;
                Handles.DrawLine(input.ActorWorldPosition, input.ActorWorldPosition + forward * 0.55f);

                HazardActorPreviewRendererUtility.DrawGhostInstances(
                    actor.Ghosts,
                    0.07f,
                    actor.Frame.SuppressedGhostCount > 0);
            }

            if (!string.IsNullOrEmpty(encounter.Frame.Warning) && actors.Count > 0)
                Handles.Label(actors[0].Input.ActorWorldPosition + Vector3.up * 0.6f, encounter.Frame.Warning);
        }

        private static void EnsureHooks()
        {
            if (_hooksRegistered)
                return;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            _hooksRegistered = true;
        }

        private static void RemoveHooksIfIdle()
        {
            if (_activeSession != null || _activeEncounterSession != null)
                return;
            RemoveHooks();
        }

        private static void RemoveHooks()
        {
            if (!_hooksRegistered)
                return;
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            _hooksRegistered = false;
        }

        private static void ResetPlaybackClock(double now)
        {
            _lastUpdateTime = now;
            _playWallAnchorTime = now;
            _playPreviewAnchorTime = GetActivePreviewTime();
            _wasPlaying = IsActivePlaying();
        }

        private static float GetActivePreviewTime()
        {
            if (_activeSession != null)
                return _activeSession.TimeSec;
            return _activeEncounterSession != null ? _activeEncounterSession.TimeSec : 0f;
        }

        private static bool IsActivePlaying()
        {
            if (_activeSession != null)
                return _activeSession.Playing;
            return _activeEncounterSession != null && _activeEncounterSession.Playing;
        }

        private static void EvaluateActiveAt(float timeSec)
        {
            if (_activeSession != null)
                _activeSession.EvaluateAt(timeSec);
            else
                _activeEncounterSession?.EvaluateAt(timeSec);
        }

        private static void RequestPreviewRepaint()
        {
            PreviewRepaintRequested?.Invoke();
            SceneView.RepaintAll();
        }

        private static void OnEditorUpdate()
        {
            UpdateAt(EditorApplication.timeSinceStartup);
        }

        private static void UpdateAt(double now)
        {
            if (_activeSession == null && _activeEncounterSession == null)
            {
                RemoveHooksIfIdle();
                return;
            }

            bool playing = IsActivePlaying();
            if (!playing)
            {
                _wasPlaying = false;
                return;
            }

            if (!_wasPlaying)
            {
                ResetPlaybackClock(now);
                _wasPlaying = true;
                return;
            }

            if (now - _lastUpdateTime < HazardActorPreviewSession.FixedDeltaTime)
                return;

            _lastUpdateTime = now;
            float targetTime = _playPreviewAnchorTime + (float)(now - _playWallAnchorTime);
            EvaluateActiveAt(targetTime);
            RequestPreviewRepaint();
        }

        private static void OnSceneGUI(SceneView view)
        {
            DrawScenePreview();
        }
    }
}
