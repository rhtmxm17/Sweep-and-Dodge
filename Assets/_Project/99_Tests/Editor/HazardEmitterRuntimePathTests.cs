using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class HazardEmitterRuntimePathTests
    {
        [SetUp]
        public void SetUp() => ForceDisposeSharedContainersIfNeeded();

        [TearDown]
        public void TearDown() => ForceDisposeSharedContainersIfNeeded();

        [Test]
        public void HazardEmitterAuthoringValidation_RequiresParentSourceAuthoring()
        {
            var root = new GameObject("hazard_emitter_root");
            var emitterGo = new GameObject("hazard_emitter");
            emitterGo.transform.SetParent(root.transform);
            var authoring = emitterGo.AddComponent<HazardEmitterAuthoring>();
            authoring.TelegraphProfile = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            authoring.EmissionProfile = CreateEmissionProfile();

            try
            {
                bool ok = HazardEmitterAuthoringValidationUtility.TryValidate(authoring, out var sourceAuthoring, out var error);
                Assert.That(ok, Is.False);
                Assert.That(sourceAuthoring, Is.Null);
                Assert.That(error, Does.Contain("parent SourceRuntimeTemplateAuthoringBase"));
            }
            finally
            {
                Object.DestroyImmediate(authoring.TelegraphProfile);
                Object.DestroyImmediate(authoring.EmissionProfile.Bullet);
                Object.DestroyImmediate(authoring.EmissionProfile);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardEmitterAuthoringValidation_RejectsUnsupportedPolicy()
        {
            var root = new GameObject("source_root");
            var sourceAuthoring = root.AddComponent<SourceRuntimeTemplateAuthoring>();
            var emitterGo = new GameObject("hazard_emitter");
            emitterGo.transform.SetParent(root.transform);
            var authoring = emitterGo.AddComponent<HazardEmitterAuthoring>();
            authoring.ActivationPolicy = HazardEmitterActivationPolicyId.ProgressReactive;
            authoring.TelegraphProfile = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            authoring.EmissionProfile = CreateEmissionProfile();

            try
            {
                bool ok = HazardEmitterAuthoringValidationUtility.TryValidate(authoring, out var owner, out var error);
                Assert.That(ok, Is.False);
                Assert.That(owner, Is.EqualTo(sourceAuthoring));
                Assert.That(error, Does.Contain(nameof(HazardEmitterActivationPolicyId.AlwaysCycle)));
            }
            finally
            {
                Object.DestroyImmediate(authoring.TelegraphProfile);
                Object.DestroyImmediate(authoring.EmissionProfile.Bullet);
                Object.DestroyImmediate(authoring.EmissionProfile);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardEmitterEmitBuild_AlwaysCycle_ZeroTelegraph_AppendsHazardEmitterDiscreteRequest()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_ZeroTelegraph", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 701);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 701,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: new float3(3f, 0f, 4f),
                localOffset: new float3(1f, 0f, -2f));

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            var requests = GetDiscreteEmitRequests(em);
            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].ProducerKind, Is.EqualTo(DiscreteEmitProducerKind.HazardEmitter));
            Assert.That(requests[0].SourceEntity, Is.EqualTo(source));
            Assert.That(requests[0].ProducerEntity, Is.EqualTo(emitter));
            Assert.That(requests[0].AnchorMode, Is.EqualTo(DiscreteEmitAnchorMode.FixedWorld));
            Assert.That(requests[0].AnchorPosition.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(requests[0].AnchorPosition.z, Is.EqualTo(2f).Within(0.0001f));

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));
        }

        [Test]
        public void HazardEmitterEmitBuild_CooldownBlocksAdditionalAppends_AndDisabledStaysDormant()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Cooldown", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.5f);

            var source = CreateSourceWithActiveCountBuffer(em, 702);
            var activeEmitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 702,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var disabledEmitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 702,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: false,
                isSuppressed: false,
                position: new float3(1f, 0f, 1f),
                localOffset: float3.zero);

            var system = world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>();

            world.SetTime(new TimeData(0.5d, 0.5f));
            system.Update(world.Unmanaged);
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(1));

            CreateFrameCounter(em, 2u);
            world.SetTime(new TimeData(1d, 0.5f));
            system.Update(world.Unmanaged);
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(1));

            CreateFrameCounter(em, 3u);
            world.SetTime(new TimeData(1.5d, 0.5f));
            system.Update(world.Unmanaged);
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(2));

            var disabledState = em.GetComponentData<HazardEmitterRuntimeStateComponent>(disabledEmitter);
            Assert.That(disabledState.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));

            var activeState = em.GetComponentData<HazardEmitterRuntimeStateComponent>(activeEmitter);
            Assert.That(activeState.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));
        }

        [Test]
        public void HazardEmitterDiscreteEmit_ConsumesThroughExecutionSystem()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Consume", out _);
            var em = world.EntityManager;

            InitializeSharedContainers();
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);
            var channel = GetDiscreteChannel(em, budgetPerFrame: 8, maxPendingCount: 16, maxPendingAgeFrames: 120u);
            var source = CreateSourceWithActiveCountBuffer(em, 703);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 703,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var pooledBullet = CreatePooledBullet(em, 703, 5f, 7f);
            BulletFieldShared.FreeByKey.Add(703, pooledBullet);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletActiveTag>(pooledBullet), Is.True);
            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(source)[0].ActiveCount, Is.EqualTo(1));
            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(0));
            Assert.That(em.GetComponentData<DiscreteEmitBacklogMetricsComponent>(channel).LastFrameBudgetUsed, Is.EqualTo(1));

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));
        }

        private static HazardEmitterEmissionProfileSO CreateEmissionProfile()
        {
            var bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            bullet.Editor_SetDefinitionId(9001);
            var profile = ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();
            profile.Bullet = bullet;
            profile.PositionPattern = new SinglePointPositionPatternAuthoring();
            profile.Aim = new FixedAimAuthoring { BaseAngleDeg = 90f };
            profile.ShotPattern = new SingleShotPatternAuthoring();
            profile.EventRepeatCount = 1;
            profile.EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
            profile.EventShotIntervalSec = 0f;
            profile.CooldownSec = 1f;
            return profile;
        }

        private static World CreateDefaultTestWorld(string worldName, out SimulationSystemGroup simGroup)
        {
            var world = new World(worldName);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null);
            return world;
        }

        private static void InitializeBuildWorld(EntityManager em, RunDirectorStageStateId stageState, float deltaTime)
        {
            CreateFrameCounter(em, 1u);

            var discreteEntity = GetDiscreteChannel(em, budgetPerFrame: 256, maxPendingCount: 8192, maxPendingAgeFrames: 120u);
            em.SetComponentData(discreteEntity, default(DiscreteEmitBacklogMetricsComponent));

            var tickEntity = GetOrCreateSingletonEntity<FixedTickStepRuntimeComponent>(em);
            em.SetComponentData(tickEntity, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = deltaTime,
                LogicDeltaTime = deltaTime,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });

            var stageEntity = GetOrCreateSingletonEntity<RunDirectorStageStateComponent>(em);
            em.SetComponentData(stageEntity, new RunDirectorStageStateComponent
            {
                State = stageState,
                StateElapsedSec = 0f,
            });
        }

        private static void CreateFrameCounter(EntityManager em, uint frame)
        {
            var entity = GetOrCreateSingletonEntity<BulletFrameCounterComponent>(em);
            em.SetComponentData(entity, new BulletFrameCounterComponent { Value = frame });
        }

        private static Entity GetDiscreteChannel(EntityManager em, int budgetPerFrame, int maxPendingCount, uint maxPendingAgeFrames)
        {
            var entity = GetOrCreateSingletonEntity<DiscreteEmitChannelSingletonTag>(em);
            if (!em.HasBuffer<DiscreteEmitRequestBuffer>(entity))
                em.AddBuffer<DiscreteEmitRequestBuffer>(entity);
            if (!em.HasComponent<DiscreteEmitPolicyComponent>(entity))
                em.AddComponentData(entity, new DiscreteEmitPolicyComponent());
            if (!em.HasComponent<DiscreteEmitBacklogMetricsComponent>(entity))
                em.AddComponentData(entity, default(DiscreteEmitBacklogMetricsComponent));

            em.SetComponentData(entity, new DiscreteEmitPolicyComponent
            {
                BudgetPerFrame = budgetPerFrame,
                MaxPendingCount = maxPendingCount,
                MaxPendingAgeFrames = maxPendingAgeFrames,
            });
            return entity;
        }

        private static Entity CreateSourceWithActiveCountBuffer(EntityManager em, int bulletTypeKey)
        {
            var entity = em.CreateEntity();
            var counts = em.AddBuffer<SourceActiveBulletCountBuffer>(entity);
            counts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = bulletTypeKey,
                ActiveCount = 0,
            });
            return entity;
        }

        private static Entity CreateEmitter(
            EntityManager em,
            Entity source,
            int bulletTypeKey,
            float telegraphDurationSec,
            float cooldownSec,
            bool isEnabled,
            bool isSuppressed,
            float3 position,
            float3 localOffset)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(HazardEmitterComponent),
                typeof(HazardEmitterTelegraphProfileComponent),
                typeof(HazardEmitterEmissionProfileComponent),
                typeof(HazardEmitterRuntimeStateComponent));

            em.SetComponentData(entity, LocalTransform.FromPosition(position));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            em.SetComponentData(entity, new HazardEmitterComponent
            {
                EmitterId = 1,
                SourceEntity = source,
                ActivationPolicy = HazardEmitterActivationPolicyId.AlwaysCycle,
                InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                AnchorKind = HazardEmitterAnchorKindId.ObjectBound,
                Mobility = HazardEmitterMobilityId.Static,
                IsEnabled = isEnabled ? (byte)1 : (byte)0,
                IsSuppressed = isSuppressed ? (byte)1 : (byte)0,
                LocalOffset = localOffset,
                TelegraphProfileRefId = 1,
                EmissionProfileRefId = 1,
            });
            em.SetComponentData(entity, new HazardEmitterTelegraphProfileComponent
            {
                ProfileId = 1,
                TelegraphDurationSec = telegraphDurationSec,
            });
            em.SetComponentData(entity, new HazardEmitterEmissionProfileComponent
            {
                ProfileId = 1,
                BulletTypeKey = bulletTypeKey,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                EventRepeatCount = 1,
                CooldownSec = cooldownSec,
            });
            em.SetComponentData(entity, new HazardEmitterRuntimeStateComponent
            {
                LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                StateElapsedSec = 0f,
            });
            return entity;
        }

        private static Entity CreatePooledBullet(EntityManager em, int bulletTypeKey, float speed, float lifetime)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(BulletVelocityComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletSpeedComponent),
                typeof(BulletLifetimeMaxComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletLifecycleTraceComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(entity, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 0f });
            em.SetComponentData(entity, new BulletSpeedComponent { Value = speed });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, default(BulletLifecycleRequestComponent));
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = bulletTypeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(entity, default(BulletLifecycleTraceComponent));
            em.SetComponentEnabled<BulletActiveTag>(entity, false);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, false);
            return entity;
        }

        private static DynamicBuffer<DiscreteEmitRequestBuffer> GetDiscreteEmitRequests(EntityManager em)
        {
            var channel = em.CreateEntityQuery(ComponentType.ReadOnly<DiscreteEmitChannelSingletonTag>()).GetSingletonEntity();
            return em.GetBuffer<DiscreteEmitRequestBuffer>(channel);
        }

        private static Entity GetOrCreateSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
                return em.CreateEntity(typeof(T));
            return query.GetSingletonEntity();
        }

        private static void InitializeSharedContainers(int capacity = 128)
        {
            BulletFieldShared.FreeByKey = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.CellMap = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.HazardCellMap = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.PoolFence = default;
            BulletFieldShared.CellMapFence = default;
            BulletFieldShared.MarkInitialized();
        }

        private static void ForceDisposeSharedContainersIfNeeded()
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            Unity.Jobs.JobHandle.CombineDependencies(BulletFieldShared.PoolFence, BulletFieldShared.CellMapFence).Complete();
            if (BulletFieldShared.CellMap.IsCreated)
                BulletFieldShared.CellMap.Dispose();
            if (BulletFieldShared.HazardCellMap.IsCreated)
                BulletFieldShared.HazardCellMap.Dispose();
            if (BulletFieldShared.FreeByKey.IsCreated)
                BulletFieldShared.FreeByKey.Dispose();

            BulletFieldShared.PoolFence = default;
            BulletFieldShared.CellMapFence = default;
            BulletFieldShared.MarkUninitialized();
        }
    }
}
