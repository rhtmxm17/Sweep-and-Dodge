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
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });

            var stageStateEntity = GetOrCreateSingletonEntity<RunDirectorStageStateComponent>(em);
            em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
                StateElapsedSec = 0f,
            });

            var source = CreateSourceWithPattern(em);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            buildSystem.Update(world.Unmanaged);
            Assert.That(em.GetBuffer<SourceSpawnRequestBuffer>(source).Length, Is.EqualTo(0));

            em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
                StateElapsedSec = 0f,
            });
            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            buildSystem.Update(world.Unmanaged);

            var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
            Assert.That(requests.Length, Is.GreaterThan(0));
            Assert.That(requests[0].Count, Is.GreaterThan(0));
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

        private static Entity CreateSourceWithPattern(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(SourceSpawnComponent),
                typeof(SourceRunDirectorStateComponent),
                typeof(BulletFieldAreaComponent),
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
            em.SetComponentData(entity, new BulletFieldAreaComponent
            {
                Shape = BulletFieldShapeId.Circle,
                Radius = 1f,
                Size = new float2(2f, 2f),
                ComputedArea = math.PI,
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
                Phase = SourceWavePhaseId.Sustain,
                Lane = SourceSpawnLaneId.Hazard,
                TriggerState = SourceStateId.Normal,
                LocalStartSec = 0f,
                LocalEndSec = 10f,
                BulletTypeKey = 101,
                EmissionMode = SourceSpawnEmissionModeId.RateField,
                SpawnMode = SourceSpawnModeId.FixedDensity,
                SamplingMode = SourceSpawnSamplingModeId.UniformField,
                CenterMode = SourceSpawnCenterModeId.SourceCenter,
                DirectionMode = SourceSpawnDirectionModeId.Fixed,
                SampleSpacing = 1f,
                SpawnSampleBudget = 16,
                SpawnDensityPerSecPerArea = 60f,
                BurstShotsPerEvent = 1,
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
    }
}
