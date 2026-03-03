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
            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            txLookup.Update(ref state);

            foreach (var (sync, intent, vacuum, actionState, slotMap, entity) in
                     SystemAPI.Query<
                         RefRW<PlayerGoSyncComponent>,
                         RefRW<PlayerInputIntentComponent>,
                         RefRW<VacuumRuntimeStateComponent>,
                         RefRW<PlayerCleanupActionStateComponent>,
                         RefRO<PlayerCleanupActionSlotMapComponent>>()
                              .WithAll<PlayerTag>()
                              .WithEntityAccess())
            {
                if (txLookup.HasComponent(entity))
                {
                    var tx = txLookup[entity];
                    var mirrored = sync.ValueRW;
                    mirrored.Position = tx.Position;
                    if (mirrored.SyncRotation != 0)
                        mirrored.Rotation = tx.Rotation;
                    sync.ValueRW = mirrored;
                }

                bool hasVacuumRequest = intent.ValueRO.VacuumRequested != 0 || sync.ValueRO.VacuumRequested != 0;
                bool hasCleanupRequest = intent.ValueRO.CleanupActionRequested != 0 || sync.ValueRO.CleanupActionRequested != 0;
                if (hasVacuumRequest)
                    vacuum.ValueRW.ActivateRequested = 1;
                if (hasCleanupRequest)
                {
                    byte requestedSlot = intent.ValueRO.CleanupActionRequested != 0
                        ? intent.ValueRO.RequestedCleanupActionSlot
                        : sync.ValueRO.RequestedCleanupActionSlot;
                    actionState.ValueRW.PendingActionId = ResolveActionId(
                        (PlayerCleanupActionSlotId)requestedSlot,
                        in slotMap.ValueRO);
                }

                // 1회성 입력 소비
                var s = sync.ValueRW;
                s.VacuumRequested = 0;
                s.CleanupActionRequested = 0;
                s.RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None;
                sync.ValueRW = s;

                var i = intent.ValueRW;
                i.VacuumRequested = 0;
                i.CleanupActionRequested = 0;
                i.RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None;
                intent.ValueRW = i;
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
