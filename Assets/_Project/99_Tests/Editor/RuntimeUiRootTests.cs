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
