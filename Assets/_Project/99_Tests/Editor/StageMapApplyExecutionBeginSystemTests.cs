using System.Collections.Generic;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageMapApplyExecutionBeginSystemTests
    {
        [Test]
        public void StageMapApply_RequestApplied_RebuildsDefinitionAndDisablesInvalidSources()
        {
            using var world = CreateDefaultTestWorld("StageMapApplyWorld_A", out _);
            var em = world.EntityManager;
            var applySystem = world.GetOrCreateSystem<StageMapApplyExecutionBeginSystem>();
            var createdAssets = new List<ScriptableObject>();

            var sourceMatched = CreateSource(em, 1001u, new float3(-4f, 0f, -2f), state: SourceStateId.Normal, version: 7u);
            var sourceMissingBinding = CreateSource(em, 1002u, new float3(6f, 0f, -2f), state: SourceStateId.Normal, version: 3u);
            var sourceLayoutInactive = CreateSource(em, 1003u, new float3(8f, 0f, -2f), state: SourceStateId.Normal, version: 4u);
            var sourceDuplicateA = CreateSource(em, 1004u, new float3(10f, 0f, -2f), state: SourceStateId.Normal, version: 5u);
            var sourceDuplicateB = CreateSource(em, 1004u, new float3(12f, 0f, -2f), state: SourceStateId.Normal, version: 6u);
            var sourceUnmapped = CreateSource(em, 9001u, new float3(14f, 0f, -2f), state: SourceStateId.Normal, version: 2u);

            var depositMapped = CreateDeposit(em, 2001u, new float3(-8f, 0f, 0f), radius: 10f);
            var depositUnmapped = CreateDeposit(em, 9002u, new float3(7f, 0f, 7f), radius: 3f);

            var stageMapCatalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
            var stageCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            createdAssets.Add(stageMapCatalog);
            createdAssets.Add(stageCatalog);

            try
            {
                var definition = CreateDefinition(createdAssets, 1);
                var bullet = CreateBulletDefinition(createdAssets, 101);
                var sustainClip = CreateSustainClip(createdAssets, bullet, clipId: 501);
                var eventClip = CreateEventClip(createdAssets, bullet, clipId: 601);

                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1001u,
                        InitialSourceState = SourceStateId.Weakened,
                        ThresholdWeakened = 50,
                        ThresholdDepleted = 90,
                        SustainSlots = new[]
                        {
                            new SustainSlotBinding
                            {
                                State = SourceStateId.Weakened,
                                Lane = SourceSpawnLaneId.Hazard,
                                Clips = new[] { sustainClip },
                                Weights = new[] { 1f },
                            },
                        },
                        EventSlots = new[]
                        {
                            new EventSlotBinding
                            {
                                TriggerState = SourceStateId.Weakened,
                                EventClips = new[] { eventClip },
                            },
                        },
                    },
                    new StageSourceBinding
                    {
                        SourceStableId = 1003u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 10,
                        ThresholdDepleted = 20,
                        SustainSlots = new[]
                        {
                            new SustainSlotBinding
                            {
                                State = SourceStateId.Normal,
                                Lane = SourceSpawnLaneId.Trash,
                                Clips = new[] { sustainClip },
                                Weights = new[] { 1f },
                            },
                        },
                        EventSlots = new EventSlotBinding[0],
                    },
                    new StageSourceBinding
                    {
                        SourceStableId = 1004u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 12,
                        ThresholdDepleted = 24,
                        SustainSlots = new[]
                        {
                            new SustainSlotBinding
                            {
                                State = SourceStateId.Normal,
                                Lane = SourceSpawnLaneId.Hazard,
                                Clips = new[] { sustainClip },
                                Weights = new[] { 1f },
                            },
                        },
                        EventSlots = new EventSlotBinding[0],
                    },
                };

                stageCatalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_01",
                        Definition = definition,
                        Layout = null,
                    },
                };

                stageMapCatalog.Stages = new[]
                {
                    new StageMapDefinition
                    {
                        StageId = 1,
                        Sources = new[]
                        {
                            new StageSourceLayoutData
                            {
                                StableId = 1001u,
                                Active = true,
                                Position = new Vector3(1f, 0f, 2f),
                                YawDeg = 90f,
                                FieldShape = BulletFieldShapeId.Rectangle,
                                FieldRadius = 0f,
                                FieldSize = new Vector2(12f, 8f),
                            },
                            new StageSourceLayoutData
                            {
                                StableId = 1002u,
                                Active = true,
                                Position = new Vector3(3f, 0f, 4f),
                                YawDeg = 0f,
                                FieldShape = BulletFieldShapeId.Circle,
                                FieldRadius = 6f,
                                FieldSize = Vector2.zero,
                            },
                            new StageSourceLayoutData
                            {
                                StableId = 1003u,
                                Active = false,
                                Position = new Vector3(5f, 0f, 6f),
                                YawDeg = 0f,
                                FieldShape = BulletFieldShapeId.Circle,
                                FieldRadius = 3f,
                                FieldSize = Vector2.zero,
                            },
                            new StageSourceLayoutData
                            {
                                StableId = 1004u,
                                Active = true,
                                Position = new Vector3(7f, 0f, 8f),
                                YawDeg = 0f,
                                FieldShape = BulletFieldShapeId.Circle,
                                FieldRadius = 2f,
                                FieldSize = Vector2.zero,
                            },
                        },
                        Deposits = new[]
                        {
                            new StageDepositLayoutData
                            {
                                StableId = 2001u,
                                Active = true,
                                Position = new Vector3(0f, 0f, 10f),
                                Radius = 5f,
                            },
                        },
                    },
                };

                var stageMapRuntimeEntity = GetOrCreateManagedSingletonEntity<StageMapCatalogRuntimeComponent>(em);
                em.GetComponentObject<StageMapCatalogRuntimeComponent>(stageMapRuntimeEntity).Catalog = stageMapCatalog;

                var stageCatalogRuntimeEntity = GetOrCreateManagedSingletonEntity<StageCatalogRuntimeComponent>(em);
                em.GetComponentObject<StageCatalogRuntimeComponent>(stageCatalogRuntimeEntity).Catalog = stageCatalog;

                var requestEntity = GetOrCreateSingletonEntity<RunDirectorStageRequestComponent>(em);
                em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
                {
                    RequestedStageId = 1,
                    StageMapApplyRequested = 1,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                applySystem.Update(world.Unmanaged);

                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(request.StageMapApplyRequested, Is.EqualTo(0));
                Assert.That(request.RequestedStageId, Is.EqualTo(1));

                var matchedSource = em.GetComponentData<SourceSpawnComponent>(sourceMatched);
                var matchedRuntime = em.GetComponentData<SourceSpawnRuntimeComponent>(sourceMatched);
                var matchedAnchor = em.GetComponentData<SourceAnchorComponent>(sourceMatched);
                var matchedArea = em.GetComponentData<BulletFieldAreaComponent>(sourceMatched);
                var matchedTransform = em.GetComponentData<LocalTransform>(sourceMatched);
                var matchedDirector = em.GetComponentData<SourceRunDirectorStateComponent>(sourceMatched);
                var matchedEventRuntime = em.GetComponentData<SourceEventRuntimeComponent>(sourceMatched);
                var matchedGrid = em.GetComponentData<SourcePollutionGridComponent>(sourceMatched);
                Assert.That(matchedSource.ThresholdWeakened, Is.EqualTo(50));
                Assert.That(matchedSource.ThresholdDepleted, Is.EqualTo(90));
                Assert.That(matchedSource.CollectedCount, Is.EqualTo(50));
                Assert.That(matchedSource.State, Is.EqualTo(SourceStateId.Weakened));
                Assert.That(matchedRuntime.SpawnSequence, Is.EqualTo(1u));
                Assert.That(matchedAnchor.Position.x, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(matchedAnchor.Position.z, Is.EqualTo(2f).Within(1e-4f));
                Assert.That(matchedTransform.Position.x, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(matchedTransform.Position.z, Is.EqualTo(2f).Within(1e-4f));
                Assert.That(matchedArea.Shape, Is.EqualTo(BulletFieldShapeId.Rectangle));
                Assert.That(matchedArea.Size.x, Is.EqualTo(12f).Within(1e-4f));
                Assert.That(matchedArea.Size.y, Is.EqualTo(8f).Within(1e-4f));
                Assert.That(matchedDirector.State, Is.EqualTo(RunDirectorSourceStateId.Baseline));
                Assert.That(matchedDirector.SelectedClipState, Is.EqualTo(SourceStateId.Weakened));
                Assert.That(matchedDirector.PressureOccupancySec, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(matchedDirector.DensityScale, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(matchedDirector.Version, Is.EqualTo(8u));
                Assert.That(matchedEventRuntime.IsPlaying, Is.EqualTo(0));
                Assert.That(matchedEventRuntime.TriggerState, Is.EqualTo(SourceStateId.Weakened));
                Assert.That(matchedEventRuntime.SelectionSequence, Is.EqualTo(1u));
                Assert.That(matchedGrid.Cols, Is.EqualTo(12));
                Assert.That(matchedGrid.Rows, Is.EqualTo(8));
                Assert.That(em.GetBuffer<SourceClipPatternBuffer>(sourceMatched).Length, Is.GreaterThan(0));
                Assert.That(em.GetBuffer<SourceSustainSlotCandidateBuffer>(sourceMatched).Length, Is.EqualTo(1));
                Assert.That(em.GetBuffer<SourceEventQueueBuffer>(sourceMatched).Length, Is.EqualTo(0));
                Assert.That(em.GetBuffer<SourceSpawnRequestBuffer>(sourceMatched).Length, Is.EqualTo(0));
                Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(sourceMatched).Length, Is.EqualTo(1));
                Assert.That(em.GetBuffer<SourceDirectorPressureInputBuffer>(sourceMatched).Length, Is.EqualTo(2));
                Assert.That(em.GetBuffer<SourcePollutionValidCellIndexBuffer>(sourceMatched).Length, Is.GreaterThan(0));

                AssertDisabledSource(em, sourceMissingBinding, expectedVersion: 4u);
                AssertDisabledSource(em, sourceLayoutInactive, expectedVersion: 5u);
                AssertDisabledSource(em, sourceDuplicateA, expectedVersion: 6u);
                AssertDisabledSource(em, sourceDuplicateB, expectedVersion: 7u);
                AssertDisabledSource(em, sourceUnmapped, expectedVersion: 3u);

                var mappedDeposit = em.GetComponentData<DepositPointComponent>(depositMapped);
                var mappedDepositTx = em.GetComponentData<LocalTransform>(depositMapped);
                Assert.That(mappedDeposit.Radius, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(mappedDepositTx.Position.z, Is.EqualTo(10f).Within(1e-4f));

                var unmappedDeposit = em.GetComponentData<DepositPointComponent>(depositUnmapped);
                var unmappedDepositTx = em.GetComponentData<LocalTransform>(depositUnmapped);
                Assert.That(unmappedDeposit.Radius, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(unmappedDepositTx.Position.y, Is.LessThan(-9999f));
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageMapApply_MissingDefinitionStage_AppliesLayoutOnly_AndPreservesClipBuffers()
        {
            using var world = CreateDefaultTestWorld("StageMapApplyWorld_B", out _);
            var em = world.EntityManager;
            var applySystem = world.GetOrCreateSystem<StageMapApplyExecutionBeginSystem>();
            var createdAssets = new List<ScriptableObject>();

            var source = CreateSource(em, 1001u, new float3(7f, 0f, 8f), state: SourceStateId.Weakened, version: 2u);
            var stageMapCatalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
            createdAssets.Add(stageMapCatalog);

            try
            {
                stageMapCatalog.Stages = new[]
                {
                    new StageMapDefinition
                    {
                        StageId = 1,
                        Sources = new[]
                        {
                            new StageSourceLayoutData
                            {
                                StableId = 1001,
                                Active = true,
                                Position = new Vector3(1f, 0f, 1f),
                                YawDeg = 0f,
                                FieldShape = BulletFieldShapeId.Rectangle,
                                FieldRadius = 0f,
                                FieldSize = new Vector2(5f, 4f),
                            },
                        },
                    },
                };

                var runtimeEntity = GetOrCreateManagedSingletonEntity<StageMapCatalogRuntimeComponent>(em);
                em.GetComponentObject<StageMapCatalogRuntimeComponent>(runtimeEntity).Catalog = stageMapCatalog;

                var requestEntity = GetOrCreateSingletonEntity<RunDirectorStageRequestComponent>(em);
                em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
                {
                    RequestedStageId = 1,
                    StageMapApplyRequested = 1,
                });

                var beforeClipPatternCount = em.GetBuffer<SourceClipPatternBuffer>(source).Length;
                var beforeThresholdWeakened = em.GetComponentData<SourceSpawnComponent>(source).ThresholdWeakened;
                var beforeThresholdDepleted = em.GetComponentData<SourceSpawnComponent>(source).ThresholdDepleted;

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                applySystem.Update(world.Unmanaged);

                var appliedSource = em.GetComponentData<SourceSpawnComponent>(source);
                var appliedAnchor = em.GetComponentData<SourceAnchorComponent>(source);
                var appliedArea = em.GetComponentData<BulletFieldAreaComponent>(source);
                Assert.That(appliedSource.State, Is.EqualTo(SourceStateId.Normal));
                Assert.That(appliedSource.CollectedCount, Is.EqualTo(0));
                Assert.That(appliedSource.ThresholdWeakened, Is.EqualTo(beforeThresholdWeakened));
                Assert.That(appliedSource.ThresholdDepleted, Is.EqualTo(beforeThresholdDepleted));
                Assert.That(appliedAnchor.Position.x, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(appliedAnchor.Position.z, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(appliedArea.Shape, Is.EqualTo(BulletFieldShapeId.Rectangle));
                Assert.That(appliedArea.Size.x, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(appliedArea.Size.y, Is.EqualTo(4f).Within(1e-4f));
                Assert.That(em.GetBuffer<SourceClipPatternBuffer>(source).Length, Is.EqualTo(beforeClipPatternCount));
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        private static void AssertDisabledSource(EntityManager em, Entity entity, uint expectedVersion)
        {
            var source = em.GetComponentData<SourceSpawnComponent>(entity);
            var runtime = em.GetComponentData<SourceSpawnRuntimeComponent>(entity);
            var eventRuntime = em.GetComponentData<SourceEventRuntimeComponent>(entity);
            var director = em.GetComponentData<SourceRunDirectorStateComponent>(entity);

            Assert.That(source.State, Is.EqualTo(SourceStateId.Depleted));
            Assert.That(runtime.SpawnSequence, Is.EqualTo(1u));
            Assert.That(eventRuntime.IsPlaying, Is.EqualTo(0));
            Assert.That(eventRuntime.TriggerState, Is.EqualTo(SourceStateId.Depleted));
            Assert.That(director.State, Is.EqualTo(RunDirectorSourceStateId.Finish));
            Assert.That(director.SelectedClipState, Is.EqualTo(SourceStateId.Depleted));
            Assert.That(director.Version, Is.EqualTo(expectedVersion));
            Assert.That(em.GetBuffer<SourceClipPatternBuffer>(entity).Length, Is.EqualTo(0));
            Assert.That(em.GetBuffer<SourceSustainSlotCandidateBuffer>(entity).Length, Is.EqualTo(0));
            Assert.That(em.GetBuffer<SourceSustainRuntimeLaneBuffer>(entity).Length, Is.EqualTo(0));
            Assert.That(em.GetBuffer<SourceEventQueueBuffer>(entity).Length, Is.EqualTo(0));
            Assert.That(em.GetBuffer<SourceSpawnRequestBuffer>(entity).Length, Is.EqualTo(0));
            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(entity).Length, Is.EqualTo(0));
        }

        private static World CreateDefaultTestWorld(string worldName, out SimulationSystemGroup simGroup)
        {
            var world = new World(worldName);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");
            return world;
        }

        private static Entity GetOrCreateSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
                return em.CreateEntity(typeof(T));
            if (query.CalculateEntityCount() == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities[0];
        }

        private static Entity GetOrCreateManagedSingletonEntity<T>(EntityManager em)
            where T : class
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = em.CreateEntity();
                em.AddComponentObject(entity, (T)System.Activator.CreateInstance(typeof(T)));
                return entity;
            }

            if (query.CalculateEntityCount() == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities[0];
        }

        private static Entity CreateSource(EntityManager em, uint stableId, float3 position, SourceStateId state, uint version)
        {
            var entity = em.CreateEntity(
                typeof(SourceStableIdComponent),
                typeof(SourceSpawnComponent),
                typeof(SourceSpawnRuntimeComponent),
                typeof(SourceAnchorComponent),
                typeof(BulletFieldAreaComponent),
                typeof(SourcePollutionConfigComponent),
                typeof(SourcePollutionGridComponent),
                typeof(SourceSustainRuntimeComponent),
                typeof(SourceEventRuntimeComponent),
                typeof(SourceRunDirectorStateComponent),
                typeof(LocalTransform));

            em.SetComponentData(entity, new SourceStableIdComponent { Value = stableId });
            em.SetComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 10,
                ThresholdDepleted = 20,
                CollectedCount = 42,
                State = state,
            });
            em.SetComponentData(entity, new SourceSpawnRuntimeComponent
            {
                SpawnSequence = 33u,
            });
            em.SetComponentData(entity, new SourceAnchorComponent
            {
                Position = position,
            });
            em.SetComponentData(entity, new BulletFieldAreaComponent
            {
                Shape = BulletFieldShapeId.Circle,
                Radius = 2f,
                Size = new float2(2f, 2f),
                ComputedArea = 0f,
            });
            em.SetComponentData(entity, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0.1f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 4,
            });
            em.SetComponentData(entity, new SourcePollutionGridComponent
            {
                CellSize = 1f,
                InvCellSize = 1f,
                HalfExtents = new float2(2f, 2f),
                Cols = 4,
                Rows = 4,
            });
            em.SetComponentData(entity, new SourceSustainRuntimeComponent
            {
                ActiveState = state,
            });
            em.SetComponentData(entity, new SourceEventRuntimeComponent
            {
                IsPlaying = 1,
                ActiveEventClipId = 99,
                TriggerState = state,
                ElapsedSec = 2f,
                SelectionSequence = 8u,
            });
            em.SetComponentData(entity, new SourceRunDirectorStateComponent
            {
                State = RunDirectorSourceStateId.Pressure,
                SelectedClipState = state,
                PressureOccupancySec = 1.5f,
                DensityScale = 2f,
                Version = version,
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));

            var requests = em.AddBuffer<SourceSpawnRequestBuffer>(entity);
            requests.Add(new SourceSpawnRequestBuffer
            {
                Count = 3,
                OldestFrame = 4u,
            });

            var clipPatterns = em.AddBuffer<SourceClipPatternBuffer>(entity);
            clipPatterns.Add(new SourceClipPatternBuffer
            {
                DirectiveId = 77,
                ClipId = 88,
                Phase = SourceWavePhaseId.Sustain,
                Lane = SourceSpawnLaneId.Hazard,
                TriggerState = state,
                BulletTypeKey = 999,
            });

            var sustainCandidates = em.AddBuffer<SourceSustainSlotCandidateBuffer>(entity);
            sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
            {
                State = state,
                Lane = SourceSpawnLaneId.Hazard,
                ClipId = 88,
                Weight = 1f,
            });

            var sustainRuntimeLanes = em.AddBuffer<SourceSustainRuntimeLaneBuffer>(entity);
            sustainRuntimeLanes.Add(new SourceSustainRuntimeLaneBuffer
            {
                Lane = SourceSpawnLaneId.Hazard,
                ActiveClipId = 88,
                ElapsedSec = 1f,
                LastClipId = 77,
                SelectionSequence = 4u,
                LastMissingLogFrame = 5u,
            });

            var eventQueue = em.AddBuffer<SourceEventQueueBuffer>(entity);
            eventQueue.Add(new SourceEventQueueBuffer
            {
                TriggerState = state,
                QueuedFrame = 2u,
            });

            var activeCounts = em.AddBuffer<SourceActiveBulletCountBuffer>(entity);
            activeCounts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = 999,
                ActiveCount = 5,
            });

            var pressureInputs = em.AddBuffer<SourceDirectorPressureInputBuffer>(entity);
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceOccupancy,
                Value = 1f,
            });
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceHoldSec,
                Value = 0.5f,
            });

            em.AddBuffer<SourcePollutionCellBuffer>(entity);
            em.AddBuffer<SourcePollutionDropRequestBuffer>(entity);
            em.AddBuffer<SourcePollutionValidCellIndexBuffer>(entity);
            InitializePollutionGrid(em, entity);
            var pollutionDrops = em.GetBuffer<SourcePollutionDropRequestBuffer>(entity);
            pollutionDrops.Add(new SourcePollutionDropRequestBuffer
            {
                CellIndex = 0,
                Count = 1,
            });

            return entity;
        }

        private static void InitializePollutionGrid(EntityManager em, Entity entity)
        {
            var area = em.GetComponentData<BulletFieldAreaComponent>(entity);
            var config = em.GetComponentData<SourcePollutionConfigComponent>(entity);
            var grid = em.GetComponentData<SourcePollutionGridComponent>(entity);

            float cellSize = math.max(0.1f, grid.CellSize);
            float2 halfExtents = SourceRuntimeApplyUtility.ComputeHalfExtents(area.Shape, area.Radius, area.Size);
            int cols = math.max(1, Mathf.CeilToInt((halfExtents.x * 2f) / cellSize));
            int rows = math.max(1, Mathf.CeilToInt((halfExtents.y * 2f) / cellSize));
            int cellCount = math.max(1, cols * rows);

            grid.Cols = cols;
            grid.Rows = rows;
            grid.CellSize = cellSize;
            grid.InvCellSize = 1f / cellSize;
            grid.HalfExtents = halfExtents;
            em.SetComponentData(entity, grid);

            float safeCellSize = math.max(0.001f, cellSize);
            float safeRadius = math.max(0f, area.Radius);
            float radiusSq = safeRadius * safeRadius;
            float maxValue = math.max(config.MinValue, config.MaxValue);

            var cellsData = new List<SourcePollutionCellBuffer>(cellCount);
            var validIndicesData = new List<int>(cellCount);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int index = y * cols + x;
                    float centerX = -halfExtents.x + (x + 0.5f) * safeCellSize;
                    float centerZ = -halfExtents.y + (y + 0.5f) * safeCellSize;
                    bool isValid = area.Shape == BulletFieldShapeId.Rectangle
                        || (centerX * centerX + centerZ * centerZ) <= radiusSq;

                    cellsData.Add(new SourcePollutionCellBuffer
                    {
                        Value = maxValue,
                        IsValid = isValid ? (byte)1 : (byte)0,
                    });

                    if (isValid)
                        validIndicesData.Add(index);
                }
            }

            if (validIndicesData.Count <= 0)
            {
                int centerIndex = math.clamp((rows / 2) * cols + (cols / 2), 0, cellCount - 1);
                var cell = cellsData[centerIndex];
                cell.IsValid = 1;
                cellsData[centerIndex] = cell;
                validIndicesData.Add(centerIndex);
            }

            var pollutionDrops = em.GetBuffer<SourcePollutionDropRequestBuffer>(entity);
            pollutionDrops.Clear();

            var pollutionCells = em.GetBuffer<SourcePollutionCellBuffer>(entity);
            pollutionCells.Clear();
            if (pollutionCells.Capacity < cellsData.Count)
                pollutionCells.Capacity = cellsData.Count;
            for (int i = 0; i < cellsData.Count; i++)
                pollutionCells.Add(cellsData[i]);

            var pollutionValidIndices = em.GetBuffer<SourcePollutionValidCellIndexBuffer>(entity);
            pollutionValidIndices.Clear();
            if (pollutionValidIndices.Capacity < validIndicesData.Count)
                pollutionValidIndices.Capacity = validIndicesData.Count;
            for (int i = 0; i < validIndicesData.Count; i++)
            {
                pollutionValidIndices.Add(new SourcePollutionValidCellIndexBuffer
                {
                    Value = validIndicesData[i],
                });
            }
        }

        private static Entity CreateDeposit(EntityManager em, uint stableId, float3 position, float radius)
        {
            var entity = em.CreateEntity(
                typeof(DepositStableIdComponent),
                typeof(DepositPointComponent),
                typeof(LocalTransform));
            em.SetComponentData(entity, new DepositStableIdComponent
            {
                Value = stableId,
            });
            em.SetComponentData(entity, new DepositPointComponent
            {
                Radius = radius,
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private static StageDefinitionSO CreateDefinition(List<ScriptableObject> created, int stageId)
        {
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = stageId;
            definition.DisplayName = $"Stage {stageId}";
            definition.StageTimeLimitSec = 90f;
            created.Add(definition);
            return definition;
        }

        private static BulletDefinitionSO CreateBulletDefinition(List<ScriptableObject> created, int definitionId)
        {
            var definition = ScriptableObject.CreateInstance<BulletDefinitionSO>();
#if UNITY_EDITOR
            definition.Editor_SetDefinitionId(definitionId);
#endif
            created.Add(definition);
            return definition;
        }

        private static WaveClipSO CreateSustainClip(List<ScriptableObject> created, BulletDefinitionSO bullet, int clipId)
        {
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            clip.ClipId = clipId;
            clip.Phase = SourceWavePhaseId.Sustain;
            clip.Lane = SourceSpawnLaneId.Hazard;
            clip.DurationSec = 1f;
            clip.Segments = new[]
            {
                new WaveClipSO.ClipSegment
                {
                    StartSec = 0f,
                    EndSec = 1f,
                    Entries = new[]
                    {
                        new WaveClipSO.SpawnEntry
                        {
                            Payload = new WaveClipSO.SpawnPayloadProfile
                            {
                                Bullet = bullet,
                            },
                            Emission = new WaveClipSO.SpawnEmissionProfile
                            {
                                EmissionMode = SourceSpawnEmissionModeId.RateField,
                                SpawnMode = SourceSpawnModeId.FixedDensity,
                                RatePerSecPerArea = 60f,
                                BurstShotsPerEvent = 1,
                            },
                            Sampling = new WaveClipSO.SpawnSamplingProfile
                            {
                                SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                SpawnSampleBudget = 16,
                            },
                            Direction = new WaveClipSO.SpawnDirectionProfile
                            {
                                DirectionMode = SourceSpawnDirectionModeId.Fixed,
                                BaseAngleDeg = 0f,
                                NWayCount = 1,
                            },
                        },
                    },
                },
            };
            created.Add(clip);
            return clip;
        }

        private static WaveClipSO CreateEventClip(List<ScriptableObject> created, BulletDefinitionSO bullet, int clipId)
        {
            var clip = CreateSustainClip(created, bullet, clipId);
            clip.Phase = SourceWavePhaseId.OnStateEnterOnce;
            return clip;
        }

        private static void DestroyAll(List<ScriptableObject> created)
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }
    }
}
