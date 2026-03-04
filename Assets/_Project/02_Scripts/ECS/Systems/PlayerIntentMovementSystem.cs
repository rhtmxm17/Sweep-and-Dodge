using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ReplayInputSyncSystem))]
    [UpdateBefore(typeof(PlayerGoSyncSystem))]
    public partial struct PlayerIntentMovementSystem : ISystem
    {
        private const float DefaultMoveSpeed = 6f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerInputIntentComponent>();
            state.RequireForUpdate<PlayerGoSyncComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<ReplayInputControlComponent>(out var replayControl) &&
                replayControl.Mode == ReplayInputModeId.Playback)
            {
                return;
            }

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float dt))
                return;

            foreach (var (intent, tx, sync) in
                     SystemAPI.Query<
                         RefRO<PlayerInputIntentComponent>,
                         RefRW<LocalTransform>,
                         RefRW<PlayerGoSyncComponent>>()
                         .WithAll<PlayerTag>())
            {
                var txValue = tx.ValueRO;
                float2 moveAxis = intent.ValueRO.MoveAxis;
                if (math.lengthsq(moveAxis) > 1f)
                    moveAxis = math.normalizesafe(moveAxis, new float2(0f, 1f));

                if (math.lengthsq(moveAxis) > 1e-8f)
                {
                    txValue.Position += new float3(moveAxis.x, 0f, moveAxis.y) * (DefaultMoveSpeed * dt);
                }

                if (intent.ValueRO.HasAimWorldPoint != 0)
                {
                    float3 aimWorld = new float3(intent.ValueRO.AimWorldXZ.x, txValue.Position.y, intent.ValueRO.AimWorldXZ.y);
                    float3 aimDir = aimWorld - txValue.Position;
                    aimDir.y = 0f;
                    if (math.lengthsq(aimDir) > 1e-8f)
                    {
                        txValue.Rotation = quaternion.LookRotationSafe(math.normalize(aimDir), new float3(0f, 1f, 0f));
                    }
                }

                tx.ValueRW = txValue;

                var syncValue = sync.ValueRW;
                syncValue.Position = txValue.Position;
                if (syncValue.SyncRotation != 0)
                    syncValue.Rotation = txValue.Rotation;
                sync.ValueRW = syncValue;
            }
        }
    }
}
