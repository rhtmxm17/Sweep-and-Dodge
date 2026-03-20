using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Demo Shell 화면 전이를 소유한다.
    /// ECS RunDirector 상태 읽기/요청 쓰기는 RunDirectorStageBridge를 통해 수행하고, topology 요청은 StageTopologyBridge를 통해 수행한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunDirectorStageBridge), typeof(StageTopologyBridge))]
    public sealed class DemoShellFlowController : MonoBehaviour
    {
        private const float DefaultStage1TimeLimitSec = 150f;
        private const float DefaultStage2TimeLimitSec = 180f;
        private const float DefaultStage3TimeLimitSec = 210f;

        [Header("References")]
        public RunDirectorStageBridge StageBridge;
        public StageTopologyBridge TopologyBridge;
        [Header("Stage Data")]
        public StageCatalogSO StageCatalog;

        [Header("Overlay")]
        public bool ShowOverlay = true;
        public Rect OverlayRect = new Rect(12f, 12f, 420f, 300f);

        [Header("Input")]
        public bool EnableKeyboardFallback = true;

        [Header("Stage Profiles")]
        public DemoShellStageProfile[] StageProfiles =
        {
            new DemoShellStageProfile { StageId = 1, DisplayName = "Stage 1", IsFinalStage = false, StageTimeLimitSec = DefaultStage1TimeLimitSec },
            new DemoShellStageProfile { StageId = 2, DisplayName = "Stage 2", IsFinalStage = false, StageTimeLimitSec = DefaultStage2TimeLimitSec },
            new DemoShellStageProfile { StageId = 3, DisplayName = "Stage 3", IsFinalStage = true, StageTimeLimitSec = DefaultStage3TimeLimitSec },
        };

        [Header("Debug")]
        public bool LogTransitions;

        private RunDirectorStageBridge _subscribedBridge;
        private PlayerRuntimeHudBridge _runtimeHudBridge;
        private DemoAudioBridge _demoAudioBridge;
        private DemoShellScreenId _currentScreen;
        private int _currentStageIndex = -1;
        private bool _stageStartPending;
        private bool _stageTopologyApplyPending;
        private DemoShellStagePlayPhaseId _currentStagePlayPhase;
        private DemoShellStageResultMetrics _pendingClearResult;
        private bool _hasPendingClearResult;
        private bool _clearPresentationCompletionSent;
        private Action<DemoShellStageResultMetrics> _preResultClearPresentationRequested;
        private bool _warnedNoBridge;
        private bool _warnedNoTopologyBridge;
        private bool _warnedStageCatalogIssue;
        private bool _stageRunningObserved;
        private int _stageStartTotalCollectValue;
        private int _stageStartTotalCleanupValue;
        private int _stageStartTotalHitValue;
        private DemoShellStageOutcomeId _currentStageOutcome;
        private DemoShellStageResultMetrics _currentStageResult;
        private bool _hasCurrentStageResult;
        private DemoShellSessionMetrics _sessionMetrics;
        private bool _hasSessionMetrics;
        private bool _runtimeUiShellActive;

        public DemoShellScreenId CurrentScreen => _currentScreen;
        public int CurrentStageIndex => _currentStageIndex;
        public int CurrentStageId => TryGetStageProfile(_currentStageIndex, out var profile) ? profile.StageId : 0;
        public DemoShellStagePlayPhaseId CurrentStagePlayPhase => _currentStagePlayPhase;
        public DemoShellStageOutcomeId CurrentStageOutcome => _currentStageOutcome;
        public bool HasCurrentStageResult => _hasCurrentStageResult;
        public DemoShellStageResultMetrics CurrentStageResult => _currentStageResult;
        public bool HasSessionMetrics => _hasSessionMetrics;
        public DemoShellSessionMetrics SessionMetrics => _sessionMetrics;
        public bool IsPreResultClearPresentationActive =>
            _currentScreen == DemoShellScreenId.StagePlay
            && (_currentStagePlayPhase == DemoShellStagePlayPhaseId.ClearPresentation
                || _currentStagePlayPhase == DemoShellStagePlayPhaseId.AwaitingClearCompleted);
        public bool IsDialogueInputExclusive => IsPreResultClearPresentationActive;
        public bool RuntimeUiShellActive => _runtimeUiShellActive;

        public event Action<DemoShellStageResultMetrics> PreResultClearPresentationRequested
        {
            add => _preResultClearPresentationRequested += value;
            remove => _preResultClearPresentationRequested -= value;
        }

        private void Reset()
        {
            StageBridge = GetComponent<RunDirectorStageBridge>();
            TopologyBridge = GetComponent<StageTopologyBridge>();
        }

        private void OnEnable()
        {
            EnsureStageProfiles();
            EnsureBridgeReference();
            EnsureTopologyBridgeReference();
            SyncTopologyBridgeStageCatalogReference();
            EnsureRuntimeHudBridge();
            EnsureDemoAudioBridge();
            EnsureBridgeSubscription();
            if (!TryBootFromSessionStaging())
                TransitionTo(DemoShellScreenId.Title);
        }

        private void OnDisable()
        {
            if (_subscribedBridge != null)
            {
                _subscribedBridge.StageRunCompleted -= OnStageRunCompleted;
                _subscribedBridge = null;
            }
        }

        private void OnValidate()
        {
            EnsureStageProfiles();
        }

        private void Update()
        {
            EnsureBridgeReference();
            EnsureTopologyBridgeReference();
            SyncTopologyBridgeStageCatalogReference();
            EnsureRuntimeHudBridge();
            EnsureDemoAudioBridge();
            EnsureBridgeSubscription();
            TryConsumeCompletedStateFallback();
            TryBootFromSessionStagingIfPending();
            ProcessKeyboardFallback();
            TickStagePlayFlow();
        }

        private void OnGUI()
        {
            if (!ShowOverlay || _runtimeUiShellActive)
                return;

            GUILayout.BeginArea(OverlayRect, GUI.skin.box);
            GUILayout.Label($"Demo Shell: {_currentScreen}");
            if (_currentStageIndex >= 0 && TryGetStageProfile(_currentStageIndex, out var profile))
                GUILayout.Label($"Current Stage: {profile.StageId} ({profile.DisplayName})");

            switch (_currentScreen)
            {
                case DemoShellScreenId.Title:
                    GUILayout.Space(8f);
                    GUILayout.Label("Press any key or Start.");
                    if (GUILayout.Button("Start Demo"))
                        RequestStartFromTitle();
                    break;

                case DemoShellScreenId.Lobby:
                    GUILayout.Space(8f);
                    GUILayout.Label("Select Stage");
                    for (int i = 0; i < StageProfiles.Length; i++)
                    {
                        var entry = StageProfiles[i];
                        if (GUILayout.Button($"{entry.StageId}. {entry.DisplayName}"))
                            RequestSelectStageById(entry.StageId);
                    }
                    if (GUILayout.Button("Quit"))
                        RequestQuit();
                    break;

                case DemoShellScreenId.StagePlay:
                    GUILayout.Space(8f);
                    GUILayout.Label("Playing...");
                    GUILayout.Label("Wait for Result trigger on ClearReady.");
                    if (TryGetStageProfile(_currentStageIndex, out var activeProfile))
                    {
                        float limit = Mathf.Max(0f, activeProfile.StageTimeLimitSec);
                        if (limit > 0f)
                            GUILayout.Label($"Time: {ReadCurrentStageGameplayElapsedSec():0.0}s / {limit:0.0}s");
                    }
                    bool canForceClearReady = CanRequestForceClearReady();
                    using (new GuiEnabledScope(canForceClearReady))
                    {
                        if (GUILayout.Button("Force ClearReady (Test)"))
                            RequestForceClearReadyForTest();
                    }
                    if (GUILayout.Button("Give Up"))
                        RequestGiveUp();
                    break;

                case DemoShellScreenId.StageResult:
                    GUILayout.Space(8f);
                    GUILayout.Label($"Stage Result ({_currentStageOutcome})");
                    if (_hasCurrentStageResult)
                    {
                        GUILayout.Label($"Time: {_currentStageResult.ElapsedSec:0.0}s");
                        GUILayout.Label(
                            $"Collect/Cleanup/Hit: {_currentStageResult.CollectValue}/{_currentStageResult.CleanupValue}/{_currentStageResult.HitValue}");
                    }

                    if (_currentStageOutcome == DemoShellStageOutcomeId.Clear && GUILayout.Button("Next Stage"))
                        RequestResultAction(DemoShellResultActionId.NextStage);
                    if (GUILayout.Button("Retry"))
                        RequestResultAction(DemoShellResultActionId.Retry);
                    if (GUILayout.Button("Return to Lobby"))
                        RequestResultAction(DemoShellResultActionId.ReturnToLobby);
                    break;

                case DemoShellScreenId.DemoComplete:
                    GUILayout.Space(8f);
                    GUILayout.Label("Demo Complete");
                    if (_hasSessionMetrics)
                    {
                        GUILayout.Label($"Cleared Stages: {_sessionMetrics.ClearedStageCount}");
                        GUILayout.Label($"Total Time: {_sessionMetrics.TotalElapsedSec:0.0}s");
                        GUILayout.Label(
                            $"Total Collect/Cleanup/Hit: {_sessionMetrics.TotalCollectValue}/{_sessionMetrics.TotalCleanupValue}/{_sessionMetrics.TotalHitValue}");
                    }
                    if (GUILayout.Button("Restart Demo"))
                        RequestRestartDemo();
                    if (GUILayout.Button("Return to Lobby"))
                        RequestReturnToLobbyFromComplete();
                    if (GUILayout.Button("Quit"))
                        RequestQuit();
                    break;
            }

            DrawAudioOptionsSection();
            GUILayout.EndArea();
        }

        public bool RequestStartFromTitle()
        {
            if (_currentScreen != DemoShellScreenId.Title)
                return false;

            TransitionTo(DemoShellScreenId.Lobby);
            return true;
        }

        public bool RequestSelectStageById(int stageId)
        {
            if (_currentScreen != DemoShellScreenId.Lobby)
                return false;

            int stageIndex = ResolveStageIndexById(stageId);
            if (stageIndex < 0)
                return false;

            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ClearActiveStageSeen();
            DemoShellSessionStaging.ResetDialogueSessionState();
            RefreshSessionMetrics();
            EnterStagePlay(stageIndex);
            return true;
        }

        public bool RequestResultAction(DemoShellResultActionId action)
        {
            if (_currentScreen != DemoShellScreenId.StageResult)
                return false;

            if (!TryGetStageProfile(_currentStageIndex, out var profile))
                return false;

            switch (action)
            {
                case DemoShellResultActionId.NextStage:
                {
                    if (_currentStageOutcome == DemoShellStageOutcomeId.Fail)
                        return false;

                    if (profile.IsFinalStage)
                    {
                        RefreshSessionMetrics();
                        TransitionTo(DemoShellScreenId.DemoComplete);
                        return true;
                    }

                    int nextStageIndex = _currentStageIndex + 1;
                    if (!TryGetStageProfile(nextStageIndex, out _))
                    {
                        RefreshSessionMetrics();
                        TransitionTo(DemoShellScreenId.DemoComplete);
                        return true;
                    }

                    DemoShellSessionStaging.ClearActiveStageSeen();
                    DemoShellSessionStaging.StageStagePlay(nextStageIndex);
                    ReloadActiveScene();
                    return true;
                }
                case DemoShellResultActionId.Retry:
                    DemoShellSessionStaging.StageStagePlay(_currentStageIndex);
                    ReloadActiveScene();
                    return true;
                case DemoShellResultActionId.ReturnToLobby:
                    DemoShellSessionStaging.ResetSessionMetrics();
                    DemoShellSessionStaging.ClearActiveStageSeen();
                    DemoShellSessionStaging.ResetDialogueSessionState();
                    DemoShellSessionStaging.StageLobby();
                    ReloadActiveScene();
                    return true;
            }

            return false;
        }

        public bool RequestRestartDemo()
        {
            if (_currentScreen != DemoShellScreenId.DemoComplete)
                return false;

            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
            DemoShellSessionStaging.StageLobby();
            ReloadActiveScene();
            return true;
        }

        public bool RequestRestartFromPause()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay)
                return false;
            if (!TryGetStageProfile(_currentStageIndex, out _))
                return false;

            DemoShellSessionStaging.StageStagePlay(_currentStageIndex);
            ReloadActiveScene();
            return true;
        }

        public bool RequestReturnToLobbyFromPause()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay)
                return false;

            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ClearActiveStageSeen();
            DemoShellSessionStaging.ResetDialogueSessionState();
            DemoShellSessionStaging.StageLobby();
            ReloadActiveScene();
            return true;
        }

        public bool RequestReturnToLobbyFromComplete()
        {
            if (_currentScreen != DemoShellScreenId.DemoComplete)
                return false;

            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ClearActiveStageSeen();
            DemoShellSessionStaging.ResetDialogueSessionState();
            DemoShellSessionStaging.StageLobby();
            ReloadActiveScene();
            return true;
        }

        public bool RequestGiveUp()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay
                || _currentStagePlayPhase != DemoShellStagePlayPhaseId.Running)
                return false;

            EnterStageResult(DemoShellStageOutcomeId.Fail);
            return true;
        }

        public bool RequestForceClearReadyForTest()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay)
                return false;
            if (StageBridge == null)
                return false;
            if (!StageBridge.TryGetStageState(out var stageState))
                return false;
            if (stageState.State != RunDirectorStageStateId.Running)
                return false;

            return StageBridge.RequestForceClearReady();
        }

        public bool NotifyPreResultClearPresentationCompleted(bool skipped = false)
        {
            _ = skipped;
            if (_currentScreen != DemoShellScreenId.StagePlay
                || _currentStagePlayPhase != DemoShellStagePlayPhaseId.ClearPresentation
                || !_hasPendingClearResult
                || _clearPresentationCompletionSent
                || StageBridge == null)
            {
                return false;
            }

            bool clearOk = StageBridge.SetClearPresentationDone(true);
            bool confirmOk = StageBridge.RequestConfirm();
            if (!clearOk || !confirmOk)
                return false;

            _clearPresentationCompletionSent = true;
            _currentStagePlayPhase = DemoShellStagePlayPhaseId.AwaitingClearCompleted;
            return true;
        }

        public bool RequestQuit()
        {
            Application.Quit();
            return true;
        }

        private void TryBootFromSessionStagingIfPending()
        {
            if (!DemoShellSessionStaging.IsStartupPending)
                return;

            if (_currentScreen != DemoShellScreenId.Title && _currentScreen != DemoShellScreenId.Lobby)
                return;

            TryBootFromSessionStaging();
        }

        private bool TryBootFromSessionStaging()
        {
            if (DemoShellSessionStaging.TryConsume(out var request))
            {
                if (request.Screen == DemoShellScreenId.StagePlay)
                {
                    int clamped = Mathf.Clamp(request.StageIndex, 0, StageProfiles.Length - 1);
                    EnterStagePlay(clamped);
                    return true;
                }

                if (request.Screen == DemoShellScreenId.Lobby)
                {
                    TransitionTo(DemoShellScreenId.Lobby);
                    return true;
                }
            }

            return false;
        }

        private void TickStagePlayFlow()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay)
                return;

            if (StageBridge == null)
                return;

            if (_stageStartPending)
            {
                if (!TryGetStageProfile(_currentStageIndex, out var startProfile))
                    return;

                if (_stageTopologyApplyPending)
                {
                    bool applyOk = TopologyBridge != null && TopologyBridge.RequestTopologyApply(startProfile.StageId);
                    if (!applyOk)
                        return;

                    _stageTopologyApplyPending = false;
                }

                bool introOk = StageBridge.SetIntroPresentationDone(true);
                bool clearGateOk = StageBridge.SetClearPresentationDone(false);
                bool startOk = StageBridge.RequestStageStart();
                if (introOk && clearGateOk && startOk)
                    _stageStartPending = false;
            }

            if (!StageBridge.TryGetStageState(out var stageState))
                return;

            if (!_stageRunningObserved)
            {
                if (stageState.State == RunDirectorStageStateId.Running)
                {
                    _stageRunningObserved = true;
                    _currentStagePlayPhase = DemoShellStagePlayPhaseId.Running;
                }
                else
                    return;
            }

            if (stageState.State == RunDirectorStageStateId.Running)
            {
                if (_currentStagePlayPhase == DemoShellStagePlayPhaseId.Starting)
                    _currentStagePlayPhase = DemoShellStagePlayPhaseId.Running;
            }

            if (stageState.State == RunDirectorStageStateId.ClearReady)
            {
                BeginPreResultClearPresentation();
                return;
            }

            if (TryGetStageProfile(_currentStageIndex, out var profile))
            {
                float limit = Mathf.Max(0f, profile.StageTimeLimitSec);
                float elapsedSec = ReadCurrentStageGameplayElapsedSec();
                if (limit > 0f
                    && stageState.State == RunDirectorStageStateId.Running
                    && elapsedSec >= limit)
                {
                    EnterStageResult(DemoShellStageOutcomeId.Fail);
                }
            }
        }

        private void ProcessKeyboardFallback()
        {
            if (!EnableKeyboardFallback || _runtimeUiShellActive)
                return;

            switch (_currentScreen)
            {
                case DemoShellScreenId.Title:
                    if (Input.anyKeyDown)
                        RequestStartFromTitle();
                    break;

                case DemoShellScreenId.Lobby:
                    if (Input.GetKeyDown(KeyCode.Alpha1))
                        RequestSelectStageById(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha2))
                        RequestSelectStageById(2);
                    else if (Input.GetKeyDown(KeyCode.Alpha3))
                        RequestSelectStageById(3);
                    break;

                case DemoShellScreenId.StagePlay:
                    if (Input.GetKeyDown(KeyCode.G))
                        RequestGiveUp();
                    break;

                case DemoShellScreenId.StageResult:
                    if (Input.GetKeyDown(KeyCode.N))
                        RequestResultAction(DemoShellResultActionId.NextStage);
                    else if (Input.GetKeyDown(KeyCode.R))
                        RequestResultAction(DemoShellResultActionId.Retry);
                    else if (Input.GetKeyDown(KeyCode.L))
                        RequestResultAction(DemoShellResultActionId.ReturnToLobby);
                    break;

                case DemoShellScreenId.DemoComplete:
                    if (Input.GetKeyDown(KeyCode.R))
                        RequestRestartDemo();
                    else if (Input.GetKeyDown(KeyCode.L))
                        RequestReturnToLobbyFromComplete();
                    else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape))
                        RequestQuit();
                    break;
            }
        }

        private void OnStageRunCompleted()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay
                || _currentStagePlayPhase != DemoShellStagePlayPhaseId.AwaitingClearCompleted
                || !_hasPendingClearResult)
                return;

            EnterPreparedClearResult();
        }

        private void TryConsumeCompletedStateFallback()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay
                || _currentStagePlayPhase != DemoShellStagePlayPhaseId.AwaitingClearCompleted
                || !_hasPendingClearResult
                || StageBridge == null)
            {
                return;
            }

            if (!StageBridge.TryGetStageState(out var stageState))
                return;

            if (stageState.State != RunDirectorStageStateId.Completed)
                return;

            OnStageRunCompleted();
        }

        private void EnterStagePlay(int stageIndex)
        {
            if (!TryGetStageProfile(stageIndex, out var profile))
                return;

            EnsureTopologyBridgeReference();
            SyncTopologyBridgeStageCatalogReference();

            DemoShellSessionStaging.IncrementDialogueStageAttempt(profile.StageId);
            DemoShellSessionStaging.BeginDialogueStageRun(profile.StageId);
            _currentStageIndex = stageIndex;
            _stageStartPending = true;
            _stageTopologyApplyPending = !(TopologyBridge != null && TopologyBridge.RequestTopologyApply(profile.StageId));
            _currentStagePlayPhase = DemoShellStagePlayPhaseId.Starting;
            _stageRunningObserved = false;
            ResetPendingClearPresentationState();
            _currentStageOutcome = DemoShellStageOutcomeId.Clear;
            _hasCurrentStageResult = false;
            CaptureStageStartTotals();
            TransitionTo(DemoShellScreenId.StagePlay);

            if (LogTransitions)
                Debug.Log($"[DemoShellFlowController] Enter StagePlay stageId={profile.StageId}");
        }

        private void EnsureBridgeReference()
        {
            if (StageBridge != null)
                return;

            StageBridge = GetComponent<RunDirectorStageBridge>();
            if (StageBridge != null)
                return;

#if UNITY_2023_1_OR_NEWER
            StageBridge = FindFirstObjectByType<RunDirectorStageBridge>();
#else
            StageBridge = FindObjectOfType<RunDirectorStageBridge>();
#endif

            if (StageBridge == null && !_warnedNoBridge)
            {
                _warnedNoBridge = true;
                Debug.LogWarning("[DemoShellFlowController] RunDirectorStageBridge was not found in scene.");
            }
        }

        private void EnsureTopologyBridgeReference()
        {
            if (TopologyBridge != null)
                return;

            TopologyBridge = GetComponent<StageTopologyBridge>();
            if (TopologyBridge == null)
                TopologyBridge = gameObject.AddComponent<StageTopologyBridge>();

#if UNITY_2023_1_OR_NEWER
            if (TopologyBridge == null)
                TopologyBridge = FindFirstObjectByType<StageTopologyBridge>();
#else
            if (TopologyBridge == null)
                TopologyBridge = FindObjectOfType<StageTopologyBridge>();
#endif

            if (TopologyBridge == null && !_warnedNoTopologyBridge)
            {
                _warnedNoTopologyBridge = true;
                Debug.LogWarning("[DemoShellFlowController] StageTopologyBridge was not found in scene.");
            }
        }

        private void SyncTopologyBridgeStageCatalogReference()
        {
            if (TopologyBridge == null || StageCatalog == null)
                return;

            if (TopologyBridge.StageCatalog != StageCatalog)
                TopologyBridge.StageCatalog = StageCatalog;
        }

        private void EnsureBridgeSubscription()
        {
            if (_subscribedBridge == StageBridge)
                return;

            if (_subscribedBridge != null)
                _subscribedBridge.StageRunCompleted -= OnStageRunCompleted;

            _subscribedBridge = StageBridge;
            if (_subscribedBridge != null)
                _subscribedBridge.StageRunCompleted += OnStageRunCompleted;
        }

        private void EnsureRuntimeHudBridge()
        {
            if (_runtimeHudBridge == null)
                _runtimeHudBridge = GetComponent<PlayerRuntimeHudBridge>();
            if (_runtimeHudBridge == null)
                _runtimeHudBridge = gameObject.AddComponent<PlayerRuntimeHudBridge>();
            if (_runtimeHudBridge != null && _runtimeHudBridge.DemoShell == null)
                _runtimeHudBridge.DemoShell = this;
        }

        private void EnsureDemoAudioBridge()
        {
            if (_demoAudioBridge == null)
                _demoAudioBridge = GetComponent<DemoAudioBridge>();
            if (_demoAudioBridge == null)
                _demoAudioBridge = gameObject.AddComponent<DemoAudioBridge>();
            if (_demoAudioBridge != null && _demoAudioBridge.DemoShell == null)
                _demoAudioBridge.DemoShell = this;
        }

        private void DrawAudioOptionsSection()
        {
            GUILayout.Space(10f);
            GUILayout.Label("Audio");

            if (_demoAudioBridge == null)
            {
                GUILayout.Label("Audio bridge unavailable");
                return;
            }

            DrawVolumeSlider("Master", DemoAudioBusId.Master);
            DrawVolumeSlider("BGM", DemoAudioBusId.Bgm);
            DrawVolumeSlider("SFX", DemoAudioBusId.Sfx);
            DrawVolumeSlider("UI", DemoAudioBusId.Ui);
        }

        private void DrawVolumeSlider(string label, DemoAudioBusId bus)
        {
            float current = Mathf.Clamp01(_demoAudioBridge.GetBusVolume(bus));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {current:0.00}", GUILayout.Width(120f));
            float next = GUILayout.HorizontalSlider(current, 0f, 1f, GUILayout.Width(170f));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(next - current) > 0.001f)
                _demoAudioBridge.SetBusVolume(bus, next);
        }

        private bool CanRequestForceClearReady()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay || StageBridge == null)
                return false;
            if (!StageBridge.TryGetStageState(out var stageState))
                return false;

            return stageState.State == RunDirectorStageStateId.Running;
        }

        private void TransitionTo(DemoShellScreenId next)
        {
            if (_currentScreen == next)
                return;

            if (LogTransitions)
                Debug.Log($"[DemoShellFlowController] {_currentScreen} -> {next}");

            if (next != DemoShellScreenId.StagePlay)
                _currentStagePlayPhase = DemoShellStagePlayPhaseId.None;

            _currentScreen = next;
        }

        private bool TryGetStageProfile(int stageIndex, out DemoShellStageProfile profile)
        {
            profile = default;
            if (StageProfiles == null || stageIndex < 0 || stageIndex >= StageProfiles.Length)
                return false;

            profile = StageProfiles[stageIndex];
            return profile.StageId > 0;
        }

        private int ResolveStageIndexById(int stageId)
        {
            if (stageId <= 0 || StageProfiles == null)
                return -1;

            for (int i = 0; i < StageProfiles.Length; i++)
            {
                if (StageProfiles[i].StageId == stageId)
                    return i;
            }

            return -1;
        }

        private void ReloadActiveScene()
        {
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid())
                return;

            if (!string.IsNullOrEmpty(active.path))
            {
                SceneManager.LoadScene(active.path, LoadSceneMode.Single);
                return;
            }

            if (active.buildIndex >= 0)
                SceneManager.LoadScene(active.buildIndex, LoadSceneMode.Single);
        }

        private void EnsureStageProfiles()
        {
            if (TryLoadStageProfilesFromCatalog())
                return;

            if (StageProfiles == null || StageProfiles.Length == 0)
            {
                StageProfiles = new[]
                {
                    new DemoShellStageProfile { StageId = 1, DisplayName = "Stage 1", IsFinalStage = false, StageTimeLimitSec = DefaultStage1TimeLimitSec },
                    new DemoShellStageProfile { StageId = 2, DisplayName = "Stage 2", IsFinalStage = false, StageTimeLimitSec = DefaultStage2TimeLimitSec },
                    new DemoShellStageProfile { StageId = 3, DisplayName = "Stage 3", IsFinalStage = true, StageTimeLimitSec = DefaultStage3TimeLimitSec },
                };
                return;
            }

            for (int i = 0; i < StageProfiles.Length; i++)
            {
                var entry = StageProfiles[i];
                int fallbackStageId = i + 1;
                if (entry.StageId <= 0)
                    entry.StageId = fallbackStageId;
                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                    entry.DisplayName = $"Stage {entry.StageId}";
                if (entry.StageTimeLimitSec <= 0f)
                    entry.StageTimeLimitSec = ResolveDefaultStageTimeLimitSec(entry.StageId, i);
                StageProfiles[i] = entry;
            }
        }

        private bool TryLoadStageProfilesFromCatalog()
        {
            if (StageCatalog == null)
                return false;

            var entries = StageCatalog.Entries;
            if (entries == null || entries.Length <= 0)
                return false;

            var profiles = new List<DemoShellStageProfile>(entries.Length);
            var stageIds = new HashSet<int>();

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!entry.Enabled)
                    continue;

                var definition = entry.Definition;
                if (definition == null)
                {
                    WarnStageCatalogIssueOnce($"Entry has null Definition. index={i}");
                    continue;
                }

                int stageId = definition.StageId;
                if (stageId <= 0)
                {
                    WarnStageCatalogIssueOnce($"Definition.StageId must be >= 1. index={i}");
                    continue;
                }

                if (!stageIds.Add(stageId))
                {
                    WarnStageCatalogIssueOnce($"Duplicate enabled StageId detected in StageCatalog. stageId={stageId}");
                    continue;
                }

                if (entry.Layout == null)
                {
                    WarnStageCatalogIssueOnce($"Entry has null Layout. stageId={stageId}");
                }
                else if (entry.Layout.StageId != stageId)
                {
                    WarnStageCatalogIssueOnce($"Definition/Layout StageId mismatch. definition={stageId}, layout={entry.Layout.StageId}");
                }

                profiles.Add(new DemoShellStageProfile
                {
                    StageId = stageId,
                    DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                        ? $"Stage {stageId}"
                        : definition.DisplayName,
                    IsFinalStage = definition.IsFinalStage,
                    StageTimeLimitSec = definition.StageTimeLimitSec > 0f
                        ? definition.StageTimeLimitSec
                        : ResolveDefaultStageTimeLimitSec(stageId, profiles.Count),
                });
            }

            if (profiles.Count <= 0)
                return false;

            StageProfiles = profiles.ToArray();
            _warnedStageCatalogIssue = false;
            return true;
        }

        private void WarnStageCatalogIssueOnce(string message)
        {
            if (_warnedStageCatalogIssue)
                return;

            _warnedStageCatalogIssue = true;
            Debug.LogWarning($"[DemoShellFlowController] StageCatalog issue: {message}");
        }

        private void BeginPreResultClearPresentation()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay)
                return;
            if (_currentStagePlayPhase == DemoShellStagePlayPhaseId.ClearPresentation
                || _currentStagePlayPhase == DemoShellStagePlayPhaseId.AwaitingClearCompleted
                || _hasPendingClearResult)
            {
                return;
            }

            _pendingClearResult = BuildCurrentStageResult(DemoShellStageOutcomeId.Clear);
            _hasPendingClearResult = true;
            _clearPresentationCompletionSent = false;
            _currentStagePlayPhase = DemoShellStagePlayPhaseId.ClearPresentation;

            var callback = _preResultClearPresentationRequested;
            if (callback != null)
                callback.Invoke(_pendingClearResult);

            if (!_clearPresentationCompletionSent && callback == null)
                NotifyPreResultClearPresentationCompleted();
        }

        private void EnterPreparedClearResult()
        {
            if (!_hasPendingClearResult)
                return;

            EnterStageResult(_pendingClearResult);
        }

        private void EnterStageResult(DemoShellStageOutcomeId outcome)
        {
            if (_currentScreen != DemoShellScreenId.StagePlay)
                return;

            EnterStageResult(BuildCurrentStageResult(outcome));
        }

        private void EnterStageResult(DemoShellStageResultMetrics result)
        {
            if (_currentScreen != DemoShellScreenId.StagePlay)
                return;

            ResetPendingClearPresentationState();
            _currentStageOutcome = result.Outcome;
            _currentStageResult = result;
            _hasCurrentStageResult = true;
            TransitionTo(DemoShellScreenId.StageResult);

            if (result.Outcome == DemoShellStageOutcomeId.Clear)
                DemoShellSessionStaging.AccumulateSuccessfulStage(in _currentStageResult);

            RefreshSessionMetrics();
        }

        private void ResetPendingClearPresentationState()
        {
            _pendingClearResult = default;
            _hasPendingClearResult = false;
            _clearPresentationCompletionSent = false;
        }

        private DemoShellStageResultMetrics BuildCurrentStageResult(DemoShellStageOutcomeId outcome)
        {
            int stageId = TryGetStageProfile(_currentStageIndex, out var stageProfile)
                ? stageProfile.StageId
                : Mathf.Max(1, _currentStageIndex + 1);

            int totalCollect = _stageStartTotalCollectValue;
            int totalCleanup = _stageStartTotalCleanupValue;
            int totalHit = _stageStartTotalHitValue;
            if (TryGetSnapshotTotals(out var snapCollect, out var snapCleanup, out var snapHit))
            {
                totalCollect = snapCollect;
                totalCleanup = snapCleanup;
                totalHit = snapHit;
            }

            return new DemoShellStageResultMetrics
            {
                StageId = stageId,
                Outcome = outcome,
                ElapsedSec = ReadCurrentStageGameplayElapsedSec(),
                CollectValue = Mathf.Max(0, totalCollect - _stageStartTotalCollectValue),
                CleanupValue = Mathf.Max(0, totalCleanup - _stageStartTotalCleanupValue),
                HitValue = Mathf.Max(0, totalHit - _stageStartTotalHitValue),
            };
        }

        private void CaptureStageStartTotals()
        {
            if (TryGetSnapshotTotals(out var collect, out var cleanup, out var hit))
            {
                _stageStartTotalCollectValue = collect;
                _stageStartTotalCleanupValue = cleanup;
                _stageStartTotalHitValue = hit;
                return;
            }

            _stageStartTotalCollectValue = 0;
            _stageStartTotalCleanupValue = 0;
            _stageStartTotalHitValue = 0;
        }

        private bool TryGetSnapshotTotals(out int collect, out int cleanup, out int hit)
        {
            collect = 0;
            cleanup = 0;
            hit = 0;

            if (_runtimeHudBridge == null || !_runtimeHudBridge.TryGetLastSnapshot(out var snapshot))
                return false;

            collect = Mathf.Max(0, snapshot.TotalCollectValue);
            cleanup = Mathf.Max(0, snapshot.TotalCleanupValue);
            hit = Mathf.Max(0, snapshot.TotalHitValue);
            return true;
        }

        private void RefreshSessionMetrics()
        {
            _hasSessionMetrics = DemoShellSessionStaging.TryGetSessionMetrics(out _sessionMetrics);
        }

        private float ReadCurrentStageGameplayElapsedSec()
        {
            return TryGetStageGameplayElapsedSec(out float elapsedSec)
                ? Mathf.Max(0f, elapsedSec)
                : 0f;
        }

        private bool TryGetStageGameplayElapsedSec(out float elapsedSec)
        {
            elapsedSec = 0f;
            if (StageBridge == null || !StageBridge.TryGetGameplayClock(out var clock))
                return false;

            elapsedSec = clock.ElapsedSec;
            return true;
        }

        private static float ResolveDefaultStageTimeLimitSec(int stageId, int stageIndex)
        {
            return stageId switch
            {
                1 => DefaultStage1TimeLimitSec,
                2 => DefaultStage2TimeLimitSec,
                3 => DefaultStage3TimeLimitSec,
                _ => stageIndex switch
                {
                    0 => DefaultStage1TimeLimitSec,
                    1 => DefaultStage2TimeLimitSec,
                    2 => DefaultStage3TimeLimitSec,
                    _ => DefaultStage2TimeLimitSec,
                },
            };
        }

        public void SetRuntimeUiShellActive(bool active)
        {
            _runtimeUiShellActive = active;
        }

        private readonly struct GuiEnabledScope : System.IDisposable
        {
            private readonly bool _previous;

            public GuiEnabledScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = _previous;
            }
        }
    }
}















