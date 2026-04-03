using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(PlayerFixedStepGroup))]
    [UpdateAfter(typeof(PlayerIntentMovementSystem))]
    [UpdateBefore(typeof(ReplayTickRecordSystem))]
    public partial struct PlayerIntentConsumeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerGoSyncComponent>();
            state.RequireForUpdate<PlayerInputIntentComponent>();
            state.RequireForUpdate<PlayerResolvedInputSnapshotComponent>();
            state.RequireForUpdate<VacuumRuntimeStateComponent>();
            state.RequireForUpdate<PlayerCleanupActionStateComponent>();
            state.RequireForUpdate<PlayerCleanupActionSlotMapComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            foreach (var (sync, intent, resolvedInput, vacuum, actionState, slotMap, entity) in
                     SystemAPI.Query<
                         RefRW<PlayerGoSyncComponent>,
                         RefRW<PlayerInputIntentComponent>,
                         RefRO<PlayerResolvedInputSnapshotComponent>,
                         RefRW<VacuumRuntimeStateComponent>,
                         RefRW<PlayerCleanupActionStateComponent>,
                         RefRO<PlayerCleanupActionSlotMapComponent>>()
                              .WithAll<PlayerTag>()
                              .WithEntityAccess())
            {
                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    var tx = SystemAPI.GetComponent<LocalTransform>(entity);
                    var mirrored = sync.ValueRW;
                    mirrored.Position = tx.Position;
                    if (mirrored.SyncRotation != 0)
                        mirrored.Rotation = tx.Rotation;
                    sync.ValueRW = mirrored;
                }

                bool hasVacuumRequest = resolvedInput.ValueRO.VacuumRequested != 0;
                bool hasCleanupRequest = resolvedInput.ValueRO.CleanupActionRequested != 0;
                if (hasVacuumRequest)
                    vacuum.ValueRW.ActivateRequested = 1;
                if (hasCleanupRequest)
                {
                    actionState.ValueRW.PendingProfileKey = ResolveProfileKey(
                        (PlayerCleanupActionSlotId)resolvedInput.ValueRO.RequestedCleanupActionSlot,
                        in slotMap.ValueRO);
                }

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

        private static FixedString64Bytes ResolveProfileKey(
            PlayerCleanupActionSlotId slotId,
            in PlayerCleanupActionSlotMapComponent slotMap)
        {
            return slotId switch
            {
                PlayerCleanupActionSlotId.Primary => slotMap.PrimaryProfileKey,
                PlayerCleanupActionSlotId.Secondary => slotMap.SecondaryProfileKey,
                _ => default,
            };
        }
    }
}
