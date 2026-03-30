using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 청소 흔적(오염도) 단일 writer.
    /// - Request 단계에서만 오염도 값을 갱신한다.
    /// - 수거 시스템이 남긴 Drop 요청을 소비하고, active/inactive 전환과 recovery wave를 처리한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(BulletVacuumRequestSystem))]
    [UpdateBefore(typeof(PlayerHazardCollisionRequestSystem))]
    public partial struct SourcePollutionUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SourcePollutionConfigComponent>();
            state.RequireForUpdate<SourcePollutionGridComponent>();
            state.RequireForUpdate<SourcePollutionCellBuffer>();
            state.RequireForUpdate<SourcePollutionDropRequestBuffer>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            bool hasStageState = SystemAPI.TryGetSingleton<RunDirectorStageStateComponent>(out var stageState);
            if (hasTopologyState
                && (!hasStageState || !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState)))
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime))
                return;

            state.CompleteDependency();
            uint currentFrame = FrameSequenceUtility.GetCurrentFrame(SystemAPI.GetSingleton<BulletFrameCounterComponent>());

            foreach (var (configRO, gridRO, pollutionCells, dropRequests, entity) in SystemAPI
                .Query<RefRO<SourcePollutionConfigComponent>, RefRO<SourcePollutionGridComponent>, DynamicBuffer<SourcePollutionCellBuffer>, DynamicBuffer<SourcePollutionDropRequestBuffer>>()
                .WithEntityAccess())
            {
                UpdateSource(
                    entity,
                    configRO.ValueRO,
                    gridRO.ValueRO,
                    pollutionCells,
                    dropRequests,
                    deltaTime,
                    currentFrame);
            }
        }

        private static void UpdateSource(
            Entity sourceEntity,
            in SourcePollutionConfigComponent config,
            in SourcePollutionGridComponent grid,
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            DynamicBuffer<SourcePollutionDropRequestBuffer> dropRequests,
            float deltaTime,
            uint currentFrame)
        {
            float minValue = math.max(0f, config.MinValue);
            float maxValue = math.max(minValue, config.MaxValue);
            float regen = math.max(0f, config.RegenPerSec) * math.max(0f, deltaTime);
            float dropPerCollect = math.max(0f, config.DropPerCollect);

            if (regen > 0f)
            {
                for (int i = 0; i < pollutionCells.Length; i++)
                {
                    var cell = pollutionCells[i];
                    if (cell.IsValid == 0 || cell.IsActive == 0)
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
                cell.LastDropFrame = currentFrame;
                cell.CooldownUntilFrame = currentFrame + config.RecoveryCooldownFrames;
                if (cell.Value <= minValue)
                    cell.IsActive = 0;
                pollutionCells[cellIndex] = cell;
            }

            if (dropRequests.Length > 0)
                dropRequests.Clear();

            CountCells(pollutionCells, out int validCount, out int activeCount);
            if (validCount <= 0)
                return;

            bool forceWave = activeCount == 0;
            float safeThreshold = math.clamp(config.ActiveRatioThreshold, 0f, 1f);
            float activeRatio = (float)activeCount / validCount;
            if (!forceWave && activeRatio >= safeThreshold)
                return;

            int seedCount = math.max(1, config.RecoveryWaveSeedCount);
            int clusterSize = math.max(1, config.RecoveryWaveClusterSize);
            float restoreValue = math.clamp(config.RecoveryWaveRestoreValue, minValue, maxValue);
            uint randomSeed = (uint)math.max(1, (int)math.hash(new uint4(currentFrame, (uint)sourceEntity.Index + 1u, (uint)validCount, (uint)activeCount + 1u)));
            var random = new Unity.Mathematics.Random(randomSeed);

            int recovered = 0;
            for (int i = 0; i < seedCount; i++)
            {
                bool allowCooldownBypass = forceWave && recovered == 0;
                if (!TrySelectRecoverySeed(
                        pollutionCells,
                        config.RecoveryRecentCleanBiasFrames,
                        currentFrame,
                        allowCooldownBypass,
                        ref random,
                        out int seedCellIndex))
                {
                    break;
                }

                recovered += RecoverClusterFromSeed(
                    seedCellIndex,
                    clusterSize,
                    restoreValue,
                    math.max(1, grid.Cols),
                    math.max(1, grid.Rows),
                    pollutionCells);
            }
        }

        private static void CountCells(
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            out int validCount,
            out int activeCount)
        {
            validCount = 0;
            activeCount = 0;
            for (int i = 0; i < pollutionCells.Length; i++)
            {
                var cell = pollutionCells[i];
                if (cell.IsValid == 0)
                    continue;

                validCount++;
                if (cell.IsActive != 0)
                    activeCount++;
            }
        }

        private static bool TrySelectRecoverySeed(
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            uint recentCleanBiasFrames,
            uint currentFrame,
            bool allowCooldownBypass,
            ref Unity.Mathematics.Random random,
            out int seedCellIndex)
        {
            seedCellIndex = -1;
            float bestScore = float.NegativeInfinity;
            uint safeBiasFrames = math.max(1u, recentCleanBiasFrames);

            for (int i = 0; i < pollutionCells.Length; i++)
            {
                var cell = pollutionCells[i];
                if (cell.IsValid == 0 || cell.IsActive != 0)
                    continue;
                if (!allowCooldownBypass && currentFrame < cell.CooldownUntilFrame)
                    continue;

                uint ageFrames = currentFrame >= cell.LastDropFrame
                    ? currentFrame - cell.LastDropFrame
                    : 0u;
                float ageScore = recentCleanBiasFrames == 0u
                    ? 1f
                    : math.saturate((float)ageFrames / safeBiasFrames);
                float score = ageScore + random.NextFloat(0f, 0.25f);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                seedCellIndex = i;
            }

            return seedCellIndex >= 0;
        }

        private static int RecoverClusterFromSeed(
            int seedCellIndex,
            int clusterSize,
            float restoreValue,
            int cols,
            int rows,
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells)
        {
            if ((uint)seedCellIndex >= (uint)pollutionCells.Length)
                return 0;

            var visited = new NativeArray<byte>(pollutionCells.Length, Allocator.Temp);
            var frontier = new NativeList<int>(Allocator.Temp);
            try
            {
                frontier.Add(seedCellIndex);
                visited[seedCellIndex] = 1;

                int recovered = 0;
                int readIndex = 0;
                while (readIndex < frontier.Length && recovered < clusterSize)
                {
                    int current = frontier[readIndex++];
                    if ((uint)current >= (uint)pollutionCells.Length)
                        continue;

                    var cell = pollutionCells[current];
                    if (cell.IsValid != 0 && cell.IsActive == 0)
                    {
                        cell.IsActive = 1;
                        cell.Value = math.max(cell.Value, restoreValue);
                        cell.CooldownUntilFrame = 0u;
                        pollutionCells[current] = cell;
                        recovered++;
                        if (recovered >= clusterSize)
                            break;
                    }

                    EnqueueNeighbors(current, cols, rows, pollutionCells, visited, frontier);
                }

                return recovered;
            }
            finally
            {
                if (frontier.IsCreated)
                    frontier.Dispose();
                if (visited.IsCreated)
                    visited.Dispose();
            }
        }

        private static void EnqueueNeighbors(
            int cellIndex,
            int cols,
            int rows,
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            NativeArray<byte> visited,
            NativeList<int> frontier)
        {
            int x = cellIndex % cols;
            int y = cellIndex / cols;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if ((uint)nx >= (uint)cols || (uint)ny >= (uint)rows)
                        continue;

                    int neighbor = ny * cols + nx;
                    if ((uint)neighbor >= (uint)pollutionCells.Length || visited[neighbor] != 0)
                        continue;
                    if (pollutionCells[neighbor].IsValid == 0)
                        continue;

                    frontier.Add(neighbor);
                    visited[neighbor] = 1;
                }
            }
        }
    }
}
