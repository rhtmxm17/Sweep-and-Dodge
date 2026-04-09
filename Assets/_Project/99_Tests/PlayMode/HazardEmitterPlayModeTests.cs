using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class HazardEmitterPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayMode_AlwaysCycleHazardEmitter_AppendsAndConsumesDiscreteEmit()
        {
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();

            try
            {
                using var world = new World("PlayMode_HazardEmitter_AlwaysCycle");
                var em = world.EntityManager;

                SetSingleton(em, new BulletFrameCounterComponent { Value = 1u });
                SetSingleton(em, new FixedTickStepRuntimeComponent
                {
                    FrameDeltaTime = 1f / 60f,
                    LogicDeltaTime = 1f / 60f,
                    LogicStepCount = 1,
                    HasStep = 1,
                    UsingFixedTick = 0,
                });
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Running,
                });
                SetSingleton(em, new StageTopologyStateComponent
                {
                    SelectedStageId = 1,
                    AppliedStageId = 1,
                    Ready = 1,
                });

                var channel = em.CreateEntity(
                    typeof(DiscreteEmitChannelSingletonTag),
                    typeof(DiscreteEmitPolicyComponent),
                    typeof(DiscreteEmitBacklogMetricsComponent));
                em.SetComponentData(channel, new DiscreteEmitPolicyComponent
                {
                    BudgetPerFrame = 8,
                    MaxPendingCount = 32,
                    MaxPendingAgeFrames = 120u,
                });
                em.SetComponentData(channel, default(DiscreteEmitBacklogMetricsComponent));
                em.AddBuffer<DiscreteEmitRequestBuffer>(channel);

                var source = em.CreateEntity();
                var activeCounts = em.AddBuffer<SourceActiveBulletCountBuffer>(source);
                activeCounts.Add(new SourceActiveBulletCountBuffer
                {
                    BulletTypeKey = 801,
                    ActiveCount = 0,
                });

                var emitter = em.CreateEntity(
                    typeof(LocalTransform),
                    typeof(LocalToWorld),
                    typeof(HazardEmitterComponent),
                    typeof(HazardEmitterAppliedConfigBaselineComponent),
                    typeof(HazardEmitterAppliedConfigComponent),
                    typeof(HazardEmitterTelegraphProfileBaselineComponent),
                    typeof(HazardEmitterTelegraphProfileComponent),
                    typeof(HazardEmitterEmissionProfileBaselineComponent),
                    typeof(HazardEmitterEmissionProfileComponent),
                    typeof(HazardEmitterRuntimeStateComponent),
                    typeof(HazardEmitterCoordinatorStateComponent));
                em.SetComponentData(emitter, LocalTransform.FromPosition(new float3(2f, 0f, 1f)));
                em.SetComponentData(emitter, new LocalToWorld { Value = float4x4.Translate(new float3(2f, 0f, 1f)) });
                var actor = CreateActor(em, source, actorId: 11);
                em.SetComponentData(emitter, new HazardEmitterComponent
                {
                    EmitterId = 11,
                    ActorEntity = actor,
                    ActivationPolicy = HazardEmitterActivationPolicyId.AlwaysCycle,
                    InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    AnchorKind = HazardEmitterAnchorKindId.ObjectBound,
                    Mobility = HazardEmitterMobilityId.Static,
                });
                em.SetComponentData(emitter, new HazardEmitterAppliedConfigBaselineComponent
                {
                    IsEnabled = 1,
                    IsSuppressed = 0,
                    LocalOffset = float3.zero,
                    TelegraphProfileRefId = 1,
                    EmissionProfileRefId = 1,
                });
                em.SetComponentData(emitter, new HazardEmitterAppliedConfigComponent
                {
                    IsEnabled = 1,
                    IsSuppressed = 0,
                    LocalOffset = float3.zero,
                    TelegraphProfileRefId = 1,
                    EmissionProfileRefId = 1,
                });
                em.SetComponentData(emitter, new HazardEmitterTelegraphProfileBaselineComponent
                {
                    ProfileId = 1,
                    TelegraphDurationSec = 0f,
                });
                em.SetComponentData(emitter, new HazardEmitterTelegraphProfileComponent
                {
                    ProfileId = 1,
                    TelegraphDurationSec = 0f,
                });
                em.SetComponentData(emitter, new HazardEmitterEmissionProfileBaselineComponent
                {
                    ProfileId = 1,
                    BulletTypeKey = 801,
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
                    CooldownSec = 1f,
                });
                em.SetComponentData(emitter, new HazardEmitterEmissionProfileComponent
                {
                    ProfileId = 1,
                    BulletTypeKey = 801,
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
                    CooldownSec = 1f,
                });
                em.SetComponentData(emitter, new HazardEmitterRuntimeStateComponent
                {
                    LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    StateElapsedSec = 0f,
                });
                em.SetComponentData(emitter, new HazardEmitterCoordinatorStateComponent
                {
                    ActivationAllowed = 0,
                    SuppressionReasonMask = 0u,
                    LastPlayerDistanceSq = float.MaxValue,
                });

                var pooledBullet = CreatePooledBullet(em, 801, 4f, 6f);
                BulletFieldShared.FreeByKey.Add(801, pooledBullet);

                world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(pooledBullet), Is.True);
                Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(source)[0].ActiveCount, Is.EqualTo(1));
                Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(0));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }

            yield break;
        }

        private static Entity CreateActor(EntityManager em, Entity source, int actorId)
        {
            var entity = em.CreateEntity(
                typeof(HazardActorComponent),
                typeof(HazardActorAppliedConfigBaselineComponent),
                typeof(HazardActorAppliedConfigComponent),
                typeof(HazardActorRuntimeBaselineComponent),
                typeof(HazardActorRuntimeStateComponent),
                typeof(HazardActorPatternSelectorStateComponent));
            em.SetComponentData(entity, new HazardActorComponent
            {
                ActorId = actorId,
                SourceEntity = source,
            });
            em.SetComponentData(entity, new HazardActorAppliedConfigBaselineComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(entity, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(entity, new HazardActorRuntimeBaselineComponent
            {
                InitialPresenceState = HazardActorPresenceStateId.Hidden,
            });
            em.SetComponentData(entity, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Hidden,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(entity, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = -1,
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                SelectionSequence = 0u,
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

            JobHandle.CombineDependencies(BulletFieldShared.PoolFence, BulletFieldShared.CellMapFence).Complete();
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

        private static void SetSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }
    }
}
