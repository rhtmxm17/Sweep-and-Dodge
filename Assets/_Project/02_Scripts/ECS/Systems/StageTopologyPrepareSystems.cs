using Unity.Collections;
using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(StageTopologyPrepareGroup), OrderFirst = true)]
    public partial struct StageTopologyBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;

            EnsureSingleton(em, default(StageTopologyRequestComponent));
            EnsureSingleton(em, default(StageTopologyStateComponent));
            EnsureSingleton(em, default(StageTopologyLifecycleStateComponent));
            EnsureSingleton(em, default(StageTopologyPrefabCatalogComponent));
            EnsureSingleton(em, new StageSessionResetBootstrapComponent
            {
                InitialResetPending = 1,
            });

            using var stageCatalogRuntimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageCatalogRuntimeComponent>());
            if (stageCatalogRuntimeQuery.IsEmptyIgnoreFilter)
            {
                var entity = em.CreateEntity();
                em.AddComponentObject(entity, new StageCatalogRuntimeComponent
                {
                    Catalog = null,
                });
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var stageConfig = EnsureRunDirectorStageConfigSingleton(em);
            EnsureSingleton(em, new RunDirectorStageStateComponent
            {
                State = stageConfig.InitialState,
                StateElapsedSec = 0f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });
            EnsureSingleton(em, new RunDirectorStageGateComponent
            {
                IntroPresentationDone = 1,
                ClearPresentationDone = 1,
                MinIdleDurationElapsed = 1,
                AutoAdvanceTimeoutElapsed = 0,
            });
            EnsureSingleton(em, default(RunDirectorStageRequestComponent));
            EnsureSingleton(em, default(RunDirectorStageSignalComponent));
        }

        private static void EnsureSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!query.IsEmptyIgnoreFilter)
                return;

            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }

        private static bool HasSingleton<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static RunDirectorStageConfigComponent EnsureRunDirectorStageConfigSingleton(EntityManager em)
        {
            var defaultValue = new RunDirectorStageConfigComponent
            {
                InitialState = RunDirectorStageStateId.Idle,
                MinIdleDurationSec = 0f,
                ClearAutoAdvanceTimeoutSec = 10f,
            };

            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<RunDirectorStageConfigComponent>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = em.CreateEntity(typeof(RunDirectorStageConfigComponent));
                em.SetComponentData(entity, defaultValue);
                return defaultValue;
            }

            if (query.CalculateEntityCount() == 1)
                return query.GetSingleton<RunDirectorStageConfigComponent>();

            using var entities = query.ToEntityArray(Allocator.Temp);
            Entity keeper = entities[0];
            var keeperValue = em.GetComponentData<RunDirectorStageConfigComponent>(keeper);
            for (int i = 1; i < entities.Length; i++)
            {
                var candidate = em.GetComponentData<RunDirectorStageConfigComponent>(entities[i]);
                if (IsPreferredStageConfig(candidate, keeperValue, defaultValue))
                {
                    keeper = entities[i];
                    keeperValue = candidate;
                }
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] == keeper || !em.Exists(entities[i]))
                    continue;

                em.RemoveComponent<RunDirectorStageConfigComponent>(entities[i]);
            }

            return keeperValue;
        }

        private static bool IsPreferredStageConfig(
            in RunDirectorStageConfigComponent candidate,
            in RunDirectorStageConfigComponent current,
            in RunDirectorStageConfigComponent defaultValue)
        {
            bool candidateIsDefault = candidate.InitialState == defaultValue.InitialState
                && candidate.MinIdleDurationSec == defaultValue.MinIdleDurationSec
                && candidate.ClearAutoAdvanceTimeoutSec == defaultValue.ClearAutoAdvanceTimeoutSec;
            bool currentIsDefault = current.InitialState == defaultValue.InitialState
                && current.MinIdleDurationSec == defaultValue.MinIdleDurationSec
                && current.ClearAutoAdvanceTimeoutSec == defaultValue.ClearAutoAdvanceTimeoutSec;

            return currentIsDefault && !candidateIsDefault;
        }
    }

    [UpdateInGroup(typeof(StageTopologyPrepareGroup))]
    [UpdateAfter(typeof(StageTopologyBootstrapSystem))]
    [UpdateBefore(typeof(StageTopologyApplyPrepareSystem))]
    public partial struct StageSessionResetPrepareSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StageSessionResetBootstrapComponent>();
            state.RequireForUpdate<StageTopologyRequestComponent>();
            state.RequireForUpdate<StageTopologyStateComponent>();
            state.RequireForUpdate<StageTopologyLifecycleStateComponent>();
            state.RequireForUpdate<RunDirectorStageConfigComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<RunDirectorStageGateComponent>();
            state.RequireForUpdate<RunDirectorStageRequestComponent>();
            state.RequireForUpdate<RunDirectorStageSignalComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            em.CompleteAllTrackedJobs();
            state.CompleteDependency();

            var bootstrapEntity = SystemAPI.GetSingletonEntity<StageSessionResetBootstrapComponent>();
            var bootstrap = em.GetComponentData<StageSessionResetBootstrapComponent>(bootstrapEntity);
            bool bootResetPending = bootstrap.InitialResetPending != 0;

            var topologyRequestEntity = SystemAPI.GetSingletonEntity<StageTopologyRequestComponent>();
            var topologyRequest = em.GetComponentData<StageTopologyRequestComponent>(topologyRequestEntity);
            bool explicitApplyRequested = topologyRequest.ApplyRequested != 0;
            var stageRequestEntity = SystemAPI.GetSingletonEntity<RunDirectorStageRequestComponent>();
            var stageRequest = em.GetComponentData<RunDirectorStageRequestComponent>(stageRequestEntity);
            var gateEntity = SystemAPI.GetSingletonEntity<RunDirectorStageGateComponent>();
            var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);
            var stageStateEntity = SystemAPI.GetSingletonEntity<RunDirectorStageStateComponent>();
            var stageState = em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity);
            var signalEntity = SystemAPI.GetSingletonEntity<RunDirectorStageSignalComponent>();
            var signal = em.GetComponentData<RunDirectorStageSignalComponent>(signalEntity);
            var topologyStateEntity = SystemAPI.GetSingletonEntity<StageTopologyStateComponent>();
            var topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
            var lifecycleEntity = SystemAPI.GetSingletonEntity<StageTopologyLifecycleStateComponent>();
            var lifecycle = em.GetComponentData<StageTopologyLifecycleStateComponent>(lifecycleEntity);
            var stageConfig = SystemAPI.GetSingleton<RunDirectorStageConfigComponent>();

            bool bootResetNeeded = bootResetPending
                && ShouldPerformBootSafetyReset(
                    stageState,
                    stageRequest,
                    topologyRequest,
                    signal,
                    topologyState,
                    lifecycle);

            if (bootResetPending && !bootResetNeeded)
            {
                bootstrap.InitialResetPending = 0;
                em.SetComponentData(bootstrapEntity, bootstrap);
            }

            if (!bootResetNeeded)
            {
                bool allowStageEntryReset = stageRequest.StageStartRequested != 0;
                if (!explicitApplyRequested
                    || !(allowStageEntryReset
                        || StageTopologyPrepareBoundaryUtility.IsApplyBoundaryState(stageState.State, topologyState)))
                {
                    return;
                }
            }

            ResetStageSession(
                em,
                stageRequestEntity,
                gateEntity,
                stageStateEntity,
                signalEntity,
                topologyStateEntity,
                lifecycleEntity,
                bootResetPending,
                preserveStageEntrySignals: explicitApplyRequested,
                stageConfig);

            if (bootResetPending)
            {
                bootstrap.InitialResetPending = 0;
                em.SetComponentData(bootstrapEntity, bootstrap);
            }
        }

        private static bool ShouldPerformBootSafetyReset(
            in RunDirectorStageStateComponent stageState,
            in RunDirectorStageRequestComponent stageRequest,
            in StageTopologyRequestComponent topologyRequest,
            in RunDirectorStageSignalComponent signal,
            in StageTopologyStateComponent topologyState,
            in StageTopologyLifecycleStateComponent lifecycle)
        {
            if (stageRequest.StageStartRequested != 0 || topologyRequest.ApplyRequested != 0)
                return false;

            if (stageState.State != RunDirectorStageStateId.Completed)
                return false;

            if (signal.StageRunCompleted != 0)
                return true;

            if (topologyState.SelectedStageId != 0
                || topologyState.AppliedStageId != 0
                || topologyState.Ready != 0)
                return true;

            if (lifecycle.CurrentAppliedVersion != 0u)
                return true;

            return false;
        }

        private static void ResetStageSession(
            EntityManager em,
            Entity stageRequestEntity,
            Entity gateEntity,
            Entity stageStateEntity,
            Entity signalEntity,
            Entity topologyStateEntity,
            Entity lifecycleEntity,
            bool bootReset,
            bool preserveStageEntrySignals,
            in RunDirectorStageConfigComponent stageConfig)
        {
            byte preservedStartRequested = 0;
            byte preservedIntroDone = 0;
            byte preservedClearDone = 0;

            var stageRequest = em.GetComponentData<RunDirectorStageRequestComponent>(stageRequestEntity);
            if (preserveStageEntrySignals)
                preservedStartRequested = stageRequest.StageStartRequested;
            stageRequest = default;
            stageRequest.StageStartRequested = preservedStartRequested;
            em.SetComponentData(stageRequestEntity, stageRequest);

            var gate = em.GetComponentData<RunDirectorStageGateComponent>(gateEntity);
            if (bootReset)
            {
                preservedIntroDone = 1;
                preservedClearDone = 1;
            }
            else if (preserveStageEntrySignals)
            {
                preservedIntroDone = gate.IntroPresentationDone;
                preservedClearDone = gate.ClearPresentationDone;
            }

            gate.IntroPresentationDone = preservedIntroDone;
            gate.ClearPresentationDone = preservedClearDone;
            gate.MinIdleDurationElapsed = stageConfig.MinIdleDurationSec <= 0f ? (byte)1 : (byte)0;
            gate.AutoAdvanceTimeoutElapsed = 0;
            em.SetComponentData(gateEntity, gate);

            em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
            {
                State = bootReset ? stageConfig.InitialState : RunDirectorStageStateId.Idle,
                StateElapsedSec = 0f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });

            em.SetComponentData(signalEntity, default(RunDirectorStageSignalComponent));

            em.SetComponentData(topologyStateEntity, default(StageTopologyStateComponent));

            em.SetComponentData(lifecycleEntity, default(StageTopologyLifecycleStateComponent));
            ResetPlayerHazardRisk(em);
        }

        private static void ResetPlayerHazardRisk(EntityManager em)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadWrite<PlayerHazardRiskStateComponent>(),
                ComponentType.ReadWrite<PlayerHazardRiskRequestComponent>());
            if (query.IsEmptyIgnoreFilter)
                return;

            using var players = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                em.SetComponentData(player, new PlayerHazardRiskStateComponent
                {
                    HazardStack = 0,
                });
                em.SetComponentData(player, new PlayerHazardRiskRequestComponent
                {
                    PendingHazardCapturedCount = 0,
                    ResetRequested = 0,
                });
            }
        }
    }

    internal static class StageTopologyPrepareBoundaryUtility
    {
        public static bool IsApplyBoundaryState(
            RunDirectorStageStateId state,
            StageTopologyStateComponent topologyState)
        {
            if (state == RunDirectorStageStateId.Idle || state == RunDirectorStageStateId.Completed)
                return true;

            return state == RunDirectorStageStateId.Running
                && topologyState.SelectedStageId <= 0
                && topologyState.AppliedStageId <= 0
                && topologyState.Ready == 0;
        }
    }

    internal static class StageTopologyRuntimeGateUtility
    {
        public static bool IsTopologyReadyForGameplay(in StageTopologyStateComponent topologyState)
        {
            if (topologyState.SelectedStageId <= 0
                && topologyState.AppliedStageId <= 0
                && topologyState.Ready == 0)
            {
                return true;
            }

            return topologyState.Ready != 0
                && topologyState.SelectedStageId > 0
                && topologyState.AppliedStageId == topologyState.SelectedStageId;
        }

        public static bool ShouldRunGameplay(
            in StageTopologyStateComponent topologyState,
            in RunDirectorStageStateComponent stageState)
        {
            return IsTopologyReadyForGameplay(in topologyState)
                && stageState.State == RunDirectorStageStateId.Running;
        }
    }
}
