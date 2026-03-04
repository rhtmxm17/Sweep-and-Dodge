using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Demo Shell 화면 전이를 소유한다.
    /// ECS Stage 상태 읽기/요청 쓰기는 RunDirectorStageBridge를 통해서만 수행한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunDirectorStageBridge))]
    public sealed class DemoShellFlowController : MonoBehaviour
    {
        [Header("References")]
        public RunDirectorStageBridge StageBridge;

        [Header("Overlay")]
        public bool ShowOverlay = true;
        public Rect OverlayRect = new Rect(12f, 12f, 420f, 300f);

        [Header("Input")]
        public bool EnableKeyboardFallback = true;

        [Header("Stage Profiles")]
        public DemoShellStageProfile[] StageProfiles =
        {
            new DemoShellStageProfile { StageId = 1, DisplayName = "Stage 1", IsFinalStage = false },
            new DemoShellStageProfile { StageId = 2, DisplayName = "Stage 2", IsFinalStage = false },
            new DemoShellStageProfile { StageId = 3, DisplayName = "Stage 3", IsFinalStage = true },
        };

        [Header("Debug")]
        public bool LogTransitions;

        private RunDirectorStageBridge _subscribedBridge;
        private DemoShellScreenId _currentScreen;
        private int _currentStageIndex = -1;
        private bool _stageStartPending;
        private bool _awaitingCompletedSignal;
        private DemoShellResultActionId _pendingResultAction;
        private bool _warnedNoBridge;

        public DemoShellScreenId CurrentScreen => _currentScreen;
        public int CurrentStageIndex => _currentStageIndex;
        public int CurrentStageId => TryGetStageProfile(_currentStageIndex, out var profile) ? profile.StageId : 0;

        private void Reset()
        {
            StageBridge = GetComponent<RunDirectorStageBridge>();
        }

        private void OnEnable()
        {
            EnsureStageProfiles();
            EnsureBridgeReference();
            EnsureBridgeSubscription();
            BootFromSessionStaging();
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
            EnsureBridgeSubscription();
            ProcessKeyboardFallback();
            TickStagePlayFlow();
        }

        private void OnGUI()
        {
            if (!ShowOverlay)
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
                    break;

                case DemoShellScreenId.StageResult:
                    GUILayout.Space(8f);
                    GUILayout.Label("Stage Result");
                    if (GUILayout.Button("Next Stage"))
                        RequestResultAction(DemoShellResultActionId.NextStage);
                    if (GUILayout.Button("Retry"))
                        RequestResultAction(DemoShellResultActionId.Retry);
                    if (GUILayout.Button("Return to Lobby"))
                        RequestResultAction(DemoShellResultActionId.ReturnToLobby);
                    break;

                case DemoShellScreenId.DemoComplete:
                    GUILayout.Space(8f);
                    GUILayout.Label("Demo Complete");
                    if (GUILayout.Button("Restart Demo"))
                        RequestRestartDemo();
                    if (GUILayout.Button("Return to Lobby"))
                        RequestReturnToLobbyFromComplete();
                    if (GUILayout.Button("Quit"))
                        RequestQuit();
                    break;
            }

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

            EnterStagePlay(stageIndex);
            return true;
        }

        public bool RequestResultAction(DemoShellResultActionId action)
        {
            if (_currentScreen != DemoShellScreenId.StageResult || _awaitingCompletedSignal || StageBridge == null)
                return false;

            if (!TryGetStageProfile(_currentStageIndex, out _))
                return false;

            bool clearOk = StageBridge.SetClearPresentationDone(true);
            bool confirmOk = StageBridge.RequestConfirm();
            if (!clearOk || !confirmOk)
                return false;

            _pendingResultAction = action;
            _awaitingCompletedSignal = true;
            return true;
        }

        public bool RequestRestartDemo()
        {
            if (_currentScreen != DemoShellScreenId.DemoComplete)
                return false;

            DemoShellSessionStaging.StageLobby();
            ReloadActiveScene();
            return true;
        }

        public bool RequestReturnToLobbyFromComplete()
        {
            if (_currentScreen != DemoShellScreenId.DemoComplete)
                return false;

            DemoShellSessionStaging.StageLobby();
            ReloadActiveScene();
            return true;
        }

        public bool RequestQuit()
        {
            Application.Quit();
            return true;
        }

        private void BootFromSessionStaging()
        {
            if (DemoShellSessionStaging.TryConsume(out var request))
            {
                if (request.Screen == DemoShellScreenId.StagePlay)
                {
                    int clamped = Mathf.Clamp(request.StageIndex, 0, StageProfiles.Length - 1);
                    EnterStagePlay(clamped);
                    return;
                }

                if (request.Screen == DemoShellScreenId.Lobby)
                {
                    TransitionTo(DemoShellScreenId.Lobby);
                    return;
                }
            }

            TransitionTo(DemoShellScreenId.Title);
        }

        private void TickStagePlayFlow()
        {
            if (_currentScreen != DemoShellScreenId.StagePlay || StageBridge == null)
                return;

            if (_stageStartPending)
            {
                bool introOk = StageBridge.SetIntroPresentationDone(true);
                bool clearGateOk = StageBridge.SetClearPresentationDone(false);
                bool startOk = StageBridge.RequestStageStart();
                if (introOk && clearGateOk && startOk)
                    _stageStartPending = false;
            }

            if (StageBridge.TryGetStageState(out var stageState)
                && stageState.State == RunDirectorStageStateId.ClearReady)
            {
                TransitionTo(DemoShellScreenId.StageResult);
                _awaitingCompletedSignal = false;
            }
        }

        private void ProcessKeyboardFallback()
        {
            if (!EnableKeyboardFallback)
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
            if (_currentScreen != DemoShellScreenId.StageResult || !_awaitingCompletedSignal)
                return;

            _awaitingCompletedSignal = false;
            switch (_pendingResultAction)
            {
                case DemoShellResultActionId.NextStage:
                {
                    if (!TryGetStageProfile(_currentStageIndex, out var profile))
                        return;

                    if (profile.IsFinalStage)
                    {
                        TransitionTo(DemoShellScreenId.DemoComplete);
                        return;
                    }

                    int nextStageIndex = _currentStageIndex + 1;
                    if (!TryGetStageProfile(nextStageIndex, out _))
                    {
                        TransitionTo(DemoShellScreenId.DemoComplete);
                        return;
                    }

                    DemoShellSessionStaging.StageStagePlay(nextStageIndex);
                    ReloadActiveScene();
                    return;
                }
                case DemoShellResultActionId.Retry:
                    DemoShellSessionStaging.StageStagePlay(_currentStageIndex);
                    ReloadActiveScene();
                    return;
                case DemoShellResultActionId.ReturnToLobby:
                    DemoShellSessionStaging.StageLobby();
                    ReloadActiveScene();
                    return;
            }
        }

        private void EnterStagePlay(int stageIndex)
        {
            if (!TryGetStageProfile(stageIndex, out var profile))
                return;

            _currentStageIndex = stageIndex;
            _stageStartPending = true;
            _awaitingCompletedSignal = false;
            _pendingResultAction = DemoShellResultActionId.NextStage;
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

        private void TransitionTo(DemoShellScreenId next)
        {
            if (_currentScreen == next)
                return;

            if (LogTransitions)
                Debug.Log($"[DemoShellFlowController] {_currentScreen} -> {next}");

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
            if (StageProfiles != null && StageProfiles.Length > 0)
                return;

            StageProfiles = new[]
            {
                new DemoShellStageProfile { StageId = 1, DisplayName = "Stage 1", IsFinalStage = false },
                new DemoShellStageProfile { StageId = 2, DisplayName = "Stage 2", IsFinalStage = false },
                new DemoShellStageProfile { StageId = 3, DisplayName = "Stage 3", IsFinalStage = true },
            };
        }
    }
}
