using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class GameplayPauseApplySystemTests
    {
        [Test]
        public void GameplayPauseApplySystem_ActiveSnapshot_MirrorsFlagsAndPauseRequested()
        {
            using var world = CreateWorldWithFixedTick();
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<GameplayPauseApplySystem>();

            var go = new GameObject("GameplayPauseController_ActiveSnapshot");
            try
            {
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;
                controller.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);

                system.Update(world.Unmanaged);

                var pauseState = GetSingleton<GameplayPauseStateComponent>(em);
                var fixedTick = GetSingleton<FixedTickTimeComponent>(em);
                Assert.That(pauseState.Flags, Is.EqualTo(GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput));
                Assert.That(pauseState.ReasonMask, Is.EqualTo(1u << (int)GameplayPauseReasonId.PauseMenu));
                Assert.That(pauseState.Version, Is.GreaterThan(0u));
                Assert.That(fixedTick.EnableFixedTick, Is.EqualTo(1));
                Assert.That(fixedTick.PauseRequested, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GameplayPauseApplySystem_Release_ClearsPauseStateAndPauseRequested()
        {
            using var world = CreateWorldWithFixedTick();
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<GameplayPauseApplySystem>();

            var go = new GameObject("GameplayPauseController_Release");
            try
            {
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;
                var handle = controller.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);

                system.Update(world.Unmanaged);
                Assert.That(controller.Release(handle), Is.True);

                system.Update(world.Unmanaged);

                var pauseState = GetSingleton<GameplayPauseStateComponent>(em);
                var fixedTick = GetSingleton<FixedTickTimeComponent>(em);
                Assert.That(pauseState.Flags, Is.EqualTo(GameplayPauseFlags.None));
                Assert.That(pauseState.ReasonMask, Is.EqualTo(0u));
                Assert.That(fixedTick.PauseRequested, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GameplayPauseApplySystem_WithoutOwner_KeepsZeroState()
        {
            using var world = CreateWorldWithFixedTick();
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<GameplayPauseApplySystem>();

            system.Update(world.Unmanaged);

            var pauseState = GetSingleton<GameplayPauseStateComponent>(em);
            var fixedTick = GetSingleton<FixedTickTimeComponent>(em);
            Assert.That(pauseState.Flags, Is.EqualTo(GameplayPauseFlags.None));
            Assert.That(pauseState.ReasonMask, Is.EqualTo(0u));
            Assert.That(pauseState.Version, Is.EqualTo(0u));
            Assert.That(fixedTick.EnableFixedTick, Is.EqualTo(0));
            Assert.That(fixedTick.PauseRequested, Is.EqualTo(0));
        }

        [Test]
        public void GameplayPauseApplySystem_MultipleHandles_OrFlagsAndReasons()
        {
            using var world = CreateWorldWithFixedTick();
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<GameplayPauseApplySystem>();

            var go = new GameObject("GameplayPauseController_MultipleHandles");
            try
            {
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;
                controller.Acquire(
                    GameplayPauseReasonId.PauseMenu,
                    GameplayPauseFlags.PauseSimulation | GameplayPauseFlags.BlockGameplayInput);
                controller.Acquire(
                    GameplayPauseReasonId.DialogueGate,
                    GameplayPauseFlags.PauseSimulation
                    | GameplayPauseFlags.BlockGameplayInput
                    | GameplayPauseFlags.ExclusivePresentationInput
                    | GameplayPauseFlags.BlockPauseMenuOpen);

                system.Update(world.Unmanaged);

                var pauseState = GetSingleton<GameplayPauseStateComponent>(em);
                Assert.That(
                    pauseState.Flags,
                    Is.EqualTo(
                        GameplayPauseFlags.PauseSimulation
                        | GameplayPauseFlags.BlockGameplayInput
                        | GameplayPauseFlags.ExclusivePresentationInput
                        | GameplayPauseFlags.BlockPauseMenuOpen));
                Assert.That(
                    pauseState.ReasonMask,
                    Is.EqualTo(
                        (1u << (int)GameplayPauseReasonId.PauseMenu)
                        | (1u << (int)GameplayPauseReasonId.DialogueGate)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GameplayPauseApplySystem_DoesNotModifyStepRequested()
        {
            using var world = CreateWorldWithFixedTick();
            var em = world.EntityManager;
            var system = world.GetOrCreateSystem<GameplayPauseApplySystem>();
            var fixedTickEntity = em.CreateEntityQuery(ComponentType.ReadWrite<FixedTickTimeComponent>()).GetSingletonEntity();

            var fixedTick = em.GetComponentData<FixedTickTimeComponent>(fixedTickEntity);
            fixedTick.StepRequested = 1;
            em.SetComponentData(fixedTickEntity, fixedTick);

            var go = new GameObject("GameplayPauseController_StepRequested");
            try
            {
                var controller = go.AddComponent<DemoShellGameplayPauseController>();
                controller.LogBindWarnings = false;
                controller.Acquire(
                    GameplayPauseReasonId.Debug,
                    GameplayPauseFlags.PauseSimulation);

                system.Update(world.Unmanaged);

                var fixedTickAfter = em.GetComponentData<FixedTickTimeComponent>(fixedTickEntity);
                Assert.That(fixedTickAfter.StepRequested, Is.EqualTo(1));
                Assert.That(fixedTickAfter.PauseRequested, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static World CreateWorldWithFixedTick()
        {
            var world = new World("GameplayPauseApplyWorld");
            var em = world.EntityManager;
            var entity = em.CreateEntity(typeof(FixedTickTimeComponent));
            em.SetComponentData(entity, new FixedTickTimeComponent
            {
                EnableFixedTick = 0,
                PauseRequested = 0,
                StepRequested = 0,
                Reserved = 0,
                MaxSubSteps = 4,
                FixedDeltaTime = 1f / 60f,
                Accumulator = 0f,
                Tick = 0u,
            });
            return world;
        }

        private static T GetSingleton<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }
    }
}
