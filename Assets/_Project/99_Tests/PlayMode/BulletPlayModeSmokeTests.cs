using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
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
                scenePath: DedicatedScenePath,
                sceneLabel: "PlayModeSmoke_Dedicated",
                frameCount: 120);
        }

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_StressSwitch_BurstRequest_ImpactsBacklogAndHud()
        {
            SceneManager.LoadScene(DedicatedScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    CountByComponentType<PlayerTag>(em) > 0 &&
                    CountByComponentType<SourceSpawnComponent>(em) > 0 &&
                    CountByComponentType<BulletFrameCounterComponent>(em) > 0 &&
                    HasSingleton<StressSwitchStateComponent>(em) &&
                    HasSingleton<SpawnBacklogMetricsComponent>(em) &&
                    HasSingleton<DebugHudMetricsComponent>(em),
                300,
                "ECS singleton setup for stress/HUD was not ready within timeout.");

            int baselineMaxPending = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                var baselineMetrics = GetSingleton<SpawnBacklogMetricsComponent>(em);
                baselineMaxPending = Mathf.Max(baselineMaxPending, baselineMetrics.PendingCount);
            }

            var stressEntity = GetSingletonEntity<StressSwitchStateComponent>(em);
            var stress = em.GetComponentData<StressSwitchStateComponent>(stressEntity);
            stress.Mode = (byte)StressSwitchModeId.BurstOnce;
            stress.BurstCount = 20000;
            stress.PreferredBulletTypeKey = -1;
            stress.RequestExecute = 1;
            em.SetComponentData(stressEntity, stress);

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

            var stressAfter = GetSingleton<StressSwitchStateComponent>(em);
            Assert.That(stressAfter.RequestExecute, Is.EqualTo(0), "Stress request flag must be consumed");
            Assert.That(stressAfter.Mode, Is.EqualTo((byte)StressSwitchModeId.None), "Burst mode must finish as one-shot request");
            Assert.That(postMaxPending, Is.GreaterThan(baselineMaxPending + 1000), "Burst request should noticeably increase pending backlog");
            Assert.That(postMaxHudSpawned, Is.GreaterThan(0), "HUD spawned metric should be updated during burst run");
        }

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_DataDrivenPatternScenario_BaselineMetricsAreRecorded()
        {
            SceneManager.LoadScene(DedicatedScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    CountByComponentType<SourceSpawnComponent>(em) > 0 &&
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
            SceneManager.LoadScene(DedicatedScenePath, LoadSceneMode.Single);
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

            Assert.That(shell.RequestResultAction(DemoShellResultActionId.ReturnToLobby), Is.True);
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

            // Stage1 clear
            SetCombatTotals(em, totalCollect: 10, totalCleanup: 5, totalHit: 1);
            yield return null;
            yield return null;
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
                "Did not enter Stage2 after Stage1 Next.");
            world = World.DefaultGameObjectInjectionWorld;
            em = world.EntityManager;

            // Stage2 clear
            SetCombatTotals(em, totalCollect: 20, totalCleanup: 8, totalHit: 2);
            yield return null;
            yield return null;
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

            // Stage3 clear -> DemoComplete
            SetCombatTotals(em, totalCollect: 30, totalCleanup: 12, totalHit: 3);
            yield return null;
            yield return null;
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

            var combatChannelEntity = em.CreateEntityQuery(
                ComponentType.ReadOnly<CombatEventChannelSingletonTag>(),
                ComponentType.ReadWrite<CombatEventMetricsComponent>(),
                ComponentType.ReadWrite<CombatEventBufferElement>()).GetSingletonEntity();
            var combatEvents = em.GetBuffer<CombatEventBufferElement>(combatChannelEntity);
            uint frame = GetSingleton<BulletFrameCounterComponent>(em).Value;
            combatEvents.Add(new CombatEventBufferElement
            {
                Type = CombatEventTypeId.Hit,
                SourceEntity = Entity.Null,
                RelatedEntity = playerEntity,
                Count = 1,
                Value = 11,
                Frame = frame,
                Sequence = (uint)combatEvents.Length,
            });

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
            SceneManager.LoadScene(DedicatedScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

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
            SceneManager.LoadScene(DedicatedScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

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
            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    CountByComponentType<PlayerTag>(em) > 0 &&
                    CountByComponentType<SourceSpawnComponent>(em) > 0 &&
                    CountByComponentType<BulletFrameCounterComponent>(em) > 0,
                300,
                $"ECS setup was not ready within timeout. scene={sceneLabel}");

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
            for (int i = 0; i < timeoutFrames; i++)
            {
                if (predicate())
                    yield break;
                yield return null;
            }

            Assert.Fail(failMessage);
        }

        private static int CountByComponentType<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static bool HasSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static bool HasReplaySingleton(EntityManager em)
        {
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ReplayInputControlComponent>(),
                ComponentType.ReadOnly<ReplayInputCursorComponent>(),
                ComponentType.ReadOnly<ReplayInputFrameBufferElement>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static bool HasCombatEventChannel(EntityManager em)
        {
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CombatEventChannelSingletonTag>(),
                ComponentType.ReadOnly<CombatEventMetricsComponent>(),
                ComponentType.ReadWrite<CombatEventBufferElement>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static Entity GetSingletonEntity<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingletonEntity();
        }

        private static T GetSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }

        private static int ComputeOldestPendingAgeFrames(EntityManager em)
        {
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

        private static void ClearDemoShellStaging()
        {
            while (DemoShellSessionStaging.TryConsume(out _))
            {
            }

            DemoShellSessionStaging.ResetSessionMetrics();
        }

        private static void ClearAudioVolumePrefs()
        {
            for (int i = 0; i < DemoAudioPrefsKeys.AllVolumeKeys.Length; i++)
                PlayerPrefs.DeleteKey(DemoAudioPrefsKeys.AllVolumeKeys[i]);
            PlayerPrefs.Save();
        }

    }
}


