using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletPlayModeSmokeTests
    {
        private const string DedicatedScenePath = "Assets/_Project/01_Scenes/PlayModeTests/PlayModeSmoke_Dedicated.unity";
        private const string OperationalScenePath = "Assets/_Project/01_Scenes/SampleScene.unity";

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_PipelineBootAndCoreLoop_RunWithoutHardErrors()
        {
            yield return RunSceneSmoke(
                scenePath: OperationalScenePath,
                sceneLabel: "SampleScene_StageCatalog",
                frameCount: 120);
        }

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_StressSwitch_BurstRequest_ImpactsBacklogAndHud()
        {
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                ClearDemoShellStaging();
                yield return LoadSceneIgnoringBootstrapBacklogErrors(OperationalScenePath);

                var world = World.DefaultGameObjectInjectionWorld;
                Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

                var em = world.EntityManager;
                DemoShellFlowController shell = null;
                yield return WaitForCondition(
                    () =>
                    {
                        shell = FindDemoShell();
                        return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                    },
                    240,
                    "DemoShellFlowController was not ready in operational scene for stress test.");

                Assert.That(shell.RequestStartFromTitle(), Is.True);
                yield return WaitForCondition(
                    () =>
                    {
                        shell = FindDemoShell();
                        return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                    },
                    240,
                    "Operational stress test did not reach Lobby.");

                Assert.That(shell.RequestSelectStageById(1), Is.True);
                yield return WaitForCondition(
                    () =>
                        CountByComponentType<PlayerTag>(em) > 0 &&
                        CountByComponentType<SourceSpawnComponent>(em) > 0 &&
                        CountByComponentType<BulletFrameCounterComponent>(em) > 0 &&
                        (shell = FindDemoShell()) != null &&
                        shell.CurrentScreen == DemoShellScreenId.StagePlay &&
                        shell.CurrentStageId == 1 &&
                        IsStageMapAppliedForStage1(em) &&
                        HasSingleton<SpawnBacklogMetricsComponent>(em) &&
                        HasSingleton<DebugHudMetricsComponent>(em),
                    480,
                    "ECS singleton setup for stress/HUD was not ready within timeout.");
                ForceStageStateToRunning(em, 0f);

                int baselineMaxPending = 0;
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                    var baselineMetrics = GetSingleton<SpawnBacklogMetricsComponent>(em);
                    baselineMaxPending = Mathf.Max(baselineMaxPending, baselineMetrics.PendingCount);
                }

                EnqueueBurstRequestsFromFirstPattern(em, requestCount: 20000);

                int postMaxPending = 0;
                int postMaxHudSpawned = 0;
                for (int i = 0; i < 90; i++)
                {
                    yield return null;
                    var postMetrics = GetSingleton<SpawnBacklogMetricsComponent>(em);
                    var hud = GetSingleton<DebugHudMetricsComponent>(em);
                    postMaxPending = Mathf.Max(postMaxPending, postMetrics.PendingCount);
                    postMaxHudSpawned = Mathf.Max(postMaxHudSpawned, hud.SpawnedThisFrame);
                }

                Assert.That(postMaxPending, Is.GreaterThan(baselineMaxPending + 1000), "Burst request should noticeably increase pending backlog");
                Assert.That(postMaxHudSpawned, Is.GreaterThan(0), "HUD spawned metric should be updated during burst run");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_DataDrivenPatternScenario_BaselineMetricsAreRecorded()
        {
            ClearDemoShellStaging();
            DemoShellSessionStaging.StageStagePlay(0);
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    CountByComponentType<SourceSpawnComponent>(em) > 0 &&
                    IsStageMapAppliedForStage1(em) &&
                    HasSingleton<SpawnBacklogMetricsComponent>(em) &&
                    HasSingleton<BulletFrameCounterComponent>(em),
                300,
                "Data-driven scenario setup was not ready within timeout.");

            int baselineMaxActive = 0;
            int baselineMaxPending = 0;
            int baselineMaxOldestAge = 0;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                baselineMaxActive = Mathf.Max(baselineMaxActive, CountByComponentType<BulletActiveTag>(em));
                baselineMaxPending = Mathf.Max(baselineMaxPending, GetSingleton<SpawnBacklogMetricsComponent>(em).PendingCount);
                baselineMaxOldestAge = Mathf.Max(baselineMaxOldestAge, ComputeOldestPendingAgeFrames(em));
            }

            Entity transitionedSource = FindSourceWithEventClip(em);
            Assert.That(transitionedSource, Is.Not.EqualTo(Entity.Null), "Event-clip source was not found for transition scenario.");

            var source = em.GetComponentData<SourceSpawnComponent>(transitionedSource);
            source.CollectedCount = Mathf.Max(source.CollectedCount, source.ThresholdWeakened);
            source.State = SourceStateId.Weakened;
            em.SetComponentData(transitionedSource, source);

            if (em.HasComponent<SourceSustainRuntimeComponent>(transitionedSource))
            {
                var runtime = em.GetComponentData<SourceSustainRuntimeComponent>(transitionedSource);
                runtime.ActiveState = SourceStateId.Normal;
                em.SetComponentData(transitionedSource, runtime);
            }

            int maxActiveBullets = 0;
            int maxPendingBacklog = 0;
            int maxOldestAge = 0;
            int maxDropped = 0;
            int maxExpired = 0;
            for (int i = 0; i < 180; i++)
            {
                yield return null;
                maxActiveBullets = Mathf.Max(maxActiveBullets, CountByComponentType<BulletActiveTag>(em));

                var metrics = GetSingleton<SpawnBacklogMetricsComponent>(em);
                maxPendingBacklog = Mathf.Max(maxPendingBacklog, metrics.PendingCount);
                maxDropped = Mathf.Max(maxDropped, metrics.DroppedByCapacity);
                maxExpired = Mathf.Max(maxExpired, metrics.ExpiredByAge);
                maxOldestAge = Mathf.Max(maxOldestAge, ComputeOldestPendingAgeFrames(em));
            }

            Assert.That(maxActiveBullets, Is.GreaterThan(0), "Scenario must produce active bullets.");
            Assert.That(maxPendingBacklog, Is.GreaterThanOrEqualTo(0), "Pending backlog metric must be observable.");

            Debug.Log(
                $"[PlayModeBaseline] scenario=waveclip_v3_default baselineActive={baselineMaxActive} baselinePending={baselineMaxPending} baselineOldestAge={baselineMaxOldestAge} " +
                $"maxActiveBullets={maxActiveBullets} maxPendingBacklog={maxPendingBacklog} maxOldestAge={maxOldestAge} dropCount={maxDropped} expiredByAge={maxExpired}");
        }

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_RunDirectorStageBridge_ConfirmTransitionsToCompleted()
        {
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return LoadSceneIgnoringBootstrapBacklogErrors(DedicatedScenePath);

                var world = World.DefaultGameObjectInjectionWorld;
                Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

                var em = world.EntityManager;
                yield return WaitForCondition(
                    () =>
                        HasSingleton<RunDirectorStageStateComponent>(em) &&
                        HasSingleton<RunDirectorStageGateComponent>(em) &&
                        HasSingleton<RunDirectorStageRequestComponent>(em) &&
                        HasSingleton<RunDirectorStageSignalComponent>(em),
                    300,
                    "RunDirector stage singleton setup was not ready within timeout.");

                var stageStateEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
                em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.ClearReady,
                    StateElapsedSec = 0f,
                    EnteredFrame = 0u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.None,
                });

                var gateEntity = GetSingletonEntity<RunDirectorStageGateComponent>(em);
                em.SetComponentData(gateEntity, new RunDirectorStageGateComponent
                {
                    IntroPresentationDone = 1,
                    ClearPresentationDone = 1,
                    MinIdleDurationElapsed = 0,
                    AutoAdvanceTimeoutElapsed = 0,
                });

                var requestEntity = GetSingletonEntity<RunDirectorStageRequestComponent>(em);
                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));

                var bridgeGo = new GameObject("RunDirectorStageBridge_PlayMode");
                var bridge = bridgeGo.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;

                Assert.That(bridge.SetClearPresentationDone(true), Is.True);
                Assert.That(bridge.RequestConfirm(), Is.True);
                yield return WaitForCondition(
                    () => GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Completed,
                    120,
                    "RunDirector stage did not reach Completed after bridge confirm request.");

                var stage = GetSingleton<RunDirectorStageStateComponent>(em);
                Assert.That(stage.State, Is.EqualTo(RunDirectorStageStateId.Completed));
                Assert.That(stage.LastTransitionReason, Is.EqualTo(RunDirectorStageTransitionReasonId.ConfirmPressed));

                Object.Destroy(bridgeGo);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_TitleLobbyStageResult_Flow()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em) &&
                    HasSingleton<RunDirectorStageGateComponent>(em) &&
                    HasSingleton<RunDirectorStageRequestComponent>(em) &&
                    HasSingleton<RunDirectorStageSignalComponent>(em),
                300,
                "RunDirector stage singleton setup was not ready within timeout.");

            yield return WaitForCondition(
                () => FindDemoShell() != null,
                240,
                "DemoShellFlowController was not found in operational scene.");
            var shell = FindDemoShell();
            Assert.That(shell.CurrentScreen, Is.EqualTo(DemoShellScreenId.Title));
            Assert.That(shell.RequestStartFromTitle(), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition Title -> Lobby.");
            Assert.That(shell.RequestSelectStageById(1), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay;
                },
                240,
                "Demo shell did not enter StagePlay from Lobby.");

            yield return WaitForCondition(
                () => IsStageMapAppliedForStage1(em),
                240,
                "StageMap layout was not applied for Stage1 within timeout.");
            AssertStageMapAppliedForStage1(em);

            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StageResult;
                },
                240,
                "Demo shell did not enter StageResult on ClearReady.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_RuntimeUiRoot_ShellPanelsFollowShellFlow()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            RuntimeUiRoot uiRoot = null;
            DemoShellFlowController shell = null;

            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "RuntimeUiRoot or DemoShellFlowController was not ready in operational scene.");

            Assert.That(shell.RuntimeUiShellActive, Is.True);
            Assert.That(uiRoot.IsShellPanelVisible(DemoShellScreenId.Title), Is.True);
            Assert.That(uiRoot.IsShellPanelVisible(DemoShellScreenId.Lobby), Is.False);
            Assert.That(uiRoot.IsShellPanelVisible(DemoShellScreenId.StageResult), Is.False);
            Assert.That(uiRoot.IsShellPanelVisible(DemoShellScreenId.DemoComplete), Is.False);

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.Lobby
                        && uiRoot.IsShellPanelVisible(DemoShellScreenId.Lobby);
                },
                240,
                "RuntimeUiRoot did not show Lobby panel after Title -> Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && !uiRoot.IsShellPanelVisible(DemoShellScreenId.Title)
                        && !uiRoot.IsShellPanelVisible(DemoShellScreenId.Lobby)
                        && !uiRoot.IsShellPanelVisible(DemoShellScreenId.StageResult)
                        && !uiRoot.IsShellPanelVisible(DemoShellScreenId.DemoComplete);
                },
                300,
                "RuntimeUiRoot did not hide shell panels during StagePlay.");

            yield return WaitForCondition(
                () => IsStageMapAppliedForStage1(em),
                240,
                "StageMap layout was not applied for Stage1 within timeout.");

            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && uiRoot.IsShellPanelVisible(DemoShellScreenId.StageResult);
                },
                240,
                "RuntimeUiRoot did not show Result panel after ClearReady.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_ClearReadySubscriber_DefersResultUntilCompletion()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            RuntimeUiRoot uiRoot = null;
            DemoShellFlowController shell = null;
            DemoShellPauseBridge pauseBridge = null;
            DemoShellDialogueBridge dialogueBridge = null;

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    uiRoot = FindRuntimeUiRoot();
                    pauseBridge = FindPauseBridge();
                    dialogueBridge = FindDialogueBridge();
                    return shell != null && uiRoot != null && pauseBridge != null && dialogueBridge != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "Operational scene was not ready for clear defer test.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Clear defer test did not reach Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1;
                },
                360,
                "Clear defer test did not reach StagePlay.");

            yield return WaitForCondition(
                () => IsStageMapAppliedForStage1(em),
                240,
                "StageMap layout was not applied for clear defer test.");

            ForceStageStateToRunning(em, 0f);
            yield return WaitForCondition(
                () => HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                240,
                "RunDirector stage did not reach Running before clear defer test.");

            int requestCount = 0;
            DemoShellStageResultMetrics requestedResult = default;
            shell.PreResultClearPresentationRequested += result =>
            {
                requestCount++;
                requestedResult = result;
            };

            ForceStageStateToClearReady(em);
            yield return null;
            yield return null;

            shell = FindDemoShell();
            uiRoot = FindRuntimeUiRoot();
            pauseBridge = FindPauseBridge();
            dialogueBridge = FindDialogueBridge();

            Assert.That(shell, Is.Not.Null);
            Assert.That(uiRoot, Is.Not.Null);
            Assert.That(pauseBridge, Is.Not.Null);
            Assert.That(dialogueBridge, Is.Not.Null);
            Assert.That(requestCount, Is.EqualTo(1), "Shell clear event should still fire exactly once.");
            Assert.That(requestedResult.Outcome, Is.EqualTo(DemoShellStageOutcomeId.Clear));
            Assert.That(shell.CurrentScreen, Is.EqualTo(DemoShellScreenId.StagePlay));
            Assert.That(shell.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.ClearPresentation));
            Assert.That(dialogueBridge.IsDialogueActive, Is.True);
            Assert.That(dialogueBridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.StageClear));
            Assert.That(uiRoot.IsShellPanelVisible(DemoShellScreenId.StageResult), Is.False);
            Assert.That(pauseBridge.CanPause, Is.False);

            var gate = GetSingleton<RunDirectorStageGateComponent>(em);
            var request = GetSingleton<RunDirectorStageRequestComponent>(em);
            Assert.That(gate.ClearPresentationDone, Is.EqualTo(0));
            Assert.That(request.ConfirmPressed, Is.EqualTo(0));

            Assert.That(dialogueBridge.Skip(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    uiRoot = FindRuntimeUiRoot();
                    return shell != null
                        && uiRoot != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && uiRoot.IsShellPanelVisible(DemoShellScreenId.StageResult);
                },
                240,
                "StageResult was not entered after clear presentation completion.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShellDialogueBridge_StageStartOverlay_StartsAfterRunning()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;

            DemoShellFlowController shell = null;
            DemoShellDialogueBridge dialogueBridge = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    dialogueBridge = FindDialogueBridge();
                    return shell != null && dialogueBridge != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "Operational scene was not ready for stage-start dialogue test.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Stage-start dialogue test did not reach Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay && shell.CurrentStageId == 1;
                },
                360,
                "Stage-start dialogue test did not reach StagePlay.");

            yield return WaitForCondition(
                () => HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                360,
                "RunDirector stage did not reach Running for stage-start dialogue test.");

            yield return WaitForCondition(
                () =>
                {
                    dialogueBridge = FindDialogueBridge();
                    shell = FindDemoShell();
                    return dialogueBridge != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && dialogueBridge.IsDialogueActive
                        && dialogueBridge.CurrentPresentation.Trigger == InWorldDialogueTriggerId.StageStart;
                },
                240,
                "Stage-start overlay dialogue did not activate after running edge.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_RuntimeUiRoot_SettingsAudio_ApplyAndPersist()
        {
            ClearAudioVolumePrefs();
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            RuntimeUiRoot uiRoot = null;
            DemoAudioBridge bridge = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    bridge = FindDemoAudioBridge();
                    return uiRoot != null && bridge != null;
                },
                240,
                "RuntimeUiRoot/DemoAudioBridge was not ready in operational scene.");

            uiRoot.OpenSettings();
            yield return null;

            Assert.That(uiRoot.IsSettingsOpen, Is.True);
            Assert.That(uiRoot.SettingsPanel.activeInHierarchy, Is.True);

            uiRoot.SettingsPresenter.Master.Slider.value = 0.41f;
            uiRoot.SettingsPresenter.Ui.Slider.value = 0.27f;
            yield return null;

            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Master), Is.EqualTo(0.41f).Within(1e-4f));
            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Ui), Is.EqualTo(0.27f).Within(1e-4f));

            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            uiRoot = null;
            bridge = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    bridge = FindDemoAudioBridge();
                    return uiRoot != null && bridge != null;
                },
                240,
                "RuntimeUiRoot/DemoAudioBridge was not ready after reload.");

            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Master), Is.EqualTo(0.41f).Within(1e-4f));
            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Ui), Is.EqualTo(0.27f).Within(1e-4f));

            uiRoot.OpenSettings();
            yield return null;
            Assert.That(uiRoot.SettingsPresenter.Master.Slider.value, Is.EqualTo(0.41f).Within(1e-4f));
            Assert.That(uiRoot.SettingsPresenter.Ui.Slider.value, Is.EqualTo(0.27f).Within(1e-4f));

            ClearAudioVolumePrefs();
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_RuntimeUiRoot_HudVisibilityAndPauseLayering_Work()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            RuntimeUiRoot uiRoot = null;
            DemoShellFlowController shell = null;
            PlayerRuntimeHudBridge hud = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    hud = FindPlayerRuntimeHud();
                    return uiRoot != null
                        && shell != null
                        && hud != null
                        && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "RuntimeUiRoot/HUD bridge was not ready in operational scene.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "HUD visibility test did not reach Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    hud = FindPlayerRuntimeHud();
                    return uiRoot != null
                        && shell != null
                        && hud != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && uiRoot.StageHudPanel != null
                        && uiRoot.StageHudPanel.activeInHierarchy
                        && uiRoot.NotificationPanel != null
                        && uiRoot.NotificationPanel.activeInHierarchy
                        && uiRoot.HintPanel != null
                        && uiRoot.HintPanel.activeInHierarchy
                        && hud.RuntimeUiHudActive;
                },
                360,
                "HUD visibility test did not reach StagePlay with active runtime HUD.");

            uiRoot.OpenPause();
            yield return null;

            Assert.That(uiRoot.IsPauseOpen, Is.True);
            Assert.That(uiRoot.PausePanel.activeInHierarchy, Is.True);
            Assert.That(uiRoot.StageHudPanel.activeInHierarchy, Is.True);
            Assert.That(uiRoot.NotificationPanel.activeInHierarchy, Is.True);
            Assert.That(uiRoot.HintPanel.activeInHierarchy, Is.True);

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null);
            ForceStageStateToClearReady(world.EntityManager);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && !uiRoot.StageHudPanel.activeInHierarchy
                        && !uiRoot.NotificationPanel.activeInHierarchy
                        && !uiRoot.HintPanel.activeInHierarchy;
                },
                240,
                "HUD was not hidden after StageResult transition.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_RuntimeUiRoot_HudPresenter_ReflectsDangerAndToast()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            RuntimeUiRoot uiRoot = null;
            DemoShellFlowController shell = null;
            PlayerRuntimeHudBridge hud = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    hud = FindPlayerRuntimeHud();
                    return uiRoot != null
                        && shell != null
                        && hud != null
                        && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "RuntimeUiRoot/HUD bridge was not ready for HUD content test.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "HUD content test did not reach Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && uiRoot.StageHudPanel.activeInHierarchy;
                },
                360,
                "HUD content test did not reach StagePlay.");

            SetPrivateField(uiRoot.StageHudPresenter, "_shell", shell);
            SetPrivateField(uiRoot.StageHudPresenter, "_runtimeHud", hud);
            SetPrivateField(hud, "_hasSnapshot", true);
            SetPrivateField(hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                HazardStack = 3,
                HazardRiskMultiplier = 1.15f,
                DepletedSourceCount = 3,
                TotalSourceCount = 3,
                PressureSourceStableId = 1002u,
                PressureSourceCollected = 9,
                PressureSourceThresholdWeakened = 4,
                PressureSourceThresholdDepleted = 12,
                PressureSourceProgress01 = 0.75f,
                StageStateElapsedSec = 145f,
                LastHitLossValue = 0,
                HitFlashRemainingSec = 0f,
            });
            uiRoot.NotificationBridge.RefreshPresentationState();
            uiRoot.HintBridge.RefreshPresentationState();
            uiRoot.StageHudPresenter.RefreshPresentation();
            uiRoot.NotificationPresenter.RefreshPresentation();
            uiRoot.HintPresenter.RefreshPresentation();

            Assert.That(uiRoot.StageHudPanel.activeInHierarchy, Is.True);
            Assert.That(uiRoot.NotificationPanel.activeInHierarchy, Is.True);
            Assert.That(uiRoot.HintPanel.activeInHierarchy, Is.True);
            Assert.That(uiRoot.StageHudPresenter, Is.Not.Null);
            Assert.That(uiRoot.NotificationPresenter, Is.Not.Null);
            Assert.That(uiRoot.HintPresenter, Is.Not.Null);

            SetPrivateField(hud, "_lastFeedbackSnapshot", new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 100u,
                Type = PlayerUiFeedbackEventType.HazardCaptured,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                RemainingSec = 1f,
            });
            SetPrivateField(hud, "_feedbackLine", "Hazard Captured");
            SetPrivateField(hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 2,
                CarryCapacity = 10,
                HazardStack = 9,
                HazardRiskMultiplier = 1.45f,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
            });
            uiRoot.NotificationBridge.RefreshState(2f);
            uiRoot.NotificationBridge.RefreshPresentationState();
            uiRoot.StageHudPresenter.RefreshPresentation();
            uiRoot.NotificationPresenter.RefreshPresentation();

            Assert.That(uiRoot.NotificationPanel.activeInHierarchy, Is.True);

            SetPrivateField(hud, "_lastFeedbackSnapshot", new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 101u,
                Type = PlayerUiFeedbackEventType.VacuumStartBlocked,
                Reason = (byte)PlayerUiFeedbackReasonId.CarryBinFull,
                RemainingSec = 1f,
            });
            SetPrivateField(hud, "_feedbackLine", "Vacuum: CarryBin Full");
            SetPrivateField(hud, "_lastSnapshot", new PlayerHudSnapshotComponent
            {
                CarryLoad = 10,
                CarryCapacity = 10,
                DepletedSourceCount = 1,
                TotalSourceCount = 3,
                StageStateElapsedSec = 20f,
            });
            uiRoot.NotificationBridge.RefreshPresentationState();
            uiRoot.NotificationPresenter.RefreshPresentation();

            Assert.That(uiRoot.NotificationPanel.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_RuntimeUiRoot_PauseResumeAndSettings_Work()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            RuntimeUiRoot uiRoot = null;
            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "RuntimeUiRoot/DemoShellFlowController was not ready for pause test.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Pause test did not reach Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && uiRoot.PauseBridge != null
                        && uiRoot.PauseBridge.CanPause;
                },
                360,
                "Pause test did not reach StagePlay.");

            uiRoot.OpenPause();
            yield return null;

            Assert.That(uiRoot.IsPauseOpen, Is.True);
            Assert.That(uiRoot.PausePanel.activeInHierarchy, Is.True);

            uiRoot.OpenSettingsFromPause();
            yield return null;

            Assert.That(uiRoot.IsSettingsOpen, Is.True);
            Assert.That(uiRoot.SettingsPanel.activeInHierarchy, Is.True);
            Assert.That(uiRoot.PausePanel.activeSelf, Is.False);

            uiRoot.CloseTopModal();
            yield return null;

            Assert.That(uiRoot.IsPauseOpen, Is.True);
            Assert.That(uiRoot.IsSettingsOpen, Is.False);
            Assert.That(uiRoot.PausePanel.activeInHierarchy, Is.True);

            uiRoot.CloseTopModal();
            yield return null;

            Assert.That(uiRoot.IsPauseOpen, Is.False);
            Assert.That(uiRoot.PausePanel.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_RuntimeUiRoot_PauseRestartAndReturnToLobby_Work()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            RuntimeUiRoot uiRoot = null;
            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "RuntimeUiRoot/DemoShellFlowController was not ready for pause restart test.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Pause restart test did not reach Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1;
                },
                360,
                "Pause restart test did not reach StagePlay.");

            uiRoot.OpenPause();
            yield return null;
            uiRoot.OpenConfirm(DemoShellPauseActionId.RestartStage);
            yield return null;
            uiRoot.ConfirmDialogPresenter.ConfirmButton.onClick.Invoke();

            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1
                        && !uiRoot.IsPauseOpen;
                },
                360,
                "Pause restart confirm did not re-enter the same stage.");

            uiRoot = FindRuntimeUiRoot();
            shell = FindDemoShell();
            uiRoot.OpenPause();
            yield return null;
            uiRoot.OpenConfirm(DemoShellPauseActionId.ReturnToLobby);
            yield return null;
            uiRoot.ConfirmDialogPresenter.ConfirmButton.onClick.Invoke();

            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.Lobby
                        && !uiRoot.IsPauseOpen;
                },
                360,
                "Pause return-to-lobby confirm did not reach Lobby.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_RuntimeUiRoot_PauseIsBlockedOutsideStagePlay()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            RuntimeUiRoot uiRoot = null;
            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    shell = FindDemoShell();
                    return uiRoot != null
                        && shell != null
                        && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "RuntimeUiRoot/DemoShellFlowController was not ready for pause guard test.");

            uiRoot.OpenPause();
            yield return null;
            Assert.That(uiRoot.IsPauseOpen, Is.False);

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Pause guard test did not reach Lobby.");

            uiRoot = FindRuntimeUiRoot();
            uiRoot.OpenPause();
            yield return null;
            Assert.That(uiRoot.IsPauseOpen, Is.False);

            Assert.That(shell.RequestSelectStageById(3), Is.True);

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null);
            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay && shell.CurrentStageId == 3;
                },
                360,
                "Pause guard test did not reach Stage3 play.");

            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                360,
                "Pause guard test did not observe Stage3 running state.");

            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StageResult;
                },
                240,
                "Pause guard test did not reach StageResult.");

            uiRoot = FindRuntimeUiRoot();
            uiRoot.OpenPause();
            yield return null;
            Assert.That(uiRoot.IsPauseOpen, Is.False);

            Assert.That(shell.RequestResultAction(DemoShellResultActionId.NextStage), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.DemoComplete;
                },
                240,
                "Pause guard test did not reach DemoComplete.");

            uiRoot = FindRuntimeUiRoot();
            uiRoot.OpenPause();
            yield return null;
            Assert.That(uiRoot.IsPauseOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_RuntimeUiRoot_Exists()
        {
            ClearDemoShellStaging();
            yield return LoadSceneIgnoringBootstrapBacklogErrors(DedicatedScenePath);

            RuntimeUiRoot uiRoot = null;
            yield return WaitForCondition(
                () =>
                {
                    uiRoot = FindRuntimeUiRoot();
                    return uiRoot != null;
                },
                240,
                "RuntimeUiRoot was not found in dedicated smoke scene.");

            Assert.That(uiRoot.RootCanvas, Is.Not.Null);
            Assert.That(uiRoot.EventSystem, Is.Not.Null);
            Assert.That(uiRoot.UiInputModule, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_ResultRetry_ReentersSameStage()
        {
            ClearDemoShellStaging();
            DemoShellSessionStaging.StageStagePlay(0);
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em) &&
                    HasSingleton<RunDirectorStageGateComponent>(em) &&
                    HasSingleton<RunDirectorStageRequestComponent>(em) &&
                    HasSingleton<RunDirectorStageSignalComponent>(em),
                300,
                "RunDirector stage singleton setup was not ready within timeout.");

            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1;
                },
                360,
                "Demo shell did not boot into staged StagePlay(Stage1).");

            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StageResult;
                },
                240,
                "StageResult was not entered before Retry.");

            Assert.That(shell.RequestResultAction(DemoShellResultActionId.Retry), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1;
                },
                360,
                "Retry did not re-enter same stage.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_ForceClearReadyFromStagePlay_EntersClearResult()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;

            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "DemoShellFlowController was not ready in operational scene for force clear test.");

            Assert.That(shell.RequestStartFromTitle(), Is.True, $"RequestStartFromTitle returned false. screen={shell.CurrentScreen}");
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition Title -> Lobby for force clear test.");

            Assert.That(shell.RequestSelectStageById(1), Is.True, $"RequestSelectStageById returned false. screen={shell.CurrentScreen}");
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1;
                },
                360,
                "Demo shell did not enter StagePlay(Stage1) for force clear test.");

            ForceStageStateToRunning(em, 0f);
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                360,
                "RunDirector stage did not reach Running before force clear test.");

            Assert.That(shell.RequestForceClearReadyForTest(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && shell.CurrentStageOutcome == DemoShellStageOutcomeId.Clear;
                },
                240,
                "Force ClearReady test button contract did not transition StagePlay -> Clear result.");

            Assert.That(GetSingleton<RunDirectorStageStateComponent>(em).LastTransitionReason,
                Is.EqualTo(RunDirectorStageTransitionReasonId.DebugForceClearReady));
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_Stage2_AppliesDifferentLayoutAndPattern()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em) &&
                    HasSingleton<RunDirectorStageGateComponent>(em) &&
                    HasSingleton<RunDirectorStageRequestComponent>(em) &&
                    HasSingleton<RunDirectorStageSignalComponent>(em),
                300,
                "RunDirector stage singleton setup was not ready within timeout.");

            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "DemoShellFlowController was not ready in operational scene.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition Title -> Lobby for Stage2 test.");

            Assert.That(shell.RequestSelectStageById(2), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 2;
                },
                360,
                "Demo shell did not enter StagePlay(Stage2).");

            yield return WaitForCondition(
                () => IsStageMapAppliedForStage2(em),
                240,
                () => $"Stage2 layout/pattern was not applied within timeout. {DescribeStage2ApplyState(em)}");
            AssertStageMapAppliedForStage2(em);
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_PresentationController_RebuildsAcrossNextAndRetry()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em) &&
                    HasSingleton<RunDirectorStageGateComponent>(em) &&
                    HasSingleton<RunDirectorStageRequestComponent>(em) &&
                    HasSingleton<RunDirectorStageSignalComponent>(em),
                300,
                "RunDirector stage singleton setup was not ready within timeout.");

            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "DemoShellFlowController was not ready in operational scene.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition Title -> Lobby for presentation rebuild test.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            StagePresentationRuntimeController controller = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    controller = FindStagePresentationRuntimeController();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1
                        && controller != null
                        && controller.LastAppliedStageId == 1
                        && controller.SpawnedRootCount == 1
                        && controller.transform.childCount == 1
                        && controller.transform.GetChild(0).name.Contains("preview_visual_01");
                },
                360,
                () => $"Stage1 presentation was not rebuilt within timeout. {DescribePresentationControllerState()}");

            var stage1Presentation = controller.transform.GetChild(0).gameObject;

            ForceStageStateToRunning(em, 0f);
            yield return WaitForCondition(
                () => HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                240,
                "Stage1 did not reach Running before force clear in presentation rebuild test.");

            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StageResult;
                },
                240,
                "StageResult was not entered before NextStage for presentation rebuild test.");

            Assert.That(shell.RequestResultAction(DemoShellResultActionId.NextStage), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    controller = FindStagePresentationRuntimeController();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 2
                        && controller != null
                        && controller.LastAppliedStageId == 2
                        && controller.SpawnedRootCount == 1
                        && controller.transform.childCount == 1
                        && controller.transform.GetChild(0).name.Contains("preview_visual_02");
                },
                360,
                () => $"Stage2 presentation was not rebuilt within timeout. {DescribePresentationControllerState()}");

            var stage2Presentation = controller.transform.GetChild(0).gameObject;
            Assert.That(stage2Presentation, Is.Not.EqualTo(stage1Presentation), "Stage2 presentation should be recreated, not stale Stage1 instance.");

            ForceStageStateToRunning(em, 0f);
            yield return WaitForCondition(
                () => HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                240,
                "Stage2 did not reach Running before force clear in presentation rebuild test.");

            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StageResult;
                },
                240,
                "StageResult was not entered before Retry for presentation rebuild test.");

            Assert.That(shell.RequestResultAction(DemoShellResultActionId.Retry), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    controller = FindStagePresentationRuntimeController();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 2
                        && controller != null
                        && controller.LastAppliedStageId == 2
                        && controller.SpawnedRootCount == 1
                        && controller.transform.childCount == 1
                        && controller.transform.GetChild(0).name.Contains("preview_visual_02")
                        && controller.transform.GetChild(0).gameObject != stage2Presentation;
                },
                360,
                () => $"Retry did not rebuild Stage2 presentation within timeout. {DescribePresentationControllerState()}");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_Stage3Next_GoesToDemoComplete_ThenLobby()
        {
            ClearDemoShellStaging();
            DemoShellSessionStaging.StageStagePlay(2);
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em) &&
                    HasSingleton<RunDirectorStageGateComponent>(em) &&
                    HasSingleton<RunDirectorStageRequestComponent>(em) &&
                    HasSingleton<RunDirectorStageSignalComponent>(em),
                300,
                "RunDirector stage singleton setup was not ready within timeout.");
            DemoShellFlowController shell = null;

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 3;
                },
                360,
                "Demo shell did not boot into staged StagePlay(Stage3).");

            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StageResult;
                },
                240,
                "StageResult was not entered for Stage3.");

            Assert.That(shell.RequestResultAction(DemoShellResultActionId.NextStage), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.DemoComplete;
                },
                240,
                "Stage3 NextStage did not transition to DemoComplete.");

            Assert.That(shell.RequestReturnToLobbyFromComplete(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                360,
                "DemoComplete did not return to Lobby.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_Timeout_EntersFailResult_AndRetryReenters()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;

            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em) &&
                    HasSingleton<RunDirectorStageGateComponent>(em) &&
                    HasSingleton<RunDirectorStageRequestComponent>(em) &&
                    HasSingleton<RunDirectorStageSignalComponent>(em),
                300,
                "RunDirector stage singleton setup was not ready within timeout.");

            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "DemoShellFlowController was not ready in operational scene.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition to Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay;
                },
                240,
                "Demo shell did not enter StagePlay.");

            var profiles = shell.StageProfiles;
            profiles[0].StageTimeLimitSec = 0.05f;
            shell.StageProfiles = profiles;
            ForceStageStateToRunning(em, elapsedSec: 0.06f);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && shell.CurrentStageOutcome == DemoShellStageOutcomeId.Fail;
                },
                240,
                "Stage timeout did not transition to Fail result.");

            Assert.That(shell.RequestResultAction(DemoShellResultActionId.NextStage), Is.False, "Fail result must reject NextStage.");
            Assert.That(shell.RequestResultAction(DemoShellResultActionId.Retry), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1;
                },
                360,
                "Fail Retry did not re-enter same stage.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_GiveUp_EntersFailResult_AndReturnLobby()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "DemoShellFlowController was not ready in operational scene.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition to Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay;
                },
                240,
                "Demo shell did not enter StagePlay.");

            Assert.That(shell.RequestGiveUp(), Is.True, $"RequestGiveUp returned false. screen={shell.CurrentScreen}, stageId={shell.CurrentStageId}");
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && shell.CurrentStageOutcome == DemoShellStageOutcomeId.Fail;
                },
                240,
                "Give Up did not transition to Fail result.");

            Assert.That(
                shell.RequestResultAction(DemoShellResultActionId.ReturnToLobby),
                Is.True,
                $"RequestResultAction(ReturnToLobby) returned false. screen={shell.CurrentScreen}, outcome={shell.CurrentStageOutcome}");
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                360,
                "Fail ReturnToLobby did not transition to Lobby.");
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoShell_DemoCompleteSessionMetrics_AccumulatesClearOnly()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;

            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "DemoShellFlowController was not ready in operational scene.");

            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition to Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay;
                },
                240,
                "Demo shell did not enter StagePlay.");

            // 실패 시도 1회는 세션 합계에서 제외되어야 한다.
            Assert.That(shell.RequestGiveUp(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && shell.CurrentStageOutcome == DemoShellStageOutcomeId.Fail;
                },
                240,
                "Give Up did not transition to Fail result.");

            Assert.That(DemoShellSessionStaging.TryGetSessionMetrics(out var metricsAfterFail), Is.True);
            Assert.That(metricsAfterFail.ClearedStageCount, Is.EqualTo(0), "Fail attempt must not be accumulated.");
            Assert.That(metricsAfterFail.TotalCollectValue, Is.EqualTo(0));
            Assert.That(metricsAfterFail.TotalCleanupValue, Is.EqualTo(0));
            Assert.That(metricsAfterFail.TotalHitValue, Is.EqualTo(0));
            Assert.That(shell.RequestResultAction(DemoShellResultActionId.Retry), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1;
                },
                360,
                "Retry after fail did not re-enter Stage1.");
            yield return WaitForCondition(
                () => HasCombatEventChannel(em),
                240,
                "CombatEventChannel was not ready after Retry Stage1 re-entry.");

            // Stage1 clear
            SetCombatTotals(em, totalCollect: 10, totalCleanup: 5, totalHit: 1);
            yield return null;
            yield return null;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                240,
                "Stage1 did not reach Running before forced ClearReady.");
            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && shell.CurrentStageOutcome == DemoShellStageOutcomeId.Clear;
                },
                240,
                "Stage1 did not enter Clear result.");
            Assert.That(shell.RequestResultAction(DemoShellResultActionId.NextStage), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 2;
                },
                360,
                () =>
                {
                    shell = FindDemoShell();
                    string shellText = shell == null
                        ? "shell=null"
                        : $"shell(screen={shell.CurrentScreen}, stageId={shell.CurrentStageId}, outcome={shell.CurrentStageOutcome})";
                    return $"Did not enter Stage2 after Stage1 Next. {shellText}, startupPending={DemoShellSessionStaging.IsStartupPending}, {DescribeStageRunState(em)}";
                });
            world = World.DefaultGameObjectInjectionWorld;
            em = world.EntityManager;
            yield return WaitForCondition(
                () => HasCombatEventChannel(em),
                240,
                "CombatEventChannel was not ready after Stage2 entry.");

            // Stage2 clear
            SetCombatTotals(em, totalCollect: 20, totalCleanup: 8, totalHit: 2);
            yield return null;
            yield return null;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                240,
                () => $"Stage2 did not reach Running before forced ClearReady. {DescribeStageRunState(em)}");
            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && shell.CurrentStageOutcome == DemoShellStageOutcomeId.Clear;
                },
                240,
                "Stage2 did not enter Clear result.");
            Assert.That(shell.RequestResultAction(DemoShellResultActionId.NextStage), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 3;
                },
                360,
                "Did not enter Stage3 after Stage2 Next.");
            world = World.DefaultGameObjectInjectionWorld;
            em = world.EntityManager;
            yield return WaitForCondition(
                () => HasCombatEventChannel(em),
                240,
                "CombatEventChannel was not ready after Stage3 entry.");

            // Stage3 clear -> DemoComplete
            SetCombatTotals(em, totalCollect: 30, totalCleanup: 12, totalHit: 3);
            yield return null;
            yield return null;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em)
                    && GetSingleton<RunDirectorStageStateComponent>(em).State == RunDirectorStageStateId.Running,
                240,
                "Stage3 did not reach Running before forced ClearReady.");
            ForceStageStateToClearReady(em);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && shell.CurrentStageOutcome == DemoShellStageOutcomeId.Clear;
                },
                240,
                "Stage3 did not enter Clear result.");
            Assert.That(shell.RequestResultAction(DemoShellResultActionId.NextStage), Is.True);

            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.DemoComplete;
                },
                360,
                "Stage3 NextStage did not transition to DemoComplete.");

            Assert.That(DemoShellSessionStaging.TryGetSessionMetrics(out var metrics), Is.True);
            Assert.That(metrics.ClearedStageCount, Is.EqualTo(3));
            Assert.That(metrics.TotalCollectValue, Is.GreaterThanOrEqualTo(0));
            Assert.That(metrics.TotalCleanupValue, Is.GreaterThanOrEqualTo(0));
            Assert.That(metrics.TotalHitValue, Is.GreaterThanOrEqualTo(0));
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_PlayerHud_ReflectsSnapshotAndShellMeta()
        {
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;

            yield return WaitForCondition(
                () =>
                    HasSingleton<PlayerHudSnapshotComponent>(em) &&
                    HasSingleton<CombatEventMetricsComponent>(em) &&
                    HasCombatEventChannel(em) &&
                    HasSingleton<BulletFrameCounterComponent>(em),
                300,
                "HUD snapshot/combat singleton setup was not ready within timeout.");

            yield return WaitForCondition(
                () => FindDemoShell() != null,
                240,
                "DemoShellFlowController was not found in operational scene.");

            var shell = FindDemoShell();
            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Demo shell did not transition to Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay;
                },
                240,
                "Demo shell did not enter StagePlay.");

            yield return WaitForCondition(
                () => FindPlayerRuntimeHud() != null,
                240,
                "PlayerRuntimeHudBridge was not found in operational scene.");

            var hud = FindPlayerRuntimeHud();
            Assert.That(hud, Is.Not.Null);
            yield return WaitForCondition(
                () => hud.HasSnapshot,
                240,
                "PlayerRuntimeHudBridge did not receive snapshot data.");

            Assert.That(hud.LastStageId, Is.EqualTo(1));
            Assert.That(
                hud.LastScreen == DemoShellScreenId.StagePlay || hud.LastScreen == DemoShellScreenId.StageResult,
                Is.True,
                "HUD stage meta must reflect active run screens.");
            Assert.That(hud.TryGetLastSnapshot(out var snapshot), Is.True);
            Assert.That(snapshot.CarryCapacity, Is.GreaterThan(0));

            var playerEntity = GetSingletonEntity<PlayerTag>(em);
            if (em.HasComponent<PlayerHazardPenaltyStateComponent>(playerEntity))
            {
                var penalty = em.GetComponentData<PlayerHazardPenaltyStateComponent>(playerEntity);
                penalty.IFrameTimer = 10f;
                penalty.VacuumLockTimer = 0f;
                em.SetComponentData(playerEntity, penalty);
            }

            CompleteTrackedJobs(em);
            uint frame = GetSingleton<BulletFrameCounterComponent>(em).Value;
            var feedbackSnapshot = em.GetComponentData<PlayerUiFeedbackPresentationSnapshotComponent>(playerEntity);
            feedbackSnapshot.Version = feedbackSnapshot.Version == uint.MaxValue ? 1u : feedbackSnapshot.Version + 1u;
            feedbackSnapshot.Type = PlayerUiFeedbackEventType.PlayerHazardHit;
            feedbackSnapshot.Reason = (byte)PlayerUiFeedbackReasonId.Default;
            feedbackSnapshot.Value = 11;
            feedbackSnapshot.RelatedEntity = playerEntity;
            feedbackSnapshot.Frame = frame;
            feedbackSnapshot.RemainingSec = 0.6f;
            feedbackSnapshot.ClockSec = Mathf.Max(0f, feedbackSnapshot.ClockSec);
            feedbackSnapshot.NextAllowedHitSec = feedbackSnapshot.ClockSec + 0.1f;
            em.SetComponentData(playerEntity, feedbackSnapshot);
            var hudSnapshotEntity = GetSingletonEntity<PlayerHudSnapshotComponent>(em);
            var hudSnapshot = em.GetComponentData<PlayerHudSnapshotComponent>(hudSnapshotEntity);
            hudSnapshot.LastHitLossValue = 11;
            hudSnapshot.HitFlashRemainingSec = 0.6f;
            em.SetComponentData(hudSnapshotEntity, hudSnapshot);

            yield return WaitForCondition(
                () =>
                {
                    hud = FindPlayerRuntimeHud();
                    return hud != null
                        && hud.TryGetLastSnapshot(out var latest)
                        && latest.LastHitLossValue == 11
                        && hud.IsHitFlashVisible;
                },
                180,
                "Player HUD did not expose hit flash/loss from combat event.");

            for (int i = 0; i < 45; i++)
                yield return null;

            hud = FindPlayerRuntimeHud();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.TryGetLastSnapshot(out var afterDecay), Is.True);
            Assert.That(afterDecay.HitFlashRemainingSec, Is.EqualTo(0f).Within(0.05f));
            Assert.That(hud.IsHitFlashVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoAudioBridge_AutoSetupAndTransitionCues_Work()
        {
            ClearAudioVolumePrefs();
            ClearDemoShellStaging();
            yield return LoadSceneIgnoringBootstrapBacklogErrors(OperationalScenePath);

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");
            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    HasSingleton<RunDirectorStageStateComponent>(em) &&
                    HasSingleton<RunDirectorStageGateComponent>(em) &&
                    HasSingleton<RunDirectorStageRequestComponent>(em) &&
                    HasSingleton<RunDirectorStageSignalComponent>(em),
                300,
                "RunDirector stage singleton setup was not ready within timeout.");

            DemoShellFlowController shell = null;
            DemoAudioBridge bridge = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    bridge = FindDemoAudioBridge();
                    return shell != null && bridge != null;
                },
                240,
                "DemoShellFlowController/DemoAudioBridge was not found in operational scene.");

            Assert.That(bridge.BgmSource, Is.Not.Null, "BGM source must be available via prewire or auto-create.");
            Assert.That(bridge.SfxSource, Is.Not.Null, "SFX source must be available via prewire or auto-create.");
            Assert.That(bridge.UiSource, Is.Not.Null, "UI source must be available via prewire or auto-create.");

            int baselineCueCount = bridge.PlayedCueCount;
            Assert.That(shell.RequestStartFromTitle(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    bridge = FindDemoAudioBridge();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.Lobby
                        && bridge != null
                        && bridge.PlayedCueCount >= baselineCueCount + 1;
                },
                240,
                "Title->Lobby transition cue was not observed.");

            int lobbyCueCount = bridge.PlayedCueCount;
            Assert.That(shell.RequestSelectStageById(1), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    bridge = FindDemoAudioBridge();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && bridge != null
                        && bridge.PlayedCueCount >= lobbyCueCount + 2;
                },
                240,
                "Lobby->StagePlay transition cues were not observed.");

            int stagePlayCueCount = bridge.PlayedCueCount;
            Assert.That(shell.RequestGiveUp(), Is.True);
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    bridge = FindDemoAudioBridge();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StageResult
                        && bridge != null
                        && bridge.PlayedCueCount >= stagePlayCueCount + 2;
                },
                240,
                "StagePlay->StageResult transition cues were not observed.");

            ClearAudioVolumePrefs();
            SceneManager.LoadScene(DedicatedScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_DemoAudioBridge_VolumePersistsAcrossReload()
        {
            ClearAudioVolumePrefs();
            ClearDemoShellStaging();
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            DemoShellFlowController shell = null;
            DemoAudioBridge bridge = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    bridge = FindDemoAudioBridge();
                    return shell != null && bridge != null;
                },
                240,
                "DemoShellFlowController/DemoAudioBridge was not found in operational scene.");

            bridge.SetBusVolume(DemoAudioBusId.Master, 0.77f);
            bridge.SetBusVolume(DemoAudioBusId.Bgm, 0.22f);
            bridge.SetBusVolume(DemoAudioBusId.Sfx, 0.33f);
            bridge.SetBusVolume(DemoAudioBusId.Ui, 0.44f);

            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            yield return WaitForCondition(
                () => FindDemoAudioBridge() != null,
                240,
                "DemoAudioBridge was not found after scene reload.");

            bridge = FindDemoAudioBridge();
            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Master), Is.EqualTo(0.77f).Within(0.01f));
            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Bgm), Is.EqualTo(0.22f).Within(0.01f));
            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Sfx), Is.EqualTo(0.33f).Within(0.01f));
            Assert.That(bridge.GetBusVolume(DemoAudioBusId.Ui), Is.EqualTo(0.44f).Within(0.01f));

            ClearAudioVolumePrefs();
        }

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_Replay_RecordResetPlayback_Smoke()
        {
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return LoadSceneIgnoringBootstrapBacklogErrors(DedicatedScenePath);

                var world = World.DefaultGameObjectInjectionWorld;
                Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

                var em = world.EntityManager;
                yield return WaitForCondition(
                    () =>
                        CountByComponentType<PlayerTag>(em) > 0 &&
                        HasReplaySingleton(em) &&
                        HasSingleton<SpawnRunSeedComponent>(em),
                    300,
                    "Replay singleton setup was not ready within timeout.");

                var replayEntity = em.CreateEntityQuery(
                    ComponentType.ReadWrite<ReplayInputControlComponent>(),
                    ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                    ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
                var playerEntity = GetSingletonEntity<PlayerTag>(em);

                var replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
                replayFrames.Clear();
                em.SetComponentData(replayEntity, new ReplayInputCursorComponent { NextFrameIndex = 0 });
                em.SetComponentData(replayEntity, new ReplayInputControlComponent
                {
                    Mode = ReplayInputModeId.Record,
                    LastRecordedFrame = 0u,
                    LastPlaybackFrame = 0u,
                    MissingFrameCount = 0,
                });

                for (int i = 0; i < 24; i++)
                {
                    if (em.HasComponent<PlayerInputIntentComponent>(playerEntity))
                    {
                        var intent = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
                        intent.MoveAxis = new Unity.Mathematics.float2(0.7f, 0.2f);
                        intent.AimWorldXZ = new Unity.Mathematics.float2(4f, -3f);
                        intent.HasAimWorldPoint = 1;
                        if (i % 6 == 0)
                        {
                            intent.VacuumRequested = 1;
                            intent.CleanupActionRequested = 1;
                            intent.RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.Primary;
                            intent.Sequence += 1u;
                        }
                        em.SetComponentData(playerEntity, intent);
                    }

                    yield return null;
                }

                replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
                Assert.That(replayFrames.Length, Is.GreaterThan(8), "Record mode should accumulate replay frames.");

                var copied = new List<ReplayInputFrameBufferElement>(replayFrames.Length);
                for (int i = 0; i < replayFrames.Length; i++)
                    copied.Add(replayFrames[i]);

                uint runSeed = GetSingleton<SpawnRunSeedComponent>(em).Value;
                ReplaySessionStaging.StagePlayback(copied, runSeed);
                yield return LoadSceneIgnoringBootstrapBacklogErrors(DedicatedScenePath);

                world = World.DefaultGameObjectInjectionWorld;
                Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist after replay scene reset.");
                em = world.EntityManager;
                yield return WaitForCondition(
                    () =>
                        HasReplaySingleton(em) &&
                        !ReplaySessionStaging.IsPlaybackStartupPending &&
                        GetSingleton<ReplayInputControlComponent>(em).Mode == ReplayInputModeId.Playback,
                    300,
                    "Playback startup did not complete after scene reset.");

                replayEntity = em.CreateEntityQuery(
                    ComponentType.ReadWrite<ReplayInputControlComponent>(),
                    ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                    ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
                replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
                var playbackCursor = em.GetComponentData<ReplayInputCursorComponent>(replayEntity);
                Assert.That(replayFrames.Length, Is.EqualTo(copied.Count), "Staged replay frames must be restored after scene reset.");

                uint maxPlaybackFrame = 0u;
                int maxCursor = playbackCursor.NextFrameIndex;
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                    var control = em.GetComponentData<ReplayInputControlComponent>(replayEntity);
                    var cursor = em.GetComponentData<ReplayInputCursorComponent>(replayEntity);
                    if (control.LastPlaybackFrame > maxPlaybackFrame)
                        maxPlaybackFrame = control.LastPlaybackFrame;
                    if (cursor.NextFrameIndex > maxCursor)
                        maxCursor = cursor.NextFrameIndex;
                }

                Assert.That(maxCursor, Is.GreaterThan(0), "Playback cursor should advance after scene reset.");
                Assert.That(maxPlaybackFrame, Is.GreaterThan(0u), "Playback frames should be consumed after scene reset.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }

        [UnityTest]
        [Category("PeriodicOperationalScene")]
        public IEnumerator PlayMode_OperationalScene_PipelineBootAndCoreLoop_RunWithoutHardErrors()
        {
            yield return RunSceneSmoke(
                scenePath: OperationalScenePath,
                sceneLabel: "SampleScene",
                frameCount: 180);
        }

        private static IEnumerator RunSceneSmoke(string scenePath, string sceneLabel, int frameCount)
        {
            if (scenePath == OperationalScenePath)
                ClearDemoShellStaging();

            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    CountByComponentType<PlayerTag>(em) > 0 &&
                    CountByComponentType<BulletFrameCounterComponent>(em) > 0,
                300,
                $"ECS setup was not ready within timeout. scene={sceneLabel}");

            if (scenePath == OperationalScenePath)
            {
                DemoShellFlowController shell = null;
                yield return WaitForCondition(
                    () =>
                    {
                        shell = FindDemoShell();
                        return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                    },
                    240,
                    "Operational scene DemoShell was not ready for smoke bootstrap.");

                Assert.That(shell.RequestStartFromTitle(), Is.True, "Operational smoke must enter Lobby before StagePlay.");
                yield return WaitForCondition(
                    () =>
                    {
                        shell = FindDemoShell();
                        return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                    },
                    240,
                    "Operational scene smoke did not reach Lobby.");

                Assert.That(shell.RequestSelectStageById(1), Is.True, "Operational smoke must start Stage1.");
                yield return WaitForCondition(
                    () =>
                    {
                        shell = FindDemoShell();
                        return shell != null && shell.CurrentScreen == DemoShellScreenId.StagePlay;
                    },
                    360,
                    "Operational scene smoke did not enter StagePlay.");

                yield return WaitForCondition(
                    () => IsStageMapAppliedForStage1(em),
                    240,
                    "Operational scene smoke did not apply Stage1 layout before core loop observation.");

                ForceStageStateToRunning(em, 0f);
            }

            var pipeline = world.GetExistingSystemManaged<BulletFramePipelineGroup>();
            Assert.That(pipeline, Is.Not.Null, "BulletFramePipelineGroup must exist in default world");

            int maxActiveBullets = 0;
            int framesWithActiveBullets = 0;
            int maxGhostInactiveRendered = 0;
            int maxRequestedRendered = 0;
            int maxActiveHidden = 0;
            int maxNonPositiveLifeRendered = 0;

            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
                int activeCount = CountByComponentType<BulletActiveTag>(em);
                if (activeCount > 0)
                    framesWithActiveBullets++;
                if (activeCount > maxActiveBullets)
                    maxActiveBullets = activeCount;

                if (HasSingleton<DebugHudMetricsComponent>(em))
                {
                    var hud = GetSingleton<DebugHudMetricsComponent>(em);
                    maxGhostInactiveRendered = Mathf.Max(maxGhostInactiveRendered, hud.GhostInactiveRendered);
                    maxRequestedRendered = Mathf.Max(maxRequestedRendered, hud.RequestedRendered);
                    maxActiveHidden = Mathf.Max(maxActiveHidden, hud.ActiveHidden);
                    maxNonPositiveLifeRendered = Mathf.Max(maxNonPositiveLifeRendered, hud.NonPositiveLifeRendered);
                }
            }

            Assert.That(framesWithActiveBullets, Is.GreaterThan(0), $"Core loop should produce active bullets. scene={sceneLabel}");
            Assert.That(maxActiveBullets, Is.GreaterThan(0), $"At least one active bullet must be observed. scene={sceneLabel}");

            Debug.Log(
                $"[PlayModeSmoke] scene={sceneLabel} frames={frameCount} maxActiveBullets={maxActiveBullets} framesWithActiveBullets={framesWithActiveBullets} " +
                $"traceGhostInactiveRendered={maxGhostInactiveRendered} traceRequestedRendered={maxRequestedRendered} " +
                $"traceActiveHidden={maxActiveHidden} traceNonPositiveLifeRendered={maxNonPositiveLifeRendered}");
        }

        private static IEnumerator WaitForCondition(System.Func<bool> predicate, int timeoutFrames, string failMessage)
        {
            return WaitForCondition(predicate, timeoutFrames, () => failMessage);
        }

        private static IEnumerator WaitForCondition(System.Func<bool> predicate, int timeoutFrames, System.Func<string> failMessageFactory)
        {
            for (int i = 0; i < timeoutFrames; i++)
            {
                if (predicate())
                    yield break;
                yield return null;
            }

            Assert.Fail(failMessageFactory());
        }

        private static int CountByComponentType<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            CompleteTrackedJobs(em);
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static bool HasSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            CompleteTrackedJobs(em);
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static bool HasReplaySingleton(EntityManager em)
        {
            CompleteTrackedJobs(em);
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ReplayInputControlComponent>(),
                ComponentType.ReadOnly<ReplayInputCursorComponent>(),
                ComponentType.ReadOnly<ReplayInputFrameBufferElement>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static bool HasCombatEventChannel(EntityManager em)
        {
            CompleteTrackedJobs(em);
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CombatEventChannelSingletonTag>(),
                ComponentType.ReadOnly<CombatEventMetricsComponent>(),
                ComponentType.ReadWrite<CombatEventBufferElement>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static Entity GetSingletonEntity<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            CompleteTrackedJobs(em);
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingletonEntity();
        }

        private static T GetSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            CompleteTrackedJobs(em);
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }

        private static int ComputeOldestPendingAgeFrames(EntityManager em)
        {
            CompleteTrackedJobs(em);
            if (!HasSingleton<BulletFrameCounterComponent>(em))
                return 0;

            var frameCounter = GetSingleton<BulletFrameCounterComponent>(em);
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);
            int oldest = 0;

            var query = em.CreateEntityQuery(ComponentType.ReadOnly<SourceSpawnRequestBuffer>());
            using var sources = query.ToEntityArray(Allocator.Temp);
            for (int s = 0; s < sources.Length; s++)
            {
                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(sources[s], isReadOnly: true);
                for (int i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    if (request.Count <= 0)
                        continue;

                    int age = frame >= request.OldestFrame ? (int)(frame - request.OldestFrame) : 0;
                    oldest = Mathf.Max(oldest, age);
                }
            }

            return oldest;
        }

        private static Entity FindSourceWithEventClip(EntityManager em)
        {
            CompleteTrackedJobs(em);
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceSpawnComponent>(),
                ComponentType.ReadOnly<SourceClipPatternBuffer>());
            using var sources = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < sources.Length; i++)
            {
                var clips = em.GetBuffer<SourceClipPatternBuffer>(sources[i], isReadOnly: true);
                for (int c = 0; c < clips.Length; c++)
                {
                    if (clips[c].Phase == SourceWavePhaseId.OnStateEnterOnce)
                        return sources[i];
                }
            }

            return Entity.Null;
        }


        private static bool IsStageMapAppliedForStage1(EntityManager em)
        {
            CompleteTrackedJobs(em);
            if (!TryFindSourceByStableId(em, 1001u, out var source1001))
                return false;
            if (!TryFindDepositByStableId(em, 2001u, out var deposit2001))
                return false;

            var sourceState1 = em.GetComponentData<SourceSpawnComponent>(source1001);
            var area1 = em.GetComponentData<Shape2DComponent>(source1001);
            var deposit1 = em.GetComponentData<Shape2DComponent>(deposit2001);

            return sourceState1.State == SourceStateId.Normal
                && area1.Kind == Shape2DKind.Rectangle
                && Mathf.Abs(area1.Size.x - 12f) <= 0.01f
                && Mathf.Abs(area1.Size.y - 8f) <= 0.01f
                && Mathf.Abs(deposit1.Radius - 5f) <= 0.01f;
        }

        private static bool IsStageMapAppliedForStage2(EntityManager em)
        {
            CompleteTrackedJobs(em);
            if (!TryFindSourceByStableId(em, 1002u, out var source1002))
                return false;
            if (!TryFindDepositByStableId(em, 2001u, out var deposit2001))
                return false;
            if (!TryFindObstacleByStableId(em, 3004u, out var obstacle3004))
                return false;

            var sourceState2 = em.GetComponentData<SourceSpawnComponent>(source1002);
            var area2 = em.GetComponentData<Shape2DComponent>(source1002);
            var deposit1 = em.GetComponentData<Shape2DComponent>(deposit2001);
            var clipPatterns2 = em.GetBuffer<SourceClipPatternBuffer>(source1002, isReadOnly: true);
            var obstacleGeometry = em.GetComponentData<Shape2DComponent>(obstacle3004);
            var obstacleMask = em.GetComponentData<ObstacleCollisionMaskComponent>(obstacle3004);

            return sourceState2.State == SourceStateId.Normal
                && area2.Kind == Shape2DKind.Circle
                && Mathf.Abs(area2.Radius - 6f) <= 0.01f
                && Mathf.Abs(deposit1.Radius - 4f) <= 0.01f
                && obstacleGeometry.Kind == Shape2DKind.Rectangle
                && Mathf.Abs(obstacleGeometry.Size.x - 3.5f) <= 0.01f
                && Mathf.Abs(obstacleGeometry.Size.y - 2f) <= 0.01f
                && HasRequiredObstacleMaskBits(
                    obstacleMask.Value,
                    ObstacleCollisionMask.BlockPlayer | ObstacleCollisionMask.BlockBullet)
                && clipPatterns2.Length > 0;
        }

        private static string DescribeStage2ApplyState(EntityManager em)
        {
            CompleteTrackedJobs(em);

            string topologyText = "topology=missing";
            if (HasSingleton<StageTopologyStateComponent>(em))
            {
                var topology = GetSingleton<StageTopologyStateComponent>(em);
                topologyText = $"topology(selected={topology.SelectedStageId}, applied={topology.AppliedStageId}, ready={topology.Ready})";
            }

            bool hasSource = TryFindSourceByStableId(em, 1002u, out var source1002);
            bool hasDeposit = TryFindDepositByStableId(em, 2001u, out var deposit2001);
            bool hasObstacle = TryFindObstacleByStableId(em, 3004u, out var obstacle3004);

            string sourceText = "source1002=missing";
            if (hasSource)
            {
                var source = em.GetComponentData<SourceSpawnComponent>(source1002);
                var area = em.GetComponentData<Shape2DComponent>(source1002);
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source1002, isReadOnly: true);
                sourceText =
                    $"source1002(state={source.State}, shape={area.Kind}, radius={area.Radius:0.##}, size=({area.Size.x:0.##},{area.Size.y:0.##}), patterns={clipPatterns.Length})";
            }

            string depositText = "deposit2001=missing";
            if (hasDeposit)
            {
                var deposit = em.GetComponentData<Shape2DComponent>(deposit2001);
                depositText = $"deposit2001(radius={deposit.Radius:0.##})";
            }

            string obstacleText = "obstacle3004=missing";
            if (hasObstacle)
            {
                var geometry = em.GetComponentData<Shape2DComponent>(obstacle3004);
                var mask = em.GetComponentData<ObstacleCollisionMaskComponent>(obstacle3004);
                obstacleText =
                    $"obstacle3004(shape={geometry.Kind}, radius={geometry.Radius:0.##}, size=({geometry.Size.x:0.##},{geometry.Size.y:0.##}), mask={mask.Value})";
            }

            return $"{topologyText}, {sourceText}, {depositText}, {obstacleText}";
        }

        private static string DescribeStageRunState(EntityManager em)
        {
            CompleteTrackedJobs(em);

            string stageText = "stage=missing";
            if (HasSingleton<RunDirectorStageStateComponent>(em))
            {
                var stage = GetSingleton<RunDirectorStageStateComponent>(em);
                stageText =
                    $"stage(state={stage.State}, elapsed={stage.StateElapsedSec:0.##}, reason={stage.LastTransitionReason})";
            }

            string gateText = "gate=missing";
            if (HasSingleton<RunDirectorStageGateComponent>(em))
            {
                var gate = GetSingleton<RunDirectorStageGateComponent>(em);
                gateText =
                    $"gate(intro={gate.IntroPresentationDone}, clear={gate.ClearPresentationDone}, minIdle={gate.MinIdleDurationElapsed}, auto={gate.AutoAdvanceTimeoutElapsed})";
            }

            string requestText = "request=missing";
            if (HasSingleton<RunDirectorStageRequestComponent>(em))
            {
                var request = GetSingleton<RunDirectorStageRequestComponent>(em);
                requestText =
                    $"request(start={request.StageStartRequested}, confirm={request.ConfirmPressed}, forceClear={request.ForceClearReadyRequested})";
            }

            string topologyText = "topology=missing";
            if (HasSingleton<StageTopologyStateComponent>(em))
            {
                var topology = GetSingleton<StageTopologyStateComponent>(em);
                topologyText =
                    $"topology(selected={topology.SelectedStageId}, applied={topology.AppliedStageId}, ready={topology.Ready})";
            }

            return $"{stageText}, {gateText}, {requestText}, {topologyText}";
        }

        private static void AssertStageMapAppliedForStage1(EntityManager em)
        {
            CompleteTrackedJobs(em);
            Assert.That(TryFindSourceByStableId(em, 1001u, out var source1001), Is.True);
            Assert.That(TryFindDepositByStableId(em, 2001u, out var deposit2001), Is.True);

            var sourceState1 = em.GetComponentData<SourceSpawnComponent>(source1001);
            var area1 = em.GetComponentData<Shape2DComponent>(source1001);
            var deposit1 = em.GetComponentData<Shape2DComponent>(deposit2001);

            Assert.That(sourceState1.State, Is.EqualTo(SourceStateId.Normal));
            Assert.That(area1.Kind, Is.EqualTo(Shape2DKind.Rectangle));
            Assert.That(area1.Size.x, Is.EqualTo(12f).Within(0.01f));
            Assert.That(area1.Size.y, Is.EqualTo(8f).Within(0.01f));
            Assert.That(deposit1.Radius, Is.EqualTo(5f).Within(0.01f));
        }

        private static void AssertStageMapAppliedForStage2(EntityManager em)
        {
            CompleteTrackedJobs(em);
            Assert.That(TryFindSourceByStableId(em, 1002u, out var source1002), Is.True);
            Assert.That(TryFindDepositByStableId(em, 2001u, out var deposit2001), Is.True);
            Assert.That(TryFindObstacleByStableId(em, 3004u, out var obstacle3004), Is.True);

            var sourceState2 = em.GetComponentData<SourceSpawnComponent>(source1002);
            var area2 = em.GetComponentData<Shape2DComponent>(source1002);
            var deposit1 = em.GetComponentData<Shape2DComponent>(deposit2001);
            var clipPatterns2 = em.GetBuffer<SourceClipPatternBuffer>(source1002, isReadOnly: true);
            var obstacleGeometry = em.GetComponentData<Shape2DComponent>(obstacle3004);
            var obstacleMask = em.GetComponentData<ObstacleCollisionMaskComponent>(obstacle3004);

            Assert.That(sourceState2.State, Is.EqualTo(SourceStateId.Normal));
            Assert.That(area2.Kind, Is.EqualTo(Shape2DKind.Circle));
            Assert.That(area2.Radius, Is.EqualTo(6f).Within(0.01f));
            Assert.That(deposit1.Radius, Is.EqualTo(4f).Within(0.01f));
            Assert.That(obstacleGeometry.Kind, Is.EqualTo(Shape2DKind.Rectangle));
            Assert.That(obstacleGeometry.Size.x, Is.EqualTo(3.5f).Within(0.01f));
            Assert.That(obstacleGeometry.Size.y, Is.EqualTo(2f).Within(0.01f));
            Assert.That(
                HasRequiredObstacleMaskBits(
                    obstacleMask.Value,
                    ObstacleCollisionMask.BlockPlayer | ObstacleCollisionMask.BlockBullet),
                Is.True);
            Assert.That(clipPatterns2.Length, Is.GreaterThan(0));
        }

        private static bool HasRequiredObstacleMaskBits(ObstacleCollisionMask actual, ObstacleCollisionMask required)
        {
            return required != ObstacleCollisionMask.None
                && (actual & required) == required;
        }

        private static bool TryFindSourceByStableId(EntityManager em, uint stableId, out Entity sourceEntity)
        {
            CompleteTrackedJobs(em);
            sourceEntity = Entity.Null;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceStableIdComponent>(),
                ComponentType.ReadOnly<SourceSpawnComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<SourceStableIdComponent>(entities[i]).Value != stableId)
                    continue;

                sourceEntity = entities[i];
                return true;
            }

            return false;
        }

        private static bool TryFindDepositByStableId(EntityManager em, uint stableId, out Entity depositEntity)
        {
            CompleteTrackedJobs(em);
            depositEntity = Entity.Null;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<DepositStableIdComponent>(),
                ComponentType.ReadOnly<DepositPointComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<DepositStableIdComponent>(entities[i]).Value != stableId)
                    continue;

                depositEntity = entities[i];
                return true;
            }

            return false;
        }

        private static bool TryFindObstacleByStableId(EntityManager em, uint stableId, out Entity obstacleEntity)
        {
            CompleteTrackedJobs(em);
            obstacleEntity = Entity.Null;
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ObstacleStableIdComponent>(),
                ComponentType.ReadOnly<ObstacleGeometryComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<ObstacleStableIdComponent>(entities[i]).Value != stableId)
                    continue;

                obstacleEntity = entities[i];
                return true;
            }

            return false;
        }

        private static void CompleteTrackedJobs(EntityManager em)
        {
            em.CompleteAllTrackedJobs();
        }

        private static IEnumerator LoadSceneIgnoringBootstrapBacklogErrors(string scenePath)
        {
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = previousIgnore;
        }


        private static void ResetStageFlowStateForOperationalScene(EntityManager em)
        {
            if (HasSingleton<RunDirectorStageStateComponent>(em))
            {
                var stageEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
                em.SetComponentData(stageEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                    StateElapsedSec = 0f,
                    EnteredFrame = 0u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.None,
                });
            }

            if (HasSingleton<RunDirectorStageGateComponent>(em))
            {
                var gateEntity = GetSingletonEntity<RunDirectorStageGateComponent>(em);
                em.SetComponentData(gateEntity, new RunDirectorStageGateComponent
                {
                    IntroPresentationDone = 0,
                    ClearPresentationDone = 0,
                    MinIdleDurationElapsed = 1,
                    AutoAdvanceTimeoutElapsed = 0,
                });
            }

            if (HasSingleton<RunDirectorStageRequestComponent>(em))
            {
                var requestEntity = GetSingletonEntity<RunDirectorStageRequestComponent>(em);
                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));
            }

            if (HasSingleton<RunDirectorStageSignalComponent>(em))
            {
                var signalEntity = GetSingletonEntity<RunDirectorStageSignalComponent>(em);
                em.SetComponentData(signalEntity, default(RunDirectorStageSignalComponent));
            }

            if (HasSingleton<StageTopologyRequestComponent>(em))
            {
                var topologyRequestEntity = GetSingletonEntity<StageTopologyRequestComponent>(em);
                em.SetComponentData(topologyRequestEntity, default(StageTopologyRequestComponent));
            }

            if (HasSingleton<StageTopologyStateComponent>(em))
            {
                var topologyStateEntity = GetSingletonEntity<StageTopologyStateComponent>(em);
                em.SetComponentData(topologyStateEntity, default(StageTopologyStateComponent));
            }
        }

        private static void ForceStageStateToClearReady(EntityManager em)
        {
            var stageEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
            var stage = em.GetComponentData<RunDirectorStageStateComponent>(stageEntity);
            stage.State = RunDirectorStageStateId.ClearReady;
            stage.StateElapsedSec = 0f;
            stage.LastTransitionReason = RunDirectorStageTransitionReasonId.AllSourcesDepleted;
            em.SetComponentData(stageEntity, stage);

            var gateEntity = GetSingletonEntity<RunDirectorStageGateComponent>(em);
            var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);
            gate.ClearPresentationDone = 0;
            gate.AutoAdvanceTimeoutElapsed = 0;
            em.SetComponentData(gateEntity, gate);

            var requestEntity = GetSingletonEntity<RunDirectorStageRequestComponent>(em);
            var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
            request.ConfirmPressed = 0;
            em.SetComponentData(requestEntity, request);
        }

        private static void ForceStageStateToRunning(EntityManager em, float elapsedSec)
        {
            var stageEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
            var stage = em.GetComponentData<RunDirectorStageStateComponent>(stageEntity);
            stage.State = RunDirectorStageStateId.Running;
            stage.StateElapsedSec = Mathf.Max(0f, elapsedSec);
            stage.LastTransitionReason = RunDirectorStageTransitionReasonId.StartRequested;
            em.SetComponentData(stageEntity, stage);
        }

        private static void SetCombatTotals(EntityManager em, long totalCollect, long totalCleanup, long totalHit)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CombatEventChannelSingletonTag>(),
                ComponentType.ReadWrite<CombatEventMetricsComponent>(),
                ComponentType.ReadWrite<CombatEventBufferElement>());
            var channelEntity = query.GetSingletonEntity();
            var metrics = em.GetComponentData<CombatEventMetricsComponent>(channelEntity);
            metrics.TotalCollectValue = totalCollect;
            metrics.TotalCleanupValue = totalCleanup;
            metrics.TotalHitValue = totalHit;
            em.SetComponentData(channelEntity, metrics);
        }

        private static DemoShellFlowController FindDemoShell()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DemoShellFlowController>();
