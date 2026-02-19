using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PlayerGoSyncSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (sync, tx, vacuum, actionState, slotMap) in
                     SystemAPI.Query<
                         RefRW<PlayerGoSyncComponent>,
                         RefRW<LocalTransform>,
                         RefRW<VacuumBurstComponent>,
                         RefRW<PlayerCleanupActionStateComponent>,
                         RefRO<PlayerCleanupActionSlotMapComponent>>()
                              .WithAll<PlayerTag>())
            {
                tx.ValueRW.Position = sync.ValueRO.Position;
                if (sync.ValueRO.SyncRotation != 0)
                    tx.ValueRW.Rotation = sync.ValueRO.Rotation;

                if (sync.ValueRO.VacuumRequested != 0)
                    vacuum.ValueRW.ActivateRequested = 1;
                if (sync.ValueRO.CleanupActionRequested != 0)
                    actionState.ValueRW.PendingActionId = ResolveActionId(
                        (PlayerCleanupActionSlotId)sync.ValueRO.RequestedCleanupActionSlot,
                        in slotMap.ValueRO);

                // 1회성 입력 소비
                var s = sync.ValueRW;
                s.VacuumRequested = 0;
                s.CleanupActionRequested = 0;
                s.RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None;
                sync.ValueRW = s;
            }
        }

        private static PlayerCleanupActionId ResolveActionId(
            PlayerCleanupActionSlotId slotId,
            in PlayerCleanupActionSlotMapComponent slotMap)
        {
            return slotId switch
            {
                PlayerCleanupActionSlotId.Primary => slotMap.PrimaryActionId,
                PlayerCleanupActionSlotId.Secondary => slotMap.SecondaryActionId,
                _ => PlayerCleanupActionId.None,
            };
        }
    }

}
