using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 행동 분기 선택 상태를 확정한다.
    /// - 외부 입력/선택 경로는 PendingActionId만 기록한다.
    /// - 실제 적용은 Request 그룹 시작 지점에서 단일 책임으로 수행한다.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateBefore(typeof(BulletVacuumRequestSystem))]
    public partial struct PlayerCleanupActionSelectSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerCleanupActionStateComponent>();
            state.RequireForUpdate<VacuumRuntimeStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (actionState, vacuum) in
                     SystemAPI.Query<RefRW<PlayerCleanupActionStateComponent>, RefRO<VacuumRuntimeStateComponent>>().WithAll<PlayerTag>())
            {
                var pending = Normalize(actionState.ValueRO.PendingActionId);
                var selected = Normalize(actionState.ValueRO.SelectedActionId);

                if (actionState.ValueRO.SelectedActionId != selected)
                    actionState.ValueRW.SelectedActionId = selected;

                if (actionState.ValueRO.PendingActionId != pending)
                    actionState.ValueRW.PendingActionId = pending;

                if (pending == PlayerCleanupActionId.None)
                    continue;

                if (vacuum.ValueRO.IsActive != 0)
                {
                    // 기존 동작 진행 중 들어온 전환 입력은 무시(소비)한다.
                    actionState.ValueRW.PendingActionId = PlayerCleanupActionId.None;
                    continue;
                }

                if (pending == selected)
                {
                    actionState.ValueRW.PendingActionId = PlayerCleanupActionId.None;
                    continue;
                }

                actionState.ValueRW.SelectedActionId = pending;
                actionState.ValueRW.PendingActionId = PlayerCleanupActionId.None;
                actionState.ValueRW.Version++;
                uint version = actionState.ValueRO.Version;
                Debug.Log($"[CleanupActionSelect] selected={pending}, version={version}");
            }
        }

        private static PlayerCleanupActionId Normalize(PlayerCleanupActionId actionId)
        {
            return PlayerCleanupActionContractUtility.NormalizeRuntimeActionId(actionId, allowNone: true);
        }
    }
}
