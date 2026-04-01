using NUnit.Framework;
using Unity.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class SourceClipRequestBuildSystemTests
    {
        [Test]
        public void SourceClipRequestBuild_GatesOnRunningStageState()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_A", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Idle, deltaTime: 1f / 60f);
            var source = CreateSourceWithPattern(em);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            buildSystem.Update(world.Unmanaged);
            Assert.That(em.GetBuffer<SourceSpawnRequestBuffer>(source).Length, Is.EqualTo(0));

            SetStageState(em, RunDirectorStageStateId.Running);
            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            buildSystem.Update(world.Unmanaged);

            var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
            Assert.That(requests.Length, Is.GreaterThan(0));
            Assert.That(requests[0].Count, Is.GreaterThan(0));
        }

        [Test]
        public void SourceClipRequestBuild_UniformFieldRateField_ScalesByActiveAreaRatio()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_UniformScale", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 1f);
            var source = CreateSourceWithPattern(
                em,
                samplingMode: SourceSpawnSamplingModeId.UniformField,
                emissionMode: SourceSpawnEmissionModeId.RateField,
                spawnMode: SourceSpawnModeId.FixedDensity,
                shapeSize: new float2(2f, 2f),
                spawnDensityPerSecPerArea: 4f);
            AttachPollutionCells(em, source, 2, 2, activeValidCount: 1);

            world.SetTime(new TimeData(1d, 1f));
            buildSystem.Update(world.Unmanaged);

            AssertRequestCount(em, source, 4);
        }

        [Test]
        public void SourceClipRequestBuild_PollutionTopKRateField_ScalesByActiveAreaRatio()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_PollutionTopKScale", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 1f);
            var source = CreateSourceWithPattern(
                em,
                samplingMode: SourceSpawnSamplingModeId.PollutionTopK,
                emissionMode: SourceSpawnEmissionModeId.RateField,
                spawnMode: SourceSpawnModeId.FixedDensity,
                shapeSize: new float2(2f, 2f),
                spawnDensityPerSecPerArea: 4f);
            AttachPollutionCells(em, source, 2, 2, activeValidCount: 1);

            world.SetTime(new TimeData(1d, 1f));
            buildSystem.Update(world.Unmanaged);

            AssertRequestCount(em, source, 4);
        }

        [Test]
        public void SourceClipRequestBuild_LineEvenRateField_IgnoresActiveAreaScaling()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_LineEvenFullArea", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 1f);
            var source = CreateSourceWithPattern(
                em,
                samplingMode: SourceSpawnSamplingModeId.LineEven,
                emissionMode: SourceSpawnEmissionModeId.RateField,
                spawnMode: SourceSpawnModeId.FixedDensity,
                shapeSize: new float2(2f, 2f),
                spawnDensityPerSecPerArea: 4f);
            AttachPollutionCells(em, source, 2, 2, activeValidCount: 1);

            world.SetTime(new TimeData(1d, 1f));
            buildSystem.Update(world.Unmanaged);

            AssertRequestCount(em, source, 16);
        }

        [Test]
        public void SourceClipRequestBuild_FieldSamplingWithoutPollution_UsesFullArea()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_NoPollutionFallback", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 1f);
            var source = CreateSourceWithPattern(
                em,
                samplingMode: SourceSpawnSamplingModeId.UniformField,
                emissionMode: SourceSpawnEmissionModeId.RateField,
                spawnMode: SourceSpawnModeId.FixedDensity,
                shapeSize: new float2(2f, 2f),
                spawnDensityPerSecPerArea: 4f);

            world.SetTime(new TimeData(1d, 1f));
            buildSystem.Update(world.Unmanaged);

            AssertRequestCount(em, source, 16);
        }

        [Test]
        public void SourceClipRequestBuild_FieldSamplingCap_UsesEffectiveAreaForEventBurst()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_CapScale", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 4f);
            var source = CreateSourceWithPattern(
                em,
                samplingMode: SourceSpawnSamplingModeId.UniformField,
                emissionMode: SourceSpawnEmissionModeId.EventBurst,
                spawnMode: SourceSpawnModeId.CapAndMaxDensity,
                shapeSize: new float2(2f, 2f),
                spawnDensityPerSecPerArea: 0f,
                burstIntervalSec: 1f,
                burstShotsPerEvent: 1,
                burstRepeatCount: -1,
                maxActiveDensityPerArea: 2f);
            AttachPollutionCells(em, source, 2, 2, activeValidCount: 1);

            world.SetTime(new TimeData(4d, 4f));
            buildSystem.Update(world.Unmanaged);

            AssertRequestCount(em, source, 2);
        }

        [Test]
        public void SourceClipRequestBuild_EventBurst_FiresImmediatelyWhenSegmentStarts()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_EventBurstStartAligned", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 1f);
            var source = CreateSourceWithPattern(
                em,
                emissionMode: SourceSpawnEmissionModeId.EventBurst,
                spawnMode: SourceSpawnModeId.FixedDensity,
                burstIntervalSec: 2f,
                burstShotsPerEvent: 1,
                burstRepeatCount: 3,
                localStartSec: 1f,
                localEndSec: 10f,
                clipDurationSec: 10f);

            world.SetTime(new TimeData(1d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 0);

            world.SetTime(new TimeData(2d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 1);
            em.GetBuffer<SourceSpawnRequestBuffer>(source).Clear();

            world.SetTime(new TimeData(3d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 0);

            world.SetTime(new TimeData(4d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 1);
            em.GetBuffer<SourceSpawnRequestBuffer>(source).Clear();

            world.SetTime(new TimeData(5d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 0);

            world.SetTime(new TimeData(6d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 1);
        }

        [Test]
        public void SourceClipRequestBuild_SustainLoop_UsesClipDurationSecInsteadOfLastSegmentEnd()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_SustainClipDuration", out _);
            var em = world.EntityManager;
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 1f);
            var source = CreateSourceWithPattern(
                em,
                emissionMode: SourceSpawnEmissionModeId.RateField,
                spawnMode: SourceSpawnModeId.FixedDensity,
                spawnDensityPerSecPerArea: 1f,
                localStartSec: 0f,
                localEndSec: 1f,
                clipDurationSec: 5f);

            world.SetTime(new TimeData(1d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 1);
            em.GetBuffer<SourceSpawnRequestBuffer>(source).Clear();

            for (int step = 2; step <= 5; step++)
            {
                world.SetTime(new TimeData(step, 1f));
                buildSystem.Update(world.Unmanaged);
                AssertPendingCount(em, source, 0);
            }

            world.SetTime(new TimeData(6d, 1f));
            buildSystem.Update(world.Unmanaged);
            AssertPendingCount(em, source, 1);
        }

        [Test]
        public void SourceClipRequestBuild_ReadsPollutionStateUpdatedEarlierInSameRequestFrame()
        {
            using var world = CreateDefaultTestWorld("SourceClipRequestBuildWorld_RequestOrder", out _);
            var em = world.EntityManager;
            var pollutionSystem = world.GetOrCreateSystem<SourcePollutionUpdateSystem>();
            var buildSystem = world.GetOrCreateSystem<SourceClipRequestBuildSystem>();

            InitializeBuildWorld(em, stageState: RunDirectorStageStateId.Running, deltaTime: 1f);
            var source = CreateSourceWithPattern(
                em,
                samplingMode: SourceSpawnSamplingModeId.UniformField,
                emissionMode: SourceSpawnEmissionModeId.RateField,
                spawnMode: SourceSpawnModeId.FixedDensity,
                shapeSize: new float2(2f, 2f),
                spawnDensityPerSecPerArea: 4f);
            AttachPollutionRuntime(em, source, 2, 2, activeValidCount: 4, initialValue: 1f, activeRatioThreshold: 0f);

            var drops = em.GetBuffer<SourcePollutionDropRequestBuffer>(source);
            drops.Add(new SourcePollutionDropRequestBuffer { CellIndex = 1, Count = 1 });
            drops.Add(new SourcePollutionDropRequestBuffer { CellIndex = 2, Count = 1 });
            drops.Add(new SourcePollutionDropRequestBuffer { CellIndex = 3, Count = 1 });

            world.SetTime(new TimeData(1d, 1f));
            pollutionSystem.Update(world.Unmanaged);
            buildSystem.Update(world.Unmanaged);

            AssertRequestCount(em, source, 4);
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

        private static void InitializeBuildWorld(
            EntityManager em,
            RunDirectorStageStateId stageState,
            float deltaTime)
        {
            var frameEntity = GetOrCreateSingletonEntity<BulletFrameCounterComponent>(em);
            em.SetComponentData(frameEntity, new BulletFrameCounterComponent
            {
                Value = 1u,
            });

            var policyEntity = GetOrCreateSingletonEntity<SpawnRequestPolicyComponent>(em);
            em.SetComponentData(policyEntity, new SpawnRequestPolicyComponent
            {
                BudgetPerFrame = 1024,
                MaxPendingCount = 4096,
                MaxPendingAgeFrames = 120,
            });

            var metricsEntity = GetOrCreateSingletonEntity<SpawnBacklogMetricsComponent>(em);
            em.SetComponentData(metricsEntity, default(SpawnBacklogMetricsComponent));

            var seedEntity = GetOrCreateSingletonEntity<SpawnRunSeedComponent>(em);
            em.SetComponentData(seedEntity, new SpawnRunSeedComponent
            {
                Value = 1u,
            });

            var tickEntity = GetOrCreateSingletonEntity<FixedTickStepRuntimeComponent>(em);
            em.SetComponentData(tickEntity, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = deltaTime,
                LogicDeltaTime = deltaTime,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });

            SetStageState(em, stageState);
        }

        private static void SetStageState(EntityManager em, RunDirectorStageStateId state)
        {
            var stageStateEntity = GetOrCreateSingletonEntity<RunDirectorStageStateComponent>(em);
            em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
            {
                State = state,
                StateElapsedSec = 0f,
            });
        }

        private static Entity GetOrCreateSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
                return em.CreateEntity(typeof(T));
            if (query.CalculateEntityCount() == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities[0];
        }

        private static Entity CreateSourceWithPattern(
            EntityManager em,
            SourceSpawnSamplingModeId samplingMode = SourceSpawnSamplingModeId.UniformField,
            SourceSpawnEmissionModeId emissionMode = SourceSpawnEmissionModeId.RateField,
            SourceSpawnModeId spawnMode = SourceSpawnModeId.FixedDensity,
            float2? shapeSize = null,
            float spawnDensityPerSecPerArea = 60f,
            float meanEventsPerSec = 0f,
            float burstIntervalSec = 1f,
            int burstShotsPerEvent = 1,
            int burstRepeatCount = 0,
            float maxActiveDensityPerArea = 0f,
            float localStartSec = 0f,
            float localEndSec = 10f,
            float clipDurationSec = 10f)
        {
            var entity = em.CreateEntity(
                typeof(SourceSpawnComponent),
                typeof(SourceRunDirectorStateComponent),
                typeof(BulletFieldAreaComponent),
                typeof(Shape2DComponent),
                typeof(SourceShapeDerivedComponent),
                typeof(SourceStableIdComponent),
                typeof(SourceSustainRuntimeComponent),
                typeof(SourceEventRuntimeComponent));

            em.SetComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 10,
                ThresholdDepleted = 20,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            em.SetComponentData(entity, new SourceRunDirectorStateComponent
            {
                State = RunDirectorSourceStateId.Baseline,
                SelectedClipState = SourceStateId.Normal,
                PressureOccupancySec = 0f,
                DensityScale = 1f,
                Version = 1u,
            });

            float2 resolvedSize = shapeSize ?? new float2(1f, 1f);
            var shape = new Shape2DComponent
            {
                Kind = Shape2DKind.Rectangle,
                Radius = 0f,
                Size = resolvedSize,
            };
            em.SetComponentData(entity, shape);
            em.SetComponentData(entity, new SourceShapeDerivedComponent
            {
                ComputedArea = Shape2DUtility.ComputeArea(in shape),
                HalfExtents = Shape2DUtility.ComputeHalfExtents(in shape),
            });
            em.SetComponentData(entity, new SourceStableIdComponent
            {
                Value = 1001u,
            });
            em.SetComponentData(entity, new SourceSustainRuntimeComponent
            {
                ActiveState = SourceStateId.Normal,
            });
            em.SetComponentData(entity, new SourceEventRuntimeComponent
            {
                IsPlaying = 0,
                ActiveEventClipId = 0,
                TriggerState = SourceStateId.Normal,
                ElapsedSec = 0f,
                SelectionSequence = 1u,
            });

            var clipPatterns = em.AddBuffer<SourceClipPatternBuffer>(entity);
            clipPatterns.Add(new SourceClipPatternBuffer
            {
                DirectiveId = 1,
                ClipId = 10,
                ClipDurationSec = clipDurationSec,
                Phase = SourceWavePhaseId.Sustain,
                Lane = SourceSpawnLaneId.Hazard,
                TriggerState = SourceStateId.Normal,
                LocalStartSec = localStartSec,
                LocalEndSec = localEndSec,
                BulletTypeKey = 101,
                EmissionMode = emissionMode,
                SpawnMode = spawnMode,
                SamplingMode = samplingMode,
                CenterMode = SourceSpawnCenterModeId.SourceCenter,
                DirectionMode = SourceSpawnDirectionModeId.Fixed,
                SampleSpacing = 1f,
                LineStart = new float2(-0.5f, 0f),
                LineEnd = new float2(0.5f, 0f),
                SpawnSampleBudget = 16,
                SpawnDensityPerSecPerArea = spawnDensityPerSecPerArea,
                MeanEventsPerSec = meanEventsPerSec,
                BurstIntervalSec = burstIntervalSec,
                BurstShotsPerEvent = burstShotsPerEvent,
                BurstRepeatCount = burstRepeatCount,
                MaxActiveDensityPerArea = maxActiveDensityPerArea,
                LanePriority = 1,
                SpawnAccumulator = 0f,
            });

            var sustainCandidates = em.AddBuffer<SourceSustainSlotCandidateBuffer>(entity);
            sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
            {
                State = SourceStateId.Normal,
                Lane = SourceSpawnLaneId.Hazard,
                ClipId = 10,
                Weight = 1f,
            });

            var sustainLanes = em.AddBuffer<SourceSustainRuntimeLaneBuffer>(entity);
            sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
            {
                Lane = SourceSpawnLaneId.Hazard,
                ActiveClipId = 0,
                ElapsedSec = 0f,
                LastClipId = 0,
                SelectionSequence = 1u,
                LastMissingLogFrame = 0u,
            });

            em.AddBuffer<SourceEventQueueBuffer>(entity);

            var activeCounts = em.AddBuffer<SourceActiveBulletCountBuffer>(entity);
            activeCounts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = 101,
                ActiveCount = 0,
            });

            em.AddBuffer<SourceSpawnRequestBuffer>(entity);
            return entity;
        }

        private static void AttachPollutionCells(
            EntityManager em,
            Entity source,
            int cols,
            int rows,
            int activeValidCount,
            float activeValue = 1f,
            float inactiveValue = 0f)
        {
            if (!em.HasBuffer<SourcePollutionCellBuffer>(source))
                em.AddBuffer<SourcePollutionCellBuffer>(source);

            var cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            cells.Clear();

            int total = math.max(1, cols * rows);
            int clampedActiveCount = math.clamp(activeValidCount, 0, total);
            for (int i = 0; i < total; i++)
            {
                bool isActive = i < clampedActiveCount;
                cells.Add(new SourcePollutionCellBuffer
                {
                    Value = isActive ? activeValue : inactiveValue,
                    IsValid = 1,
                    IsActive = isActive ? (byte)1 : (byte)0,
                    LastDropFrame = 0u,
                    CooldownUntilFrame = 0u,
                });
            }
        }

        private static void AttachPollutionRuntime(
            EntityManager em,
            Entity source,
            int cols,
            int rows,
            int activeValidCount,
            float initialValue,
            float activeRatioThreshold)
        {
            if (!em.HasComponent<SourcePollutionConfigComponent>(source))
            {
                em.AddComponentData(source, new SourcePollutionConfigComponent
                {
                    MinValue = 0f,
                    MaxValue = 1f,
                    RegenPerSec = 0f,
                    DropPerCollect = 1f,
                    TopKSampleCount = 1,
                    ActiveRatioThreshold = activeRatioThreshold,
                    RecoveryCooldownFrames = 10u,
                    RecoveryWaveSeedCount = 1,
                    RecoveryWaveClusterSize = 1,
                    RecoveryWaveRestoreValue = 0.5f,
                    RecoveryRecentCleanBiasFrames = 10u,
                });
            }
            else
            {
                em.SetComponentData(source, new SourcePollutionConfigComponent
                {
                    MinValue = 0f,
                    MaxValue = 1f,
                    RegenPerSec = 0f,
                    DropPerCollect = 1f,
                    TopKSampleCount = 1,
                    ActiveRatioThreshold = activeRatioThreshold,
                    RecoveryCooldownFrames = 10u,
                    RecoveryWaveSeedCount = 1,
                    RecoveryWaveClusterSize = 1,
                    RecoveryWaveRestoreValue = 0.5f,
                    RecoveryRecentCleanBiasFrames = 10u,
                });
            }

            if (!em.HasComponent<SourcePollutionGridComponent>(source))
            {
                em.AddComponentData(source, new SourcePollutionGridComponent
                {
                    Cols = cols,
                    Rows = rows,
                    CellSize = 1f,
                    InvCellSize = 1f,
                });
            }
            else
            {
                em.SetComponentData(source, new SourcePollutionGridComponent
                {
                    Cols = cols,
                    Rows = rows,
                    CellSize = 1f,
                    InvCellSize = 1f,
                });
            }

            if (!em.HasBuffer<SourcePollutionDropRequestBuffer>(source))
                em.AddBuffer<SourcePollutionDropRequestBuffer>(source);

            AttachPollutionCells(em, source, cols, rows, activeValidCount, activeValue: initialValue, inactiveValue: 0f);
        }

        private static void AssertRequestCount(EntityManager em, Entity source, int expectedCount)
        {
            var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
            Assert.That(requests.Length, Is.GreaterThan(0));
            Assert.That(requests[0].Count, Is.EqualTo(expectedCount));
        }

        private static void AssertPendingCount(EntityManager em, Entity source, int expectedCount)
        {
            var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
            int pendingCount = 0;
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].Count <= 0)
                    continue;

                pendingCount += requests[i].Count;
            }

            Assert.That(pendingCount, Is.EqualTo(expectedCount));
        }
    }
}
