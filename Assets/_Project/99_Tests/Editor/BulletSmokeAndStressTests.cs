using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletSmokeAndStressTests
    {
        private static readonly FixedString64Bytes BroomDefaultProfileKey = "broom_default";

        [Test]
        public void Smoke_CoreLoopAndBurstSpawnDespawn_RunWithoutHardLimit()
        {
            using var world = new World("BulletSmokeStressWorld");
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");

            var em = world.EntityManager;

            // 테스트 부하 시나리오:
            // - 대량 스폰/디스폰이 매 프레임 반복되도록 lifetime=0
            // - backlog hard-limit(drop/expired)은 발생하지 않도록 policy를 넉넉하게 설정
            var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 0f);
            CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 6000);
            CreatePlayer(em);
            CreateConfigSingletons(em, budgetPerFrame: 6000, maxPendingCount: 32768, maxPendingAgeFrames: 120);
            var sourceEntity = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 750f);

            int maxBudgetUsed = 0;
            int maxPending = 0;
            int maxOldestAge = 0;
            int droppedTotal = 0;
            int expiredTotal = 0;
            int spawnedFrames = 0;

            double elapsed = 0d;
            const double delta = 1d / 60d;
            for (int frame = 0; frame < 180; frame++)
            {
                elapsed += delta;
                world.SetTime(new TimeData(elapsed, (float)delta));
                simGroup.Update();

                if (!TryGetSingleton(em, out SpawnBacklogMetricsComponent metrics))
                    continue;
                if (!TryGetSingleton(em, out BulletFrameCounterComponent frameCounter))
                    continue;

                maxBudgetUsed = math.max(maxBudgetUsed, metrics.LastFrameBudgetUsed);
                maxPending = math.max(maxPending, math.max(0, metrics.PendingCount));
                droppedTotal = math.max(droppedTotal, metrics.DroppedByCapacity);
                expiredTotal = math.max(expiredTotal, metrics.ExpiredByAge);
                if (metrics.LastFrameBudgetUsed > 0)
                    spawnedFrames++;

                int oldestAge = ComputeOldestBacklogAge(em, FrameSequenceUtility.GetCurrentFrame(in frameCounter));
                maxOldestAge = math.max(maxOldestAge, oldestAge);
            }

            Assert.That(spawnedFrames, Is.GreaterThan(0));
            Assert.That(maxBudgetUsed, Is.GreaterThan(0));
            Assert.That(droppedTotal, Is.EqualTo(0));
            Assert.That(expiredTotal, Is.EqualTo(0));

            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(sourceEntity).Length, Is.GreaterThan(0));
            foreach (var item in em.GetBuffer<SourceActiveBulletCountBuffer>(sourceEntity))
                Assert.That(item.ActiveCount, Is.EqualTo(0));

            Debug.Log(
                $"[SmokeStress] scenario=A+B frames=180 budget=6000 maxPendingCount=32768 maxPendingAge=120 " +
                $"maxBudgetUsed={maxBudgetUsed} maxPending={maxPending} maxOldestAge={maxOldestAge} " +
                $"dropCount={droppedTotal} expiredByAge={expiredTotal}");

            ForceDisposeSharedContainersIfNeeded();
        }

        [Test]
        public void StressSwitch_BurstOnce_InjectsRequests_AndUpdatesHudMetrics()
        {
            using var world = new World("BulletStressSwitchWorld");
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");

            var em = world.EntityManager;

            var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 10f);
            CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 1000, lifetime: 10f);
            CreatePlayer(em);
            CreateConfigSingletons(em, budgetPerFrame: 100, maxPendingCount: 10000, maxPendingAgeFrames: 120);
            CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

            var hudEntity = em.CreateEntity(typeof(DebugHudMetricsComponent));
            em.SetComponentData(hudEntity, default(DebugHudMetricsComponent));

            var stressEntity = em.CreateEntity(typeof(StressSwitchStateComponent));
            em.SetComponentData(stressEntity, new StressSwitchStateComponent
            {
                RequestExecute = 1,
                Mode = (byte)StressSwitchModeId.BurstOnce,
                BurstCount = 300,
                SustainFrames = 0,
                SustainPerFrame = 0,
                PreferredBulletTypeKey = -1,
                RemainingFrames = 0
            });

            // Frame #1: Request 단계에서 burst가 요청 버퍼로 들어간다.
            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            simGroup.Update();
            var metricsAfterFrame1 = em.CreateEntityQuery(ComponentType.ReadOnly<SpawnBacklogMetricsComponent>()).GetSingleton<SpawnBacklogMetricsComponent>();
            Assert.That(metricsAfterFrame1.PendingCount, Is.GreaterThan(0), "Burst command must enqueue pending requests in Request group");

            // Frame #2: ExecutionBegin에서 budget만큼 실제 스폰된다.
            world.SetTime(new TimeData(2d / 60d, 1f / 60f));
            simGroup.Update();

            var metricsAfterFrame2 = em.CreateEntityQuery(ComponentType.ReadOnly<SpawnBacklogMetricsComponent>()).GetSingleton<SpawnBacklogMetricsComponent>();
            Assert.That(metricsAfterFrame2.LastFrameBudgetUsed, Is.GreaterThan(0), "Round-robin execution must consume requests with available budget");

            var hud = em.CreateEntityQuery(ComponentType.ReadOnly<DebugHudMetricsComponent>()).GetSingleton<DebugHudMetricsComponent>();
            Assert.That(hud.SpawnedThisFrame, Is.GreaterThan(0), "HUD metrics must expose spawned throughput");
            Assert.That(hud.ActiveBullets, Is.GreaterThan(0), "HUD metrics must expose active bullet count");

            var stress = em.CreateEntityQuery(ComponentType.ReadOnly<StressSwitchStateComponent>()).GetSingleton<StressSwitchStateComponent>();
            Assert.That(stress.RequestExecute, Is.EqualTo(0), "Stress request flag must be consumed");
            Assert.That(stress.Mode, Is.EqualTo((byte)StressSwitchModeId.None), "Burst mode must complete in a single request cycle");

            ForceDisposeSharedContainersIfNeeded();
        }

        [Test]
        public void FixedTickSkeleton_BootstrapsRootGroupAndSingleton()
        {
            using var world = CreateDefaultTestWorld("FixedTickSkeletonWorld", out var simGroup);
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");
            var fixedTickRoot = world.GetExistingSystemManaged<FixedTickRootGroup>();
            Assert.That(fixedTickRoot, Is.Not.Null, "FixedTickRootGroup must exist");

            var em = world.EntityManager;
            CreatePlayer(em);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            simGroup.Update();

            Assert.That(TryGetSingleton(em, out FixedTickTimeComponent fixedTick), Is.True);
            Assert.That(fixedTick.EnableFixedTick, Is.EqualTo(1));
            Assert.That(fixedTick.PauseRequested, Is.EqualTo(0));
            Assert.That(fixedTick.StepRequested, Is.EqualTo(0));
            Assert.That(fixedTick.MaxSubSteps, Is.EqualTo(4));
            Assert.That(fixedTick.FixedDeltaTime, Is.EqualTo(1f / 60f).Within(1e-6f));
            Assert.That(TryGetSingleton(em, out FixedTickStepRuntimeComponent fixedTickRuntime), Is.True);
            Assert.That(fixedTickRuntime.UsingFixedTick, Is.EqualTo(1));
            Assert.That(fixedTickRuntime.CurrentLogicFrame, Is.EqualTo(1u));
        }

        [Test]
        public void FixedTickRuntime_UsesFrameDelta_WhenFixedTickDisabled()
        {
            using var world = CreateDefaultTestWorld("FixedTickRuntimeFrameDeltaWorld", out var simGroup);
            var em = world.EntityManager;
            CreatePlayer(em);
            SetFixedTickEnabled(em, enabled: false);

            const float dt = 0.02f;
            world.SetTime(new TimeData(dt, dt));
            simGroup.Update();

            Assert.That(TryGetSingleton(em, out FixedTickStepRuntimeComponent runtime), Is.True);
            Assert.That(runtime.HasStep, Is.EqualTo(1));
            Assert.That(runtime.UsingFixedTick, Is.EqualTo(0));
            Assert.That(runtime.LogicStepCount, Is.EqualTo(1));
            Assert.That(runtime.LogicDeltaTime, Is.EqualTo(dt).Within(1e-6f));
            Assert.That(runtime.CurrentLogicFrame, Is.EqualTo(1u));
        }

        [Test]
        public void FixedTickRuntime_ClampsAccumulator_AndPublishesFixedDelta()
        {
            using var world = CreateDefaultTestWorld("FixedTickRuntimeClampWorld", out var simGroup);
            var em = world.EntityManager;
            CreatePlayer(em);

            Assert.That(TryGetSingleton(em, out FixedTickTimeComponent fixedTick), Is.True);
            fixedTick.EnableFixedTick = 1;
            fixedTick.MaxSubSteps = 2;
            fixedTick.FixedDeltaTime = 0.1f;
            fixedTick.Accumulator = 0f;
            em.SetComponentData(em.CreateEntityQuery(ComponentType.ReadWrite<FixedTickTimeComponent>()).GetSingletonEntity(), fixedTick);

            world.SetTime(new TimeData(1d, 1f));
            simGroup.Update();

            Assert.That(TryGetSingleton(em, out FixedTickStepRuntimeComponent runtime), Is.True);
            Assert.That(runtime.HasStep, Is.EqualTo(1));
            Assert.That(runtime.UsingFixedTick, Is.EqualTo(1));
            Assert.That(runtime.LogicDeltaTime, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(runtime.CurrentLogicFrame, Is.EqualTo(1u));

            Assert.That(TryGetSingleton(em, out FixedTickTimeComponent fixedTickAfter), Is.True);
            Assert.That(fixedTickAfter.Accumulator, Is.EqualTo(0.1f).Within(1e-6f));
        }

        [Test]
        public void FixedTick_PauseAndSingleStep_AdvancesExactlyOneTick()
        {
            var pauseControllerGo = new GameObject("FixedTickPauseStepController");
            try
            {
                var pauseController = pauseControllerGo.AddComponent<DemoShellGameplayPauseController>();
                pauseController.LogBindWarnings = false;

                using var world = CreateDefaultTestWorld("FixedTickPauseStepWorld", out var simGroup);
                var em = world.EntityManager;
                CreatePlayer(em);

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var fixedTickEntity = em.CreateEntityQuery(ComponentType.ReadWrite<FixedTickTimeComponent>()).GetSingletonEntity();
                var frameCounterEntity = em.CreateEntityQuery(ComponentType.ReadWrite<BulletFrameCounterComponent>()).GetSingletonEntity();
                uint baseTick = em.GetComponentData<BulletFrameCounterComponent>(frameCounterEntity).Value;

                var fixedTick = em.GetComponentData<FixedTickTimeComponent>(fixedTickEntity);
                fixedTick.StepRequested = 0;
                fixedTick.MaxSubSteps = 1;
                fixedTick.FixedDeltaTime = 1f / 60f;
                fixedTick.Accumulator = 0f;
                em.SetComponentData(fixedTickEntity, fixedTick);

                pauseController.Acquire(GameplayPauseReasonId.Debug, GameplayPauseFlags.PauseSimulation);

                world.SetTime(new TimeData(2d / 60d, 0.5f));
                simGroup.Update();
                Assert.That(em.GetComponentData<BulletFrameCounterComponent>(frameCounterEntity).Value, Is.EqualTo(baseTick));
                Assert.That(TryGetSingleton(em, out FixedTickStepRuntimeComponent pausedRuntime), Is.True);
                Assert.That(pausedRuntime.HasStep, Is.EqualTo(0));
                Assert.That(pausedRuntime.CurrentLogicFrame, Is.EqualTo(baseTick));

                fixedTick = em.GetComponentData<FixedTickTimeComponent>(fixedTickEntity);
                fixedTick.StepRequested = 1;
                em.SetComponentData(fixedTickEntity, fixedTick);

                world.SetTime(new TimeData(3d / 60d, 0f));
                simGroup.Update();
                Assert.That(em.GetComponentData<BulletFrameCounterComponent>(frameCounterEntity).Value, Is.EqualTo(baseTick + 1u));
                Assert.That(TryGetSingleton(em, out FixedTickStepRuntimeComponent stepRuntime), Is.True);
                Assert.That(stepRuntime.HasStep, Is.EqualTo(1));
                Assert.That(stepRuntime.LogicDeltaTime, Is.EqualTo(1f / 60f).Within(1e-6f));
                Assert.That(stepRuntime.CurrentLogicFrame, Is.EqualTo(baseTick + 1u));

                world.SetTime(new TimeData(4d / 60d, 0.2f));
                simGroup.Update();
                Assert.That(em.GetComponentData<BulletFrameCounterComponent>(frameCounterEntity).Value, Is.EqualTo(baseTick + 1u));
                Assert.That(TryGetSingleton(em, out FixedTickStepRuntimeComponent pauseRuntimeAfterStep), Is.True);
                Assert.That(pauseRuntimeAfterStep.HasStep, Is.EqualTo(0));
                Assert.That(pauseRuntimeAfterStep.CurrentLogicFrame, Is.EqualTo(baseTick + 1u));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pauseControllerGo);
            }
        }

        [Test]
        public void CombatEventChannel_ConsumesAndAggregatesHitCollectCleanup()
        {
            using var world = new World("CombatEventChannelWorld");
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");

            var em = world.EntityManager;
            CreatePlayer(em);
            CreateConfigSingletons(em, budgetPerFrame: 1, maxPendingCount: 1024, maxPendingAgeFrames: 120);

            var channelEntity = em.CreateEntity(
                typeof(CombatEventChannelSingletonTag),
                typeof(CombatEventMetricsComponent));
            em.SetComponentData(channelEntity, default(CombatEventMetricsComponent));
            var combatEvents = em.AddBuffer<CombatEventBufferElement>(channelEntity);
            combatEvents.Add(new CombatEventBufferElement
            {
                Type = CombatEventTypeId.Hit,
                SourceEntity = Entity.Null,
                RelatedEntity = Entity.Null,
                Count = 1,
                Value = 7,
                Frame = 41,
                Sequence = 0,
            });
            combatEvents.Add(new CombatEventBufferElement
            {
                Type = CombatEventTypeId.Collect,
                SourceEntity = Entity.Null,
                RelatedEntity = Entity.Null,
                Count = 1,
                Value = 13,
                Frame = 41,
                Sequence = 1,
            });
            combatEvents.Add(new CombatEventBufferElement
            {
                Type = CombatEventTypeId.Cleanup,
                SourceEntity = Entity.Null,
                RelatedEntity = Entity.Null,
                Count = 1,
                Value = 5,
                Frame = 42,
                Sequence = 2,
            });

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            simGroup.Update();

            var metrics = em.GetComponentData<CombatEventMetricsComponent>(channelEntity);
            Assert.That(metrics.LastConsumedFrame, Is.EqualTo(42));
            Assert.That(metrics.LastFrameHitCount, Is.EqualTo(1));
            Assert.That(metrics.LastFrameCollectCount, Is.EqualTo(1));
            Assert.That(metrics.LastFrameCleanupCount, Is.EqualTo(1));
            Assert.That(metrics.LastFrameHitValue, Is.EqualTo(7));
            Assert.That(metrics.LastFrameCollectValue, Is.EqualTo(13));
            Assert.That(metrics.LastFrameCleanupValue, Is.EqualTo(5));
            Assert.That(metrics.TotalHitCount, Is.EqualTo(1));
            Assert.That(metrics.TotalCollectCount, Is.EqualTo(1));
            Assert.That(metrics.TotalCleanupCount, Is.EqualTo(1));
            Assert.That(metrics.TotalHitValue, Is.EqualTo(7));
            Assert.That(metrics.TotalCollectValue, Is.EqualTo(13));
            Assert.That(metrics.TotalCleanupValue, Is.EqualTo(5));
            Assert.That(em.GetBuffer<CombatEventBufferElement>(channelEntity).Length, Is.EqualTo(0));

            ForceDisposeSharedContainersIfNeeded();
        }

        [Test]
        public void Vacuum_FullBin_Activation_AllowsActive_AndEmitsCarryBinFullOnce()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("VacuumFullBinActivationWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(em, carryLoad: 10, carryCapacity: 10, out var playerEntity);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                RequestVacuum(em, playerEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                var vacuum = em.GetComponentData<VacuumRuntimeStateComponent>(playerEntity);
                Assert.That(vacuum.IsActive, Is.EqualTo(1), "FullBin에서도 Vacuum 발동은 허용돼야 한다.");

                var uiBuffer = em.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
                int blockedCount = CountUiEvents(
                    uiBuffer,
                    PlayerUiFeedbackEventType.VacuumStartBlocked,
                    (byte)PlayerUiFeedbackReasonId.CarryBinFull);
                Assert.That(blockedCount, Is.EqualTo(1), "CarryBinFull 차단 피드백은 발동 입력 시 1회여야 한다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void VacuumContractDefaultFixture_UsesBroomSweepDefaults()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("VacuumContractDefaultFixtureWorld", out _);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(em, carryLoad: 0, carryCapacity: 10, out var playerEntity);

                var actionState = em.GetComponentData<PlayerCleanupActionStateComponent>(playerEntity);
                var selectionConfig = em.GetComponentData<PlayerCleanupActionSelectionConfigComponent>(playerEntity);
                var slotMap = em.GetComponentData<PlayerCleanupActionSlotMapComponent>(playerEntity);
                var resolvedProfile = em.GetComponentData<PlayerCleanupResolvedProfileComponent>(playerEntity);
                var profiles = em.GetBuffer<PlayerCleanupActionProfileBufferElement>(playerEntity);

                Assert.That(actionState.SelectedProfileKey, Is.EqualTo(BroomDefaultProfileKey));
                Assert.That(actionState.PendingProfileKey, Is.EqualTo(default(FixedString64Bytes)));
                Assert.That(selectionConfig.DefaultProfileKey, Is.EqualTo(BroomDefaultProfileKey));
                Assert.That(slotMap.PrimaryProfileKey, Is.EqualTo(BroomDefaultProfileKey));
                Assert.That(slotMap.SecondaryProfileKey, Is.EqualTo(BroomDefaultProfileKey));
                Assert.That(resolvedProfile.ProfileKey, Is.EqualTo(BroomDefaultProfileKey));
                Assert.That(resolvedProfile.ActionKind, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
                Assert.That(resolvedProfile.LockFacingWhileActive, Is.EqualTo(1));
                Assert.That(resolvedProfile.ActiveMoveSpeedScale, Is.EqualTo(0.5f));
                Assert.That(profiles.Length, Is.EqualTo(1));
                Assert.That(profiles[0].ProfileKey, Is.EqualTo(BroomDefaultProfileKey));
                Assert.That(profiles[0].ActionId, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void Vacuum_FullBin_HazardCapture_RemovesOnly_WithoutCarrySourceCollect()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("VacuumFullBinHazardRemovedWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(em, carryLoad: 10, carryCapacity: 10, out var playerEntity);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                var sourceEntity = CreateVacuumContractSource(em);
                var hazard = CreateVacuumContractBullet(
                    em,
                    position: new float3(0f, 0f, 2.88f),
                    captureRule: BulletCaptureRuleId.RiskTimedResolve,
                    scoreValue: 5,
                    sourceEntity: sourceEntity);
                PrimeBroomSweepForwardWindow(em, playerEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(hazard), Is.False, "FullBin Hazard 조건부 성공은 제거(디스폰)되어야 한다.");
                var fullBinRemovalRequest = em.GetComponentData<BulletLifecycleRequestComponent>(hazard);
                Assert.That(fullBinRemovalRequest.Reason, Is.EqualTo(BulletLifecycleReasonId.CarryFullRemoved));
                Assert.That(fullBinRemovalRequest.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.CarryFullRemoved)));
                Assert.That(fullBinRemovalRequest.RelatedEntity, Is.EqualTo(playerEntity));

                var carry = em.GetComponentData<PlayerCarryBinComponent>(playerEntity);
                Assert.That(carry.Load, Is.EqualTo(10), "FullBin 제거는 Carry를 변경하지 않아야 한다.");

                var source = em.GetComponentData<SourceSpawnComponent>(sourceEntity);
                Assert.That(source.CollectedCount, Is.EqualTo(0), "FullBin 제거는 Source 진행에 반영되지 않아야 한다.");

                var riskState = em.GetComponentData<PlayerHazardRiskStateComponent>(playerEntity);
                Assert.That(riskState.HazardStack, Is.EqualTo(0), "FullBin 제거는 HazardStack을 변경하지 않아야 한다.");

                var metrics = em.CreateEntityQuery(ComponentType.ReadOnly<CombatEventMetricsComponent>())
                    .GetSingleton<CombatEventMetricsComponent>();
                Assert.That(metrics.TotalCollectValue, Is.EqualTo(0), "FullBin 제거는 Collect 집계에 포함되면 안 된다.");

                var uiBuffer = em.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
                int removedCount = 0;
                int capturedCount = 0;
                for (int i = 0; i < uiBuffer.Length; i++)
                {
                    var evt = uiBuffer[i];
                    if (evt.Type == PlayerUiFeedbackEventType.HazardRemoved && evt.RelatedEntity == hazard)
                        removedCount++;
                    if (evt.Type == PlayerUiFeedbackEventType.HazardCaptured)
                        capturedCount++;
                }

                Assert.That(removedCount, Is.EqualTo(1), "HazardRemoved 이벤트는 탄환별로 발행되어야 한다.");
                Assert.That(capturedCount, Is.EqualTo(0), "FullBin 제거 경로에서 HazardCaptured는 발행되면 안 된다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void Vacuum_NotFull_HazardCapture_CapturedPath_UpdatesCarrySourceCollect()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("VacuumCapturedPathWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(em, carryLoad: 0, carryCapacity: 10, out var playerEntity);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                var sourceEntity = CreateVacuumContractSource(em);
                var hazard = CreateVacuumContractBullet(
                    em,
                    position: new float3(0f, 0f, 2.88f),
                    captureRule: BulletCaptureRuleId.RiskTimedResolve,
                    scoreValue: 3,
                    sourceEntity: sourceEntity);
                PrimeBroomSweepForwardWindow(em, playerEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(hazard), Is.False, "HazardCaptured 경로에서는 Hazard가 제거되어야 한다.");
                var capturedRequest = em.GetComponentData<BulletLifecycleRequestComponent>(hazard);
                Assert.That(capturedRequest.Reason, Is.EqualTo(BulletLifecycleReasonId.VacuumCollected));
                Assert.That(capturedRequest.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.VacuumCollected)));
                Assert.That(capturedRequest.RelatedEntity, Is.EqualTo(playerEntity));

                var carry = em.GetComponentData<PlayerCarryBinComponent>(playerEntity);
                Assert.That(carry.Load, Is.EqualTo(3), "HazardCaptured는 Carry 증가를 반영해야 한다.");

                var source = em.GetComponentData<SourceSpawnComponent>(sourceEntity);
                Assert.That(source.CollectedCount, Is.EqualTo(3), "HazardCaptured는 ScoreValue 기반 Source 진행을 반영해야 한다.");

                var riskState = em.GetComponentData<PlayerHazardRiskStateComponent>(playerEntity);
                Assert.That(riskState.HazardStack, Is.EqualTo(1), "HazardCaptured는 frame end HazardStack 증가를 남겨야 한다.");

                var metrics = em.CreateEntityQuery(ComponentType.ReadOnly<CombatEventMetricsComponent>())
                    .GetSingleton<CombatEventMetricsComponent>();
                Assert.That(metrics.TotalCollectValue, Is.EqualTo(3), "HazardCaptured는 Collect 집계에 반영되어야 한다.");

                var uiBuffer = em.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
                int capturedCount = 0;
                int removedCount = 0;
                for (int i = 0; i < uiBuffer.Length; i++)
                {
                    var evt = uiBuffer[i];
                    if (evt.Type == PlayerUiFeedbackEventType.HazardCaptured && evt.RelatedEntity == hazard)
                        capturedCount++;
                    if (evt.Type == PlayerUiFeedbackEventType.HazardRemoved)
                        removedCount++;
                }

                Assert.That(capturedCount, Is.EqualTo(1), "HazardCaptured 이벤트는 탄환별로 발행되어야 한다.");
                Assert.That(removedCount, Is.EqualTo(0), "비포화 수거 경로에서 HazardRemoved는 발행되면 안 된다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void HazardStack_NextFrameApply_UsesFrameStartSnapshotOnly()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("HazardStackNextFrameWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(
                    em,
                    carryLoad: 0,
                    carryCapacity: 100,
                    out var playerEntity,
                    hazardStack: 1,
                    hazardStackMax: 5,
                    hazardBonusRate: 0.1f);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                var sourceEntity = CreateVacuumContractSource(em);
                CreateVacuumContractBullet(
                    em,
                    position: new float3(0f, 0f, 2.88f),
                    captureRule: BulletCaptureRuleId.RiskTimedResolve,
                    scoreValue: 10,
                    sourceEntity: sourceEntity);
                CreateVacuumContractBullet(
                    em,
                    position: new float3(0f, 0f, 1.6f),
                    captureRule: BulletCaptureRuleId.RiskTimedResolve,
                    scoreValue: 10,
                    sourceEntity: sourceEntity);
                PrimeBroomSweepForwardWindow(em, playerEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                var sourceAfterHazards = em.GetComponentData<SourceSpawnComponent>(sourceEntity);
                Assert.That(sourceAfterHazards.CollectedCount, Is.EqualTo(22), "같은 프레임 다중 HazardCaptured는 동일 시작 stack snapshot(1.1x)을 공유해야 한다.");
                Assert.That(em.GetComponentData<PlayerHazardRiskStateComponent>(playerEntity).HazardStack, Is.EqualTo(3), "두 HazardCaptured 후 frame end HazardStack은 3이어야 한다.");

                CreateVacuumContractBullet(
                    em,
                    position: BroomPolarPosition(1.2f, 0f),
                    captureRule: BulletCaptureRuleId.StandardCollectible,
                    scoreValue: 10,
                    sourceEntity: sourceEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                var sourceAfterTrash = em.GetComponentData<SourceSpawnComponent>(sourceEntity);
                Assert.That(sourceAfterTrash.CollectedCount, Is.EqualTo(35), "다음 프레임 Trash는 증가된 HazardStack(1.3x)을 반영해야 한다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void HazardStack_SameFrameHazardCaptureAndHit_ResolvesToZeroAfterAppliedProgress()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("HazardStackCaptureHitWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(
                    em,
                    carryLoad: 0,
                    carryCapacity: 100,
                    out var playerEntity,
                    hazardStack: 2,
                    hazardStackMax: 5,
                    hazardBonusRate: 0.1f);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                var captureSource = CreateVacuumContractSource(em);
                var hitSource = CreateVacuumContractSource(em);
                CreateVacuumContractBullet(
                    em,
                    position: new float3(0f, 0f, 2.88f),
                    captureRule: BulletCaptureRuleId.RiskTimedResolve,
                    scoreValue: 10,
                    sourceEntity: captureSource);
                CreateHazardCollisionBullet(em, new float3(0.15f, 0f, -0.25f), hitSource);
                PrimeBroomSweepForwardWindow(em, playerEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                var carry = em.GetComponentData<PlayerCarryBinComponent>(playerEntity);
                Assert.That(carry.Load, Is.EqualTo(9), "수거가 먼저 적용된 뒤 같은 프레임 hit 손실이 반영되어야 한다.");

                var captureSourceState = em.GetComponentData<SourceSpawnComponent>(captureSource);
                Assert.That(captureSourceState.CollectedCount, Is.EqualTo(12), "같은 프레임 hit가 있어도 captured hazard의 Source 진행은 유지되어야 한다.");

                var riskState = em.GetComponentData<PlayerHazardRiskStateComponent>(playerEntity);
                Assert.That(riskState.HazardStack, Is.EqualTo(0), "같은 프레임 HazardCaptured + Hit 최종 HazardStack은 0이어야 한다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void HazardStack_SameFrameHazardCaptureAndDeposit_ResolvesToZeroAfterAppliedProgress()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("HazardStackCaptureDepositWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(
                    em,
                    carryLoad: 5,
                    carryCapacity: 100,
                    out var playerEntity,
                    hazardStack: 2,
                    hazardStackMax: 5,
                    hazardBonusRate: 0.1f);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                var captureSource = CreateVacuumContractSource(em);
                CreateVacuumContractBullet(
                    em,
                    position: new float3(0f, 0f, 2.88f),
                    captureRule: BulletCaptureRuleId.RiskTimedResolve,
                    scoreValue: 10,
                    sourceEntity: captureSource);
                CreateDepositRegionGrid(em, depositRegionId: 2001u);
                PrimeBroomSweepForwardWindow(em, playerEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                var carry = em.GetComponentData<PlayerCarryBinComponent>(playerEntity);
                Assert.That(carry.Load, Is.EqualTo(0), "같은 프레임 Deposit은 수거 이후 최종 Carry를 0으로 리셋해야 한다.");

                var captureSourceState = em.GetComponentData<SourceSpawnComponent>(captureSource);
                Assert.That(captureSourceState.CollectedCount, Is.EqualTo(12), "같은 프레임 Deposit이 있어도 captured hazard의 Source 진행은 유지되어야 한다.");

                var riskState = em.GetComponentData<PlayerHazardRiskStateComponent>(playerEntity);
                Assert.That(riskState.HazardStack, Is.EqualTo(0), "같은 프레임 HazardCaptured + Deposit 최종 HazardStack은 0이어야 한다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void Vacuum_FullBin_TrashInRange_NotRemoved_NoCollect()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("VacuumFullBinTrashBlockedWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(em, carryLoad: 10, carryCapacity: 10, out var playerEntity);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                var trash = CreateVacuumContractBullet(
                    em,
                    position: BroomPolarPosition(1.2f, -20f),
                    captureRule: BulletCaptureRuleId.StandardCollectible,
                    scoreValue: 4,
                    sourceEntity: Entity.Null);

                RequestVacuum(em, playerEntity);
                StepSimulationFrame(world, simGroup, ref elapsed);

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(trash), Is.True, "FullBin에서는 Trash가 제거되면 안 된다.");
                Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(trash), Is.False, "FullBin Trash는 despawn request가 생기면 안 된다.");

                var carry = em.GetComponentData<PlayerCarryBinComponent>(playerEntity);
                Assert.That(carry.Load, Is.EqualTo(10), "FullBin Trash 제한에서 Carry 변화가 없어야 한다.");

                var metrics = em.CreateEntityQuery(ComponentType.ReadOnly<CombatEventMetricsComponent>())
                    .GetSingleton<CombatEventMetricsComponent>();
                Assert.That(metrics.TotalCollectValue, Is.EqualTo(0), "FullBin Trash 제한은 Collect 집계가 증가하면 안 된다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void HazardCollision_IFrame_BlocksAdditionalHit_AndKeepsSingleHitPerFrame()
        {
            using var world = CreateDefaultTestWorldWithoutFeedbackConsumers("HazardCollisionIFrameWorld", out var simGroup);
            try
            {
                var em = world.EntityManager;
                SetupVacuumContractEnvironment(em, carryLoad: 20, carryCapacity: 100, out var playerEntity);

                double elapsed = 0d;
                StepSimulationFrame(world, simGroup, ref elapsed); // bootstrap

                var sourceEntity = CreateVacuumContractSource(em);
                var hazardA = CreateHazardCollisionBullet(em, new float3(0.15f, 0f, 0f), sourceEntity);
                var hazardB = CreateHazardCollisionBullet(em, new float3(-0.15f, 0f, 0f), sourceEntity);

                StepSimulationFrame(world, simGroup, ref elapsed); // first hit

                var firstMetrics = em.CreateEntityQuery(ComponentType.ReadOnly<CombatEventMetricsComponent>())
                    .GetSingleton<CombatEventMetricsComponent>();
                Assert.That(firstMetrics.TotalHitCount, Is.EqualTo(1), "동일 프레임 다건 겹침에서도 hit는 1회만 확정되어야 한다.");
                Assert.That(firstMetrics.LastFrameHitCount, Is.EqualTo(1));

                var impulseBuffer = em.GetBuffer<PlayerImpulseEventBufferElement>(playerEntity);
                Assert.That(impulseBuffer.Length, Is.EqualTo(1), "동일 프레임 다건 충돌 입력에서도 impulse 이벤트는 1건이어야 한다.");
                int activeAfterFirstHit = (em.IsComponentEnabled<BulletActiveTag>(hazardA) ? 1 : 0)
                    + (em.IsComponentEnabled<BulletActiveTag>(hazardB) ? 1 : 0);
                Assert.That(activeAfterFirstHit, Is.EqualTo(1), "첫 hit 프레임에서는 두 hazard 중 1개만 비활성화되어야 한다.");
                var despawnedHazard = em.IsComponentEnabled<BulletActiveTag>(hazardA) ? hazardB : hazardA;
                var hitRequest = em.GetComponentData<BulletLifecycleRequestComponent>(despawnedHazard);
                Assert.That(hitRequest.Reason, Is.EqualTo(BulletLifecycleReasonId.PlayerHit));
                Assert.That(hitRequest.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.PlayerHit)));
                Assert.That(hitRequest.RelatedEntity, Is.EqualTo(playerEntity));

                var penaltyState = em.GetComponentData<PlayerHazardPenaltyStateComponent>(playerEntity);
                Assert.That(penaltyState.IFrameTimer, Is.GreaterThan(0f), "첫 hit 이후 iFrame이 시작되어야 한다.");

                StepSimulationFrame(world, simGroup, ref elapsed); // iFrame frame

                var secondMetrics = em.CreateEntityQuery(ComponentType.ReadOnly<CombatEventMetricsComponent>())
                    .GetSingleton<CombatEventMetricsComponent>();
                Assert.That(secondMetrics.TotalHitCount, Is.EqualTo(1), "iFrame 동안 추가 hit 누적은 제외되어야 한다.");
                Assert.That(secondMetrics.LastFrameHitCount, Is.EqualTo(0), "iFrame frame에는 신규 hit가 없어야 한다.");
                impulseBuffer = em.GetBuffer<PlayerImpulseEventBufferElement>(playerEntity);
                Assert.That(impulseBuffer.Length, Is.EqualTo(1), "iFrame 동안 impulse 추가 누적이 발생하면 안 된다.");
                int activeAfterIFrame = (em.IsComponentEnabled<BulletActiveTag>(hazardA) ? 1 : 0)
                    + (em.IsComponentEnabled<BulletActiveTag>(hazardB) ? 1 : 0);
                Assert.That(activeAfterIFrame, Is.EqualTo(1), "iFrame frame에서 남은 hazard가 추가로 제거되면 안 된다.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnRequestBuild_MergesByDirectiveId_AndSeparatesDifferentDirective()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnDirectiveMergeWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));

                EnableV3Source(em, source, stableId: 11u, activeState: SourceStateId.Normal);
                const int mergeClipId = 1101;
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 101,
                    clipId: mergeClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 1f,
                    ratePerSecPerArea: 2f));
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 101,
                    clipId: mergeClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 1f,
                    ratePerSecPerArea: 3f));
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 202,
                    clipId: mergeClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 1f,
                    ratePerSecPerArea: 5f));

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = mergeClipId,
                    Weight = 1f
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = mergeClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u
                });

                world.SetTime(new TimeData(1d, 1f));
                simGroup.Update();

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(requests.Length, Is.EqualTo(2), "Different DirectiveId entries must remain separated");
                Assert.That(GetRequestCountByDirective(requests, 101), Is.EqualTo(5), "Same DirectiveId entries must be merged");
                Assert.That(GetRequestCountByDirective(requests, 202), Is.EqualTo(5));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_FixedPointCenter_SpawnsAtFixedPointWhenAreaIsZero()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnFixedPointWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 1, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, new float3(0f, 7f, 0f));
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5001,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = new float2(3f, 4f),
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 1,
                    OldestFrame = 0,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                Assert.That(TryGetSingleActiveBulletPositionForSource(em, source, out var position), Is.True);
                Assert.That(position.x, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(position.y, Is.EqualTo(7f).Within(0.0001f));
                Assert.That(position.z, Is.EqualTo(4f).Within(0.0001f));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PlayerRelativeCenter_SpawnsAtPlayerOffsetWhenAreaIsZero()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPlayerRelativeWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayerWithTransform(em, new float3(10f, 1f, 20f));
                CreateConfigSingletons(em, budgetPerFrame: 1, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5002,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.PlayerRelative,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    SpawnOffset = new float2(2f, -3f),
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 1,
                    OldestFrame = 0,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                Assert.That(TryGetSingleActiveBulletPositionForSource(em, source, out var position), Is.True);
                Assert.That(position.x, Is.EqualTo(12f).Within(0.0001f));
                Assert.That(position.y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(position.z, Is.EqualTo(17f).Within(0.0001f));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_NoSpawnRadiusWithBudget_UsesFallbackWhenAllSamplesRejected()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnNoSpawnRadiusBudgetWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                var playerPos = new float3(0f, 0f, 0f);
                CreatePlayerWithTransform(em, playerPos);
                CreateConfigSingletons(em, budgetPerFrame: 1, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5003,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.PlayerRelative,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 2,
                    PlayerNoSpawnRadius = 5f,
                    Count = 1,
                    OldestFrame = 0,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                Assert.That(TryGetSingleActiveBulletPositionForSource(em, source, out var position), Is.True);
                float2 delta = new float2(position.x - playerPos.x, position.z - playerPos.z);
                Assert.That(math.lengthsq(delta), Is.LessThan(25f), "When every sample is rejected, last-sample fallback is expected");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_SameLanePriority_OlderRequestConsumesFirst()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnOldestTieBreakWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 8, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 1, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, new float3(0f, 7f, 0f));
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5004,
                    Lane = SourceSpawnLaneId.Hazard,
                    LanePriority = SourceSpawnLanePriorityUtility.ResolvePriority(SourceSpawnLaneId.Hazard),
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = new float2(1f, 4f),
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 1,
                    OldestFrame = 1u,
                });
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5005,
                    Lane = SourceSpawnLaneId.Hazard,
                    LanePriority = SourceSpawnLanePriorityUtility.ResolvePriority(SourceSpawnLaneId.Hazard),
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = new float2(9f, 10f),
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 1,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                Assert.That(TryGetSingleActiveBulletPositionForSource(em, source, out var position), Is.True);
                Assert.That(position.x, Is.EqualTo(9f).Within(0.0001f));
                Assert.That(position.y, Is.EqualTo(7f).Within(0.0001f));
                Assert.That(position.z, Is.EqualTo(10f).Within(0.0001f));

                requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(GetRequestCountByDirective(requests, 5004), Is.EqualTo(1), "Newer request must remain pending.");
                Assert.That(GetRequestCountByDirective(requests, 5005), Is.EqualTo(0), "When lane priority ties, older request must be consumed first.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PointSetRoundRobin_SpawnsAcrossConfiguredPoints()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPointSetRoundRobinWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 3, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, new float3(0f, 6f, 0f));
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5101,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.PointSet,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = new float2(5f, 7f),
                    PointSetCount = 3,
                    Point0 = new float2(-1f, 0f),
                    Point1 = new float2(0f, 2f),
                    Point2 = new float2(3f, -1f),
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    Count = 3,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(3));

                Assert.That(
                    ContainsPosition(snapshots, new float3(4f, 6f, 7f), 0.0001f),
                    Is.True,
                    "Point0 offset should be used by round-robin sampling.");
                Assert.That(
                    ContainsPosition(snapshots, new float3(5f, 6f, 9f), 0.0001f),
                    Is.True,
                    "Point1 offset should be used by round-robin sampling.");
                Assert.That(
                    ContainsPosition(snapshots, new float3(8f, 6f, 6f), 0.0001f),
                    Is.True,
                    "Point2 offset should be used by round-robin sampling.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PointSetSpiral_UsesRepeatSequencePerEvent()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPointSetSpiralWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 6, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, new float3(0f, 0f, 0f));
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5102,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.PointSet,
                    AimMode = WaveAimModeId.Spiral,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    PointSetCount = 3,
                    Point0 = new float2(-2f, 0f),
                    Point1 = new float2(0f, 0f),
                    Point2 = new float2(2f, 0f),
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    SpiralStepDeg = 90f,
                    Count = 6,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(6));

                float3 p0 = new float3(-2f, 0f, 0f);
                float3 p1 = new float3(0f, 0f, 0f);
                float3 p2 = new float3(2f, 0f, 0f);
                float2 dirRight = new float2(1f, 0f);
                float2 dirUp = new float2(0f, 1f);
                float2 dirLeft = new float2(-1f, 0f);
                float2 dirDown = new float2(0f, -1f);

                Assert.That(CountDirectionAtPoint(snapshots, p0, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p0, dirDown, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p1, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p1, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p2, dirLeft, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p2, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_LineEvenNWayFan_SpawnsAtomicSetsPerSamplePoint()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnLineEvenNWayAtomicWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 32, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, new float3(0f, 0f, 0f));
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5201,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.NWay,
                    ShotCount = 4,
                    NWayAngleSpacingDeg = 30f,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    Count = 20,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(24);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(20));

                float2 dirNeg45 = new float2(0.70710677f, -0.70710677f);
                float2 dirNeg15 = new float2(0.9659258f, -0.25881904f);
                float2 dirPos15 = new float2(0.9659258f, 0.25881904f);
                float2 dirPos45 = new float2(0.70710677f, 0.70710677f);

                for (int i = -2; i <= 2; i++)
                {
                    var point = new float3(i, 0f, 0f);
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirNeg45, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirNeg15, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirPos15, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirPos45, 0.0001f, 0.0001f), Is.EqualTo(1));
                }

                var requestsAfter = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(requestsAfter.Length, Is.EqualTo(0));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_LineNormalAimLeft_Single_UsesLineLeftNormal()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnLineNormalLeftWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 16, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5203,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.LineNormal,
                    LineNormalSide = WaveLineNormalSideId.Left,
                    LineNormalAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 2f,
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    Count = 3,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(3));

                float2 dirUp = new float2(0f, 1f);
                Assert.That(CountDirectionAtPoint(snapshots, new float3(-2f, 0f, 0f), dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, new float3(0f, 0f, 0f), dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, new float3(2f, 0f, 0f), dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_LineNormalAimRight_Single_UsesLineRightNormal()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnLineNormalRightWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 16, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5204,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.LineNormal,
                    LineNormalSide = WaveLineNormalSideId.Right,
                    LineNormalAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 2f,
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    Count = 3,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(3));

                float2 dirDown = new float2(0f, -1f);
                Assert.That(CountDirectionAtPoint(snapshots, new float3(-2f, 0f, 0f), dirDown, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, new float3(0f, 0f, 0f), dirDown, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, new float3(2f, 0f, 0f), dirDown, 0.0001f, 0.0001f), Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_LineNormalAimOffset_Single_RotatesFromNormal()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnLineNormalOffsetWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 8, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5205,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.LineNormal,
                    LineNormalSide = WaveLineNormalSideId.Left,
                    LineNormalAngleOffsetDeg = 15f,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 2f,
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    Count = 3,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(3));

                float2 dirOffset = new float2(-0.25881904f, 0.9659258f);
                Assert.That(CountDirectionAtPoint(snapshots, new float3(-2f, 0f, 0f), dirOffset, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, new float3(0f, 0f, 0f), dirOffset, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, new float3(2f, 0f, 0f), dirOffset, 0.0001f, 0.0001f), Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_LineNormalAimNWay_UsesLineNormalAsFanCenter()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnLineNormalNWayWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 16, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5206,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.LineNormal,
                    LineNormalSide = WaveLineNormalSideId.Left,
                    LineNormalAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.NWay,
                    ShotCount = 3,
                    NWayAngleSpacingDeg = 30f,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 2f,
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    Count = 9,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(16);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(9));

                float2 dir60 = new float2(0.5f, 0.8660254f);
                float2 dir90 = new float2(0f, 1f);
                float2 dir120 = new float2(-0.5f, 0.8660254f);
                for (int i = -2; i <= 2; i += 2)
                {
                    var point = new float3(i, 0f, 0f);
                    Assert.That(CountDirectionAtPoint(snapshots, point, dir60, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dir90, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dir120, 0.0001f, 0.0001f), Is.EqualTo(1));
                }
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_LineNormalAimRadial_UsesLineNormalAsRadialBaseAngle()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnLineNormalRadialWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 16, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5207,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.LineNormal,
                    LineNormalSide = WaveLineNormalSideId.Left,
                    LineNormalAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.Radial,
                    ShotCount = 4,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 2f,
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    Count = 12,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(16);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(12));

                float2 dirRight = new float2(1f, 0f);
                float2 dirUp = new float2(0f, 1f);
                float2 dirLeft = new float2(-1f, 0f);
                float2 dirDown = new float2(0f, -1f);
                for (int i = -2; i <= 2; i += 2)
                {
                    var point = new float3(i, 0f, 0f);
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirLeft, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirDown, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                }
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_NWayAtomicity_BudgetAndPoolShortage_KeepSequenceAndPending()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnNWayAtomicityDeferralWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 3, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 3, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5202,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.NWay,
                    ShotCount = 4,
                    NWayAngleSpacingDeg = 30f,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-1f, 0f),
                    LineEnd = new float2(1f, 0f),
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    SpawnSequence = 7u,
                    Count = 4,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(0), "NWay set must not partially spawn when budget/pool is insufficient.");

                var requestsAfter = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(requestsAfter.Length, Is.EqualTo(1));
                Assert.That(requestsAfter[0].Count, Is.EqualTo(4));
                Assert.That(requestsAfter[0].SpawnSequence, Is.EqualTo(7u));

                Assert.That(TryGetSingleton(em, out SpawnBacklogMetricsComponent metrics), Is.True);
                Assert.That(metrics.LastFrameBudgetUsed, Is.EqualTo(0));
                Assert.That(metrics.PendingCount, Is.GreaterThanOrEqualTo(4));
                Assert.That(metrics.DeferredByPool, Is.GreaterThan(0));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_NWaySetConsumption_AdvancesSequenceOncePerSet()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnNWaySetSequenceWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 4, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5203,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.FixedPoint,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.NWay,
                    ShotCount = 4,
                    NWayAngleSpacingDeg = 30f,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 45f,
                    SpawnSequence = 3u,
                    Count = 8,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var requestsAfter = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(requestsAfter.Length, Is.EqualTo(1));
                Assert.That(requestsAfter[0].Count, Is.EqualTo(4), "NWay set must consume 4 pending units at once.");
                Assert.That(requestsAfter[0].SpawnSequence, Is.EqualTo(4u), "SpawnSequence should advance once per NWay set.");

                Assert.That(TryGetSingleton(em, out SpawnBacklogMetricsComponent metrics), Is.True);
                Assert.That(metrics.LastFrameBudgetUsed, Is.EqualTo(4));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnRequestBuild_PoissonMeanPositive_AccumulatesPendingRequests()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPoissonAccumulationWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var playerEntity = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>()).GetSingletonEntity();
                if (em.HasComponent<PlayerGoSyncComponent>(playerEntity))
                    em.RemoveComponent<PlayerGoSyncComponent>(playerEntity);
                var directorConfigEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunProgressDirectorConfigComponent>()).GetSingletonEntity();
                em.SetComponentData(directorConfigEntity, new RunProgressDirectorConfigComponent
                {
                    PressureHoldSec = 999f,
                    BaselineTrashDensityScale = 0.25f,
                    PressureDensityScale = 1.0f,
                });
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));

                EnableV3Source(em, source, stableId: 12u, activeState: SourceStateId.Normal);
                const int poissonClipId = 1201;
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                var poissonPattern = CreateClipPattern(
                    directiveId: 7001,
                    clipId: poissonClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 10f,
                    ratePerSecPerArea: 0f);
                poissonPattern.EmissionMode = SourceSpawnEmissionModeId.Poisson;
                poissonPattern.MeanEventsPerSec = 3600f;
                clipPatterns.Add(poissonPattern);

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = poissonClipId,
                    Weight = 1f
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = poissonClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u
                });

                var pendingAfterFrame = new int[3];
                double elapsed = 0d;
                const float delta = 1f / 60f;
                for (int i = 0; i < pendingAfterFrame.Length; i++)
                {
                    elapsed += delta;
                    world.SetTime(new TimeData(elapsed, delta));
                    simGroup.Update();
                    pendingAfterFrame[i] = SumPendingRequestCount(em.GetBuffer<SourceSpawnRequestBuffer>(source));
                }

                Assert.That(pendingAfterFrame[0], Is.GreaterThan(0));
                Assert.That(pendingAfterFrame[1], Is.GreaterThanOrEqualTo(pendingAfterFrame[0]));
                Assert.That(pendingAfterFrame[2], Is.GreaterThanOrEqualTo(pendingAfterFrame[1]));
                Assert.That(pendingAfterFrame[2], Is.GreaterThan(pendingAfterFrame[0]));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnRequestBuild_PoissonEventRepeatCount_AccumulatesAsShotMultiples()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPoissonEventRepeatWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var playerEntity = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>()).GetSingletonEntity();
                if (em.HasComponent<PlayerGoSyncComponent>(playerEntity))
                    em.RemoveComponent<PlayerGoSyncComponent>(playerEntity);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));

                EnableV3Source(em, source, stableId: 19u, activeState: SourceStateId.Normal);
                const int poissonClipId = 1901;
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                var poissonPattern = CreateClipPattern(
                    directiveId: 7901,
                    clipId: poissonClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 10f,
                    ratePerSecPerArea: 0f);
                poissonPattern.EmissionMode = SourceSpawnEmissionModeId.Poisson;
                poissonPattern.MeanEventsPerSec = 3600f;
                poissonPattern.EventRepeatCount = 3;
                clipPatterns.Add(poissonPattern);

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = poissonClipId,
                    Weight = 1f
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = poissonClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                int pending = SumPendingRequestCount(em.GetBuffer<SourceSpawnRequestBuffer>(source));
                Assert.That(pending, Is.GreaterThan(0));
                Assert.That(pending % 3, Is.EqualTo(0), "Poisson pending shots should follow EventRepeatCount multiples.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_TimedUniformEvent_KeepsFixedWorldPositionAcrossFrames()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnTimedUniformAnchorWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, new float3(0f, 0f, 0f));
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(2f, 2f));

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8001,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.Poisson,
                    EventRepeatCount = 3,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    Count = 3,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.2f,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();
                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(1));
                float3 anchoredPosition = snapshots[0].Position;

                SetSourceAnchor(em, source, new float3(10f, 0f, 0f));

                world.SetTime(new TimeData(0.2d, 0.1f));
                simGroup.Update();
                snapshots.Clear();
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(1), "Timed interval should defer second shot until interval is reached.");

                world.SetTime(new TimeData(0.3d, 0.1f));
                simGroup.Update();
                snapshots.Clear();
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(2));

                int anchoredCount = 0;
                const float tolerance = 0.0001f;
                float tolSq = tolerance * tolerance;
                for (int i = 0; i < snapshots.Count; i++)
                {
                    float3 delta = snapshots[i].Position - anchoredPosition;
                    if (math.lengthsq(delta) <= tolSq)
                        anchoredCount++;
                }

                Assert.That(anchoredCount, Is.EqualTo(2), "Timed Uniform event should keep a fixed world anchor across shots.");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PlayerPositionAimEventStart_KeepsInitialAimTargetAcrossRepeats()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPlayerAimEventStartWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayerWithTransform(em, new float3(10f, 0f, 0f));
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8010,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.Poisson,
                    EventRepeatCount = 3,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.PlayerPosition,
                    AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                    AimAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 3,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.2f,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();
                SetPlayerPosition(em, new float3(0f, 0f, 10f));
                world.SetTime(new TimeData(0.2d, 0.1f));
                simGroup.Update();
                world.SetTime(new TimeData(0.3d, 0.1f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(2));

                float2 dirRight = new float2(1f, 0f);
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirRight, 0.0001f, 0.0001f), Is.EqualTo(2));

                var requestsAfter = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(requestsAfter.Length, Is.EqualTo(1));
                Assert.That(requestsAfter[0].EventAimInitialized, Is.EqualTo(1));
                Assert.That(requestsAfter[0].EventAimTargetPosition.x, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(requestsAfter[0].EventAimTargetPosition.z, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PlayerPositionAimPerShot_RetargetsAcrossRepeats()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPlayerAimPerShotWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayerWithTransform(em, new float3(10f, 0f, 0f));
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8011,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.Poisson,
                    EventRepeatCount = 3,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.PlayerPosition,
                    AimSnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                    AimAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 3,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.2f,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();
                SetPlayerPosition(em, new float3(0f, 0f, 10f));
                world.SetTime(new TimeData(0.2d, 0.1f));
                simGroup.Update();
                world.SetTime(new TimeData(0.3d, 0.1f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(2));

                float2 dirRight = new float2(1f, 0f);
                float2 dirUp = new float2(0f, 1f);
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));

                var requestsAfter = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(requestsAfter.Length, Is.EqualTo(1));
                Assert.That(requestsAfter[0].EventAimInitialized, Is.EqualTo(0));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PlayerPositionAimPerShotNWay_UsesCurrentPlayerForEachSet()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPlayerAimPerShotNWayWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayerWithTransform(em, new float3(10f, 0f, 0f));
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8012,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.Poisson,
                    EventRepeatCount = 2,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.PlayerPosition,
                    AimSnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                    AimAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.NWay,
                    ShotCount = 2,
                    NWayAngleSpacingDeg = 180f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 4,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.2f,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();
                SetPlayerPosition(em, new float3(0f, 0f, 10f));
                world.SetTime(new TimeData(0.2d, 0.1f));
                simGroup.Update();
                world.SetTime(new TimeData(0.3d, 0.1f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(4));

                float2 dirRight = new float2(1f, 0f);
                float2 dirLeft = new float2(-1f, 0f);
                float2 dirUp = new float2(0f, 1f);
                float2 dirDown = new float2(0f, -1f);
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirLeft, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirDown, 0.0001f, 0.0001f), Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PlayerPositionAimPerShotRadial_UsesCurrentPlayerForEachSet()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPlayerAimPerShotRadialWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayerWithTransform(em, new float3(10f, 0f, 0f));
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8013,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.Poisson,
                    EventRepeatCount = 2,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.PlayerPosition,
                    AimSnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                    AimAngleOffsetDeg = 0f,
                    ShotPatternMode = WaveShotPatternModeId.Radial,
                    ShotCount = 2,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    Count = 4,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.2f,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();
                SetPlayerPosition(em, new float3(0f, 0f, 10f));
                world.SetTime(new TimeData(0.2d, 0.1f));
                simGroup.Update();
                world.SetTime(new TimeData(0.3d, 0.1f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(4));

                float2 dirRight = new float2(1f, 0f);
                float2 dirLeft = new float2(-1f, 0f);
                float2 dirUp = new float2(0f, 1f);
                float2 dirDown = new float2(0f, -1f);
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirLeft, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, float3.zero, dirDown, 0.0001f, 0.0001f), Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_PollutionFieldSampling_UsesPollutionGridOriginInsteadOfSourceAnchor()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPollutionGridOriginWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(4f, 2f));

                em.SetComponentData(source, new SourcePollutionGridComponent
                {
                    CellSize = 1f,
                    InvCellSize = 1f,
                    HalfExtents = new float2(2f, 1f),
                    OriginX = -4f,
                    OriginZ = -1f,
                    Cols = 4,
                    Rows = 2,
                });

                var pollutionCells = em.GetBuffer<SourcePollutionCellBuffer>(source);
                pollutionCells.Clear();
                for (int i = 0; i < 8; i++)
                {
                    pollutionCells.Add(new SourcePollutionCellBuffer
                    {
                        Value = 1f,
                        IsValid = 0,
                        IsActive = 0,
                    });
                }

                pollutionCells[0] = new SourcePollutionCellBuffer
                {
                    Value = 1f,
                    IsValid = 1,
                    IsActive = 1,
                };

                var validCells = em.GetBuffer<SourcePollutionValidCellIndexBuffer>(source);
                validCells.Clear();
                validCells.Add(new SourcePollutionValidCellIndexBuffer { Value = 0 });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8003,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.PollutionTopK,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    SpawnSampleBudget = 1,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    Count = 1,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(1));

                float3 spawned = snapshots[0].Position;
                Assert.That(spawned.x, Is.GreaterThanOrEqualTo(-4f).And.LessThan(-3f));
                Assert.That(spawned.z, Is.GreaterThanOrEqualTo(-1f).And.LessThan(0f));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_UniformFieldSampling_SkipsInactiveCells()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnUniformSkipsInactiveWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(2f, 1f));

                em.SetComponentData(source, new SourcePollutionGridComponent
                {
                    CellSize = 1f,
                    InvCellSize = 1f,
                    HalfExtents = new float2(1f, 0.5f),
                    OriginX = -1f,
                    OriginZ = -0.5f,
                    Cols = 2,
                    Rows = 1,
                });

                var pollutionCells = em.GetBuffer<SourcePollutionCellBuffer>(source);
                pollutionCells.Clear();
                pollutionCells.Add(new SourcePollutionCellBuffer
                {
                    Value = 1f,
                    IsValid = 1,
                    IsActive = 0,
                });
                pollutionCells.Add(new SourcePollutionCellBuffer
                {
                    Value = 1f,
                    IsValid = 1,
                    IsActive = 1,
                });

                var validCells = em.GetBuffer<SourcePollutionValidCellIndexBuffer>(source);
                validCells.Clear();
                validCells.Add(new SourcePollutionValidCellIndexBuffer { Value = 0 });
                validCells.Add(new SourcePollutionValidCellIndexBuffer { Value = 1 });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8004,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    SpawnSampleBudget = 1,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    Count = 1,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(1));

                float3 spawned = snapshots[0].Position;
                Assert.That(spawned.x, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(spawned.z, Is.GreaterThanOrEqualTo(-0.5f).And.LessThan(0.5f));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_TimedLineEven_KeepsInitialCenterWhenSourceMoves()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnTimedLineEvenAnchorWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, float2.zero);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8002,
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.CenterPoint,
                    PositionPatternMode = WavePositionPatternModeId.LineEven,
                    AimMode = WaveAimModeId.Fixed,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    LineStart = new float2(-1f, 0f),
                    LineEnd = new float2(1f, 0f),
                    SampleSpacing = 2f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    Count = 2,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.2f,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                SetSourceAnchor(em, source, new float3(10f, 0f, 0f));

                world.SetTime(new TimeData(0.2d, 0.1f));
                simGroup.Update();
                world.SetTime(new TimeData(0.3d, 0.1f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(8);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(2));
                Assert.That(ContainsPosition(snapshots, new float3(-1f, 0f, 0f), 0.0001f), Is.True);
                Assert.That(ContainsPosition(snapshots, new float3(1f, 0f, 0f), 0.0001f), Is.True);
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void Smoke_MultiDirectiveSameTypeKey_DoesNotRegressBacklogAgeOrDrop()
        {
            try
            {
                using var world = CreateDefaultTestWorld("BulletSmokeStressMultiDirectiveWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 0f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 7000);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 7000, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var sourceEntity = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                EnableV3Source(em, sourceEntity, stableId: 13u, activeState: SourceStateId.Normal);
                const int multiDirectiveClipId = 1301;
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(sourceEntity);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 801,
                    clipId: multiDirectiveClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 1f,
                    ratePerSecPerArea: 450f));
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 802,
                    clipId: multiDirectiveClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 1f,
                    ratePerSecPerArea: 450f));

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(sourceEntity);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = multiDirectiveClipId,
                    Weight = 1f
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(sourceEntity);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = multiDirectiveClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u
                });

                int maxBudgetUsed = 0;
                int maxPending = 0;
                int maxOldestAge = 0;
                int droppedTotal = 0;
                int expiredTotal = 0;
                int spawnedFrames = 0;

                double elapsed = 0d;
                const double delta = 1d / 60d;
                for (int frame = 0; frame < 180; frame++)
                {
                    elapsed += delta;
                    world.SetTime(new TimeData(elapsed, (float)delta));
                    simGroup.Update();

                    if (frame == 0)
                    {
                        var requests = em.GetBuffer<SourceSpawnRequestBuffer>(sourceEntity);
                        Assert.That(HasDirective(requests, 801), Is.True);
                        Assert.That(HasDirective(requests, 802), Is.True);
                    }

                    if (!TryGetSingleton(em, out SpawnBacklogMetricsComponent metrics))
                        continue;
                    if (!TryGetSingleton(em, out BulletFrameCounterComponent frameCounter))
                        continue;

                    maxBudgetUsed = math.max(maxBudgetUsed, metrics.LastFrameBudgetUsed);
                    maxPending = math.max(maxPending, math.max(0, metrics.PendingCount));
                    droppedTotal = math.max(droppedTotal, metrics.DroppedByCapacity);
                    expiredTotal = math.max(expiredTotal, metrics.ExpiredByAge);
                    if (metrics.LastFrameBudgetUsed > 0)
                        spawnedFrames++;

                    int oldestAge = ComputeOldestBacklogAge(em, FrameSequenceUtility.GetCurrentFrame(in frameCounter));
                    maxOldestAge = math.max(maxOldestAge, oldestAge);
                }

                Assert.That(spawnedFrames, Is.GreaterThan(0));
                Assert.That(maxBudgetUsed, Is.GreaterThan(0));
                Assert.That(droppedTotal, Is.EqualTo(0));
                Assert.That(expiredTotal, Is.EqualTo(0));
                Assert.That(maxOldestAge, Is.LessThan(120));
                Assert.That(maxPending, Is.LessThan(32768));

                Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(sourceEntity).Length, Is.GreaterThan(0));
                foreach (var item in em.GetBuffer<SourceActiveBulletCountBuffer>(sourceEntity))
                    Assert.That(item.ActiveCount, Is.EqualTo(0));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void V3_EventHardPreemption_RemovesSustainPendingImmediately()
        {
            try
            {
                using var world = CreateDefaultTestWorld("V3HardPreemptionWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 8192, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                EnableV3Source(em, source, stableId: 101u, activeState: SourceStateId.Normal);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));

                var sourceData = em.GetComponentData<SourceSpawnComponent>(source);
                sourceData.State = SourceStateId.Weakened;
                em.SetComponentData(source, sourceData);

                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 9101,
                    clipId: 1101,
                    phase: SourceWavePhaseId.OnStateEnterOnce,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Weakened,
                    startSec: 0f,
                    endSec: 5f,
                    ratePerSecPerArea: 6f));

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = 2001,
                    ElapsedSec = 0.25f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u,
                });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8101,
                    Phase = SourceWavePhaseId.Sustain,
                    Lane = SourceSpawnLaneId.Hazard,
                    LanePriority = SourceSpawnLanePriorityUtility.ResolvePriority(SourceSpawnLaneId.Hazard),
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    LineStart = float2.zero,
                    LineEnd = float2.zero,
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    SpiralStepDeg = 0f,
                    SpawnSequence = 0u,
                    Count = 5,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d, 1f));
                simGroup.Update();

                var eventRuntime = em.GetComponentData<SourceEventRuntimeComponent>(source);
                Assert.That(eventRuntime.IsPlaying, Is.EqualTo(1), "Event clip must start immediately on state-trigger");

                var requestsAfter = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(HasPendingForPhase(requestsAfter, SourceWavePhaseId.Sustain), Is.False, "Hard preemption must remove pending sustain requests");
                Assert.That(HasPendingForPhase(requestsAfter, SourceWavePhaseId.OnStateEnterOnce), Is.True, "Event clip requests should remain pending");

                var laneAfter = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source)[0];
                Assert.That(laneAfter.ActiveClipId, Is.EqualTo(0), "Sustain lane must be interrupted while event clip is active");
                Assert.That(laneAfter.LastClipId, Is.EqualTo(2001), "Interrupted sustain clip should be retained as last clip for next selection");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void V3_FinishEnter_StartsEventClip_AndDoesNotRequeueWhileMaintained()
        {
            try
            {
                using var world = CreateDefaultTestWorld("V3FinishEnterEventWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 8192, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                EnableV3Source(em, source, stableId: 111u, activeState: SourceStateId.Normal);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));

                var sourceData = em.GetComponentData<SourceSpawnComponent>(source);
                sourceData.State = SourceStateId.Depleted;
                sourceData.CollectedCount = math.max(sourceData.CollectedCount, sourceData.ThresholdDepleted);
                em.SetComponentData(source, sourceData);

                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 9111,
                    clipId: 2111,
                    phase: SourceWavePhaseId.OnStateEnterOnce,
                    lane: SourceSpawnLaneId.Trash,
                    triggerState: SourceStateId.Depleted,
                    startSec: 0f,
                    endSec: 3f,
                    ratePerSecPerArea: 2f));
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 9112,
                    clipId: 2112,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Trash,
                    triggerState: SourceStateId.Depleted,
                    startSec: 0f,
                    endSec: 3f,
                    ratePerSecPerArea: 1f));

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Depleted,
                    Lane = SourceSpawnLaneId.Trash,
                    ClipId = 2112,
                    Weight = 1f,
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Trash,
                    ActiveClipId = 3001,
                    ElapsedSec = 0.25f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u,
                });
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = 3999,
                    ElapsedSec = 0.25f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u,
                });

                var sustainRuntime = em.GetComponentData<SourceSustainRuntimeComponent>(source);
                sustainRuntime.ActiveState = SourceStateId.Normal;
                em.SetComponentData(source, sustainRuntime);
                em.SetComponentData(source, new SourceEventRuntimeComponent
                {
                    IsPlaying = 0,
                    ActiveEventClipId = 0,
                    TriggerState = SourceStateId.Normal,
                    ElapsedSec = 0f,
                    SelectionSequence = 1u,
                });
                em.GetBuffer<SourceEventQueueBuffer>(source).Clear();

                world.SetTime(new TimeData(1d, 1f));
                simGroup.Update();

                var sustainAfter = em.GetComponentData<SourceSustainRuntimeComponent>(source);
                var eventAfter = em.GetComponentData<SourceEventRuntimeComponent>(source);
                Assert.That(sustainAfter.ActiveState, Is.EqualTo(SourceStateId.Depleted), "Finish entry must switch active clip-state to depleted");
                Assert.That(eventAfter.IsPlaying, Is.EqualTo(1), "Finish entry must start depleted event clip when available");
                Assert.That(eventAfter.TriggerState, Is.EqualTo(SourceStateId.Depleted));
                Assert.That(em.GetBuffer<SourceEventQueueBuffer>(source).Length, Is.EqualTo(0), "Entry event should be consumed immediately");

                world.SetTime(new TimeData(1.1d, 0.1f));
                simGroup.Update();

                Assert.That(em.GetBuffer<SourceEventQueueBuffer>(source).Length, Is.EqualTo(0), "Maintained finish state must not requeue duplicate entry events");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void V3_FinishEnter_DoesNotQueueDuplicate_WhenSameStateEventAlreadyPlaying()
        {
            try
            {
                using var world = CreateDefaultTestWorld("V3FinishDuplicateGuardWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 8192, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                EnableV3Source(em, source, stableId: 112u, activeState: SourceStateId.Normal);

                var sourceData = em.GetComponentData<SourceSpawnComponent>(source);
                sourceData.State = SourceStateId.Depleted;
                sourceData.CollectedCount = math.max(sourceData.CollectedCount, sourceData.ThresholdDepleted);
                em.SetComponentData(source, sourceData);

                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 9121,
                    clipId: 2121,
                    phase: SourceWavePhaseId.OnStateEnterOnce,
                    lane: SourceSpawnLaneId.Trash,
                    triggerState: SourceStateId.Depleted,
                    startSec: 0f,
                    endSec: 3f,
                    ratePerSecPerArea: 1f));

                var sustainRuntime = em.GetComponentData<SourceSustainRuntimeComponent>(source);
                sustainRuntime.ActiveState = SourceStateId.Normal;
                em.SetComponentData(source, sustainRuntime);
                em.SetComponentData(source, new SourceEventRuntimeComponent
                {
                    IsPlaying = 1,
                    ActiveEventClipId = 2121,
                    TriggerState = SourceStateId.Depleted,
                    ElapsedSec = 0.2f,
                    SelectionSequence = 7u,
                });
                var eventQueue = em.GetBuffer<SourceEventQueueBuffer>(source);
                eventQueue.Clear();

                world.SetTime(new TimeData(1d, 0.1f));
                simGroup.Update();

                var eventAfter = em.GetComponentData<SourceEventRuntimeComponent>(source);
                Assert.That(eventAfter.IsPlaying, Is.EqualTo(1));
                Assert.That(eventAfter.TriggerState, Is.EqualTo(SourceStateId.Depleted));
                Assert.That(em.GetBuffer<SourceEventQueueBuffer>(source).Length, Is.EqualTo(0), "State-enter queue must not duplicate while same-state event is already playing");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void V3_EventQueue_DuplicateTriggers_AreQueuedAndConsumedSequentially()
        {
            try
            {
                using var world = CreateDefaultTestWorld("V3EventQueueWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 8192, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                EnableV3Source(em, source, stableId: 102u, activeState: SourceStateId.Normal);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));

                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 9201,
                    clipId: 3101,
                    phase: SourceWavePhaseId.OnStateEnterOnce,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 0.5f,
                    ratePerSecPerArea: 2f));

                var eventQueue = em.GetBuffer<SourceEventQueueBuffer>(source);
                eventQueue.Clear();
                eventQueue.Add(new SourceEventQueueBuffer { TriggerState = SourceStateId.Normal, QueuedFrame = 0u });
                eventQueue.Add(new SourceEventQueueBuffer { TriggerState = SourceStateId.Normal, QueuedFrame = 0u });

                double elapsed = 0d;
                elapsed += 0.1d;
                world.SetTime(new TimeData(elapsed, 0.1f));
                simGroup.Update();

                Assert.That(em.GetBuffer<SourceEventQueueBuffer>(source).Length, Is.EqualTo(1), "One queued event should remain while first is playing");
                Assert.That(em.GetComponentData<SourceEventRuntimeComponent>(source).IsPlaying, Is.EqualTo(1));

                elapsed += 1.0d;
                world.SetTime(new TimeData(elapsed, 1.0f));
                simGroup.Update();

                Assert.That(em.GetBuffer<SourceEventQueueBuffer>(source).Length, Is.EqualTo(1), "Second queued event must remain queued until next start window");
                Assert.That(em.GetComponentData<SourceEventRuntimeComponent>(source).IsPlaying, Is.EqualTo(0), "First event should have ended");

                elapsed += 0.1d;
                world.SetTime(new TimeData(elapsed, 0.1f));
                simGroup.Update();

                Assert.That(em.GetBuffer<SourceEventQueueBuffer>(source).Length, Is.EqualTo(0), "Queued duplicate trigger must eventually be consumed");
                Assert.That(em.GetComponentData<SourceEventRuntimeComponent>(source).IsPlaying, Is.EqualTo(1), "Second queued event should start after first completes");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void V3_SustainChain_ExcludesLastClip_WhenAlternativeExists()
        {
            try
            {
                using var world = CreateDefaultTestWorld("V3SustainChainWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 8192, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                EnableV3Source(em, source, stableId: 103u, activeState: SourceStateId.Normal);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));

                const int firstClipId = 4001;
                const int secondClipId = 4002;
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 9301,
                    clipId: firstClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 1f,
                    ratePerSecPerArea: 0f));
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 9302,
                    clipId: secondClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 1f,
                    ratePerSecPerArea: 0f));

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = firstClipId,
                    Weight = 1f
                });
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = secondClipId,
                    Weight = 1f
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = 0,
                    ElapsedSec = 0f,
                    LastClipId = firstClipId,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                var laneAfter = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source)[0];
                Assert.That(laneAfter.ActiveClipId, Is.EqualTo(secondClipId), "When alternatives exist, sustain chain must exclude immediately previous clip");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SourceClipRequestBuild_BaselineScalesOnlyTrashRateField()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorBaselineScaleWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                var keepRunningSource = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));
                em.SetComponentData(source, new SourceSpawnComponent
                {
                    ThresholdWeakened = 1000,
                    ThresholdDepleted = 2000,
                    CollectedCount = 0,
                    State = SourceStateId.Normal,
                });
                em.SetComponentData(source, new SourceSustainRuntimeComponent
                {
                    ActiveState = SourceStateId.Normal,
                });
                em.SetComponentData(source, new SourceRunDirectorStateComponent
                {
                    State = RunDirectorSourceStateId.Baseline,
                    SelectedClipState = SourceStateId.Normal,
                    PressureOccupancySec = 0f,
                    DensityScale = 0.25f,
                    Version = 1u,
                });

                const int trashClipId = 7001;
                const int hazardClipId = 7002;
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 701,
                    clipId: trashClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Trash,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 10f,
                    ratePerSecPerArea: 10f));
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 702,
                    clipId: hazardClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Normal,
                    startSec: 0f,
                    endSec: 10f,
                    ratePerSecPerArea: 10f));

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Trash,
                    ClipId = trashClipId,
                    Weight = 1f,
                });
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Normal,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = hazardClipId,
                    Weight = 1f,
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Trash,
                    ActiveClipId = trashClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u,
                });
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = hazardClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u,
                });

                using (var playerSyncQuery = em.CreateEntityQuery(ComponentType.ReadWrite<PlayerGoSyncComponent>()))
                using (var playerSyncEntities = playerSyncQuery.ToEntityArray(Allocator.Temp))
                {
                    for (int i = 0; i < playerSyncEntities.Length; i++)
                    {
                        var sync = em.GetComponentData<PlayerGoSyncComponent>(playerSyncEntities[i]);
                        sync.Position = new float3(1024f, 0f, 1024f);
                        em.SetComponentData(playerSyncEntities[i], sync);
                    }
                }

                world.SetTime(new TimeData(1d, 1f));
                simGroup.Update();

                var directorAfter = em.GetComponentData<SourceRunDirectorStateComponent>(source);
                Assert.That(directorAfter.State, Is.EqualTo(RunDirectorSourceStateId.Baseline));
                int expectedTrash = (int)math.floor(10f * math.max(0f, directorAfter.DensityScale));

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(GetRequestCountByDirective(requests, 701), Is.EqualTo(expectedTrash), "Baseline must scale trash sustain rate-field density");
                Assert.That(GetRequestCountByDirective(requests, 702), Is.EqualTo(10), "Baseline must not scale hazard/event path");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SourceClipRequestBuild_FinishKeepsOnlyTrashSustainRequests()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorFinishTrashOnlyWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                var keepRunningSource = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(1f, 1f));
                em.SetComponentData(source, new SourceSpawnComponent
                {
                    ThresholdWeakened = 1000,
                    ThresholdDepleted = 2000,
                    CollectedCount = 2000,
                    State = SourceStateId.Depleted,
                });
                em.SetComponentData(source, new SourceSustainRuntimeComponent
                {
                    ActiveState = SourceStateId.Depleted,
                });
                em.SetComponentData(source, new SourceRunDirectorStateComponent
                {
                    State = RunDirectorSourceStateId.Finish,
                    SelectedClipState = SourceStateId.Depleted,
                    PressureOccupancySec = 0f,
                    DensityScale = 1f,
                    Version = 2u,
                });

                const int trashClipId = 7101;
                const int hazardClipId = 7102;
                var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
                clipPatterns.Clear();
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 711,
                    clipId: trashClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Trash,
                    triggerState: SourceStateId.Depleted,
                    startSec: 0f,
                    endSec: 10f,
                    ratePerSecPerArea: 4f));
                clipPatterns.Add(CreateClipPattern(
                    directiveId: 712,
                    clipId: hazardClipId,
                    phase: SourceWavePhaseId.Sustain,
                    lane: SourceSpawnLaneId.Hazard,
                    triggerState: SourceStateId.Depleted,
                    startSec: 0f,
                    endSec: 10f,
                    ratePerSecPerArea: 4f));

                var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
                sustainCandidates.Clear();
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Depleted,
                    Lane = SourceSpawnLaneId.Trash,
                    ClipId = trashClipId,
                    Weight = 1f,
                });
                sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
                {
                    State = SourceStateId.Depleted,
                    Lane = SourceSpawnLaneId.Hazard,
                    ClipId = hazardClipId,
                    Weight = 1f,
                });

                var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
                sustainLanes.Clear();
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Trash,
                    ActiveClipId = trashClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u,
                });
                sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
                {
                    Lane = SourceSpawnLaneId.Hazard,
                    ActiveClipId = hazardClipId,
                    ElapsedSec = 0f,
                    LastClipId = 0,
                    SelectionSequence = 1u,
                    LastMissingLogFrame = 0u,
                });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 7999,
                    Phase = SourceWavePhaseId.Sustain,
                    Lane = SourceSpawnLaneId.Hazard,
                    Count = 3,
                    OldestFrame = 1u,
                });

                em.SetComponentData(keepRunningSource, new SourceRunDirectorStateComponent
                {
                    State = RunDirectorSourceStateId.Baseline,
                    SelectedClipState = SourceStateId.Normal,
                    PressureOccupancySec = 0f,
                    DensityScale = 1f,
                    Version = 1u,
                });
                em.GetBuffer<SourceClipPatternBuffer>(keepRunningSource).Clear();
                em.GetBuffer<SourceSustainSlotCandidateBuffer>(keepRunningSource).Clear();
                em.GetBuffer<SourceSustainRuntimeLaneBuffer>(keepRunningSource).Clear();
                em.GetBuffer<SourceEventQueueBuffer>(keepRunningSource).Clear();
                em.GetBuffer<SourceSpawnRequestBuffer>(keepRunningSource).Clear();

                world.SetTime(new TimeData(1d, 1f));
                simGroup.Update();

                requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                Assert.That(GetRequestCountByDirective(requests, 711), Is.EqualTo(4), "Finish must keep trash sustain output");
                Assert.That(GetRequestCountByDirective(requests, 712), Is.EqualTo(0), "Finish must block non-trash sustain output");
                Assert.That(HasPendingSustainForLane(requests, SourceSpawnLaneId.Hazard), Is.False, "Finish must clear non-trash sustain backlog");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void RunDirector_PressureImmediateInside_AndReturnsBaselineAfterHold()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorImmediateAndReleaseWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                var directorConfigEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunProgressDirectorConfigComponent>()).GetSingletonEntity();
                em.SetComponentData(directorConfigEntity, new RunProgressDirectorConfigComponent
                {
                    PressureHoldSec = 0.5f,
                    BaselineTrashDensityScale = 0.4f,
                    PressureDensityScale = 1.0f,
                });

                em.SetComponentData(source, new SourceSpawnComponent
                {
                    ThresholdWeakened = 1000,
                    ThresholdDepleted = 2000,
                    CollectedCount = 0,
                    State = SourceStateId.Normal,
                });
                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Circle, 3f, float2.zero);
                em.SetComponentData(source, new SourceRunDirectorStateComponent
                {
                    State = RunDirectorSourceStateId.Baseline,
                    SelectedClipState = SourceStateId.Normal,
                    PressureOccupancySec = 0f,
                    DensityScale = 0.4f,
                    Version = 1u,
                });
                CreateSourceRegionGrid(em, em.GetComponentData<SourceStableIdComponent>(source).Value);

                SetPlayerPosition(em, float3.zero);
                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                var directorAfterEnter = em.GetComponentData<SourceRunDirectorStateComponent>(source);
                Assert.That(directorAfterEnter.State, Is.EqualTo(RunDirectorSourceStateId.Pressure), "Player entering source area should switch to Pressure immediately");

                CreateSourceRegionGrid(em, 0u);
                SetPlayerPosition(em, new float3(30f, 0f, 0f));
                world.SetTime(new TimeData(0.3d, 0.2f));
                simGroup.Update();

                var directorDuringHold = em.GetComponentData<SourceRunDirectorStateComponent>(source);
                Assert.That(directorDuringHold.State, Is.EqualTo(RunDirectorSourceStateId.Pressure), "After exiting source area, Pressure should remain during hold time");

                world.SetTime(new TimeData(0.7d, 0.4f));
                simGroup.Update();

                var directorAfterHold = em.GetComponentData<SourceRunDirectorStateComponent>(source);
                Assert.That(directorAfterHold.State, Is.EqualTo(RunDirectorSourceStateId.Baseline), "Pressure should return to Baseline after hold timer expires");
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void RunDirectorStage_IdleToRunning_RequiresMinIdleAndIntroDone()
        {
            using var world = new World("RunDirectorStageIdleToRunningContractWorld");
            var em = world.EntityManager;

            var frameEntity = em.CreateEntity(typeof(BulletFrameCounterComponent));
            em.SetComponentData(frameEntity, new BulletFrameCounterComponent { Value = 0u });

            var stageStateEntity = em.CreateEntity(typeof(RunDirectorStageStateComponent));
            em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
                StateElapsedSec = 0.4f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });

            var topologyStateEntity = em.CreateEntity(typeof(StageTopologyStateComponent));
            em.SetComponentData(topologyStateEntity, default(StageTopologyStateComponent));

            var gateEntity = em.CreateEntity(typeof(RunDirectorStageGateComponent));
            em.SetComponentData(gateEntity, new RunDirectorStageGateComponent
            {
                IntroPresentationDone = 0,
                ClearPresentationDone = 1,
                MinIdleDurationElapsed = 1,
                AutoAdvanceTimeoutElapsed = 0,
            });

            var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
            em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
            {
                StageStartRequested = 1,
                ConfirmPressed = 0,
                ForceClearReadyRequested = 0,
            });

            var signalEntity = em.CreateEntity(typeof(RunDirectorStageSignalComponent));
            em.SetComponentData(signalEntity, default(RunDirectorStageSignalComponent));

            var system = world.GetOrCreateSystem<RunDirectorStageTransitionSystem>();
            system.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity).State, Is.EqualTo(RunDirectorStageStateId.Idle));

            var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);
            gate.IntroPresentationDone = 1;
            em.SetComponentData(gateEntity, gate);

            system.Update(world.Unmanaged);

            var stageAfterRun = em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity);
            var requestAfterRun = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
            Assert.That(stageAfterRun.State, Is.EqualTo(RunDirectorStageStateId.Running));
            Assert.That(stageAfterRun.LastTransitionReason, Is.EqualTo(RunDirectorStageTransitionReasonId.StartRequested));
            Assert.That(requestAfterRun.StageStartRequested, Is.EqualTo(0));
        }

        [Test]
        public void RunDirectorStage_ClearReadyToCompleted_RequiresClearPresentationDone()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorStageClearReadyConfirmWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                var stageStateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageStateComponent>()).GetSingletonEntity();
                em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.ClearReady,
                    StateElapsedSec = 0f,
                    EnteredFrame = 0u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.None,
                });

                var gateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageGateComponent>()).GetSingletonEntity();
                em.SetComponentData(gateEntity, new RunDirectorStageGateComponent
                {
                    IntroPresentationDone = 1,
                    ClearPresentationDone = 0,
                    MinIdleDurationElapsed = 0,
                    AutoAdvanceTimeoutElapsed = 0,
                });

                var requestEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageRequestComponent>()).GetSingletonEntity();
                em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
                {
                    StageStartRequested = 0,
                    ConfirmPressed = 1,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();
                Assert.That(em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity).State, Is.EqualTo(RunDirectorStageStateId.ClearReady));

                var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);
                gate.ClearPresentationDone = 1;
                em.SetComponentData(gateEntity, gate);

                world.SetTime(new TimeData(0.2d, 0.1f));
                simGroup.Update();

                var completed = em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity);
                var signal = em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageSignalComponent>()).GetSingleton<RunDirectorStageSignalComponent>();
                Assert.That(completed.State, Is.EqualTo(RunDirectorStageStateId.Completed));
                Assert.That(completed.LastTransitionReason, Is.EqualTo(RunDirectorStageTransitionReasonId.ConfirmPressed));
                Assert.That(signal.StageRunCompleted, Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void RunDirectorStage_RunningToClearReady_AllowsDebugForceRequest()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorStageForceClearReadyWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                var stageStateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageStateComponent>()).GetSingletonEntity();
                em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Running,
                    StateElapsedSec = 0.3f,
                    EnteredFrame = 9u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.StartRequested,
                });

                var requestEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageRequestComponent>()).GetSingletonEntity();
                em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
                {
                    StageStartRequested = 0,
                    ConfirmPressed = 1,
                    ForceClearReadyRequested = 1,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                var stage = em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity);
                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(stage.State, Is.EqualTo(RunDirectorStageStateId.ClearReady));
                Assert.That(stage.LastTransitionReason, Is.EqualTo(RunDirectorStageTransitionReasonId.DebugForceClearReady));
                Assert.That(request.ForceClearReadyRequested, Is.EqualTo(0));
                Assert.That(request.ConfirmPressed, Is.EqualTo(0));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void RunDirectorStage_ClearReadyToCompleted_AllowsAutoAdvanceTimeout()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorStageClearReadyTimeoutWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                SetFixedTickEnabled(em, enabled: false);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                // Flush initial boot reset before seeding the stage-start request under test.
                world.SetTime(new TimeData(0d, 0f));
                simGroup.Update();

                var stageConfigEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageConfigComponent>()).GetSingletonEntity();
                em.SetComponentData(stageConfigEntity, new RunDirectorStageConfigComponent
                {
                    InitialState = RunDirectorStageStateId.ClearReady,
                    MinIdleDurationSec = 0f,
                    ClearAutoAdvanceTimeoutSec = 0.25f,
                });

                var stageStateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageStateComponent>()).GetSingletonEntity();
                em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.ClearReady,
                    StateElapsedSec = 0f,
                    EnteredFrame = 0u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.None,
                });

                var gateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageGateComponent>()).GetSingletonEntity();
                em.SetComponentData(gateEntity, new RunDirectorStageGateComponent
                {
                    IntroPresentationDone = 1,
                    ClearPresentationDone = 1,
                    MinIdleDurationElapsed = 0,
                    AutoAdvanceTimeoutElapsed = 0,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();
                Assert.That(em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity).State, Is.EqualTo(RunDirectorStageStateId.ClearReady));

                world.SetTime(new TimeData(0.4d, 0.3f));
                simGroup.Update();

                var completed = em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity);
                Assert.That(completed.State, Is.EqualTo(RunDirectorStageStateId.Completed));
                Assert.That(completed.LastTransitionReason, Is.EqualTo(RunDirectorStageTransitionReasonId.AutoAdvanceTimeout));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void RunDirector_DoesNotSelectPressure_WhenStageIsNotRunning()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorStageGateWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                var stageStateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageStateComponent>()).GetSingletonEntity();
                em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                    StateElapsedSec = 0f,
                    EnteredFrame = 0u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.None,
                });

                SetSourceAnchor(em, source, float3.zero);
                SetSourceShape(em, source, Shape2DKind.Circle, 3f, float2.zero);
                em.SetComponentData(source, new SourceRunDirectorStateComponent
                {
                    State = RunDirectorSourceStateId.Baseline,
                    SelectedClipState = SourceStateId.Normal,
                    PressureOccupancySec = 0f,
                    DensityScale = 1f,
                    Version = 1u,
                });

                SetPlayerPosition(em, float3.zero);
                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                var director = em.GetComponentData<SourceRunDirectorStateComponent>(source);
                Assert.That(director.State, Is.EqualTo(RunDirectorSourceStateId.Baseline));
                Assert.That(director.PressureOccupancySec, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void ReplayInput_RecordAndPlayback_RoundTripsFrameSnapshots()
        {
            using var world = CreateDefaultTestWorld("ReplayRecordPlaybackWorld", out var simGroup);
            var em = world.EntityManager;
            CreatePlayer(em);

            var replayEntity = em.CreateEntityQuery(
                ComponentType.ReadWrite<ReplayInputControlComponent>(),
                ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
            var frameCounterEntity = em.CreateEntityQuery(ComponentType.ReadWrite<BulletFrameCounterComponent>()).GetSingletonEntity();
            var playerEntity = em.CreateEntityQuery(ComponentType.ReadWrite<PlayerGoSyncComponent>(), ComponentType.ReadOnly<PlayerTag>()).GetSingletonEntity();

            var replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
            replayFrames.Clear();
            em.SetComponentData(replayEntity, new ReplayInputCursorComponent { NextFrameIndex = 0 });
            em.SetComponentData(replayEntity, new ReplayInputControlComponent
            {
                Mode = ReplayInputModeId.Record,
                LastRecordedFrame = 0u,
                LastPlaybackFrame = 0u,
                MissingFrameCount = 0,
            });

            var expected = new List<ReplayInputFrameBufferElement>();
            for (int i = 0; i < 6; i++)
            {
                uint frame = (uint)(i + 1);
                var sync = new PlayerGoSyncComponent
                {
                    Position = new float3(i, 0f, i * 2f),
                    Rotation = quaternion.RotateY(math.radians(i * 15f)),
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                };
                var intent = new PlayerInputIntentComponent
                {
                    MoveAxis = math.normalizesafe(new float2(i + 1f, i + 2f), float2.zero),
                    AimWorldXZ = new float2(i * 10f, i * 10f + 1f),
                    HasAimWorldPoint = 1,
                    VacuumRequested = (byte)(i % 2),
                    CleanupActionRequested = (byte)((i + 1) % 2),
                    RequestedCleanupActionSlot = (byte)(i % 3),
                    Sequence = (uint)(100 + i),
                };
                expected.Add(new ReplayInputFrameBufferElement
                {
                    Frame = frame,
                    MoveAxis = intent.MoveAxis,
                    AimWorldXZ = intent.AimWorldXZ,
                    HasAimWorldPoint = intent.HasAimWorldPoint,
                    Position = sync.Position,
                    Rotation = sync.Rotation,
                    SyncRotation = sync.SyncRotation,
                    VacuumRequested = intent.VacuumRequested,
                    CleanupActionRequested = intent.CleanupActionRequested,
                    RequestedCleanupActionSlot = intent.RequestedCleanupActionSlot,
                    InputSequence = intent.Sequence,
                });

                em.SetComponentData(playerEntity, sync);
                em.SetComponentData(playerEntity, intent);
                world.SetTime(new TimeData(i + 1d, 1f));
                simGroup.Update();
            }

            replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
            Assert.That(replayFrames.Length, Is.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(replayFrames[i].Frame, Is.EqualTo(expected[i].Frame));
                Assert.That(replayFrames[i].MoveAxis, Is.EqualTo(expected[i].MoveAxis));
                Assert.That(replayFrames[i].AimWorldXZ, Is.EqualTo(expected[i].AimWorldXZ));
                Assert.That(replayFrames[i].HasAimWorldPoint, Is.EqualTo(expected[i].HasAimWorldPoint));
                Assert.That(replayFrames[i].Position, Is.EqualTo(expected[i].Position));
                Assert.That(replayFrames[i].SyncRotation, Is.EqualTo(expected[i].SyncRotation));
                Assert.That(replayFrames[i].VacuumRequested, Is.EqualTo(expected[i].VacuumRequested));
                Assert.That(replayFrames[i].CleanupActionRequested, Is.EqualTo(expected[i].CleanupActionRequested));
                Assert.That(replayFrames[i].RequestedCleanupActionSlot, Is.EqualTo(expected[i].RequestedCleanupActionSlot));
                Assert.That(replayFrames[i].InputSequence, Is.EqualTo(expected[i].InputSequence));
            }

            em.SetComponentData(replayEntity, new ReplayInputCursorComponent { NextFrameIndex = 0 });
            em.SetComponentData(replayEntity, new ReplayInputControlComponent
            {
                Mode = ReplayInputModeId.Playback,
                LastRecordedFrame = 0u,
                LastPlaybackFrame = 0u,
                MissingFrameCount = 0,
            });
            em.SetComponentData(frameCounterEntity, new BulletFrameCounterComponent { Value = 0u });

            for (int i = 0; i < expected.Count; i++)
            {
                em.SetComponentData(playerEntity, new PlayerGoSyncComponent
                {
                    Position = new float3(-99f, 0f, -99f),
                    Rotation = quaternion.identity,
                    SyncRotation = 0,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                });
                em.SetComponentData(playerEntity, new PlayerInputIntentComponent
                {
                    MoveAxis = float2.zero,
                    AimWorldXZ = float2.zero,
                    HasAimWorldPoint = 0,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    Sequence = 0u,
                });
                world.SetTime(new TimeData(100d + i, 1f));
                simGroup.Update();

                var replayed = em.GetComponentData<PlayerGoSyncComponent>(playerEntity);
                var replayedIntent = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
                Assert.That(replayed.Position, Is.EqualTo(expected[i].Position));
                Assert.That(replayed.SyncRotation, Is.EqualTo(expected[i].SyncRotation));
                Assert.That(replayed.VacuumRequested, Is.EqualTo(expected[i].VacuumRequested));
                Assert.That(replayed.CleanupActionRequested, Is.EqualTo(expected[i].CleanupActionRequested));
                Assert.That(replayed.RequestedCleanupActionSlot, Is.EqualTo(expected[i].RequestedCleanupActionSlot));
                Assert.That(replayedIntent.MoveAxis, Is.EqualTo(expected[i].MoveAxis));
                Assert.That(replayedIntent.AimWorldXZ, Is.EqualTo(expected[i].AimWorldXZ));
                Assert.That(replayedIntent.HasAimWorldPoint, Is.EqualTo(expected[i].HasAimWorldPoint));
                Assert.That(replayedIntent.VacuumRequested, Is.EqualTo(expected[i].VacuumRequested));
                Assert.That(replayedIntent.CleanupActionRequested, Is.EqualTo(expected[i].CleanupActionRequested));
                Assert.That(replayedIntent.RequestedCleanupActionSlot, Is.EqualTo(expected[i].RequestedCleanupActionSlot));
                Assert.That(replayedIntent.Sequence, Is.EqualTo(expected[i].InputSequence));
            }

            var cursorAfter = em.GetComponentData<ReplayInputCursorComponent>(replayEntity);
            var controlAfter = em.GetComponentData<ReplayInputControlComponent>(replayEntity);
            Assert.That(cursorAfter.NextFrameIndex, Is.EqualTo(expected.Count));
            Assert.That(controlAfter.MissingFrameCount, Is.EqualTo(0));
        }

        [Test]
        public void ReplayInputQueue_CapturesAndConsumesAtCurrentTick()
        {
            using var world = CreateDefaultTestWorld("ReplayInputQueueCaptureConsumeWorld", out var simGroup);
            var em = world.EntityManager;
            CreatePlayer(em);

            var replayEntity = em.CreateEntityQuery(
                ComponentType.ReadWrite<ReplayInputControlComponent>(),
                ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                ComponentType.ReadWrite<ReplayInputFrameBufferElement>(),
                ComponentType.ReadWrite<ReplayTickInputQueueStateComponent>(),
                ComponentType.ReadWrite<ReplayTickInputQueueBufferElement>()).GetSingletonEntity();
            var frameCounterEntity = em.CreateEntityQuery(ComponentType.ReadWrite<BulletFrameCounterComponent>()).GetSingletonEntity();
            var playerEntity = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadWrite<PlayerGoSyncComponent>(),
                ComponentType.ReadWrite<PlayerInputIntentComponent>()).GetSingletonEntity();

            em.SetComponentData(replayEntity, new ReplayInputControlComponent
            {
                Mode = ReplayInputModeId.Off,
                LastRecordedFrame = 0u,
                LastPlaybackFrame = 0u,
                MissingFrameCount = 0,
            });

            em.SetComponentData(playerEntity, new PlayerInputIntentComponent
            {
                MoveAxis = new float2(0.25f, 0.75f),
                AimWorldXZ = new float2(9f, 1f),
                HasAimWorldPoint = 1,
                VacuumRequested = 1,
                CleanupActionRequested = 1,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.Primary,
                Sequence = 10u,
            });
            em.SetComponentData(frameCounterEntity, new BulletFrameCounterComponent { Value = 3u });

            world.SetTime(new TimeData(1d, 1f / 60f));
            simGroup.Update();

            var queueState = em.GetComponentData<ReplayTickInputQueueStateComponent>(replayEntity);
            var queue = em.GetBuffer<ReplayTickInputQueueBufferElement>(replayEntity);
            var intentAfter = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);

            Assert.That(queueState.LastEnqueuedTick, Is.EqualTo(4u));
            Assert.That(queueState.LastConsumedTick, Is.EqualTo(4u));
            Assert.That(queueState.LastEnqueuedSequence, Is.EqualTo(10u));
            Assert.That(queueState.LastConsumedSequence, Is.EqualTo(10u));
            Assert.That(queueState.PendingCount, Is.EqualTo(0));
            Assert.That(queue.Length, Is.EqualTo(0));
            Assert.That(intentAfter.MoveAxis, Is.EqualTo(new float2(0.25f, 0.75f)));
            Assert.That(intentAfter.VacuumRequested, Is.EqualTo(1));
            Assert.That(intentAfter.CleanupActionRequested, Is.EqualTo(1));
            Assert.That(intentAfter.RequestedCleanupActionSlot, Is.EqualTo((byte)PlayerCleanupActionSlotId.Primary));
            Assert.That(intentAfter.Sequence, Is.EqualTo(10u));
        }

        [Test]
        public void ReplayInputQueue_DeduplicatesOneShot_WhenSameTickAndSequenceReconsumed()
        {
            using var world = CreateDefaultTestWorld("ReplayInputQueueDedupWorld", out var simGroup);
            var em = world.EntityManager;
            CreatePlayer(em);

            var replayEntity = em.CreateEntityQuery(
                ComponentType.ReadWrite<ReplayInputControlComponent>(),
                ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                ComponentType.ReadWrite<ReplayInputFrameBufferElement>(),
                ComponentType.ReadWrite<ReplayTickInputQueueStateComponent>(),
                ComponentType.ReadWrite<ReplayTickInputQueueBufferElement>()).GetSingletonEntity();
            var frameCounterEntity = em.CreateEntityQuery(ComponentType.ReadWrite<BulletFrameCounterComponent>()).GetSingletonEntity();
            var playerEntity = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadWrite<PlayerGoSyncComponent>(),
                ComponentType.ReadWrite<PlayerInputIntentComponent>()).GetSingletonEntity();

            em.SetComponentData(replayEntity, new ReplayInputControlComponent
            {
                Mode = ReplayInputModeId.Off,
                LastRecordedFrame = 0u,
                LastPlaybackFrame = 0u,
                MissingFrameCount = 0,
            });

            var duplicatedOneShot = new PlayerInputIntentComponent
            {
                MoveAxis = new float2(1f, 0f),
                AimWorldXZ = new float2(4f, 0f),
                HasAimWorldPoint = 1,
                VacuumRequested = 1,
                CleanupActionRequested = 1,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.Secondary,
                Sequence = 77u,
            };

            em.SetComponentData(frameCounterEntity, new BulletFrameCounterComponent { Value = 9u });
            em.SetComponentData(playerEntity, duplicatedOneShot);
            world.SetTime(new TimeData(1d, 1f / 60f));
            simGroup.Update();

            em.SetComponentData(frameCounterEntity, new BulletFrameCounterComponent { Value = 9u });
            em.SetComponentData(playerEntity, duplicatedOneShot);
            world.SetTime(new TimeData(2d, 1f / 60f));
            simGroup.Update();

            var intentAfterSecond = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
            var queueState = em.GetComponentData<ReplayTickInputQueueStateComponent>(replayEntity);
            Assert.That(queueState.LastConsumedTick, Is.EqualTo(10u));
            Assert.That(queueState.LastConsumedSequence, Is.EqualTo(77u));
            Assert.That(intentAfterSecond.VacuumRequested, Is.EqualTo(0));
            Assert.That(intentAfterSecond.CleanupActionRequested, Is.EqualTo(0));
            Assert.That(intentAfterSecond.RequestedCleanupActionSlot, Is.EqualTo((byte)PlayerCleanupActionSlotId.None));
            Assert.That(intentAfterSecond.Sequence, Is.EqualTo(77u));
        }

        [Test]
        public void ReplayInput_PlaybackWithLocalTransform_KeepsSnapshotPoseUnderVariableDeltaTime()
        {
            using var world = CreateDefaultTestWorld("ReplayPlaybackLocalTransformWorld", out var simGroup);
            var em = world.EntityManager;
            CreatePlayerWithTransform(em, float3.zero);
            CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 64, maxPendingAgeFrames: 30);
            SetFixedTickEnabled(em, enabled: false);

            var replayEntity = em.CreateEntityQuery(
                ComponentType.ReadWrite<ReplayInputControlComponent>(),
                ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
            var frameCounterEntity = em.CreateEntityQuery(ComponentType.ReadWrite<BulletFrameCounterComponent>()).GetSingletonEntity();
            var playerEntity = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<PlayerGoSyncComponent>(),
                ComponentType.ReadWrite<PlayerInputIntentComponent>()).GetSingletonEntity();

            var expected = new List<ReplayInputFrameBufferElement>
            {
                new ReplayInputFrameBufferElement
                {
                    Frame = 1u,
                    MoveAxis = new float2(1f, 0f),
                    AimWorldXZ = new float2(5f, 0f),
                    HasAimWorldPoint = 1,
                    Position = new float3(0.5f, 0f, 1.5f),
                    Rotation = quaternion.RotateY(math.radians(15f)),
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    InputSequence = 10u,
                },
                new ReplayInputFrameBufferElement
                {
                    Frame = 2u,
                    MoveAxis = new float2(0.7f, -0.2f),
                    AimWorldXZ = new float2(6f, 2f),
                    HasAimWorldPoint = 1,
                    Position = new float3(1.2f, 0f, 2.6f),
                    Rotation = quaternion.RotateY(math.radians(32f)),
                    SyncRotation = 1,
                    VacuumRequested = 1,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    InputSequence = 11u,
                },
                new ReplayInputFrameBufferElement
                {
                    Frame = 3u,
                    MoveAxis = new float2(-0.3f, 0.9f),
                    AimWorldXZ = new float2(2f, 7f),
                    HasAimWorldPoint = 1,
                    Position = new float3(2.1f, 0f, 1.8f),
                    Rotation = quaternion.RotateY(math.radians(78f)),
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 1,
                    RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.Primary,
                    InputSequence = 12u,
                },
                new ReplayInputFrameBufferElement
                {
                    Frame = 4u,
                    MoveAxis = new float2(0.4f, 0.4f),
                    AimWorldXZ = new float2(-1f, 9f),
                    HasAimWorldPoint = 1,
                    Position = new float3(2.9f, 0f, 3.3f),
                    Rotation = quaternion.RotateY(math.radians(121f)),
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    InputSequence = 13u,
                },
                new ReplayInputFrameBufferElement
                {
                    Frame = 5u,
                    MoveAxis = new float2(-1f, 0.1f),
                    AimWorldXZ = new float2(-4f, 3f),
                    HasAimWorldPoint = 1,
                    Position = new float3(4.2f, 0f, 2.7f),
                    Rotation = quaternion.RotateY(math.radians(170f)),
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    InputSequence = 14u,
                },
            };

            var replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
            replayFrames.Clear();
            for (int i = 0; i < expected.Count; i++)
                replayFrames.Add(expected[i]);

            em.SetComponentData(replayEntity, new ReplayInputCursorComponent { NextFrameIndex = 0 });
            em.SetComponentData(replayEntity, new ReplayInputControlComponent
            {
                Mode = ReplayInputModeId.Playback,
                LastRecordedFrame = 0u,
                LastPlaybackFrame = 0u,
                MissingFrameCount = 0,
            });

            float[] deltas = { 1f / 30f, 1f / 120f, 1f / 24f, 1f / 90f, 1f / 55f };
            double elapsed = 0d;
            for (int i = 0; i < expected.Count; i++)
            {
                em.SetComponentData(playerEntity, LocalTransform.FromPositionRotationScale(
                    new float3(-100f - i, 0f, -100f - i),
                    quaternion.identity,
                    1f));
                em.SetComponentData(playerEntity, new PlayerGoSyncComponent
                {
                    Position = new float3(999f + i, 0f, 999f + i),
                    Rotation = quaternion.identity,
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                });
                elapsed += deltas[i];
                world.SetTime(new TimeData(elapsed, deltas[i]));
                simGroup.Update();

                var sync = em.GetComponentData<PlayerGoSyncComponent>(playerEntity);
                var tx = em.GetComponentData<LocalTransform>(playerEntity);
                var intent = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);

                Assert.That(sync.Position, Is.EqualTo(expected[i].Position), $"sync position mismatch at frame={i}");
                Assert.That(tx.Position, Is.EqualTo(expected[i].Position), $"transform position mismatch at frame={i}");
                Assert.That(sync.Rotation.value, Is.EqualTo(expected[i].Rotation.value), $"sync rotation mismatch at frame={i}");
                Assert.That(tx.Rotation.value, Is.EqualTo(expected[i].Rotation.value), $"transform rotation mismatch at frame={i}");
                Assert.That(intent.Sequence, Is.EqualTo(expected[i].InputSequence), $"intent sequence mismatch at frame={i}");
            }

            var cursorAfter = em.GetComponentData<ReplayInputCursorComponent>(replayEntity);
            Assert.That(cursorAfter.NextFrameIndex, Is.EqualTo(expected.Count));
        }

        [Test]
        public void ReplayInput_StagedPlayback_ResetsFrameAndAppliesSeedAndFrames()
        {
            using var world = CreateDefaultTestWorld("ReplayStagedPlaybackWorld", out var simGroup);
            var em = world.EntityManager;
            CreatePlayer(em);
            CreateConfigSingletons(em, budgetPerFrame: 1, maxPendingCount: 64, maxPendingAgeFrames: 30);

            var replayEntity = em.CreateEntityQuery(
                ComponentType.ReadWrite<ReplayInputControlComponent>(),
                ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
            var frameCounterEntity = em.CreateEntityQuery(ComponentType.ReadWrite<BulletFrameCounterComponent>()).GetSingletonEntity();
            var runSeedEntity = em.CreateEntityQuery(ComponentType.ReadWrite<SpawnRunSeedComponent>()).GetSingletonEntity();
            var playerEntity = em.CreateEntityQuery(
                ComponentType.ReadWrite<PlayerInputIntentComponent>(),
                ComponentType.ReadOnly<PlayerTag>()).GetSingletonEntity();

            em.SetComponentData(frameCounterEntity, new BulletFrameCounterComponent { Value = 99u });
            em.SetComponentData(runSeedEntity, new SpawnRunSeedComponent { Value = 7u });
            em.SetComponentData(replayEntity, new ReplayInputControlComponent
            {
                Mode = ReplayInputModeId.Off,
                LastRecordedFrame = 0u,
                LastPlaybackFrame = 0u,
                MissingFrameCount = 0,
            });

            var staged = new List<ReplayInputFrameBufferElement>
            {
                new ReplayInputFrameBufferElement
                {
                    Frame = 1u,
                    MoveAxis = new float2(0.25f, 0.75f),
                    AimWorldXZ = new float2(9f, 11f),
                    HasAimWorldPoint = 1,
                    Position = new float3(1f, 0f, 2f),
                    Rotation = quaternion.identity,
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    InputSequence = 10u,
                },
                new ReplayInputFrameBufferElement
                {
                    Frame = 2u,
                    MoveAxis = new float2(-1f, 0f),
                    AimWorldXZ = new float2(-3f, 5f),
                    HasAimWorldPoint = 1,
                    Position = new float3(3f, 0f, 4f),
                    Rotation = quaternion.identity,
                    SyncRotation = 1,
                    VacuumRequested = 1,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    InputSequence = 11u,
                }
            };

            ReplaySessionStaging.StagePlayback(staged, runSeed: 0x1234u);
            world.SetTime(new TimeData(1d, 1f));
            simGroup.Update();

            var controlAfter = em.GetComponentData<ReplayInputControlComponent>(replayEntity);
            var cursorAfter = em.GetComponentData<ReplayInputCursorComponent>(replayEntity);
            var framesAfter = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
            var frameAfter = em.GetComponentData<BulletFrameCounterComponent>(frameCounterEntity);
            var seedAfter = em.GetComponentData<SpawnRunSeedComponent>(runSeedEntity);
            var playerIntentAfter = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);

            Assert.That(controlAfter.Mode, Is.EqualTo(ReplayInputModeId.Playback));
            Assert.That(cursorAfter.NextFrameIndex, Is.EqualTo(1), "First staged replay tick should be consumed on first update");
            Assert.That(framesAfter.Length, Is.EqualTo(2));
            Assert.That(frameAfter.Value, Is.EqualTo(1u));
            Assert.That(seedAfter.Value, Is.EqualTo(0x1234u));
            Assert.That(playerIntentAfter.MoveAxis, Is.EqualTo(staged[0].MoveAxis));
            Assert.That(playerIntentAfter.AimWorldXZ, Is.EqualTo(staged[0].AimWorldXZ));
            Assert.That(playerIntentAfter.HasAimWorldPoint, Is.EqualTo(staged[0].HasAimWorldPoint));
            Assert.That(playerIntentAfter.Sequence, Is.EqualTo(staged[0].InputSequence));
            Assert.That(ReplaySessionStaging.IsPlaybackStartupPending, Is.False);
        }

        [Test]
        public void Determinism_SameSeedAndReplayInput_ProducesSameSpawnSnapshot()
        {
            const int replayFrameCount = 24;
            const int totalFrames = 24;

            List<ActiveBulletSnapshot> RunScenario(string worldName)
            {
                using var world = CreateDefaultTestWorld(worldName, out var simGroup);
                var em = world.EntityManager;
                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 8f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 256, lifetime: 8f);
                CreatePlayerWithTransform(em, float3.zero);
                CreateConfigSingletons(em, budgetPerFrame: 256, maxPendingCount: 4096, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 7101,
                    Phase = SourceWavePhaseId.Sustain,
                    Lane = SourceSpawnLaneId.Hazard,
                    LanePriority = SourceSpawnLanePriorityUtility.ResolvePriority(SourceSpawnLaneId.Hazard),
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    SpawnSampleBudget = 8,
                    Count = 60,
                    OldestFrame = 0u,
                });

                var replayEntity = em.CreateEntityQuery(
                    ComponentType.ReadWrite<ReplayInputControlComponent>(),
                    ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                    ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
                var replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
                replayFrames.Clear();
                for (int i = 0; i < replayFrameCount; i++)
                {
                    replayFrames.Add(new ReplayInputFrameBufferElement
                    {
                        Frame = (uint)(i + 1),
                        Position = new float3(0f, 0f, 0f),
                        Rotation = quaternion.identity,
                        SyncRotation = 0,
                        VacuumRequested = 0,
                        CleanupActionRequested = 0,
                        RequestedCleanupActionSlot = 0,
                    });
                }

                em.SetComponentData(replayEntity, new ReplayInputCursorComponent { NextFrameIndex = 0 });
                em.SetComponentData(replayEntity, new ReplayInputControlComponent
                {
                    Mode = ReplayInputModeId.Playback,
                    LastRecordedFrame = 0u,
                    LastPlaybackFrame = 0u,
                    MissingFrameCount = 0,
                });

                for (int i = 0; i < totalFrames; i++)
                {
                    double elapsed = (i + 1d) / 60d;
                    world.SetTime(new TimeData(elapsed, 1f / 60f));
                    simGroup.Update();
                }

                var snapshots = new List<ActiveBulletSnapshot>();
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                snapshots.Sort((a, b) =>
                {
                    int cx = a.Position.x.CompareTo(b.Position.x);
                    if (cx != 0)
                        return cx;
                    int cy = a.Position.y.CompareTo(b.Position.y);
                    if (cy != 0)
                        return cy;
                    return a.Position.z.CompareTo(b.Position.z);
                });

                ForceDisposeSharedContainersIfNeeded();
                return snapshots;
            }

            var first = RunScenario("DeterminismReplayWorld_A");
            var second = RunScenario("DeterminismReplayWorld_B");

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                float3 delta = first[i].Position - second[i].Position;
                Assert.That(math.lengthsq(delta), Is.LessThanOrEqualTo(1e-6f), $"spawn position mismatch at index={i}");
            }
        }

        [Test]
        public void Determinism_FixedTickVariableFrameDelta_SameSeedAndReplayInput_ProducesSameSpawnSnapshot()
        {
            const int replayFrameCount = 240;
            const int totalFrames = 240;

            static float ResolveVariableFrameDelta(int frame)
            {
                return frame % 8 switch
                {
                    0 => 1f / 120f,
                    1 => 1f / 45f,
                    2 => 1f / 90f,
                    3 => 1f / 30f,
                    4 => 1f / 75f,
                    5 => 1f / 50f,
                    6 => 1f / 110f,
                    _ => 1f / 40f,
                };
            }

            List<ActiveBulletSnapshot> RunScenario(string worldName)
            {
                using var world = CreateDefaultTestWorld(worldName, out var simGroup);
                var em = world.EntityManager;
                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 8f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 512, lifetime: 8f);
                CreatePlayerWithTransform(em, float3.zero);
                CreateConfigSingletons(em, budgetPerFrame: 512, maxPendingCount: 8192, maxPendingAgeFrames: 240);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                var fixedTickEntity = em.CreateEntityQuery(ComponentType.ReadWrite<FixedTickTimeComponent>()).GetSingletonEntity();
                var fixedTick = em.GetComponentData<FixedTickTimeComponent>(fixedTickEntity);
                fixedTick.EnableFixedTick = 1;
                fixedTick.PauseRequested = 0;
                fixedTick.StepRequested = 0;
                fixedTick.MaxSubSteps = 1;
                fixedTick.FixedDeltaTime = 1f / 60f;
                fixedTick.Accumulator = 0f;
                em.SetComponentData(fixedTickEntity, fixedTick);

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 7201,
                    Phase = SourceWavePhaseId.Sustain,
                    Lane = SourceSpawnLaneId.Hazard,
                    LanePriority = SourceSpawnLanePriorityUtility.ResolvePriority(SourceSpawnLaneId.Hazard),
                    BulletTypeKey = 1,
                    SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                    AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Random,
                    ShotPatternMode = WaveShotPatternModeId.Single,
                    ShotCount = 1,
                    EventRepeatCount = 1,
                    SpawnSampleBudget = 8,
                    Count = 100,
                    OldestFrame = 0u,
                });

                var replayEntity = em.CreateEntityQuery(
                    ComponentType.ReadWrite<ReplayInputControlComponent>(),
                    ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                    ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
                var replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
                replayFrames.Clear();
                for (int i = 0; i < replayFrameCount; i++)
                {
                    replayFrames.Add(new ReplayInputFrameBufferElement
                    {
                        Frame = (uint)(i + 1),
                        Position = float3.zero,
                        Rotation = quaternion.identity,
                        SyncRotation = 0,
                        VacuumRequested = 0,
                        CleanupActionRequested = 0,
                        RequestedCleanupActionSlot = 0,
                    });
                }

                em.SetComponentData(replayEntity, new ReplayInputCursorComponent { NextFrameIndex = 0 });
                em.SetComponentData(replayEntity, new ReplayInputControlComponent
                {
                    Mode = ReplayInputModeId.Playback,
                    LastRecordedFrame = 0u,
                    LastPlaybackFrame = 0u,
                    MissingFrameCount = 0,
                });

                double elapsed = 0d;
                for (int i = 0; i < totalFrames; i++)
                {
                    float dt = ResolveVariableFrameDelta(i);
                    elapsed += dt;
                    world.SetTime(new TimeData(elapsed, dt));
                    simGroup.Update();
                }

                var snapshots = new List<ActiveBulletSnapshot>();
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                snapshots.Sort((a, b) =>
                {
                    int cx = a.Position.x.CompareTo(b.Position.x);
                    if (cx != 0)
                        return cx;
                    int cy = a.Position.y.CompareTo(b.Position.y);
                    if (cy != 0)
                        return cy;
                    return a.Position.z.CompareTo(b.Position.z);
                });

                ForceDisposeSharedContainersIfNeeded();
                return snapshots;
            }

            var first = RunScenario("DeterminismFixedTickVariableDeltaWorld_A");
            var second = RunScenario("DeterminismFixedTickVariableDeltaWorld_B");

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                float3 delta = first[i].Position - second[i].Position;
                Assert.That(math.lengthsq(delta), Is.LessThanOrEqualTo(1e-6f), $"fixed tick variable-delta spawn mismatch at index={i}");
            }
        }

        [Test]
        public void Determinism_SameSeedAndReplayInput_ProducesSamePlayerTrack()
        {
            const int frameCount = 32;

            List<PlayerTrackSnapshot> RunScenario(string worldName)
            {
                using var world = CreateDefaultTestWorld(worldName, out _);
                var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
                Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");
                var em = world.EntityManager;
                CreatePlayerWithTransform(em, float3.zero);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var frameCounterEntity = em.CreateEntityQuery(ComponentType.ReadWrite<BulletFrameCounterComponent>()).GetSingletonEntity();
                var runSeedEntity = em.CreateEntityQuery(ComponentType.ReadWrite<SpawnRunSeedComponent>()).GetSingletonEntity();
                em.SetComponentData(runSeedEntity, new SpawnRunSeedComponent { Value = 0x13579BDFu });

                var replayEntity = em.CreateEntityQuery(
                    ComponentType.ReadWrite<ReplayInputControlComponent>(),
                    ComponentType.ReadWrite<ReplayInputCursorComponent>(),
                    ComponentType.ReadWrite<ReplayInputFrameBufferElement>()).GetSingletonEntity();
                var replayFrames = em.GetBuffer<ReplayInputFrameBufferElement>(replayEntity);
                replayFrames.Clear();

                for (int i = 0; i < frameCount; i++)
                {
                    replayFrames.Add(new ReplayInputFrameBufferElement
                    {
                        Frame = (uint)(i + 1),
                        MoveAxis = math.normalizesafe(new float2((i % 3) - 1f, ((i + 1) % 3) - 1f), float2.zero),
                        AimWorldXZ = new float2((i % 7) - 3f, (i % 5) - 2f),
                        HasAimWorldPoint = 1,
                        Position = float3.zero,
                        Rotation = quaternion.identity,
                        SyncRotation = 1,
                        VacuumRequested = (byte)(i % 8 == 0 ? 1 : 0),
                        CleanupActionRequested = (byte)(i % 10 == 0 ? 1 : 0),
                        RequestedCleanupActionSlot = (byte)(i % 2 == 0
                            ? PlayerCleanupActionSlotId.Primary
                            : PlayerCleanupActionSlotId.Secondary),
                        InputSequence = (uint)(1000 + i),
                    });
                }

                em.SetComponentData(replayEntity, new ReplayInputCursorComponent { NextFrameIndex = 0 });
                em.SetComponentData(replayEntity, new ReplayInputControlComponent
                {
                    Mode = ReplayInputModeId.Playback,
                    LastRecordedFrame = 0u,
                    LastPlaybackFrame = 0u,
                    MissingFrameCount = 0,
                });

                var playerEntity = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>(),
                    ComponentType.ReadWrite<PlayerGoSyncComponent>(),
                    ComponentType.ReadWrite<PlayerInputIntentComponent>()).GetSingletonEntity();

                var track = new List<PlayerTrackSnapshot>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    world.SetTime(new TimeData((i + 1d) / 60d, 1f / 60f));
                    simGroup.Update();

                    var sync = em.GetComponentData<PlayerGoSyncComponent>(playerEntity);
                    var intent = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
                    track.Add(new PlayerTrackSnapshot
                    {
                        Position = sync.Position,
                        Rotation = sync.Rotation,
                        MoveAxis = intent.MoveAxis,
                        AimWorldXZ = intent.AimWorldXZ,
                        Sequence = intent.Sequence,
                    });
                }

                ForceDisposeSharedContainersIfNeeded();
                return track;
            }

            var first = RunScenario("DeterminismPlayerTrackWorld_A");
            var second = RunScenario("DeterminismPlayerTrackWorld_B");

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                float3 positionDelta = first[i].Position - second[i].Position;
                Assert.That(math.lengthsq(positionDelta), Is.LessThanOrEqualTo(1e-6f), $"player position mismatch at frame={i}");

                float rotationDelta = math.length(first[i].Rotation.value - second[i].Rotation.value);
                Assert.That(rotationDelta, Is.LessThanOrEqualTo(1e-6f), $"player rotation mismatch at frame={i}");

                float2 moveDelta = first[i].MoveAxis - second[i].MoveAxis;
                Assert.That(math.lengthsq(moveDelta), Is.LessThanOrEqualTo(1e-6f), $"move axis mismatch at frame={i}");

                float2 aimDelta = first[i].AimWorldXZ - second[i].AimWorldXZ;
                Assert.That(math.lengthsq(aimDelta), Is.LessThanOrEqualTo(1e-6f), $"aim mismatch at frame={i}");
                Assert.That(first[i].Sequence, Is.EqualTo(second[i].Sequence), $"input sequence mismatch at frame={i}");
            }
        }

        private static World CreateDefaultTestWorld(string worldName, out SimulationSystemGroup simGroup)
        {
            var world = new World(worldName);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");
            EnsureDefaultStageRuntimeGrid(world.EntityManager);
            return world;
        }

        private static World CreateDefaultTestWorldWithoutFeedbackConsumers(string worldName, out SimulationSystemGroup simGroup)
        {
            var world = new World(worldName);
            var allSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            var systems = new List<Type>(allSystems);
            systems.RemoveAll(t =>
                t == typeof(PlayerUiFeedbackConsumeSystem) ||
                t == typeof(PlayerImpulseConsumeSystem));
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");
            EnsureDefaultStageRuntimeGrid(world.EntityManager);
            return world;
        }

        private static void EnsureDefaultStageRuntimeGrid(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>());
            var gridEntity = query.IsEmptyIgnoreFilter
                ? em.CreateEntity(typeof(StageRuntimeGridComponent))
                : query.GetSingletonEntity();

            em.SetComponentData(gridEntity, new StageRuntimeGridComponent
            {
                StageId = 1,
                Width = 1,
                Height = 1,
                CellSize = 1f,
                OriginX = -0.5f,
                OriginZ = -0.5f,
                Ready = 1,
            });

            var cells = em.HasBuffer<StageRuntimeGridCellBufferElement>(gridEntity)
                ? em.GetBuffer<StageRuntimeGridCellBufferElement>(gridEntity)
                : em.AddBuffer<StageRuntimeGridCellBufferElement>(gridEntity);
            cells.Clear();
            cells.Add(new StageRuntimeGridCellBufferElement
            {
                MovementFlags = StageCellMovementFlags.None,
                SourceRegionId = 0u,
                DepositRegionId = 0u,
            });
        }

        private static void SetFixedTickEnabled(EntityManager em, bool enabled)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<FixedTickTimeComponent>());
            var entity = query.GetSingletonEntity();
            var fixedTick = em.GetComponentData<FixedTickTimeComponent>(entity);
            fixedTick.EnableFixedTick = (byte)(enabled ? 1 : 0);
            if (!enabled)
                fixedTick.Accumulator = 0f;
            em.SetComponentData(entity, fixedTick);
        }

        private static void SetupVacuumContractEnvironment(
            EntityManager em,
            int carryLoad,
            int carryCapacity,
            out Entity playerEntity,
            int hazardStack = 0,
            int hazardStackMax = 5,
            float hazardBonusRate = 0.1f)
        {
            var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 8f);
            CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 256, lifetime: 8f);
            CreateConfigSingletons(em, budgetPerFrame: 256, maxPendingCount: 4096, maxPendingAgeFrames: 120);
            playerEntity = CreateVacuumContractPlayer(em, carryLoad, carryCapacity, hazardStack, hazardStackMax, hazardBonusRate);
        }

        private static Entity CreateVacuumContractPlayer(
            EntityManager em,
            int carryLoad,
            int carryCapacity,
            int hazardStack,
            int hazardStackMax,
            float hazardBonusRate)
        {
            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(LocalTransform),
                typeof(PlayerGoSyncComponent),
                typeof(PlayerInputIntentComponent),
                typeof(PlayerResolvedInputSnapshotComponent),
                typeof(PlayerRadiusComponent),
                typeof(VacuumRuntimeStateComponent),
                typeof(PlayerCarryBinComponent),
                typeof(PlayerHazardRiskConfigComponent),
                typeof(PlayerHazardRiskStateComponent),
                typeof(PlayerHazardRiskRequestComponent),
                typeof(PlayerHazardPenaltyConfigComponent),
                typeof(PlayerHazardPenaltyStateComponent),
                typeof(PlayerCleanupActionStateComponent),
                typeof(PlayerCleanupActionSelectionConfigComponent),
                typeof(PlayerCleanupActionSlotMapComponent),
                typeof(PlayerCleanupSweepRuntimeStateComponent),
                typeof(PlayerCleanupResolvedProfileComponent),
                typeof(PlayerCarryBinDepositRequestTag),
                typeof(PlayerCarryBinDepositContextComponent),
                typeof(PlayerHazardHitRequestTag),
                typeof(PlayerHazardHitContextComponent));

            em.SetName(player, "VacuumContract_Player");
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
                Capacity = math.max(0, carryCapacity),
            });
            em.SetComponentData(player, new PlayerHazardRiskConfigComponent
            {
                HazardStackMax = math.max(0, hazardStackMax),
                HazardBonusRate = math.max(0f, hazardBonusRate),
            });
            em.SetComponentData(player, new PlayerHazardRiskStateComponent
            {
                HazardStack = math.max(0, hazardStack),
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
                SelectedProfileKey = BroomDefaultProfileKey,
                PendingProfileKey = default,
                Version = 0,
            });
            em.SetComponentData(player, new PlayerCleanupActionSelectionConfigComponent
            {
                DefaultProfileKey = BroomDefaultProfileKey,
            });
            em.SetComponentData(player, new PlayerCleanupActionSlotMapComponent
            {
                PrimaryProfileKey = BroomDefaultProfileKey,
                SecondaryProfileKey = BroomDefaultProfileKey,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = float2.zero,
                HasLockedFacing = 0,
                ActivationFrame = 0u,
            });
            em.SetComponentEnabled<PlayerCarryBinDepositRequestTag>(player, false);
            em.SetComponentData(player, new PlayerCarryBinDepositContextComponent
            {
                DepositRegionId = 0u,
            });
            em.SetComponentEnabled<PlayerHazardHitRequestTag>(player, false);
            em.SetComponentData(player, new PlayerHazardHitContextComponent
            {
                SourceEntity = Entity.Null,
                HitDirX = 0f,
                HitDirZ = 0f,
            });

            var actionProfiles = em.AddBuffer<PlayerCleanupActionProfileBufferElement>(player);
            var broomProfile = PlayerCleanupActionContractUtility.CreateFallbackBroomSweepProfile(
                "broom_default",
                3.2f,
                2.88f,
                0.8f,
                0.25f,
                0f,
                0.25f,
                0f);
            actionProfiles.Add(broomProfile);
            em.SetComponentData(player, PlayerCleanupActionContractUtility.CreateResolvedProfile(broomProfile, 0u));

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
            em.AddComponentData(player, new PlayerImpulsePresentationSnapshotComponent
            {
                Version = 0u,
                Reason = (byte)PlayerImpulseReasonId.None,
                DirX = 0f,
                DirZ = 0f,
                Magnitude = 0f,
                Frame = 0u,
                MergedEventCount = 0,
            });

            return player;
        }

        private static Entity CreateVacuumContractSource(EntityManager em)
        {
            var source = em.CreateEntity(typeof(SourceSpawnComponent));
            em.SetComponentData(source, new SourceSpawnComponent
            {
                ThresholdWeakened = 100,
                ThresholdDepleted = 200,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            return source;
        }

        private static Entity CreateVacuumContractBullet(
            EntityManager em,
            float3 position,
            BulletCaptureRuleId captureRule,
            int scoreValue,
            Entity sourceEntity)
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
            em.SetComponentData(bullet, new BulletCaptureRuleComponent { Value = captureRule });
            em.SetComponentEnabled<BulletActiveTag>(bullet, true);
            em.SetComponentEnabled<BulletDespawnRequestTag>(bullet, false);
            return bullet;
        }

        private static Entity CreateHazardCollisionBullet(EntityManager em, float3 position, Entity sourceEntity)
        {
            var bullet = CreateVacuumContractBullet(
                em,
                position: position,
                captureRule: BulletCaptureRuleId.RiskTimedResolve,
                scoreValue: 1,
                sourceEntity: sourceEntity);

            if (!em.HasComponent<BulletHazardTag>(bullet))
                em.AddComponent<BulletHazardTag>(bullet);
            em.SetComponentEnabled<BulletHazardTag>(bullet, true);
            return bullet;
        }

        private static void RequestVacuum(EntityManager em, Entity playerEntity)
        {
            var intent = em.GetComponentData<PlayerInputIntentComponent>(playerEntity);
            intent.VacuumRequested = 1;
            intent.Sequence += 1u;
            em.SetComponentData(playerEntity, intent);

            var vacuum = em.GetComponentData<VacuumRuntimeStateComponent>(playerEntity);
            vacuum.ActivateRequested = 1;
            em.SetComponentData(playerEntity, vacuum);
        }

        private static void PrimeBroomSweepForwardWindow(
            EntityManager em,
            Entity playerEntity,
            sbyte activeSweepDirectionSign = 1)
        {
            em.SetComponentData(playerEntity, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 13f / 60f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 13f / 60f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(playerEntity, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = (sbyte)(-activeSweepDirectionSign),
                ActiveSweepDirectionSign = activeSweepDirectionSign,
                LockedFacingXZ = new float2(0f, 1f),
                HasLockedFacing = 1,
                ActivationFrame = 1u,
            });
        }

        private static float3 BroomPolarPosition(float radius, float angleDeg)
        {
            float rad = math.radians(angleDeg);
            return new float3(
                radius * math.sin(rad),
                0f,
                radius * math.cos(rad));
        }

        private static void StepSimulationFrame(World world, SimulationSystemGroup simGroup, ref double elapsed)
        {
            const float dt = 1f / 60f;
            elapsed += dt;
            world.SetTime(new TimeData(elapsed, dt));
            simGroup.Update();
        }

        private static int CountUiEvents(
            DynamicBuffer<PlayerUiFeedbackEventBufferElement> buffer,
            PlayerUiFeedbackEventType type,
            byte reason)
        {
            int count = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                var evt = buffer[i];
                if (evt.Type != type)
                    continue;
                if (evt.Reason != reason)
                    continue;
                count++;
            }

            return count;
        }

        private static Entity CreateBulletPrefab(EntityManager em, int typeKey, float lifetime)
        {
            var prefab = em.CreateEntity();
            em.AddComponent<Prefab>(prefab);
            em.AddComponent<LocalTransform>(prefab);
            em.AddComponent<BulletVelocityComponent>(prefab);
            em.AddComponent<BulletSpeedComponent>(prefab);
            em.AddComponent<BulletLifetimeComponent>(prefab);
            em.AddComponent<BulletLifetimeMaxComponent>(prefab);
            em.AddComponent<BulletLifecycleRequestComponent>(prefab);
            em.AddComponent<BulletLifecycleContactComponent>(prefab);
            em.AddComponent<BulletTypeKeyComponent>(prefab);
            em.AddComponent<BulletSourceRefComponent>(prefab);
            em.AddComponent<BulletRadiusComponent>(prefab);
            em.AddComponent<BulletScoreValueComponent>(prefab);
            em.AddComponent<BulletCaptureRuleComponent>(prefab);
            em.AddComponent<BulletActiveTag>(prefab);
            em.AddComponent<BulletDespawnRequestTag>(prefab);
            em.AddBuffer<EntityRenderElementBuffer>(prefab);

            em.SetComponentData(prefab, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            em.SetComponentData(prefab, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(prefab, new BulletSpeedComponent { Value = 0f });
            em.SetComponentData(prefab, new BulletLifetimeComponent { Value = lifetime });
            em.SetComponentData(prefab, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(prefab, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.None,
                Priority = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(prefab, default(BulletLifecycleContactComponent));
            em.SetComponentData(prefab, new BulletTypeKeyComponent { Value = typeKey });
            em.SetComponentData(prefab, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(prefab, new BulletRadiusComponent { Value = 0.2f });
            em.SetComponentData(prefab, new BulletScoreValueComponent { Value = 1 });
            em.SetComponentData(prefab, new BulletCaptureRuleComponent { Value = BulletCaptureRuleId.StandardCollectible });
            return prefab;
        }

        private static void CreatePoolRegistry(EntityManager em, Entity prefab, int typeKey, int poolSize, float lifetime = 0f)
        {
            var registry = em.CreateEntity(typeof(BulletPoolRegistryTag));
            var defs = em.AddBuffer<BulletPoolDefinitionBuffer>(registry);
            defs.Add(new BulletPoolDefinitionBuffer
            {
                TypeKey = typeKey,
                Prefab = prefab,
                PoolSize = poolSize,
                CaptureRule = BulletCaptureRuleId.StandardCollectible,
                Speed = 0f,
                Lifetime = lifetime,
                Radius = 0.2f,
                ScoreValue = 1,
            });
        }

        private static void CreatePlayer(EntityManager em)
        {
            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerGoSyncComponent),
                typeof(PlayerInputIntentComponent),
                typeof(PlayerResolvedInputSnapshotComponent));
            em.SetName(player, "SmokeStress_Player");
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                SyncRotation = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
            });
            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
                Sequence = 0u,
            });
            em.SetComponentData(player, new PlayerResolvedInputSnapshotComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
                Sequence = 0u,
            });
        }

        private static void CreatePlayerWithTransform(EntityManager em, float3 position)
        {
            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(LocalTransform),
                typeof(PlayerGoSyncComponent),
                typeof(PlayerInputIntentComponent),
                typeof(PlayerResolvedInputSnapshotComponent),
                typeof(VacuumRuntimeStateComponent),
                typeof(PlayerCleanupResolvedProfileComponent),
                typeof(PlayerCleanupSweepRuntimeStateComponent));
            em.SetName(player, "SmokeStress_Player_WithTransform");
            em.SetComponentData(player, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = position,
                Rotation = quaternion.identity,
                SyncRotation = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
            });
            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
                Sequence = 0u,
            });
            em.SetComponentData(player, new PlayerResolvedInputSnapshotComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
                Sequence = 0u,
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
            em.SetComponentData(player, PlayerCleanupActionContractUtility.CreateResolvedProfile(
                PlayerCleanupActionContractUtility.CreateFallbackBroomSweepProfile(
                    "broom_default",
                    3.2f,
                    2.88f,
                    0.8f),
                0u));
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = float2.zero,
                HasLockedFacing = 0,
                ActivationFrame = 0u,
            });
        }

        private static void CreateConfigSingletons(EntityManager em, int budgetPerFrame, int maxPendingCount, uint maxPendingAgeFrames)
        {
            var cfgEntity = em.CreateEntity(typeof(BulletFieldConfigComponent), typeof(MetaScrapComponent));
            em.SetComponentData(cfgEntity, new BulletFieldConfigComponent
            {
                PoolSize = 6000,
                InvCellSize = 1f / 1.6f,
            });
            em.SetComponentData(cfgEntity, new MetaScrapComponent { Value = 0 });

            var policyEntity = em.CreateEntity(typeof(SpawnRequestPolicyComponent));
            em.SetComponentData(policyEntity, new SpawnRequestPolicyComponent
            {
                BudgetPerFrame = budgetPerFrame,
                MaxPendingCount = maxPendingCount,
                MaxPendingAgeFrames = maxPendingAgeFrames,
                WarningLogCooldownFrames = 60,
                WarningBacklogPercent = 70,
                WarningHighBacklogPercent = 85,
            });

            var metricsEntity = em.CreateEntity(typeof(SpawnBacklogMetricsComponent));
            em.SetComponentData(metricsEntity, default(SpawnBacklogMetricsComponent));

            var cursorEntity = em.CreateEntity(typeof(SpawnBudgetCursorComponent));
            em.SetComponentData(cursorEntity, new SpawnBudgetCursorComponent { SourceStartIndex = 0 });

            var runSeedEntity = em.CreateEntity(typeof(SpawnRunSeedComponent));
            em.SetComponentData(runSeedEntity, new SpawnRunSeedComponent { Value = 0x9E3779B9u });

            var runDirectorConfigEntity = em.CreateEntity(typeof(RunProgressDirectorConfigComponent));
            em.SetComponentData(runDirectorConfigEntity, new RunProgressDirectorConfigComponent
            {
                PressureHoldSec = 0.35f,
                BaselineTrashDensityScale = 0.45f,
                PressureDensityScale = 1.0f,
            });

            var stageConfigEntity = em.CreateEntity(typeof(RunDirectorStageConfigComponent));
            em.SetComponentData(stageConfigEntity, new RunDirectorStageConfigComponent
            {
                InitialState = RunDirectorStageStateId.Running,
                MinIdleDurationSec = 0f,
                ClearAutoAdvanceTimeoutSec = 10f,
            });

            var stageStateEntity = em.CreateEntity(typeof(RunDirectorStageStateComponent));
            em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
                StateElapsedSec = 0f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });

            var stageGateEntity = em.CreateEntity(typeof(RunDirectorStageGateComponent));
            em.SetComponentData(stageGateEntity, new RunDirectorStageGateComponent
            {
                IntroPresentationDone = 1,
                ClearPresentationDone = 1,
                MinIdleDurationElapsed = 1,
                AutoAdvanceTimeoutElapsed = 0,
            });

            var stageRequestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
            em.SetComponentData(stageRequestEntity, default(RunDirectorStageRequestComponent));

            var stageSignalEntity = em.CreateEntity(typeof(RunDirectorStageSignalComponent));
            em.SetComponentData(stageSignalEntity, default(RunDirectorStageSignalComponent));

            var pressureWeightEntity = em.CreateEntity(typeof(RunDirectorPressureWeightSingletonTag));
            var pressureWeights = em.AddBuffer<RunDirectorPressureWeightBuffer>(pressureWeightEntity);
            pressureWeights.Add(new RunDirectorPressureWeightBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceOccupancy,
                Weight = 1.0f,
            });
            pressureWeights.Add(new RunDirectorPressureWeightBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceHoldSec,
                Weight = 1.0f,
            });
        }

        private static void SetPlayerPosition(EntityManager em, float3 position)
        {
            using var playerQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadWrite<PlayerGoSyncComponent>());
            using var players = playerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var sync = em.GetComponentData<PlayerGoSyncComponent>(player);
                sync.Position = position;
                em.SetComponentData(player, sync);
                if (em.HasComponent<LocalTransform>(player))
                    em.SetComponentData(player, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            }
        }

        private static Entity CreateSource(EntityManager em, int typeKey, float spawnDensityPerSecPerArea)
        {
            var source = em.CreateEntity(
                typeof(SourceSpawnComponent),
                typeof(SourceSpawnRuntimeComponent),
                typeof(SourceAnchorComponent),
                typeof(BulletFieldAreaComponent),
                typeof(Shape2DComponent),
                typeof(SourceShapeDerivedComponent),
                typeof(SourcePollutionConfigComponent),
                typeof(SourcePollutionGridComponent),
                typeof(LocalTransform));

            em.SetComponentData(source, new SourceSpawnComponent
            {
                ThresholdWeakened = 1000000,
                ThresholdDepleted = 2000000,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            em.SetComponentData(source, new SourceSpawnRuntimeComponent { SpawnSequence = 1 });
            SetSourceAnchor(em, source, float3.zero);
            SetSourceShape(em, source, Shape2DKind.Rectangle, 0f, new float2(20f, 20f));
            em.SetComponentData(source, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0.1f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 4,
                ActiveRatioThreshold = 0.35f,
                RecoveryCooldownFrames = 45u,
                RecoveryWaveSeedCount = 2,
                RecoveryWaveClusterSize = 4,
                RecoveryWaveRestoreValue = 0.4f,
                RecoveryRecentCleanBiasFrames = 90u,
            });
            em.SetComponentData(source, new SourcePollutionGridComponent
            {
                CellSize = 1f,
                InvCellSize = 1f,
                HalfExtents = new float2(10f, 10f),
                OriginX = -10f,
                OriginZ = -10f,
                Cols = 1,
                Rows = 1,
            });

            em.AddBuffer<SourceSpawnRequestBuffer>(source);
            em.AddBuffer<SourceActiveBulletCountBuffer>(source);
            em.AddBuffer<SourcePollutionCellBuffer>(source);
            em.AddBuffer<SourcePollutionDropRequestBuffer>(source);
            em.AddBuffer<SourcePollutionValidCellIndexBuffer>(source);
            em.AddBuffer<SourceRegionCellIndexBuffer>(source);
            EnableV3Source(em, source, stableId: (uint)math.max(1, source.Index + 1), activeState: SourceStateId.Normal);
            var pollutionGrid = em.GetComponentData<SourcePollutionGridComponent>(source);
            var pollutionConfig = em.GetComponentData<SourcePollutionConfigComponent>(source);
            var sourceShape = em.GetComponentData<Shape2DComponent>(source);
            var sourceDerived = em.GetComponentData<SourceShapeDerivedComponent>(source);
            SourceRuntimeApplyUtility.RebuildPollutionGrid(
                in sourceShape,
                in sourceDerived,
                in pollutionConfig,
                ref pollutionGrid,
                em.GetBuffer<SourcePollutionCellBuffer>(source),
                em.GetBuffer<SourcePollutionDropRequestBuffer>(source),
                em.GetBuffer<SourcePollutionValidCellIndexBuffer>(source));
            em.SetComponentData(source, pollutionGrid);

            const int defaultClipId = 1001;
            var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(source);
            clipPatterns.Clear();
            var defaultPattern = CreateClipPattern(
                directiveId: 1,
                clipId: defaultClipId,
                phase: SourceWavePhaseId.Sustain,
                lane: SourceSpawnLaneId.Hazard,
                triggerState: SourceStateId.Normal,
                startSec: 0f,
                endSec: 1f,
                ratePerSecPerArea: spawnDensityPerSecPerArea);
            defaultPattern.BulletTypeKey = typeKey;
            clipPatterns.Add(defaultPattern);

            var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(source);
            sustainCandidates.Clear();
            sustainCandidates.Add(new SourceSustainSlotCandidateBuffer
            {
                State = SourceStateId.Normal,
                Lane = SourceSpawnLaneId.Hazard,
                ClipId = defaultClipId,
                Weight = 1f
            });

            var sustainLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source);
            sustainLanes.Clear();
            sustainLanes.Add(new SourceSustainRuntimeLaneBuffer
            {
                Lane = SourceSpawnLaneId.Hazard,
                ActiveClipId = defaultClipId,
                ElapsedSec = 0f,
                LastClipId = 0,
                SelectionSequence = 1u,
                LastMissingLogFrame = 0u
            });

            return source;
        }

        private static Entity CreateDepositRegionGrid(EntityManager em, uint depositRegionId)
        {
            Entity grid;
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>()))
            {
                grid = query.IsEmptyIgnoreFilter
                    ? em.CreateEntity(typeof(StageRuntimeGridComponent))
                    : query.GetSingletonEntity();
            }

            em.SetComponentData(grid, new StageRuntimeGridComponent
            {
                StageId = 1,
                Width = 1,
                Height = 1,
                CellSize = 1f,
                OriginX = -0.5f,
                OriginZ = -0.5f,
                Ready = 1,
            });

            var cells = em.HasBuffer<StageRuntimeGridCellBufferElement>(grid)
                ? em.GetBuffer<StageRuntimeGridCellBufferElement>(grid)
                : em.AddBuffer<StageRuntimeGridCellBufferElement>(grid);
            cells.Clear();
            cells.Add(new StageRuntimeGridCellBufferElement
            {
                MovementFlags = StageCellMovementFlags.None,
                SourceRegionId = 0u,
                DepositRegionId = depositRegionId,
            });

            return grid;
        }

        private static Entity CreateSourceRegionGrid(EntityManager em, uint sourceRegionId)
        {
            Entity grid;
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>()))
            {
                grid = query.IsEmptyIgnoreFilter
                    ? em.CreateEntity(typeof(StageRuntimeGridComponent))
                    : query.GetSingletonEntity();
            }

            em.SetComponentData(grid, new StageRuntimeGridComponent
            {
                StageId = 1,
                Width = 1,
                Height = 1,
                CellSize = 4f,
                OriginX = -2f,
                OriginZ = -2f,
                Ready = 1,
            });

            var cells = em.HasBuffer<StageRuntimeGridCellBufferElement>(grid)
                ? em.GetBuffer<StageRuntimeGridCellBufferElement>(grid)
                : em.AddBuffer<StageRuntimeGridCellBufferElement>(grid);
            cells.Clear();
            cells.Add(new StageRuntimeGridCellBufferElement
            {
                MovementFlags = StageCellMovementFlags.None,
                SourceRegionId = sourceRegionId,
                DepositRegionId = 0u,
            });

            return grid;
        }

        private static void SetSourceShape(EntityManager em, Entity source, Shape2DKind kind, float radius, float2 size)
        {
            var shape = new Shape2DComponent
            {
                Kind = kind,
                Radius = radius,
                Size = size,
            };

            em.SetComponentData(source, shape);
            var derived = new SourceShapeDerivedComponent
            {
                ComputedArea = Shape2DUtility.ComputeArea(in shape),
                HalfExtents = Shape2DUtility.ComputeHalfExtents(in shape),
            };
            em.SetComponentData(source, derived);

            if (em.HasComponent<SourcePollutionConfigComponent>(source)
                && em.HasComponent<SourcePollutionGridComponent>(source)
                && em.HasBuffer<SourcePollutionCellBuffer>(source)
                && em.HasBuffer<SourcePollutionDropRequestBuffer>(source)
                && em.HasBuffer<SourcePollutionValidCellIndexBuffer>(source))
            {
                var pollutionConfig = em.GetComponentData<SourcePollutionConfigComponent>(source);
                var pollutionGrid = em.GetComponentData<SourcePollutionGridComponent>(source);
                SourceRuntimeApplyUtility.RebuildPollutionGrid(
                    in shape,
                    in derived,
                    in pollutionConfig,
                    ref pollutionGrid,
                    em.GetBuffer<SourcePollutionCellBuffer>(source),
                    em.GetBuffer<SourcePollutionDropRequestBuffer>(source),
                    em.GetBuffer<SourcePollutionValidCellIndexBuffer>(source));
                var anchor = em.GetComponentData<SourceAnchorComponent>(source).Position;
                pollutionGrid.OriginX = anchor.x - pollutionGrid.HalfExtents.x;
                pollutionGrid.OriginZ = anchor.z - pollutionGrid.HalfExtents.y;
                em.SetComponentData(source, pollutionGrid);
            }
        }

        private static void SetSourceAnchor(EntityManager em, Entity source, float3 position)
        {
            em.SetComponentData(source, new SourceAnchorComponent { Position = position });
            if (em.HasComponent<LocalTransform>(source))
                em.SetComponentData(source, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            if (em.HasComponent<SourcePollutionGridComponent>(source))
            {
                var pollutionGrid = em.GetComponentData<SourcePollutionGridComponent>(source);
                pollutionGrid.OriginX = position.x - pollutionGrid.HalfExtents.x;
                pollutionGrid.OriginZ = position.z - pollutionGrid.HalfExtents.y;
                em.SetComponentData(source, pollutionGrid);
            }
        }

        private static int ComputeOldestBacklogAge(EntityManager em, uint frame)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<SourceSpawnRequestBuffer>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            int oldest = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(entities[i]);
                for (int r = 0; r < requests.Length; r++)
                {
                    if (requests[r].Count <= 0)
                        continue;
                    uint age = frame - requests[r].OldestFrame;
                    oldest = math.max(oldest, (int)age);
                }
            }

            return oldest;
        }

        private static int GetRequestCountByDirective(DynamicBuffer<SourceSpawnRequestBuffer> requests, int directiveId)
        {
            int total = 0;
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.DirectiveId != directiveId)
                    continue;
                if (item.Count <= 0)
                    continue;

                total += item.Count;
            }

            return total;
        }

        private static int SumPendingRequestCount(DynamicBuffer<SourceSpawnRequestBuffer> requests)
        {
            int total = 0;
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].Count <= 0)
                    continue;

                total += requests[i].Count;
            }

            return total;
        }

        private static bool HasPendingForPhase(DynamicBuffer<SourceSpawnRequestBuffer> requests, SourceWavePhaseId phase)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].Count <= 0)
                    continue;
                if (requests[i].Phase != phase)
                    continue;

                return true;
            }

            return false;
        }

        private static bool HasPendingSustainForLane(DynamicBuffer<SourceSpawnRequestBuffer> requests, SourceSpawnLaneId lane)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.Count <= 0)
                    continue;
                if (item.Phase != SourceWavePhaseId.Sustain)
                    continue;
                if (item.Lane != lane)
                    continue;

                return true;
            }

            return false;
        }

        private static void EnableV3Source(EntityManager em, Entity source, uint stableId, SourceStateId activeState)
        {
            if (!em.HasComponent<SourceStableIdComponent>(source))
            {
                em.AddComponentData(source, new SourceStableIdComponent
                {
                    Value = math.max(1u, stableId)
                });
            }
            else
            {
                em.SetComponentData(source, new SourceStableIdComponent
                {
                    Value = math.max(1u, stableId)
                });
            }

            if (!em.HasComponent<SourceSustainRuntimeComponent>(source))
            {
                em.AddComponentData(source, new SourceSustainRuntimeComponent
                {
                    ActiveState = activeState
                });
            }
            else
            {
                em.SetComponentData(source, new SourceSustainRuntimeComponent
                {
                    ActiveState = activeState
                });
            }

            if (!em.HasComponent<SourceEventRuntimeComponent>(source))
            {
                em.AddComponentData(source, new SourceEventRuntimeComponent
                {
                    IsPlaying = 0,
                    ActiveEventClipId = 0,
                    TriggerState = activeState,
                    ElapsedSec = 0f,
                    SelectionSequence = 1u,
                });
            }
            else
            {
                em.SetComponentData(source, new SourceEventRuntimeComponent
                {
                    IsPlaying = 0,
                    ActiveEventClipId = 0,
                    TriggerState = activeState,
                    ElapsedSec = 0f,
                    SelectionSequence = 1u,
                });
            }

            var runDirectorState = new SourceRunDirectorStateComponent
            {
                State = activeState == SourceStateId.Depleted
                    ? RunDirectorSourceStateId.Finish
                    : RunDirectorSourceStateId.Pressure,
                SelectedClipState = activeState,
                PressureOccupancySec = 0f,
                DensityScale = 1f,
                Version = 1u,
            };
            if (!em.HasComponent<SourceRunDirectorStateComponent>(source))
                em.AddComponentData(source, runDirectorState);
            else
                em.SetComponentData(source, runDirectorState);

            if (!em.HasBuffer<SourceDirectorPressureInputBuffer>(source))
                em.AddBuffer<SourceDirectorPressureInputBuffer>(source);
            if (!em.HasBuffer<SourceClipPatternBuffer>(source))
                em.AddBuffer<SourceClipPatternBuffer>(source);
            if (!em.HasBuffer<SourceSustainSlotCandidateBuffer>(source))
                em.AddBuffer<SourceSustainSlotCandidateBuffer>(source);
            if (!em.HasBuffer<SourceSustainRuntimeLaneBuffer>(source))
                em.AddBuffer<SourceSustainRuntimeLaneBuffer>(source);
            if (!em.HasBuffer<SourceEventQueueBuffer>(source))
                em.AddBuffer<SourceEventQueueBuffer>(source);

            var pressureInputs = em.GetBuffer<SourceDirectorPressureInputBuffer>(source);
            pressureInputs.Clear();
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceOccupancy,
                Value = 0f,
            });
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceHoldSec,
                Value = 0f,
            });
            em.GetBuffer<SourceClipPatternBuffer>(source).Clear();
            em.GetBuffer<SourceSustainSlotCandidateBuffer>(source).Clear();
            em.GetBuffer<SourceSustainRuntimeLaneBuffer>(source).Clear();
            em.GetBuffer<SourceEventQueueBuffer>(source).Clear();
        }

        private static SourceClipPatternBuffer CreateClipPattern(
            int directiveId,
            int clipId,
            SourceWavePhaseId phase,
            SourceSpawnLaneId lane,
            SourceStateId triggerState,
            float startSec,
            float endSec,
            float ratePerSecPerArea)
        {
            return new SourceClipPatternBuffer
            {
                DirectiveId = directiveId,
                ClipId = clipId,
                ClipDurationSec = endSec,
                Phase = phase,
                Lane = lane,
                TriggerState = triggerState,
                LocalStartSec = startSec,
                LocalEndSec = endSec,
                BulletTypeKey = 1,
                EmissionMode = SourceSpawnEmissionModeId.RateField,
                SpawnMode = SourceSpawnModeId.FixedDensity,
                SamplingAnchorMode = WaveSamplingAnchorModeId.SourceCenter,
                AreaSamplerMode = WaveAreaSamplerModeId.UniformField,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Random,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                AimAngleOffsetDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                LineNormalAngleOffsetDeg = 0f,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                NWayAngleSpacingDeg = 0f,
                EventRepeatCount = 1,
                FixedPoint = float2.zero,
                SpawnOffset = float2.zero,
                LineStart = float2.zero,
                LineEnd = float2.zero,
                SampleSpacing = 1f,
                PointSetCount = 0,
                Point0 = float2.zero,
                Point1 = float2.zero,
                Point2 = float2.zero,
                Point3 = float2.zero,
                SpawnSampleBudget = 16,
                PlayerNoSpawnRadius = 0f,
                BaseAngleDeg = 0f,
                SpiralStepDeg = 0f,
                SpawnDensityPerSecPerArea = ratePerSecPerArea,
                MeanEventsPerSec = 0f,
                BurstRepeatCount = 1,
                BurstIntervalSec = 1f,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                LanePriority = SourceSpawnLanePriorityUtility.ResolvePriority(lane),
                MaxActiveDensityPerArea = 0f,
                SpawnAccumulator = 0f,
                BurstEventsEmitted = 0,
            };
        }

        private static bool HasDirective(DynamicBuffer<SourceSpawnRequestBuffer> requests, int directiveId)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].DirectiveId == directiveId && requests[i].Count > 0)
                    return true;
            }

            return false;
        }

        private struct ActiveBulletSnapshot
        {
            public float3 Position;
            public quaternion Rotation;
        }

        private struct PlayerTrackSnapshot
        {
            public float3 Position;
            public quaternion Rotation;
            public float2 MoveAxis;
            public float2 AimWorldXZ;
            public uint Sequence;
        }

        private static void CollectActiveBulletSnapshotsForSource(
            EntityManager em,
            Entity sourceEntity,
            List<ActiveBulletSnapshot> snapshots)
        {
            snapshots.Clear();
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<BulletActiveTag>(),
                ComponentType.ReadOnly<BulletSourceRefComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var sourceRef = em.GetComponentData<BulletSourceRefComponent>(entity);
                if (sourceRef.Value != sourceEntity)
                    continue;

                var tx = em.GetComponentData<LocalTransform>(entity);
                snapshots.Add(new ActiveBulletSnapshot
                {
                    Position = tx.Position,
                    Rotation = tx.Rotation,
                });
            }
        }

        private static bool ContainsPosition(
            List<ActiveBulletSnapshot> snapshots,
            float3 expected,
            float tolerance)
        {
            float tolSq = tolerance * tolerance;
            for (int i = 0; i < snapshots.Count; i++)
            {
                float3 delta = snapshots[i].Position - expected;
                if (math.lengthsq(delta) <= tolSq)
                    return true;
            }

            return false;
        }

        private static int CountDirectionAtPoint(
            List<ActiveBulletSnapshot> snapshots,
            float3 point,
            float2 expectedDirection,
            float positionTolerance,
            float directionTolerance)
        {
            int count = 0;
            float posTolSq = positionTolerance * positionTolerance;
            float dirTolSq = directionTolerance * directionTolerance;
            float2 expected = math.normalizesafe(expectedDirection, new float2(1f, 0f));

            for (int i = 0; i < snapshots.Count; i++)
            {
                float3 delta = snapshots[i].Position - point;
                if (math.lengthsq(delta) > posTolSq)
                    continue;

                float3 forward = math.mul(snapshots[i].Rotation, new float3(0f, 0f, 1f));
                float2 dir = math.normalizesafe(new float2(forward.x, forward.z), new float2(1f, 0f));
                float2 diff = dir - expected;
                if (math.lengthsq(diff) <= dirTolSq)
                    count++;
            }

            return count;
        }

        private static bool TryGetSingleActiveBulletPositionForSource(EntityManager em, Entity sourceEntity, out float3 position)
        {
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<BulletActiveTag>(),
                ComponentType.ReadOnly<BulletSourceRefComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            int count = 0;
            position = float3.zero;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var sourceRef = em.GetComponentData<BulletSourceRefComponent>(entity);
                if (sourceRef.Value != sourceEntity)
                    continue;

                position = em.GetComponentData<LocalTransform>(entity).Position;
                count++;
            }

            return count == 1;
        }

        private static bool TryGetSingleton<T>(EntityManager em, out T value) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                value = default;
                return false;
            }

            value = query.GetSingleton<T>();
            return true;
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
    }
}
