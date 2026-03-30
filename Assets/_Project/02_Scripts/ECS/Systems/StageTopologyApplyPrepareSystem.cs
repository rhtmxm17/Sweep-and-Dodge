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
    /// - Source topology entity set은 기존 계약대로 reconcile한다.
    /// - Movement/Deposit runtime은 StageLayoutSO grid를 singleton cache로 publish한다.
    /// </summary>
    [UpdateInGroup(typeof(StageTopologyPrepareGroup))]
    [UpdateAfter(typeof(StageTopologyBootstrapSystem))]
    public partial struct StageTopologyApplyPrepareSystem : ISystem
    {
        private struct SourceRegionRuntimeData
        {
            public StageSourceRegionLayoutData Region;
            public NativeList<int> OwnedCellIndices;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageTopologyRequestComponent>();
            state.RequireForUpdate<StageTopologyStateComponent>();
            state.RequireForUpdate<StageTopologyLifecycleStateComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<StageCatalogRuntimeComponent>();
            state.RequireForUpdate<StageTopologyPrefabCatalogComponent>();
            state.RequireForUpdate<StageRuntimeGridComponent>();
            state.RequireForUpdate<StagePlayerStartRuntimeComponent>();
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
            if (!StageTopologyPrepareBoundaryUtility.IsApplyBoundaryState(stageState.State, topologyState))
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

            bool needsSourceTemplate = HasActiveSourceRegion(entry.Layout);
            if (needsSourceTemplate && prefabs.SourceTemplate == Entity.Null)
            {
                Debug.LogWarning($"[StageTopologyApply] Source template prefab is missing. stageId={requestedStageId}");
                return;
            }

            if (!ApplyGridCache(ref state, requestedStageId, entry.Layout))
            {
                Debug.LogWarning($"[StageTopologyApply] Stage runtime grid cache build failed. stageId={requestedStageId}");
                return;
            }

            var lifecycleStateEntity = SystemAPI.GetSingletonEntity<StageTopologyLifecycleStateComponent>();
            var lifecycleState = em.GetComponentData<StageTopologyLifecycleStateComponent>(lifecycleStateEntity);
            uint currentApplyVersion = lifecycleState.CurrentAppliedVersion + 1u;
            if (currentApplyVersion == 0u)
                currentApplyVersion = 1u;

            if (!ApplyPlayerStartRuntime(ref state, requestedStageId, entry.Layout, currentApplyVersion))
            {
                Debug.LogWarning($"[StageTopologyApply] Player start runtime apply failed. stageId={requestedStageId}");
                return;
            }

            ApplySourceTopology(ref state, requestedStageId, prefabs.SourceTemplate, entry.Layout, entry.Definition, currentApplyVersion);
            CleanupUnmappedOwnedEntities(em, currentApplyVersion);

            lifecycleState.CurrentAppliedVersion = currentApplyVersion;
            em.SetComponentData(lifecycleStateEntity, lifecycleState);

            topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
            topologyState.SelectedStageId = requestedStageId;
            topologyState.AppliedStageId = requestedStageId;
            topologyState.Ready = 1;
            em.SetComponentData(topologyStateEntity, topologyState);
        }

        private static bool ApplyGridCache(ref SystemState state, int stageId, StageLayoutSO layout)
        {
            if (layout == null)
                return false;

            var gridSpec = layout.Grid;
            if (gridSpec.Width <= 0 || gridSpec.Height <= 0 || gridSpec.CellSize <= 0f)
                return false;

            int expectedCellCount = gridSpec.Width * gridSpec.Height;
            if (layout.Cells == null || layout.Cells.Length != expectedCellCount)
                return false;

            var em = state.EntityManager;
            using var gridQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>());
            if (gridQuery.IsEmptyIgnoreFilter)
                return false;

            var gridEntity = ResolveFirstEntity(gridQuery);
            if (gridEntity == Entity.Null || !em.Exists(gridEntity))
                return false;

            var buffer = em.GetBuffer<StageRuntimeGridCellBufferElement>(gridEntity);
            buffer.ResizeUninitialized(expectedCellCount);
            for (int i = 0; i < expectedCellCount; i++)
            {
                buffer[i] = new StageRuntimeGridCellBufferElement
                {
                    MovementFlags = layout.Cells[i].MovementFlags,
                    SourceRegionId = layout.Cells[i].SourceRegionId,
                    DepositRegionId = layout.Cells[i].DepositRegionId,
                };
            }

            em.SetComponentData(gridEntity, new StageRuntimeGridComponent
            {
                StageId = stageId,
                Width = gridSpec.Width,
                Height = gridSpec.Height,
                CellSize = gridSpec.CellSize,
                OriginX = gridSpec.Origin.x,
                OriginZ = gridSpec.Origin.z,
                Ready = 1,
            });
            return true;
        }

        private static bool ApplyPlayerStartRuntime(ref SystemState state, int stageId, StageLayoutSO layout, uint currentApplyVersion)
        {
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<StagePlayerStartRuntimeComponent>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            Entity entity = ResolveFirstEntity(query);
            if (entity == Entity.Null || !em.Exists(entity))
                return false;

            var runtime = default(StagePlayerStartRuntimeComponent);
            if (layout == null)
            {
                em.SetComponentData(entity, runtime);
                return false;
            }

            runtime.StageId = stageId;
            runtime.YawDeg = layout.PlayerStart.YawDeg;
            runtime.AppliedVersion = currentApplyVersion;

            if (!TryResolvePlayerStartPose(layout, out float3 position, out float yawDeg))
            {
                em.SetComponentData(entity, runtime);
                return false;
            }

            runtime.PositionX = position.x;
            runtime.PositionY = position.y;
            runtime.PositionZ = position.z;
            runtime.YawDeg = yawDeg;
            runtime.Ready = 1;
            em.SetComponentData(entity, runtime);
            return true;
        }

        private static bool TryResolvePlayerStartPose(StageLayoutSO layout, out float3 position, out float yawDeg)
        {
            position = float3.zero;
            yawDeg = 0f;

            if (layout == null)
                return false;

            var playerStart = layout.PlayerStart;
            if (!playerStart.Active)
            {
                Debug.LogWarning($"[StageTopologyApply] PlayerStart is missing or inactive. stageId={layout.StageId}");
                return false;
            }

            var grid = layout.Grid;
            if (grid.Width <= 0 || grid.Height <= 0 || grid.CellSize <= 0f)
            {
                Debug.LogWarning($"[StageTopologyApply] Grid spec is invalid while resolving PlayerStart. stageId={layout.StageId}");
                return false;
            }

            if (playerStart.AnchorCell.x < 0
                || playerStart.AnchorCell.y < 0
                || playerStart.AnchorCell.x >= grid.Width
                || playerStart.AnchorCell.y >= grid.Height)
            {
                Debug.LogWarning($"[StageTopologyApply] PlayerStart AnchorCell is out of bounds. stageId={layout.StageId}, anchor={playerStart.AnchorCell}");
                return false;
            }

            int expectedCellCount = grid.Width * grid.Height;
            if (layout.Cells == null || layout.Cells.Length != expectedCellCount)
            {
                Debug.LogWarning($"[StageTopologyApply] Cells are invalid while resolving PlayerStart. stageId={layout.StageId}");
                return false;
            }

            int index = (playerStart.AnchorCell.y * grid.Width) + playerStart.AnchorCell.x;
            var cell = layout.Cells[index];
            if ((cell.MovementFlags & StageCellMovementFlags.BlockPlayer) != 0)
            {
                Debug.LogWarning($"[StageTopologyApply] PlayerStart cell is blocked. stageId={layout.StageId}, anchor={playerStart.AnchorCell}");
                return false;
            }

            position = StageRuntimeGridUtility.GetAnchorWorldPosition(
                in grid,
                new int2(playerStart.AnchorCell.x, playerStart.AnchorCell.y),
                new float2(playerStart.AnchorOffset.x, playerStart.AnchorOffset.y),
                grid.Origin.y);
            yawDeg = playerStart.YawDeg;
            return true;
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
            int bestScore = -1;
            for (int i = 0; i < entities.Length; i++)
            {
                if (!em.Exists(entities[i]))
                    continue;

                var candidate = em.GetComponentData<StageTopologyPrefabCatalogComponent>(entities[i]);
                int score = candidate.SourceTemplate != Entity.Null ? 1 : 0;
                if (selected == Entity.Null || score > bestScore)
                {
                    selected = entities[i];
                    prefabs = candidate;
                    bestScore = score;
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
            StageDefinitionSO definition,
            uint currentApplyVersion)
        {
            var em = state.EntityManager;
            var layoutById = BuildSourceRegionMap(layout, out int layoutDuplicateCount);
            var activeLayoutIds = BuildActiveSourceStableIdSet(layoutById.Keys);
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

                if (duplicateActiveIds.Contains(stableId) || definitionDuplicateIds.Contains(stableId))
                    continue;

                Entity sourceEntity = ResolveTopologyEntity(
                    em,
                    stableId,
                    sourceTemplate,
                    ref activeById,
                    reusableEntities);
                if (sourceEntity == Entity.Null)
                {
                    Debug.LogWarning($"[StageTopologyApply] Failed to resolve source instance. stageId={stageId}, stableId={stableId}");
                    continue;
                }

                em.SetEnabled(sourceEntity, true);
                EnsureSourceTags(em, sourceEntity);
                em.SetComponentData(sourceEntity, new SourceStableIdComponent { Value = stableId });
                ApplySourceLayout(em, sourceEntity, in layout.Grid, layoutData);

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
                {
                    StampTopologyOwnedEntity(em, sourceEntity, StageTopologyKind.Source, currentApplyVersion);
                    mappedEntities.Add(sourceEntity);
                }
            }

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var entity = sourceEntities[i];
                if (mappedEntities.Contains(entity))
                    continue;

                DisableSourceInstance(em, entity);
            }

            foreach (var value in layoutById.Values)
            {
                if (value.OwnedCellIndices.IsCreated)
                    value.OwnedCellIndices.Dispose();
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
            reusableEntities = new List<Entity>();
            duplicateActiveIds = new HashSet<uint>();

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.Exists(entity))
                    continue;

                uint stableId = stableIdAccessor(entity);
                if (!em.IsEnabled(entity) || !activeLayoutIds.Contains(stableId))
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
            List<Entity> reusableEntities)
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
            EnsureSourceTags(em, created);
            return created;
        }

        private static void EnsureSourceTags(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            EnsureOwnedMetadata(em, entity, StageTopologyKind.Source);
            if (!em.HasComponent<StageTopologySourceTag>(entity))
                em.AddComponent<StageTopologySourceTag>(entity);
            if (!em.HasComponent<BulletFieldAreaComponent>(entity))
                em.AddComponent<BulletFieldAreaComponent>(entity);
            if (!em.HasComponent<Shape2DComponent>(entity))
            {
                em.AddComponentData(entity, new Shape2DComponent
                {
                    Kind = Shape2DKind.Circle,
                    Radius = 1f,
                    Size = float2.zero,
                });
            }
            if (!em.HasComponent<SourceShapeDerivedComponent>(entity))
            {
                var defaultShape = em.GetComponentData<Shape2DComponent>(entity);
                var derived = default(SourceShapeDerivedComponent);
                SourceRuntimeApplyUtility.RefreshSourceShapeDerived(in defaultShape, ref derived);
                em.AddComponentData(entity, derived);
            }
            if (!em.HasBuffer<SourceRegionCellIndexBuffer>(entity))
                em.AddBuffer<SourceRegionCellIndexBuffer>(entity);
        }

        private static void EnsureOwnedMetadata(EntityManager em, Entity entity, StageTopologyKind kind)
        {
            if (!em.HasComponent<StageTopologyOwnedComponent>(entity))
            {
                em.AddComponentData(entity, new StageTopologyOwnedComponent
                {
                    Kind = kind,
                    LastAppliedVersion = 0u,
                });
                return;
            }

            var owned = em.GetComponentData<StageTopologyOwnedComponent>(entity);
            owned.Kind = kind;
            em.SetComponentData(entity, owned);
        }

        private static void StampTopologyOwnedEntity(EntityManager em, Entity entity, StageTopologyKind kind, uint currentApplyVersion)
        {
            EnsureOwnedMetadata(em, entity, kind);
            var owned = em.GetComponentData<StageTopologyOwnedComponent>(entity);
            owned.Kind = kind;
            owned.LastAppliedVersion = currentApplyVersion;
            em.SetComponentData(entity, owned);
        }

        private static void CleanupUnmappedOwnedEntities(EntityManager em, uint currentApplyVersion)
        {
            using var ownedQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var ownedEntities = ownedQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ownedEntities.Length; i++)
            {
                var entity = ownedEntities[i];
                if (!em.Exists(entity))
                    continue;

                if (!em.HasComponent<StageTopologyOwnedComponent>(entity))
                {
                    em.SetEnabled(entity, false);
                    continue;
                }

                var owned = em.GetComponentData<StageTopologyOwnedComponent>(entity);
                if (owned.LastAppliedVersion == currentApplyVersion)
                    continue;

                if (owned.Kind == StageTopologyKind.Source)
                    DisableSourceInstance(em, entity);
                else
                    em.SetEnabled(entity, false);
            }
        }

        private static bool HasActiveSourceRegion(StageLayoutSO layout)
        {
            if (layout?.SourceRegions == null)
                return false;

            for (int i = 0; i < layout.SourceRegions.Length; i++)
            {
                if (layout.SourceRegions[i].Active)
                    return true;
            }

            return false;
        }

        private static Dictionary<uint, SourceRegionRuntimeData> BuildSourceRegionMap(StageLayoutSO layout, out int duplicateCount)
        {
            duplicateCount = 0;
            var map = new Dictionary<uint, SourceRegionRuntimeData>();
            var duplicateIds = new HashSet<uint>();

            if (layout == null || layout.SourceRegions == null || layout.Cells == null)
                return map;

            for (int i = 0; i < layout.SourceRegions.Length; i++)
            {
                var region = layout.SourceRegions[i];
                uint stableId = math.max(1u, region.StableId);
                if (!region.Active)
                    continue;
                if (duplicateIds.Contains(stableId))
                    continue;

                if (map.ContainsKey(stableId))
                {
                    map[stableId].OwnedCellIndices.Dispose();
                    map.Remove(stableId);
                    duplicateIds.Add(stableId);
                    duplicateCount++;
                    continue;
                }

                map.Add(stableId, new SourceRegionRuntimeData
                {
                    Region = region,
                    OwnedCellIndices = new NativeList<int>(Allocator.Temp),
                });
            }

            int cellCount = layout.Cells.Length;
            for (int i = 0; i < cellCount; i++)
            {
                uint stableId = layout.Cells[i].SourceRegionId;
                if (stableId == 0u || duplicateIds.Contains(stableId))
                    continue;
                if (!map.TryGetValue(stableId, out var runtime))
                    continue;

                runtime.OwnedCellIndices.Add(i);
                map[stableId] = runtime;
            }

            return map;
        }

        private static HashSet<uint> BuildActiveSourceStableIdSet(Dictionary<uint, SourceRegionRuntimeData>.KeyCollection keys)
        {
            var result = new HashSet<uint>();
            foreach (var key in keys)
                result.Add(math.max(1u, key));
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

        private static void ApplySourceLayout(EntityManager em, Entity entity, in StageGridSpec gridSpec, SourceRegionRuntimeData sourceData)
        {
            var anchor = em.GetComponentData<SourceAnchorComponent>(entity);
            var shape = em.GetComponentData<Shape2DComponent>(entity);
            var derived = em.GetComponentData<SourceShapeDerivedComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);
            var pollutionConfig = em.GetComponentData<SourcePollutionConfigComponent>(entity);
            var pollutionGrid = em.GetComponentData<SourcePollutionGridComponent>(entity);
            var regionCellIndices = em.GetBuffer<SourceRegionCellIndexBuffer>(entity);
            regionCellIndices.Clear();

            int ownedCount = sourceData.OwnedCellIndices.Length;
            int stageWidth = math.max(1, gridSpec.Width);
            int minCellX = int.MaxValue;
            int minCellY = int.MaxValue;
            int maxCellX = int.MinValue;
            int maxCellY = int.MinValue;

            for (int i = 0; i < ownedCount; i++)
            {
                int globalIndex = sourceData.OwnedCellIndices[i];
                regionCellIndices.Add(new SourceRegionCellIndexBuffer { Value = globalIndex });
                int cellX = globalIndex % stageWidth;
                int cellY = globalIndex / stageWidth;
                minCellX = math.min(minCellX, cellX);
                minCellY = math.min(minCellY, cellY);
                maxCellX = math.max(maxCellX, cellX);
                maxCellY = math.max(maxCellY, cellY);
            }

            if (ownedCount <= 0)
            {
                minCellX = maxCellX = math.clamp(sourceData.Region.AnchorCell.x, 0, math.max(0, gridSpec.Width - 1));
                minCellY = maxCellY = math.clamp(sourceData.Region.AnchorCell.y, 0, math.max(0, gridSpec.Height - 1));
            }

            float3 position = StageRuntimeGridUtility.GetAnchorWorldPosition(
                in gridSpec,
                new int2(sourceData.Region.AnchorCell.x, sourceData.Region.AnchorCell.y),
                new float2(sourceData.Region.AnchorOffset.x, sourceData.Region.AnchorOffset.y),
                gridSpec.Origin.y);
            anchor.Position = position;
            tx.Position = position;
            tx.Rotation = quaternion.identity;

            float boundsMinX = gridSpec.Origin.x + (minCellX * gridSpec.CellSize);
            float boundsMinZ = gridSpec.Origin.z + (minCellY * gridSpec.CellSize);
            float boundsMaxX = gridSpec.Origin.x + ((maxCellX + 1) * gridSpec.CellSize);
            float boundsMaxZ = gridSpec.Origin.z + ((maxCellY + 1) * gridSpec.CellSize);
            float2 halfExtents = new float2(
                math.max(math.abs(position.x - boundsMinX), math.abs(boundsMaxX - position.x)),
                math.max(math.abs(position.z - boundsMinZ), math.abs(boundsMaxZ - position.z)));

            shape.Kind = Shape2DKind.Rectangle;
            shape.Radius = 0f;
            shape.Size = math.max(float2.zero, halfExtents * 2f);
            derived.ComputedArea = ownedCount * gridSpec.CellSize * gridSpec.CellSize;
            derived.HalfExtents = halfExtents;

            SourceRuntimeApplyUtility.RebuildPollutionGridFromRegionBounds(
                minCellX,
                minCellY,
                maxCellX,
                maxCellY,
                stageWidth,
                gridSpec.CellSize,
                gridSpec.Origin.x,
                gridSpec.Origin.z,
                in pollutionConfig,
                regionCellIndices,
                ref pollutionGrid,
                em.GetBuffer<SourcePollutionCellBuffer>(entity),
                em.GetBuffer<SourcePollutionDropRequestBuffer>(entity),
                em.GetBuffer<SourcePollutionValidCellIndexBuffer>(entity));

            em.SetComponentData(entity, anchor);
            em.SetComponentData(entity, shape);
            em.SetComponentData(entity, derived);
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
