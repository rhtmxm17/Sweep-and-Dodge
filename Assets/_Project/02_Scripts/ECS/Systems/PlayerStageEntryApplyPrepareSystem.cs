using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(StageTopologyPrepareGroup))]
    [UpdateAfter(typeof(StageTopologyApplyPrepareSystem))]
    public partial struct PlayerStageEntryApplyPrepareSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StagePlayerStartRuntimeComponent>();
            state.RequireForUpdate<StageTopologyLifecycleStateComponent>();
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerStart = SystemAPI.GetSingleton<StagePlayerStartRuntimeComponent>();
            var lifecycle = SystemAPI.GetSingleton<StageTopologyLifecycleStateComponent>();
            if (playerStart.Ready == 0 || playerStart.AppliedVersion == 0u)
                return;
            if (playerStart.AppliedVersion > lifecycle.CurrentAppliedVersion)
                return;

            quaternion rotation = quaternion.RotateY(math.radians(playerStart.YawDeg));
            float3 position = new float3(playerStart.PositionX, playerStart.PositionY, playerStart.PositionZ);

            foreach (var (tx, sync, previous, applyState) in SystemAPI.Query<
                         RefRW<LocalTransform>,
                         RefRW<PlayerGoSyncComponent>,
                         RefRW<PlayerPreviousPositionComponent>,
                         RefRW<PlayerStageEntryApplyStateComponent>>()
                     .WithAll<PlayerTag>())
            {
                if (playerStart.AppliedVersion <= applyState.ValueRO.LastAppliedVersion)
                    continue;

                var txValue = tx.ValueRO;
                txValue.Position = position;
                txValue.Rotation = rotation;
                tx.ValueRW = txValue;

                var syncValue = sync.ValueRW;
                syncValue.Position = position;
                if (syncValue.SyncRotation != 0)
                    syncValue.Rotation = rotation;
                sync.ValueRW = syncValue;

                previous.ValueRW = new PlayerPreviousPositionComponent
                {
                    Position = position,
                };

                applyState.ValueRW = new PlayerStageEntryApplyStateComponent
                {
                    LastAppliedVersion = playerStart.AppliedVersion,
                };
            }
        }
    }
}
