using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed partial class RuntimeUiRoot : MonoBehaviour
    {
        [Header("Scene References")]
        public DemoShellFlowController DemoShell;
        public DemoAudioBridge DemoAudio;
        public DemoShellPauseBridge PauseBridge;
        public PlayerRuntimeHudBridge RuntimeHudBridge;
        public DemoShellNotificationBridge NotificationBridge;
        public DemoShellHintBridge HintBridge;
        public DemoShellDialogueBridge DialogueBridge;
        public StagePresentationRuntimeController PresentationRuntimeController;

        [Header("Canvas")]
        public Canvas RootCanvas;
        public CanvasScaler RootCanvasScaler;
        public GraphicRaycaster RootGraphicRaycaster;
        public EventSystem EventSystem;
        public InputSystemUIInputModule UiInputModule;

        [Header("Layers")]
        public RectTransform ShellLayer;
        public RectTransform HudLayer;
        public RectTransform PresentationLayer;
        public RectTransform ModalLayer;
        public RectTransform FxLayer;

        [Header("Panels")]
        public GameObject TitlePanel;
        public GameObject LobbyPanel;
        public GameObject ResultPanel;
        public GameObject DemoCompletePanel;
        public GameObject StageHudPanel;
        public GameObject NotificationPanel;
        public GameObject HintPanel;
        public GameObject DialoguePanel;
        public GameObject PausePanel;
        public GameObject ConfirmDialogPanel;
        public GameObject SettingsPanel;

        [Header("Presenters")]
        public TitleScreenPresenter TitlePresenter;
        public LobbyScreenPresenter LobbyPresenter;
        public ResultPresenter ResultPresenter;
        public DemoCompletePresenter DemoCompletePresenter;
        public StageHudPresenter StageHudPresenter;
        public NotificationPresenter NotificationPresenter;
        public HintPresenter HintPresenter;
        public InWorldDialoguePresenter DialoguePresenter;
        public PausePresenter PausePresenter;
        public ConfirmDialogPresenter ConfirmDialogPresenter;
        public SettingsPresenter SettingsPresenter;

        [Header("Policy")]
        public bool AutoBuildHierarchy = true;
        public bool LogBindWarnings = true;

        private DemoShellScreenId _lastScreen;
        private bool _hasLastScreen;
        private bool _settingsOpen;
        private bool _settingsOpenedFromPause;
        private bool _confirmOpen;
        private bool _lastSettingsOpen;
        private bool _lastConfirmOpen;
        private Selectable _lastShellSelectable;
        private Selectable _lastPauseSelectable;
        private DemoShellFlowController _configuredShell;
        private DemoAudioBridge _configuredAudio;
        private DemoShellPauseBridge _configuredPauseBridge;
        private PlayerRuntimeHudBridge _configuredRuntimeHud;
        private DemoShellNotificationBridge _configuredNotificationBridge;
        private DemoShellHintBridge _configuredHintBridge;
        private DemoShellDialogueBridge _configuredDialogueBridge;
        private StagePresentationRuntimeController _configuredPresentationRuntimeController;
        private InWorldDialoguePresenter _configuredDialoguePresenter;
        private Canvas _configuredRootCanvas;
        private Action _cachedOpenSettingsAction;
        private Action _cachedCloseSettingsAction;
        private Action _cachedOpenSettingsFromPauseAction;
        private Action<DemoShellPauseActionId> _cachedOpenConfirmAction;
        private Action _cachedCloseConfirmAction;
#if UNITY_EDITOR
        private bool _editorEnsureHierarchyQueued;
#endif

        public bool IsSettingsOpen => _settingsOpen;
        public bool IsPauseOpen => PauseBridge != null && PauseBridge.IsPaused;
        public bool IsConfirmOpen => _confirmOpen;

        private void Reset()
        {
            EnsureHierarchy();
            AutoBindReferences();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (ShouldAutoAuthorInEditor())
                {
                    EnsureHierarchy();
                    AutoBindReferences();
                }

                return;
            }
#endif

            EnsureHierarchy();
            AutoBindReferences();
            ConfigurePresenters();

            if (Application.isPlaying && DemoShell != null)
                DemoShell.SetRuntimeUiShellActive(true);
            if (Application.isPlaying && RuntimeHudBridge != null)
                RuntimeHudBridge.SetRuntimeUiHudActive(true);

            ApplyShellState(force: true);
        }

        private void OnDisable()
        {
            if (Application.isPlaying && DemoShell != null)
                DemoShell.SetRuntimeUiShellActive(false);
            if (Application.isPlaying && RuntimeHudBridge != null)
                RuntimeHudBridge.SetRuntimeUiHudActive(false);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && ShouldAutoAuthorInEditor() && !_editorEnsureHierarchyQueued)
            {
                _editorEnsureHierarchyQueued = true;
                EditorApplication.delayCall += EnsureHierarchyInEditor;
            }
