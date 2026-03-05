using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 플레이 HUD 표시용 스냅샷 단일 writer.
    /// GO HUD는 본 스냅샷을 read-only로 소비한다.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateAfter(typeof(CombatEventChannelConsumeSystem))]
    [UpdateBefore(typeof(PlayerUiFeedbackConsumeSystem))]
    public partial struct PlayerHudSnapshotCollectSystem : ISystem
    {
        private const float HitFlashDurationSec = 0.6f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerCarryBinComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<CombatEventMetricsComponent>();
            state.RequireForUpdate<PlayerHudSnapshotComponent>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var carry = SystemAPI.GetSingleton<PlayerCarryBinComponent>();
            var stage = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            var combat = SystemAPI.GetSingleton<CombatEventMetricsComponent>();
            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);

            float dt = 0f;
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float resolvedDt))
                dt = resolvedDt;

            int totalSources = 0;
            int depletedSources = 0;
            bool foundPressureSource = false;
            uint selectedStableId = 0u;
            int selectedCollected = 0;
            int selectedThresholdDepleted = 0;

            foreach (var (source, stableId, director) in SystemAPI
                         .Query<RefRO<SourceSpawnComponent>, RefRO<SourceStableIdComponent>, RefRO<SourceRunDirectorStateComponent>>())
            {
                totalSources++;
                if (source.ValueRO.State == SourceStateId.Depleted)
                    depletedSources++;

                if (director.ValueRO.State != RunDirectorSourceStateId.Pressure)
                    continue;

                uint candidateStableId = math.max(1u, stableId.ValueRO.Value);
                if (foundPressureSource && candidateStableId >= selectedStableId)
                    continue;

                foundPressureSource = true;
                selectedStableId = candidateStableId;
                selectedCollected = math.max(0, source.ValueRO.CollectedCount);
                selectedThresholdDepleted = math.max(0, source.ValueRO.ThresholdDepleted);
            }

            var snapshotRW = SystemAPI.GetSingletonRW<PlayerHudSnapshotComponent>();
            var snapshot = snapshotRW.ValueRO;

            snapshot.CarryLoad = math.max(0, carry.Load);
            snapshot.CarryCapacity = math.max(0, carry.Capacity);
            snapshot.TotalSourceCount = math.max(0, totalSources);
            snapshot.DepletedSourceCount = math.clamp(depletedSources, 0, snapshot.TotalSourceCount);

            if (foundPressureSource)
            {
                int denominator = math.max(1, selectedThresholdDepleted);
                snapshot.PressureSourceStableId = selectedStableId;
                snapshot.PressureSourceCollected = selectedCollected;
                snapshot.PressureSourceThresholdDepleted = selectedThresholdDepleted;
                snapshot.PressureSourceProgress01 = math.saturate((float)selectedCollected / denominator);
            }
            else
            {
                snapshot.PressureSourceStableId = 0u;
                snapshot.PressureSourceCollected = 0;
                snapshot.PressureSourceThresholdDepleted = 0;
                snapshot.PressureSourceProgress01 = 0f;
            }

            snapshot.StageState = stage.State;
            snapshot.StageStateElapsedSec = math.max(0f, stage.StateElapsedSec);

            if (combat.LastFrameHitCount > 0)
            {
                snapshot.LastHitLossValue = math.max(0, combat.LastFrameHitValue);
                snapshot.HitFlashRemainingSec = HitFlashDurationSec;
            }
            else
            {
                snapshot.HitFlashRemainingSec = math.max(0f, snapshot.HitFlashRemainingSec - dt);
            }

            snapshot.LastUpdatedFrame = frame;
            snapshotRW.ValueRW = snapshot;
        }
    }
}
