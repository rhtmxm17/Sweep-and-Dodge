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
    /// - StageTopologyPrepareGroup에서 topology apply 요청을 소비한다.
    /// - StageCatalog + topology template prefab을 이용해 Source/Deposit/Obstacle entity set을 reconcile한다.
    /// </summary>
    [UpdateInGroup(typeof(StageTopologyPrepareGroup))]
    [UpdateAfter(typeof(StageTopologyBootstrapSystem))]
    public partial struct StageTopologyApplyPrepareSystem : ISystem
    {
        private static readonly float3 DepositSinkPosition = new float3(0f, -10000f, 0f);

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageTopologyRequestComponent>();
            state.RequireForUpdate<StageTopologyStateComponent>();
            state.RequireForUpdate<StageTopologyLifecycleStateComponent>();
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

            bool needsSourceTemplate = entry.Layout.Sources != null && entry.Layout.Sources.Length > 0;
            bool needsDepositTemplate = entry.Layout.Deposits != null && entry.Layout.Deposits.Length > 0;
            bool needsObstacleTemplate = HasActiveObstacles(entry.Layout.Obstacles);
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

            if (needsObstacleTemplate && prefabs.ObstacleTemplate == Entity.Null)
            {
                Debug.LogWarning($"[StageTopologyApply] Obstacle template prefab is missing. stageId={requestedStageId}");
                return;
            }

            var lifecycleStateEntity = SystemAPI.GetSingletonEntity<StageTopologyLifecycleStateComponent>();
            var lifecycleState = em.GetComponentData<StageTopologyLifecycleStateComponent>(lifecycleStateEntity);
            uint currentApplyVersion = lifecycleState.CurrentAppliedVersion + 1u;
            if (currentApplyVersion == 0u)
                currentApplyVersion = 1u;

            ApplySourceTopology(ref state, requestedStageId, prefabs.SourceTemplate, entry.Layout, entry.Definition, currentApplyVersion);
            ApplyDepositTopology(ref state, requestedStageId, prefabs.DepositTemplate, entry.Layout, currentApplyVersion);
            ApplyObstacleTopology(ref state, requestedStageId, prefabs.ObstacleTemplate, entry.Layout, currentApplyVersion);
            CleanupUnmappedOwnedEntities(em, currentApplyVersion);

            lifecycleState.CurrentAppliedVersion = currentApplyVersion;
            em.SetComponentData(lifecycleStateEntity, lifecycleState);

            topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
            topologyState.SelectedStageId = requestedStageId;
            topologyState.AppliedStageId = requestedStageId;
            topologyState.Ready = 1;
            em.SetComponentData(topologyStateEntity, topologyState);
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
                int score = 0;
                if (candidate.SourceTemplate != Entity.Null)
                    score++;
                if (candidate.DepositTemplate != Entity.Null)
                    score++;
                if (candidate.ObstacleTemplate != Entity.Null)
                    score++;

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
        }

        private static void ApplyDepositTopology(
            ref SystemState state,
            int stageId,
            Entity depositTemplate,
            StageLayoutSO layout,
            uint currentApplyVersion)
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
                StampTopologyOwnedEntity(em, depositEntity, StageTopologyKind.Deposit, currentApplyVersion);
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

        private static void ApplyObstacleTopology(
            ref SystemState state,
            int stageId,
            Entity obstacleTemplate,
            StageLayoutSO layout,
            uint currentApplyVersion)
        {
            var em = state.EntityManager;
            var layoutById = BuildStageObstacleMap(layout != null ? layout.Obstacles : null, out int layoutDuplicateCount);
            var activeLayoutIds = BuildActiveStableIdSet(layoutById.Values);
            if (layoutDuplicateCount > 0)
                Debug.LogWarning($"[StageTopologyApply] Duplicate obstacle stableId in layout. stageId={stageId}, duplicateCount={layoutDuplicateCount}");

            using var obstacleQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologyObstacleTag>(),
                    ComponentType.ReadOnly<ObstacleStableIdComponent>(),
                    ComponentType.ReadOnly<ObstacleGeometryComponent>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var obstacleEntities = obstacleQuery.ToEntityArray(Allocator.Temp);
            BuildRuntimeInstanceSets(
                em,
                obstacleEntities,
                activeLayoutIds,
                stableIdAccessor: entity => math.max(1u, em.GetComponentData<ObstacleStableIdComponent>(entity).Value),
                out var activeById,
                out var reusableEntities,
                out var duplicateActiveIds);

            if (duplicateActiveIds.Count > 0)
                Debug.LogWarning($"[StageTopologyApply] Duplicate active runtime obstacle stableId detected. stageId={stageId}, duplicateCount={duplicateActiveIds.Count}");

            var mappedEntities = new HashSet<Entity>();
            foreach (var pair in layoutById)
            {
                uint stableId = pair.Key;
                var layoutData = pair.Value;
                if (!layoutData.Active || duplicateActiveIds.Contains(stableId))
                    continue;

                if (!TryValidateObstacleLayoutData(stageId, stableId, in layoutData, out string validationMessage))
                {
                    Debug.LogWarning(validationMessage);
                    continue;
                }

                Entity obstacleEntity = ResolveTopologyEntity(
                    em,
                    stableId,
                    obstacleTemplate,
                    ref activeById,
                    reusableEntities,
                    StageTopologyKind.Obstacle);
                if (obstacleEntity == Entity.Null)
                {
                    Debug.LogWarning($"[StageTopologyApply] Failed to resolve obstacle instance. stageId={stageId}, stableId={stableId}");
                    continue;
                }

                em.SetEnabled(obstacleEntity, true);
                EnsureObstacleTags(em, obstacleEntity);
                em.SetComponentData(obstacleEntity, new ObstacleStableIdComponent { Value = stableId });
                ApplyObstacle(em, obstacleEntity, layoutData);
                StampTopologyOwnedEntity(em, obstacleEntity, StageTopologyKind.Obstacle, currentApplyVersion);
                mappedEntities.Add(obstacleEntity);
            }

            for (int i = 0; i < obstacleEntities.Length; i++)
            {
                var entity = obstacleEntities[i];
                if (mappedEntities.Contains(entity))
                    continue;

                DisableObstacleInstance(em, entity);
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
                case StageTopologyKind.Obstacle:
                    EnsureObstacleTags(em, created);
                    break;
            }

            return created;
        }

        private static void EnsureSourceTags(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            EnsureOwnedMetadata(em, entity, StageTopologyKind.Source);
            if (!em.HasComponent<StageTopologySourceTag>(entity))
                em.AddComponent<StageTopologySourceTag>(entity);
            if (em.HasComponent<StageTopologyDepositTag>(entity))
                em.RemoveComponent<StageTopologyDepositTag>(entity);
            if (em.HasComponent<StageTopologyObstacleTag>(entity))
                em.RemoveComponent<StageTopologyObstacleTag>(entity);
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
        }

        private static void EnsureDepositTags(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            EnsureOwnedMetadata(em, entity, StageTopologyKind.Deposit);
            if (!em.HasComponent<StageTopologyDepositTag>(entity))
                em.AddComponent<StageTopologyDepositTag>(entity);
            if (em.HasComponent<StageTopologySourceTag>(entity))
                em.RemoveComponent<StageTopologySourceTag>(entity);
            if (em.HasComponent<StageTopologyObstacleTag>(entity))
                em.RemoveComponent<StageTopologyObstacleTag>(entity);
            if (!em.HasComponent<DepositPointComponent>(entity))
                em.AddComponent<DepositPointComponent>(entity);
            if (!em.HasComponent<Shape2DComponent>(entity))
            {
                em.AddComponentData(entity, new Shape2DComponent
                {
                    Kind = Shape2DKind.Circle,
                    Radius = 1f,
                    Size = float2.zero,
                });
            }
        }

        private static void EnsureObstacleTags(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            EnsureOwnedMetadata(em, entity, StageTopologyKind.Obstacle);
            if (!em.HasComponent<StageTopologyObstacleTag>(entity))
                em.AddComponent<StageTopologyObstacleTag>(entity);
            if (em.HasComponent<StageTopologySourceTag>(entity))
                em.RemoveComponent<StageTopologySourceTag>(entity);
            if (em.HasComponent<StageTopologyDepositTag>(entity))
                em.RemoveComponent<StageTopologyDepositTag>(entity);

            if (!em.HasComponent<ObstacleStableIdComponent>(entity))
                em.AddComponentData(entity, new ObstacleStableIdComponent { Value = 1u });
            if (!em.HasComponent<ObstacleCollisionMaskComponent>(entity))
            {
                em.AddComponentData(entity, new ObstacleCollisionMaskComponent
                {
                    Value = ObstacleCollisionMask.BlockPlayer | ObstacleCollisionMask.BlockBullet,
                });
            }
            if (!em.HasComponent<ObstacleGeometryComponent>(entity))
                em.AddComponent<ObstacleGeometryComponent>(entity);
            if (!em.HasComponent<Shape2DComponent>(entity))
            {
                em.AddComponentData(entity, new Shape2DComponent
                {
                    Kind = Shape2DKind.Rectangle,
                    Radius = 1f,
                    Size = new float2(2f, 2f),
                });
            }
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

                switch (owned.Kind)
                {
                    case StageTopologyKind.Source:
                        DisableSourceInstance(em, entity);
                        break;
                    case StageTopologyKind.Deposit:
                        DisableDepositInstance(em, entity);
                        break;
                    case StageTopologyKind.Obstacle:
                        DisableObstacleInstance(em, entity);
                        break;
                    default:
                        em.SetEnabled(entity, false);
                        break;
                }
            }
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
                    case StageObstacleLayoutData obstacleData when obstacleData.Active:
                        result.Add(math.max(1u, obstacleData.StableId));
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

        private static Dictionary<uint, StageObstacleLayoutData> BuildStageObstacleMap(StageObstacleLayoutData[] obstacles, out int duplicateCount)
        {
            duplicateCount = 0;
            var map = new Dictionary<uint, StageObstacleLayoutData>();
            var duplicateIds = new HashSet<uint>();

            if (obstacles == null)
                return map;

            for (int i = 0; i < obstacles.Length; i++)
            {
                uint stableId = math.max(1u, obstacles[i].StableId);
                if (duplicateIds.Contains(stableId))
                    continue;

                if (map.ContainsKey(stableId))
                {
                    map.Remove(stableId);
                    duplicateIds.Add(stableId);
                    duplicateCount++;
                    continue;
                }

                map.Add(stableId, obstacles[i]);
            }

            return map;
        }

        private static void ApplySourceLayout(EntityManager em, Entity entity, StageSourceLayoutData sourceData)
        {
            var anchor = em.GetComponentData<SourceAnchorComponent>(entity);
            var shape = em.GetComponentData<Shape2DComponent>(entity);
            var derived = em.GetComponentData<SourceShapeDerivedComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);
            var pollutionConfig = em.GetComponentData<SourcePollutionConfigComponent>(entity);
            var pollutionGrid = em.GetComponentData<SourcePollutionGridComponent>(entity);

            float3 position = new float3(sourceData.Position.x, sourceData.Position.y, sourceData.Position.z);
            anchor.Position = position;
            tx.Position = position;
            tx.Rotation = quaternion.RotateY(math.radians(sourceData.YawDeg));

            shape.Kind = sourceData.Shape;
            shape.Radius = math.max(0f, sourceData.Radius);
            shape.Size = math.max(float2.zero, new float2(sourceData.Size.x, sourceData.Size.y));
            SourceRuntimeApplyUtility.RefreshSourceShapeDerived(in shape, ref derived);

            SourceRuntimeApplyUtility.RebuildPollutionGrid(
                in shape,
                in derived,
                in pollutionConfig,
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

        private static void ApplyDeposit(EntityManager em, Entity entity, StageDepositLayoutData depositData)
        {
            var shape = em.GetComponentData<Shape2DComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);

            shape.Kind = depositData.Shape;
            shape.Radius = math.max(0f, depositData.Radius);
            shape.Size = math.max(float2.zero, new float2(depositData.Size.x, depositData.Size.y));
            tx.Position = new float3(depositData.Position.x, depositData.Position.y, depositData.Position.z);
            tx.Rotation = quaternion.RotateY(math.radians(depositData.YawDeg));

            em.SetComponentData(entity, shape);
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
            var shape = em.GetComponentData<Shape2DComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);
            shape.Radius = 0f;
            shape.Size = float2.zero;
            tx.Position = DepositSinkPosition;
            tx.Rotation = quaternion.identity;
            em.SetComponentData(entity, shape);
            em.SetComponentData(entity, tx);
        }

        private static void ApplyObstacle(EntityManager em, Entity entity, StageObstacleLayoutData obstacleData)
        {
            var shape = em.GetComponentData<Shape2DComponent>(entity);
            var mask = em.GetComponentData<ObstacleCollisionMaskComponent>(entity);
            var tx = em.GetComponentData<LocalTransform>(entity);

            shape.Kind = obstacleData.Shape;
            shape.Radius = math.max(0f, obstacleData.Radius);
            shape.Size = math.max(float2.zero, new float2(obstacleData.Size.x, obstacleData.Size.y));
            mask.Value = obstacleData.CollisionMask;

            tx.Position = new float3(obstacleData.Position.x, obstacleData.Position.y, obstacleData.Position.z);
            tx.Rotation = quaternion.RotateY(math.radians(obstacleData.YawDeg));

            em.SetComponentData(entity, shape);
            em.SetComponentData(entity, mask);
            em.SetComponentData(entity, tx);
        }

        private static void DisableObstacleInstance(EntityManager em, Entity entity)
        {
            if (!em.Exists(entity))
                return;

            em.SetEnabled(entity, false);
        }

        private static bool TryValidateObstacleLayoutData(int stageId, uint stableId, in StageObstacleLayoutData obstacleData, out string message)
        {
            if (obstacleData.CollisionMask == ObstacleCollisionMask.None)
            {
                message = $"[StageTopologyApply] Obstacle item has empty collision mask and will be skipped. stageId={stageId}, stableId={stableId}";
                return false;
            }

            bool validShape = obstacleData.Shape switch
            {
                Shape2DKind.Circle => obstacleData.Radius > 0f,
                Shape2DKind.Rectangle => obstacleData.Size.x > 0f && obstacleData.Size.y > 0f,
                _ => false,
            };

            if (!validShape)
            {
                message = $"[StageTopologyApply] Obstacle item has invalid shape parameters and will be skipped. stageId={stageId}, stableId={stableId}, shape={obstacleData.Shape}";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool HasActiveObstacles(StageObstacleLayoutData[] obstacles)
        {
            if (obstacles == null)
                return false;

            for (int i = 0; i < obstacles.Length; i++)
            {
                if (obstacles[i].Active)
                    return true;
            }

            return false;
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




