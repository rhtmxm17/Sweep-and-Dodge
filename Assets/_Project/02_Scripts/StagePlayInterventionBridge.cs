using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StagePlayInterventionBridge : MonoBehaviour
    {
        [Header("References")]
        public DemoShellFlowController DemoShell;
        public PlayerRuntimeHudBridge RuntimeHudBridge;
        public DemoShellDialogueBridge DialogueBridge;
        public DemoShellPauseBridge PauseBridge;
        public DemoShellHintBridge HintBridge;

        [Header("Policy")]
        public bool LogBindWarnings = true;

        private uint _lastObservedFeedbackVersion;
        private bool _warnedBindFailure;

        public bool IsInterventionBlocked => !CanEvaluateInterventions();
        public InWorldDialogueTriggerId LastTriggeredIntervention { get; private set; }

        private void Reset()
        {
            DemoShell = GetComponent<DemoShellFlowController>();
            RuntimeHudBridge = GetComponent<PlayerRuntimeHudBridge>();
            DialogueBridge = GetComponent<DemoShellDialogueBridge>();
            PauseBridge = GetComponent<DemoShellPauseBridge>();
            HintBridge = GetComponent<DemoShellHintBridge>();
        }

        private void Update()
        {
            EnsureReferences();

            bool hasFirstHitEdge = TryObserveFirstHitEdge(out uint latestFeedbackVersion);
            int stageId = DemoShell != null ? Mathf.Max(0, DemoShell.CurrentStageId) : 0;
            if (!CanEvaluateInterventions() || stageId <= 0)
            {
                _lastObservedFeedbackVersion = latestFeedbackVersion;
                LastTriggeredIntervention = InWorldDialogueTriggerId.None;
                return;
            }

            if (hasFirstHitEdge && DialogueBridge.TryStartStagePlayIntervention(InWorldDialogueTriggerId.InterventionFirstHit, stageId))
            {
                _lastObservedFeedbackVersion = latestFeedbackVersion;
                LastTriggeredIntervention = InWorldDialogueTriggerId.InterventionFirstHit;
                return;
            }

            _lastObservedFeedbackVersion = latestFeedbackVersion;
            if (TryStartCarryFull(stageId))
            {
                LastTriggeredIntervention = InWorldDialogueTriggerId.InterventionCarryFull;
                return;
            }

            LastTriggeredIntervention = InWorldDialogueTriggerId.None;
        }

        private bool CanEvaluateInterventions()
        {
            if (DemoShell == null
                || RuntimeHudBridge == null
                || DialogueBridge == null
                || DemoShell.CurrentScreen != DemoShellScreenId.StagePlay
                || DemoShell.CurrentStagePlayPhase != DemoShellStagePlayPhaseId.Running
                || !RuntimeHudBridge.HasSnapshot
                || DialogueBridge.IsDialogueActive)
            {
                return false;
            }

            if (PauseBridge != null && PauseBridge.IsPaused)
                return false;

            return PauseBridge == null
                   || PauseBridge.PauseController == null
                   || !PauseBridge.PauseController.CurrentSnapshot.IsPauseMenuOpenBlocked;
        }

        private bool TryObserveFirstHitEdge(out uint latestFeedbackVersion)
        {
            latestFeedbackVersion = _lastObservedFeedbackVersion;
            if (RuntimeHudBridge == null || !RuntimeHudBridge.TryGetLastFeedbackSnapshot(out var snapshot))
                return false;

            if (snapshot.Version > latestFeedbackVersion)
                latestFeedbackVersion = snapshot.Version;

            return snapshot.Version > _lastObservedFeedbackVersion
                   && snapshot.Type == PlayerUiFeedbackEventType.PlayerHazardHit;
        }

        private bool TryStartCarryFull(int stageId)
        {
            if (RuntimeHudBridge == null || !RuntimeHudBridge.TryGetLastSnapshot(out var snapshot))
                return false;
            if (snapshot.CarryCapacity <= 0 || snapshot.CarryLoad < snapshot.CarryCapacity)
                return false;
            if (DemoShellSessionStaging.HasSeenDialogueTriggerThisRun(stageId, InWorldDialogueTriggerId.InterventionCarryFull))
                return false;

            return DialogueBridge != null
                   && DialogueBridge.TryStartStagePlayIntervention(InWorldDialogueTriggerId.InterventionCarryFull, stageId);
        }

        private void EnsureReferences()
        {
            DemoShell ??= GetComponent<DemoShellFlowController>() ?? FindFirst<DemoShellFlowController>();
            RuntimeHudBridge ??= GetComponent<PlayerRuntimeHudBridge>() ?? FindFirst<PlayerRuntimeHudBridge>();
            DialogueBridge ??= GetComponent<DemoShellDialogueBridge>() ?? FindFirst<DemoShellDialogueBridge>();
            PauseBridge ??= GetComponent<DemoShellPauseBridge>() ?? FindFirst<DemoShellPauseBridge>();
            HintBridge ??= GetComponent<DemoShellHintBridge>() ?? FindFirst<DemoShellHintBridge>();

            if ((DemoShell == null || RuntimeHudBridge == null || DialogueBridge == null) && !_warnedBindFailure && LogBindWarnings)
            {
                _warnedBindFailure = true;
                Debug.LogWarning("[StagePlayInterventionBridge] DemoShellFlowController, PlayerRuntimeHudBridge, or DemoShellDialogueBridge was not found.");
            }
            else if (DemoShell != null && RuntimeHudBridge != null && DialogueBridge != null)
            {
                _warnedBindFailure = false;
            }
        }

        private static T FindFirst<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