#endif
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            AutoBindRuntimeReferences();
            if (RuntimeHudBridge != null && !RuntimeHudBridge.RuntimeUiHudActive)
                RuntimeHudBridge.SetRuntimeUiHudActive(true);
            ConfigurePresenters();

            if (WasCancelPressedThisFrame())
            {
                if (IsConfirmOpen || IsSettingsOpen || IsPauseOpen)
                    CloseTopModal();
                else if (PauseBridge != null && PauseBridge.CanPause)
                    OpenPause();
            }

            ApplyShellState(force: false);
        }

#if UNITY_EDITOR
        private void EnsureHierarchyInEditor()
        {
            _editorEnsureHierarchyQueued = false;
            if (this == null || Application.isPlaying)
                return;
            if (!ShouldAutoAuthorInEditor())
                return;

            EnsureHierarchy();
            AutoBindReferences();
        }

        private bool ShouldAutoAuthorInEditor()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return false;

            if (EditorUtility.IsPersistent(gameObject))
                return true;

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage != null
                   && prefabStage.prefabContentsRoot != null
                   && gameObject.scene == prefabStage.scene;
#else
            return false;
#endif
        }
#endif

        public void OpenSettings()
        {
            if (_settingsOpen || _confirmOpen)
                return;

            _lastShellSelectable = ResolveCurrentShellDefaultSelectable();
            _settingsOpenedFromPause = false;
            _settingsOpen = true;
            ApplyShellState(force: true);
        }

        public void OpenSettingsFromPause()
        {
            if (_settingsOpen || !IsPauseOpen || _confirmOpen)
                return;

            _lastPauseSelectable = PausePresenter != null
                ? PausePresenter.ResolveSelectableForAction(DemoShellPauseActionId.OpenSettings)
                : null;
            _settingsOpenedFromPause = true;
            _settingsOpen = true;
            ApplyShellState(force: true);
        }

        public void CloseSettings()
        {
            if (!_settingsOpen)
                return;

            _settingsOpen = false;
            ApplyShellState(force: true);
        }

        public void OpenPause()
        {
            if (PauseBridge == null || _settingsOpen || _confirmOpen)
                return;

            if (!PauseBridge.RequestPause())
                return;

            ApplyShellState(force: true);
        }

        public void ClosePause()
        {
            if (PauseBridge == null || !PauseBridge.IsPaused)
                return;

            _confirmOpen = false;
            _settingsOpen = false;
            _settingsOpenedFromPause = false;
            PauseBridge.RequestResume();
            ApplyShellState(force: true);
        }

        public void OpenConfirm(DemoShellPauseActionId action)
        {
            if (!IsPauseOpen || _settingsOpen)
                return;

            _lastPauseSelectable = PausePresenter != null
                ? PausePresenter.ResolveSelectableForAction(action)
                : null;
            PauseBridge?.SetPendingAction(action);
            _confirmOpen = true;
            ApplyShellState(force: true);
        }

        public void CloseConfirm()
        {
            if (!_confirmOpen)
                return;

            _confirmOpen = false;
            ApplyShellState(force: true);
        }

        public void CloseTopModal()
        {
            if (_confirmOpen)
            {
                CloseConfirm();
                return;
            }

            if (_settingsOpen)
            {
                CloseSettings();
                return;
            }

            if (IsPauseOpen)
                ClosePause();
        }

        public bool IsShellPanelVisible(DemoShellScreenId screen)
        {
            return screen switch
            {
                DemoShellScreenId.Title => TitlePanel != null && TitlePanel.activeInHierarchy,
                DemoShellScreenId.Lobby => LobbyPanel != null && LobbyPanel.activeInHierarchy,
                DemoShellScreenId.StageResult => ResultPanel != null && ResultPanel.activeInHierarchy,
                DemoShellScreenId.DemoComplete => DemoCompletePanel != null && DemoCompletePanel.activeInHierarchy,
                _ => false,
            };
        }

        private void ConfigurePresenters()
        {
            _cachedOpenSettingsAction ??= OpenSettings;
            _cachedCloseSettingsAction ??= CloseSettings;
            _cachedOpenSettingsFromPauseAction ??= OpenSettingsFromPause;
            _cachedCloseConfirmAction ??= CloseConfirm;
            _cachedOpenConfirmAction ??= OpenConfirm;

            if (_configuredShell == DemoShell
                && _configuredAudio == DemoAudio
                && _configuredPauseBridge == PauseBridge
                && _configuredRuntimeHud == RuntimeHudBridge
                && _configuredNotificationBridge == NotificationBridge
                && _configuredHintBridge == HintBridge
                && _configuredDialogueBridge == DialogueBridge
                && _configuredPresentationRuntimeController == PresentationRuntimeController
                && _configuredDialoguePresenter == DialoguePresenter
                && _configuredRootCanvas == RootCanvas)
                return;

            TitlePresenter?.Configure(DemoShell, _cachedOpenSettingsAction);
            LobbyPresenter?.Configure(DemoShell, _cachedOpenSettingsAction);
            ResultPresenter?.Configure(DemoShell, _cachedOpenSettingsAction);
            DemoCompletePresenter?.Configure(DemoShell, _cachedOpenSettingsAction);
            StageHudPresenter?.Configure(DemoShell, RuntimeHudBridge);
            NotificationPresenter?.Configure(NotificationBridge);
            HintPresenter?.Configure(HintBridge);
            DialoguePresenter?.Configure(DialogueBridge, PresentationRuntimeController, RootCanvas);
            PausePresenter?.Configure(PauseBridge, _cachedOpenSettingsFromPauseAction, _cachedOpenConfirmAction);
            ConfirmDialogPresenter?.Configure(PauseBridge, _cachedCloseConfirmAction);
            SettingsPresenter?.Configure(DemoAudio, _cachedCloseSettingsAction);

            _configuredShell = DemoShell;
            _configuredAudio = DemoAudio;
            _configuredPauseBridge = PauseBridge;
            _configuredRuntimeHud = RuntimeHudBridge;
            _configuredNotificationBridge = NotificationBridge;
            _configuredHintBridge = HintBridge;
            _configuredDialogueBridge = DialogueBridge;
            _configuredPresentationRuntimeController = PresentationRuntimeController;
            _configuredDialoguePresenter = DialoguePresenter;
            _configuredRootCanvas = RootCanvas;
        }

        private void ApplyShellState(bool force)
        {
            if (DemoShell != null)
                DemoShell.SetRuntimeUiShellActive(true);

            bool hasShell = DemoShell != null;
            DemoShellScreenId screen = hasShell ? DemoShell.CurrentScreen : DemoShellScreenId.Title;
            bool stagePlay = hasShell && screen == DemoShellScreenId.StagePlay;

            if (!stagePlay && IsPauseOpen)
                PauseBridge?.RequestResume();

            if (!stagePlay)
            {
                _confirmOpen = false;
                if (_settingsOpenedFromPause)
                {
                    _settingsOpen = false;
                    _settingsOpenedFromPause = false;
                }
            }

            bool pauseOpen = stagePlay && IsPauseOpen;
            bool confirmOpen = pauseOpen && _confirmOpen;
            bool settingsOpen = _settingsOpen;

            bool showTitle = hasShell && screen == DemoShellScreenId.Title;
            bool showLobby = hasShell && screen == DemoShellScreenId.Lobby;
            bool showHud = stagePlay;
            bool showDialogue = DialogueBridge != null && DialogueBridge.CurrentPresentation.Visible;
            bool showResult = hasShell && screen == DemoShellScreenId.StageResult;
            bool showComplete = hasShell && screen == DemoShellScreenId.DemoComplete;

            SetActive(TitlePanel, showTitle);
            SetActive(LobbyPanel, showLobby);
            SetActive(StageHudPanel, showHud);
            SetActive(NotificationPanel, showHud && !showDialogue);
            SetActive(HintPanel, showHud && !showDialogue);
            SetActive(DialoguePanel, showDialogue);
            SetActive(ResultPanel, showResult);
            SetActive(DemoCompletePanel, showComplete);
            SetActive(PausePanel, pauseOpen && !settingsOpen && !confirmOpen);
            SetActive(SettingsPanel, settingsOpen && !confirmOpen);
            SetActive(ConfirmDialogPanel, confirmOpen);

            TitlePresenter?.RefreshPresentation();
            LobbyPresenter?.RefreshPresentation();
            if (showHud)
            {
                NotificationBridge?.RefreshPresentationState();
                HintBridge?.RefreshPresentationState();
                StageHudPresenter?.RefreshPresentation();
                NotificationPresenter?.RefreshPresentation();
                HintPresenter?.RefreshPresentation();
            }
            DialoguePresenter?.RefreshPresentation();
            if (showResult)
                ResultPresenter?.RefreshPresentation();
            if (showComplete)
                DemoCompletePresenter?.RefreshPresentation();
            if (pauseOpen)
                PausePresenter?.RefreshPresentation();
            if (confirmOpen)
                ConfirmDialogPresenter?.RefreshPresentation();
            if (settingsOpen)
                SettingsPresenter?.RefreshPresentation();

            bool stateChanged = force
                || !_hasLastScreen
                || _lastScreen != screen
                || _lastSettingsOpen != settingsOpen
                || _lastConfirmOpen != confirmOpen;
            if (!stateChanged)
                return;

            UpdateSelection();
            _lastScreen = screen;
            _hasLastScreen = true;
            _lastSettingsOpen = settingsOpen;
            _lastConfirmOpen = confirmOpen;
        }

        private void UpdateSelection()
        {
            Selectable target;
            if (_confirmOpen)
            {
                target = ConfirmDialogPresenter != null ? ConfirmDialogPresenter.DefaultSelectable : null;
            }
            else if (_settingsOpen)
            {
                target = SettingsPresenter != null ? SettingsPresenter.DefaultSelectable : null;
            }
            else if (IsPauseOpen
                     && _lastSettingsOpen
                     && _settingsOpenedFromPause
                     && _lastPauseSelectable != null
                     && _lastPauseSelectable.gameObject.activeInHierarchy)
            {
                target = _lastPauseSelectable;
            }
            else if (IsPauseOpen
                     && _lastConfirmOpen
                     && _lastPauseSelectable != null
                     && _lastPauseSelectable.gameObject.activeInHierarchy)
            {
                target = _lastPauseSelectable;
            }
            else if (IsPauseOpen
                     && EventSystem != null
                     && EventSystem.currentSelectedGameObject != null
                     && PausePanel != null
                     && EventSystem.currentSelectedGameObject.activeInHierarchy
                     && EventSystem.currentSelectedGameObject.transform.IsChildOf(PausePanel.transform))
            {
                target = EventSystem.currentSelectedGameObject.GetComponent<Selectable>();
            }
            else if (IsPauseOpen)
            {
                target = PausePresenter != null ? PausePresenter.DefaultSelectable : null;
            }
            else if (_lastSettingsOpen
                     && !_settingsOpenedFromPause
                     && _lastShellSelectable != null
                     && _lastShellSelectable.gameObject.activeInHierarchy)
            {
                target = _lastShellSelectable;
            }
            else
            {
                target = ResolveCurrentShellDefaultSelectable();
            }

            if (!IsPauseOpen && !_settingsOpen)
                _lastShellSelectable = target;
            else if (IsPauseOpen && !_confirmOpen && !_settingsOpen)
                _lastPauseSelectable = target;

            if (target == null || EventSystem == null)
                return;

            var next = target.gameObject;
            if (EventSystem.currentSelectedGameObject == next)
                return;

            EventSystem.SetSelectedGameObject(next);
        }

        private Selectable ResolveCurrentShellDefaultSelectable()
        {
            if (DemoShell == null)
                return null;

            return DemoShell.CurrentScreen switch
            {
                DemoShellScreenId.Title => TitlePresenter != null ? TitlePresenter.DefaultSelectable : null,
                DemoShellScreenId.Lobby => LobbyPresenter != null ? LobbyPresenter.DefaultSelectable : null,
                DemoShellScreenId.StageResult => ResultPresenter != null ? ResultPresenter.DefaultSelectable : null,
                DemoShellScreenId.DemoComplete => DemoCompletePresenter != null ? DemoCompletePresenter.DefaultSelectable : null,
                _ => null,
            };
        }

        private bool WasCancelPressedThisFrame()
        {
            if (UiInputModule == null || UiInputModule.cancel == null || UiInputModule.cancel.action == null)
                return false;

            return UiInputModule.cancel.action.WasPerformedThisFrame();
        }

        private void AutoBindRuntimeReferences()
        {
            DemoShell ??= FindFirst<DemoShellFlowController>();
            DemoAudio ??= FindFirst<DemoAudioBridge>();
            PauseBridge ??= FindFirst<DemoShellPauseBridge>();
            RuntimeHudBridge ??= FindFirst<PlayerRuntimeHudBridge>();
            NotificationBridge ??= FindFirst<DemoShellNotificationBridge>();
            HintBridge ??= FindFirst<DemoShellHintBridge>();
            DialogueBridge ??= FindFirst<DemoShellDialogueBridge>();
            PresentationRuntimeController ??= FindFirst<StagePresentationRuntimeController>();
            DialoguePresenter ??= FindFirst<InWorldDialoguePresenter>();

            if (DemoShell != null)
            {
                NotificationBridge ??= DemoShell.GetComponent<DemoShellNotificationBridge>();
                HintBridge ??= DemoShell.GetComponent<DemoShellHintBridge>();
                DialogueBridge ??= DemoShell.GetComponent<DemoShellDialogueBridge>();

                if (NotificationBridge == null)
                    NotificationBridge = DemoShell.gameObject.AddComponent<DemoShellNotificationBridge>();
                if (HintBridge == null)
                    HintBridge = DemoShell.gameObject.AddComponent<DemoShellHintBridge>();
                if (DialogueBridge == null)
                    DialogueBridge = DemoShell.gameObject.AddComponent<DemoShellDialogueBridge>();
            }
        }

        private void AutoBindReferences()
        {
            AutoBindRuntimeReferences();

            RootCanvas ??= GetComponent<Canvas>();
            RootCanvasScaler ??= GetComponent<CanvasScaler>();
            RootGraphicRaycaster ??= GetComponent<GraphicRaycaster>();
            EventSystem ??= GetComponentInChildren<EventSystem>(true);
            UiInputModule ??= GetComponentInChildren<InputSystemUIInputModule>(true);

            ShellLayer ??= FindDirectChildRect(transform, "ShellLayer");
            HudLayer ??= FindDirectChildRect(transform, "HudLayer");
            PresentationLayer ??= FindDirectChildRect(transform, "PresentationLayer");
            ModalLayer ??= FindDirectChildRect(transform, "ModalLayer");
            FxLayer ??= FindDirectChildRect(transform, "FxLayer");

            TitlePanel ??= FindDirectChild(ShellLayer, "TitlePanel");
            LobbyPanel ??= FindDirectChild(ShellLayer, "LobbyPanel");
            ResultPanel ??= FindDirectChild(ShellLayer, "ResultPanel");
            DemoCompletePanel ??= FindDirectChild(ShellLayer, "DemoCompletePanel");
            StageHudPanel ??= FindDirectChild(HudLayer, "StageHudPanel");
            NotificationPanel ??= FindDirectChild(HudLayer, "NotificationPanel");
            HintPanel ??= FindDirectChild(HudLayer, "HintPanel");
            DialoguePanel ??= FindDirectChild(PresentationLayer, "DialoguePanel");
            PausePanel ??= FindDirectChild(ModalLayer, "PausePanel");
            ConfirmDialogPanel ??= FindDirectChild(ModalLayer, "ConfirmDialogPanel");
            SettingsPanel ??= FindDirectChild(ModalLayer, "SettingsPanel");

            TitlePresenter ??= TitlePanel != null ? TitlePanel.GetComponent<TitleScreenPresenter>() : null;
            LobbyPresenter ??= LobbyPanel != null ? LobbyPanel.GetComponent<LobbyScreenPresenter>() : null;
            ResultPresenter ??= ResultPanel != null ? ResultPanel.GetComponent<ResultPresenter>() : null;
            DemoCompletePresenter ??= DemoCompletePanel != null ? DemoCompletePanel.GetComponent<DemoCompletePresenter>() : null;
            StageHudPresenter ??= StageHudPanel != null ? StageHudPanel.GetComponent<StageHudPresenter>() : null;
            NotificationPresenter ??= NotificationPanel != null ? NotificationPanel.GetComponent<NotificationPresenter>() : null;
            HintPresenter ??= HintPanel != null ? HintPanel.GetComponent<HintPresenter>() : null;
            DialoguePresenter ??= DialoguePanel != null ? DialoguePanel.GetComponent<InWorldDialoguePresenter>() : null;
            PausePresenter ??= PausePanel != null ? PausePanel.GetComponent<PausePresenter>() : null;
            ConfirmDialogPresenter ??= ConfirmDialogPanel != null ? ConfirmDialogPanel.GetComponent<ConfirmDialogPresenter>() : null;
            SettingsPresenter ??= SettingsPanel != null ? SettingsPanel.GetComponent<SettingsPresenter>() : null;
        }
    }
}

