using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class RunDirectorStageBridgeTests
    {
        [Test]
        public void Bridge_RequestsAndGateWrites_AreAppliedToEcsSingletons()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_A");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));

                var gateEntity = em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.SetComponentData(gateEntity, default(RunDirectorStageGateComponent));

                var signalEntity = em.CreateEntity(typeof(RunDirectorStageSignalComponent));
                em.SetComponentData(signalEntity, default(RunDirectorStageSignalComponent));

                go = new GameObject("RunDirectorStageBridge_Edit");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;

                Assert.That(bridge.SetIntroPresentationDone(true), Is.True);
                Assert.That(bridge.SetClearPresentationDone(true), Is.True);
                Assert.That(bridge.RequestStageStart(), Is.True);
                Assert.That(bridge.RequestConfirm(), Is.True);

                var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);
                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(gate.IntroPresentationDone, Is.EqualTo(1));
                Assert.That(gate.ClearPresentationDone, Is.EqualTo(1));
                Assert.That(request.StageStartRequested, Is.EqualTo(1));
                Assert.That(request.ConfirmPressed, Is.EqualTo(1));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void Bridge_BindsWithoutTopologySingletons()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_NoTopology");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));

                go = new GameObject("RunDirectorStageBridge_Edit_NoTopology");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;

                Assert.That(bridge.RequestStageStart(), Is.True);
                Assert.That(bridge.SetIntroPresentationDone(true), Is.True);
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void Bridge_StageCompletedSignal_IsPublishedOncePerFrame_AndReset()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_B");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                var signalEntity = em.CreateEntity(typeof(RunDirectorStageSignalComponent));
                em.SetComponentData(signalEntity, new RunDirectorStageSignalComponent
                {
                    StageRunCompleted = 1
                });

                go = new GameObject("RunDirectorStageBridge_Edit_Signal");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;

                int fired = 0;
                bridge.StageRunCompleted += () => fired++;

                bridge.Tick();
                Assert.That(fired, Is.EqualTo(1));
                Assert.That(em.GetComponentData<RunDirectorStageSignalComponent>(signalEntity).StageRunCompleted, Is.EqualTo(0));

                em.SetComponentData(signalEntity, new RunDirectorStageSignalComponent
                {
                    StageRunCompleted = 1
                });
                bridge.Tick();
                Assert.That(fired, Is.EqualTo(1));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void Bridge_TryGetStageState_ReadsSingleton()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_D");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));
                var stateEntity = em.CreateEntity(typeof(RunDirectorStageStateComponent));
                em.SetComponentData(stateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.ClearReady,
                    StateElapsedSec = 1.5f,
                    EnteredFrame = 120u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.AllSourcesDepleted
                });

                go = new GameObject("RunDirectorStageBridge_Edit_State");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;

                Assert.That(bridge.TryGetStageState(out var state), Is.True);
                Assert.That(state.State, Is.EqualTo(RunDirectorStageStateId.ClearReady));
                Assert.That(state.StateElapsedSec, Is.EqualTo(1.5f).Within(1e-4f));
                Assert.That(state.EnteredFrame, Is.EqualTo(120u));
                Assert.That(state.LastTransitionReason, Is.EqualTo(RunDirectorStageTransitionReasonId.AllSourcesDepleted));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void Bridge_AllowsOnlyOneInstancePerScene()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject goA = null;
            GameObject goB = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_C");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));

                goA = new GameObject("RunDirectorStageBridge_A");
                var bridgeA = goA.AddComponent<RunDirectorStageBridge>();
                bridgeA.LogBindWarnings = false;

                goB = new GameObject("RunDirectorStageBridge_B");
                var bridgeB = goB.AddComponent<RunDirectorStageBridge>();
                bridgeB.LogBindWarnings = false;

                var requestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageRequestComponent>()).GetSingletonEntity();

                Assert.That(bridgeA.RequestStageStart(), Is.True);
                var requestAfterA = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(requestAfterA.StageStartRequested, Is.EqualTo(1));

                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));
                Assert.That(bridgeB.RequestStageStart(), Is.False);
                var requestAfterB = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(requestAfterB.StageStartRequested, Is.EqualTo(0));
            }
            finally
            {
                if (goA != null)
                    Object.DestroyImmediate(goA);
                if (goB != null)
                    Object.DestroyImmediate(goB);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }
    }
}
