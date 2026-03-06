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
        public void Bridge_RequestStageMapApply_WritesRequestAndCatalogSingleton()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            StageMapCatalogSO stageMapCatalog = null;
            StageCatalogSO stageCatalog = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_StageMap");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));

                stageMapCatalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
                stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();

                go = new GameObject("RunDirectorStageBridge_Edit_StageMap");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;
                bridge.StageMapCatalog = stageMapCatalog;
                bridge.StageCatalog = stageCatalog;

                Assert.That(bridge.RequestStageMapApply(7), Is.True);

                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(request.RequestedStageId, Is.EqualTo(7));
                Assert.That(request.StageMapApplyRequested, Is.EqualTo(1));

                using var stageMapRuntimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageMapCatalogRuntimeComponent>());
                Assert.That(stageMapRuntimeQuery.IsEmptyIgnoreFilter, Is.False);
                var runtimeEntity = stageMapRuntimeQuery.GetSingletonEntity();
                var runtime = em.GetComponentObject<StageMapCatalogRuntimeComponent>(runtimeEntity);
                Assert.That(runtime, Is.Not.Null);
                Assert.That(runtime.Catalog, Is.SameAs(stageMapCatalog));

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
                if (stageMapCatalog != null)
                    Object.DestroyImmediate(stageMapCatalog);
                if (stageCatalog != null)
                    Object.DestroyImmediate(stageCatalog);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void Bridge_RequestStageMapApply_ComposesLegacyStageMapCatalogFromStageCatalog()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            StageCatalogSO stageCatalog = null;
            StageDefinitionSO definition = null;
            StageLayoutSO layout = null;
            try
            {
                world = new World("RunDirectorStageBridgeEditWorld_ComposeStageMap");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(requestEntity, default(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));

                definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
                definition.StageId = 3;
                definition.DisplayName = "Stage 3";
                definition.StageTimeLimitSec = 120f;

                layout = ScriptableObject.CreateInstance<StageLayoutSO>();
                layout.StageId = 3;
                layout.Sources = new[]
                {
                    new StageSourceLayoutData
                    {
                        StableId = 1001u,
                        Active = true,
                    },
                };

                stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                stageCatalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_03",
                        Definition = definition,
                        Layout = layout,
                    },
                };

                go = new GameObject("RunDirectorStageBridge_Edit_ComposeStageMap");
                var bridge = go.AddComponent<RunDirectorStageBridge>();
                bridge.LogBindWarnings = false;
                bridge.StageCatalog = stageCatalog;

                Assert.That(bridge.RequestStageMapApply(3), Is.True);

                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(request.RequestedStageId, Is.EqualTo(3));
                Assert.That(request.StageMapApplyRequested, Is.EqualTo(1));

                using var runtimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageMapCatalogRuntimeComponent>());
                Assert.That(runtimeQuery.IsEmptyIgnoreFilter, Is.False);
                var runtimeEntity = runtimeQuery.GetSingletonEntity();
                var runtime = em.GetComponentObject<StageMapCatalogRuntimeComponent>(runtimeEntity);
                Assert.That(runtime, Is.Not.Null);
                Assert.That(runtime.Catalog, Is.Not.Null);
                Assert.That(runtime.Catalog, Is.Not.SameAs(layout));
                Assert.That(runtime.Catalog.Stages, Has.Length.EqualTo(1));
                Assert.That(runtime.Catalog.Stages[0].StageId, Is.EqualTo(3));
                Assert.That(runtime.Catalog.Stages[0].Sources, Has.Length.EqualTo(1));
                Assert.That(runtime.Catalog.Stages[0].Sources[0].StableId, Is.EqualTo(1001u));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                if (stageCatalog != null)
                    Object.DestroyImmediate(stageCatalog);
                if (definition != null)
                    Object.DestroyImmediate(definition);
                if (layout != null)
                    Object.DestroyImmediate(layout);
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
