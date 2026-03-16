using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class DemoShellNotificationBridge : MonoBehaviour
    {
        [Header("References")]
        public DemoShellFlowController DemoShell;
        public PlayerRuntimeHudBridge RuntimeHudBridge;

        [Header("Policy")]
        public bool LogBindWarnings = true;

        private NotificationRuntimeState _runtimeState;
        private NotificationResolvedState _currentNotification;
        private bool _warnedBindFailure;

        public NotificationResolvedState CurrentNotification => _currentNotification;
        public NotificationRuntimeState RuntimeState => _runtimeState;

        private void Reset()
        {
            DemoShell = GetComponent<DemoShellFlowController>();
            RuntimeHudBridge = GetComponent<PlayerRuntimeHudBridge>();
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
                _currentNotification = default;
                _runtimeState.CurrentId = NotificationId.None;
                _runtimeState.RemainingSec = 0f;
                return;
            }

            float stageTimeLimitSec = ResolveStageTimeLimitSec(DemoShell);
            bool justStageClear = false;
            bool justTimeUp = false;

            if (_runtimeState.LastScreen == DemoShellScreenId.StagePlay
                && DemoShell.CurrentScreen == DemoShellScreenId.StageResult
                && DemoShell.HasCurrentStageResult)
            {
                if (DemoShell.CurrentStageOutcome == DemoShellStageOutcomeId.Clear)
                {
                    justStageClear = true;
                }
                else if (stageTimeLimitSec > 0f && DemoShell.CurrentStageResult.ElapsedSec >= stageTimeLimitSec)
                {
                    justTimeUp = true;
                }
            }

            bool hasFeedbackSnapshot = RuntimeHudBridge.TryGetLastFeedbackSnapshot(out var feedbackSnapshot);
            var context = new NotificationResolveContext(
                DemoShell.CurrentScreen,
                stageTimeLimitSec,
                hudSnapshot,
                hasFeedbackSnapshot,
                feedbackSnapshot,
                RuntimeHudBridge.LastFeedbackLine,
                justStageClear,
                justTimeUp,
                deltaSec);

            _currentNotification = NotificationResolver.Resolve(in context, ref _runtimeState);
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

            if (RuntimeHudBridge == null)
                RuntimeHudBridge = GetComponent<PlayerRuntimeHudBridge>() ?? FindFirst<PlayerRuntimeHudBridge>();

            if ((DemoShell == null || RuntimeHudBridge == null) && !_warnedBindFailure && LogBindWarnings)
            {
                _warnedBindFailure = true;
                Debug.LogWarning("[DemoShellNotificationBridge] DemoShellFlowController or PlayerRuntimeHudBridge was not found.");
            }
            else if (DemoShell != null && RuntimeHudBridge != null)
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
