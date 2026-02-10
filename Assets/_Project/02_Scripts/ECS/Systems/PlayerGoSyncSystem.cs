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
            foreach (var (sync, tx, vacuum) in
                     SystemAPI.Query<RefRW<PlayerGoSyncComponent>, RefRW<LocalTransform>, RefRW<VacuumBurstComponent>>()
                              .WithAll<PlayerTag>())
            {
                tx.ValueRW.Position = sync.ValueRO.Position;
                if (sync.ValueRO.SyncRotation != 0)
                    tx.ValueRW.Rotation = sync.ValueRO.Rotation;

                if (sync.ValueRO.VacuumRequested != 0)
                    vacuum.ValueRW.ActivateRequested = 1;

                // 1회성 입력 소비
                var s = sync.ValueRW;
                s.VacuumRequested = 0;
                sync.ValueRW = s;
            }
        }
    }

}