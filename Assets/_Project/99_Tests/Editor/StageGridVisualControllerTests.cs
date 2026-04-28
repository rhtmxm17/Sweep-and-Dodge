using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageGridVisualControllerTests
    {
        private World _previousWorld;
        private World _world;
        private EntityManager _em;
        private readonly List<Object> _toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _previousWorld = World.DefaultGameObjectInjectionWorld;
            _world = new World("StageGridVisualControllerTests");
            World.DefaultGameObjectInjectionWorld = _world;
            _em = _world.EntityManager;

            var topologyRequest = _em.CreateEntity(typeof(StageTopologyRequestComponent));
            _em.SetComponentData(topologyRequest, default(StageTopologyRequestComponent));
            var topologyState = _em.CreateEntity(typeof(StageTopologyStateComponent));
            _em.SetComponentData(topologyState, default(StageTopologyStateComponent));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _toDestroy.Count - 1; i >= 0; i--)
            {
                if (_toDestroy[i] != null)
                    Object.DestroyImmediate(_toDestroy[i]);
            }

            _toDestroy.Clear();
            _world.Dispose();
            World.DefaultGameObjectInjectionWorld = _previousWorld;
        }

        [Test]
        public void Tick_ReadyEdge_BuildsGridVisualAtLayoutOrigin()
        {
            var (controller, _, stageCatalog) = CreateControllerGraph();
            var prefab = CreateGridVisualPrefab("grid_visual_01", Quaternion.Euler(90f, 0f, 0f));
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(1, new Vector3(2f, 0f, 3f), prefab),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();

            Assert.That(controller.CurrentInstance, Is.Not.Null);
            Assert.That(controller.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.CurrentInstance.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(controller.CurrentInstance.transform.position.z, Is.EqualTo(3f).Within(0.001f));
            Assert.That(controller.CurrentInstance.transform.rotation.eulerAngles.x, Is.EqualTo(90f).Within(0.5f));
        }

        [Test]
        public void Tick_AppliedStageChange_RebuildsForNewStage()
        {
            var (controller, _, stageCatalog) = CreateControllerGraph();
            var prefab1 = CreateGridVisualPrefab("grid_visual_01", Quaternion.identity);
            var prefab2 = CreateGridVisualPrefab("grid_visual_02", Quaternion.identity);
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(1, new Vector3(1f, 0f, 0f), prefab1),
                },
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_02",
                    Layout = CreateLayout(2, new Vector3(5f, 0f, 0f), prefab2),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();
            var firstInstance = controller.CurrentInstance;

            SetTopologyState(selected: 2, applied: 2, ready: 1);
            controller.Tick();

            Assert.That(controller.CurrentInstance, Is.Not.Null);
            Assert.That(controller.CurrentInstance, Is.Not.EqualTo(firstInstance));
            Assert.That(controller.transform.childCount, Is.EqualTo(1));
            Assert.That(controller.CurrentInstance.transform.position.x, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void Tick_ReadyDropsToZero_ClearsGridVisual()
        {
            var (controller, _, stageCatalog) = CreateControllerGraph();
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(1, Vector3.zero, CreateGridVisualPrefab("grid_visual_01", Quaternion.identity)),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();
            Assert.That(controller.CurrentInstance, Is.Not.Null);

            SetTopologyState(selected: 1, applied: 1, ready: 0);
            controller.Tick();

            Assert.That(controller.CurrentInstance, Is.Null);
            Assert.That(controller.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void Tick_MissingPrefab_MarksStageReadyWithoutInstance()
        {
            var (controller, _, stageCatalog) = CreateControllerGraph();
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(1, Vector3.zero, null),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();

            Assert.That(controller.CurrentInstance, Is.Null);
            Assert.That(controller.LastAppliedStageId, Is.EqualTo(1));
            Assert.That(controller.LastReady, Is.True);
            Assert.That(controller.transform.childCount, Is.EqualTo(0));
        }

        private (StageGridVisualController Controller, StageTopologyBridge TopologyBridge, StageCatalogSO StageCatalog) CreateControllerGraph()
        {
            var root = new GameObject("grid_visual_controller");
            var topologyBridge = root.AddComponent<StageTopologyBridge>();
            topologyBridge.LogBindWarnings = false;

            var controller = root.AddComponent<StageGridVisualController>();
            controller.TopologyBridge = topologyBridge;

            var stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            stageCatalog.Entries = System.Array.Empty<StageCatalogEntry>();
            controller.StageCatalog = stageCatalog;
            topologyBridge.StageCatalog = stageCatalog;

            _toDestroy.Add(stageCatalog);
            _toDestroy.Add(root);
            return (controller, topologyBridge, stageCatalog);
        }

        private StageLayoutSO CreateLayout(int stageId, Vector3 origin, GameObject gridVisualPrefab)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.SchemaVersion = 2;
            layout.StageId = stageId;
            layout.Grid = new StageGridSpec { Width = 1, Height = 1, CellSize = 1f, Origin = origin };
            layout.Cells = new[] { new StageCellLayoutData() };
            layout.SourceRegions = System.Array.Empty<StageSourceRegionLayoutData>();
            layout.DepositRegions = System.Array.Empty<StageDepositRegionLayoutData>();
            layout.PlayerStart = new StagePlayerStartLayoutData
            {
                Active = true,
                AnchorCell = Vector2Int.zero,
                AnchorOffset = Vector2.zero,
                YawDeg = 0f,
            };
            layout.Presentations = System.Array.Empty<StagePresentationLayoutData>();
            layout.GridVisualPrefab = gridVisualPrefab;
            _toDestroy.Add(layout);
            return layout;
        }

        private GameObject CreateGridVisualPrefab(string name, Quaternion rotation)
        {
            var prefab = new GameObject(name);
            prefab.transform.rotation = rotation;
            prefab.AddComponent<Grid>();
            _toDestroy.Add(prefab);
            return prefab;
        }

        private void SetTopologyState(int selected, int applied, byte ready)
        {
            using var query = _em.CreateEntityQuery(ComponentType.ReadWrite<StageTopologyStateComponent>());
            var entity = query.GetSingletonEntity();
            _em.SetComponentData(entity, new StageTopologyStateComponent
            {
                SelectedStageId = selected,
                AppliedStageId = applied,
                Ready = ready,
            });
        }
    }
}
