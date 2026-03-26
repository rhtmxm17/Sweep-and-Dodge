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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float dt))
                return;
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            var requestLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(false);
            requestLookup.Update(ref state);
            var bulletRadiusLookup = SystemAPI.GetComponentLookup<BulletRadiusComponent>(isReadOnly: true);
            bulletRadiusLookup.Update(ref state);
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

            // 1) Move + Lifetime + BulletBlock (활성 탄만). 만료/차단 시 디스폰 요청 태그 enable
            state.Dependency = new BulletMoveAndLifetimeJob
            {
                DeltaTime = dt,
                EvaluateBulletBlock = shouldEvaluateBulletBlock,
                RuntimeGrid = runtimeGrid,
                RuntimeGridEntity = runtimeGridEntity,
                RuntimeGridCellLookup = runtimeGridCellLookup,
                BulletRadiusLookup = bulletRadiusLookup,
                RequestLookup = requestLookup,
            }.ScheduleParallel(state.Dependency);

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
        private partial struct BulletMoveAndLifetimeJob : IJobEntity
        {
            public float DeltaTime;
            public bool EvaluateBulletBlock;
            public StageRuntimeGridComponent RuntimeGrid;
            public Entity RuntimeGridEntity;
            [ReadOnly] public BufferLookup<StageRuntimeGridCellBufferElement> RuntimeGridCellLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;
            // 주의: Enableable 토글을 위해 Lookup을 병렬 Job에서 사용.
            // 동일 엔티티에만 접근하므로 안전하지만, 교차 엔티티 write가 섞이면 레이스 위험이 있음.
            [NativeDisableParallelForRestriction] public ComponentLookup<BulletDespawnRequestTag> RequestLookup;

            private void Execute(
                Entity e,
                ref LocalTransform tx,
                ref BulletLifetimeComponent life,
                in BulletVelocityComponent vel,
                in BulletActiveTag _)
            {
                float3 previousPosition = tx.Position;
                tx.Position += new float3(vel.Value.x, 0f, vel.Value.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    if (RequestLookup.HasComponent(e))
                        RequestLookup.SetComponentEnabled(e, true);
                    return;
                }

                if (!EvaluateBulletBlock || !RequestLookup.HasComponent(e) || RequestLookup.IsComponentEnabled(e))
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

                RequestLookup.SetComponentEnabled(e, true);
            }
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
