using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageTopologyRuntimeGateTests
    {
        [Test]
        public void SourcePollutionUpdate_DoesNotAdvance_WhenTopologyNotReadyAndStageIdle()
        {
            using var world = new World("StageTopologyRuntimeGate_Pollution");
            var em = world.EntityManager;

            SetSingleton(em, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 0,
                Ready = 0,
            });
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
            });
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                HasStep = 1,
                UsingFixedTick = 0,
            });

            var source = em.CreateEntity(typeof(SourcePollutionConfigComponent));
            em.SetComponentData(source, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 10f,
                RegenPerSec = 1f,
                DropPerCollect = 1f,
                TopKSampleCount = 1,
            });
            var cells = em.AddBuffer<SourcePollutionCellBuffer>(source);
            cells.Add(new SourcePollutionCellBuffer
            {
                Value = 3f,
                IsValid = 1,
            });
            var drops = em.AddBuffer<SourcePollutionDropRequestBuffer>(source);
            drops.Add(new SourcePollutionDropRequestBuffer
            {
                CellIndex = 0,
                Count = 1,
            });

            world.GetOrCreateSystem<SourcePollutionUpdateSystem>().Update(world.Unmanaged);

            cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            drops = em.GetBuffer<SourcePollutionDropRequestBuffer>(source);
            Assert.That(cells[0].Value, Is.EqualTo(3f));
            Assert.That(drops.Length, Is.EqualTo(1));
        }

        [Test]
        public void DepositRequest_DoesNotTrigger_WhenTopologyNotReadyAndStageIdle()
        {
            using var world = new World("StageTopologyRuntimeGate_Deposit");
            var em = world.EntityManager;

            SetSingleton(em, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 0,
                Ready = 0,
            });
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
            });

            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerCarryBinComponent),
                typeof(PlayerCarryBinDepositRequestTag),
                typeof(PlayerCarryBinDepositContextComponent),
                typeof(PlayerRadiusComponent),
                typeof(LocalTransform));
            em.SetComponentData(player, new PlayerCarryBinComponent
            {
                Load = 10,
                Capacity = 20,
            });
            em.SetComponentData(player, new PlayerCarryBinDepositContextComponent
            {
                DepositRegionId = 0u,
            });
            em.SetComponentData(player, new PlayerRadiusComponent { Value = 0.5f });
            em.SetComponentData(player, LocalTransform.FromPosition(float3.zero));
            em.SetComponentEnabled<PlayerCarryBinDepositRequestTag>(player, false);

            var gridEntity = em.CreateEntity(typeof(StageRuntimeGridComponent));
            em.SetComponentData(gridEntity, new StageRuntimeGridComponent
            {
                StageId = 1,
                Width = 1,
                Height = 1,
                CellSize = 1f,
                OriginX = 0f,
                OriginZ = 0f,
                Ready = 1,
            });
            em.AddBuffer<StageRuntimeGridCellBufferElement>(gridEntity).Add(new StageRuntimeGridCellBufferElement
            {
                MovementFlags = StageCellMovementFlags.None,
                DepositRegionId = 1u,
            });

            world.GetOrCreateSystem<PlayerCarryBinDepositRequestSystem>().Update(world.Unmanaged);

            Assert.That(em.IsComponentEnabled<PlayerCarryBinDepositRequestTag>(player), Is.False);
            Assert.That(em.GetComponentData<PlayerCarryBinDepositContextComponent>(player).DepositRegionId, Is.EqualTo(0u));
        }

        [Test]
        public void FrameCounterAndStageGateUpdate_Continue_WhenTopologyNotReadyAndStageIdle()
        {
            using var world = new World("StageTopologyRuntimeGate_FrameInfra");
            var em = world.EntityManager;

            em.CreateEntity(typeof(PlayerTag));
            SetSingleton(em, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 0,
                Ready = 0,
            });
            SetSingleton(em, new BulletFrameCounterComponent { Value = 0u });
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                HasStep = 1,
                UsingFixedTick = 0,
            });
            SetSingleton(em, new RunDirectorStageConfigComponent
            {
                InitialState = RunDirectorStageStateId.Idle,
                MinIdleDurationSec = 0f,
                ClearAutoAdvanceTimeoutSec = 10f,
            });
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
                StateElapsedSec = 0f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });
            SetSingleton(em, new RunDirectorStageGateComponent());

            world.GetOrCreateSystem<BulletFrameCounterAdvanceSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<RunDirectorStageGateUpdateSystem>().Update(world.Unmanaged);

            var frameCounter = em.CreateEntityQuery(ComponentType.ReadOnly<BulletFrameCounterComponent>())
                .GetSingleton<BulletFrameCounterComponent>();
            var stageState = em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageStateComponent>())
                .GetSingleton<RunDirectorStageStateComponent>();
            var stageGate = em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageGateComponent>())
                .GetSingleton<RunDirectorStageGateComponent>();
            Assert.That(frameCounter.Value, Is.EqualTo(1u));
            Assert.That(stageState.StateElapsedSec, Is.GreaterThan(0f));
            Assert.That(stageGate.MinIdleDurationElapsed, Is.EqualTo(1));
        }

        private static void SetSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }
    }
}
