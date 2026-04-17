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
    public class BulletMotionCompletedExplodePlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator PlayMode_MotionCompletedExplode_SpawnsSecondaryBulletsNextExecutionBegin()
        {
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();

            try
            {
                using var world = new World("PlayMode_MotionCompletedExplode");
                var em = world.EntityManager;

                SetFrameAndSimulationPrerequisites(em, frame: 1u);
                SetGameplayReadySingletons(em);
                SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
                CreateSecondaryChannel(em);

                var source = em.CreateEntity();
                em.AddBuffer<SourceActiveBulletCountBuffer>(source);

                var sourceBullet = CreateSimulationBullet(
                    em,
                    position: new float3(1f, 0f, 2f),
                    velocity: new float2(0.5f, 0f),
                    radius: 0.05f,
                    lifetime: 5f,
                    typeKey: 9,
                    sourceRef: source,
                    dampedMotion: new BulletDampedMotionComponent
                    {
                        DampingPerSec = 100f,
                        StopSpeedThreshold = 0.1f,
                    },
                    explodeReaction: new BulletOnMotionCompletedExplodeReactionComponent
                    {
                        SecondaryBulletTypeKey = 21,
                        SpawnCount = 3,
                        Shape = BulletSecondarySpawnShapeId.PointBurst,
                        SpreadAngleDeg = 0f,
                        SpawnRadius = 1f,
                        SpawnDelaySec = 0f,
                    });

                var secondaryBullets = new Entity[3];
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    secondaryBullets[i] = CreatePooledSecondaryBullet(em, typeKey: 21, speed: 2f, lifetime: 6f);
                    BulletFieldShared.FreeByKey.Add(21, secondaryBullets[i]);
                }

                world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletDespawnExecutionSystem>().Update(world.Unmanaged);
                AdvanceFrame(em);
                world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(sourceBullet), Is.False);

                int activeSecondaryCount = 0;
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    var entity = secondaryBullets[i];
                    if (!em.IsComponentEnabled<BulletActiveTag>(entity))
                        continue;

                    activeSecondaryCount++;
                    Assert.That(em.GetComponentData<BulletSourceRefComponent>(entity).Value, Is.EqualTo(source));
                }

                Assert.That(activeSecondaryCount, Is.EqualTo(3));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }

            yield break;
        }

        private static void SetFrameAndSimulationPrerequisites(EntityManager em, uint frame)
        {
            SetSingleton(em, new BulletFieldConfigComponent
            {
                PoolSize = 64,
                InvCellSize = 1f,
            });
            SetSingleton(em, new BulletFrameCounterComponent
            {
                Value = frame,
            });
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f,
                LogicDeltaTime = 1f,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });
            em.CreateEntity(typeof(PlayerTag));
        }

        private static void SetGameplayReadySingletons(EntityManager em)
        {
            SetSingleton(em, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 1,
                Ready = 1,
            });
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
            });
        }

        private static void SetRuntimeGrid(EntityManager em, StageCellMovementFlags[] flags, int width = 1, int height = 1)
        {
            var entity = em.CreateEntity(typeof(StageRuntimeGridComponent));
            em.SetComponentData(entity, new StageRuntimeGridComponent
            {
                StageId = 1,
                Width = width,
                Height = height,
                CellSize = 1f,
                OriginX = 0f,
                OriginZ = 0f,
                Ready = 1,
            });

            var cells = em.AddBuffer<StageRuntimeGridCellBufferElement>(entity);
            for (int i = 0; i < flags.Length; i++)
            {
                cells.Add(new StageRuntimeGridCellBufferElement
                {
                    MovementFlags = flags[i],
                    DepositRegionId = 0u,
                });
            }
        }

        private static void CreateSecondaryChannel(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(BulletSecondarySpawnChannelSingletonTag),
                typeof(SecondarySpawnPolicyComponent),
                typeof(SecondarySpawnBacklogMetricsComponent));
            em.SetComponentData(entity, new SecondarySpawnPolicyComponent
            {
                BudgetPerFrame = 8,
                MaxPendingCount = 32,
                MaxPendingAgeFrames = 120,
            });
            em.SetComponentData(entity, default(SecondarySpawnBacklogMetricsComponent));
            em.AddBuffer<BulletSecondarySpawnRequestBuffer>(entity);
        }

        private static Entity CreateSimulationBullet(
            EntityManager em,
            float3 position,
            float2 velocity,
            float radius,
            float lifetime,
            int typeKey,
            Entity sourceRef,
            BulletDampedMotionComponent dampedMotion,
            BulletOnMotionCompletedExplodeReactionComponent explodeReaction)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletVelocityComponent),
                typeof(BulletRadiusComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletLifetimeMaxComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletLifecycleTraceComponent),
                typeof(BulletDampedMotionComponent),
                typeof(BulletOnMotionCompletedExplodeReactionComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new BulletVelocityComponent { Value = velocity });
            em.SetComponentData(entity, new BulletRadiusComponent { Value = radius });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = lifetime });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.None,
                Priority = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = typeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = sourceRef });
            em.SetComponentData(entity, new BulletLifecycleTraceComponent());
            em.SetComponentData(entity, dampedMotion);
            em.SetComponentData(entity, explodeReaction);
            em.SetComponentEnabled<BulletActiveTag>(entity, true);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, false);
            return entity;
        }

        private static Entity CreatePooledSecondaryBullet(EntityManager em, int typeKey, float speed, float lifetime)
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

            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(entity, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 0f });
            em.SetComponentData(entity, new BulletSpeedComponent { Value = speed });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.None,
                Priority = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = typeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(entity, new BulletLifecycleTraceComponent());
            em.SetComponentEnabled<BulletActiveTag>(entity, false);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, true);
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

        private static void AdvanceFrame(EntityManager em)
        {
            var entity = em.CreateEntityQuery(ComponentType.ReadOnly<BulletFrameCounterComponent>()).GetSingletonEntity();
            var counter = em.GetComponentData<BulletFrameCounterComponent>(entity);
            counter.Value += 1;
            em.SetComponentData(entity, counter);
        }
    }
}
