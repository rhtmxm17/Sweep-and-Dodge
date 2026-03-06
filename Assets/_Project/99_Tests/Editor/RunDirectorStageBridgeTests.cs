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
        public void Bridge_RequestStageApply_WritesRequestAndCatalogSingleton()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            StageCatalogSO stageCatalog = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_StageApply");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));

                stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();

                go = new GameObject("RunDirectorStageBridge_Edit_StageApply");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;
                bridge.StageCatalog = stageCatalog;

                Assert.That(bridge.RequestStageApply(7), Is.True);

                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(request.RequestedStageId, Is.EqualTo(7));
                Assert.That(request.StageApplyRequested, Is.EqualTo(1));

                using var stageCatalogRuntimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageCatalogRuntimeComponent>());
                Assert.That(stageCatalogRuntimeQuery.IsEmptyIgnoreFilter, Is.False);
                var stageCatalogRuntimeEntity = stageCatalogRuntimeQuery.GetSingletonEntity();
                var stageCatalogRuntime = em.GetComponentObject<StageCatalogRuntimeComponent>(stageCatalogRuntimeEntity);
                Assert.That(stageCatalogRuntime, Is.Not.Null);
                Assert.That(stageCatalogRuntime.Catalog, Is.SameAs(stageCatalog));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                if (stageCatalog != null)
                    Object.DestroyImmediate(stageCatalog);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void Bridge_RequestStageMapApply_ForwardsToRequestStageApply()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            StageCatalogSO stageCatalog = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_ObsoleteForward");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));

                stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();

                go = new GameObject("RunDirectorStageBridge_Edit_ObsoleteForward");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;
                bridge.StageCatalog = stageCatalog;

#pragma warning disable CS0618
                Assert.That(bridge.RequestStageMapApply(3), Is.True);
#pragma warning restore CS0618

                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(request.RequestedStageId, Is.EqualTo(3));
                Assert.That(request.StageApplyRequested, Is.EqualTo(1));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                if (stageCatalog != null)
                    Object.DestroyImmediate(stageCatalog);
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

                // 같은 프레임에 신호가 다시 올라와도 중복 발행 방지.
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
