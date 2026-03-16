using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// HazardStack 상태 단일 owner.
    /// - Request/Execution 단계에서 누적된 risk 요청을 ExecutionEnd 말단에서 확정한다.
    /// - 같은 프레임 수거 결과는 유지하고, reset 요청이 있으면 최종 stack은 0으로 덮는다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositExecutionSystem))]
    [UpdateBefore(typeof(BulletDespawnExecutionSystem))]
    public partial struct PlayerHazardRiskResolveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerHazardRiskConfigComponent>();
            state.RequireForUpdate<PlayerHazardRiskStateComponent>();
            state.RequireForUpdate<PlayerHazardRiskRequestComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            foreach (var (riskState, riskConfig, riskRequest) in
                     SystemAPI.Query<
                         RefRW<PlayerHazardRiskStateComponent>,
                         RefRO<PlayerHazardRiskConfigComponent>,
                         RefRW<PlayerHazardRiskRequestComponent>>().WithAll<PlayerTag>())
            {
                int nextStack;
                if (riskRequest.ValueRO.ResetRequested != 0)
                {
                    nextStack = 0;
                }
                else
                {
                    int currentStack = math.max(0, riskState.ValueRO.HazardStack);
                    int maxStack = math.max(0, riskConfig.ValueRO.HazardStackMax);
                    long pending = math.max(0, riskRequest.ValueRO.PendingHazardCapturedCount);
                    long unclamped = currentStack + pending;
                    nextStack = maxStack <= 0
                        ? 0
                        : math.min(maxStack, unclamped >= int.MaxValue ? int.MaxValue : (int)unclamped);
                }

                riskState.ValueRW.HazardStack = nextStack;
                riskRequest.ValueRW.PendingHazardCapturedCount = 0;
                riskRequest.ValueRW.ResetRequested = 0;
            }
        }
    }
}
