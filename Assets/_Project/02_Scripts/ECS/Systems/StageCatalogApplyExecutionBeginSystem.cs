using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Stage runtime apply owner.
    /// - ExecutionBegin에서 stage apply one-shot 요청을 소비한다.
    /// - StageCatalog에서 layout + definition을 직접 조회해 Source/Deposit 적용을 수행한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(BulletPoolOwnerBootstrapSystem))]
    [UpdateBefore(typeof(BulletFieldAreaUpdateSystem))]
    public partial struct StageCatalogApplyExecutionBeginSystem : ISystem
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
            if (request.StageApplyRequested == 0)
                return;

            request.StageApplyRequested = 0;
            requestRW.ValueRW = request;

            int requestedStageId = request.RequestedStageId;
            if (requestedStageId <= 0)
            {
                Debug.LogWarning("[StageCatalogApply] Ignored request with invalid stageId.");
                return;
            }

            StageLayoutSO layout = null;
            bool hasLayout = TryResolveLayout(ref state, requestedStageId, out layout);
            StageDefinitionSO definition = null;
            bool hasDefinition = TryResolveDefinition(ref state, requestedStageId, out definition);

            if (hasLayout)
            {
                ApplySourceStage(ref state, requestedStageId, layout, definition, hasDefinition);
                ApplyDepositLayout(ref state, requestedStageId, layout);
            }
            else if (hasDefinition)
            {
                Debug.LogWarning($"[StageCatalogApply] Layout is missing but definition exists. stageId={requestedStageId}, definition={definition.name}");
            }
        }

        private static bool TryResolveLayout(ref SystemState state, int stageId, out StageLayoutSO layout)
        {
            layout = null;
            var runtime = TryGetStageCatalogRuntime(ref state);
            if (runtime == null || runtime.Catalog == null)
            {
                Debug.LogWarning($"[StageCatalogApply] StageCatalog is missing. stageId={stageId}");
                return false;
            }

            if (!TryFindEnabledStageLayout(runtime.Catalog, stageId, out layout, out bool duplicateMatch))
            {
                if (duplicateMatch)
                {
                    Debug.LogWarning($"[StageCatalogApply] Duplicate enabled StageLayout match detected. Layout apply will be skipped. stageId={stageId}, catalog={runtime.Catalog.name}");
                }
                else
                {
                    Debug.LogWarning($"[StageCatalogApply] StageLayout not found in StageCatalog. stageId={stageId}, catalog={runtime.Catalog.name}");
                }

                return false;
            }

            return true;
        }

        private static bool TryResolveDefinition(ref SystemState state, int stageId, out StageDefinitionSO definition)
        {
            definition = null;
            var runtime = TryGetStageCatalogRuntime(ref state);
            if (runtime == null || runtime.Catalog == null)
            {
                Debug.LogWarning($"[StageCatalogApply] StageCatalog is missing. Definition apply will be skipped. stageId={stageId}");
                return false;
            }

            if (!TryFindEnabledStageDefinition(runtime.Catalog, stageId, out definition, out bool duplicateMatch))
            {
                if (duplicateMatch)
                {
                    Debug.LogWarning($"[StageCatalogApply] Duplicate enabled StageDefinition match detected. Definition apply will be skipped. stageId={stageId}, catalog={runtime.Catalog.name}");
                }
                else
                {
                    Debug.LogWarning($"[StageCatalogApply] StageDefinition not found in StageCatalog. Definition apply will be skipped. stageId={stageId}, catalog={runtime.Catalog.name}");
                }

                return false;
            }

            return true;
        }

        private static void ApplySourceStage(
            ref SystemState state,
            int stageId,
            StageLayoutSO layout,
            StageDefinitionSO definition,
            bool hasDefinitionStage)
        {
            var em = state.EntityManager;
            using var sourceQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<SourceStableIdComponent>(),
                ComponentType.ReadWrite<SourceSpawnComponent>(),
                ComponentType.ReadWrite<SourceSpawnRuntimeComponent>(),
                ComponentType.ReadWrite<SourceAnchorComponent>(),
                ComponentType.ReadWrite<BulletFieldAreaComponent>(),
                ComponentType.ReadWrite<SourcePollutionConfigComponent>(),
                ComponentType.ReadWrite<SourcePollutionGridComponent>(),
                ComponentType.ReadWrite<SourceSustainRuntimeComponent>(),
                ComponentType.ReadWrite<SourceEventRuntimeComponent>(),
                ComponentType.ReadWrite<SourceRunDirectorStateComponent>(),
                ComponentType.ReadWrite<LocalTransform>());
            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);

            var layoutById = BuildStageSourceMap(layout != null ? layout.Sources : null, out int layoutDuplicateCount);
            BuildRuntimeSourceMap(em, sourceEntities, out int runtimeDuplicateCount, out var runtimeDuplicateIds);
            var definitionById = hasDefinitionStage
                ? BuildDefinitionSourceMap(definition.SourceBindings, out int _, out _)
                : new Dictionary<uint, StageSourceBinding>();
            var definitionDuplicateIds = hasDefinitionStage
                ? BuildDefinitionDuplicateIdSet(definition.SourceBindings, out int _)
                : new HashSet<uint>();

            if (layoutDuplicateCount > 0)
            {
                Debug.LogWarning($"[StageCatalogApply] Duplicate source stableId in layout. stageId={stageId}, duplicateCount={layoutDuplicateCount}");
            }

            if (runtimeDuplicateCount > 0)
            {
                Debug.LogWarning($"[StageCatalogApply] Duplicate runtime source stableId detected. stageId={stageId}, duplicateCount={runtimeDuplicateCount}");
            }

            if (hasDefinitionStage && definitionDuplicateIds.Count > 0)
            {
                Debug.LogWarning($"[StageCatalogApply] Duplicate source stableId in StageDefinition. stageId={stageId}, duplicateCount={definitionDuplicateIds.Count}, definition={definition.name}");
            }

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                uint stableId = math.max(1u, em.GetComponentData<SourceStableIdComponent>(sourceEntity).Value);

                if (runtimeDuplicateIds.Contains(stableId))
                {
                    DisableSource(em, sourceEntity);
                    continue;
                }

                if (!layoutById.TryGetValue(stableId, out var layoutData))
                {
                    DisableSource(em, sourceEntity);
                    continue;
                }

                if (!layoutData.Active)
                {
                    DisableSource(em, sourceEntity);
                    continue;
                }

                ApplySourceLayout(em, sourceEntity, layoutData);

                if (!hasDefinitionStage)
                {
                    ApplySourceLayoutOnly(em, sourceEntity);
                    continue;
                }

                if (definitionDuplicateIds.Contains(stableId))
                {
                    DisableSource(em, sourceEntity);
                    continue;
                }

                if (!definitionById.TryGetValue(stableId, out var binding))
                {
                    Debug.LogWarning($"[StageCatalogApply] Source binding is missing in StageDefinition. stageId={stageId}, stableId={stableId}, definition={definition.name}");
                    DisableSource(em, sourceEntity);
                    continue;
                }

                ApplySourceDefinition(em, sourceEntity, in binding);
            }
        }

        private static void ApplyDepositLayout(ref SystemState state, int stageId, StageLayoutSO layout)
        {
            var em = state.EntityManager;
            using var depositQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<DepositStableIdComponent>(),
                ComponentType.ReadWrite<DepositPointComponent>(),
                ComponentType.ReadWrite<LocalTransform>());
            using var depositEntities = depositQuery.ToEntityArray(Allocator.Temp);

            var stageById = BuildStageDepositMap(layout != null ? layout.Deposits : null, out int stageDuplicateCount);
            var runtimeById = BuildRuntimeDepositMap(em, depositEntities, out int runtimeDuplicateCount, out var runtimeDuplicateIds);
            var mappedIds = new HashSet<uint>();

            if (stageDuplicateCount > 0)
            {
                Debug.LogWarning($"[StageCatalogApply] Duplicate deposit stableId in layout. stageId={stageId}, duplicateCount={stageDuplicateCount}");
            }

            if (runtimeDuplicateCount > 0)
            {
                Debug.LogWarning($"[StageCatalogApply] Duplicate runtime deposit stableId detected. stageId={stageId}, duplicateCount={runtimeDuplicateCount}");
            }

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
        private static StageCatalogRuntimeComponent TryGetStageCatalogRuntime(ref SystemState state)
        {
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<StageCatalogRuntimeComponent>());
            if (query.IsEmptyIgnoreFilter)
                return null;

            var runtimeEntity = ResolveFirstEntity(query);
            if (runtimeEntity == Entity.Null || !em.Exists(runtimeEntity))
                return null;

            return em.GetComponentObject<StageCatalogRuntimeComponent>(runtimeEntity);
        }

        private static bool TryFindEnabledStageLayout(StageCatalogSO catalog, int stageId, out StageLayoutSO layout, out bool duplicateMatch)
        {
            layout = null;
            duplicateMatch = false;
            if (catalog == null || catalog.Entries == null)
                return false;

            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                var entry = catalog.Entries[i];
                if (!entry.Enabled || entry.Layout == null)
                    continue;
                if (entry.Layout.StageId != stageId)
                    continue;

                if (layout != null)
                {
                    duplicateMatch = true;
                    layout = null;
                    return false;
                }

                layout = entry.Layout;
            }

            return layout != null;
        }

        private static bool TryFindEnabledStageDefinition(StageCatalogSO catalog, int stageId, out StageDefinitionSO definition, out bool duplicateMatch)
        {
            definition = null;
            duplicateMatch = false;
            if (catalog == null || catalog.Entries == null)
                return false;

            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                var entry = catalog.Entries[i];
                if (!entry.Enabled || entry.Definition == null)
                    continue;
                if (entry.Definition.StageId != stageId)
                    continue;

                if (definition != null)
                {
                    duplicateMatch = true;
                    definition = null;
                    return false;
                }

                definition = entry.Definition;
            }

            return definition != null;
        }

        private static Dictionary<uint, StageSourceLayoutData> BuildStageSourceMap(StageSourceLayoutData[] sources, out int duplicateCount)
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

        private static Dictionary<uint, StageSourceBinding> BuildDefinitionSourceMap(StageSourceBinding[] bindings, out int duplicateCount, out HashSet<uint> duplicateIds)
        {
            duplicateCount = 0;
            duplicateIds = new HashSet<uint>();
            var map = new Dictionary<uint, StageSourceBinding>();

            if (bindings == null)
                return map;

            for (int i = 0; i < bindings.Length; i++)
            {
                uint stableId = math.max(1u, bindings[i].SourceStableId);
                if (duplicateIds.Contains(stableId))
                    continue;

                if (map.ContainsKey(stableId))
                {
                    map.Remove(stableId);
                    duplicateIds.Add(stableId);
                    duplicateCount++;
                    continue;
                }

                map.Add(stableId, bindings[i]);
            }

            return map;
        }

        private static HashSet<uint> BuildDefinitionDuplicateIdSet(StageSourceBinding[] bindings, out int duplicateCount)
        {
            BuildDefinitionSourceMap(bindings, out duplicateCount, out var duplicateIds);
            return duplicateIds;
        }

        private static Dictionary<uint, StageDepositLayoutData> BuildStageDepositMap(StageDepositLayoutData[] deposits, out int duplicateCount)
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

        private static void BuildRuntimeSourceMap(EntityManager em, NativeArray<Entity> entities, out int duplicateCount, out HashSet<uint> duplicateIds)
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
        }

        private static Dictionary<uint, Entity> BuildRuntimeDepositMap(EntityManager em, NativeArray<Entity> entities, out int duplicateCount, out HashSet<uint> duplicateIds)
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

        private static void ApplySourceLayout(EntityManager em, Entity entity, StageSourceLayoutData sourceData)
        {
            var anchor = em.GetComponentData<SourceAnchorComponent>(entity);
            var area = em.GetComponentData<BulletFieldAreaComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);
            var pollutionConfig = em.GetComponentData<SourcePollutionConfigComponent>(entity);
            var pollutionGrid = em.GetComponentData<SourcePollutionGridComponent>(entity);

            float3 position = new float3(sourceData.Position.x, sourceData.Position.y, sourceData.Position.z);
            anchor.Position = position;
            tx.Position = position;
            tx.Rotation = quaternion.RotateY(math.radians(sourceData.YawDeg));

            area.Shape = sourceData.FieldShape;
            area.Radius = math.max(0f, sourceData.FieldRadius);
            area.Size = math.max(float2.zero, new float2(sourceData.FieldSize.x, sourceData.FieldSize.y));
            area.ComputedArea = SourceRuntimeApplyUtility.ComputeArea(area.Shape, area.Radius, new Vector2(area.Size.x, area.Size.y));

            SourceRuntimeApplyUtility.RebuildPollutionGrid(
                in area,
                in pollutionConfig,
                ref pollutionGrid,
                em.GetBuffer<SourcePollutionCellBuffer>(entity),
                em.GetBuffer<SourcePollutionDropRequestBuffer>(entity),
                em.GetBuffer<SourcePollutionValidCellIndexBuffer>(entity));

            em.SetComponentData(entity, anchor);
            em.SetComponentData(entity, area);
            em.SetComponentData(entity, tx);
            em.SetComponentData(entity, pollutionGrid);
        }

        private static void ApplySourceLayoutOnly(EntityManager em, Entity entity)
        {
            var source = em.GetComponentData<SourceSpawnComponent>(entity);
            var sourceRuntime = em.GetComponentData<SourceSpawnRuntimeComponent>(entity);
            var sustainRuntime = em.GetComponentData<SourceSustainRuntimeComponent>(entity);
            var eventRuntime = em.GetComponentData<SourceEventRuntimeComponent>(entity);
            var directorState = em.GetComponentData<SourceRunDirectorStateComponent>(entity);
            var spawnRequests = em.GetBuffer<SourceSpawnRequestBuffer>(entity);
            var sustainRuntimeLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(entity);
            var eventQueue = em.GetBuffer<SourceEventQueueBuffer>(entity);
            var activeCounts = em.GetBuffer<SourceActiveBulletCountBuffer>(entity);
            var pressureInputs = em.GetBuffer<SourceDirectorPressureInputBuffer>(entity);
            var pollutionDrops = em.GetBuffer<SourcePollutionDropRequestBuffer>(entity);

            source.State = SourceStateId.Normal;
            source.CollectedCount = 0;
            sourceRuntime.SpawnSequence = 1u;
            sustainRuntime.ActiveState = SourceStateId.Normal;

            eventRuntime.IsPlaying = 0;
            eventRuntime.ActiveEventClipId = 0;
            eventRuntime.TriggerState = SourceStateId.Normal;
            eventRuntime.ElapsedSec = 0f;
            eventRuntime.SelectionSequence = 1u;

            directorState.State = RunDirectorSourceStateId.Baseline;
            directorState.SelectedClipState = SourceStateId.Normal;
            directorState.PressureOccupancySec = 0f;
            directorState.DensityScale = 1f;
            directorState.Version = math.max(1u, directorState.Version + 1u);

            spawnRequests.Clear();
            eventQueue.Clear();
            pollutionDrops.Clear();
            SourceRuntimeApplyUtility.ResetPressureInputs(pressureInputs);

            for (int i = 0; i < sustainRuntimeLanes.Length; i++)
            {
                var lane = sustainRuntimeLanes[i];
                lane.ActiveClipId = 0;
                lane.ElapsedSec = 0f;
                lane.LastClipId = 0;
                lane.SelectionSequence = 1u;
                lane.LastMissingLogFrame = 0u;
                sustainRuntimeLanes[i] = lane;
            }

            for (int i = 0; i < activeCounts.Length; i++)
            {
                var count = activeCounts[i];
                count.ActiveCount = 0;
                activeCounts[i] = count;
            }

            em.SetComponentData(entity, source);
            em.SetComponentData(entity, sourceRuntime);
            em.SetComponentData(entity, sustainRuntime);
            em.SetComponentData(entity, eventRuntime);
            em.SetComponentData(entity, directorState);
        }
        private static void ApplySourceDefinition(EntityManager em, Entity entity, in StageSourceBinding binding)
        {
            int thresholdWeakened = math.max(0, binding.ThresholdWeakened);
            int thresholdDepleted = math.max(thresholdWeakened, binding.ThresholdDepleted);
            var initialState = binding.InitialSourceState;

            var source = em.GetComponentData<SourceSpawnComponent>(entity);
            var sourceRuntime = em.GetComponentData<SourceSpawnRuntimeComponent>(entity);
            var sustainRuntime = em.GetComponentData<SourceSustainRuntimeComponent>(entity);
            var eventRuntime = em.GetComponentData<SourceEventRuntimeComponent>(entity);
            var directorState = em.GetComponentData<SourceRunDirectorStateComponent>(entity);
            var spawnRequests = em.GetBuffer<SourceSpawnRequestBuffer>(entity);
            var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(entity);
            var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(entity);
            var sustainRuntimeLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(entity);
            var eventQueue = em.GetBuffer<SourceEventQueueBuffer>(entity);
            var activeCounts = em.GetBuffer<SourceActiveBulletCountBuffer>(entity);
            var pressureInputs = em.GetBuffer<SourceDirectorPressureInputBuffer>(entity);
            var pollutionDrops = em.GetBuffer<SourcePollutionDropRequestBuffer>(entity);

            source.ThresholdWeakened = thresholdWeakened;
            source.ThresholdDepleted = thresholdDepleted;
            source.CollectedCount = SourceRuntimeApplyUtility.ResolveCollectedCount(initialState, thresholdWeakened, thresholdDepleted);
            source.State = initialState;

            sourceRuntime.SpawnSequence = 1u;
            sustainRuntime.ActiveState = initialState;

            eventRuntime.IsPlaying = 0;
            eventRuntime.ActiveEventClipId = 0;
            eventRuntime.TriggerState = initialState;
            eventRuntime.ElapsedSec = 0f;
            eventRuntime.SelectionSequence = 1u;

            directorState.State = initialState == SourceStateId.Depleted ? RunDirectorSourceStateId.Finish : RunDirectorSourceStateId.Baseline;
            directorState.SelectedClipState = initialState;
            directorState.PressureOccupancySec = 0f;
            directorState.DensityScale = 1f;
            directorState.Version = math.max(1u, directorState.Version + 1u);

            spawnRequests.Clear();
            pollutionDrops.Clear();
            SourceRuntimeApplyUtility.ResetPressureInputs(pressureInputs);
            SourceRuntimeApplyUtility.RebuildClipBindingsFromStageDefinition(in binding, clipPatterns, sustainCandidates, sustainRuntimeLanes, eventQueue, activeCounts);

            em.SetComponentData(entity, source);
            em.SetComponentData(entity, sourceRuntime);
            em.SetComponentData(entity, sustainRuntime);
            em.SetComponentData(entity, eventRuntime);
            em.SetComponentData(entity, directorState);
        }

        private static void DisableSource(EntityManager em, Entity entity)
        {
            var source = em.GetComponentData<SourceSpawnComponent>(entity);
            var sourceRuntime = em.GetComponentData<SourceSpawnRuntimeComponent>(entity);
            var sustainRuntime = em.GetComponentData<SourceSustainRuntimeComponent>(entity);
            var eventRuntime = em.GetComponentData<SourceEventRuntimeComponent>(entity);
            var directorState = em.GetComponentData<SourceRunDirectorStateComponent>(entity);
            var spawnRequests = em.GetBuffer<SourceSpawnRequestBuffer>(entity);
            var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(entity);
            var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(entity);
            var sustainRuntimeLanes = em.GetBuffer<SourceSustainRuntimeLaneBuffer>(entity);
            var eventQueue = em.GetBuffer<SourceEventQueueBuffer>(entity);
            var activeCounts = em.GetBuffer<SourceActiveBulletCountBuffer>(entity);
            var pressureInputs = em.GetBuffer<SourceDirectorPressureInputBuffer>(entity);
            var pollutionDrops = em.GetBuffer<SourcePollutionDropRequestBuffer>(entity);

            source.CollectedCount = math.max(math.max(0, source.ThresholdWeakened), source.ThresholdDepleted);
            source.State = SourceStateId.Depleted;
            sourceRuntime.SpawnSequence = 1u;
            sustainRuntime.ActiveState = SourceStateId.Depleted;

            eventRuntime.IsPlaying = 0;
            eventRuntime.ActiveEventClipId = 0;
            eventRuntime.TriggerState = SourceStateId.Depleted;
            eventRuntime.ElapsedSec = 0f;
            eventRuntime.SelectionSequence = 1u;

            directorState.State = RunDirectorSourceStateId.Finish;
            directorState.SelectedClipState = SourceStateId.Depleted;
            directorState.PressureOccupancySec = 0f;
            directorState.DensityScale = 1f;
            directorState.Version = math.max(1u, directorState.Version + 1u);

            spawnRequests.Clear();
            clipPatterns.Clear();
            sustainCandidates.Clear();
            sustainRuntimeLanes.Clear();
            eventQueue.Clear();
            activeCounts.Clear();
            pollutionDrops.Clear();
            SourceRuntimeApplyUtility.ResetPressureInputs(pressureInputs);

            em.SetComponentData(entity, source);
            em.SetComponentData(entity, sourceRuntime);
            em.SetComponentData(entity, sustainRuntime);
            em.SetComponentData(entity, eventRuntime);
            em.SetComponentData(entity, directorState);
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
