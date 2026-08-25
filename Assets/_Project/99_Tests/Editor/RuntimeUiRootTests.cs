using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets.Tests
{
    public class RuntimeUiRootTests
    {
        private const string RuntimeUiRootPrefabPath = "Assets/_Project/04_Prefabs/UI/RuntimeUiRoot.prefab";
        private static readonly Color FlatHazardActiveColor = new(1f, 0.72f, 0.18f, 1f);
        private static readonly Color FlatHazardInactiveColor = new(0.20f, 0.27f, 0.37f, 0.8f);

        private static readonly MethodInfo ConfigurePresentersMethod = typeof(RuntimeUiRoot)
            .GetMethod("ConfigurePresenters", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ApplyShellStateMethod = typeof(RuntimeUiRoot)
            .GetMethod("ApplyShellState", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ResolveCurrentShellDefaultSelectableMethod = typeof(RuntimeUiRoot)
            .GetMethod("ResolveCurrentShellDefaultSelectable", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo PauseBridgeUpdateMethod = typeof(DemoShellPauseBridge)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            ClearVolumePrefs();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
        }

        [TearDown]
        public void TearDown()
        {
            ClearVolumePrefs();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
        }

        [Test]
        public void OnEnable_DoesNotBuildHierarchyByDefault()
        {
            var go = new GameObject("RuntimeUiRoot_NoAutoBuild");
            try
            {
                go.SetActive(false);
                var root = go.AddComponent<RuntimeUiRoot>();
                root.LogBindWarnings = false;

                go.SetActive(true);

                Assert.That(root.RootCanvas, Is.Null);
                Assert.That(root.ShellLayer, Is.Null);
                Assert.That(root.HudLayer, Is.Null);
                Assert.That(root.TitlePanel, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuildDefaultHierarchyForTests_BuildsExplicitly()
        {
            var go = new GameObject("RuntimeUiRoot_ExplicitBuild");
            try
            {
                var root = go.AddComponent<RuntimeUiRoot>();
                root.LogBindWarnings = false;

                root.BuildDefaultHierarchyForTests();

                Assert.That(root.RootCanvas, Is.Not.Null);
                Assert.That(root.ShellLayer, Is.Not.Null);
                Assert.That(root.HudLayer, Is.Not.Null);
                Assert.That(root.TitlePanel, Is.Not.Null);
                Assert.That(root.SettingsPanel, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuntimePrefab_DefaultsToTechDemoFlat_AndKeepsLegacyVisualReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeUiRootPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            var presenter = prefab.GetComponentInChildren<StageHudPresenter>(includeInactive: true);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.VisualStyle, Is.EqualTo(StageHudVisualStyle.TechDemoFlat));
            Assert.That(presenter.ObjectiveSummaryBackgroundImage, Is.Not.Null);
            Assert.That(presenter.ObjectiveSummaryDecorationImage, Is.Not.Null);
            Assert.That(presenter.TimerBackgroundImage, Is.Not.Null);
            Assert.That(presenter.TimerDecorationImage, Is.Not.Null);
            Assert.That(presenter.PressureSourceBackgroundImage, Is.Not.Null);
            Assert.That(presenter.PressureSourceTrackImage, Is.Not.Null);
            Assert.That(presenter.PressureSourceMaskImage, Is.Not.Null);
            Assert.That(presenter.PressureSourceFillGraphicImage, Is.Not.Null);
            Assert.That(presenter.PressureSourceFillImage, Is.Not.Null);
            Assert.That(presenter.CarryTrackImage, Is.Not.Null);
            Assert.That(presenter.CarryMaskImage, Is.Not.Null);
            Assert.That(presenter.CarryFillGraphicImage, Is.Not.Null);
            Assert.That(presenter.CarryFillImage, Is.Not.Null);
            Assert.That(presenter.HazardStackFrameImage, Is.Not.Null);
            Assert.That(presenter.HazardStackActiveSprite, Is.Not.Null);
            Assert.That(presenter.HazardStackInactiveSprite, Is.Not.Null);
            Assert.That(presenter.ObjectiveSummaryBackgroundImage.enabled, Is.False,
                "The added flat background must remain disabled in the serialized Legacy baseline.");
        }

        [Test]
        public void StageHudVisualStyle_FlatLegacyFlat_RestoresSerializedVisualsWithoutAccumulation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeUiRootPrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);

            try
            {
                var presenter = instance.GetComponentInChildren<StageHudPresenter>(includeInactive: true);
                Assert.That(presenter, Is.Not.Null);

                Sprite legacyTimerDecorationSprite = presenter.TimerDecorationImage.sprite;
                Sprite legacyPressureTrackSprite = presenter.PressureSourceTrackImage.sprite;
                Sprite legacyPressureMaskSprite = presenter.PressureSourceMaskImage.sprite;
                Sprite legacyPressureFillSprite = presenter.PressureSourceFillGraphicImage.sprite;
                Sprite legacyCarryTrackSprite = presenter.CarryTrackImage.sprite;
                Sprite legacyCarryMaskSprite = presenter.CarryMaskImage.sprite;
                Sprite legacyCarryFillSprite = presenter.CarryFillGraphicImage.sprite;
                Sprite legacyHazardFrameSprite = presenter.HazardStackFrameImage.sprite;
                bool legacyClearedBackgroundEnabled = presenter.ObjectiveSummaryBackgroundImage.enabled;

                Assert.That(legacyPressureMaskSprite, Is.Not.Null);
                Assert.That(legacyCarryMaskSprite, Is.Not.Null);

                presenter.SetVisualStyle(StageHudVisualStyle.TechDemoFlat);
                Assert.That(presenter.ObjectiveSummaryBackgroundImage.enabled, Is.True);
                Assert.That(presenter.ObjectiveSummaryBackgroundImage.sprite, Is.Null);
                Assert.That(presenter.TimerDecorationImage.enabled, Is.False);
                Assert.That(presenter.PressureSourceTrackImage.sprite, Is.Null);
                Assert.That(presenter.PressureSourceMaskImage.sprite, Is.SameAs(legacyPressureMaskSprite));
                Assert.That(presenter.PressureSourceFillGraphicImage.sprite, Is.Null);
                Assert.That(presenter.CarryTrackImage.sprite, Is.Null);
                Assert.That(presenter.CarryMaskImage.sprite, Is.SameAs(legacyCarryMaskSprite));
                Assert.That(presenter.CarryFillGraphicImage.sprite, Is.Null);
                Assert.That(presenter.HazardStackFrameImage.sprite, Is.Null);

                presenter.SetVisualStyle(StageHudVisualStyle.LegacyIllustrated);
                Assert.That(presenter.ObjectiveSummaryBackgroundImage.enabled, Is.EqualTo(legacyClearedBackgroundEnabled));
                Assert.That(presenter.TimerDecorationImage.enabled, Is.True);
                Assert.That(presenter.TimerDecorationImage.sprite, Is.SameAs(legacyTimerDecorationSprite));
                Assert.That(presenter.PressureSourceTrackImage.sprite, Is.SameAs(legacyPressureTrackSprite));
                Assert.That(presenter.PressureSourceMaskImage.sprite, Is.SameAs(legacyPressureMaskSprite));
                Assert.That(presenter.PressureSourceFillGraphicImage.sprite, Is.SameAs(legacyPressureFillSprite));
                Assert.That(presenter.CarryTrackImage.sprite, Is.SameAs(legacyCarryTrackSprite));
                Assert.That(presenter.CarryMaskImage.sprite, Is.SameAs(legacyCarryMaskSprite));
                Assert.That(presenter.CarryFillGraphicImage.sprite, Is.SameAs(legacyCarryFillSprite));
                Assert.That(presenter.HazardStackFrameImage.sprite, Is.SameAs(legacyHazardFrameSprite));

                presenter.SetVisualStyle(StageHudVisualStyle.TechDemoFlat);
                presenter.SetVisualStyle(StageHudVisualStyle.LegacyIllustrated);
                Assert.That(presenter.TimerDecorationImage.sprite, Is.SameAs(legacyTimerDecorationSprite));
                Assert.That(presenter.PressureSourceTrackImage.sprite, Is.SameAs(legacyPressureTrackSprite));
                Assert.That(presenter.PressureSourceMaskImage.sprite, Is.SameAs(legacyPressureMaskSprite));
                Assert.That(presenter.PressureSourceFillGraphicImage.sprite, Is.SameAs(legacyPressureFillSprite));
                Assert.That(presenter.CarryTrackImage.sprite, Is.SameAs(legacyCarryTrackSprite));
                Assert.That(presenter.CarryMaskImage.sprite, Is.SameAs(legacyCarryMaskSprite));
                Assert.That(presenter.CarryFillGraphicImage.sprite, Is.SameAs(legacyCarryFillSprite));
                Assert.That(presenter.HazardStackFrameImage.sprite, Is.SameAs(legacyHazardFrameSprite));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ApplyShellState_MapsPanelsAndDefaultSelectables_PerScreen()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.Title);
            InvokeConfigurePresenters(context.Root);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.IsShellPanelVisible(DemoShellScreenId.Title), Is.True);
            Assert.That(context.Root.IsShellPanelVisible(DemoShellScreenId.Lobby), Is.False);
            Assert.That(ResolveCurrentDefaultSelectable(context.Root), Is.EqualTo(context.Root.TitlePresenter.StartButton));

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.Lobby);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.IsShellPanelVisible(DemoShellScreenId.Lobby), Is.True);
            Assert.That(context.Root.LobbyPresenter.DefaultSelectable, Is.Not.Null);
            Assert.That(ResolveCurrentDefaultSelectable(context.Root), Is.EqualTo(context.Root.LobbyPresenter.DefaultSelectable));

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StageResult);
            SetPrivateField(context.Shell, "_currentStageOutcome", DemoShellStageOutcomeId.Clear);
            SetPrivateField(context.Shell, "_currentStageResult", new DemoShellStageResultMetrics
            {
                StageId = 2,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 42.5f,
                CollectValue = 12,
                CleanupValue = 8,
                HitValue = 1,
            });
            SetPrivateField(context.Shell, "_hasCurrentStageResult", true);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.IsShellPanelVisible(DemoShellScreenId.StageResult), Is.True);
            Assert.That(context.Root.ResultPresenter.NextStageButton.gameObject.activeSelf, Is.True);
            Assert.That(ResolveCurrentDefaultSelectable(context.Root), Is.EqualTo(context.Root.ResultPresenter.NextStageButton));

            SetPrivateField(context.Shell, "_currentStageOutcome", DemoShellStageOutcomeId.Fail);
            SetPrivateField(context.Shell, "_currentStageResult", new DemoShellStageResultMetrics
            {
                StageId = 2,
                Outcome = DemoShellStageOutcomeId.Fail,
                ElapsedSec = 55f,
                CollectValue = 4,
                CleanupValue = 3,
                HitValue = 7,
            });
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.ResultPresenter.NextStageButton.gameObject.activeSelf, Is.False);
            Assert.That(ResolveCurrentDefaultSelectable(context.Root), Is.EqualTo(context.Root.ResultPresenter.RetryButton));

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.DemoComplete);
            SetPrivateField(context.Shell, "_sessionMetrics", new DemoShellSessionMetrics
            {
                ClearedStageCount = 3,
                TotalElapsedSec = 123.4f,
                TotalCollectValue = 31,
                TotalCleanupValue = 22,
                TotalHitValue = 5,
            });
            SetPrivateField(context.Shell, "_hasSessionMetrics", true);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.IsShellPanelVisible(DemoShellScreenId.DemoComplete), Is.True);
            Assert.That(ResolveCurrentDefaultSelectable(context.Root), Is.EqualTo(context.Root.DemoCompletePresenter.RestartDemoButton));
        }

        [Test]
        public void LobbyPresenter_GeneratesButtons_FromStageProfiles()
        {
            using var context = CreateContext();

            context.Shell.StageProfiles = new[]
            {
                new DemoShellStageProfile { StageId = 1, DisplayName = "Alpha", StageTimeLimitSec = 90f },
                new DemoShellStageProfile { StageId = 2, DisplayName = "Beta", StageTimeLimitSec = 120f },
                new DemoShellStageProfile { StageId = 7, DisplayName = "Gamma", IsFinalStage = true, StageTimeLimitSec = 180f },
            };

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.Lobby);
            InvokeConfigurePresenters(context.Root);
            InvokeApplyShellState(context.Root, force: true);

            var buttons = context.Root.LobbyPresenter.StageButtonContainer.GetComponentsInChildren<Button>(includeInactive: true);
            Assert.That(buttons.Length, Is.EqualTo(4), "Template + 3 generated stage buttons are expected.");

            var labels = context.Root.LobbyPresenter.StageButtonContainer.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            Assert.That(ContainsText(labels, "1. Alpha"), Is.True);
            Assert.That(ContainsText(labels, "2. Beta"), Is.True);
            Assert.That(ContainsText(labels, "7. Gamma"), Is.True);
            Assert.That(context.Root.LobbyPresenter.DefaultSelectable, Is.Not.Null);
            Assert.That(context.Root.LobbyPresenter.DefaultSelectable.name, Is.EqualTo("StageButton_1"));
        }

        [Test]
        public void SettingsPresenter_ReflectsAndUpdatesAudioVolumes()
        {
            using var context = CreateContext();

            context.Audio.SetBusVolume(DemoAudioBusId.Master, 0.85f);
            context.Audio.SetBusVolume(DemoAudioBusId.Bgm, 0.35f);
            context.Audio.SetBusVolume(DemoAudioBusId.Sfx, 0.65f);
            context.Audio.SetBusVolume(DemoAudioBusId.Ui, 0.20f);

            InvokeConfigurePresenters(context.Root);
            context.Root.SettingsPresenter.RefreshPresentation();

            Assert.That(context.Root.SettingsPresenter.Master.Slider.value, Is.EqualTo(0.85f).Within(1e-4f));
            Assert.That(context.Root.SettingsPresenter.Bgm.Slider.value, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(context.Root.SettingsPresenter.Sfx.Slider.value, Is.EqualTo(0.65f).Within(1e-4f));
            Assert.That(context.Root.SettingsPresenter.Ui.Slider.value, Is.EqualTo(0.20f).Within(1e-4f));
            Assert.That(context.Root.SettingsPresenter.Bgm.ValueText.text, Is.EqualTo("0.35"));

            context.Root.SettingsPresenter.Bgm.Slider.value = 0.58f;
            context.Root.SettingsPresenter.Ui.Slider.value = 0.42f;

            Assert.That(context.Audio.GetBusVolume(DemoAudioBusId.Bgm), Is.EqualTo(0.58f).Within(1e-4f));
            Assert.That(context.Audio.GetBusVolume(DemoAudioBusId.Ui), Is.EqualTo(0.42f).Within(1e-4f));
            Assert.That(context.Root.SettingsPresenter.Ui.ValueText.text, Is.EqualTo("0.42"));
        }

        [Test]
        public void ModalPanels_ApplyPriority_AndRestorePauseSelection()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            InvokeConfigurePresenters(context.Root);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.PauseBridge.RequestPause(), Is.True);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.IsPauseOpen, Is.True);
            Assert.That(context.Root.PausePanel.activeSelf, Is.True);
            Assert.That(context.Root.SettingsPanel.activeSelf, Is.False);
            Assert.That(context.Root.ConfirmDialogPanel.activeSelf, Is.False);
            Assert.That(context.Root.PausePresenter.DefaultSelectable, Is.EqualTo(context.Root.PausePresenter.ResumeButton));

            context.Root.OpenSettingsFromPause();
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.SettingsPanel.activeSelf, Is.True);
            Assert.That(context.Root.PausePanel.activeSelf, Is.False);

            context.Root.CloseSettings();
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.PausePanel.activeSelf, Is.True);
            Assert.That(context.Root.EventSystem.currentSelectedGameObject, Is.EqualTo(context.Root.PausePresenter.SettingsButton.gameObject));

            context.Root.OpenConfirm(DemoShellPauseActionId.RestartStage);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.IsConfirmOpen, Is.True);
            Assert.That(context.Root.ConfirmDialogPanel.activeSelf, Is.True);
            Assert.That(context.Root.SettingsPanel.activeSelf, Is.False);
            Assert.That(context.Root.PausePanel.activeSelf, Is.False);
            Assert.That(context.Root.ConfirmDialogPresenter.TitleText.text, Is.EqualTo("Restart Stage?"));
            Assert.That(context.Root.ConfirmDialogPresenter.DefaultSelectable, Is.EqualTo(context.Root.ConfirmDialogPresenter.CancelButton));

            context.Root.CloseConfirm();
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.IsConfirmOpen, Is.False);
            Assert.That(context.Root.PausePanel.activeSelf, Is.True);
            Assert.That(context.Root.EventSystem.currentSelectedGameObject, Is.EqualTo(context.Root.PausePresenter.RestartStageButton.gameObject));
        }

        [Test]
        public void PauseBridge_CanPauseOnlyDuringStagePlay_AndRoutesCommands()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.Lobby);
            Assert.That(context.PauseBridge.CanPause, Is.False);
            Assert.That(context.PauseBridge.RequestPause(), Is.False);

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 1);
            Assert.That(context.PauseBridge.CanPause, Is.True);
            Assert.That(context.PauseBridge.RequestPause(), Is.True);
            Assert.That(context.PauseBridge.IsPaused, Is.True);

            Assert.That(context.PauseBridge.RequestConfirmedAction(DemoShellPauseActionId.Resume), Is.True);
            Assert.That(context.PauseBridge.IsPaused, Is.False);

            context.PauseBridge.RequestPause();
            Assert.That(context.PauseBridge.RequestConfirmedAction(DemoShellPauseActionId.RestartStage), Is.True);
            Assert.That(DemoShellSessionStaging.TryConsume(out var restartRequest), Is.True);
            Assert.That(restartRequest.Screen, Is.EqualTo(DemoShellScreenId.StagePlay));
            Assert.That(restartRequest.StageIndex, Is.EqualTo(1));

            DemoShellSessionStaging.ResetSessionMetrics();
            context.PauseBridge.RequestPause();
            Assert.That(context.PauseBridge.RequestConfirmedAction(DemoShellPauseActionId.ReturnToLobby), Is.True);
            Assert.That(DemoShellSessionStaging.TryConsume(out var lobbyRequest), Is.True);
            Assert.That(lobbyRequest.Screen, Is.EqualTo(DemoShellScreenId.Lobby));
        }

        [Test]
        public void PauseBridge_CanPauseFalse_WhenPauseMenuBlockedBySnapshot()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 1);

            GameplayPauseHandle handle = context.PauseController.Acquire(
                GameplayPauseReasonId.DialogueGate,
                GameplayPauseFlags.PauseSimulation
                | GameplayPauseFlags.BlockGameplayInput
                | GameplayPauseFlags.ExclusivePresentationInput
                | GameplayPauseFlags.BlockPauseMenuOpen);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(context.PauseBridge.CanPause, Is.False);
            Assert.That(context.PauseBridge.RequestPause(), Is.False);
        }

        [Test]
        public void PauseBridge_AutoReleasesPauseMenuHandle_WhenPauseBecomesBlocked()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 1);

            Assert.That(context.PauseBridge.RequestPause(), Is.True);
            var pauseMenuHandle = context.PauseController.CurrentSnapshot.ReasonMask;
            Assert.That((pauseMenuHandle & (1u << (int)GameplayPauseReasonId.PauseMenu)) != 0, Is.True);

            GameplayPauseHandle dialogueHandle = context.PauseController.Acquire(
                GameplayPauseReasonId.DialogueGate,
                GameplayPauseFlags.PauseSimulation
                | GameplayPauseFlags.BlockGameplayInput
                | GameplayPauseFlags.ExclusivePresentationInput
                | GameplayPauseFlags.BlockPauseMenuOpen);

            Assert.That(dialogueHandle.IsValid, Is.True);
            InvokePauseBridgeUpdate(context.PauseBridge);

            Assert.That(context.PauseBridge.IsPaused, Is.False);
            Assert.That(context.PauseController.CurrentSnapshot.IsPauseMenuOpenBlocked, Is.True);
            Assert.That(
                context.PauseController.CurrentSnapshot.ReasonMask,
                Is.EqualTo(1u << (int)GameplayPauseReasonId.DialogueGate));
        }

        [Test]
        public void PauseBridge_AutoClosesPause_WhenDialogueGateBlocksPauseMenu()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 1);
            InvokeConfigurePresenters(context.Root);

            Assert.That(context.PauseBridge.RequestPause(), Is.True);
            InvokeApplyShellState(context.Root, force: true);
            Assert.That(context.Root.PausePanel.activeSelf, Is.True);

            GameplayPauseHandle dialogueHandle = context.PauseController.Acquire(
                GameplayPauseReasonId.DialogueGate,
                GameplayPauseFlags.PauseSimulation
                | GameplayPauseFlags.BlockGameplayInput
                | GameplayPauseFlags.ExclusivePresentationInput
                | GameplayPauseFlags.BlockPauseMenuOpen);

            Assert.That(dialogueHandle.IsValid, Is.True);
            InvokePauseBridgeUpdate(context.PauseBridge);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.PauseBridge.IsPaused, Is.False);
            Assert.That(context.Root.PausePanel.activeSelf, Is.False);
            Assert.That(
                context.PauseController.CurrentSnapshot.ReasonMask,
                Is.EqualTo(1u << (int)GameplayPauseReasonId.DialogueGate));
        }

        [Test]
        public void DemoShellSessionStaging_HintSessionAndStageSeen_ArePersistedSeparately()
        {
            DemoShellSessionStaging.ResetHintSessionState();

            Assert.That(DemoShellSessionStaging.HasSessionSeenHint(HintId.FirstHitAvoidHazards), Is.False);
            DemoShellSessionStaging.MarkSessionSeenHint(HintId.FirstHitAvoidHazards);
            DemoShellSessionStaging.SetActiveStageSeen(2, HintResolver.MarkSeen(0UL, HintId.CarryFullGoToDeposit));

            Assert.That(DemoShellSessionStaging.HasSessionSeenHint(HintId.FirstHitAvoidHazards), Is.True);
            Assert.That(DemoShellSessionStaging.TryGetActiveStageSeen(2, out ulong stageSeenMask), Is.True);
            Assert.That(HintResolver.HasSeen(stageSeenMask, HintId.CarryFullGoToDeposit), Is.True);

            DemoShellSessionStaging.ClearActiveStageSeen();
            Assert.That(DemoShellSessionStaging.HasSessionSeenHint(HintId.FirstHitAvoidHazards), Is.True);
            Assert.That(DemoShellSessionStaging.TryGetActiveStageSeen(2, out _), Is.False);

            DemoShellSessionStaging.ResetHintSessionState();
            Assert.That(DemoShellSessionStaging.HasSessionSeenHint(HintId.FirstHitAvoidHazards), Is.False);
        }

        [Test]
        public void HudPanels_ShowOnlyDuringStagePlay_AndReflectSnapshotValues()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 0);
            SetPrivateField(context.Hud, "_hasSnapshot", true);
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 5,
                CarryCapacity = 10,
                HazardStack = 0,
                HazardStackMax = 5,
                HazardRiskMultiplier = 1f,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                PressureSourceStableId = 1002u,
                PressureSourceCollected = 6,
                PressureSourceThresholdWeakened = 4,
                PressureSourceThresholdDepleted = 8,
                PressureSourceProgress01 = 0.75f,
                StageStateElapsedSec = 50f,
                GameplayElapsedSec = 50f,
            });
            SetPrivateField(context.Hud, "_lastFeedbackSnapshot", default(PlayerUiFeedbackPresentationSnapshotComponent));
            SetPrivateField(context.Hud, "_feedbackLine", string.Empty);
            context.Hud.SetRuntimeUiHudActive(true);

            InvokeConfigurePresenters(context.Root);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPanel.activeSelf, Is.True);
            Assert.That(context.Root.NotificationPanel.activeSelf, Is.True);
            Assert.That(context.Root.HintPanel.activeSelf, Is.True);
            Assert.That(context.Hud.RuntimeUiHudActive, Is.True);
            Assert.That(context.Root.StageHudPresenter.ObjectiveSummaryText.text, Is.EqualTo("1 / 3"));
            Assert.That(context.Root.StageHudPresenter.ObjectiveDetailText.text, Is.EqualTo("Pressure Source #1002"));
            Assert.That(context.Root.StageHudPresenter.PressureSourceProgressRoot.activeSelf, Is.True);
            Assert.That(context.Root.StageHudPresenter.PressureSourceValueText.text, Is.EqualTo("6 / 8"));
            Assert.That(context.Root.StageHudPresenter.PressureSourceFillImage.fillAmount, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(context.Root.StageHudPresenter.PressureSourceWeakThresholdMarker.gameObject.activeSelf, Is.True);
            Assert.That(context.Root.StageHudPresenter.PressureSourceWeakThresholdMarker.anchorMin.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(context.Root.StageHudPresenter.CarryValueText, Is.Null);
            Assert.That(context.Root.StageHudPresenter.HazardStackLabel, Is.Null);
            Assert.That(context.Root.StageHudPresenter.CarryFillImage.fillAmount, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(context.Root.StageHudPresenter.CarryFillImage.fillMethod, Is.EqualTo(Image.FillMethod.Vertical));
            Assert.That(context.Root.StageHudPresenter.HazardStackRoot.activeSelf, Is.True);
            Assert.That(context.Root.StageHudPresenter.HazardStackSegmentImages, Has.Length.EqualTo(5));
            Assert.That(CountHighlightedHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(0));
            Assert.That(CountInactiveHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(5));
            AssertHazardSegmentOrder(context.Root.StageHudPresenter, activeCount: 0);
            AssertHazardStackFrameHeight(context.Root.StageHudPresenter, maxSegments: 5);
            AssertHazardSegmentDisplayLayout(context.Root.StageHudPresenter, width: 20.5f, height: 24f);
            Assert.That(context.Root.StageHudPresenter.RiskMultiplierText.text, Is.EqualTo("x1.00"));
            Assert.That(HazardStackDisplaysNoMaxText(context.Root.StageHudPresenter), Is.True);
            Assert.That(context.Root.StageHudPresenter.TimerValueText.text, Is.EqualTo("70.0s"));
            Assert.That(context.Root.NotificationPresenter.NotificationRoot.activeSelf, Is.False);
            Assert.That(context.Root.HintPresenter.HintRoot.activeSelf, Is.True);
            Assert.That(context.Root.HintPresenter.HintText.text, Is.EqualTo("Collect trash from active sources."));

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.Title);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPanel.activeSelf, Is.False);
            Assert.That(context.Root.NotificationPanel.activeSelf, Is.False);
            Assert.That(context.Root.HintPanel.activeSelf, Is.False);
        }

        [Test]
        public void StageHudVisualStyle_HazardSegmentsSwitchBetweenFlatColorsAndLegacySprites()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 0);
            SetPrivateField(context.Hud, "_hasSnapshot", true);
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                HazardStack = 3,
                HazardStackMax = 5,
                HazardRiskMultiplier = 1.15f,
            });
            InvokeConfigurePresenters(context.Root);

            context.Root.StageHudPresenter.SetVisualStyle(StageHudVisualStyle.TechDemoFlat);
            Assert.That(CountHighlightedHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(3));
            Assert.That(CountInactiveHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(2));
            AssertHazardSegmentDisplayLayout(context.Root.StageHudPresenter, width: 20.5f, height: 24f);

            context.Root.StageHudPresenter.SetVisualStyle(StageHudVisualStyle.LegacyIllustrated);
            Assert.That(CountHighlightedHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(3));
            Assert.That(CountInactiveHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(2));
            AssertHazardSegmentDisplayLayout(context.Root.StageHudPresenter, width: 20.5f, height: 24f);

            context.Root.StageHudPresenter.SetVisualStyle(StageHudVisualStyle.TechDemoFlat);
            context.Root.StageHudPresenter.SetVisualStyle(StageHudVisualStyle.LegacyIllustrated);
            Assert.That(CountHighlightedHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(3));
            Assert.That(CountInactiveHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(2));
            AssertHazardSegmentOrder(context.Root.StageHudPresenter, activeCount: 3);
            AssertHazardSegmentDisplayLayout(context.Root.StageHudPresenter, width: 20.5f, height: 24f);
        }

        [Test]
        public void HudPresenters_ApplyNotificationPriority_AndHintOneShot()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 1);
            SetPrivateField(context.Hud, "_hasSnapshot", true);
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                HazardStack = 3,
                HazardStackMax = 5,
                HazardRiskMultiplier = 1.15f,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 145f,
                GameplayElapsedSec = 145f,
                LastHitLossValue = 4,
                HitFlashRemainingSec = 0.5f,
            });

            InvokeConfigurePresenters(context.Root);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPresenter.ObjectiveSummaryText.text, Is.EqualTo("3 / 3"));
            Assert.That(context.Root.StageHudPresenter.RiskMultiplierText.text, Is.EqualTo("x1.15"));
            Assert.That(CountHighlightedHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(3));
            Assert.That(CountInactiveHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(2));
            AssertHazardSegmentOrder(context.Root.StageHudPresenter, activeCount: 3);
            Assert.That(context.Root.NotificationPresenter.NotificationRoot.activeSelf, Is.True);
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Hit! Carry lost"));
            Assert.That(context.Root.HintPresenter.HintRoot.activeSelf, Is.True);
            Assert.That(context.Root.HintPresenter.HintText.text, Is.EqualTo("Carry is full. Head to Deposit."));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 2,
                TotalSourceCount = 3,
                StageStateElapsedSec = 80f,
                GameplayElapsedSec = 80f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.NotificationBridge.RefreshState(2f);

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 141f,
                GameplayElapsedSec = 141f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.NotificationBridge.RefreshPresentationState();
            context.Root.StageHudPresenter.RefreshPresentation();
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Time critical"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 2,
                TotalSourceCount = 3,
                StageStateElapsedSec = 70f,
                GameplayElapsedSec = 70f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.NotificationBridge.RefreshState(2f);

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 2,
                TotalSourceCount = 3,
                StageStateElapsedSec = 70f,
                GameplayElapsedSec = 70f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.NotificationBridge.RefreshPresentationState();
            context.Root.StageHudPresenter.RefreshPresentation();
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Carry full - deposit now"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 70f,
                GameplayElapsedSec = 70f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.Root.StageHudPresenter.RefreshPresentation();
            Assert.That(context.Root.StageHudPresenter.ObjectiveSummaryText.text, Is.EqualTo("3 / 3"));
            Assert.That(context.Root.StageHudPresenter.PressureSourceProgressRoot.activeSelf, Is.False);

            SetPrivateField(context.Hud, "_lastFeedbackSnapshot", new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 1u,
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                Value = 4,
                RemainingSec = 1f,
            });
            SetPrivateField(context.Hud, "_feedbackLine", "Hit -4");
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 2,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
            });
            context.NotificationBridge.RefreshPresentationState();
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Hit! Carry lost"));

            SetPrivateField(context.Hud, "_lastFeedbackSnapshot", new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 2u,
                Type = PlayerUiFeedbackEventType.VacuumStartBlocked,
                Reason = (byte)PlayerUiFeedbackReasonId.CarryBinFull,
                RemainingSec = 1f,
            });
            SetPrivateField(context.Hud, "_feedbackLine", "Vacuum: CarryBin Full");
            context.NotificationBridge.RefreshPresentationState();
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Hit! Carry lost"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                HazardStack = 9,
                HazardStackMax = 5,
                HazardRiskMultiplier = 1.45f,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
            });
            SetPrivateField(context.Hud, "_lastFeedbackSnapshot", new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 3u,
                Type = PlayerUiFeedbackEventType.HazardCaptured,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                RemainingSec = 1f,
            });
            SetPrivateField(context.Hud, "_feedbackLine", "Hazard Captured");
            context.NotificationBridge.RefreshState(2f);
            context.NotificationBridge.RefreshPresentationState();
            context.Root.StageHudPresenter.RefreshPresentation();
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationRoot.activeSelf, Is.True);
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Hazard captured"));
            Assert.That(CountHighlightedHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(5));
            Assert.That(CountInactiveHazardSegments(context.Root.StageHudPresenter), Is.EqualTo(0));
            AssertHazardSegmentOrder(context.Root.StageHudPresenter, activeCount: 5);
            Assert.That(context.Root.StageHudPresenter.RiskMultiplierText.text, Is.EqualTo("x1.45"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 4,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
            });
            context.HintBridge.RefreshState(5f);
            context.HintBridge.RefreshPresentationState();
            context.Root.HintPresenter.RefreshPresentation();
            Assert.That(context.Root.HintPresenter.HintText.text, Is.EqualTo("Collect trash from active sources."));
        }

        [Test]
        public void NotificationBridge_EmitsStageClearAndTimeUp_OnStageResultTransition()
        {
            using var context = CreateContext();

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 0);
            SetPrivateField(context.Hud, "_hasSnapshot", true);
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 0,
                CarryCapacity = 10,
                HazardStackMax = 5,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
            });

            context.NotificationBridge.RefreshPresentationState();
            Assert.That(context.NotificationBridge.CurrentNotification.Visible, Is.False);

            SetPrivateField(context.Shell, "_currentStageOutcome", DemoShellStageOutcomeId.Clear);
            SetPrivateField(context.Shell, "_currentStageResult", new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 110f,
            });
            SetPrivateField(context.Shell, "_hasCurrentStageResult", true);
            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StageResult);

            context.NotificationBridge.RefreshPresentationState();
            Assert.That(context.NotificationBridge.CurrentNotification.Id, Is.EqualTo(NotificationId.StageClear));

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            context.NotificationBridge.RefreshPresentationState();
            SetPrivateField(context.Shell, "_currentStageOutcome", DemoShellStageOutcomeId.Fail);
            SetPrivateField(context.Shell, "_currentStageResult", new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Fail,
                ElapsedSec = 120f,
                HitValue = 4,
            });
            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StageResult);

            context.NotificationBridge.RefreshPresentationState();
            Assert.That(context.NotificationBridge.CurrentNotification.Id, Is.EqualTo(NotificationId.TimeUp));
        }

        [Test]
        public void HintBridge_PersistsStageSeenAcrossRetry_AndSessionSeenAcrossContextReset()
        {
            using var context = CreateContext();

            DemoShellSessionStaging.ResetHintSessionState();
            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 1);
            SetPrivateField(context.Hud, "_hasSnapshot", true);
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
            });

            context.HintBridge.RefreshPresentationState();
            Assert.That(context.HintBridge.CurrentHint.Id, Is.EqualTo(HintId.CarryFullGoToDeposit));
            Assert.That(DemoShellSessionStaging.TryGetActiveStageSeen(2, out ulong stageSeenMask), Is.True);
            Assert.That(HintResolver.HasSeen(stageSeenMask, HintId.CarryFullGoToDeposit), Is.True);

            using var retryContext = CreateContext();
            SetPrivateField(retryContext.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(retryContext.Shell, "_currentStageIndex", 1);
            SetPrivateField(retryContext.Hud, "_hasSnapshot", true);
            SetPrivateField(retryContext.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
            });
            retryContext.HintBridge.RefreshPresentationState();
            Assert.That(retryContext.HintBridge.CurrentHint.Visible, Is.False);

            SetPrivateField(retryContext.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 0,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
                LastHitLossValue = 2,
                HitFlashRemainingSec = 0.5f,
            });
            retryContext.HintBridge.RefreshPresentationState();
            Assert.That(retryContext.HintBridge.CurrentHint.Id, Is.EqualTo(HintId.FirstHitAvoidHazards));
            Assert.That(DemoShellSessionStaging.HasSessionSeenHint(HintId.FirstHitAvoidHazards), Is.True);

            using var reloadContext = CreateContext();
            SetPrivateField(reloadContext.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(reloadContext.Shell, "_currentStageIndex", 2);
            SetPrivateField(reloadContext.Hud, "_hasSnapshot", true);
            SetPrivateField(reloadContext.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 0,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
                GameplayElapsedSec = 20f,
                LastHitLossValue = 2,
                HitFlashRemainingSec = 0.5f,
            });
            reloadContext.HintBridge.RefreshPresentationState();
            Assert.That(reloadContext.HintBridge.CurrentHint.Visible, Is.False);
        }

        [Test]
        public void EnsureHierarchy_BuildsPresentationLayer_AndDialoguePresenter()
        {
            using var context = CreateContext();

            Assert.That(context.Root.PresentationLayer, Is.Not.Null);
            Assert.That(context.Root.DialoguePanel, Is.Not.Null);
            Assert.That(context.Root.DialoguePresenter, Is.Not.Null);
            Assert.That(context.Root.PresentationLayer.GetSiblingIndex(), Is.EqualTo(2));
            Assert.That(context.Root.DialoguePresenter.DialogueRoot, Is.Not.Null);
            Assert.That(context.Root.DialoguePresenter.DialoguePlateRoot, Is.Not.Null);
            Assert.That(context.Root.DialoguePresenter.WorldBubbleRoot, Is.Not.Null);
        }

        [Test]
        public void ApplyShellState_ShowsDialoguePanel_AndSuppressesHudBanners()
        {
            using var context = CreateContext();

            InvokeConfigurePresenters(context.Root);
            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 0);
            SetPrivateField(context.Shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.Running);

            var portrait = CreateTestSprite();
            SetPrivateField(context.NotificationBridge, "_currentNotification", new NotificationResolvedState
            {
                Id = NotificationId.StageClear,
                Message = "Clear",
                Severity = NotificationSeverity.Info,
                Visible = true,
            });
            SetPrivateField(context.HintBridge, "_currentHint", new HintResolvedState
            {
                Id = HintId.CollectFromSources,
                Message = "Collect",
                Visible = true,
            });
            SetPrivateField(context.DialogueBridge, "_currentPresentation", new DialoguePresentationState(
                visible: true,
                trigger: InWorldDialogueTriggerId.StageClear,
                blockingMode: InWorldDialogueBlockingMode.GateClear,
                entryKey: "stage1_clear",
                lineIndex: 0,
                lineCount: 1,
                speakerKey: "hero",
                speakerDisplayName: "Hero",
                speakerPortrait: portrait,
                portraitSide: DialoguePortraitSide.Right,
                bodyText: "Clear line",
                anchor: new InWorldDialogueAnchorRef
                {
                    Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                    ScreenAnchor = InWorldDialogueScreenAnchorId.Center,
                },
                canAdvance: true,
                canSkip: true,
                autoAdvanceEnabled: false,
                lineElapsedSec: 0.4f,
                minHoldSec: 0.1f,
                autoAdvanceSec: 0f));

            context.Root.NotificationPresenter.RefreshPresentation();
            context.Root.HintPresenter.RefreshPresentation();
            context.Root.DialoguePresenter.RefreshPresentation();
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.DialoguePanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.NotificationPanel.activeInHierarchy, Is.False);
            Assert.That(context.Root.HintPanel.activeInHierarchy, Is.False);
            Assert.That(context.Root.DialoguePresenter.DialogueRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.DimRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.WorldBubbleRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.AdvancePromptRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.SkipPromptRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.NameText.text, Is.EqualTo("Hero"));
            Assert.That(context.Root.DialoguePresenter.BodyText.text, Is.EqualTo("Clear line"));
            Assert.That(context.Root.DialoguePresenter.WorldBubbleText.text, Is.EqualTo("Clear line"));
            Assert.That(context.Root.DialoguePresenter.PortraitRoot.activeSelf, Is.True);

            var portraitRect = context.Root.DialoguePresenter.PortraitRoot.GetComponent<RectTransform>();
            Assert.That(portraitRect.anchorMin.x, Is.EqualTo(1f).Within(1e-4f));

            Object.DestroyImmediate(portrait.texture);
            Object.DestroyImmediate(portrait);
        }

        [Test]
        public void ApplyShellState_InterventionOverlay_SuppressesHudBanners_AndRestoresAfterHide()
        {
            using var context = CreateContext();

            InvokeConfigurePresenters(context.Root);
            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 0);
            SetPrivateField(context.Shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.Running);
            SetPrivateField(context.Hud, "_hasSnapshot", true);
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                HazardStackMax = 5,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                GameplayElapsedSec = 35f,
                StageStateElapsedSec = 35f,
            });
            context.Hud.SetRuntimeUiHudActive(true);
            SetPrivateField(context.NotificationBridge, "_currentNotification", new NotificationResolvedState
            {
                Id = NotificationId.CarryFull,
                Message = "Carry Full",
                Severity = NotificationSeverity.Warning,
                Visible = true,
            });
            SetPrivateField(context.HintBridge, "_currentHint", new HintResolvedState
            {
                Id = HintId.CarryFullGoToDeposit,
                Message = "Deposit now",
                Visible = true,
            });
            context.Root.NotificationPresenter.RefreshPresentation();
            context.Root.HintPresenter.RefreshPresentation();
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.NotificationPanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.HintPanel.activeInHierarchy, Is.True);

            SetPrivateField(context.DialogueBridge, "_currentPresentation", new DialoguePresentationState(
                visible: true,
                trigger: InWorldDialogueTriggerId.InterventionCarryFull,
                blockingMode: InWorldDialogueBlockingMode.OverlayOnly,
                entryKey: "stage_01_intervention_carry_full",
                lineIndex: 0,
                lineCount: 1,
                speakerKey: "hero",
                speakerDisplayName: "Hero",
                speakerPortrait: null,
                portraitSide: DialoguePortraitSide.Auto,
                bodyText: "Carry full",
                anchor: new InWorldDialogueAnchorRef
                {
                    Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                    ScreenAnchor = InWorldDialogueScreenAnchorId.LowerCenter,
                },
                canAdvance: true,
                canSkip: true,
                autoAdvanceEnabled: false,
                lineElapsedSec: 0.2f,
                minHoldSec: 0f,
                autoAdvanceSec: 0f));

            context.Root.DialoguePresenter.RefreshPresentation();
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.DialoguePanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.NotificationPanel.activeInHierarchy, Is.False);
            Assert.That(context.Root.HintPanel.activeInHierarchy, Is.False);
            Assert.That(context.Root.DialoguePresenter.DialogueRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.DimRoot.activeSelf, Is.False);
            Assert.That(context.Root.DialoguePresenter.WorldBubbleRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.BodyText.text, Is.EqualTo("Carry full"));

            SetPrivateField(context.DialogueBridge, "_currentPresentation", DialoguePresentationState.Hidden);
            context.Root.DialoguePresenter.RefreshPresentation();
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.DialoguePanel.activeInHierarchy, Is.False);
            Assert.That(context.Root.StageHudPanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.NotificationPanel.activeInHierarchy, Is.True);
            Assert.That(context.Root.HintPanel.activeInHierarchy, Is.True);
        }

        [Test]
        public void DialoguePresenter_AppliesOverlayFallback_AndHiddenState()
        {
            using var context = CreateContext();

            InvokeConfigurePresenters(context.Root);
            SetPrivateField(context.DialogueBridge, "_currentPresentation", new DialoguePresentationState(
                visible: true,
                trigger: InWorldDialogueTriggerId.StageStart,
                blockingMode: InWorldDialogueBlockingMode.OverlayOnly,
                entryKey: "stage1_start",
                lineIndex: 0,
                lineCount: 1,
                speakerKey: "hero",
                speakerDisplayName: "Hero",
                speakerPortrait: null,
                portraitSide: DialoguePortraitSide.Auto,
                bodyText: "Intro line",
                anchor: new InWorldDialogueAnchorRef
                {
                    Kind = InWorldDialogueAnchorKind.StagePresentationStableId,
                    StagePresentationStableId = 1001u,
                },
                canAdvance: false,
                canSkip: true,
                autoAdvanceEnabled: false,
                lineElapsedSec: 0f,
                minHoldSec: 0.2f,
                autoAdvanceSec: 0f));

            context.Root.DialoguePresenter.RefreshPresentation();

            Assert.That(context.Root.DialoguePresenter.DialogueRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.DimRoot.activeSelf, Is.False);
            Assert.That(context.Root.DialoguePresenter.WorldBubbleRoot.activeSelf, Is.False);
            Assert.That(context.Root.DialoguePresenter.PortraitRoot.activeSelf, Is.False);
            Assert.That(context.Root.DialoguePresenter.AdvancePromptRoot.activeSelf, Is.False);
            Assert.That(context.Root.DialoguePresenter.SkipPromptRoot.activeSelf, Is.True);

            SetPrivateField(context.DialogueBridge, "_currentPresentation", DialoguePresentationState.Hidden);
            context.Root.DialoguePresenter.RefreshPresentation();
            Assert.That(context.Root.DialoguePresenter.DialogueRoot.activeSelf, Is.False);
        }

        [Test]
        public void DialoguePresenter_ProjectsStablePresentationAnchor_WhenVisibleOnScreen()
        {
            using var context = CreateContext();

            var cameraGo = new GameObject("ProjectionCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;

            var spawnedRoot = new GameObject("Presentation_9001_preview_visual_01");
            var markerGo = new GameObject("DialogueBubbleAnchor");
            markerGo.transform.SetParent(spawnedRoot.transform, false);
            markerGo.transform.position = new Vector3(0f, 1.5f, 0f);
            markerGo.AddComponent<StagePresentationAnchorMarker>().AnchorKind = StagePresentationAnchorKind.DialogueBubble;

            RegisterSpawnedPresentation(context.PresentationRuntime, 9001u, spawnedRoot);
            context.Root.DialoguePresenter.ProjectionCamera = camera;
            InvokeConfigurePresenters(context.Root);
            SetPrivateField(context.DialogueBridge, "_currentPresentation", new DialoguePresentationState(
                visible: true,
                trigger: InWorldDialogueTriggerId.StageStart,
                blockingMode: InWorldDialogueBlockingMode.OverlayOnly,
                entryKey: "stage1_start_world",
                lineIndex: 0,
                lineCount: 1,
                speakerKey: "hero",
                speakerDisplayName: "Hero",
                speakerPortrait: null,
                portraitSide: DialoguePortraitSide.Left,
                bodyText: "World anchor line",
                anchor: new InWorldDialogueAnchorRef
                {
                    Kind = InWorldDialogueAnchorKind.StagePresentationStableId,
                    StagePresentationStableId = 9001u,
                },
                canAdvance: true,
                canSkip: true,
                autoAdvanceEnabled: false,
                lineElapsedSec: 0.2f,
                minHoldSec: 0.1f,
                autoAdvanceSec: 0f));

            context.Root.DialoguePresenter.RefreshPresentation();

            Assert.That(context.PresentationRuntime.TryGetPresentationAnchor(9001u, out var anchor), Is.True);
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.name, Is.EqualTo("DialogueBubbleAnchor"));
            Assert.That(context.Root.DialoguePresenter.WorldBubbleRoot.activeSelf, Is.True);
            Assert.That(context.Root.DialoguePresenter.WorldBubbleText.text, Is.EqualTo("World anchor line"));

            Object.DestroyImmediate(spawnedRoot);
            Object.DestroyImmediate(cameraGo);
        }

        [Test]
        public void DialoguePresenter_HidesWorldBubble_WhenAnchorIsBehindCamera()
        {
            using var context = CreateContext();

            var cameraGo = new GameObject("ProjectionCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;

            var spawnedRoot = new GameObject("Presentation_9002_preview_visual_02");
            spawnedRoot.transform.position = new Vector3(0f, 0f, -5f);
            RegisterSpawnedPresentation(context.PresentationRuntime, 9002u, spawnedRoot);
            context.Root.DialoguePresenter.ProjectionCamera = camera;
            InvokeConfigurePresenters(context.Root);
            SetPrivateField(context.DialogueBridge, "_currentPresentation", new DialoguePresentationState(
                visible: true,
                trigger: InWorldDialogueTriggerId.StageStart,
                blockingMode: InWorldDialogueBlockingMode.OverlayOnly,
                entryKey: "stage2_start_world",
                lineIndex: 0,
                lineCount: 1,
                speakerKey: "hero",
                speakerDisplayName: "Hero",
                speakerPortrait: null,
                portraitSide: DialoguePortraitSide.Auto,
                bodyText: "Behind camera",
                anchor: new InWorldDialogueAnchorRef
                {
                    Kind = InWorldDialogueAnchorKind.StagePresentationStableId,
                    StagePresentationStableId = 9002u,
                },
                canAdvance: true,
                canSkip: true,
                autoAdvanceEnabled: false,
                lineElapsedSec: 0f,
                minHoldSec: 0f,
                autoAdvanceSec: 0f));

            context.Root.DialoguePresenter.RefreshPresentation();

            Assert.That(context.PresentationRuntime.TryGetPresentationAnchor(9002u, out var anchor), Is.True);
            Assert.That(anchor, Is.EqualTo(spawnedRoot.transform));
            Assert.That(context.Root.DialoguePresenter.WorldBubbleRoot.activeSelf, Is.False);
            Assert.That(context.Root.DialoguePresenter.DialogueRoot.activeSelf, Is.True);

            Object.DestroyImmediate(spawnedRoot);
            Object.DestroyImmediate(cameraGo);
        }

        private static TestContext CreateContext()
        {
            var shellGo = new GameObject("DemoShell_Test");
            shellGo.SetActive(false);

            var stageBridge = shellGo.AddComponent<RunDirectorStageBridge>();
            stageBridge.LogBindWarnings = false;
            var topologyBridge = shellGo.AddComponent<StageTopologyBridge>();
            var shell = shellGo.AddComponent<DemoShellFlowController>();
            shell.StageBridge = stageBridge;
            shell.TopologyBridge = topologyBridge;
            shell.StageProfiles = new[]
            {
                new DemoShellStageProfile { StageId = 1, DisplayName = "Stage 1", StageTimeLimitSec = 120f },
                new DemoShellStageProfile { StageId = 2, DisplayName = "Stage 2", StageTimeLimitSec = 150f },
                new DemoShellStageProfile { StageId = 3, DisplayName = "Stage 3", IsFinalStage = true, StageTimeLimitSec = 180f },
            };

            var audio = shellGo.AddComponent<DemoAudioBridge>();
            audio.DemoShell = shell;
            var pauseController = shellGo.AddComponent<DemoShellGameplayPauseController>();
            pauseController.LogBindWarnings = false;
            var pauseBridge = shellGo.AddComponent<DemoShellPauseBridge>();
            pauseBridge.DemoShell = shell;
            pauseBridge.StageBridge = stageBridge;
            pauseBridge.PauseController = pauseController;
            var hud = shellGo.AddComponent<PlayerRuntimeHudBridge>();
            hud.DemoShell = shell;
            var notificationBridge = shellGo.AddComponent<DemoShellNotificationBridge>();
            notificationBridge.DemoShell = shell;
            notificationBridge.RuntimeHudBridge = hud;
            var hintBridge = shellGo.AddComponent<DemoShellHintBridge>();
            hintBridge.DemoShell = shell;
            hintBridge.PauseBridge = pauseBridge;
            hintBridge.RuntimeHudBridge = hud;
            hintBridge.NotificationBridge = notificationBridge;
            var dialogueBridge = shellGo.AddComponent<DemoShellDialogueBridge>();
            dialogueBridge.DemoShell = shell;
            dialogueBridge.PauseController = pauseController;
            dialogueBridge.LogBindWarnings = false;
            var presentationRuntime = shellGo.AddComponent<StagePresentationRuntimeController>();
            presentationRuntime.LogWarnings = false;
            presentationRuntime.RebuildOnEnable = false;
            presentationRuntime.DestroyOnDisable = true;

            var rootGo = new GameObject("RuntimeUiRoot_Test");
            rootGo.SetActive(false);
            var root = rootGo.AddComponent<RuntimeUiRoot>();
            root.DemoShell = shell;
            root.DemoAudio = audio;
            root.PauseBridge = pauseBridge;
            root.RuntimeHudBridge = hud;
            root.NotificationBridge = notificationBridge;
            root.HintBridge = hintBridge;
            root.DialogueBridge = dialogueBridge;
            root.PresentationRuntimeController = presentationRuntime;
            root.LogBindWarnings = false;
            dialogueBridge.RuntimeUiRoot = root;
            root.BuildDefaultHierarchyForTests();
            var hazardActiveSprite = CreateTestSprite(
                "HazardBrushActive",
                new Color(0.95f, 0.82f, 0.34f, 1f),
                82,
                96,
                new Vector2(0.5121951f, 0.46875f));
            var hazardInactiveSprite = CreateTestSprite(
                "HazardBrushInactive",
                new Color(0.58f, 0.62f, 0.68f, 1f),
                82,
                96,
                new Vector2(0.5121951f, 0.46875f));
            root.StageHudPresenter.HazardStackActiveSprite = hazardActiveSprite;
            root.StageHudPresenter.HazardStackInactiveSprite = hazardInactiveSprite;
            rootGo.SetActive(true);

            return new TestContext(
                shellGo,
                rootGo,
                shell,
                audio,
                pauseController,
                pauseBridge,
                hud,
                notificationBridge,
                hintBridge,
                dialogueBridge,
                presentationRuntime,
                root,
                hazardActiveSprite,
                hazardInactiveSprite);
        }

        private static void InvokeConfigurePresenters(RuntimeUiRoot root)
        {
            Assert.That(ConfigurePresentersMethod, Is.Not.Null, "RuntimeUiRoot.ConfigurePresenters method not found.");
            ConfigurePresentersMethod.Invoke(root, null);
        }

        private static void InvokeApplyShellState(RuntimeUiRoot root, bool force)
        {
            Assert.That(ApplyShellStateMethod, Is.Not.Null, "RuntimeUiRoot.ApplyShellState method not found.");
            ApplyShellStateMethod.Invoke(root, new object[] { force });
        }

        private static void InvokePauseBridgeUpdate(DemoShellPauseBridge bridge)
        {
            Assert.That(PauseBridgeUpdateMethod, Is.Not.Null, "DemoShellPauseBridge.Update method not found.");
            PauseBridgeUpdateMethod.Invoke(bridge, null);
        }

        private static Selectable ResolveCurrentDefaultSelectable(RuntimeUiRoot root)
        {
            Assert.That(ResolveCurrentShellDefaultSelectableMethod, Is.Not.Null, "RuntimeUiRoot.ResolveCurrentShellDefaultSelectable method not found.");
            return ResolveCurrentShellDefaultSelectableMethod.Invoke(root, null) as Selectable;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void RegisterSpawnedPresentation(StagePresentationRuntimeController controller, uint stableId, GameObject root)
        {
            Assert.That(controller, Is.Not.Null);
            var field = typeof(StagePresentationRuntimeController).GetField("_spawnedByStableId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var map = field.GetValue(controller) as System.Collections.IDictionary;
            Assert.That(map, Is.Not.Null);
            map[stableId] = root;
        }

        private static bool ContainsText(TextMeshProUGUI[] texts, string expected)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].text == expected)
                    return true;
            }

            return false;
        }

        private static int CountHighlightedHazardSegments(StageHudPresenter presenter)
        {
            if (presenter == null || presenter.HazardStackSegmentImages == null)
                return 0;

            int count = 0;
            for (int i = 0; i < presenter.HazardStackSegmentImages.Length; i++)
            {
                var image = presenter.HazardStackSegmentImages[i];
                bool highlighted = presenter.VisualStyle == StageHudVisualStyle.TechDemoFlat
                    ? image != null && image.sprite == null && ColorsApproximatelyEqual(image.color, FlatHazardActiveColor)
                    : image != null && image.sprite == presenter.HazardStackActiveSprite;
                if (highlighted)
                    count++;
            }

            return count;
        }

        private static int CountInactiveHazardSegments(StageHudPresenter presenter)
        {
            if (presenter == null || presenter.HazardStackSegmentImages == null)
                return 0;

            int count = 0;
            for (int i = 0; i < presenter.HazardStackSegmentImages.Length; i++)
            {
                var image = presenter.HazardStackSegmentImages[i];
                bool inactive = presenter.VisualStyle == StageHudVisualStyle.TechDemoFlat
                    ? image != null && image.sprite == null && ColorsApproximatelyEqual(image.color, FlatHazardInactiveColor)
                    : image != null && image.sprite == presenter.HazardStackInactiveSprite;
                if (inactive)
                    count++;
            }

            return count;
        }

        private static void AssertHazardSegmentOrder(StageHudPresenter presenter, int activeCount)
        {
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.HazardStackSegmentImages, Is.Not.Null);

            int maxSegments = presenter.HazardStackSegmentImages.Length;
            for (int i = 0; i < maxSegments; i++)
            {
                var image = presenter.HazardStackSegmentImages[i];
                Assert.That(image, Is.Not.Null, $"Hazard segment image {i} is missing.");

                var slot = image.transform.parent as RectTransform;
                Assert.That(slot, Is.Not.Null, $"Hazard segment slot {i} is missing.");

                int expectedSibling = i < activeCount
                    ? (maxSegments - activeCount) + i
                    : (maxSegments - 1) - i;
                Assert.That(slot.GetSiblingIndex(), Is.EqualTo(expectedSibling), $"Hazard segment {i} sibling order mismatch.");
            }
        }

        private static void AssertHazardStackFrameHeight(StageHudPresenter presenter, int maxSegments)
        {
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.HazardStackFrameImage, Is.Not.Null);
            Assert.That(presenter.HazardStackSegmentsRoot, Is.Not.Null);

            float expectedHeight = presenter.FrameBaseHeight + maxSegments * presenter.FrameHeightPerSegment;
            Assert.That(presenter.HazardStackFrameImage.rectTransform.sizeDelta.y, Is.EqualTo(expectedHeight).Within(1e-4f));
            Assert.That(presenter.HazardStackFrameImage.rectTransform.anchoredPosition.y, Is.EqualTo(expectedHeight * 0.5f).Within(1e-4f));
            Assert.That(presenter.HazardStackSegmentsRoot.sizeDelta.y, Is.EqualTo(expectedHeight).Within(1e-4f));
        }

        private static void AssertHazardSegmentDisplayLayout(StageHudPresenter presenter, float width, float height)
        {
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.HazardStackSegmentImages, Is.Not.Null);

            for (int i = 0; i < presenter.HazardStackSegmentImages.Length; i++)
            {
                var image = presenter.HazardStackSegmentImages[i];
                Assert.That(image, Is.Not.Null, $"Hazard segment image {i} is missing.");

                var rect = image.rectTransform;
                Assert.That(rect.localPosition.x, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(rect.localPosition.y, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(rect.localPosition.z, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(0f).Within(1e-4f));
                float expectedPivotX = presenter.VisualStyle == StageHudVisualStyle.TechDemoFlat ? 0.5f : 0.5121951f;
                float expectedPivotY = presenter.VisualStyle == StageHudVisualStyle.TechDemoFlat ? 0.5f : 0.46875f;
                Assert.That(rect.pivot.x, Is.EqualTo(expectedPivotX).Within(1e-4f));
                Assert.That(rect.pivot.y, Is.EqualTo(expectedPivotY).Within(1e-4f));
                Assert.That(rect.sizeDelta.x, Is.EqualTo(width).Within(1e-4f));
                Assert.That(rect.sizeDelta.y, Is.EqualTo(height).Within(1e-4f));
            }
        }

        private static bool ColorsApproximatelyEqual(Color lhs, Color rhs)
        {
            return Mathf.Abs(lhs.r - rhs.r) <= 1e-4f
                && Mathf.Abs(lhs.g - rhs.g) <= 1e-4f
                && Mathf.Abs(lhs.b - rhs.b) <= 1e-4f
                && Mathf.Abs(lhs.a - rhs.a) <= 1e-4f;
        }

        private static bool HazardStackDisplaysNoMaxText(StageHudPresenter presenter)
        {
            if (presenter == null || presenter.HazardStackRoot == null)
                return false;

            var texts = presenter.HazardStackRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].text.Contains("/"))
                    return false;
            }

            return true;
        }

        private static Sprite CreateTestSprite(
            string name = "TestSprite",
            Color? color = null,
            int width = 1,
            int height = 1,
            Vector2? pivot = null)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"{name}_Texture",
            };
            Color pixelColor = color ?? Color.white;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, pixelColor);
            }
            texture.Apply();
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                pivot ?? new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = name;
            return sprite;
        }

        private static void ClearVolumePrefs()
        {
            for (int i = 0; i < DemoAudioPrefsKeys.AllVolumeKeys.Length; i++)
                PlayerPrefs.DeleteKey(DemoAudioPrefsKeys.AllVolumeKeys[i]);
            PlayerPrefs.Save();
        }

        private sealed class TestContext : System.IDisposable
        {
            private readonly GameObject _shellGo;
            private readonly GameObject _rootGo;
            private readonly Sprite _hazardActiveSprite;
            private readonly Sprite _hazardInactiveSprite;

            public TestContext(
                GameObject shellGo,
                GameObject rootGo,
                DemoShellFlowController shell,
                DemoAudioBridge audio,
                DemoShellGameplayPauseController pauseController,
                DemoShellPauseBridge pauseBridge,
                PlayerRuntimeHudBridge hud,
                DemoShellNotificationBridge notificationBridge,
                DemoShellHintBridge hintBridge,
                DemoShellDialogueBridge dialogueBridge,
                StagePresentationRuntimeController presentationRuntime,
                RuntimeUiRoot root,
                Sprite hazardActiveSprite,
                Sprite hazardInactiveSprite)
            {
                _shellGo = shellGo;
                _rootGo = rootGo;
                _hazardActiveSprite = hazardActiveSprite;
                _hazardInactiveSprite = hazardInactiveSprite;
                Shell = shell;
                Audio = audio;
                PauseController = pauseController;
                PauseBridge = pauseBridge;
                Hud = hud;
                NotificationBridge = notificationBridge;
                HintBridge = hintBridge;
                DialogueBridge = dialogueBridge;
                PresentationRuntime = presentationRuntime;
                Root = root;
            }

            public DemoShellFlowController Shell { get; }
            public DemoAudioBridge Audio { get; }
            public DemoShellGameplayPauseController PauseController { get; }
            public DemoShellPauseBridge PauseBridge { get; }
            public PlayerRuntimeHudBridge Hud { get; }
            public DemoShellNotificationBridge NotificationBridge { get; }
            public DemoShellHintBridge HintBridge { get; }
            public DemoShellDialogueBridge DialogueBridge { get; }
            public StagePresentationRuntimeController PresentationRuntime { get; }
            public RuntimeUiRoot Root { get; }

            public void Dispose()
            {
                DestroySprite(_hazardActiveSprite);
                DestroySprite(_hazardInactiveSprite);
                if (_rootGo != null)
                    Object.DestroyImmediate(_rootGo);
                if (_shellGo != null)
                    Object.DestroyImmediate(_shellGo);
            }

            private static void DestroySprite(Sprite sprite)
            {
                if (sprite == null)
                    return;

                if (sprite.texture != null)
                    Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
            }
        }
    }
}
