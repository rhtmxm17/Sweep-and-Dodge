using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StagePresentationRuntimeControllerTests
    {
        private World _previousWorld;
        private World _world;
        private EntityManager _em;
        private readonly List<Object> _toDestroy = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _previousWorld = World.DefaultGameObjectInjectionWorld;
            _world = new World("StagePresentationRuntimeControllerTests");
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
        public void Tick_ReadyEdge_BuildsStandalonePresentation()
        {
            var (controller, topologyBridge, stageCatalog, presentationCatalog) = CreateControllerGraph();
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(
                        1,
                        new StagePresentationLayoutData
                        {
                            StableId = 9001,
                            Active = true,
                            PlacementMode = StagePresentationPlacementMode.Standalone,
                            PresentationKey = "preview_visual_01",
                            Position = new Vector3(2f, 0f, 3f),
                            Euler = new Vector3(0f, 45f, 0f),
                            Scale = new Vector3(2f, 1f, 2f),
                        }),
                },
            };
            presentationCatalog.Entries = new[]
            {
                new StagePresentationCatalogEntry
                {
                    PresentationKey = "preview_visual_01",
                    Prefab = CreatePresentationPrefab("preview_visual_01"),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();

            Assert.That(controller.SpawnedRootCount, Is.EqualTo(1));
            Assert.That(controller.transform.childCount, Is.EqualTo(1));

            var child = controller.transform.GetChild(0);
            Assert.That(child.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(child.position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(child.position.z, Is.EqualTo(3f).Within(0.001f));
            Assert.That(child.localScale.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(child.localScale.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(child.localScale.z, Is.EqualTo(2f).Within(0.001f));
            Assert.That(topologyBridge.TryGetTopologyState(out _), Is.True);
        }

        [Test]
        public void Tick_AppliedStageChange_RebuildsForNewStage()
        {
            var (controller, _, stageCatalog, presentationCatalog) = CreateControllerGraph();
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(
                        1,
                        new StagePresentationLayoutData
                        {
                            StableId = 9001,
                            Active = true,
                            PlacementMode = StagePresentationPlacementMode.Standalone,
                            PresentationKey = "preview_visual_01",
                            Position = new Vector3(1f, 0f, 0f),
                            Euler = Vector3.zero,
                            Scale = Vector3.one,
                        }),
                },
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_02",
                    Layout = CreateLayout(
                        2,
                        new StagePresentationLayoutData
                        {
                            StableId = 9002,
                            Active = true,
                            PlacementMode = StagePresentationPlacementMode.Standalone,
                            PresentationKey = "preview_visual_02",
                            Position = new Vector3(5f, 0f, 0f),
                            Euler = Vector3.zero,
                            Scale = Vector3.one,
                        }),
                },
            };
            presentationCatalog.Entries = new[]
            {
                new StagePresentationCatalogEntry
                {
                    PresentationKey = "preview_visual_01",
                    Prefab = CreatePresentationPrefab("preview_visual_01"),
                },
                new StagePresentationCatalogEntry
                {
                    PresentationKey = "preview_visual_02",
                    Prefab = CreatePresentationPrefab("preview_visual_02"),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();
            Assert.That(controller.transform.GetChild(0).position.x, Is.EqualTo(1f).Within(0.001f));

            SetTopologyState(selected: 2, applied: 2, ready: 1);
            controller.Tick();
            Assert.That(controller.SpawnedRootCount, Is.EqualTo(1));
            Assert.That(controller.transform.GetChild(0).position.x, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void Tick_ReadyDropsToZero_ClearsSpawnedPresentations()
        {
            var (controller, _, stageCatalog, presentationCatalog) = CreateControllerGraph();
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(
                        1,
                        new StagePresentationLayoutData
                        {
                            StableId = 9001,
                            Active = true,
                            PlacementMode = StagePresentationPlacementMode.Standalone,
                            PresentationKey = "preview_visual_01",
                            Position = Vector3.zero,
                            Euler = Vector3.zero,
                            Scale = Vector3.one,
                        }),
                },
            };
            presentationCatalog.Entries = new[]
            {
                new StagePresentationCatalogEntry
                {
                    PresentationKey = "preview_visual_01",
                    Prefab = CreatePresentationPrefab("preview_visual_01"),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();
            Assert.That(controller.SpawnedRootCount, Is.EqualTo(1));

            SetTopologyState(selected: 1, applied: 1, ready: 0);
            controller.Tick();
            Assert.That(controller.SpawnedRootCount, Is.EqualTo(0));
            Assert.That(controller.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void Tick_SelectedStageOnlyChange_DoesNotRebuild()
        {
            var (controller, _, stageCatalog, presentationCatalog) = CreateControllerGraph();
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Layout = CreateLayout(
                        1,
                        new StagePresentationLayoutData
                        {
                            StableId = 9001,
                            Active = true,
                            PlacementMode = StagePresentationPlacementMode.Standalone,
                            PresentationKey = "preview_visual_01",
                            Position = new Vector3(2f, 0f, 0f),
                            Euler = Vector3.zero,
                            Scale = Vector3.one,
                        }),
                },
            };
            presentationCatalog.Entries = new[]
            {
                new StagePresentationCatalogEntry
                {
                    PresentationKey = "preview_visual_01",
                    Prefab = CreatePresentationPrefab("preview_visual_01"),
                },
            };

            SetTopologyState(selected: 1, applied: 1, ready: 1);
            controller.Tick();
            var firstChild = controller.transform.GetChild(0);

            SetTopologyState(selected: 2, applied: 1, ready: 1);
            controller.Tick();

            Assert.That(controller.SpawnedRootCount, Is.EqualTo(1));
            Assert.That(controller.transform.GetChild(0), Is.EqualTo(firstChild));
        }

        [Test]
        public void Tick_LinkedObstacle_ResolvesRuntimeAnchor()
        {
            var (controller, _, stageCatalog, presentationCatalog) = CreateControllerGraph();
            stageCatalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_02",
                    Layout = CreateLayout(
                        2,
                        new StagePresentationLayoutData
                        {
                            StableId = 9002,
                            Active = true,
                            PlacementMode = StagePresentationPlacementMode.LinkedToParent,
                            LinkKind = StagePresentationLinkKind.Obstacle,
                            LinkedStableId = 3002,
                            PresentationKey = "wall_basic",
                            Position = new Vector3(1f, 0f, 0f),
                            Euler = new Vector3(0f, 45f, 0f),
                            Scale = Vector3.one,
                        }),
                },
            };
            presentationCatalog.Entries = new[]
            {
                new StagePresentationCatalogEntry
                {
                    PresentationKey = "wall_basic",
                    Prefab = CreatePresentationPrefab("wall_basic"),
                },
            };
            CreateObstacleEntity(3002u, new float3(10f, 0f, 0f), quaternion.RotateY(math.radians(90f)));

            SetTopologyState(selected: 2, applied: 2, ready: 1);
            controller.Tick();

            Assert.That(controller.SpawnedRootCount, Is.EqualTo(1));
            var child = controller.transform.GetChild(0);
            Assert.That(child.position.x, Is.EqualTo(10f).Within(0.01f));
            Assert.That(child.position.z, Is.EqualTo(-1f).Within(0.01f));
            Assert.That(child.rotation.eulerAngles.y, Is.EqualTo(135f).Within(0.5f));
        }

        private (StagePresentationRuntimeController Controller, StageTopologyBridge TopologyBridge, StageCatalogSO StageCatalog, StagePresentationCatalogSO PresentationCatalog) CreateControllerGraph()
        {
            var root = new GameObject("presentation_controller");
            var topologyBridge = root.AddComponent<StageTopologyBridge>();
            topologyBridge.LogBindWarnings = false;

            var controller = root.AddComponent<StagePresentationRuntimeController>();
            controller.LogWarnings = false;
            controller.RebuildOnEnable = true;
            controller.DestroyOnDisable = true;
            controller.TopologyBridge = topologyBridge;

            var stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            stageCatalog.Entries = System.Array.Empty<StageCatalogEntry>();
            var presentationCatalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();
            presentationCatalog.Entries = System.Array.Empty<StagePresentationCatalogEntry>();

            controller.StageCatalog = stageCatalog;
            controller.PresentationCatalog = presentationCatalog;
            topologyBridge.StageCatalog = stageCatalog;

            _toDestroy.Add(stageCatalog);
            _toDestroy.Add(presentationCatalog);
            _toDestroy.Add(root);
            return (controller, topologyBridge, stageCatalog, presentationCatalog);
        }

        private StageLayoutSO CreateLayout(int stageId, params StagePresentationLayoutData[] presentations)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.StageId = stageId;
            layout.Sources = System.Array.Empty<StageSourceLayoutData>();
            layout.Deposits = System.Array.Empty<StageDepositLayoutData>();
            layout.Obstacles = System.Array.Empty<StageObstacleLayoutData>();
            layout.Presentations = presentations;
            _toDestroy.Add(layout);
            return layout;
        }

        private GameObject CreatePresentationPrefab(string name)
        {
            var prefab = new GameObject(name);
            _toDestroy.Add(prefab);
            return prefab;
        }

        private void CreateObstacleEntity(uint stableId, float3 position, quaternion rotation)
        {
            var entity = _em.CreateEntity(
                typeof(ObstacleStableIdComponent),
                typeof(LocalTransform),
                typeof(ObstacleGeometryComponent));
            _em.SetComponentData(entity, new ObstacleStableIdComponent { Value = stableId });
            _em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, rotation, 1f));
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
