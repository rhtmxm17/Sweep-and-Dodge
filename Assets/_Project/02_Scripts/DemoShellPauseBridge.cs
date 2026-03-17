using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Runtime pause/modal owner.
    /// - UI는 이 브리지를 통해 pause open/close 및 destructive action routing만 요청한다.
    /// - 실제 world time/fixed tick pause는 후속 단계에서 별도 결정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoShellPauseBridge : MonoBehaviour
    {
        [Header("References")]
        public DemoShellFlowController DemoShell;
        public RunDirectorStageBridge StageBridge;
        public DemoShellGameplayPauseController PauseController;

        [Header("Policy")]
        public bool LogBindWarnings = true;

        private bool _isPaused;
        private DemoShellPauseActionId _pendingAction;
        private GameplayPauseHandle _pauseHandle;
        private bool _warnedBindFailure;

        public bool CanPause
        {
            get
            {
                EnsureReferences();
                return DemoShell != null
                       && PauseController != null
                       && DemoShell.CurrentScreen == DemoShellScreenId.StagePlay
                       && !DemoShell.IsDialogueInputExclusive
                       && !PauseController.CurrentSnapshot.IsPauseMenuOpenBlocked;
            }
        }
        public bool IsPaused => _isPaused;
        public DemoShellPauseActionId PendingAction => _pendingAction;
        public bool GameplayInputBlocked
        {
            get
            {
                EnsureReferences();
                return PauseController != null && PauseController.CurrentSnapshot.IsGameplayInputBlocked;
            }
        }

        private void Reset()
        {
            DemoShell = GetComponent<DemoShellFlowController>();
            StageBridge = GetComponent<RunDirectorStageBridge>();
            PauseController = GetComponent<DemoShellGameplayPauseController>();
        }

        private void Update()
        {
            EnsureReferences();

            if (_isPaused && !CanPause)
            {
                _isPaused = false;
                _pendingAction = DemoShellPauseActionId.Resume;
            }
        }

        public bool RequestPause()
        {
            EnsureReferences();
            if (!CanPause)
                return false;
            if (_isPaused)
                return true;

            GameplayPauseHandle handle = PauseController.Acquire(
                GameplayPauseReasonId.PauseMenu,
                GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);
            if (!handle.IsValid)
                return false;

            _pauseHandle = handle;
            _isPaused = true;
            _pendingAction = DemoShellPauseActionId.Resume;
            return true;
        }

        public bool RequestResume()
        {
            if (!_isPaused)
                return false;

            ReleasePauseState();
            return true;
        }

        public bool RequestConfirmedAction(DemoShellPauseActionId action)
        {
            EnsureReferences();
            if (!_isPaused || DemoShell == null)
                return false;

            bool ok = action switch
            {
                DemoShellPauseActionId.Resume => RequestResume(),
                DemoShellPauseActionId.RestartStage => DemoShell.RequestRestartFromPause(),
                DemoShellPauseActionId.ReturnToLobby => DemoShell.RequestReturnToLobbyFromPause(),
                DemoShellPauseActionId.QuitApplication => DemoShell.RequestQuit(),
                _ => false,
            };

            if (!ok)
                return false;

            _pendingAction = action;
            if (action != DemoShellPauseActionId.QuitApplication)
                ReleasePauseState();
            return true;
        }

        public void SetPendingAction(DemoShellPauseActionId action)
        {
            _pendingAction = action;
        }

        private void EnsureReferences()
        {
            if (DemoShell == null)
            {
                DemoShell = GetComponent<DemoShellFlowController>();
                if (DemoShell == null)
                    DemoShell = FindFirst<DemoShellFlowController>();
            }

            if (StageBridge == null)
            {
                StageBridge = GetComponent<RunDirectorStageBridge>();
                if (StageBridge == null)
                    StageBridge = FindFirst<RunDirectorStageBridge>();
            }

            if (PauseController == null)
            {
                PauseController = GetComponent<DemoShellGameplayPauseController>();
                if (PauseController == null)
                    PauseController = FindFirst<DemoShellGameplayPauseController>();
            }

            if ((DemoShell == null || PauseController == null) && !_warnedBindFailure && LogBindWarnings)
            {
                _warnedBindFailure = true;
                Debug.LogWarning("[DemoShellPauseBridge] DemoShellFlowController or DemoShellGameplayPauseController was not found.");
            }
            else if (DemoShell != null && PauseController != null)
            {
                _warnedBindFailure = false;
            }
        }

        private void ReleasePauseState()
        {
            if (PauseController != null && _pauseHandle.IsValid)
                PauseController.Release(_pauseHandle);

            _pauseHandle = GameplayPauseHandle.Invalid;
            _isPaused = false;
            _pendingAction = DemoShellPauseActionId.Resume;
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
