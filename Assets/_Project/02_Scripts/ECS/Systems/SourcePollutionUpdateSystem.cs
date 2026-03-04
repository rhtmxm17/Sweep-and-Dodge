using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 청소 흔적(오염도) 단일 writer.
    /// - Request 단계에서만 오염도 값을 갱신한다.
    /// - 수거 시스템이 남긴 Drop 요청을 소비하고, 프레임 회복(regen)을 적용한다.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(BulletVacuumRequestSystem))]
    [UpdateBefore(typeof(PlayerHazardCollisionRequestSystem))]
    public partial struct SourcePollutionUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SourcePollutionConfigComponent>();
            state.RequireForUpdate<SourcePollutionCellBuffer>();
            state.RequireForUpdate<SourcePollutionDropRequestBuffer>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime))
                return;

            state.Dependency = new SourcePollutionUpdateJob
            {
                DeltaTime = deltaTime,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct SourcePollutionUpdateJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(
                in SourcePollutionConfigComponent config,
                DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
                DynamicBuffer<SourcePollutionDropRequestBuffer> dropRequests)
            {
                float minValue = math.max(0f, config.MinValue);
                float maxValue = math.max(minValue, config.MaxValue);
                float regen = math.max(0f, config.RegenPerSec) * math.max(0f, DeltaTime);
                float dropPerCollect = math.max(0f, config.DropPerCollect);

                if (regen > 0f)
                {
                    for (int i = 0; i < pollutionCells.Length; i++)
                    {
                        var cell = pollutionCells[i];
                        if (cell.IsValid == 0)
                            continue;

                        cell.Value = math.clamp(cell.Value + regen, minValue, maxValue);
                        pollutionCells[i] = cell;
                    }
                }

                for (int i = 0; i < dropRequests.Length; i++)
                {
                    var request = dropRequests[i];
                    int cellIndex = request.CellIndex;
                    if ((uint)cellIndex >= (uint)pollutionCells.Length)
                        continue;

                    int count = math.max(0, request.Count);
                    if (count <= 0)
                        continue;

                    var cell = pollutionCells[cellIndex];
                    if (cell.IsValid == 0)
                        continue;

                    cell.Value = math.clamp(cell.Value - dropPerCollect * count, minValue, maxValue);
                    pollutionCells[cellIndex] = cell;
                }

                if (dropRequests.Length > 0)
                    dropRequests.Clear();
            }
        }
    }
}