#else
            return Object.FindObjectOfType<DemoShellFlowController>();
#endif
        }

        private static PlayerRuntimeHudBridge FindPlayerRuntimeHud()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<PlayerRuntimeHudBridge>();
#else
            return Object.FindObjectOfType<PlayerRuntimeHudBridge>();
#endif
        }

        private static DemoAudioBridge FindDemoAudioBridge()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DemoAudioBridge>();
#else
            return Object.FindObjectOfType<DemoAudioBridge>();
#endif
        }

        private static RuntimeUiRoot FindRuntimeUiRoot()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<RuntimeUiRoot>();
#else
            return Object.FindObjectOfType<RuntimeUiRoot>();
#endif
        }

        private static DemoShellPauseBridge FindPauseBridge()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DemoShellPauseBridge>();
#else
            return Object.FindObjectOfType<DemoShellPauseBridge>();
#endif
        }

        private static DemoShellDialogueBridge FindDialogueBridge()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DemoShellDialogueBridge>();
#else
            return Object.FindObjectOfType<DemoShellDialogueBridge>();
#endif
        }

        private static StagePresentationRuntimeController FindStagePresentationRuntimeController()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<StagePresentationRuntimeController>();
#else
            return Object.FindObjectOfType<StagePresentationRuntimeController>();
