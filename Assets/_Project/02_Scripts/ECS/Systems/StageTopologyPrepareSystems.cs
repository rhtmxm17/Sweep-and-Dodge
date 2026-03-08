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
    }

    internal static class StageTopologyRuntimeGateUtility
    {
        public static bool ShouldRunGameplay(
            in StageTopologyStateComponent topologyState,
            in RunDirectorStageStateComponent stageState)
        {
            if (topologyState.SelectedStageId <= 0
                && topologyState.AppliedStageId <= 0
                && topologyState.Ready == 0)
            {
                return true;
            }

            return topologyState.Ready != 0
                && topologyState.SelectedStageId > 0
                && topologyState.AppliedStageId == topologyState.SelectedStageId
                && stageState.State == RunDirectorStageStateId.Running;
        }
    }
}
