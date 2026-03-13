using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets.Tests
{
    public class RuntimeUiRootTests
    {
        private static readonly MethodInfo ConfigurePresentersMethod = typeof(RuntimeUiRoot)
            .GetMethod("ConfigurePresenters", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ApplyShellStateMethod = typeof(RuntimeUiRoot)
            .GetMethod("ApplyShellState", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ResolveCurrentShellDefaultSelectableMethod = typeof(RuntimeUiRoot)
            .GetMethod("ResolveCurrentShellDefaultSelectable", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            ClearVolumePrefs();
        }

        [TearDown]
        public void TearDown()
        {
            ClearVolumePrefs();
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
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                PressureSourceStableId = 1002u,
                PressureSourceCollected = 6,
                PressureSourceThresholdWeakened = 4,
                PressureSourceThresholdDepleted = 8,
                PressureSourceProgress01 = 0.75f,
                StageStateElapsedSec = 50f,
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
            Assert.That(context.Root.StageHudPresenter.ObjectiveSummaryText.text, Is.EqualTo("Sources 1/3 cleared"));
            Assert.That(context.Root.StageHudPresenter.ObjectiveDetailText.text, Is.EqualTo("Pressure Source #1002  6/8"));
            Assert.That(context.Root.StageHudPresenter.PressureSourceProgressRoot.activeSelf, Is.True);
            Assert.That(context.Root.StageHudPresenter.PressureSourceValueText.text, Is.EqualTo("6 / 8"));
            Assert.That(context.Root.StageHudPresenter.PressureSourceFillImage.fillAmount, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(context.Root.StageHudPresenter.PressureSourceWeakThresholdMarker.gameObject.activeSelf, Is.True);
            Assert.That(context.Root.StageHudPresenter.PressureSourceWeakThresholdMarker.anchorMin.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(context.Root.StageHudPresenter.CarryValueText.text, Is.EqualTo("5 / 10"));
            Assert.That(context.Root.StageHudPresenter.CarryFillImage.fillAmount, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(context.Root.StageHudPresenter.TimerValueText.text, Is.EqualTo("70.0s"));
            Assert.That(context.Root.NotificationPresenter.NotificationRoot.activeSelf, Is.False);
            Assert.That(context.Root.HintPresenter.HintRoot.activeSelf, Is.False);

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.Title);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPanel.activeSelf, Is.False);
            Assert.That(context.Root.NotificationPanel.activeSelf, Is.False);
            Assert.That(context.Root.HintPanel.activeSelf, Is.False);
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
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 145f,
                LastHitLossValue = 4,
                HitFlashRemainingSec = 0.5f,
            });

            InvokeConfigurePresenters(context.Root);
            InvokeApplyShellState(context.Root, force: true);

            Assert.That(context.Root.StageHudPresenter.ObjectiveSummaryText.text, Is.EqualTo("Sources 3/3 cleared"));
            Assert.That(context.Root.NotificationPresenter.NotificationRoot.activeSelf, Is.True);
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Hit! Carry lost"));
            Assert.That(context.Root.HintPresenter.HintRoot.activeSelf, Is.True);
            Assert.That(context.Root.HintPresenter.HintText.text, Is.EqualTo("Carry is full. Head to Deposit."));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 141f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.Root.StageHudPresenter.RefreshPresentation();
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Time critical"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                DepletedSourceCount = 2,
                TotalSourceCount = 3,
                StageStateElapsedSec = 70f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.Root.StageHudPresenter.RefreshPresentation();
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Carry full - deposit now"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                StageStateElapsedSec = 70f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            context.Root.StageHudPresenter.RefreshPresentation();
            Assert.That(context.Root.StageHudPresenter.ObjectiveSummaryText.text, Is.EqualTo("Sources 3/3 cleared"));
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
                DepletedSourceCount = 2,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
            });
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Carry full - deposit now"));

            SetPrivateField(context.Hud, "_lastFeedbackSnapshot", new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 2u,
                Type = PlayerUiFeedbackEventType.VacuumStartBlocked,
                Reason = (byte)PlayerUiFeedbackReasonId.CarryBinFull,
                RemainingSec = 1f,
            });
            SetPrivateField(context.Hud, "_feedbackLine", "Vacuum: CarryBin Full");
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Carry full - deposit now"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
            });
            SetPrivateField(context.Hud, "_lastFeedbackSnapshot", new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 3u,
                Type = PlayerUiFeedbackEventType.HazardCaptured,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                RemainingSec = 1f,
            });
            SetPrivateField(context.Hud, "_feedbackLine", "Hazard Captured");
            context.Root.NotificationPresenter.RefreshPresentation();
            Assert.That(context.Root.NotificationPresenter.NotificationRoot.activeSelf, Is.True);
            Assert.That(context.Root.NotificationPresenter.NotificationText.text, Is.EqualTo("Hazard Captured"));

            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 4,
                CarryCapacity = 10,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
            });
            context.Root.HintPresenter.RefreshPresentation();
            SetPrivateField(context.Hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
            });
            context.Root.HintPresenter.RefreshPresentation();
            Assert.That(context.Root.HintPresenter.HintText.text, Is.EqualTo("Carry is full. Head to Deposit."));
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
            var pauseBridge = shellGo.AddComponent<DemoShellPauseBridge>();
            pauseBridge.DemoShell = shell;
            pauseBridge.StageBridge = stageBridge;
            var hud = shellGo.AddComponent<PlayerRuntimeHudBridge>();
            hud.DemoShell = shell;

            var rootGo = new GameObject("RuntimeUiRoot_Test");
            rootGo.SetActive(false);
            var root = rootGo.AddComponent<RuntimeUiRoot>();
            root.DemoShell = shell;
            root.DemoAudio = audio;
            root.PauseBridge = pauseBridge;
            root.RuntimeHudBridge = hud;
            root.LogBindWarnings = false;
            root.EnsureHierarchy();
            rootGo.SetActive(true);

            return new TestContext(shellGo, rootGo, shell, audio, pauseBridge, hud, root);
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

        private static bool ContainsText(TextMeshProUGUI[] texts, string expected)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].text == expected)
                    return true;
            }

            return false;
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

            public TestContext(
                GameObject shellGo,
                GameObject rootGo,
                DemoShellFlowController shell,
                DemoAudioBridge audio,
                DemoShellPauseBridge pauseBridge,
                PlayerRuntimeHudBridge hud,
                RuntimeUiRoot root)
            {
                _shellGo = shellGo;
                _rootGo = rootGo;
                Shell = shell;
                Audio = audio;
                PauseBridge = pauseBridge;
                Hud = hud;
                Root = root;
            }

            public DemoShellFlowController Shell { get; }
            public DemoAudioBridge Audio { get; }
            public DemoShellPauseBridge PauseBridge { get; }
            public PlayerRuntimeHudBridge Hud { get; }
            public RuntimeUiRoot Root { get; }

            public void Dispose()
            {
                if (_rootGo != null)
                    Object.DestroyImmediate(_rootGo);
                if (_shellGo != null)
                    Object.DestroyImmediate(_shellGo);
            }
        }
    }
}