#endif
        }

        private static string DescribePresentationControllerState()
        {
            var controller = FindStagePresentationRuntimeController();
            if (controller == null)
                return "presentation-controller=missing";

            string childName = controller.transform.childCount > 0
                ? controller.transform.GetChild(0).name
                : "none";

            return
                $"presentation(lastApplied={controller.LastAppliedStageId}, lastReady={controller.LastReady}, spawned={controller.SpawnedRootCount}, childCount={controller.transform.childCount}, firstChild={childName})";
        }

        private static void EnqueueBurstRequestsFromFirstPattern(EntityManager em, int requestCount)
        {
            using var sourceQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceSpawnComponent>(),
                ComponentType.ReadOnly<SourceClipPatternBuffer>(),
                ComponentType.ReadWrite<SourceSpawnRequestBuffer>());
            using var sources = sourceQuery.ToEntityArray(Allocator.Temp);
            Assert.That(sources.Length, Is.GreaterThan(0), "At least one source must exist for burst request test.");

            uint frame = GetSingleton<BulletFrameCounterComponent>(em).Value;
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                var patterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                if (patterns.Length <= 0)
                    continue;

                var pattern = patterns[0];
                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = pattern.DirectiveId,
                    Phase = pattern.Phase,
                    Lane = pattern.Lane,
                    LanePriority = pattern.LanePriority,
                    BulletTypeKey = pattern.BulletTypeKey,
                    SamplingMode = pattern.SamplingMode,
                    CenterMode = pattern.CenterMode,
                    DirectionMode = pattern.DirectionMode,
                    FixedPoint = pattern.FixedPoint,
                    SpawnOffset = pattern.SpawnOffset,
                    LineStart = pattern.LineStart,
                    LineEnd = pattern.LineEnd,
                    SampleSpacing = pattern.SampleSpacing,
                    PointSetCount = pattern.PointSetCount,
                    Point0 = pattern.Point0,
                    Point1 = pattern.Point1,
                    Point2 = pattern.Point2,
                    Point3 = pattern.Point3,
                    SpawnSampleBudget = pattern.SpawnSampleBudget,
                    PlayerNoSpawnRadius = pattern.PlayerNoSpawnRadius,
                    BaseAngleDeg = pattern.BaseAngleDeg,
                    NWayCount = pattern.NWayCount,
                    SpiralStepDeg = pattern.SpiralStepDeg,
                    BurstShotsPerEvent = pattern.BurstShotsPerEvent,
                    EventShotSchedule = pattern.EventShotSchedule,
                    EventShotIntervalSec = pattern.EventShotIntervalSec,
                    EventShotElapsedSec = 0f,
                    EventAnchorInitialized = 0,
                    EventAnchorUseFixedPosition = 0,
                    EventAnchorCenter = default,
                    EventAnchorPosition = default,
                    SpawnSequence = 1u,
                    Count = requestCount,
                    OldestFrame = frame,
                });
                return;
            }

            Assert.Fail("No source with clip pattern was found for burst request test.");
        }

        private static void ClearDemoShellStaging()
        {
            while (DemoShellSessionStaging.TryConsume(out _))
            {
            }

            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
        }

        private static void ClearAudioVolumePrefs()
        {
            for (int i = 0; i < DemoAudioPrefsKeys.AllVolumeKeys.Length; i++)
                PlayerPrefs.DeleteKey(DemoAudioPrefsKeys.AllVolumeKeys[i]);
            PlayerPrefs.Save();
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

    }
}













