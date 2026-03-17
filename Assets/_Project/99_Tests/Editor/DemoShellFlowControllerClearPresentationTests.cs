using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DemoShellFlowControllerClearPresentationTests
    {
        private static readonly MethodInfo TickStagePlayFlowMethod = typeof(DemoShellFlowController)
            .GetMethod("TickStagePlayFlow", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo TryConsumeCompletedStateFallbackMethod = typeof(DemoShellFlowController)
            .GetMethod("TryConsumeCompletedStateFallback", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
        }

        [TearDown]
        public void TearDown()
        {
            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
        }

        [Test]
        public void ClearReady_WithoutSubscriber_FallsBackToAwaitingCompleted_AndPublishesClearResult()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            try
            {
                world = CreateBridgeWorld("DemoShellFlowController_ClearFallbackWorld");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                var stateEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
                em.SetComponentData(stateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.ClearReady,
                    StateElapsedSec = 0f,
                    EnteredFrame = 1u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.AllSourcesDepleted,
                });

                go = CreateShellGameObject("DemoShellFlowController_ClearFallback");
                var shell = go.GetComponent<DemoShellFlowController>();
                PrepareRunningStage(shell, elapsedSec: 12.5f);

                InvokeTickStagePlayFlow(shell);

                Assert.That(shell.CurrentScreen, Is.EqualTo(DemoShellScreenId.StagePlay));
                Assert.That(shell.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.AwaitingClearCompleted));
                Assert.That(GetSingleton<RunDirectorStageGateComponent>(em).ClearPresentationDone, Is.EqualTo(1));
                Assert.That(GetSingleton<RunDirectorStageRequestComponent>(em).ConfirmPressed, Is.EqualTo(1));
                Assert.That(shell.NotifyPreResultClearPresentationCompleted(), Is.False, "Fallback completion should already have been sent.");
                Assert.That(GetSingleton<StageGameplayClockComponent>(em).ElapsedSec, Is.EqualTo(12.5f).Within(1e-4f));

                em.SetComponentData(stateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Completed,
                    StateElapsedSec = 0f,
                    EnteredFrame = 2u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.ConfirmPressed,
                });

                InvokeTryConsumeCompletedStateFallback(shell);

                Assert.That(shell.CurrentScreen, Is.EqualTo(DemoShellScreenId.StageResult));
                Assert.That(shell.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.None));
                Assert.That(shell.CurrentStageOutcome, Is.EqualTo(DemoShellStageOutcomeId.Clear));
                Assert.That(shell.HasCurrentStageResult, Is.True);
                Assert.That(shell.CurrentStageResult.ElapsedSec, Is.EqualTo(12.5f).Within(1e-4f));
                Assert.That(DemoShellSessionStaging.TryGetSessionMetrics(out var metrics), Is.True);
                Assert.That(metrics.ClearedStageCount, Is.EqualTo(1));
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void ClearReady_WithSubscriber_StaysInStagePlayUntilCompletion()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            try
            {
                world = CreateBridgeWorld("DemoShellFlowController_ClearSubscriberWorld");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                var stateEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
                em.SetComponentData(stateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.ClearReady,
                    StateElapsedSec = 0f,
                    EnteredFrame = 3u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.AllSourcesDepleted,
                });

                go = CreateShellGameObject("DemoShellFlowController_ClearSubscriber");
                var shell = go.GetComponent<DemoShellFlowController>();
                PrepareRunningStage(shell, elapsedSec: 20f);

                int callbackCount = 0;
                DemoShellStageResultMetrics requestedResult = default;
                shell.PreResultClearPresentationRequested += result =>
                {
                    callbackCount++;
                    requestedResult = result;
                };

                InvokeTickStagePlayFlow(shell);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(requestedResult.StageId, Is.EqualTo(1));
                Assert.That(requestedResult.Outcome, Is.EqualTo(DemoShellStageOutcomeId.Clear));
                Assert.That(requestedResult.ElapsedSec, Is.EqualTo(20f).Within(1e-4f));
                Assert.That(shell.CurrentScreen, Is.EqualTo(DemoShellScreenId.StagePlay));
                Assert.That(shell.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.ClearPresentation));
                Assert.That(GetSingleton<RunDirectorStageGateComponent>(em).ClearPresentationDone, Is.EqualTo(0));
                Assert.That(GetSingleton<RunDirectorStageRequestComponent>(em).ConfirmPressed, Is.EqualTo(0));
                Assert.That(GetSingleton<StageGameplayClockComponent>(em).ElapsedSec, Is.EqualTo(20f).Within(1e-4f));

                Assert.That(shell.NotifyPreResultClearPresentationCompleted(), Is.True);
                Assert.That(shell.NotifyPreResultClearPresentationCompleted(), Is.False);
                Assert.That(shell.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.AwaitingClearCompleted));
                Assert.That(GetSingleton<RunDirectorStageGateComponent>(em).ClearPresentationDone, Is.EqualTo(1));
                Assert.That(GetSingleton<RunDirectorStageRequestComponent>(em).ConfirmPressed, Is.EqualTo(1));

                em.SetComponentData(stateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Completed,
                    StateElapsedSec = 0f,
                    EnteredFrame = 4u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.ConfirmPressed,
                });

                InvokeTryConsumeCompletedStateFallback(shell);

                Assert.That(shell.CurrentScreen, Is.EqualTo(DemoShellScreenId.StageResult));
                Assert.That(shell.CurrentStageOutcome, Is.EqualTo(DemoShellStageOutcomeId.Clear));
                Assert.That(shell.CurrentStageResult.ElapsedSec, Is.EqualTo(20f).Within(1e-4f));
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void PauseBridge_CannotPause_WhenDialogueInputExclusive()
        {
            GameObject go = null;
            try
            {
                go = CreateShellGameObject("DemoShellFlowController_PauseGuard");
                var shell = go.GetComponent<DemoShellFlowController>();
                var pauseController = go.GetComponent<DemoShellGameplayPauseController>() ?? go.AddComponent<DemoShellGameplayPauseController>();
                pauseController.LogBindWarnings = false;
                var pauseBridge = go.GetComponent<DemoShellPauseBridge>() ?? go.AddComponent<DemoShellPauseBridge>();
                pauseBridge.DemoShell = shell;
                pauseBridge.PauseController = pauseController;

                SetPrivateField(shell, "_currentScreen", DemoShellScreenId.StagePlay);
                SetPrivateField(shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.ClearPresentation);

                Assert.That(shell.IsDialogueInputExclusive, Is.True);
                Assert.That(pauseBridge.CanPause, Is.False);
                Assert.That(pauseBridge.RequestPause(), Is.False);
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static World CreateBridgeWorld(string worldName)
        {
            var world = new World(worldName);
            var em = world.EntityManager;

            var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
            em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));

            var gateEntity = em.CreateEntity(typeof(RunDirectorStageGateComponent));
            em.SetComponentData(gateEntity, default(RunDirectorStageGateComponent));

            var signalEntity = em.CreateEntity(typeof(RunDirectorStageSignalComponent));
            em.SetComponentData(signalEntity, default(RunDirectorStageSignalComponent));

            var stateEntity = em.CreateEntity(typeof(RunDirectorStageStateComponent));
            em.SetComponentData(stateEntity, default(RunDirectorStageStateComponent));

            var gameplayClockEntity = em.CreateEntity(typeof(StageGameplayClockComponent));
            em.SetComponentData(gameplayClockEntity, default(StageGameplayClockComponent));

            return world;
        }

        private static GameObject CreateShellGameObject(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);

            var shell = go.AddComponent<DemoShellFlowController>();
            shell.StageBridge = go.GetComponent<RunDirectorStageBridge>();
            shell.TopologyBridge = go.GetComponent<StageTopologyBridge>();

            var stageBridge = shell.StageBridge;
            if (stageBridge != null)
                stageBridge.LogBindWarnings = false;

            return go;
        }

        private static void PrepareRunningStage(DemoShellFlowController shell, float elapsedSec)
        {
            if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                var gameplayClockEntity = GetSingletonEntity<StageGameplayClockComponent>(em);
                em.SetComponentData(gameplayClockEntity, new StageGameplayClockComponent
                {
                    ElapsedSec = elapsedSec,
                    Version = 1u,
                });
            }

            SetPrivateField(shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(shell, "_currentStageIndex", 0);
            SetPrivateField(shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.Running);
            SetPrivateField(shell, "_stageRunningObserved", true);
            SetPrivateField(shell, "_stageStartPending", false);
            SetPrivateField(shell, "_stageTopologyApplyPending", false);
            SetPrivateField(shell, "_hasCurrentStageResult", false);
        }

        private static void InvokeTickStagePlayFlow(DemoShellFlowController shell)
        {
            Assert.That(TickStagePlayFlowMethod, Is.Not.Null, "DemoShellFlowController.TickStagePlayFlow method not found.");
            TickStagePlayFlowMethod.Invoke(shell, null);
        }

        private static void InvokeTryConsumeCompletedStateFallback(DemoShellFlowController shell)
        {
            Assert.That(TryConsumeCompletedStateFallbackMethod, Is.Not.Null, "DemoShellFlowController.TryConsumeCompletedStateFallback method not found.");
            TryConsumeCompletedStateFallbackMethod.Invoke(shell, null);
        }

        private static Entity GetSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingletonEntity();
        }

        private static T GetSingleton<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

    }
}
