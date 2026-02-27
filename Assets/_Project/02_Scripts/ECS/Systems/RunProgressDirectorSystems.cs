using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateBefore(typeof(SourceClipRequestBuildSystem))]
    public partial struct RunProgressDirectorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceStableIdComponent>();
            state.RequireForUpdate<SourceAnchorComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceRunDirectorStateComponent>();
            state.RequireForUpdate<RunProgressDirectorConfigComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            bool hasPlayerSync = SystemAPI.HasComponent<PlayerGoSyncComponent>(playerEntity);
            bool hasPlayerTransform = SystemAPI.HasComponent<LocalTransform>(playerEntity);
            if (!hasPlayerSync && !hasPlayerTransform)
                return;

            var config = SystemAPI.GetSingleton<RunProgressDirectorConfigComponent>();
            float holdSec = math.max(0f, config.PressureHoldSec);
            float baselineScale = math.max(0f, config.BaselineTrashDensityScale);
            float pressureScale = math.max(0f, config.PressureDensityScale);
            float deltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
            float3 playerPosition = hasPlayerSync
                ? SystemAPI.GetComponent<PlayerGoSyncComponent>(playerEntity).Position
                : SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(true);
            var stableIdLookup = SystemAPI.GetComponentLookup<SourceStableIdComponent>(true);
            var anchorLookup = SystemAPI.GetComponentLookup<SourceAnchorComponent>(true);
            var areaLookup = SystemAPI.GetComponentLookup<BulletFieldAreaComponent>(true);
            var directorLookup = SystemAPI.GetComponentLookup<SourceRunDirectorStateComponent>(false);

            sourceLookup.Update(ref state);
            stableIdLookup.Update(ref state);
            anchorLookup.Update(ref state);
            areaLookup.Update(ref state);
            directorLookup.Update(ref state);

            var sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<SourceSpawnComponent>()
                .WithAll<SourceStableIdComponent>()
                .WithAll<SourceAnchorComponent>()
                .WithAll<BulletFieldAreaComponent>()
                .WithAll<SourceRunDirectorStateComponent>()
                .Build();

            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);
            Entity pressureEntity = Entity.Null;
            float bestPressureScore = float.MinValue;
            uint bestStableId = uint.MaxValue;

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                var source = sourceLookup[sourceEntity];
                var director = directorLookup[sourceEntity];
                if (source.State == SourceStateId.Depleted)
                {
                    director.PressureOccupancySec = 0f;
                    directorLookup[sourceEntity] = director;
                    continue;
                }

                bool isOccupied = IsPlayerInsideSourceArea(
                    playerPosition,
                    anchorLookup[sourceEntity].Position,
                    areaLookup[sourceEntity]);
                director.PressureOccupancySec = isOccupied
                    ? director.PressureOccupancySec + deltaTime
                    : 0f;
                directorLookup[sourceEntity] = director;

                if (director.PressureOccupancySec < holdSec)
                    continue;

                uint stableId = math.max(1u, stableIdLookup[sourceEntity].Value);
                float score = director.PressureOccupancySec;
                bool isBetter = score > bestPressureScore;
                bool tie = math.abs(score - bestPressureScore) <= 1e-5f;
                if (isBetter || (tie && stableId < bestStableId))
                {
                    bestPressureScore = score;
                    bestStableId = stableId;
                    pressureEntity = sourceEntity;
                }
            }

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                var source = sourceLookup[sourceEntity];
                var director = directorLookup[sourceEntity];

                RunDirectorSourceStateId nextState;
                if (source.State == SourceStateId.Depleted)
                    nextState = RunDirectorSourceStateId.Finish;
                else if (sourceEntity == pressureEntity)
                    nextState = RunDirectorSourceStateId.Pressure;
                else
                    nextState = RunDirectorSourceStateId.Baseline;

                SourceStateId nextClipState = nextState == RunDirectorSourceStateId.Finish
                    ? SourceStateId.Depleted
                    : source.State;
                float nextDensityScale = nextState == RunDirectorSourceStateId.Baseline
                    ? baselineScale
                    : pressureScale;

                if (director.State != nextState
                    || director.SelectedClipState != nextClipState
                    || math.abs(director.DensityScale - nextDensityScale) > 1e-5f)
                {
                    director.Version += 1u;
                }

                director.State = nextState;
                director.SelectedClipState = nextClipState;
                director.DensityScale = nextDensityScale;
                directorLookup[sourceEntity] = director;
            }
        }

        private static bool IsPlayerInsideSourceArea(
            float3 playerPosition,
            float3 sourcePosition,
            in BulletFieldAreaComponent area)
        {
            float dx = playerPosition.x - sourcePosition.x;
            float dz = playerPosition.z - sourcePosition.z;
            if (area.Shape == BulletFieldShapeId.Rectangle)
            {
                float hx = math.max(0f, area.Size.x * 0.5f);
                float hz = math.max(0f, area.Size.y * 0.5f);
                return math.abs(dx) <= hx && math.abs(dz) <= hz;
            }

            float radius = math.max(0f, area.Radius);
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
