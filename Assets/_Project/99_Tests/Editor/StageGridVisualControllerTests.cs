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
            Assert.That(controller.CellOverlayRoot, Is.Not.Null);
            Assert.That(controller.transform.childCount, Is.EqualTo(2));
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
            Assert.That(controller.transform.childCount, Is.EqualTo(2));
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
            Assert.That(controller.CellOverlayRoot, Is.Null);
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
            Assert.That(controller.CellOverlayRoot, Is.Not.Null);
            Assert.That(controller.LastAppliedStageId, Is.EqualTo(1));
            Assert.That(controller.LastReady, Is.True);
            Assert.That(controller.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void StaticGeometry_SourceAndDepositUseFillAndOuterPerimeterWithoutHatch()
        {
            var mesh = new Mesh();
            _toDestroy.Add(mesh);
            var grid = new StageGridSpec
            {
                Width = 2,
                Height = 1,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            var cells = new[]
            {
                new StageCellLayoutData { SourceRegionId = 7u, DepositRegionId = 9u },
                new StageCellLayoutData { SourceRegionId = 7u, DepositRegionId = 9u },
            };

            var stats = StageCellOverlayGeometryBuilder.BuildStaticMesh(in grid, cells, mesh);

            Assert.That(stats.SourceOutlineQuadCount, Is.EqualTo(6), "Shared internal Source edge must not be drawn.");
            Assert.That(stats.DepositFillQuadCount, Is.EqualTo(2));
            Assert.That(stats.DepositOutlineQuadCount, Is.EqualTo(6), "Shared internal Deposit edge must not be drawn.");
            Assert.That(stats.MovementFillQuadCount, Is.Zero);
            Assert.That(stats.MovementHatchQuadCount, Is.Zero, "Source/Deposit must not emit diagonal or X hatch geometry.");
            Assert.That(CountVerticesWithColor(mesh, StageCellOverlayGeometryBuilder.SourceOutlineColor), Is.EqualTo(24));
            Assert.That(CountVerticesWithColor(mesh, StageCellOverlayGeometryBuilder.DepositFillColor), Is.EqualTo(8));
            Assert.That(CountVerticesWithColor(mesh, StageCellOverlayGeometryBuilder.DepositOutlineColor), Is.EqualTo(24));
        }

        [Test]
        public void StaticGeometry_MovementBlockingRetainsDirectionalHatchContract()
        {
            var mesh = new Mesh();
            _toDestroy.Add(mesh);
            var grid = new StageGridSpec
            {
                Width = 3,
                Height = 1,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            var cells = new[]
            {
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.BlockPlayer },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.BlockBullet },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet },
            };

            var stats = StageCellOverlayGeometryBuilder.BuildStaticMesh(in grid, cells, mesh);

            Assert.That(stats.MovementFillQuadCount, Is.EqualTo(1));
            Assert.That(stats.MovementHatchQuadCount, Is.EqualTo(8));
            Assert.That(CountVerticesWithColor(mesh, StageCellOverlayGeometryBuilder.BlockPlayerColor), Is.EqualTo(16));
            Assert.That(CountVerticesWithColor(mesh, StageCellOverlayGeometryBuilder.BlockBulletColor), Is.EqualTo(16));
        }

        [Test]
        public void SourceOverlay_UsesRuntimeGridValidMaskCoordinatesAndFadeContract()
        {
            var (controller, _, stageCatalog) = CreateControllerGraph();
            var layout = CreateLayout(1, new Vector3(10f, 0f, 20f), null);
            layout.Grid = new StageGridSpec { Width = 8, Height = 8, CellSize = 1f, Origin = new Vector3(10f, 0f, 20f) };
            layout.Cells = new StageCellLayoutData[64];
            layout.Cells[0].SourceRegionId = 7u;
            layout.SourceRegions = new[]
            {
                new StageSourceRegionLayoutData { StableId = 7u, Active = true },
            };
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry { Enabled = true, EntryKey = "stage_01", Layout = layout },
            };
            Entity sourceEntity = CreateSourceEntity(7u, new SourcePollutionGridComponent
            {
                Cols = 2,
                Rows = 1,
                CellSize = 2f,
                InvCellSize = 0.5f,
                OriginX = 12f,
                OriginZ = 24f,
            });
            var pollution = _em.GetBuffer<SourcePollutionCellBuffer>(sourceEntity);
            pollution.Add(new SourcePollutionCellBuffer { IsValid = 1, IsActive = 1 });
            pollution.Add(new SourcePollutionCellBuffer { IsValid = 0, IsActive = 0 });

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick(0f);

            Assert.That(controller.SourceOverlayCount, Is.EqualTo(1));
            Assert.That(controller.TryGetSourceCellAlpha(7u, 0, out float activeAlpha), Is.True);
            Assert.That(activeAlpha, Is.EqualTo(StageGridVisualController.SourceActiveAlpha).Within(0.001f));
            Assert.That(controller.TryGetSourceCellAlpha(7u, 1, out _), Is.False, "Invalid pollution cells must not produce geometry.");
            var sourceMesh = controller.CellOverlayRoot.transform.Find("Source_7").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(sourceMesh.vertexCount, Is.EqualTo(4));
            Assert.That(sourceMesh.vertices[0].x, Is.EqualTo(2.11f).Within(0.001f));
            Assert.That(sourceMesh.vertices[0].z, Is.EqualTo(4.11f).Within(0.001f));

            pollution[0] = new SourcePollutionCellBuffer { IsValid = 1, IsActive = 0 };
            controller.Tick(0.1f);
            controller.Tick(0.1f);
            Assert.That(controller.TryGetSourceCellAlpha(7u, 0, out float inactiveAlpha), Is.True);
            Assert.That(inactiveAlpha, Is.EqualTo(StageGridVisualController.SourceInactiveAlpha).Within(0.001f));

            pollution[0] = new SourcePollutionCellBuffer { IsValid = 1, IsActive = 1 };
            controller.Tick(0.1f);
            controller.Tick(0.1f);
            controller.Tick(0.15f);
            Assert.That(controller.TryGetSourceCellAlpha(7u, 0, out float restoredAlpha), Is.True);
            Assert.That(restoredAlpha, Is.EqualTo(StageGridVisualController.SourceActiveAlpha).Within(0.001f));

            var source = _em.GetComponentData<SourceSpawnComponent>(sourceEntity);
            source.State = SourceStateId.Depleted;
            _em.SetComponentData(sourceEntity, source);
            controller.Tick(0.1f);
            controller.Tick(0.1f);
            Assert.That(controller.TryGetSourceCellAlpha(7u, 0, out float depletedAlpha), Is.True);
            Assert.That(depletedAlpha, Is.EqualTo(StageGridVisualController.SourceDepletedAlpha).Within(0.001f));
        }

        [Test]
        public void SourceOverlay_WarmSteadyAndFadeTicks_DoNotAllocateManagedMemory()
        {
            var (controller, _, stageCatalog) = CreateControllerGraph();
            controller.PollutionPollIntervalSec = 0.01f;
            var layout = CreateLayout(1, Vector3.zero, null);
            layout.Cells[0].SourceRegionId = 3u;
            layout.SourceRegions = new[]
            {
                new StageSourceRegionLayoutData { StableId = 3u, Active = true },
            };
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry { Enabled = true, EntryKey = "stage_01", Layout = layout },
            };
            Entity sourceEntity = CreateSourceEntity(3u, new SourcePollutionGridComponent
            {
                Cols = 1,
                Rows = 1,
                CellSize = 1f,
                InvCellSize = 1f,
                OriginX = 0f,
                OriginZ = 0f,
            });
            var pollution = _em.GetBuffer<SourcePollutionCellBuffer>(sourceEntity);
            pollution.Add(new SourcePollutionCellBuffer { IsValid = 1, IsActive = 1 });

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick(0f);
            controller.PollutionPollIntervalSec = 1000f;
            controller.Tick(0.01f);

            long steadyBefore = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++)
                controller.Tick(1f / 60f);
            long steadyAllocated = System.GC.GetAllocatedBytesForCurrentThread() - steadyBefore;
            Assert.That(steadyAllocated, Is.EqualTo(0L));

            controller.PollutionPollIntervalSec = 0.01f;
            controller.SourceFadeOutSec = 1000000000f;
            pollution[0] = new SourcePollutionCellBuffer { IsValid = 1, IsActive = 0 };
            controller.Tick(1000f);
            controller.PollutionPollIntervalSec = 1000f;
            controller.SourceFadeOutSec = 0.2f;
            controller.Tick(0.01f);

            long fadeBefore = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 8; i++)
                controller.Tick(0.01f);
            long fadeAllocated = System.GC.GetAllocatedBytesForCurrentThread() - fadeBefore;
            Assert.That(fadeAllocated, Is.EqualTo(0L));
        }

        private (StageGridVisualController Controller, StageTopologyBridge TopologyBridge, StageCatalogSO StageCatalog) CreateControllerGraph()
        {
            var root = new GameObject("grid_visual_controller");
            var topologyBridge = root.AddComponent<StageTopologyBridge>();
            topologyBridge.LogBindWarnings = false;

            var controller = root.AddComponent<StageGridVisualController>();
            controller.TopologyBridge = topologyBridge;
            var shader = Shader.Find("SweepNDodge/StageCellOverlay");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            controller.CellOverlayMaterial = material;

            var stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            stageCatalog.Entries = System.Array.Empty<StageCatalogEntry>();
            controller.StageCatalog = stageCatalog;
            topologyBridge.StageCatalog = stageCatalog;

            _toDestroy.Add(stageCatalog);
            _toDestroy.Add(material);
            _toDestroy.Add(root);
            return (controller, topologyBridge, stageCatalog);
        }

        private Entity CreateSourceEntity(uint stableId, SourcePollutionGridComponent grid)
        {
            var entity = _em.CreateEntity(
                typeof(StageTopologyOwnedTag),
                typeof(StageTopologySourceTag),
                typeof(SourceStableIdComponent),
                typeof(SourceSpawnComponent),
                typeof(SourcePollutionGridComponent));
            _em.SetComponentData(entity, new SourceStableIdComponent { Value = stableId });
            _em.SetComponentData(entity, new SourceSpawnComponent { State = SourceStateId.Normal });
            _em.SetComponentData(entity, grid);
            _em.AddBuffer<SourcePollutionCellBuffer>(entity);
            return entity;
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

        private static int CountVerticesWithColor(Mesh mesh, Color32 expected)
        {
            int count = 0;
            var colors = mesh.colors32;
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].Equals(expected))
                    count++;
            }

            return count;
        }
    }
}
