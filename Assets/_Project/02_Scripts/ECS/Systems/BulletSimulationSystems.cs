using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    // ----------------------------------------------------------------------
    // Simulation: Move/Lifetime + BulletBlock + SpatialHash Build (Owner)
    // ----------------------------------------------------------------------

    [BurstCompile]
    [UpdateInGroup(typeof(BulletSimulationGroup))]
    public partial struct BulletSimulationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFieldConfigComponent>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        // Entities 1.4.4에서 WithPresent + EnabledRefRW IJobEntity의 implicit scheduling을
        // Burst-compiled OnUpdate에서 수행하면 생성 코드가 NullReferenceException을 낸다.
        // Orchestration은 managed로 유지하고, 실제 대량 순회 job은 각각 Burst 컴파일한다.
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float dt))
                return;
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            var bulletRadiusLookup = SystemAPI.GetComponentLookup<BulletRadiusComponent>(isReadOnly: true);
            bulletRadiusLookup.Update(ref state);
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            bool hasPlayerPosition = SystemAPI.HasComponent<LocalTransform>(playerEntity);
            float2 playerPositionXZ = hasPlayerPosition
                ? ToXZ(SystemAPI.GetComponent<LocalTransform>(playerEntity).Position)
                : float2.zero;
            var runtimeGridCellLookup = SystemAPI.GetBufferLookup<StageRuntimeGridCellBufferElement>(isReadOnly: true);
            runtimeGridCellLookup.Update(ref state);
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            bool hasStageState = SystemAPI.TryGetSingleton<RunDirectorStageStateComponent>(out var stageState);
            bool allowBulletBlock = !hasTopologyState
                || (hasStageState && StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState));
            StageRuntimeGridComponent runtimeGrid = default;
            Entity runtimeGridEntity = Entity.Null;
            bool shouldEvaluateBulletBlock = allowBulletBlock
                && SystemAPI.TryGetSingleton<StageRuntimeGridComponent>(out runtimeGrid)
                && StageRuntimeGridUtility.IsReady(in runtimeGrid);
            if (shouldEvaluateBulletBlock)
            {
                runtimeGridEntity = SystemAPI.GetSingletonEntity<StageRuntimeGridComponent>();
                if (!state.EntityManager.HasBuffer<StageRuntimeGridCellBufferElement>(runtimeGridEntity))
                {
                    shouldEvaluateBulletBlock = false;
                }
                else
                {
                    var runtimeGridCells = state.EntityManager
                        .GetBuffer<StageRuntimeGridCellBufferElement>(runtimeGridEntity, isReadOnly: true);
                    shouldEvaluateBulletBlock = runtimeGridCells.Length == (runtimeGrid.Width * runtimeGrid.Height);
                }
            }
            uint currentFrame = 0u;
            if (SystemAPI.TryGetSingleton<BulletFrameCounterComponent>(out var frameCounter))
                currentFrame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);

            // 1) Move + Lifetime + BulletBlock (활성 탄만). 만료/차단/정지 완료 시 lifecycle request를 남긴다.
            var runtimeMovementHandle = new RuntimeMovementAndLifetimeJob
            {
                DeltaTime = dt,
                CurrentFrame = currentFrame,
                HasPlayerPosition = hasPlayerPosition,
                PlayerPositionXZ = playerPositionXZ,
                EvaluateBulletBlock = shouldEvaluateBulletBlock,
                RuntimeGrid = runtimeGrid,
                RuntimeGridEntity = runtimeGridEntity,
                RuntimeGridCellLookup = runtimeGridCellLookup,
                BulletRadiusLookup = bulletRadiusLookup,
            }.ScheduleParallel(state.Dependency);

            var linearHandle = new LinearMoveAndLifetimeJob
            {
                DeltaTime = dt,
                CurrentFrame = currentFrame,
                EvaluateBulletBlock = shouldEvaluateBulletBlock,
                RuntimeGrid = runtimeGrid,
                RuntimeGridEntity = runtimeGridEntity,
                RuntimeGridCellLookup = runtimeGridCellLookup,
                BulletRadiusLookup = bulletRadiusLookup,
            }.ScheduleParallel(runtimeMovementHandle);

            // NOTE:
            // movement family queries are disjoint, but the jobs write the same component types.
            // Keep the explicit in-order chain and the existing CellMap build dependency.
            var dampedHandle = new DampedMoveAndLifetimeJob
            {
                DeltaTime = dt,
                CurrentFrame = currentFrame,
                EvaluateBulletBlock = shouldEvaluateBulletBlock,
                RuntimeGrid = runtimeGrid,
                RuntimeGridEntity = runtimeGridEntity,
                RuntimeGridCellLookup = runtimeGridCellLookup,
                BulletRadiusLookup = bulletRadiusLookup,
            }.ScheduleParallel(linearHandle);
            var homingHandle = new HomingLiteMoveAndLifetimeJob
            {
                DeltaTime = dt,
                CurrentFrame = currentFrame,
                HasPlayerPosition = hasPlayerPosition,
                PlayerPositionXZ = playerPositionXZ,
                EvaluateBulletBlock = shouldEvaluateBulletBlock,
                RuntimeGrid = runtimeGrid,
                RuntimeGridEntity = runtimeGridEntity,
                RuntimeGridCellLookup = runtimeGridCellLookup,
                BulletRadiusLookup = bulletRadiusLookup,
            }.ScheduleParallel(dampedHandle);
            state.Dependency = homingHandle;

            // 2) SpatialHash Build
            // - CellMap: 전체 활성 탄
            // - HazardCellMap: 위험탄(BulletHazardTag enabled)만
            // - SharedStatic이므로 이전 프레임/요청 시스템의 read가 끝난 뒤 Clear/Build해야 한다.
            var cellMapDeps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence);

            var clearHandle = new ClearCellMapJob
            {
                CellMap = BulletFieldShared.CellMap
            }.Schedule(cellMapDeps);
            var clearHazardHandle = new ClearCellMapJob
            {
                CellMap = BulletFieldShared.HazardCellMap
            }.Schedule(cellMapDeps);

            var clearDeps = JobHandle.CombineDependencies(clearHandle, clearHazardHandle);
            var buildDeps = JobHandle.CombineDependencies(state.Dependency, clearDeps);
            var buildHandle = new BuildSpatialHashJob
            {
                InvCellSize = cfg.InvCellSize,
                Writer = BulletFieldShared.CellMap.AsParallelWriter()
            }.ScheduleParallel(buildDeps);
            var buildHazardHandle = new BuildHazardSpatialHashJob
            {
                InvCellSize = cfg.InvCellSize,
                Writer = BulletFieldShared.HazardCellMap.AsParallelWriter()
            }.ScheduleParallel(buildDeps);

            state.Dependency = JobHandle.CombineDependencies(buildHandle, buildHazardHandle);
            BulletFieldShared.CellMapFence = state.Dependency; // RequestGroup이 이 fence에 의존하도록
        }

        [BurstCompile]
        [WithPresent(typeof(BulletDespawnRequestTag))]
        private partial struct RuntimeMovementAndLifetimeJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentFrame;
            public bool HasPlayerPosition;
            public float2 PlayerPositionXZ;
            public bool EvaluateBulletBlock;
            public StageRuntimeGridComponent RuntimeGrid;
            public Entity RuntimeGridEntity;
            [ReadOnly] public BufferLookup<StageRuntimeGridCellBufferElement> RuntimeGridCellLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;

            private void Execute(
                Entity e,
                ref LocalTransform tx,
                ref BulletVelocityComponent vel,
                ref BulletLifetimeComponent life,
                EnabledRefRW<BulletDespawnRequestTag> despawnRequest,
                ref BulletLifecycleRequestComponent lifecycleRequest,
                ref BulletLifecycleContactComponent lifecycleContact,
                in BulletSpeedComponent speed,
                in BulletMovementRuntimeComponent movement,
                in BulletActiveTag _)
            {
                float3 previousPosition = tx.Position;
                float2 previousVelocity = vel.Value;
                float2 nextVelocity = previousVelocity;

                if (movement.Family == BulletMovementFamilyId.HomingLite)
                {
                    var homingMotion = movement.HomingLite;
                    if (math.lengthsq(previousVelocity) > 1e-8f &&
                        speed.Value > 0f &&
                        HasPlayerPosition)
                    {
                        float2 bulletPosition = ToXZ(previousPosition);
                        float2 toPlayer = PlayerPositionXZ - bulletPosition;
                        float distanceSq = math.lengthsq(toPlayer);
                        float minDistance = math.max(0f, homingMotion.MinRetargetDistance);
                        float maxDistance = math.max(minDistance, homingMotion.MaxAcquireDistance);
                        if (distanceSq >= (minDistance * minDistance) &&
                            distanceSq <= (maxDistance * maxDistance) &&
                            distanceSq > 1e-8f)
                        {
                            float2 desiredDirection = math.normalize(toPlayer);
                            float2 currentDirection = math.normalize(previousVelocity);
                            float maxTurnRad = math.radians(math.max(0f, homingMotion.TurnRateDegPerSec)) * DeltaTime;
                            float2 finalDirection = RotateTowards(currentDirection, desiredDirection, maxTurnRad);
                            nextVelocity = finalDirection * speed.Value;
                        }
                    }

                    vel.Value = nextVelocity;
                    tx.Position += new float3(nextVelocity.x, 0f, nextVelocity.y) * DeltaTime;
                }
                else
                {
                    tx.Position += new float3(previousVelocity.x, 0f, previousVelocity.y) * DeltaTime;
                }

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                        BulletLifecycleReasonId.LifetimeExpired,
                        Entity.Null,
                        CurrentFrame,
                        new float2(tx.Position.x, tx.Position.z),
                        movement.Family == BulletMovementFamilyId.HomingLite ? nextVelocity : previousVelocity,
                        despawnRequest,
                        ref lifecycleRequest,
                        ref lifecycleContact);
                    return;
                }

                if (movement.Family == BulletMovementFamilyId.DampedLinear)
                {
                    var dampedMotion = movement.DampedLinear;
                    float dampingFactor = math.exp(-math.max(0f, dampedMotion.DampingPerSec) * DeltaTime);
                    float2 dampedVelocity = previousVelocity * dampingFactor;
                    vel.Value = dampedVelocity;

                    float stopThreshold = math.max(0f, dampedMotion.StopSpeedThreshold);
                    if (math.lengthsq(dampedVelocity) <= stopThreshold * stopThreshold)
                    {
                        vel.Value = float2.zero;
                        float2 motionDirection = math.lengthsq(dampedVelocity) > 1e-8f
                            ? dampedVelocity
                            : previousVelocity;
                        BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                            BulletLifecycleReasonId.MotionCompleted,
                            Entity.Null,
                            CurrentFrame,
                            new float2(tx.Position.x, tx.Position.z),
                            motionDirection,
                            despawnRequest,
                            ref lifecycleRequest,
                            ref lifecycleContact);
                        return;
                    }

                    nextVelocity = dampedVelocity;
                }

                if (!EvaluateBulletBlock
                    || !BulletLifecycleRequestUtility.CanPromoteLifecycleRequest(
                        BulletLifecycleReasonId.StageBlocked,
                        despawnRequest.ValueRO,
                        in lifecycleRequest))
                    return;

                float bulletRadius = 0f;
                if (BulletRadiusLookup.HasComponent(e))
                    bulletRadius = math.max(0f, BulletRadiusLookup[e].Value);

                float2 prevXZ = new float2(previousPosition.x, previousPosition.z);
                float2 nextXZ = new float2(tx.Position.x, tx.Position.z);
                if (!RuntimeGridCellLookup.HasBuffer(RuntimeGridEntity))
                    return;

                var runtimeGridCells = RuntimeGridCellLookup[RuntimeGridEntity];
                if (!StageRuntimeBlockQuery.HitsBulletFullCell(prevXZ, nextXZ, bulletRadius, in RuntimeGrid, runtimeGridCells))
                    return;

                BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                    BulletLifecycleReasonId.StageBlocked,
                    Entity.Null,
                    CurrentFrame,
                    nextXZ,
                    movement.Family == BulletMovementFamilyId.Linear ? previousVelocity : nextVelocity,
                    despawnRequest,
                    ref lifecycleRequest,
                    ref lifecycleContact);
            }
        }

        [BurstCompile]
        [WithPresent(typeof(BulletDespawnRequestTag))]
        [WithNone(typeof(BulletDampedMotionComponent), typeof(BulletHomingLiteMotionComponent), typeof(BulletMovementRuntimeComponent))]
        private partial struct LinearMoveAndLifetimeJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentFrame;
            public bool EvaluateBulletBlock;
            public StageRuntimeGridComponent RuntimeGrid;
            public Entity RuntimeGridEntity;
            [ReadOnly] public BufferLookup<StageRuntimeGridCellBufferElement> RuntimeGridCellLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;

            private void Execute(
                Entity e,
                ref LocalTransform tx,
                ref BulletLifetimeComponent life,
                EnabledRefRW<BulletDespawnRequestTag> despawnRequest,
                ref BulletLifecycleRequestComponent lifecycleRequest,
                ref BulletLifecycleContactComponent lifecycleContact,
                in BulletVelocityComponent vel,
                in BulletActiveTag _)
            {
                float3 previousPosition = tx.Position;
                tx.Position += new float3(vel.Value.x, 0f, vel.Value.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                        BulletLifecycleReasonId.LifetimeExpired,
                        Entity.Null,
                        CurrentFrame,
                        new float2(tx.Position.x, tx.Position.z),
                        vel.Value,
                        despawnRequest,
                        ref lifecycleRequest,
                        ref lifecycleContact);
                    return;
                }

                if (!EvaluateBulletBlock
                    || !BulletLifecycleRequestUtility.CanPromoteLifecycleRequest(
                        BulletLifecycleReasonId.StageBlocked,
                        despawnRequest.ValueRO,
                        in lifecycleRequest))
                    return;

                float bulletRadius = 0f;
                if (BulletRadiusLookup.HasComponent(e))
                    bulletRadius = math.max(0f, BulletRadiusLookup[e].Value);

                float2 prevXZ = new float2(previousPosition.x, previousPosition.z);
                float2 nextXZ = new float2(tx.Position.x, tx.Position.z);
                if (!RuntimeGridCellLookup.HasBuffer(RuntimeGridEntity))
                    return;

                var runtimeGridCells = RuntimeGridCellLookup[RuntimeGridEntity];
                if (!StageRuntimeBlockQuery.HitsBulletFullCell(prevXZ, nextXZ, bulletRadius, in RuntimeGrid, runtimeGridCells))
                    return;

                BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                    BulletLifecycleReasonId.StageBlocked,
                    Entity.Null,
                    CurrentFrame,
                    nextXZ,
                    vel.Value,
                    despawnRequest,
                    ref lifecycleRequest,
                    ref lifecycleContact);
            }
        }

        [BurstCompile]
        [WithPresent(typeof(BulletDespawnRequestTag))]
        [WithAll(typeof(BulletDampedMotionComponent))]
        [WithNone(typeof(BulletHomingLiteMotionComponent), typeof(BulletMovementRuntimeComponent))]
        private partial struct DampedMoveAndLifetimeJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentFrame;
            public bool EvaluateBulletBlock;
            public StageRuntimeGridComponent RuntimeGrid;
            public Entity RuntimeGridEntity;
            [ReadOnly] public BufferLookup<StageRuntimeGridCellBufferElement> RuntimeGridCellLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;

            private void Execute(
                Entity e,
                ref LocalTransform tx,
                ref BulletVelocityComponent vel,
                ref BulletLifetimeComponent life,
                EnabledRefRW<BulletDespawnRequestTag> despawnRequest,
                ref BulletLifecycleRequestComponent lifecycleRequest,
                ref BulletLifecycleContactComponent lifecycleContact,
                in BulletDampedMotionComponent dampedMotion,
                in BulletActiveTag _)
            {
                float3 previousPosition = tx.Position;
                float2 previousVelocity = vel.Value;
                tx.Position += new float3(previousVelocity.x, 0f, previousVelocity.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                        BulletLifecycleReasonId.LifetimeExpired,
                        Entity.Null,
                        CurrentFrame,
                        new float2(tx.Position.x, tx.Position.z),
                        previousVelocity,
                        despawnRequest,
                        ref lifecycleRequest,
                        ref lifecycleContact);
                    return;
                }

                float dampingFactor = math.exp(-math.max(0f, dampedMotion.DampingPerSec) * DeltaTime);
                float2 dampedVelocity = previousVelocity * dampingFactor;
                vel.Value = dampedVelocity;

                float stopThreshold = math.max(0f, dampedMotion.StopSpeedThreshold);
                if (math.lengthsq(dampedVelocity) <= stopThreshold * stopThreshold)
                {
                    vel.Value = float2.zero;
                    float2 motionDirection = math.lengthsq(dampedVelocity) > 1e-8f
                        ? dampedVelocity
                        : previousVelocity;
                    BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                        BulletLifecycleReasonId.MotionCompleted,
                        Entity.Null,
                        CurrentFrame,
                        new float2(tx.Position.x, tx.Position.z),
                        motionDirection,
                        despawnRequest,
                        ref lifecycleRequest,
                        ref lifecycleContact);
                    return;
                }

                if (!EvaluateBulletBlock
                    || !BulletLifecycleRequestUtility.CanPromoteLifecycleRequest(
                        BulletLifecycleReasonId.StageBlocked,
                        despawnRequest.ValueRO,
                        in lifecycleRequest))
                    return;

                float bulletRadius = 0f;
                if (BulletRadiusLookup.HasComponent(e))
                    bulletRadius = math.max(0f, BulletRadiusLookup[e].Value);

                float2 prevXZ = new float2(previousPosition.x, previousPosition.z);
                float2 nextXZ = new float2(tx.Position.x, tx.Position.z);
                if (!RuntimeGridCellLookup.HasBuffer(RuntimeGridEntity))
                    return;

                var runtimeGridCells = RuntimeGridCellLookup[RuntimeGridEntity];
                if (!StageRuntimeBlockQuery.HitsBulletFullCell(prevXZ, nextXZ, bulletRadius, in RuntimeGrid, runtimeGridCells))
                    return;

                BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                    BulletLifecycleReasonId.StageBlocked,
                    Entity.Null,
                    CurrentFrame,
                    nextXZ,
                    dampedVelocity,
                    despawnRequest,
                    ref lifecycleRequest,
                    ref lifecycleContact);
            }
        }

        [BurstCompile]
        [WithPresent(typeof(BulletDespawnRequestTag))]
        [WithAll(typeof(BulletHomingLiteMotionComponent))]
        [WithNone(typeof(BulletDampedMotionComponent), typeof(BulletMovementRuntimeComponent))]
        private partial struct HomingLiteMoveAndLifetimeJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentFrame;
            public bool HasPlayerPosition;
            public float2 PlayerPositionXZ;
            public bool EvaluateBulletBlock;
            public StageRuntimeGridComponent RuntimeGrid;
            public Entity RuntimeGridEntity;
            [ReadOnly] public BufferLookup<StageRuntimeGridCellBufferElement> RuntimeGridCellLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;

            private void Execute(
                Entity e,
                ref LocalTransform tx,
                ref BulletVelocityComponent vel,
                ref BulletLifetimeComponent life,
                EnabledRefRW<BulletDespawnRequestTag> despawnRequest,
                ref BulletLifecycleRequestComponent lifecycleRequest,
                ref BulletLifecycleContactComponent lifecycleContact,
                in BulletSpeedComponent speed,
                in BulletHomingLiteMotionComponent homingMotion,
                in BulletActiveTag _)
            {
                float3 previousPosition = tx.Position;
                float2 previousVelocity = vel.Value;
                float2 nextVelocity = previousVelocity;

                if (math.lengthsq(previousVelocity) > 1e-8f &&
                    speed.Value > 0f &&
                    HasPlayerPosition)
                {
                    float2 bulletPosition = ToXZ(previousPosition);
                    float2 toPlayer = PlayerPositionXZ - bulletPosition;
                    float distanceSq = math.lengthsq(toPlayer);
                    float minDistance = math.max(0f, homingMotion.MinRetargetDistance);
                    float maxDistance = math.max(minDistance, homingMotion.MaxAcquireDistance);
                    if (distanceSq >= (minDistance * minDistance) &&
                        distanceSq <= (maxDistance * maxDistance) &&
                        distanceSq > 1e-8f)
                    {
                        float2 desiredDirection = math.normalize(toPlayer);
                        float2 currentDirection = math.normalize(previousVelocity);
                        float maxTurnRad = math.radians(math.max(0f, homingMotion.TurnRateDegPerSec)) * DeltaTime;
                        float2 finalDirection = RotateTowards(currentDirection, desiredDirection, maxTurnRad);
                        nextVelocity = finalDirection * speed.Value;
                    }
                }

                vel.Value = nextVelocity;
                tx.Position += new float3(nextVelocity.x, 0f, nextVelocity.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                        BulletLifecycleReasonId.LifetimeExpired,
                        Entity.Null,
                        CurrentFrame,
                        new float2(tx.Position.x, tx.Position.z),
                        nextVelocity,
                        despawnRequest,
                        ref lifecycleRequest,
                        ref lifecycleContact);
                    return;
                }

                if (!EvaluateBulletBlock
                    || !BulletLifecycleRequestUtility.CanPromoteLifecycleRequest(
                        BulletLifecycleReasonId.StageBlocked,
                        despawnRequest.ValueRO,
                        in lifecycleRequest))
                    return;

                float bulletRadius = 0f;
                if (BulletRadiusLookup.HasComponent(e))
                    bulletRadius = math.max(0f, BulletRadiusLookup[e].Value);

                float2 prevXZ = new float2(previousPosition.x, previousPosition.z);
                float2 nextXZ = new float2(tx.Position.x, tx.Position.z);
                if (!RuntimeGridCellLookup.HasBuffer(RuntimeGridEntity))
                    return;

                var runtimeGridCells = RuntimeGridCellLookup[RuntimeGridEntity];
                if (!StageRuntimeBlockQuery.HitsBulletFullCell(prevXZ, nextXZ, bulletRadius, in RuntimeGrid, runtimeGridCells))
                    return;

                BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                    BulletLifecycleReasonId.StageBlocked,
                    Entity.Null,
                    CurrentFrame,
                    nextXZ,
                    nextVelocity,
                    despawnRequest,
                    ref lifecycleRequest,
                    ref lifecycleContact);
            }
        }

        private static float2 ToXZ(float3 position) => new(position.x, position.z);

        private static float2 RotateTowards(float2 currentDirection, float2 desiredDirection, float maxTurnRad)
        {
            if (maxTurnRad <= 0f)
                return currentDirection;

            float currentAngle = math.atan2(currentDirection.y, currentDirection.x);
            float desiredAngle = math.atan2(desiredDirection.y, desiredDirection.x);
            float delta = math.atan2(math.sin(desiredAngle - currentAngle), math.cos(desiredAngle - currentAngle));
            float clampedDelta = math.clamp(delta, -maxTurnRad, maxTurnRad);
            float finalAngle = currentAngle + clampedDelta;
            return new float2(math.cos(finalAngle), math.sin(finalAngle));
        }

        [BurstCompile]
        private struct ClearCellMapJob : IJob
        {
            public NativeParallelMultiHashMap<int, Entity> CellMap;
            public void Execute() => CellMap.Clear();
        }

        [BurstCompile]
        private partial struct BuildSpatialHashJob : IJobEntity
        {
            public float InvCellSize;
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter Writer;

            private void Execute(Entity e, in LocalTransform tx, in BulletActiveTag _)
            {
                var cell = SpatialHashUtility.ToCell(tx.Position, InvCellSize);
                Writer.Add(SpatialHashUtility.Hash(cell), e);
            }
        }

        [BurstCompile]
        private partial struct BuildHazardSpatialHashJob : IJobEntity
        {
            public float InvCellSize;
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter Writer;

            private void Execute(Entity e, in LocalTransform tx, in BulletActiveTag _, in BulletHazardTag __)
            {
                var cell = SpatialHashUtility.ToCell(tx.Position, InvCellSize);
                Writer.Add(SpatialHashUtility.Hash(cell), e);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup), OrderLast = true)]
    public partial struct BulletRequestFencePublishSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            BulletFieldShared.CellMapFence = JobHandle.CombineDependencies(
                BulletFieldShared.CellMapFence,
                state.Dependency);
        }
    }
}
