using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageTopologyBridgeTests
    {
        [Test]
        public void Bridge_RequestTopologyApply_WritesOnlyTopologyRequest_AndPublishesCatalog()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            StageCatalogSO stageCatalog = null;
            StageTopologyPrefabCatalogSO topologyCatalog = null;
            try
            {
                world = new World("StageTopologyBridgeEditWorld_A");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(StageTopologyRequestComponent));
                var topologyStateEntity = em.CreateEntity(typeof(StageTopologyStateComponent));
                em.SetComponentData(topologyStateEntity, new StageTopologyStateComponent
                {
                    SelectedStageId = 1,
                    AppliedStageId = 1,
                    Ready = 1,
                });
                em.CreateEntity(typeof(StageTopologyPrefabCatalogComponent));
                var runDirectorRequestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(runDirectorRequestEntity, default(RunDirectorStageRequestComponent));

                stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                topologyCatalog = ScriptableObject.CreateInstance<StageTopologyPrefabCatalogSO>();

                go = new GameObject("StageTopologyBridge_Edit");
                var bridge = go.AddComponent<StageTopologyBridge>();
                bridge.LogBindWarnings = false;
                bridge.StageCatalog = stageCatalog;
                bridge.TopologyPrefabCatalog = topologyCatalog;

                Assert.That(bridge.RequestTopologyApply(7), Is.True);

                var request = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingleton<StageTopologyRequestComponent>();
                Assert.That(request.RequestedStageId, Is.EqualTo(7));
                Assert.That(request.ApplyRequested, Is.EqualTo(1));

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
                Assert.That(topologyState.SelectedStageId, Is.EqualTo(1));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(1));

                var runDirectorRequest = em.GetComponentData<RunDirectorStageRequestComponent>(runDirectorRequestEntity);
                Assert.That(runDirectorRequest.StageStartRequested, Is.EqualTo(0));
                Assert.That(runDirectorRequest.ConfirmPressed, Is.EqualTo(0));

                using var stageCatalogRuntimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageCatalogRuntimeComponent>());
                Assert.That(stageCatalogRuntimeQuery.IsEmptyIgnoreFilter, Is.False);
                var stageCatalogRuntime = em.GetComponentObject<StageCatalogRuntimeComponent>(stageCatalogRuntimeQuery.GetSingletonEntity());
                Assert.That(stageCatalogRuntime.Catalog, Is.SameAs(stageCatalog));

                using var topologyCatalogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>());
                Assert.That(topologyCatalogQuery.IsEmptyIgnoreFilter, Is.False);
                var topologyPrefabs = em.GetComponentData<StageTopologyPrefabCatalogComponent>(topologyCatalogQuery.GetSingletonEntity());
                Assert.That(topologyPrefabs.SourceTemplate, Is.Not.EqualTo(Entity.Null));
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                if (stageCatalog != null)
                    Object.DestroyImmediate(stageCatalog);
                if (topologyCatalog != null)
                    Object.DestroyImmediate(topologyCatalog);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }

        [Test]
        public void Bridge_TryGetTopologyState_ReadsSingleton()
        {
            var oldDefault = World.DefaultGameObjectInjectionWorld;
            World world = null;
            GameObject go = null;
            try
            {
                world = new World("StageTopologyBridgeEditWorld_B");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(StageTopologyRequestComponent));
                var topologyStateEntity = em.CreateEntity(typeof(StageTopologyStateComponent));
                em.SetComponentData(topologyStateEntity, new StageTopologyStateComponent
                {
                    SelectedStageId = 3,
                    AppliedStageId = 2,
                    Ready = 0,
                });

                go = new GameObject("StageTopologyBridge_Edit_State");
                var bridge = go.AddComponent<StageTopologyBridge>();
                bridge.LogBindWarnings = false;

                Assert.That(bridge.TryGetTopologyState(out var state), Is.True);
                Assert.That(state.SelectedStageId, Is.EqualTo(3));
                Assert.That(state.AppliedStageId, Is.EqualTo(2));
                Assert.That(state.Ready, Is.EqualTo(0));
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
            StageCatalogSO stageCatalog = null;
            try
            {
                world = new World("StageTopologyBridgeEditWorld_C");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;

                em.CreateEntity(typeof(StageTopologyRequestComponent));
                em.CreateEntity(typeof(StageTopologyStateComponent));
                em.CreateEntity(typeof(StageTopologyPrefabCatalogComponent));
                stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();

                goA = new GameObject("StageTopologyBridge_A");
                var bridgeA = goA.AddComponent<StageTopologyBridge>();
                bridgeA.LogBindWarnings = false;
                bridgeA.StageCatalog = stageCatalog;

                goB = new GameObject("StageTopologyBridge_B");
                var bridgeB = goB.AddComponent<StageTopologyBridge>();
                bridgeB.LogBindWarnings = false;
                bridgeB.StageCatalog = stageCatalog;

                Assert.That(bridgeA.RequestTopologyApply(2), Is.True);
                var requestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingletonEntity();
                var requestAfterA = em.GetComponentData<StageTopologyRequestComponent>(requestEntity);
                Assert.That(requestAfterA.RequestedStageId, Is.EqualTo(2));
                Assert.That(requestAfterA.ApplyRequested, Is.EqualTo(1));

                em.SetComponentData(requestEntity, default(StageTopologyRequestComponent));
                Assert.That(bridgeB.RequestTopologyApply(3), Is.False);
                var requestAfterB = em.GetComponentData<StageTopologyRequestComponent>(requestEntity);
                Assert.That(requestAfterB.RequestedStageId, Is.EqualTo(0));
                Assert.That(requestAfterB.ApplyRequested, Is.EqualTo(0));
            }
            finally
            {
                if (goA != null)
                    Object.DestroyImmediate(goA);
                if (goB != null)
                    Object.DestroyImmediate(goB);
                if (stageCatalog != null)
                    Object.DestroyImmediate(stageCatalog);
                world?.Dispose();
                World.DefaultGameObjectInjectionWorld = oldDefault;
            }
        }
    }
}
