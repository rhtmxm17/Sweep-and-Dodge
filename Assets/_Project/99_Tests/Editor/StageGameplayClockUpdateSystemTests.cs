using System.Reflection;
using NUnit.Framework;
using Unity.Entities;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageGameplayClockUpdateSystemTests
    {
        private static readonly MethodInfo ResetStageSessionMethod = typeof(StageSessionResetPrepareSystem)
            .GetMethod("ResetStageSession", BindingFlags.Static | BindingFlags.NonPublic);

        [Test]
        public void StageGameplayClockUpdateSystem_RunningGameplay_AccumulatesFixedDelta()
        {
            using var world = CreateWorld();
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<StageGameplayClockUpdateSystem>();

            system.Update(world.Unmanaged);

            var clock = GetSingleton<StageGameplayClockComponent>(em);
            Assert.That(clock.ElapsedSec, Is.EqualTo(1f / 60f).Within(1e-6f));
            Assert.That(clock.Version, Is.EqualTo(1u));
        }

        [Test]
        public void StageGameplayClockUpdateSystem_DoesNotAdvance_WhenNoLogicStep()
        {
            using var world = CreateWorld();
            var em = world.EntityManager;
            var runtimeEntity = GetSingletonEntity<FixedTickStepRuntimeComponent>(em);
            em.SetComponentData(runtimeEntity, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 0f,
                LogicStepCount = 0,
                HasStep = 0,
                UsingFixedTick = 1,
                CurrentLogicFrame = 0u,
            });

            var system = world.GetOrCreateSystem<StageGameplayClockUpdateSystem>();
            system.Update(world.Unmanaged);

            var clock = GetSingleton<StageGameplayClockComponent>(em);
            Assert.That(clock.ElapsedSec, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(clock.Version, Is.EqualTo(0u));
        }

        [Test]
        public void StageGameplayClockUpdateSystem_DoesNotAdvance_WhenStageIsNotRunning()
        {
            using var world = CreateWorld();
            var em = world.EntityManager;
            var stageEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
            em.SetComponentData(stageEntity, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.ClearReady,
                StateElapsedSec = 0.5f,
                EnteredFrame = 1u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.AllSourcesDepleted,
            });

            var system = world.GetOrCreateSystem<StageGameplayClockUpdateSystem>();
            system.Update(world.Unmanaged);

            var clock = GetSingleton<StageGameplayClockComponent>(em);
            Assert.That(clock.ElapsedSec, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(clock.Version, Is.EqualTo(0u));
        }

        [Test]
        public void StageSessionResetPrepareSystem_ResetStageSession_ClearsGameplayClock()
        {
            Assert.That(ResetStageSessionMethod, Is.Not.Null, "StageSessionResetPrepareSystem.ResetStageSession method not found.");

            using var world = CreateWorld();
            var em = world.EntityManager;

            var stageRequestEntity = GetSingletonEntity<RunDirectorStageRequestComponent>(em);
            var gateEntity = GetSingletonEntity<RunDirectorStageGateComponent>(em);
            var stageStateEntity = GetSingletonEntity<RunDirectorStageStateComponent>(em);
            var signalEntity = GetSingletonEntity<RunDirectorStageSignalComponent>(em);
            var gameplayClockEntity = GetSingletonEntity<StageGameplayClockComponent>(em);
            var topologyStateEntity = GetSingletonEntity<StageTopologyStateComponent>(em);
            var lifecycleEntity = GetSingletonEntity<StageTopologyLifecycleStateComponent>(em);

            em.SetComponentData(gameplayClockEntity, new StageGameplayClockComponent
            {
                ElapsedSec = 9.75f,
                Version = 3u,
            });

            var stageConfig = new RunDirectorStageConfigComponent
            {
                InitialState = RunDirectorStageStateId.Idle,
                MinIdleDurationSec = 0f,
                ClearAutoAdvanceTimeoutSec = 10f,
            };

            ResetStageSessionMethod.Invoke(null, new object[]
            {
                em,
                stageRequestEntity,
                gateEntity,
                stageStateEntity,
                signalEntity,
                gameplayClockEntity,
                topologyStateEntity,
                lifecycleEntity,
                false,
                false,
                stageConfig,
            });

            var clock = em.GetComponentData<StageGameplayClockComponent>(gameplayClockEntity);
            Assert.That(clock.ElapsedSec, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(clock.Version, Is.EqualTo(4u));
        }

        private static World CreateWorld()
        {
            var world = new World("StageGameplayClockUpdateSystemWorld");
            var em = world.EntityManager;

            CreateSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 1,
                CurrentLogicFrame = 1u,
            });
            CreateSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
                StateElapsedSec = 0f,
                EnteredFrame = 1u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.StartRequested,
            });
            CreateSingleton(em, default(StageTopologyStateComponent));
            CreateSingleton(em, default(StageGameplayClockComponent));
            CreateSingleton(em, default(RunDirectorStageRequestComponent));
            CreateSingleton(em, new RunDirectorStageGateComponent
            {
                IntroPresentationDone = 1,
                ClearPresentationDone = 1,
                MinIdleDurationElapsed = 1,
                AutoAdvanceTimeoutElapsed = 0,
            });
            CreateSingleton(em, default(RunDirectorStageSignalComponent));
            CreateSingleton(em, default(StageTopologyLifecycleStateComponent));
            return world;
        }

        private static void CreateSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
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
    }
}
