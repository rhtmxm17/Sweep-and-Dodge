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
    public class BulletCleanupRemovedSecondarySpawnPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayMode_VacuumCollectedCleanupRemoved_SpawnsRewardBulletsNextExecutionBegin()
        {
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();

            try
            {
                using var world = new World("PlayMode_VacuumCollectedCleanupRemoved");
                var em = world.EntityManager;

                SetSimulationAndVacuumPrerequisites(em, frame: 1u);
                SetGameplayReadySingletons(em);
                SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
                CreateCombatChannel(em);
                CreateSecondaryChannel(em);
                var player = CreateVacuumContractPlayer(em, carryLoad: 0, carryCapacity: 10);

                var source = em.CreateEntity();
                em.AddBuffer<SourceActiveBulletCountBuffer>(source);

                var collectedBullet = CreateCollectibleBullet(
                    em,
                    position: new float3(1f, 0f, 0f),
                    scoreValue: 2,
                    sourceEntity: source,
                    collectReaction: new BulletOnCleanupRemovedSpawnSecondaryReactionComponent
                    {
                        SecondaryBulletTypeKey = 21,
                        SpawnCount = 2,
                        Shape = BulletSecondarySpawnShapeId.PointBurst,
                        SpreadAngleDeg = 0f,
                        SpawnRadius = 1f,
                    });

                var secondaryBullets = new Entity[2];
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    secondaryBullets[i] = CreatePooledSecondaryBullet(em, typeKey: 21, speed: 2f, lifetime: 6f);
                    BulletFieldShared.FreeByKey.Add(21, secondaryBullets[i]);
                }

                ActivateVacuum(em, player);

                world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletDespawnExecutionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(collectedBullet), Is.False);

                int activeSecondaryCount = 0;
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    var entity = secondaryBullets[i];
                    if (!em.IsComponentEnabled<BulletActiveTag>(entity))
                        continue;

                    activeSecondaryCount++;
                    Assert.That(em.GetComponentData<BulletSourceRefComponent>(entity).Value, Is.EqualTo(source));
                }

                Assert.That(activeSecondaryCount, Is.EqualTo(2));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayMode_CarryFullRemovedCleanupRemoved_SpawnsRewardBulletsWithoutProgressGain()
        {
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();

            try
            {
                using var world = new World("PlayMode_CarryFullRemovedCleanupRemoved");
                var em = world.EntityManager;

                SetSimulationAndVacuumPrerequisites(em, frame: 1u);
                SetGameplayReadySingletons(em);
                SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
                CreateCombatChannel(em);
                CreateSecondaryChannel(em);
                var player = CreateVacuumContractPlayer(em, carryLoad: 10, carryCapacity: 10);

                var source = em.CreateEntity();
                em.AddBuffer<SourceActiveBulletCountBuffer>(source);

                var removedBullet = CreateCleanupRemovableHazardBullet(
                    em,
                    position: new float3(2.88f, 0f, 0f),
                    scoreValue: 2,
                    sourceEntity: source,
                    cleanupRemovedReaction: new BulletOnCleanupRemovedSpawnSecondaryReactionComponent
                    {
                        SecondaryBulletTypeKey = 21,
                        SpawnCount = 2,
                        Shape = BulletSecondarySpawnShapeId.PointBurst,
                        SpreadAngleDeg = 0f,
                        SpawnRadius = 1f,
                    });

                var secondaryBullets = new Entity[2];
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    secondaryBullets[i] = CreatePooledSecondaryBullet(em, typeKey: 21, speed: 2f, lifetime: 6f);
                    BulletFieldShared.FreeByKey.Add(21, secondaryBullets[i]);
                }

                ActivateVacuum(em, player);

                world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletDespawnExecutionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(removedBullet), Is.False);
                Assert.That(em.GetComponentData<PlayerCarryBinComponent>(player).Load, Is.EqualTo(10));

                int activeSecondaryCount = 0;
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    var entity = secondaryBullets[i];
                    if (!em.IsComponentEnabled<BulletActiveTag>(entity))
                        continue;

                    activeSecondaryCount++;
                    Assert.That(em.GetComponentData<BulletSourceRefComponent>(entity).Value, Is.EqualTo(source));
                }

                Assert.That(activeSecondaryCount, Is.EqualTo(2));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }

            yield break;
        }

        private static void SetSimulationAndVacuumPrerequisites(EntityManager em, uint frame)
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

        private static void CreateCombatChannel(EntityManager em)
        {
            var entity = em.CreateEntity(typeof(CombatEventChannelSingletonTag), typeof(CombatEventMetricsComponent));
            em.SetComponentData(entity, default(CombatEventMetricsComponent));
            em.AddBuffer<CombatEventBufferElement>(entity);
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

        private static Entity CreateVacuumContractPlayer(EntityManager em, int carryLoad, int carryCapacity)
        {
            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(LocalTransform),
                typeof(PlayerGoSyncComponent),
                typeof(PlayerInputIntentComponent),
                typeof(PlayerResolvedInputSnapshotComponent),
                typeof(PlayerRadiusComponent),
                typeof(VacuumActivationConfigComponent),
                typeof(VacuumRuntimeStateComponent),
                typeof(PlayerCarryBinComponent),
                typeof(PlayerHazardRiskConfigComponent),
                typeof(PlayerHazardRiskStateComponent),
                typeof(PlayerHazardRiskRequestComponent),
                typeof(PlayerHazardPenaltyConfigComponent),
                typeof(PlayerHazardPenaltyStateComponent),
                typeof(PlayerCleanupActionStateComponent),
                typeof(PlayerCleanupActionSlotMapComponent),
                typeof(PlayerCarryBinDepositRequestTag),
                typeof(PlayerCarryBinDepositContextComponent),
                typeof(PlayerHazardHitRequestTag),
                typeof(PlayerHazardHitContextComponent));

            em.SetComponentData(player, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                SyncRotation = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
            });
            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });
            em.SetComponentData(player, new PlayerResolvedInputSnapshotComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });
            em.SetComponentData(player, new PlayerRadiusComponent { Value = 0.35f });
            em.SetComponentData(player, new VacuumActivationConfigComponent
            {
                CaptureActiveTime = 0.25f,
                CaptureCooldown = 0f,
                ActiveTime = 0.25f,
                Cooldown = 0f,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0f,
                CooldownTimer = 0f,
                IsActive = 0,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCarryBinComponent
            {
                Load = math.max(0, carryLoad),
                Capacity = math.max(1, carryCapacity),
            });
            em.SetComponentData(player, new PlayerHazardRiskConfigComponent
            {
                HazardBonusRate = 0.1f,
                HazardStackMax = 5,
            });
            em.SetComponentData(player, new PlayerHazardRiskStateComponent
            {
                HazardStack = 0,
            });
            em.SetComponentData(player, new PlayerHazardRiskRequestComponent
            {
                PendingHazardCapturedCount = 0,
                ResetRequested = 0,
            });
            em.SetComponentData(player, new PlayerHazardPenaltyConfigComponent
            {
                CarryLossFrac = 0.15f,
                CarryLossMin = 1,
                CarryLossMax = 5,
                IFrameTime = 0.7f,
                VacuumLockTime = 0.7f,
                HitImpulseMagnitude = 1f,
            });
            em.SetComponentData(player, new PlayerHazardPenaltyStateComponent
            {
                IFrameTimer = 0f,
                VacuumLockTimer = 0f,
            });
            em.SetComponentData(player, new PlayerCleanupActionStateComponent
            {
                SelectedActionId = PlayerCleanupActionId.RadialRing,
                PendingActionId = PlayerCleanupActionId.None,
                Version = 0,
            });
            em.SetComponentData(player, new PlayerCleanupActionSlotMapComponent
            {
                PrimaryActionId = PlayerCleanupActionId.RadialRing,
                SecondaryActionId = PlayerCleanupActionId.ForwardFanLine,
            });
            em.SetComponentEnabled<PlayerCarryBinDepositRequestTag>(player, false);
            em.SetComponentData(player, new PlayerCarryBinDepositContextComponent());
            em.SetComponentEnabled<PlayerHazardHitRequestTag>(player, false);
            em.SetComponentData(player, new PlayerHazardHitContextComponent
            {
                SourceEntity = Entity.Null,
                HitDirX = 0f,
                HitDirZ = 0f,
            });
            var actionProfiles = em.AddBuffer<PlayerCleanupActionProfileBufferElement>(player);
            actionProfiles.Add(new PlayerCleanupActionProfileBufferElement
            {
                ActionId = PlayerCleanupActionId.RadialRing,
                TrashRange = 3.2f,
                TrashFanHalfAngleDeg = 180f,
                HazardRingRadius = 2.88f,
                HazardRingWidth = 0.8f,
                HazardLineLength = 0f,
                HazardLineHalfWidth = 0f,
            });
            var uiBuffer = em.AddBuffer<PlayerUiFeedbackEventBufferElement>(player);
            uiBuffer.EnsureCapacity(64);
            var impulseBuffer = em.AddBuffer<PlayerImpulseEventBufferElement>(player);
            impulseBuffer.EnsureCapacity(16);
            em.AddComponentData(player, new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 0u,
                Type = PlayerUiFeedbackEventType.None,
                Reason = (byte)PlayerUiFeedbackReasonId.None,
                Value = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
                RemainingSec = 0f,
                ClockSec = 0f,
                NextAllowedVacuumBlockedSec = 0f,
                NextAllowedSourceStateChangedSec = 0f,
                NextAllowedHazardCapturedSec = 0f,
                NextAllowedHazardRemovedSec = 0f,
                NextAllowedHitSec = 0f,
            });
            return player;
        }

        private static void ActivateVacuum(EntityManager em, Entity player)
        {
            var intent = em.GetComponentData<PlayerInputIntentComponent>(player);
            intent.VacuumRequested = 1;
            intent.Sequence += 1u;
            em.SetComponentData(player, intent);

            var vacuum = em.GetComponentData<VacuumRuntimeStateComponent>(player);
            vacuum.ActivateRequested = 1;
            em.SetComponentData(player, vacuum);
        }

        private static Entity CreateCollectibleBullet(
            EntityManager em,
            float3 position,
            int scoreValue,
            Entity sourceEntity,
            BulletOnCleanupRemovedSpawnSecondaryReactionComponent collectReaction)
        {
            var bullet = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletVelocityComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletRadiusComponent),
                typeof(BulletScoreValueComponent),
                typeof(BulletCaptureRuleComponent),
                typeof(BulletOnCleanupRemovedSpawnSecondaryReactionComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(bullet, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(bullet, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(bullet, new BulletLifetimeComponent { Value = 8f });
            em.SetComponentData(bullet, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.None,
                Priority = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(bullet, default(BulletLifecycleContactComponent));
            em.SetComponentData(bullet, new BulletTypeKeyComponent { Value = 1 });
            em.SetComponentData(bullet, new BulletSourceRefComponent { Value = sourceEntity });
            em.SetComponentData(bullet, new BulletRadiusComponent { Value = 0.2f });
            em.SetComponentData(bullet, new BulletScoreValueComponent { Value = math.max(0, scoreValue) });
            em.SetComponentData(bullet, new BulletCaptureRuleComponent { Value = BulletCaptureRuleId.StandardCollectible });
            em.SetComponentData(bullet, collectReaction);
            em.SetComponentEnabled<BulletActiveTag>(bullet, true);
            em.SetComponentEnabled<BulletDespawnRequestTag>(bullet, false);
            return bullet;
        }

        private static Entity CreateCleanupRemovableHazardBullet(
            EntityManager em,
            float3 position,
            int scoreValue,
            Entity sourceEntity,
            BulletOnCleanupRemovedSpawnSecondaryReactionComponent cleanupRemovedReaction)
        {
            var bullet = CreateCollectibleBullet(em, position, scoreValue, sourceEntity, cleanupRemovedReaction);
            em.SetComponentData(bullet, new BulletCaptureRuleComponent { Value = BulletCaptureRuleId.RiskTimedResolve });
            return bullet;
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
    }
}
