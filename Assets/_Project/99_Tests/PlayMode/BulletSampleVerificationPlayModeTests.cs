using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletSampleVerificationPlayModeTests
    {
        private const string ScenePath = "Assets/_Project/01_Scenes/PlayModeTests/PlayModeSmoke_SampleVerification.unity";

        private const int LinearHazardTypeKey = 1435459723;
        [UnityTest]
        public IEnumerator PlayMode_SampleVerificationScene_ExpandedBulletSamples_ExerciseRepresentativeStateTransitions()
        {
            ClearDemoShellStaging();
            yield return LoadSceneWithSettle(ScenePath);

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode.");

            var em = world.EntityManager;
            DemoShellFlowController shell = null;
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Title;
                },
                240,
                "Sample verification scene did not reach Title.");

            Assert.That(shell.RequestStartFromTitle(), Is.True, "Sample verification shell did not accept Title -> Lobby request.");
            yield return WaitForCondition(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null && shell.CurrentScreen == DemoShellScreenId.Lobby;
                },
                240,
                "Sample verification scene did not reach Lobby.");

            Assert.That(shell.RequestSelectStageById(1), Is.True, "Sample verification shell did not accept Stage 1 selection.");
            yield return WaitForStagePlayRunning(
                () =>
                {
                    shell = FindDemoShell();
                    return shell != null
                        && shell.CurrentScreen == DemoShellScreenId.StagePlay
                        && shell.CurrentStageId == 1
                        && shell.CurrentStagePlayPhase == DemoShellStagePlayPhaseId.Running
                        && HasSingleton<PlayerTag>(em)
                        && HasSingleton<PlayerCarryBinComponent>(em)
                        && HasSingleton<RunDirectorStageStateComponent>(em);
                },
                480,
                "Sample verification scene did not reach StagePlay/Running in time.");

            SetCarryState(em, load: 0, capacity: 100);

            bool sawLinear = false;
            bool sawActor = false;
            bool sawEmitter = false;
            bool sawEmitterProgress = false;
            for (int frame = 0; frame < 1200; frame++)
            {
                CompleteTrackedJobs(em);

                sawLinear |= CountActiveBulletsByType(em, LinearHazardTypeKey) > 0;
                sawActor |= CountByComponentType<HazardActorComponent>(em) > 0;
                sawEmitter |= CountByComponentType<HazardEmitterComponent>(em) > 0;
                sawEmitterProgress |= AnyEmitterAdvancedFromDormant(em);

                if (sawLinear && sawActor && sawEmitter && sawEmitterProgress)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(sawLinear, Is.True, "Linear baseline sample bullet was not observed.");
            Assert.That(sawActor, Is.True, "Sample verification scene did not create a HazardActor entity.");
            Assert.That(sawEmitter, Is.True, "Sample verification scene did not create a HazardEmitter entity.");
            Assert.That(sawEmitterProgress, Is.True, "Sample verification scene did not advance an actor-owned emitter runtime path.");
        }

        private static IEnumerator LoadSceneWithSettle(string scenePath, int settleFrames = 4)
        {
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);

            int waitForActiveSceneFrames = 16;
            while (waitForActiveSceneFrames-- > 0)
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid() && activeScene.path == scenePath)
                    break;

                yield return null;
            }

            for (int i = 0; i < settleFrames; i++)
                yield return null;

            LogAssert.ignoreFailingMessages = previousIgnore;
        }

        private static IEnumerator WaitForStagePlayRunning(System.Func<bool> predicate, int timeoutFrames, string failMessage)
        {
            for (int i = 0; i < timeoutFrames; i++)
            {
                var dialogueBridge = FindDialogueBridge();
                bool startDialogueActive = dialogueBridge != null
                    && dialogueBridge.IsDialogueActive
                    && dialogueBridge.CurrentPresentation.Trigger == InWorldDialogueTriggerId.StageStart;
                if (predicate() && !startDialogueActive)
                    yield break;

                if (startDialogueActive)
                    dialogueBridge.Skip();

                yield return null;
            }

            Assert.Fail(failMessage);
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

        private static void SetCarryState(EntityManager em, int load, int capacity)
        {
            var carryEntity = GetSingletonEntity<PlayerCarryBinComponent>(em);
            var carry = em.GetComponentData<PlayerCarryBinComponent>(carryEntity);
            carry.Capacity = Mathf.Max(1, capacity);
            carry.Load = Mathf.Clamp(load, 0, carry.Capacity);
            em.SetComponentData(carryEntity, carry);
        }

        private static int CountActiveBulletsByType(EntityManager em, int typeKey)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<BulletTypeKeyComponent>(),
                ComponentType.ReadOnly<BulletActiveTag>());
            using var bullets = query.ToComponentDataArray<BulletTypeKeyComponent>(Allocator.Temp);

            int count = 0;
            for (int i = 0; i < bullets.Length; i++)
            {
                if (bullets[i].Value == typeKey)
                    count++;
            }

            return count;
        }

        private static int CountByComponentType<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static bool AnyEmitterAdvancedFromDormant(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<HazardEmitterRuntimeStateComponent>());
            using var emitters = query.ToComponentDataArray<HazardEmitterRuntimeStateComponent>(Allocator.Temp);
            for (int i = 0; i < emitters.Length; i++)
            {
                if (emitters[i].LifecycleState != HazardEmitterLifecycleStateId.Dormant || emitters[i].StateElapsedSec > 0f)
                    return true;
            }

            return false;
        }

        private static bool HasSingleton<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount() > 0;
        }

        private static T GetSingleton<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }

        private static Entity GetSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingletonEntity();
        }

        private static void CompleteTrackedJobs(EntityManager em)
        {
            em.CompleteAllTrackedJobs();
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

        private static DemoShellFlowController FindDemoShell()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DemoShellFlowController>();
#else
            return Object.FindObjectOfType<DemoShellFlowController>();
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
    }
}
