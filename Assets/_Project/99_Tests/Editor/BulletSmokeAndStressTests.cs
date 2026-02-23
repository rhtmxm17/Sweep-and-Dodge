using NUnit.Framework;
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
        public void SpawnRequestBuild_MergesByDirectiveId_AndSeparatesDifferentDirective()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnDirectiveMergeWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

                var patterns = em.GetBuffer<SourceSpawnPatternBuffer>(source);
                patterns.Clear();
                patterns.Add(new SourceSpawnPatternBuffer
                {
                    DirectiveId = 101,
                    State = SourceStateId.Normal,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                    SpawnDensityPerSecPerArea = 2f,
                    MeanEventsPerSec = 0f,
                    MaxActiveDensityPerArea = 0f,
                    SpawnAccumulator = 0f,
                });
                patterns.Add(new SourceSpawnPatternBuffer
                {
                    DirectiveId = 101,
                    State = SourceStateId.Normal,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                    SpawnDensityPerSecPerArea = 3f,
                    MeanEventsPerSec = 0f,
                    MaxActiveDensityPerArea = 0f,
                    SpawnAccumulator = 0f,
                });
                patterns.Add(new SourceSpawnPatternBuffer
                {
                    DirectiveId = 202,
                    State = SourceStateId.Normal,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                    SpawnDensityPerSecPerArea = 5f,
                    MeanEventsPerSec = 0f,
                    MaxActiveDensityPerArea = 0f,
                    SpawnAccumulator = 0f,
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

                em.SetComponentData(source, new SourceAnchorComponent { Position = new float3(0f, 7f, 0f) });
                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = float2.zero,
                    ComputedArea = 0f,
                });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5001,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.FixedPoint,
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

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = float2.zero,
                    ComputedArea = 0f,
                });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5002,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.PlayerRelative,
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

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = float2.zero,
                    ComputedArea = 0f,
                });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 5003,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.PlayerRelative,
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
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

                var patterns = em.GetBuffer<SourceSpawnPatternBuffer>(source);
                patterns.Clear();
                patterns.Add(new SourceSpawnPatternBuffer
                {
                    DirectiveId = 7001,
                    State = SourceStateId.Normal,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.Poisson,
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                    SpawnDensityPerSecPerArea = 0f,
                    MeanEventsPerSec = 3600f,
                    MaxActiveDensityPerArea = 0f,
                    SpawnAccumulator = 0f,
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

                var patterns = em.GetBuffer<SourceSpawnPatternBuffer>(sourceEntity);
                patterns.Clear();
                patterns.Add(new SourceSpawnPatternBuffer
                {
                    DirectiveId = 801,
                    State = SourceStateId.Normal,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                    SpawnDensityPerSecPerArea = 450f,
                    MeanEventsPerSec = 0f,
                    MaxActiveDensityPerArea = 0f,
                    SpawnAccumulator = 0f,
                });
                patterns.Add(new SourceSpawnPatternBuffer
                {
                    DirectiveId = 802,
                    State = SourceStateId.Normal,
                    BulletTypeKey = 1,
                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                    SpawnDensityPerSecPerArea = 450f,
                    MeanEventsPerSec = 0f,
                    MaxActiveDensityPerArea = 0f,
                    SpawnAccumulator = 0f,
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

        private static World CreateDefaultTestWorld(string worldName, out SimulationSystemGroup simGroup)
        {
            var world = new World(worldName);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");
            return world;
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
            var player = em.CreateEntity(typeof(PlayerTag));
            em.SetName(player, "SmokeStress_Player");
        }

        private static void CreatePlayerWithTransform(EntityManager em, float3 position)
        {
            var player = em.CreateEntity(typeof(PlayerTag), typeof(LocalTransform));
            em.SetName(player, "SmokeStress_Player_WithTransform");
            em.SetComponentData(player, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
        }

        private static void CreateConfigSingletons(EntityManager em, int budgetPerFrame, int maxPendingCount, uint maxPendingAgeFrames)
        {
            var cfgEntity = em.CreateEntity(typeof(BulletFieldConfigComponent), typeof(MetaScrapComponent));
            em.SetComponentData(cfgEntity, new BulletFieldConfigComponent
            {
                PoolSize = 6000,
                MaxActiveTarget = 6000,
                CellSize = 1.6f,
                InvCellSize = 1f / 1.6f,
                BulletLifetime = 0f,
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
        }

        private static Entity CreateSource(EntityManager em, int typeKey, float spawnDensityPerSecPerArea)
        {
            var source = em.CreateEntity(
                typeof(SourceSpawnComponent),
                typeof(SourceSpawnRuntimeComponent),
                typeof(SourceAnchorComponent),
                typeof(BulletFieldAreaComponent));

            em.SetComponentData(source, new SourceSpawnComponent
            {
                ThresholdWeakened = 1000000,
                ThresholdDepleted = 2000000,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            em.SetComponentData(source, new SourceSpawnRuntimeComponent { SpawnSequence = 1 });
            em.SetComponentData(source, new SourceAnchorComponent { Position = float3.zero });
            em.SetComponentData(source, new BulletFieldAreaComponent
            {
                Shape = BulletFieldShapeId.Rectangle,
                Radius = 0f,
                Size = new float2(20f, 20f),
                ComputedArea = 400f,
            });

            var patterns = em.AddBuffer<SourceSpawnPatternBuffer>(source);
            patterns.Add(new SourceSpawnPatternBuffer
            {
                DirectiveId = 1,
                State = SourceStateId.Normal,
                BulletTypeKey = typeKey,
                EmissionMode = SourceSpawnEmissionModeId.RateField,
                SpawnMode = SourceSpawnModeId.FixedDensity,
                SamplingMode = SourceSpawnSamplingModeId.UniformField,
                CenterMode = SourceSpawnCenterModeId.SourceCenter,
                FixedPoint = float2.zero,
                SpawnOffset = float2.zero,
                SpawnSampleBudget = 16,
                PlayerNoSpawnRadius = 0f,
                SpawnDensityPerSecPerArea = spawnDensityPerSecPerArea,
                MeanEventsPerSec = 0f,
                MaxActiveDensityPerArea = 0f,
                SpawnAccumulator = 0f,
            });

            em.AddBuffer<SourceSpawnRequestBuffer>(source);
            em.AddBuffer<SourceActiveBulletCountBuffer>(source);
            return source;
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

        private static bool HasDirective(DynamicBuffer<SourceSpawnRequestBuffer> requests, int directiveId)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].DirectiveId == directiveId && requests[i].Count > 0)
                    return true;
            }

            return false;
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
