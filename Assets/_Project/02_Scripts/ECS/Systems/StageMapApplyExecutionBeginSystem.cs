using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// StageMap 런타임 적용 Owner.
    /// - ExecutionBegin에서 StageMap one-shot 요청을 소비한다.
    /// - Source/Deposit layout write 단일 소유를 보장한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(BulletPoolOwnerBootstrapSystem))]
    [UpdateBefore(typeof(BulletFieldAreaUpdateSystem))]
    public partial struct StageMapApplyExecutionBeginSystem : ISystem
    {
        private static readonly float3 DepositSinkPosition = new float3(0f, -10000f, 0f);

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunDirectorStageRequestComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var requestRW = SystemAPI.GetSingletonRW<RunDirectorStageRequestComponent>();
            var request = requestRW.ValueRO;
            if (request.StageMapApplyRequested == 0)
                return;

            // one-shot 요청 소비.
            request.StageMapApplyRequested = 0;
            requestRW.ValueRW = request;

            int requestedStageId = request.RequestedStageId;
            if (requestedStageId <= 0)
            {
                Debug.LogWarning("[StageMapApply] Ignored request with invalid stageId.");
                return;
            }

            var catalogRuntime = TryGetCatalogRuntime(ref state);
            if (catalogRuntime == null || catalogRuntime.Catalog == null)
            {
                Debug.LogWarning($"[StageMapApply] Catalog is missing. stageId={requestedStageId}");
                return;
            }

            if (!TryFindStage(catalogRuntime.Catalog, requestedStageId, out var stage))
            {
                Debug.LogWarning($"[StageMapApply] StageId not found in catalog. stageId={requestedStageId}, catalog={catalogRuntime.Catalog.name}");
                return;
            }

            ApplySourceLayout(ref state, requestedStageId, in stage);
            ApplyDepositLayout(ref state, requestedStageId, in stage);
        }

        private static StageMapCatalogRuntimeComponent TryGetCatalogRuntime(ref SystemState state)
        {
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<StageMapCatalogRuntimeComponent>());
            if (query.IsEmptyIgnoreFilter)
                return null;

            var runtimeEntity = ResolveFirstEntity(query);
            if (runtimeEntity == Entity.Null || !em.Exists(runtimeEntity))
                return null;

            return em.GetComponentObject<StageMapCatalogRuntimeComponent>(runtimeEntity);
        }

        private static bool TryFindStage(StageMapCatalogSO catalog, int stageId, out StageMapDefinition stage)
        {
            stage = default;
            if (catalog == null || catalog.Stages == null)
                return false;

            for (int i = 0; i < catalog.Stages.Length; i++)
            {
                if (catalog.Stages[i].StageId != stageId)
                    continue;

                stage = catalog.Stages[i];
                return true;
            }

            return false;
        }

        private static void ApplySourceLayout(ref SystemState state, int stageId, in StageMapDefinition stage)
        {
            var em = state.EntityManager;
            using var sourceQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceStableIdComponent>(),
                ComponentType.ReadWrite<SourceSpawnComponent>(),
                ComponentType.ReadWrite<SourceAnchorComponent>(),
                ComponentType.ReadWrite<BulletFieldAreaComponent>(),
                ComponentType.ReadWrite<LocalTransform>());
            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);

            var stageById = BuildStageSourceMap(stage.Sources, out int stageDuplicateCount);
            var runtimeById = BuildRuntimeSourceMap(em, sourceEntities, out int runtimeDuplicateCount, out var runtimeDuplicateIds);
            var mappedIds = new HashSet<uint>();

            if (stageDuplicateCount > 0)
                Debug.LogWarning($"[StageMapApply] Duplicate source stableId in catalog stage. stageId={stageId}, duplicateCount={stageDuplicateCount}");

            if (runtimeDuplicateCount > 0)
                Debug.LogWarning($"[StageMapApply] Duplicate runtime source stableId detected. stageId={stageId}, duplicateCount={runtimeDuplicateCount}");

            foreach (var pair in stageById)
            {
                uint stableId = pair.Key;
                if (runtimeDuplicateIds.Contains(stableId))
                    continue;
                if (!runtimeById.TryGetValue(stableId, out var sourceEntity))
                    continue;

                ApplySource(em, sourceEntity, pair.Value);
                mappedIds.Add(stableId);
            }

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                uint stableId = math.max(1u, em.GetComponentData<SourceStableIdComponent>(sourceEntity).Value);
                if (mappedIds.Contains(stableId))
                    continue;

                DisableSource(em, sourceEntity);
            }
        }

        private static void ApplyDepositLayout(ref SystemState state, int stageId, in StageMapDefinition stage)
        {
            var em = state.EntityManager;
            using var depositQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<DepositStableIdComponent>(),
                ComponentType.ReadWrite<DepositPointComponent>(),
                ComponentType.ReadWrite<LocalTransform>());
            using var depositEntities = depositQuery.ToEntityArray(Allocator.Temp);

            var stageById = BuildStageDepositMap(stage.Deposits, out int stageDuplicateCount);
            var runtimeById = BuildRuntimeDepositMap(em, depositEntities, out int runtimeDuplicateCount, out var runtimeDuplicateIds);
            var mappedIds = new HashSet<uint>();

            if (stageDuplicateCount > 0)
                Debug.LogWarning($"[StageMapApply] Duplicate deposit stableId in catalog stage. stageId={stageId}, duplicateCount={stageDuplicateCount}");

            if (runtimeDuplicateCount > 0)
                Debug.LogWarning($"[StageMapApply] Duplicate runtime deposit stableId detected. stageId={stageId}, duplicateCount={runtimeDuplicateCount}");

            foreach (var pair in stageById)
            {
                uint stableId = pair.Key;
                if (runtimeDuplicateIds.Contains(stableId))
                    continue;
                if (!runtimeById.TryGetValue(stableId, out var depositEntity))
                    continue;

                ApplyDeposit(em, depositEntity, pair.Value);
                mappedIds.Add(stableId);
            }

            for (int i = 0; i < depositEntities.Length; i++)
            {
                var depositEntity = depositEntities[i];
                uint stableId = math.max(1u, em.GetComponentData<DepositStableIdComponent>(depositEntity).Value);
                if (mappedIds.Contains(stableId))
                    continue;

                DisableDeposit(em, depositEntity);
            }
        }

        private static Dictionary<uint, StageSourceLayoutData> BuildStageSourceMap(
            StageSourceLayoutData[] sources,
            out int duplicateCount)
        {
            duplicateCount = 0;
            var map = new Dictionary<uint, StageSourceLayoutData>();
            var duplicateIds = new HashSet<uint>();

            if (sources == null)
                return map;

            for (int i = 0; i < sources.Length; i++)
            {
                uint stableId = math.max(1u, sources[i].StableId);
                if (duplicateIds.Contains(stableId))
                    continue;

                if (map.ContainsKey(stableId))
                {
                    map.Remove(stableId);
                    duplicateIds.Add(stableId);
                    duplicateCount++;
                    continue;
                }

                map.Add(stableId, sources[i]);
            }

            return map;
        }

        private static Dictionary<uint, StageDepositLayoutData> BuildStageDepositMap(
            StageDepositLayoutData[] deposits,
            out int duplicateCount)
        {
            duplicateCount = 0;
            var map = new Dictionary<uint, StageDepositLayoutData>();
            var duplicateIds = new HashSet<uint>();

            if (deposits == null)
                return map;

            for (int i = 0; i < deposits.Length; i++)
            {
                uint stableId = math.max(1u, deposits[i].StableId);
                if (duplicateIds.Contains(stableId))
                    continue;

                if (map.ContainsKey(stableId))
                {
                    map.Remove(stableId);
                    duplicateIds.Add(stableId);
                    duplicateCount++;
                    continue;
                }

                map.Add(stableId, deposits[i]);
            }

            return map;
        }

        private static Dictionary<uint, Entity> BuildRuntimeSourceMap(
            EntityManager em,
            NativeArray<Entity> entities,
            out int duplicateCount,
            out HashSet<uint> duplicateIds)
        {
            duplicateCount = 0;
            duplicateIds = new HashSet<uint>();
            var map = new Dictionary<uint, Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                uint stableId = math.max(1u, em.GetComponentData<SourceStableIdComponent>(entities[i]).Value);
                if (duplicateIds.Contains(stableId))
                    continue;

                if (map.ContainsKey(stableId))
                {
                    map.Remove(stableId);
                    duplicateIds.Add(stableId);
                    duplicateCount++;
                    continue;
                }

                map.Add(stableId, entities[i]);
            }

            return map;
        }

        private static Dictionary<uint, Entity> BuildRuntimeDepositMap(
            EntityManager em,
            NativeArray<Entity> entities,
            out int duplicateCount,
            out HashSet<uint> duplicateIds)
        {
            duplicateCount = 0;
            duplicateIds = new HashSet<uint>();
            var map = new Dictionary<uint, Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                uint stableId = math.max(1u, em.GetComponentData<DepositStableIdComponent>(entities[i]).Value);
                if (duplicateIds.Contains(stableId))
                    continue;

                if (map.ContainsKey(stableId))
                {
                    map.Remove(stableId);
                    duplicateIds.Add(stableId);
                    duplicateCount++;
                    continue;
                }

                map.Add(stableId, entities[i]);
            }

            return map;
        }

        private static void ApplySource(EntityManager em, Entity entity, StageSourceLayoutData sourceData)
        {
            var source = em.GetComponentData<SourceSpawnComponent>(entity);
            var anchor = em.GetComponentData<SourceAnchorComponent>(entity);
            var area = em.GetComponentData<BulletFieldAreaComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);

            if (sourceData.Active)
            {
                source.State = SourceStateId.Normal;
                source.CollectedCount = 0;

                float3 position = new float3(sourceData.Position.x, sourceData.Position.y, sourceData.Position.z);
                anchor.Position = position;
                tx.Position = position;
                tx.Rotation = quaternion.RotateY(math.radians(sourceData.YawDeg));

                area.Shape = sourceData.FieldShape;
                area.Radius = math.max(0f, sourceData.FieldRadius);
                area.Size = math.max(float2.zero, new float2(sourceData.FieldSize.x, sourceData.FieldSize.y));
                area.ComputedArea = area.Shape == BulletFieldShapeId.Rectangle
                    ? area.Size.x * area.Size.y
                    : math.PI * area.Radius * area.Radius;
            }
            else
            {
                source.State = SourceStateId.Depleted;
            }

            em.SetComponentData(entity, source);
            em.SetComponentData(entity, anchor);
            em.SetComponentData(entity, area);
            em.SetComponentData(entity, tx);
        }

        private static void DisableSource(EntityManager em, Entity entity)
        {
            var source = em.GetComponentData<SourceSpawnComponent>(entity);
            source.State = SourceStateId.Depleted;
            em.SetComponentData(entity, source);
        }

        private static void ApplyDeposit(EntityManager em, Entity entity, StageDepositLayoutData depositData)
        {
            var deposit = em.GetComponentData<DepositPointComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);

            if (depositData.Active)
            {
                deposit.Radius = math.max(0f, depositData.Radius);
                tx.Position = new float3(depositData.Position.x, depositData.Position.y, depositData.Position.z);
            }
            else
            {
                deposit.Radius = 0f;
                tx.Position = DepositSinkPosition;
            }

            em.SetComponentData(entity, deposit);
            em.SetComponentData(entity, tx);
        }

        private static void DisableDeposit(EntityManager em, Entity entity)
        {
            var deposit = em.GetComponentData<DepositPointComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);
            deposit.Radius = 0f;
            tx.Position = DepositSinkPosition;
            em.SetComponentData(entity, deposit);
            em.SetComponentData(entity, tx);
        }

        private static Entity ResolveFirstEntity(EntityQuery query)
        {
            int count = query.CalculateEntityCount();
            if (count <= 0)
                return Entity.Null;
            if (count == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }
    }
}