namespace SweepNDodge.DotsBullets
{
    public sealed partial class RuntimeUiRoot
    {
        [ContextMenu("Ensure Default Hierarchy")]
        public void EnsureHierarchy()
        {
            if (!AutoBuildHierarchy)
                return;

            EnsureCanvasSetup();
            ShellLayer = EnsureLayer(ShellLayer, "ShellLayer");
            HudLayer = EnsureLayer(HudLayer, "HudLayer");
            PresentationLayer = EnsureLayer(PresentationLayer, "PresentationLayer");
            ModalLayer = EnsureLayer(ModalLayer, "ModalLayer");
            FxLayer = EnsureLayer(FxLayer, "FxLayer");
            SetLayerOrder(ShellLayer, 0);
            SetLayerOrder(HudLayer, 1);
            SetLayerOrder(PresentationLayer, 2);
            SetLayerOrder(ModalLayer, 3);
            SetLayerOrder(FxLayer, 4);

            BuildTitlePanel();
            BuildLobbyPanel();
            BuildResultPanel();
            BuildDemoCompletePanel();
            BuildStageHudPanel();
            BuildNotificationPanel();
            BuildHintPanel();
            BuildDialoguePanel();
            BuildPausePanel();
            BuildConfirmDialogPanel();
            BuildSettingsPanel();
            NormalizeImageSprites(transform);

            AutoBindReferences();
        }

