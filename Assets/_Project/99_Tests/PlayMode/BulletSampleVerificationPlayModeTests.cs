using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
            Entity actorEntity = Entity.Null;
            Entity emitterEntity = Entity.Null;
            Entity sourceEntity = Entity.Null;
            Entity playerEntity = Entity.Null;
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

            CompleteTrackedJobs(em);
            actorEntity = FindFirstEntity<HazardActorComponent>(em);
            emitterEntity = FindFirstEntity<HazardEmitterComponent>(em);
            playerEntity = GetSingletonEntity<PlayerTag>(em);
            Assert.That(actorEntity, Is.Not.EqualTo(Entity.Null), "Sample verification scene did not create a HazardActor entity.");
            Assert.That(emitterEntity, Is.Not.EqualTo(Entity.Null), "Sample verification scene did not create a HazardEmitter entity.");
            Assert.That(playerEntity, Is.Not.EqualTo(Entity.Null), "Sample verification scene did not create a Player entity.");
            sourceEntity = em.GetComponentData<HazardActorComponent>(actorEntity).SourceEntity;
            Assert.That(sourceEntity, Is.Not.EqualTo(Entity.Null), "Hazard actor must point to a source entity.");

            var selectorPolicies = em.GetBuffer<HazardActorPhaseSelectorPolicyBuffer>(actorEntity);
            var selectorCandidates = em.GetBuffer<HazardActorPhaseSelectorCandidateBuffer>(actorEntity);
            var transitions = em.GetBuffer<HazardActorPhaseProgressTransitionBuffer>(actorEntity);
            var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
            Assert.That(selectorPolicies.Length, Is.EqualTo(2), "Blueprint sample actor must expose two phase selector policies.");
            Assert.That(selectorCandidates.Length, Is.EqualTo(4), "Blueprint sample actor must expose four ordered selector candidates.");
            Assert.That(transitions.Length, Is.EqualTo(1), "Blueprint sample actor must expose one phase transition.");
            Assert.That(patternSlots.Length, Is.EqualTo(3), "Blueprint sample emitter must expose A/B/B' pattern slots.");

            SetCarryState(em, load: 0, capacity: 100);

            yield return WaitForCondition(
                () =>
                {
                    CompleteTrackedJobs(em);
                    if (!em.Exists(actorEntity) || !em.Exists(emitterEntity))
                        return false;

                    var actorPhase = em.GetComponentData<HazardActorBehaviorPhaseStateComponent>(actorEntity);
                    var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actorEntity);
                    return actorPhase.CurrentPhaseId == 1
                        && selector.CurrentPatternSlotId == 1
                        && CountActiveBulletsByType(em, LinearHazardTypeKey) > 0
                        && AnyEmitterAdvancedFromDormant(em);
                },
                600,
                "Sample verification scene did not start phase 1 entry pattern A in time.");

            yield return WaitForCondition(
                () =>
                {
                    CompleteTrackedJobs(em);
                    if (!em.Exists(actorEntity))
                        return false;

                    var actorPhase = em.GetComponentData<HazardActorBehaviorPhaseStateComponent>(actorEntity);
                    var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actorEntity);
                    return actorPhase.CurrentPhaseId == 1
                        && selector.CurrentPatternSlotId == 2
                        && selector.SelectionSequence > 0u;
                },
                600,
                "Sample verification scene did not alternate to phase 1 pattern B in time.");

            AdvanceSourceProgressToHalfThreshold(em, sourceEntity);

            bool preparingStarted = false;
            int preparingSlotId = -1;
            uint preparingSequence = 0u;
            int preparingProgress = 0;
            uint preparingSignalVersion = 0u;
            for (int frame = 0; frame < 300; frame++)
            {
                yield return null;
                CompleteTrackedJobs(em);

                if (!em.Exists(actorEntity))
                    continue;

                var signal = em.GetComponentData<HazardActorPhaseTransitionSignalComponent>(actorEntity);
                var transitionRuntime = em.GetComponentData<HazardActorPhaseTransitionRuntimeComponent>(actorEntity);
                if (transitionRuntime.State != HazardActorPhaseTransitionStateId.Preparing
                    || signal.Cue != HazardActorPhaseTransitionSignalCueId.PreparingStarted)
                {
                    continue;
                }

                var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actorEntity);
                preparingStarted = true;
                preparingSlotId = selector.CurrentPatternSlotId;
                preparingSequence = selector.SelectionSequence;
                preparingProgress = em.GetComponentData<SourceSpawnComponent>(sourceEntity).CollectedCount;
                preparingSignalVersion = signal.Version;
                break;
            }

            Assert.That(preparingStarted, Is.True, "Blueprint sample did not enter Preparing after reaching half progress.");
            var sourceAtPreparing = em.GetComponentData<SourceSpawnComponent>(sourceEntity);
            float preparingProgress01 = math.saturate((float)preparingProgress / math.max(1, sourceAtPreparing.ThresholdDepleted));
            Assert.That(preparingProgress01, Is.GreaterThanOrEqualTo(0.5f), "Preparing must start after source progress crosses the half-progress threshold.");

            bool sawPreparingSuppression = false;
            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                CompleteTrackedJobs(em);

                var transitionRuntime = em.GetComponentData<HazardActorPhaseTransitionRuntimeComponent>(actorEntity);
                if (transitionRuntime.State != HazardActorPhaseTransitionStateId.Preparing)
                    break;

                var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actorEntity);
                var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitterEntity);
                Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(preparingSlotId), "Selector must freeze while the actor is Preparing.");
                Assert.That(selector.SelectionSequence, Is.EqualTo(preparingSequence), "Selector sequence must freeze while the actor is Preparing.");

                if (coordinator.ActivationAllowed == 0
                    && (coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.ActorPhaseTransitionPreparing) != 0u)
                {
                    sawPreparingSuppression = true;
                }
            }

            Assert.That(sawPreparingSuppression, Is.True, "Preparing must block emitter activation through the actor transition suppression flag.");

            yield return WaitForCondition(
                () =>
                {
                    CompleteTrackedJobs(em);
                    if (!em.Exists(actorEntity))
                        return false;

                    var phase = em.GetComponentData<HazardActorBehaviorPhaseStateComponent>(actorEntity);
                    var signal = em.GetComponentData<HazardActorPhaseTransitionSignalComponent>(actorEntity);
                    var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actorEntity);
                    return phase.CurrentPhaseId == 2
                        && signal.Cue == HazardActorPhaseTransitionSignalCueId.PhaseCommitted
                        && signal.Version > preparingSignalVersion
                        && selector.CurrentPatternSlotId == 1;
                },
                300,
                "Blueprint sample did not commit phase 2 and restart from entry slot A in time.");

            yield return WaitForCondition(
                () =>
                {
                    CompleteTrackedJobs(em);
                    if (!em.Exists(actorEntity))
                        return false;

                    var phase = em.GetComponentData<HazardActorBehaviorPhaseStateComponent>(actorEntity);
                    var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actorEntity);
                    return phase.CurrentPhaseId == 2
                        && selector.CurrentPatternSlotId == 3;
                },
                600,
                "Blueprint sample did not advance to strengthened phase 2 pattern B' in time.");
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

        private static void AdvanceSourceProgressToHalfThreshold(EntityManager em, Entity sourceEntity)
        {
            if (sourceEntity == Entity.Null || !em.Exists(sourceEntity) || !em.HasComponent<SourceSpawnComponent>(sourceEntity))
                return;

            var source = em.GetComponentData<SourceSpawnComponent>(sourceEntity);
            int halfThreshold = math.max(1, (int)math.ceil(math.max(1, source.ThresholdDepleted) * 0.5f));
            source.CollectedCount = math.max(source.CollectedCount, halfThreshold);

            int weakenedThreshold = math.max(0, source.ThresholdWeakened);
            int depletedThreshold = math.max(weakenedThreshold, source.ThresholdDepleted);
            if (source.CollectedCount >= depletedThreshold)
                source.State = SourceStateId.Depleted;
            else if (source.CollectedCount >= weakenedThreshold)
                source.State = SourceStateId.Weakened;
            else
                source.State = SourceStateId.Normal;

            em.SetComponentData(sourceEntity, source);
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

        private static Entity FindFirstEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
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
