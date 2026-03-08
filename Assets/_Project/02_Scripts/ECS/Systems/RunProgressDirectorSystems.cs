using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateBefore(typeof(RunDirectorStageTransitionSystem))]
    public partial struct RunDirectorStageGateUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunDirectorStageConfigComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<RunDirectorStageGateComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float dt))
                return;
            var config = SystemAPI.GetSingleton<RunDirectorStageConfigComponent>();
            var stageRW = SystemAPI.GetSingletonRW<RunDirectorStageStateComponent>();
            var gateRW = SystemAPI.GetSingletonRW<RunDirectorStageGateComponent>();

            var stageState = stageRW.ValueRO;
            var stageGate = gateRW.ValueRO;
            stageState.StateElapsedSec = math.max(0f, stageState.StateElapsedSec + dt);

            switch (stageState.State)
            {
                case RunDirectorStageStateId.Idle:
                    stageGate.MinIdleDurationElapsed = stageState.StateElapsedSec >= math.max(0f, config.MinIdleDurationSec) ? (byte)1 : (byte)0;
                    stageGate.AutoAdvanceTimeoutElapsed = 0;
                    break;
                case RunDirectorStageStateId.ClearReady:
                    stageGate.MinIdleDurationElapsed = 0;
                    stageGate.AutoAdvanceTimeoutElapsed = stageState.StateElapsedSec >= math.max(0f, config.ClearAutoAdvanceTimeoutSec) ? (byte)1 : (byte)0;
                    break;
                default:
                    stageGate.MinIdleDurationElapsed = 0;
                    stageGate.AutoAdvanceTimeoutElapsed = 0;
                    break;
            }

            stageRW.ValueRW = stageState;
            gateRW.ValueRW = stageGate;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(RunDirectorStageGateUpdateSystem))]
    [UpdateBefore(typeof(RunProgressDirectorSystem))]
    public partial struct RunDirectorStageTransitionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<StageTopologyStateComponent>();
            state.RequireForUpdate<RunDirectorStageGateComponent>();
            state.RequireForUpdate<RunDirectorStageRequestComponent>();
            state.RequireForUpdate<RunDirectorStageSignalComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            uint frame = FrameSequenceUtility.GetCurrentFrame(SystemAPI.GetSingleton<BulletFrameCounterComponent>());
            var stageRW = SystemAPI.GetSingletonRW<RunDirectorStageStateComponent>();
            var topologyState = SystemAPI.GetSingleton<StageTopologyStateComponent>();
            var gateRW = SystemAPI.GetSingletonRW<RunDirectorStageGateComponent>();
            var requestRW = SystemAPI.GetSingletonRW<RunDirectorStageRequestComponent>();
            var signalRW = SystemAPI.GetSingletonRW<RunDirectorStageSignalComponent>();

            var stageState = stageRW.ValueRO;
            var stageGate = gateRW.ValueRO;
            var stageRequest = requestRW.ValueRO;
            var stageSignal = signalRW.ValueRO;

            switch (stageState.State)
            {
                case RunDirectorStageStateId.Idle:
                {
                    bool canRun = stageRequest.StageStartRequested != 0
                        && topologyState.Ready != 0
                        && topologyState.SelectedStageId > 0
                        && topologyState.AppliedStageId == topologyState.SelectedStageId
                        && stageGate.MinIdleDurationElapsed != 0
                        && stageGate.IntroPresentationDone != 0;
                    if (canRun)
                    {
                        TransitionTo(
                            ref stageState,
                            RunDirectorStageStateId.Running,
                            RunDirectorStageTransitionReasonId.StartRequested,
                            frame);
                        stageRequest.StageStartRequested = 0;
                    }
                    break;
                }
                case RunDirectorStageStateId.Running:
                {
                    bool anySource = false;
                    bool allFinish = true;
                    foreach (var sourceDirector in SystemAPI.Query<RefRO<SourceRunDirectorStateComponent>>())
                    {
                        anySource = true;
                        if (sourceDirector.ValueRO.State == RunDirectorSourceStateId.Finish)
                            continue;

                        allFinish = false;
                        break;
                    }

                    if (anySource && allFinish)
                    {
                        TransitionTo(
                            ref stageState,
                            RunDirectorStageStateId.ClearReady,
                            RunDirectorStageTransitionReasonId.AllSourcesDepleted,
                            frame);
                        stageRequest.ConfirmPressed = 0;
                    }
                    break;
                }
                case RunDirectorStageStateId.ClearReady:
                {
                    bool confirm = stageRequest.ConfirmPressed != 0;
                    bool timeout = stageGate.AutoAdvanceTimeoutElapsed != 0;
                    bool canComplete = (confirm || timeout) && stageGate.ClearPresentationDone != 0;
                    if (canComplete)
                    {
                        TransitionTo(
                            ref stageState,
                            RunDirectorStageStateId.Completed,
                            confirm
                                ? RunDirectorStageTransitionReasonId.ConfirmPressed
                                : RunDirectorStageTransitionReasonId.AutoAdvanceTimeout,
                            frame);
                        stageRequest.ConfirmPressed = 0;
                        stageSignal.StageRunCompleted = 1;
                    }
                    break;
                }
            }

            stageRW.ValueRW = stageState;
            gateRW.ValueRW = stageGate;
            requestRW.ValueRW = stageRequest;
            signalRW.ValueRW = stageSignal;
        }

        private static void TransitionTo(
            ref RunDirectorStageStateComponent stageState,
            RunDirectorStageStateId nextState,
            RunDirectorStageTransitionReasonId reason,
            uint frame)
        {
            stageState.State = nextState;
            stageState.StateElapsedSec = 0f;
            stageState.EnteredFrame = frame;
            stageState.LastTransitionReason = reason;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateBefore(typeof(SourceClipRequestBuildSystem))]
    public partial struct RunProgressDirectorSystem : ISystem
    {
        private EntityQuery _directorConfigQuery;
        private EntityQuery _pressureWeightQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceStableIdComponent>();
            state.RequireForUpdate<SourceAnchorComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceRunDirectorStateComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            _directorConfigQuery = SystemAPI.QueryBuilder()
                .WithAll<RunProgressDirectorConfigComponent>()
                .Build();
            _pressureWeightQuery = SystemAPI.QueryBuilder()
                .WithAll<RunDirectorPressureWeightSingletonTag>()
                .WithAll<RunDirectorPressureWeightBuffer>()
                .Build();
            state.RequireForUpdate(_directorConfigQuery);
            state.RequireForUpdate(_pressureWeightQuery);
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            var stageState = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            if (hasTopologyState
                && !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState))
                return;

            if (stageState.State != RunDirectorStageStateId.Running)
                return;

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            bool hasPlayerSync = SystemAPI.HasComponent<PlayerGoSyncComponent>(playerEntity);
            bool hasPlayerTransform = SystemAPI.HasComponent<LocalTransform>(playerEntity);
            if (!hasPlayerSync && !hasPlayerTransform)
                return;

            var configEntity = ResolveFirstEntity(ref _directorConfigQuery);
            if (configEntity == Entity.Null)
                return;
            var config = SystemAPI.GetComponent<RunProgressDirectorConfigComponent>(configEntity);

            var weightEntity = ResolveFirstEntity(ref _pressureWeightQuery);
            if (weightEntity == Entity.Null)
                return;
            var weightBuffer = SystemAPI.GetBuffer<RunDirectorPressureWeightBuffer>(weightEntity);
            float holdSec = math.max(0f, config.PressureHoldSec);
            float baselineScale = math.max(0f, config.BaselineTrashDensityScale);
            float pressureScale = math.max(0f, config.PressureDensityScale);
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime))
                return;
            var scoreWeights = PressureScoreWeights.CreateDefault();
            ApplyWeightOverrides(ref scoreWeights, in weightBuffer);
            float3 playerPosition = hasPlayerSync
                ? SystemAPI.GetComponent<PlayerGoSyncComponent>(playerEntity).Position
                : SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(true);
            var stableIdLookup = SystemAPI.GetComponentLookup<SourceStableIdComponent>(true);
            var anchorLookup = SystemAPI.GetComponentLookup<SourceAnchorComponent>(true);
            var areaLookup = SystemAPI.GetComponentLookup<BulletFieldAreaComponent>(true);
            var directorLookup = SystemAPI.GetComponentLookup<SourceRunDirectorStateComponent>(false);
            var pressureInputLookup = SystemAPI.GetBufferLookup<SourceDirectorPressureInputBuffer>(false);

            sourceLookup.Update(ref state);
            stableIdLookup.Update(ref state);
            anchorLookup.Update(ref state);
            areaLookup.Update(ref state);
            directorLookup.Update(ref state);
            pressureInputLookup.Update(ref state);

            var sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<SourceSpawnComponent>()
                .WithAll<SourceStableIdComponent>()
                .WithAll<SourceAnchorComponent>()
                .WithAll<BulletFieldAreaComponent>()
                .WithAll<SourceRunDirectorStateComponent>()
                .Build();

            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);
            Entity pressureEntity = Entity.Null;
            bool bestPressureOccupied = false;
            float bestPressureScore = float.MinValue;
            uint bestStableId = uint.MaxValue;

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                var source = sourceLookup[sourceEntity];
                var director = directorLookup[sourceEntity];
                if (source.State == SourceStateId.Depleted)
                {
                    director.PressureOccupancySec = 0f;
                    directorLookup[sourceEntity] = director;
                    if (pressureInputLookup.TryGetBuffer(sourceEntity, out var depletedInputs))
                    {
                        SetOrAddPressureInput(ref depletedInputs, RunDirectorPressureInputSlotId.InfluenceOccupancy, 0f);
                        SetOrAddPressureInput(ref depletedInputs, RunDirectorPressureInputSlotId.InfluenceHoldSec, 0f);
                    }

                    continue;
                }

                bool isOccupied = IsPlayerInsideSourceArea(
                    playerPosition,
                    anchorLookup[sourceEntity].Position,
                    areaLookup[sourceEntity]);
                director.PressureOccupancySec = isOccupied
                    ? holdSec
                    : math.max(0f, director.PressureOccupancySec - deltaTime);
                directorLookup[sourceEntity] = director;

                float occupancyInput = isOccupied ? 1f : 0f;
                float holdInput = director.PressureOccupancySec;
                float score = EvaluatePressureScore(
                    occupancyInput,
                    holdInput,
                    in scoreWeights);
                if (pressureInputLookup.TryGetBuffer(sourceEntity, out var pressureInputs))
                {
                    SetOrAddPressureInput(ref pressureInputs, RunDirectorPressureInputSlotId.InfluenceOccupancy, occupancyInput);
                    SetOrAddPressureInput(ref pressureInputs, RunDirectorPressureInputSlotId.InfluenceHoldSec, holdInput);
                    score = EvaluatePressureScoreFromBuffer(ref pressureInputs, in scoreWeights);
                }

                bool isPressureCandidate = isOccupied || director.PressureOccupancySec > 0f;
                if (!isPressureCandidate)
                    continue;

                uint stableId = math.max(1u, stableIdLookup[sourceEntity].Value);
                bool pick = false;
                if (pressureEntity == Entity.Null)
                {
                    pick = true;
                }
                else if (isOccupied != bestPressureOccupied)
                {
                    pick = isOccupied;
                }
                else
                {
                    bool isBetter = score > bestPressureScore;
                    bool tie = math.abs(score - bestPressureScore) <= 1e-5f;
                    pick = isBetter || (tie && stableId < bestStableId);
                }

                if (pick)
                {
                    bestPressureOccupied = isOccupied;
                    bestPressureScore = score;
                    bestStableId = stableId;
                    pressureEntity = sourceEntity;
                }
            }

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                var source = sourceLookup[sourceEntity];
                var director = directorLookup[sourceEntity];

                RunDirectorSourceStateId nextState;
                if (source.State == SourceStateId.Depleted)
                    nextState = RunDirectorSourceStateId.Finish;
                else if (sourceEntity == pressureEntity)
                    nextState = RunDirectorSourceStateId.Pressure;
                else
                    nextState = RunDirectorSourceStateId.Baseline;

                SourceStateId nextClipState = nextState == RunDirectorSourceStateId.Finish
                    ? SourceStateId.Depleted
                    : source.State;
                float nextDensityScale = nextState == RunDirectorSourceStateId.Baseline
                    ? baselineScale
                    : pressureScale;

                if (director.State != nextState
                    || director.SelectedClipState != nextClipState
                    || math.abs(director.DensityScale - nextDensityScale) > 1e-5f)
                {
                    director.Version += 1u;
                }

                director.State = nextState;
                director.SelectedClipState = nextClipState;
                director.DensityScale = nextDensityScale;
                directorLookup[sourceEntity] = director;
            }
        }

        private static bool IsPlayerInsideSourceArea(
            float3 playerPosition,
            float3 sourcePosition,
            in BulletFieldAreaComponent area)
        {
            float dx = playerPosition.x - sourcePosition.x;
            float dz = playerPosition.z - sourcePosition.z;
            if (area.Shape == BulletFieldShapeId.Rectangle)
            {
                float hx = math.max(0f, area.Size.x * 0.5f);
                float hz = math.max(0f, area.Size.y * 0.5f);
                return math.abs(dx) <= hx && math.abs(dz) <= hz;
            }

            float radius = math.max(0f, area.Radius);
            return dx * dx + dz * dz <= radius * radius;
        }

        private static Entity ResolveFirstEntity(ref EntityQuery query)
        {
            int count = query.CalculateEntityCount();
            if (count <= 0)
                return Entity.Null;
            if (count == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        private static void SetOrAddPressureInput(
            ref DynamicBuffer<SourceDirectorPressureInputBuffer> inputs,
            RunDirectorPressureInputSlotId slot,
            float value)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].Slot != slot)
                    continue;

                inputs[i] = new SourceDirectorPressureInputBuffer
                {
                    Slot = slot,
                    Value = value
                };
                return;
            }

            inputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = slot,
                Value = value
            });
        }

        private static float EvaluatePressureScoreFromBuffer(
            ref DynamicBuffer<SourceDirectorPressureInputBuffer> inputs,
            in PressureScoreWeights weights)
        {
            float score = 0f;
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                score += input.Value * ResolveSlotWeight(input.Slot, in weights);
            }

            return score;
        }

        private static float EvaluatePressureScore(
            float occupancy,
            float holdSec,
            in PressureScoreWeights weights)
        {
            return occupancy * weights.Occupancy
                + holdSec * weights.HoldSec;
        }

        private static float ResolveSlotWeight(
            RunDirectorPressureInputSlotId slot,
            in PressureScoreWeights weights)
        {
            return slot switch
            {
                RunDirectorPressureInputSlotId.InfluenceOccupancy => weights.Occupancy,
                RunDirectorPressureInputSlotId.InfluenceHoldSec => weights.HoldSec,
                _ => 0f,
            };
        }

        private static void ApplyWeightOverrides(
            ref PressureScoreWeights weights,
            in DynamicBuffer<RunDirectorPressureWeightBuffer> weightBuffer)
        {
            for (int i = 0; i < weightBuffer.Length; i++)
            {
                var item = weightBuffer[i];
                float safeWeight = item.Weight;
                switch (item.Slot)
                {
                    case RunDirectorPressureInputSlotId.InfluenceOccupancy:
                        weights.Occupancy = safeWeight;
                        break;
                    case RunDirectorPressureInputSlotId.InfluenceHoldSec:
                        weights.HoldSec = safeWeight;
                        break;
                }
            }
        }

        private struct PressureScoreWeights
        {
            public float Occupancy;
            public float HoldSec;

            public static PressureScoreWeights CreateDefault()
            {
                return new PressureScoreWeights
                {
                    Occupancy = 1.0f,
                    HoldSec = 1.0f,
                };
            }
        }
    }
}


