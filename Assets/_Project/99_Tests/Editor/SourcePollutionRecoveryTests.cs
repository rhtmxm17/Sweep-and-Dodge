using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class SourcePollutionRecoveryTests
    {
        [Test]
        public void DropToMin_MarksCellInactive_AndRecordsCooldownFrame()
        {
            using var world = CreateWorld(10u, 1f);
            var em = world.EntityManager;
            var source = CreateSource(em, cols: 2, rows: 1, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0f,
                DropPerCollect = 1f,
                TopKSampleCount = 1,
                ActiveRatioThreshold = 0.25f,
                RecoveryCooldownFrames = 5u,
                RecoveryWaveSeedCount = 1,
                RecoveryWaveClusterSize = 1,
                RecoveryWaveRestoreValue = 0.4f,
                RecoveryRecentCleanBiasFrames = 10u,
            });

            var cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            cells.Add(CreateCell(value: 0.5f, isValid: true, isActive: true));
            cells.Add(CreateCell(value: 0.5f, isValid: true, isActive: true));
            em.GetBuffer<SourcePollutionDropRequestBuffer>(source).Add(new SourcePollutionDropRequestBuffer
            {
                CellIndex = 0,
                Count = 1,
            });

            world.GetOrCreateSystem<SourcePollutionUpdateSystem>().Update(world.Unmanaged);

            cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            Assert.That(cells[0].IsActive, Is.EqualTo(0));
            Assert.That(cells[0].Value, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(cells[0].LastDropFrame, Is.EqualTo(10u));
            Assert.That(cells[0].CooldownUntilFrame, Is.EqualTo(15u));
        }

        [Test]
        public void Regen_AppliesOnlyToActiveCells()
        {
            using var world = CreateWorld(20u, 1f);
            var em = world.EntityManager;
            var source = CreateSource(em, cols: 2, rows: 1, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0.5f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 1,
                ActiveRatioThreshold = 0.4f,
                RecoveryCooldownFrames = 5u,
                RecoveryWaveSeedCount = 1,
                RecoveryWaveClusterSize = 1,
                RecoveryWaveRestoreValue = 0.4f,
                RecoveryRecentCleanBiasFrames = 10u,
            });

            var cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            cells.Add(CreateCell(value: 0.2f, isValid: true, isActive: true));
            cells.Add(CreateCell(value: 0.3f, isValid: true, isActive: false, cooldownUntilFrame: 30u));

            world.GetOrCreateSystem<SourcePollutionUpdateSystem>().Update(world.Unmanaged);

            cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            Assert.That(cells[0].Value, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(cells[1].Value, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(cells[1].IsActive, Is.EqualTo(0));
        }

        [Test]
        public void RecoveryWave_UsesEightNeighborClusterExpansion()
        {
            using var world = CreateWorld(10u, 1f);
            var em = world.EntityManager;
            var source = CreateSource(em, cols: 3, rows: 3, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 1,
                ActiveRatioThreshold = 0.5f,
                RecoveryCooldownFrames = 0u,
                RecoveryWaveSeedCount = 1,
                RecoveryWaveClusterSize = 2,
                RecoveryWaveRestoreValue = 0.6f,
                RecoveryRecentCleanBiasFrames = 10u,
            });

            var cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            for (int i = 0; i < 9; i++)
                cells.Add(CreateCell(value: 0f, isValid: false, isActive: false));

            cells[4] = CreateCell(value: 0f, isValid: true, isActive: false, lastDropFrame: 0u);
            cells[0] = CreateCell(value: 0f, isValid: true, isActive: false, lastDropFrame: 5u);

            world.GetOrCreateSystem<SourcePollutionUpdateSystem>().Update(world.Unmanaged);

            cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            Assert.That(cells[4].IsActive, Is.EqualTo(1));
            Assert.That(cells[0].IsActive, Is.EqualTo(1), "Diagonal neighbor should be recovered by 8-neighbor wave expansion.");
        }

        [Test]
        public void RecoveryWave_PrefersOlderInactiveSeed_WhenRecentCleanBiasIsEnabled()
        {
            using var world = CreateWorld(10u, 1f);
            var em = world.EntityManager;
            var source = CreateSource(em, cols: 3, rows: 1, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 1,
                ActiveRatioThreshold = 0.8f,
                RecoveryCooldownFrames = 0u,
                RecoveryWaveSeedCount = 1,
                RecoveryWaveClusterSize = 1,
                RecoveryWaveRestoreValue = 0.5f,
                RecoveryRecentCleanBiasFrames = 10u,
            });

            var cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            cells.Add(CreateCell(value: 0f, isValid: true, isActive: false, lastDropFrame: 9u));
            cells.Add(CreateCell(value: 0f, isValid: true, isActive: false, lastDropFrame: 0u));
            cells.Add(CreateCell(value: 1f, isValid: true, isActive: true));

            world.GetOrCreateSystem<SourcePollutionUpdateSystem>().Update(world.Unmanaged);

            cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            Assert.That(cells[1].IsActive, Is.EqualTo(1));
            Assert.That(cells[0].IsActive, Is.EqualTo(0));
        }

        [Test]
        public void ForceWaveFallback_RecoversOldestInactiveEvenWhenCooldownHasNotElapsed()
        {
            using var world = CreateWorld(10u, 1f);
            var em = world.EntityManager;
            var source = CreateSource(em, cols: 2, rows: 1, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 1,
                ActiveRatioThreshold = 0.5f,
                RecoveryCooldownFrames = 10u,
                RecoveryWaveSeedCount = 1,
                RecoveryWaveClusterSize = 1,
                RecoveryWaveRestoreValue = 0.5f,
                RecoveryRecentCleanBiasFrames = 10u,
            });

            var cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            cells.Add(CreateCell(value: 0f, isValid: true, isActive: false, lastDropFrame: 0u, cooldownUntilFrame: 20u));
            cells.Add(CreateCell(value: 0f, isValid: true, isActive: false, lastDropFrame: 5u, cooldownUntilFrame: 20u));

            world.GetOrCreateSystem<SourcePollutionUpdateSystem>().Update(world.Unmanaged);

            cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            Assert.That(cells[0].IsActive, Is.EqualTo(1));
            Assert.That(cells[1].IsActive, Is.EqualTo(0));
        }

        [Test]
        public void ResetPollutionRuntimeState_ReactivatesValidCells_AndClearsTransientState()
        {
            using var world = new World("PollutionResetHelper");
            var em = world.EntityManager;
            var source = em.CreateEntity();
            em.AddBuffer<SourcePollutionCellBuffer>(source);
            em.AddBuffer<SourcePollutionDropRequestBuffer>(source);

            var cells = em.GetBuffer<SourcePollutionCellBuffer>(source);
            var drops = em.GetBuffer<SourcePollutionDropRequestBuffer>(source);
            cells.Add(CreateCell(value: 0f, isValid: true, isActive: false, lastDropFrame: 4u, cooldownUntilFrame: 9u));
            cells.Add(CreateCell(value: 0.2f, isValid: false, isActive: false, lastDropFrame: 7u, cooldownUntilFrame: 11u));
            drops.Add(new SourcePollutionDropRequestBuffer { CellIndex = 0, Count = 1 });

            var config = new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 1,
                ActiveRatioThreshold = 0.5f,
                RecoveryCooldownFrames = 5u,
                RecoveryWaveSeedCount = 1,
                RecoveryWaveClusterSize = 1,
                RecoveryWaveRestoreValue = 0.5f,
                RecoveryRecentCleanBiasFrames = 10u,
            };

            SourceRuntimeApplyUtility.ResetPollutionRuntimeState(in config, cells, drops);

            Assert.That(cells[0].IsActive, Is.EqualTo(1));
            Assert.That(cells[0].Value, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(cells[0].LastDropFrame, Is.EqualTo(0u));
            Assert.That(cells[0].CooldownUntilFrame, Is.EqualTo(0u));
            Assert.That(cells[1].IsActive, Is.EqualTo(0));
            Assert.That(drops.Length, Is.EqualTo(0));
        }

        private static World CreateWorld(uint frame, float deltaTime)
        {
            var world = new World("SourcePollutionRecovery");
            var em = world.EntityManager;
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = deltaTime,
                LogicDeltaTime = deltaTime,
                HasStep = 1,
                UsingFixedTick = 0,
            });
            SetSingleton(em, new BulletFrameCounterComponent { Value = frame });
            return world;
        }

        private static Entity CreateSource(
            EntityManager em,
            int cols,
            int rows,
            in SourcePollutionConfigComponent config)
        {
            var source = em.CreateEntity(typeof(SourcePollutionConfigComponent), typeof(SourcePollutionGridComponent));
            em.SetComponentData(source, config);
            em.SetComponentData(source, new SourcePollutionGridComponent
            {
                Cols = cols,
                Rows = rows,
                CellSize = 1f,
                InvCellSize = 1f,
            });
            em.AddBuffer<SourcePollutionCellBuffer>(source);
            em.AddBuffer<SourcePollutionDropRequestBuffer>(source);
            return source;
        }

        private static SourcePollutionCellBuffer CreateCell(
            float value,
            bool isValid,
            bool isActive,
            uint lastDropFrame = 0u,
            uint cooldownUntilFrame = 0u)
        {
            return new SourcePollutionCellBuffer
            {
                Value = value,
                IsValid = isValid ? (byte)1 : (byte)0,
                IsActive = isActive ? (byte)1 : (byte)0,
                LastDropFrame = lastDropFrame,
                CooldownUntilFrame = cooldownUntilFrame,
            };
        }

        private static void SetSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }
    }
}
