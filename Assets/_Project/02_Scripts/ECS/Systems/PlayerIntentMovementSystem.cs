using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    [UpdateInGroup(typeof(PlayerFixedStepGroup))]
    [UpdateAfter(typeof(ReplayTickInputApplySystem))]
    [UpdateBefore(typeof(PlayerIntentConsumeSystem))]
    public partial struct PlayerIntentMovementSystem : ISystem
    {
        private const float DefaultMoveSpeed = 6f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerInputIntentComponent>();
            state.RequireForUpdate<PlayerGoSyncComponent>();
            state.RequireForUpdate<VacuumRuntimeStateComponent>();
            state.RequireForUpdate<PlayerCleanupResolvedProfileComponent>();
            state.RequireForUpdate<PlayerCleanupSweepRuntimeStateComponent>();
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

            foreach (var (intent, tx, sync, vacuum, resolvedProfile, sweepRuntime) in
                     SystemAPI.Query<
                         RefRO<PlayerInputIntentComponent>,
                         RefRW<LocalTransform>,
                         RefRW<PlayerGoSyncComponent>,
                         RefRO<VacuumRuntimeStateComponent>,
                         RefRO<PlayerCleanupResolvedProfileComponent>,
                         RefRO<PlayerCleanupSweepRuntimeStateComponent>>()
                         .WithAll<PlayerTag>())
            {
                var txValue = tx.ValueRO;
                var normalizedActionId = PlayerCleanupActionContractUtility.NormalizeRuntimeActionId(
                    resolvedProfile.ValueRO.ActionKind,
                    allowNone: true);
                bool isBroomSweepConstraintActive = normalizedActionId == PlayerCleanupActionId.BroomSweep
                    && vacuum.ValueRO.IsActive != 0;

                float2 moveAxis = intent.ValueRO.MoveAxis;
                if (math.lengthsq(moveAxis) > 1f)
                    moveAxis = math.normalizesafe(moveAxis, new float2(0f, 1f));

                if (math.lengthsq(moveAxis) > 1e-8f)
                {
                    float moveSpeed = DefaultMoveSpeed;
                    if (isBroomSweepConstraintActive)
                        moveSpeed *= math.max(0f, resolvedProfile.ValueRO.ActiveMoveSpeedScale);

                    txValue.Position += new float3(moveAxis.x, 0f, moveAxis.y) * (moveSpeed * dt);
                }

                bool shouldLockFacing = isBroomSweepConstraintActive
                    && resolvedProfile.ValueRO.LockFacingWhileActive != 0;
                if (shouldLockFacing)
                {
                    float2 lockedFacingXZ = sweepRuntime.ValueRO.LockedFacingXZ;
                    if (sweepRuntime.ValueRO.HasLockedFacing != 0
                        && math.lengthsq(lockedFacingXZ) > 1e-8f)
                    {
                        float3 lockedForward = math.normalize(new float3(lockedFacingXZ.x, 0f, lockedFacingXZ.y));
                        txValue.Rotation = quaternion.LookRotationSafe(lockedForward, new float3(0f, 1f, 0f));
                    }
                }
                else if (intent.ValueRO.HasAimWorldPoint != 0)
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
