using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class PlayerStageEntryApplyPrepareSystemTests
    {
        [Test]
        public void PlayerStageEntryApply_SynchronizesSpatialStateAndVersion()
        {
            using var world = CreateWorld(out var em, out var player);

            SetPlayerStartRuntime(em, stageId: 2, position: new float3(4f, 0f, -3f), yawDeg: 90f, appliedVersion: 3u, ready: 1);
            world.GetOrCreateSystem<PlayerStageEntryApplyPrepareSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            var sync = em.GetComponentData<PlayerGoSyncComponent>(player);
            var previous = em.GetComponentData<PlayerPreviousPositionComponent>(player);
            var applyState = em.GetComponentData<PlayerStageEntryApplyStateComponent>(player);

            Assert.That(tx.Position.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(math.degrees(math.atan2(2f * (tx.Rotation.value.w * tx.Rotation.value.y), 1f - 2f * tx.Rotation.value.y * tx.Rotation.value.y)), Is.EqualTo(90f).Within(0.01f));
            Assert.That(sync.Position.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(sync.Position.z, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(previous.Position.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(previous.Position.z, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(applyState.LastAppliedVersion, Is.EqualTo(3u));
        }

        [Test]
        public void PlayerStageEntryApply_SameVersion_IsIdempotent()
        {
            using var world = CreateWorld(out var em, out var player);

            SetPlayerStartRuntime(em, stageId: 2, position: new float3(2f, 0f, 5f), yawDeg: 30f, appliedVersion: 7u, ready: 1);
            world.GetOrCreateSystem<PlayerStageEntryApplyPrepareSystem>().Update(world.Unmanaged);

            var moved = LocalTransform.FromPositionRotationScale(new float3(100f, 0f, 100f), quaternion.identity, 1f);
            em.SetComponentData(player, moved);
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = new float3(100f, 0f, 100f),
                Rotation = quaternion.identity,
                SyncRotation = 1,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
            });

            world.GetOrCreateSystem<PlayerStageEntryApplyPrepareSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            var sync = em.GetComponentData<PlayerGoSyncComponent>(player);
            Assert.That(tx.Position.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(100f).Within(0.001f));
            Assert.That(sync.Position.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(sync.Position.z, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void PlayerStageEntryApply_NewVersion_ReappliesStageEntry()
        {
            using var world = CreateWorld(out var em, out var player);

            SetPlayerStartRuntime(em, stageId: 2, position: new float3(1f, 0f, 1f), yawDeg: 0f, appliedVersion: 1u, ready: 1);
            world.GetOrCreateSystem<PlayerStageEntryApplyPrepareSystem>().Update(world.Unmanaged);

            SetPlayerStartRuntime(em, stageId: 2, position: new float3(-6f, 0f, 8f), yawDeg: 180f, appliedVersion: 2u, ready: 1);
            world.GetOrCreateSystem<PlayerStageEntryApplyPrepareSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            var previous = em.GetComponentData<PlayerPreviousPositionComponent>(player);
            var applyState = em.GetComponentData<PlayerStageEntryApplyStateComponent>(player);

            Assert.That(tx.Position.x, Is.EqualTo(-6f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(8f).Within(0.001f));
            Assert.That(previous.Position.x, Is.EqualTo(-6f).Within(0.001f));
            Assert.That(previous.Position.z, Is.EqualTo(8f).Within(0.001f));
            Assert.That(applyState.LastAppliedVersion, Is.EqualTo(2u));
        }

        private static World CreateWorld(out EntityManager em, out Entity player)
        {
            var world = new World("PlayerStageEntryApplyPrepareSystemTests");
            em = world.EntityManager;

            CreateSingleton(em, default(StagePlayerStartRuntimeComponent));
            CreateSingleton(em, new StageTopologyLifecycleStateComponent
            {
                CurrentAppliedVersion = 0u,
            });

            player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(LocalTransform),
                typeof(PlayerGoSyncComponent),
                typeof(PlayerPreviousPositionComponent),
                typeof(PlayerStageEntryApplyStateComponent));
            em.SetComponentData(player, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                SyncRotation = 1,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = 0,
            });
            em.SetComponentData(player, new PlayerPreviousPositionComponent
            {
                Position = float3.zero,
            });
            em.SetComponentData(player, new PlayerStageEntryApplyStateComponent
            {
                LastAppliedVersion = 0u,
            });

            return world;
        }

        private static void CreateSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }

        private static void SetPlayerStartRuntime(EntityManager em, int stageId, float3 position, float yawDeg, uint appliedVersion, byte ready)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<StagePlayerStartRuntimeComponent>());
            var entity = query.GetSingletonEntity();
            em.SetComponentData(entity, new StagePlayerStartRuntimeComponent
            {
                StageId = stageId,
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
                YawDeg = yawDeg,
                Ready = ready,
                AppliedVersion = appliedVersion,
            });

            using var lifecycleQuery = em.CreateEntityQuery(ComponentType.ReadWrite<StageTopologyLifecycleStateComponent>());
            var lifecycleEntity = lifecycleQuery.GetSingletonEntity();
            em.SetComponentData(lifecycleEntity, new StageTopologyLifecycleStateComponent
            {
                CurrentAppliedVersion = appliedVersion,
            });
        }
    }
}
