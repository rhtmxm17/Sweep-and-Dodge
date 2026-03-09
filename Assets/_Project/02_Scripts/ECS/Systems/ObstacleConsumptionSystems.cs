using Unity.Collections;
using Unity.Entities;
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
            public ObstacleGeometryComponent Geometry;
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
                var geometry = em.GetComponentData<ObstacleGeometryComponent>(entity);

                list.Add(new ObstacleSnapshot
                {
                    Transform = tx,
                    Geometry = geometry,
                });
            }

            return list;
        }

        private static bool IsCandidateValid(float2 position, float radius, NativeList<ObstacleSnapshot> obstacles)
        {
            for (int i = 0; i < obstacles.Length; i++)
            {
                var obstacle = obstacles[i];
                if (ObstacleGeometryUtility.OverlapsCircleXZ(position, radius, in obstacle.Transform, in obstacle.Geometry))
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
            public ObstacleGeometryComponent Geometry;
        }

        private EntityQuery _bulletQuery;
        private EntityQuery _obstacleQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletActiveTag>();
            state.RequireForUpdate<BulletDespawnRequestTag>();
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();

            _bulletQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<BulletActiveTag>(),
                    ComponentType.ReadOnly<BulletDespawnRequestTag>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });

            _obstacleQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<StageTopologyObstacleTag>(),
                ComponentType.ReadOnly<ObstacleCollisionMaskComponent>(),
                ComponentType.ReadOnly<ObstacleGeometryComponent>(),
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

            using var obstacles = CollectObstacles(state.EntityManager, _obstacleQuery, ObstacleCollisionMask.BlockBullet);
            if (obstacles.Length <= 0 || _bulletQuery.IsEmptyIgnoreFilter)
                return;

            var em = state.EntityManager;
            using var bullets = _bulletQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < bullets.Length; i++)
            {
                var bullet = bullets[i];
                if (!em.Exists(bullet))
                    continue;
                if (!em.IsEnabled(bullet))
                    continue;
                if (!em.IsComponentEnabled<BulletActiveTag>(bullet))
                    continue;
                if (em.IsComponentEnabled<BulletDespawnRequestTag>(bullet))
                    continue;

                var bulletPosition = em.GetComponentData<LocalTransform>(bullet).Position;
                float2 point = new float2(bulletPosition.x, bulletPosition.z);
                if (!HitsAnyObstacle(point, obstacles))
                    continue;

                em.SetComponentEnabled<BulletDespawnRequestTag>(bullet, true);
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
                var geometry = em.GetComponentData<ObstacleGeometryComponent>(entity);

                list.Add(new ObstacleSnapshot
                {
                    Transform = tx,
                    Geometry = geometry,
                });
            }

            return list;
        }

        private static bool HitsAnyObstacle(float2 point, NativeList<ObstacleSnapshot> obstacles)
        {
            for (int i = 0; i < obstacles.Length; i++)
            {
                var obstacle = obstacles[i];
                if (ObstacleGeometryUtility.ContainsPointXZ(point, in obstacle.Transform, in obstacle.Geometry))
                    return true;
            }

            return false;
        }
    }
}
