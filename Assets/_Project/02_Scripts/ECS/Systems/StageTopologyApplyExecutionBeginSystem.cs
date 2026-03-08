using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Stage topology runtime apply owner.
    /// - ExecutionBegin에서 topology apply 요청을 소비한다.
    /// - StageCatalog + topology template prefab을 이용해 Source/Deposit entity set을 reconcile한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(BulletPoolOwnerBootstrapSystem))]
    [UpdateBefore(typeof(BulletFieldAreaUpdateSystem))]
    public partial struct StageTopologyApplyExecutionBeginSystem : ISystem
    {
        private static readonly float3 DepositSinkPosition = new float3(0f, -10000f, 0f);

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageTopologyRequestComponent>();
            state.RequireForUpdate<StageTopologyStateComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<StageCatalogRuntimeComponent>();
            state.RequireForUpdate<StageTopologyPrefabCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            em.CompleteAllTrackedJobs();
            state.CompleteDependency();

            var requestEntity = SystemAPI.GetSingletonEntity<StageTopologyRequestComponent>();
            var request = em.GetComponentData<StageTopologyRequestComponent>(requestEntity);
            if (request.ApplyRequested == 0)
                return;

            request.ApplyRequested = 0;
            em.SetComponentData(requestEntity, request);

            int requestedStageId = request.RequestedStageId;
            var stageState = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            var topologyStateEntity = SystemAPI.GetSingletonEntity<StageTopologyStateComponent>();
            var topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
            if (!IsApplyBoundaryState(stageState.State, topologyState))
            {
                Debug.LogWarning($"[StageTopologyApply] Ignored topology apply outside stage boundary. stageId={requestedStageId}, stageState={stageState.State}");
                return;
            }
            topologyState.SelectedStageId = requestedStageId;
            topologyState.Ready = 0;
            em.SetComponentData(topologyStateEntity, topologyState);

            if (requestedStageId <= 0)
            {
                Debug.LogWarning("[StageTopologyApply] Ignored request with invalid stageId.");
                return;
            }

            if (!TryResolveStageEntry(ref state, requestedStageId, out var entry, out var catalog))
            {
                Debug.LogWarning($"[StageTopologyApply] Enabled stage entry not found. stageId={requestedStageId}");
                return;
            }

            if (entry.Layout == null)
            {
                Debug.LogWarning($"[StageTopologyApply] StageLayout is missing. stageId={requestedStageId}, catalog={catalog.name}");
                return;
            }

            if (!TryGetTopologyPrefabCatalog(ref state, out var prefabs))
            {
                Debug.LogWarning($"[StageTopologyApply] Topology prefab catalog is missing. stageId={requestedStageId}");
                return;
            }

            bool needsSourceTemplate = entry.Layout.Sources != null && entry.Layout.Sources.Length > 0;
            bool needsDepositTemplate = entry.Layout.Deposits != null && entry.Layout.Deposits.Length > 0;
            if (needsSourceTemplate && prefabs.SourceTemplate == Entity.Null)
            {
                Debug.LogWarning($"[StageTopologyApply] Source template prefab is missing. stageId={requestedStageId}");
                return;
            }

            if (needsDepositTemplate && prefabs.DepositTemplate == Entity.Null)
            {
                Debug.LogWarning($"[StageTopologyApply] Deposit template prefab is missing. stageId={requestedStageId}");
                return;
            }

            ApplySourceTopology(ref state, requestedStageId, prefabs.SourceTemplate, entry.Layout, entry.Definition);
            ApplyDepositTopology(ref state, requestedStageId, prefabs.DepositTemplate, entry.Layout);

            topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
            topologyState.SelectedStageId = requestedStageId;
            topologyState.AppliedStageId = requestedStageId;
            topologyState.Ready = 1;
            em.SetComponentData(topologyStateEntity, topologyState);
        }

        private static bool IsApplyBoundaryState(RunDirectorStageStateId state, StageTopologyStateComponent topologyState)
        {
            if (state == RunDirectorStageStateId.Idle || state == RunDirectorStageStateId.Completed)
                return true;

            return state == RunDirectorStageStateId.Running
                && topologyState.SelectedStageId <= 0
                && topologyState.AppliedStageId <= 0
                && topologyState.Ready == 0;
        }

        private static bool TryGetTopologyPrefabCatalog(ref SystemState state, out StageTopologyPrefabCatalogComponent prefabs)
        {
            prefabs = default;
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            using var entities = query.ToEntityArray(Allocator.Temp);
            Entity selected = Entity.Null;
            for (int i = 0; i < entities.Length; i++)
            {
                if (!em.Exists(entities[i]))
                    continue;

                var candidate = em.GetComponentData<StageTopologyPrefabCatalogComponent>(entities[i]);
                bool hasAnyPrefab = candidate.SourceTemplate != Entity.Null || candidate.DepositTemplate != Entity.Null;
                if (selected == Entity.Null || hasAnyPrefab)
                {
                    selected = entities[i];
                    prefabs = candidate;
                    if (hasAnyPrefab)
                        return true;
                }
            }

            return selected != Entity.Null;
        }

        private static bool TryResolveStageEntry(
            ref SystemState state,
            int stageId,
            out StageCatalogEntry entry,
            out StageCatalogSO catalog)
        {
            entry = default;
            catalog = null;

            var runtime = TryGetStageCatalogRuntime(ref state);
            if (runtime == null || runtime.Catalog == null)
                return false;

            catalog = runtime.Catalog;
            return TryFindEnabledStageEntry(catalog, stageId, out entry, out _);
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

        private static bool TryFindEnabledStageEntry(StageCatalogSO catalog, int stageId, out StageCatalogEntry matched, out bool duplicateMatch)
        {
            matched = default;
            duplicateMatch = false;
            bool hasMatch = false;
            if (catalog == null || catalog.Entries == null)
                return false;

            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                var entry = catalog.Entries[i];
                if (!entry.Enabled)
                    continue;

                bool definitionMatch = entry.Definition != null && entry.Definition.StageId == stageId;
                bool layoutMatch = entry.Layout != null && entry.Layout.StageId == stageId;
                if (!definitionMatch && !layoutMatch)
                    continue;

                if (hasMatch)
                {
                    duplicateMatch = true;
                    matched = default;
                    return false;
                }

                matched = entry;
                hasMatch = true;
            }

            return hasMatch;
        }

        private static void ApplySourceTopology(
            ref SystemState state,
            int stageId,
            Entity sourceTemplate,
            StageLayoutSO layout,
            StageDefinitionSO definition)
        {
            var em = state.EntityManager;
            var layoutById = BuildStageSourceMap(layout != null ? layout.Sources : null, out int layoutDuplicateCount);
            var activeLayoutIds = BuildActiveStableIdSet(layoutById.Values);
            var definitionById = definition != null
                ? BuildDefinitionSourceMap(definition.SourceBindings, out _, out _)
                : new Dictionary<uint, StageSourceBinding>();
            var definitionDuplicateIds = definition != null
                ? BuildDefinitionDuplicateIdSet(definition.SourceBindings, out _)
                : new HashSet<uint>();

            if (layoutDuplicateCount > 0)
                Debug.LogWarning($"[StageTopologyApply] Duplicate source stableId in layout. stageId={stageId}, duplicateCount={layoutDuplicateCount}");
            if (definition != null && definitionDuplicateIds.Count > 0)
                Debug.LogWarning($"[StageTopologyApply] Duplicate source stableId in StageDefinition. stageId={stageId}, duplicateCount={definitionDuplicateIds.Count}, definition={definition.name}");

            using var sourceQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologySourceTag>(),
                    ComponentType.ReadOnly<SourceStableIdComponent>(),
                    ComponentType.ReadOnly<SourceSpawnComponent>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);
            BuildRuntimeInstanceSets(
                em,
                sourceEntities,
                activeLayoutIds,
                stableIdAccessor: entity => math.max(1u, em.GetComponentData<SourceStableIdComponent>(entity).Value),
                out var activeById,
                out var reusableEntities,
                out var duplicateActiveIds);

            if (duplicateActiveIds.Count > 0)
                Debug.LogWarning($"[StageTopologyApply] Duplicate active runtime source stableId detected. stageId={stageId}, duplicateCount={duplicateActiveIds.Count}");

            var mappedEntities = new HashSet<Entity>();
            foreach (var pair in layoutById)
            {
                uint stableId = pair.Key;
                var layoutData = pair.Value;

                if (!layoutData.Active)
                    continue;

                if (duplicateActiveIds.Contains(stableId) || definitionDuplicateIds.Contains(stableId))
                    continue;

                Entity sourceEntity = ResolveTopologyEntity(
                    em,
                    stableId,
                    sourceTemplate,
                    ref activeById,
                    reusableEntities,
                    StageTopologyKind.Source);
                if (sourceEntity == Entity.Null)
                {
                    Debug.LogWarning($"[StageTopologyApply] Failed to resolve source instance. stageId={stageId}, stableId={stableId}");
                    continue;
                }

                em.SetEnabled(sourceEntity, true);
                EnsureSourceTags(em, sourceEntity);
                em.SetComponentData(sourceEntity, new SourceStableIdComponent { Value = stableId });
                ApplySourceLayout(em, sourceEntity, layoutData);

                if (definition == null)
                {
                    Debug.LogWarning($"[StageTopologyApply] StageDefinition is missing. Layout-only source apply will be used. stageId={stageId}, stableId={stableId}");
                    ApplySourceLayoutOnly(em, sourceEntity);
                }
                else if (!definitionById.TryGetValue(stableId, out var binding))
                {
                    Debug.LogWarning($"[StageTopologyApply] Source binding is missing in StageDefinition. stageId={stageId}, stableId={stableId}, definition={definition.name}");
                    DisableSourceInstance(em, sourceEntity);
                }
                else
                {
                    ApplySourceDefinition(em, sourceEntity, in binding);
                }

                if (em.IsEnabled(sourceEntity))
                    mappedEntities.Add(sourceEntity);
            }

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var entity = sourceEntities[i];
                if (mappedEntities.Contains(entity))
                    continue;

                DisableSourceInstance(em, entity);
            }
        }

        private static void ApplyDepositTopology(
            ref SystemState state,
            int stageId,
            Entity depositTemplate,
            StageLayoutSO layout)
        {
            var em = state.EntityManager;
            var layoutById = BuildStageDepositMap(layout != null ? layout.Deposits : null, out int layoutDuplicateCount);
            var activeLayoutIds = BuildActiveStableIdSet(layoutById.Values);
            if (layoutDuplicateCount > 0)
                Debug.LogWarning($"[StageTopologyApply] Duplicate deposit stableId in layout. stageId={stageId}, duplicateCount={layoutDuplicateCount}");

            using var depositQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologyDepositTag>(),
                    ComponentType.ReadOnly<DepositStableIdComponent>(),
                    ComponentType.ReadOnly<DepositPointComponent>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var depositEntities = depositQuery.ToEntityArray(Allocator.Temp);
            BuildRuntimeInstanceSets(
                em,
                depositEntities,
                activeLayoutIds,
                stableIdAccessor: entity => math.max(1u, em.GetComponentData<DepositStableIdComponent>(entity).Value),
                out var activeById,
                out var reusableEntities,
                out var duplicateActiveIds);

            if (duplicateActiveIds.Count > 0)
                Debug.LogWarning($"[StageTopologyApply] Duplicate active runtime deposit stableId detected. stageId={stageId}, duplicateCount={duplicateActiveIds.Count}");

            var mappedEntities = new HashSet<Entity>();
            foreach (var pair in layoutById)
            {
                uint stableId = pair.Key;
                var layoutData = pair.Value;
                if (!layoutData.Active || duplicateActiveIds.Contains(stableId))
                    continue;

                Entity depositEntity = ResolveTopologyEntity(
                    em,
                    stableId,
                    depositTemplate,
                    ref activeById,
                    reusableEntities,
                    StageTopologyKind.Deposit);
                if (depositEntity == Entity.Null)
                {
                    Debug.LogWarning($"[StageTopologyApply] Failed to resolve deposit instance. stageId={stageId}, stableId={stableId}");
                    continue;
                }

                em.SetEnabled(depositEntity, true);
                EnsureDepositTags(em, depositEntity);
                em.SetComponentData(depositEntity, new DepositStableIdComponent { Value = stableId });
                ApplyDeposit(em, depositEntity, layoutData);
                mappedEntities.Add(depositEntity);
            }

            for (int i = 0; i < depositEntities.Length; i++)
            {
                var entity = depositEntities[i];
                if (mappedEntities.Contains(entity))
                    continue;

                DisableDepositInstance(em, entity);
            }
        }

        private static void BuildRuntimeInstanceSets(
            EntityManager em,
            NativeArray<Entity> entities,
            HashSet<uint> activeLayoutIds,
            System.Func<Entity, uint> stableIdAccessor,
            out Dictionary<uint, Entity> activeById,
            out List<Entity> reusableEntities,
            out HashSet<uint> duplicateActiveIds)
        {
            activeById = new Dictionary<uint, Entity>();
            reusableEntities = new List<Entity>(entities.Length);
            duplicateActiveIds = new HashSet<uint>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity))
                    continue;

                if (!em.IsEnabled(entity))
                {
                    reusableEntities.Add(entity);
                    continue;
                }

                uint stableId = stableIdAccessor(entity);
                if (!activeLayoutIds.Contains(stableId))
                {
                    reusableEntities.Add(entity);
                    continue;
                }

                if (duplicateActiveIds.Contains(stableId))
                {
                    reusableEntities.Add(entity);
                    continue;
                }

                if (activeById.ContainsKey(stableId))
                {
                    duplicateActiveIds.Add(stableId);
                    reusableEntities.Add(activeById[stableId]);
                    reusableEntities.Add(entity);
                    activeById.Remove(stableId);
                    continue;
                }

                activeById.Add(stableId, entity);
            }
        }

        private static Entity ResolveTopologyEntity(
            EntityManager em,
            uint stableId,
            Entity template,
            ref Dictionary<uint, Entity> activeById,
            List<Entity> reusableEntities,
            StageTopologyKind kind)
        {
            if (activeById.TryGetValue(stableId, out var existing))
                return existing;

            while (reusableEntities.Count > 0)
            {
                int lastIndex = reusableEntities.Count - 1;
                var entity = reusableEntities[lastIndex];
                reusableEntities.RemoveAt(lastIndex);
                if (entity == Entity.Null || !em.Exists(entity))
                    continue;

                return entity;
            }

            if (template == Entity.Null || !em.Exists(template))
                return Entity.Null;

            var created = em.Instantiate(template);
            switch (kind)
            {
                case StageTopologyKind.Source:
                    EnsureSourceTags(em, created);
                    break;
                case StageTopologyKind.Deposit:
                    EnsureDepositTags(em, created);
                    break;
            }

            return created;
        }

        private static void EnsureSourceTags(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            if (!em.HasComponent<StageTopologySourceTag>(entity))
                em.AddComponent<StageTopologySourceTag>(entity);
            if (em.HasComponent<StageTopologyDepositTag>(entity))
                em.RemoveComponent<StageTopologyDepositTag>(entity);
        }

        private static void EnsureDepositTags(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            if (!em.HasComponent<StageTopologyDepositTag>(entity))
                em.AddComponent<StageTopologyDepositTag>(entity);
            if (em.HasComponent<StageTopologySourceTag>(entity))
                em.RemoveComponent<StageTopologySourceTag>(entity);
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

        private static HashSet<uint> BuildActiveStableIdSet<TValue>(Dictionary<uint, TValue>.ValueCollection values)
        {
            var result = new HashSet<uint>();
            foreach (var value in values)
            {
                switch (value)
                {
                    case StageSourceLayoutData sourceData when sourceData.Active:
                        result.Add(math.max(1u, sourceData.StableId));
                        break;
                    case StageDepositLayoutData depositData when depositData.Active:
                        result.Add(math.max(1u, depositData.StableId));
                        break;
                }
            }

            return result;
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
            var clipPatterns = em.GetBuffer<SourceClipPatternBuffer>(entity);
            var sustainCandidates = em.GetBuffer<SourceSustainSlotCandidateBuffer>(entity);
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
            clipPatterns.Clear();
            sustainCandidates.Clear();
            sustainRuntimeLanes.Clear();
            eventQueue.Clear();
            pollutionDrops.Clear();
            SourceRuntimeApplyUtility.ResetPressureInputs(pressureInputs);

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

        private static void DisableSourceInstance(EntityManager em, Entity entity)
        {
            if (!em.Exists(entity))
                return;

            DisableSource(em, entity);
            em.SetEnabled(entity, false);
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

            deposit.Radius = math.max(0f, depositData.Radius);
            tx.Position = new float3(depositData.Position.x, depositData.Position.y, depositData.Position.z);

            em.SetComponentData(entity, deposit);
            em.SetComponentData(entity, tx);
        }

        private static void DisableDepositInstance(EntityManager em, Entity entity)
        {
            if (!em.Exists(entity))
                return;

            DisableDeposit(em, entity);
            em.SetEnabled(entity, false);
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

        private enum StageTopologyKind : byte
        {
            Source = 0,
            Deposit = 1,
        }
    }
}




