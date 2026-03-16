using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class DemoShellHintBridge : MonoBehaviour
    {
        [Header("References")]
        public DemoShellFlowController DemoShell;
        public DemoShellPauseBridge PauseBridge;
        public PlayerRuntimeHudBridge RuntimeHudBridge;
        public DemoShellNotificationBridge NotificationBridge;

        [Header("Policy")]
        public bool LogBindWarnings = true;

        private HintStageState _stageState;
        private HintResolvedState _currentHint;
        private bool _warnedBindFailure;

        public HintResolvedState CurrentHint => _currentHint;
        public HintStageState StageState => _stageState;

        private void Reset()
        {
            DemoShell = GetComponent<DemoShellFlowController>();
            PauseBridge = GetComponent<DemoShellPauseBridge>();
            RuntimeHudBridge = GetComponent<PlayerRuntimeHudBridge>();
            NotificationBridge = GetComponent<DemoShellNotificationBridge>();
        }

        private void Update()
        {
            RefreshState(Time.unscaledDeltaTime);
        }

        public void RefreshPresentationState()
        {
            RefreshState(0f);
        }

        public void RefreshState(float deltaSec)
        {
            EnsureReferences();
            if (DemoShell == null || RuntimeHudBridge == null || !RuntimeHudBridge.TryGetLastSnapshot(out var hudSnapshot))
            {
                _currentHint = default;
                _stageState.CurrentId = HintId.None;
                _stageState.RemainingSec = 0f;
                return;
            }

            int stageId = Mathf.Max(0, DemoShell.CurrentStageId);
            SyncStageSeen(stageId);

            float stageTimeLimitSec = ResolveStageTimeLimitSec(DemoShell);
            NotificationId currentNotificationId = NotificationBridge != null
                ? NotificationBridge.CurrentNotification.Id
                : NotificationId.None;

            var context = new HintResolveContext(
                DemoShell.CurrentScreen,
                stageId,
                PauseBridge != null && PauseBridge.IsPaused,
                hudSnapshot,
                DemoShell.CurrentStageOutcome,
                DemoShell.CurrentStageResult,
                DemoShell.HasCurrentStageResult,
                stageTimeLimitSec,
                currentNotificationId,
                _stageState.StageSeenMask,
                DemoShellSessionStaging.HintSessionSeenMask,
                deltaSec);

            HintId previousHintId = _stageState.CurrentId;
            _currentHint = HintResolver.Resolve(in context, ref _stageState);
            if (_currentHint.Id != HintId.None && _currentHint.Id != previousHintId)
                MarkSeen(_currentHint.Id, stageId);
        }

        private void SyncStageSeen(int stageId)
        {
            if (_stageState.ActiveStageId == stageId)
                return;

            _stageState.ActiveStageId = stageId;
            _stageState.CurrentId = HintId.None;
            _stageState.RemainingSec = 0f;
            _stageState.LastFailureHint = HintId.None;
            _stageState.PreviousCarryFull = false;
            _stageState.PreviousHitVisible = false;

            if (stageId > 0 && DemoShellSessionStaging.TryGetActiveStageSeen(stageId, out ulong seenMask))
                _stageState.StageSeenMask = seenMask;
            else
                _stageState.StageSeenMask = 0UL;
        }

        private void MarkSeen(HintId id, int stageId)
        {
            if (HintResolver.IsSessionScoped(id))
                DemoShellSessionStaging.MarkSessionSeenHint(id);

            if (!HintResolver.IsStageScoped(id))
                return;

            _stageState.StageSeenMask = HintResolver.MarkSeen(_stageState.StageSeenMask, id);
            if (stageId > 0)
                DemoShellSessionStaging.SetActiveStageSeen(stageId, _stageState.StageSeenMask);
        }

        private float ResolveStageTimeLimitSec(DemoShellFlowController shell)
        {
            if (shell == null || shell.StageProfiles == null || shell.StageProfiles.Length <= 0)
                return -1f;

            int stageIndex = shell.CurrentStageIndex;
            if (stageIndex >= 0 && stageIndex < shell.StageProfiles.Length)
                return shell.StageProfiles[stageIndex].StageTimeLimitSec;

            int stageId = shell.CurrentStageId;
            for (int i = 0; i < shell.StageProfiles.Length; i++)
            {
                if (shell.StageProfiles[i].StageId == stageId)
                    return shell.StageProfiles[i].StageTimeLimitSec;
            }

            return -1f;
        }

        private void EnsureReferences()
        {
            if (DemoShell == null)
                DemoShell = GetComponent<DemoShellFlowController>() ?? FindFirst<DemoShellFlowController>();
            if (PauseBridge == null)
                PauseBridge = GetComponent<DemoShellPauseBridge>() ?? FindFirst<DemoShellPauseBridge>();
            if (RuntimeHudBridge == null)
                RuntimeHudBridge = GetComponent<PlayerRuntimeHudBridge>() ?? FindFirst<PlayerRuntimeHudBridge>();
            if (NotificationBridge == null)
                NotificationBridge = GetComponent<DemoShellNotificationBridge>() ?? FindFirst<DemoShellNotificationBridge>();

            if ((DemoShell == null || RuntimeHudBridge == null || PauseBridge == null || NotificationBridge == null)
                && !_warnedBindFailure
                && LogBindWarnings)
            {
                _warnedBindFailure = true;
                Debug.LogWarning("[DemoShellHintBridge] Required references were not found.");
            }
            else if (DemoShell != null && RuntimeHudBridge != null && PauseBridge != null && NotificationBridge != null)
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