        private void EnsureCanvasSetup()
        {
            if (RootCanvas == null)
                RootCanvas = GetComponent<Canvas>();
            if (RootCanvas == null)
                RootCanvas = gameObject.AddComponent<Canvas>();
            RootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (RootCanvasScaler == null)
                RootCanvasScaler = GetComponent<CanvasScaler>();
            if (RootCanvasScaler == null)
                RootCanvasScaler = gameObject.AddComponent<CanvasScaler>();
            RootCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            RootCanvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            RootCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            RootCanvasScaler.matchWidthOrHeight = 0.5f;

            if (RootGraphicRaycaster == null)
                RootGraphicRaycaster = GetComponent<GraphicRaycaster>();
            if (RootGraphicRaycaster == null)
                RootGraphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            if (EventSystem == null)
            {
                var eventSystemGo = GetOrCreateChildGameObject(transform, "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                EventSystem = eventSystemGo.GetComponent<EventSystem>();
                UiInputModule = eventSystemGo.GetComponent<InputSystemUIInputModule>();
            }
            else
            {
                if (UiInputModule == null)
                    UiInputModule = EventSystem.GetComponent<InputSystemUIInputModule>();
                if (UiInputModule == null)
                    UiInputModule = EventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private RectTransform EnsureLayer(RectTransform existing, string name)
        {
            if (existing != null)
                return existing;

            var go = GetOrCreateChildGameObject(transform, name);
            var rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        private static void SetLayerOrder(RectTransform layer, int siblingIndex)
        {
            if (layer == null)
                return;

            layer.SetSiblingIndex(siblingIndex);
        }

        private void BuildTitlePanel()
        {
            var panelGo = EnsurePanel(ref TitlePanel, ShellLayer, "TitlePanel", new Color(0.05f, 0.06f, 0.09f, 0.92f));
            TitlePresenter ??= panelGo.GetComponent<TitleScreenPresenter>() ?? panelGo.AddComponent<TitleScreenPresenter>();
            if (TitlePresenter.TitleText != null)
                return;

            var content = CreateContentBox(panelGo.transform, "Content", new Vector2(680f, 420f));
            TitlePresenter.TitleText = CreateText(content, "Title", "DOTS Minigame", 52f, FontStyles.Bold, TextAlignmentOptions.Center);
            TitlePresenter.SubtitleText = CreateText(content, "Subtitle", "Bullet sweep / dodge demo", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            TitlePresenter.ControlHintText = CreateText(content, "Hint", "WASD Move  |  Mouse Aim  |  Left/Right Click Action", 20f, FontStyles.Normal, TextAlignmentOptions.Center);
            TitlePresenter.StartButton = CreateButton(content, "StartButton", "Start Demo");
            TitlePresenter.SettingsButton = CreateButton(content, "SettingsButton", "Settings");
            TitlePresenter.QuitButton = CreateButton(content, "QuitButton", "Quit");
        }

        private void BuildLobbyPanel()
        {
            var panelGo = EnsurePanel(ref LobbyPanel, ShellLayer, "LobbyPanel", new Color(0.05f, 0.06f, 0.09f, 0.92f));
            LobbyPresenter ??= panelGo.GetComponent<LobbyScreenPresenter>() ?? panelGo.AddComponent<LobbyScreenPresenter>();
            if (LobbyPresenter.StageButtonContainer != null)
                return;

            var content = CreateContentBox(panelGo.transform, "Content", new Vector2(760f, 520f));
            LobbyPresenter.HeaderText = CreateText(content, "Header", "Select Stage", 44f, FontStyles.Bold, TextAlignmentOptions.Center);
            LobbyPresenter.SubtitleText = CreateText(content, "Subtitle", "Pick a stage to start the run.", 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            var stageList = CreateVerticalList(content, "StageList");
            LobbyPresenter.StageButtonContainer = stageList;
            LobbyPresenter.StageButtonTemplate = CreateButton(stageList, "StageButtonTemplate", "Stage 1");
            LobbyPresenter.StageButtonTemplate.gameObject.SetActive(false);
            LobbyPresenter.SettingsButton = CreateButton(content, "SettingsButton", "Settings");
            LobbyPresenter.QuitButton = CreateButton(content, "QuitButton", "Quit");
        }

        private void BuildResultPanel()
        {
            var panelGo = EnsurePanel(ref ResultPanel, ShellLayer, "ResultPanel", new Color(0.05f, 0.06f, 0.09f, 0.92f));
            ResultPresenter ??= panelGo.GetComponent<ResultPresenter>() ?? panelGo.AddComponent<ResultPresenter>();
            if (ResultPresenter.OutcomeText != null)
                return;

            var content = CreateContentBox(panelGo.transform, "Content", new Vector2(720f, 560f));
            ResultPresenter.OutcomeText = CreateText(content, "Outcome", "Clear", 46f, FontStyles.Bold, TextAlignmentOptions.Center);
            ResultPresenter.StageText = CreateText(content, "Stage", "Stage 1", 28f, FontStyles.Normal, TextAlignmentOptions.Center);
            ResultPresenter.TimeText = CreateText(content, "Time", "Time  0.0s", 26f, FontStyles.Normal, TextAlignmentOptions.Center);
            ResultPresenter.CollectText = CreateText(content, "Collect", "Collect  0", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            ResultPresenter.CleanupText = CreateText(content, "Cleanup", "Cleanup  0", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            ResultPresenter.HitText = CreateText(content, "Hit", "Hit  0", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            ResultPresenter.NextStageButton = CreateButton(content, "NextStageButton", "Next Stage");
            ResultPresenter.RetryButton = CreateButton(content, "RetryButton", "Retry");
            ResultPresenter.ReturnToLobbyButton = CreateButton(content, "ReturnToLobbyButton", "Return to Lobby");
            ResultPresenter.SettingsButton = CreateButton(content, "SettingsButton", "Settings");
        }

        private void BuildDemoCompletePanel()
        {
            var panelGo = EnsurePanel(ref DemoCompletePanel, ShellLayer, "DemoCompletePanel", new Color(0.05f, 0.06f, 0.09f, 0.92f));
            DemoCompletePresenter ??= panelGo.GetComponent<DemoCompletePresenter>() ?? panelGo.AddComponent<DemoCompletePresenter>();
            if (DemoCompletePresenter.HeaderText != null)
                return;

            var content = CreateContentBox(panelGo.transform, "Content", new Vector2(760f, 580f));
            DemoCompletePresenter.HeaderText = CreateText(content, "Header", "Demo Complete", 48f, FontStyles.Bold, TextAlignmentOptions.Center);
            DemoCompletePresenter.ClearedStagesText = CreateText(content, "ClearedStages", "Cleared Stages  0", 26f, FontStyles.Normal, TextAlignmentOptions.Center);
            DemoCompletePresenter.TotalTimeText = CreateText(content, "TotalTime", "Total Time  0.0s", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            DemoCompletePresenter.TotalCollectText = CreateText(content, "TotalCollect", "Total Collect  0", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            DemoCompletePresenter.TotalCleanupText = CreateText(content, "TotalCleanup", "Total Cleanup  0", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            DemoCompletePresenter.TotalHitText = CreateText(content, "TotalHit", "Total Hit  0", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            DemoCompletePresenter.RestartDemoButton = CreateButton(content, "RestartDemoButton", "Restart Demo");
            DemoCompletePresenter.ReturnToLobbyButton = CreateButton(content, "ReturnToLobbyButton", "Return to Lobby");
            DemoCompletePresenter.QuitButton = CreateButton(content, "QuitButton", "Quit");
            DemoCompletePresenter.SettingsButton = CreateButton(content, "SettingsButton", "Settings");
        }

        private void BuildSettingsPanel()
        {
            var panelGo = EnsurePanel(ref SettingsPanel, ModalLayer, "SettingsPanel", new Color(0f, 0f, 0f, 0.65f));
            SettingsPresenter ??= panelGo.GetComponent<SettingsPresenter>() ?? panelGo.AddComponent<SettingsPresenter>();
            if (SettingsPresenter.HeaderText != null)
                return;

            var content = CreateContentBox(panelGo.transform, "Content", new Vector2(840f, 440f));
            SettingsPresenter.HeaderText = CreateText(content, "Header", "Settings", 40f, FontStyles.Bold, TextAlignmentOptions.Center);
            SettingsPresenter.Master = CreateAudioSliderRow(content, "MasterRow", "Master");
            SettingsPresenter.Bgm = CreateAudioSliderRow(content, "BgmRow", "BGM");
            SettingsPresenter.Sfx = CreateAudioSliderRow(content, "SfxRow", "SFX");
            SettingsPresenter.Ui = CreateAudioSliderRow(content, "UiRow", "UI");
            SettingsPresenter.CloseButton = CreateButton(content, "CloseButton", "Close");
        }

        private void BuildPausePanel()
        {
            var panelGo = EnsurePanel(ref PausePanel, ModalLayer, "PausePanel", new Color(0f, 0f, 0f, 0.70f));
            PausePresenter ??= panelGo.GetComponent<PausePresenter>() ?? panelGo.AddComponent<PausePresenter>();
            if (PausePresenter.HeaderText != null)
                return;

            var content = CreateContentBox(panelGo.transform, "Content", new Vector2(720f, 460f));
            PausePresenter.HeaderText = CreateText(content, "Header", "Paused", 42f, FontStyles.Bold, TextAlignmentOptions.Center);
            PausePresenter.ResumeButton = CreateButton(content, "ResumeButton", "Resume");
            PausePresenter.SettingsButton = CreateButton(content, "SettingsButton", "Settings");
            PausePresenter.RestartStageButton = CreateButton(content, "RestartStageButton", "Restart Stage");
            PausePresenter.ReturnToLobbyButton = CreateButton(content, "ReturnToLobbyButton", "Return to Lobby");
            PausePresenter.QuitButton = CreateButton(content, "QuitButton", "Quit");
        }

        private void BuildConfirmDialogPanel()
        {
            var panelGo = EnsurePanel(ref ConfirmDialogPanel, ModalLayer, "ConfirmDialogPanel", new Color(0f, 0f, 0f, 0.82f));
            ConfirmDialogPresenter ??= panelGo.GetComponent<ConfirmDialogPresenter>() ?? panelGo.AddComponent<ConfirmDialogPresenter>();
            if (ConfirmDialogPresenter.TitleText != null)
                return;

            var content = CreateContentBox(panelGo.transform, "Content", new Vector2(760f, 320f));
            ConfirmDialogPresenter.TitleText = CreateText(content, "Title", "Confirm Action?", 38f, FontStyles.Bold, TextAlignmentOptions.Center);
            ConfirmDialogPresenter.BodyText = CreateText(content, "Body", "This action cannot be undone.", 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            ConfirmDialogPresenter.ConfirmButton = CreateButton(content, "ConfirmButton", "Confirm");
            ConfirmDialogPresenter.CancelButton = CreateButton(content, "CancelButton", "Cancel");
        }
    }
}

namespace SweepNDodge.DotsBullets
{
    public sealed partial class RuntimeUiRoot
    {
        private static GameObject EnsurePanel(ref GameObject existing, RectTransform parent, string name, Color backgroundColor)
        {
            if (existing == null)
                existing = GetOrCreateChildGameObject(parent, name, typeof(Image));

            var rect = existing.GetComponent<RectTransform>();
            Stretch(rect);

            var image = existing.GetComponent<Image>() ?? existing.AddComponent<Image>();
            image.color = backgroundColor;
            ApplyDefaultImageSprite(image);
            return existing;
        }

        private static RectTransform CreateContentBox(Transform parent, string name, Vector2 size)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);
            ApplyDefaultImageSprite(image);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        private static RectTransform CreateVerticalList(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = go.GetComponent<RectTransform>();

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, fontSize + 18f);

            var layout = go.GetComponent<LayoutElement>();
            layout.minHeight = fontSize + 18f;
            layout.flexibleWidth = 1f;

            var textComponent = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            if (textComponent.font == null && TMP_Settings.defaultFontAsset != null)
                textComponent.font = TMP_Settings.defaultFontAsset;
            return textComponent;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(Button), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 56f);

            var layout = go.GetComponent<LayoutElement>();
            layout.minHeight = 56f;
            layout.flexibleWidth = 1f;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.20f, 0.30f, 0.46f, 1f);
            ApplyDefaultImageSprite(image);

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.20f, 0.30f, 0.46f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.40f, 0.60f, 1f);
            colors.pressedColor = new Color(0.15f, 0.22f, 0.34f, 1f);
            colors.selectedColor = new Color(0.30f, 0.44f, 0.66f, 1f);
            colors.disabledColor = new Color(0.20f, 0.20f, 0.20f, 0.75f);
            button.colors = colors;

            var labelGo = GetOrCreateChildGameObject(go.transform, "Label");
            var labelRect = labelGo.GetComponent<RectTransform>();
            Stretch(labelRect, 12f, 8f);

            var text = labelGo.GetComponent<TextMeshProUGUI>() ?? labelGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            if (text.font == null && TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;
            return button;
        }

        private static SettingsPresenter.AudioSliderRefs CreateAudioSliderRow(Transform parent, string name, string label)
        {
            var rowGo = GetOrCreateChildGameObject(parent, name, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 44f);

            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var rowElement = rowGo.GetComponent<LayoutElement>();
            rowElement.minHeight = 44f;
            rowElement.flexibleWidth = 1f;

            return new SettingsPresenter.AudioSliderRefs
            {
                Label = CreateFixedText(rowGo.transform, "Label", label, 22f, 180f, TextAlignmentOptions.Left),
                Slider = CreateSlider(rowGo.transform, "Slider"),
                ValueText = CreateFixedText(rowGo.transform, "Value", "1.00", 20f, 70f, TextAlignmentOptions.Right),
            };
        }

        private static TextMeshProUGUI CreateFixedText(Transform parent, string name, string text, float fontSize, float width, TextAlignmentOptions alignment)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, fontSize + 12f);

            var layout = go.GetComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = fontSize + 12f;

            var textComponent = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            if (textComponent.font == null && TMP_Settings.defaultFontAsset != null)
                textComponent.font = TMP_Settings.defaultFontAsset;
            return textComponent;
        }

        private static Slider CreateSlider(Transform parent, string name)
        {
            var sliderGo = GetOrCreateChildGameObject(parent, name, typeof(LayoutElement), typeof(Slider));
            var sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(340f, 24f);

            var layout = sliderGo.GetComponent<LayoutElement>();
            layout.minWidth = 340f;
            layout.preferredWidth = 340f;
            layout.minHeight = 24f;
            layout.flexibleWidth = 1f;

            var backgroundGo = GetOrCreateChildGameObject(sliderGo.transform, "Background", typeof(Image));
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            Stretch(backgroundRect);
            var backgroundImage = backgroundGo.GetComponent<Image>();
            backgroundImage.color = new Color(0.18f, 0.18f, 0.20f, 1f);
            ApplyDefaultImageSprite(backgroundImage);

            var fillAreaGo = GetOrCreateChildGameObject(sliderGo.transform, "Fill Area");
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(10f, 0f);
            fillAreaRect.offsetMax = new Vector2(-10f, 0f);

            var fillGo = GetOrCreateChildGameObject(fillAreaGo.transform, "Fill", typeof(Image));
            var fillRect = fillGo.GetComponent<RectTransform>();
            Stretch(fillRect);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = new Color(0.33f, 0.75f, 0.95f, 1f);
            ApplyDefaultImageSprite(fillImage);

            var handleSlideAreaGo = GetOrCreateChildGameObject(sliderGo.transform, "Handle Slide Area");
            var handleSlideAreaRect = handleSlideAreaGo.GetComponent<RectTransform>();
            Stretch(handleSlideAreaRect, 10f, 0f);

            var handleGo = GetOrCreateChildGameObject(handleSlideAreaGo.transform, "Handle", typeof(Image));
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18f, 28f);
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color(0.92f, 0.94f, 1f, 1f);
            ApplyDefaultImageSprite(handleImage);

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        private static RectTransform FindDirectChildRect(Transform parent, string name)
        {
            if (parent == null)
                return null;

            var child = parent.Find(name);
            return child as RectTransform;
        }

        private static GameObject FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            var child = parent.Find(name);
            return child != null ? child.gameObject : null;
        }

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        private static GameObject GetOrCreateChildGameObject(Transform parent, string name, params System.Type[] extraTypes)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            int extraCount = extraTypes != null ? extraTypes.Length : 0;
            var types = new System.Type[extraCount + 1];
            types[0] = typeof(RectTransform);
            for (int i = 0; i < extraCount; i++)
                types[i + 1] = extraTypes[i];

            var go = new GameObject(name, types);
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void NormalizeImageSprites(Transform root)
        {
            if (root == null)
                return;

            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
                ApplyDefaultImageSprite(images[i]);
        }

        private static void ApplyDefaultImageSprite(Image image)
        {
            if (image == null)
                return;

            var sprite = GetDefaultSprite();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }

        private static Sprite GetDefaultSprite()
        {
#if UNITY_EDITOR
            var sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite != null)
                return sprite;

            sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (sprite != null)
                return sprite;
#endif
            return null;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
