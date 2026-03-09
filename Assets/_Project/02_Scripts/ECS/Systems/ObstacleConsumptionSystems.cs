using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(PlayerFixedStepGroup))]
    [UpdateAfter(typeof(ReplayTickInputApplySystem))]
    [UpdateBefore(typeof(PlayerIntentMovementSystem))]
    public partial struct PlayerPreviousPositionCaptureSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerPreviousPositionComponent>();
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            foreach (var (tx, previous) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PlayerPreviousPositionComponent>>().WithAll<PlayerTag>())
            {
                previous.ValueRW.Position = tx.ValueRO.Position;
            }
        }
    }

    [UpdateInGroup(typeof(PlayerFixedStepGroup))]
    [UpdateAfter(typeof(PlayerIntentMovementSystem))]
    [UpdateBefore(typeof(PlayerIntentConsumeSystem))]
    public partial struct PlayerObstacleBlockSystem : ISystem
    {
        private struct ObstacleSnapshot
        {
            public LocalTransform Transform;
            public Shape2DComponent Shape;
        }

        private EntityQuery _obstacleQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerRadiusComponent>();
            state.RequireForUpdate<PlayerPreviousPositionComponent>();
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();

            _obstacleQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<StageTopologyObstacleTag>(),
                ComponentType.ReadOnly<ObstacleCollisionMaskComponent>(),
                ComponentType.ReadOnly<ObstacleGeometryComponent>(),
                ComponentType.ReadOnly<Shape2DComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            bool hasStageState = SystemAPI.TryGetSingleton<RunDirectorStageStateComponent>(out var stageState);
            if (hasTopologyState
                && (!hasStageState || !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState)))
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            using var obstacles = CollectObstacles(state.EntityManager, _obstacleQuery, ObstacleCollisionMask.BlockPlayer);
            if (obstacles.Length <= 0)
                return;

            foreach (var (tx, previous, radius) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerPreviousPositionComponent>, RefRO<PlayerRadiusComponent>>().WithAll<PlayerTag>())
            {
                float3 prev = previous.ValueRO.Position;
                float3 next = tx.ValueRO.Position;
                float2 prevXZ = new float2(prev.x, prev.z);
                float2 nextXZ = new float2(next.x, next.z);
                float playerRadius = math.max(0f, radius.ValueRO.Value);

                if (IsCandidateValid(nextXZ, playerRadius, obstacles))
                    continue;

                float2 delta = nextXZ - prevXZ;
                float2 xOnly = new float2(nextXZ.x, prevXZ.y);
                float2 zOnly = new float2(prevXZ.x, nextXZ.y);
                bool xValid = IsCandidateValid(xOnly, playerRadius, obstacles);
                bool zValid = IsCandidateValid(zOnly, playerRadius, obstacles);

                float2 resolved = prevXZ;
                if (xValid && zValid)
                {
                    float xDistanceSq = math.lengthsq(xOnly - prevXZ);
                    float zDistanceSq = math.lengthsq(zOnly - prevXZ);
                    if (math.abs(xDistanceSq - zDistanceSq) <= 1e-6f)
                        resolved = math.abs(delta.x) >= math.abs(delta.y) ? xOnly : zOnly;
                    else
                        resolved = xDistanceSq >= zDistanceSq ? xOnly : zOnly;
                }
                else if (xValid)
                {
                    resolved = xOnly;
                }
                else if (zValid)
                {
                    resolved = zOnly;
                }

                var corrected = tx.ValueRO;
                corrected.Position = new float3(resolved.x, prev.y, resolved.y);
                tx.ValueRW = corrected;
            }
        }

        private static NativeList<ObstacleSnapshot> CollectObstacles(EntityManager em, EntityQuery query, ObstacleCollisionMask requiredMask)
        {
            var list = new NativeList<ObstacleSnapshot>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    continue;
                var mask = em.GetComponentData<ObstacleCollisionMaskComponent>(entity);
                if ((mask.Value & requiredMask) == 0)
                    continue;
                var tx = em.GetComponentData<LocalTransform>(entity);
                var shape = em.GetComponentData<Shape2DComponent>(entity);

                list.Add(new ObstacleSnapshot
                {
                    Transform = tx,
                    Shape = shape,
                });
            }

            return list;
        }

        private static bool IsCandidateValid(float2 position, float radius, NativeList<ObstacleSnapshot> obstacles)
        {
            for (int i = 0; i < obstacles.Length; i++)
            {
                var obstacle = obstacles[i];
                if (ObstacleGeometryUtility.OverlapsCircleXZ(position, radius, in obstacle.Transform, in obstacle.Shape))
                    return false;
            }

            return true;
        }
    }

    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(BulletVacuumRequestSystem))]
    [UpdateBefore(typeof(PlayerHazardCollisionRequestSystem))]
    public partial struct BulletObstacleHitRequestSystem : ISystem
    {
        private struct ObstacleSnapshot
        {
            public LocalTransform Transform;
            public Shape2DComponent Shape;
            public int2 MinCell;
            public int2 MaxCell;
        }

        private EntityQuery _obstacleQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletActiveTag>();
            state.RequireForUpdate<BulletDespawnRequestTag>();
            state.RequireForUpdate<BulletFieldConfigComponent>();
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();

            _obstacleQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<StageTopologyObstacleTag>(),
                ComponentType.ReadOnly<ObstacleCollisionMaskComponent>(),
                ComponentType.ReadOnly<ObstacleGeometryComponent>(),
                ComponentType.ReadOnly<Shape2DComponent>(),
                ComponentType.ReadOnly<LocalTransform>());
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            bool hasStageState = SystemAPI.TryGetSingleton<RunDirectorStageStateComponent>(out var stageState);
            if (hasTopologyState
                && (!hasStageState || !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState)))
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            if (!BulletFieldShared.IsInitialized)
                return;

            float invCellSize = SystemAPI.GetSingleton<BulletFieldConfigComponent>().InvCellSize;
            var obstacles = CollectBulletObstacles(state.EntityManager, _obstacleQuery, ObstacleCollisionMask.BlockBullet, invCellSize);
            if (obstacles.Length <= 0)
            {
                obstacles.Dispose();
                return;
            }

            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var activeLookup = SystemAPI.GetComponentLookup<BulletActiveTag>(isReadOnly: true);
            var despawnRequestLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(isReadOnly: false);

            txLookup.Update(ref state);
            activeLookup.Update(ref state);
            despawnRequestLookup.Update(ref state);

            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence);
            state.Dependency = new BulletObstacleHitFromCellMapJob
            {
                CellMap = BulletFieldShared.CellMap,
                Obstacles = obstacles.AsArray(),
                TxLookup = txLookup,
                ActiveLookup = activeLookup,
                DespawnRequestLookup = despawnRequestLookup,
            }.Schedule(deps);
            state.Dependency = obstacles.Dispose(state.Dependency);
        }

        private static NativeList<ObstacleSnapshot> CollectBulletObstacles(
            EntityManager em,
            EntityQuery query,
            ObstacleCollisionMask requiredMask,
            float invCellSize)
        {
            var list = new NativeList<ObstacleSnapshot>(Allocator.TempJob);
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity) || !em.IsEnabled(entity))
                    continue;
                var mask = em.GetComponentData<ObstacleCollisionMaskComponent>(entity);
                if ((mask.Value & requiredMask) == 0)
                    continue;
                var tx = em.GetComponentData<LocalTransform>(entity);
                var shape = em.GetComponentData<Shape2DComponent>(entity);
                ComputeObstacleCellBounds(in tx, in shape, invCellSize, out int2 minCell, out int2 maxCell);

                list.Add(new ObstacleSnapshot
                {
                    Transform = tx,
                    Shape = shape,
                    MinCell = minCell,
                    MaxCell = maxCell,
                });
            }

            return list;
        }

        private static void ComputeObstacleCellBounds(
            in LocalTransform tx,
            in Shape2DComponent shape,
            float invCellSize,
            out int2 minCell,
            out int2 maxCell)
        {
            ComputeObstacleBoundsXZ(in tx, in shape, out float2 min, out float2 max);
            minCell = (int2)math.floor(min * invCellSize);
            maxCell = (int2)math.floor(max * invCellSize);
        }

        private static void ComputeObstacleBoundsXZ(
            in LocalTransform tx,
            in Shape2DComponent shape,
            out float2 min,
            out float2 max)
        {
            Shape2DUtility.ComputeBoundsXZ(in tx, in shape, out min, out max);
        }

        private struct BulletObstacleHitFromCellMapJob : IJob
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, Entity> CellMap;
            [ReadOnly] public NativeArray<ObstacleSnapshot> Obstacles;
            [ReadOnly] public ComponentLookup<LocalTransform> TxLookup;
            [ReadOnly] public ComponentLookup<BulletActiveTag> ActiveLookup;
            public ComponentLookup<BulletDespawnRequestTag> DespawnRequestLookup;

            public void Execute()
            {
                for (int obstacleIndex = 0; obstacleIndex < Obstacles.Length; obstacleIndex++)
                {
                    var obstacle = Obstacles[obstacleIndex];
                    for (int y = obstacle.MinCell.y; y <= obstacle.MaxCell.y; y++)
                    {
                        for (int x = obstacle.MinCell.x; x <= obstacle.MaxCell.x; x++)
                        {
                            int key = SpatialHashUtility.Hash(new int2(x, y));
                            if (!CellMap.TryGetFirstValue(key, out var bullet, out var iterator))
                                continue;

                            do
                            {
                                if (!TxLookup.HasComponent(bullet))
                                    continue;
                                if (!ActiveLookup.HasComponent(bullet) || !ActiveLookup.IsComponentEnabled(bullet))
                                    continue;
                                if (!DespawnRequestLookup.HasComponent(bullet))
                                    continue;
                                if (DespawnRequestLookup.IsComponentEnabled(bullet))
                                    continue;

                                float3 bulletPosition = TxLookup[bullet].Position;
                                float2 point = new float2(bulletPosition.x, bulletPosition.z);
                                if (!ObstacleGeometryUtility.ContainsPointXZ(point, in obstacle.Transform, in obstacle.Shape))
                                    continue;

                                DespawnRequestLookup.SetComponentEnabled(bullet, true);
                            }
                            while (CellMap.TryGetNextValue(out bullet, ref iterator));
                        }
                    }
                }
            }
        }
    }
}
