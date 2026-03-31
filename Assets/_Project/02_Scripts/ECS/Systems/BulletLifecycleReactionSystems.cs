using Unity.Burst;
using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// terminal lifecycle reaction execute owner.
    /// - ExecutionEnd에서 pending lifecycle request를 먼저 읽는다.
    /// - 이번 slice에서는 reason dispatch만 수행하고 실제 반응은 하지 않는다.
    /// - request consume / render toggle / pool enqueue는 계속 BulletDespawnExecutionSystem 단일 책임이다.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateAfter(typeof(PlayerHazardRiskResolveSystem))]
    [UpdateBefore(typeof(BulletDespawnExecutionSystem))]
    [UpdateBefore(typeof(CombatEventChannelConsumeSystem))]
    public partial struct BulletLifecycleReactionExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new BulletLifecycleReactionDispatchJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct BulletLifecycleReactionDispatchJob : IJobEntity
        {
            private void Execute(
                EnabledRefRO<BulletDespawnRequestTag> despawnRequest,
                in BulletLifecycleRequestComponent lifecycleRequest,
                in BulletLifecycleContactComponent lifecycleContact)
            {
                if (!despawnRequest.ValueRO)
                    return;

                DispatchLifecycleReaction(in lifecycleRequest, in lifecycleContact);
            }
        }

        [BurstCompile]
        private static void DispatchLifecycleReaction(
            in BulletLifecycleRequestComponent lifecycleRequest,
            in BulletLifecycleContactComponent lifecycleContact)
        {
            switch (lifecycleRequest.Reason)
            {
                case BulletLifecycleReasonId.LifetimeExpired:
                case BulletLifecycleReasonId.StageBlocked:
                case BulletLifecycleReasonId.VacuumCollected:
                case BulletLifecycleReasonId.CarryFullRemoved:
                case BulletLifecycleReasonId.PlayerHit:
                case BulletLifecycleReasonId.MotionCompleted:
                case BulletLifecycleReasonId.None:
                default:
                    break;
            }
        }
    }
}
