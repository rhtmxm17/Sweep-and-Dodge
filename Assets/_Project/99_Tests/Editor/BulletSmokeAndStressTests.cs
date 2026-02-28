using NUnit.Framework;
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

                em.SetComponentData(source, new SourceAnchorComponent { Position = new float3(0f, 6f, 0f) });
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
                    DirectiveId = 5101,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.PointSet,
                    CenterMode = SourceSpawnCenterModeId.FixedPoint,
                    FixedPoint = new float2(5f, 7f),
                    PointSetCount = 3,
                    Point0 = new float2(-1f, 0f),
                    Point1 = new float2(0f, 2f),
                    Point2 = new float2(3f, -1f),
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    DirectionMode = SourceSpawnDirectionModeId.Fixed,
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
        public void SpawnExecution_PointSetSpiral_UsesPerPointLocalSequence()
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

                em.SetComponentData(source, new SourceAnchorComponent { Position = new float3(0f, 0f, 0f) });
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
                    DirectiveId = 5102,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.PointSet,
                    CenterMode = SourceSpawnCenterModeId.FixedPoint,
                    FixedPoint = float2.zero,
                    PointSetCount = 3,
                    Point0 = new float2(-2f, 0f),
                    Point1 = new float2(0f, 0f),
                    Point2 = new float2(2f, 0f),
                    SpawnSampleBudget = 4,
                    PlayerNoSpawnRadius = 0f,
                    DirectionMode = SourceSpawnDirectionModeId.Spiral,
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

                Assert.That(CountDirectionAtPoint(snapshots, p0, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p1, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p2, dirRight, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p0, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p1, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
                Assert.That(CountDirectionAtPoint(snapshots, p2, dirUp, 0.0001f, 0.0001f), Is.EqualTo(1));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
        }

        [Test]
        public void SpawnExecution_LineEvenNWay_SpawnsAtomicSetsPerSamplePoint()
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

                em.SetComponentData(source, new SourceAnchorComponent { Position = new float3(0f, 0f, 0f) });
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
                    DirectiveId = 5201,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.LineEven,
                    CenterMode = SourceSpawnCenterModeId.FixedPoint,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    DirectionMode = SourceSpawnDirectionModeId.NWay,
                    BaseAngleDeg = 45f,
                    NWayCount = 4,
                    Count = 20,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var snapshots = new List<ActiveBulletSnapshot>(24);
                CollectActiveBulletSnapshotsForSource(em, source, snapshots);
                Assert.That(snapshots.Count, Is.EqualTo(20));

                float invSqrt2 = 0.70710677f;
                float2 dir45 = new float2(invSqrt2, invSqrt2);
                float2 dir135 = new float2(-invSqrt2, invSqrt2);
                float2 dir225 = new float2(-invSqrt2, -invSqrt2);
                float2 dir315 = new float2(invSqrt2, -invSqrt2);

                for (int i = -2; i <= 2; i++)
                {
                    var point = new float3(i, 0f, 0f);
                    Assert.That(CountDirectionAtPoint(snapshots, point, dir45, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dir135, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dir225, 0.0001f, 0.0001f), Is.EqualTo(1));
                    Assert.That(CountDirectionAtPoint(snapshots, point, dir315, 0.0001f, 0.0001f), Is.EqualTo(1));
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

                em.SetComponentData(source, new SourceAnchorComponent { Position = float3.zero });
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
                    DirectiveId = 5202,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.LineEven,
                    CenterMode = SourceSpawnCenterModeId.FixedPoint,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-1f, 0f),
                    LineEnd = new float2(1f, 0f),
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    DirectionMode = SourceSpawnDirectionModeId.NWay,
                    BaseAngleDeg = 0f,
                    NWayCount = 4,
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

                em.SetComponentData(source, new SourceAnchorComponent { Position = float3.zero });
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
                    DirectiveId = 5203,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.LineEven,
                    CenterMode = SourceSpawnCenterModeId.FixedPoint,
                    FixedPoint = float2.zero,
                    LineStart = new float2(-2f, 0f),
                    LineEnd = new float2(2f, 0f),
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    DirectionMode = SourceSpawnDirectionModeId.NWay,
                    BaseAngleDeg = 45f,
                    NWayCount = 4,
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

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

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
        public void SpawnRequestBuild_PoissonBurstShotsPerEvent_AccumulatesAsShotMultiples()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnPoissonShotMultipleWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 64, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var playerEntity = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>()).GetSingletonEntity();
                if (em.HasComponent<PlayerGoSyncComponent>(playerEntity))
                    em.RemoveComponent<PlayerGoSyncComponent>(playerEntity);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

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
                poissonPattern.BurstShotsPerEvent = 3;
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
                Assert.That(pending % 3, Is.EqualTo(0), "Poisson pending shots should follow BurstShotsPerEvent multiples.");
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
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                em.SetComponentData(source, new SourceAnchorComponent { Position = new float3(0f, 0f, 0f) });
                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(2f, 2f),
                    ComputedArea = 4f,
                });

                var requests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
                requests.Clear();
                requests.Add(new SourceSpawnRequestBuffer
                {
                    DirectiveId = 8001,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    DirectionMode = SourceSpawnDirectionModeId.Fixed,
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

                em.SetComponentData(source, new SourceAnchorComponent { Position = new float3(10f, 0f, 0f) });

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
        public void SpawnExecution_TimedLineEven_KeepsInitialCenterWhenSourceMoves()
        {
            try
            {
                using var world = CreateDefaultTestWorld("SpawnTimedLineEvenAnchorWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 16, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 8, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                em.SetComponentData(source, new SourceAnchorComponent { Position = float3.zero });
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
                    DirectiveId = 8002,
                    BulletTypeKey = 1,
                    SamplingMode = SourceSpawnSamplingModeId.LineEven,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    LineStart = new float2(-1f, 0f),
                    LineEnd = new float2(1f, 0f),
                    SampleSpacing = 2f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    DirectionMode = SourceSpawnDirectionModeId.Fixed,
                    BaseAngleDeg = 0f,
                    Count = 2,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.2f,
                    OldestFrame = 0u,
                });

                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                em.SetComponentData(source, new SourceAnchorComponent { Position = new float3(10f, 0f, 0f) });

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
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 8192, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                EnableV3Source(em, source, stableId: 101u, activeState: SourceStateId.Normal);

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

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
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    DirectionMode = SourceSpawnDirectionModeId.Random,
                    FixedPoint = float2.zero,
                    SpawnOffset = float2.zero,
                    LineStart = float2.zero,
                    LineEnd = float2.zero,
                    SampleSpacing = 1f,
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 0f,
                    BaseAngleDeg = 0f,
                    NWayCount = 1,
                    SpiralStepDeg = 0f,
                    BurstShotsPerEvent = 1,
                    SpawnPriority = 1,
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

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

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
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 8192, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);
                EnableV3Source(em, source, stableId: 102u, activeState: SourceStateId.Normal);

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

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

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });

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
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });
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
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 32768, maxPendingAgeFrames: 120);
                var source = CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Rectangle,
                    Radius = 0f,
                    Size = new float2(1f, 1f),
                    ComputedArea = 1f,
                });
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
                em.SetComponentData(source, new SourceAnchorComponent
                {
                    Position = float3.zero,
                });
                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Circle,
                    Radius = 3f,
                    Size = float2.zero,
                    ComputedArea = math.PI * 9f,
                });
                em.SetComponentData(source, new SourceRunDirectorStateComponent
                {
                    State = RunDirectorSourceStateId.Baseline,
                    SelectedClipState = SourceStateId.Normal,
                    PressureOccupancySec = 0f,
                    DensityScale = 0.4f,
                    Version = 1u,
                });

                SetPlayerPosition(em, float3.zero);
                world.SetTime(new TimeData(0.1d, 0.1f));
                simGroup.Update();

                var directorAfterEnter = em.GetComponentData<SourceRunDirectorStateComponent>(source);
                Assert.That(directorAfterEnter.State, Is.EqualTo(RunDirectorSourceStateId.Pressure), "Player entering source area should switch to Pressure immediately");

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
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorStageIdleToRunningWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

                var stageConfigEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageConfigComponent>()).GetSingletonEntity();
                em.SetComponentData(stageConfigEntity, new RunDirectorStageConfigComponent
                {
                    InitialState = RunDirectorStageStateId.Idle,
                    MinIdleDurationSec = 0.5f,
                    ClearAutoAdvanceTimeoutSec = 5f,
                });

                var stageStateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageStateComponent>()).GetSingletonEntity();
                em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                    StateElapsedSec = 0f,
                    EnteredFrame = 0u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.None,
                });

                var gateEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageGateComponent>()).GetSingletonEntity();
                em.SetComponentData(gateEntity, new RunDirectorStageGateComponent
                {
                    IntroPresentationDone = 0,
                    ClearPresentationDone = 1,
                    MinIdleDurationElapsed = 0,
                    AutoAdvanceTimeoutElapsed = 0,
                });

                var requestEntity = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageRequestComponent>()).GetSingletonEntity();
                em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
                {
                    StageStartRequested = 1,
                    ConfirmPressed = 0,
                });

                world.SetTime(new TimeData(0.2d, 0.2f));
                simGroup.Update();
                Assert.That(em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity).State, Is.EqualTo(RunDirectorStageStateId.Idle));

                var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);
                gate.IntroPresentationDone = 1;
                em.SetComponentData(gateEntity, gate);

                world.SetTime(new TimeData(0.4d, 0.2f));
                simGroup.Update();
                Assert.That(em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity).State, Is.EqualTo(RunDirectorStageStateId.Idle), "MinIdle duration not reached yet");

                world.SetTime(new TimeData(0.6d, 0.2f));
                simGroup.Update();
                var stageAfterRun = em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity);
                Assert.That(stageAfterRun.State, Is.EqualTo(RunDirectorStageStateId.Running));
                Assert.That(stageAfterRun.LastTransitionReason, Is.EqualTo(RunDirectorStageTransitionReasonId.StartRequested));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }
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
        public void RunDirectorStage_ClearReadyToCompleted_AllowsAutoAdvanceTimeout()
        {
            try
            {
                using var world = CreateDefaultTestWorld("RunDirectorStageClearReadyTimeoutWorld", out var simGroup);
                var em = world.EntityManager;

                var bulletPrefab = CreateBulletPrefab(em, typeKey: 1, lifetime: 5f);
                CreatePoolRegistry(em, bulletPrefab, typeKey: 1, poolSize: 32, lifetime: 5f);
                CreatePlayer(em);
                CreateConfigSingletons(em, budgetPerFrame: 0, maxPendingCount: 1024, maxPendingAgeFrames: 120);
                CreateSource(em, typeKey: 1, spawnDensityPerSecPerArea: 0f);

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

                em.SetComponentData(source, new SourceAnchorComponent { Position = float3.zero });
                em.SetComponentData(source, new BulletFieldAreaComponent
                {
                    Shape = BulletFieldShapeId.Circle,
                    Radius = 3f,
                    Size = float2.zero,
                    ComputedArea = math.PI * 9f,
                });
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
            var player = em.CreateEntity(typeof(PlayerTag), typeof(PlayerGoSyncComponent));
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
        }

        private static void CreatePlayerWithTransform(EntityManager em, float3 position)
        {
            var player = em.CreateEntity(typeof(PlayerTag), typeof(LocalTransform), typeof(PlayerGoSyncComponent));
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

            em.AddBuffer<SourceSpawnRequestBuffer>(source);
            em.AddBuffer<SourceActiveBulletCountBuffer>(source);
            EnableV3Source(em, source, stableId: (uint)math.max(1, source.Index + 1), activeState: SourceStateId.Normal);

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
                Phase = phase,
                Lane = lane,
                TriggerState = triggerState,
                LocalStartSec = startSec,
                LocalEndSec = endSec,
                BulletTypeKey = 1,
                EmissionMode = SourceSpawnEmissionModeId.RateField,
                SpawnMode = SourceSpawnModeId.FixedDensity,
                SamplingMode = SourceSpawnSamplingModeId.UniformField,
                CenterMode = SourceSpawnCenterModeId.SourceCenter,
                DirectionMode = SourceSpawnDirectionModeId.Random,
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
                NWayCount = 1,
                SpiralStepDeg = 0f,
                SpawnDensityPerSecPerArea = ratePerSecPerArea,
                MeanEventsPerSec = 0f,
                BurstRepeatCount = 1,
                BurstIntervalSec = 1f,
                BurstShotsPerEvent = 1,
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
