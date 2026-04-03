using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class PlayerIntentMovementSystemTests
    {
        [Test]
        public void InactiveBroomSweep_AimWorldPointRotatesPlayer()
        {
            using var world = new World("PlayerIntentMovementSystem_InactiveAim");
            var em = world.EntityManager;
            CreateFixedTickRuntime(em, deltaTime: 1f);
            var player = CreatePlayer(em);

            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = new float2(10f, 0f),
                HasAimWorldPoint = 1,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });

            world.GetOrCreateSystem<PlayerIntentMovementSystem>().Update(world.Unmanaged);

            AssertForward(em.GetComponentData<LocalTransform>(player).Rotation, new float3(1f, 0f, 0f));
            AssertForward(em.GetComponentData<PlayerGoSyncComponent>(player).Rotation, new float3(1f, 0f, 0f));
        }

        [Test]
        public void ActiveBroomSweep_WithLockedFacing_IgnoresAimRotation()
        {
            using var world = new World("PlayerIntentMovementSystem_LockedFacing");
            var em = world.EntityManager;
            CreateFixedTickRuntime(em, deltaTime: 1f);
            var player = CreatePlayer(em);

            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = new float2(0f, 10f),
                HasAimWorldPoint = 1,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.2f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.2f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = new float2(1f, 0f),
                HasLockedFacing = 1,
                ActivationFrame = 3u,
            });

            world.GetOrCreateSystem<PlayerIntentMovementSystem>().Update(world.Unmanaged);

            AssertForward(em.GetComponentData<LocalTransform>(player).Rotation, new float3(1f, 0f, 0f));
            AssertForward(em.GetComponentData<PlayerGoSyncComponent>(player).Rotation, new float3(1f, 0f, 0f));
        }

        [Test]
        public void ActiveBroomSweep_WithLockDisabled_StillUsesAimRotation()
        {
            using var world = new World("PlayerIntentMovementSystem_LockDisabled");
            var em = world.EntityManager;
            CreateFixedTickRuntime(em, deltaTime: 1f);
            var player = CreatePlayer(em);

            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = new float2(10f, 0f),
                HasAimWorldPoint = 1,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.2f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.2f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCleanupMotionConstraintConfigComponent
            {
                LockFacingWhileActive = 0,
                ActiveMoveSpeedScale = 0.5f,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = new float2(0f, 1f),
                HasLockedFacing = 1,
                ActivationFrame = 3u,
            });

            world.GetOrCreateSystem<PlayerIntentMovementSystem>().Update(world.Unmanaged);

            AssertForward(em.GetComponentData<LocalTransform>(player).Rotation, new float3(1f, 0f, 0f));
        }

        [Test]
        public void ActiveBroomSweep_MovementUsesConfiguredScale()
        {
            using var world = new World("PlayerIntentMovementSystem_MovementScale");
            var em = world.EntityManager;
            CreateFixedTickRuntime(em, deltaTime: 1f);
            var player = CreatePlayer(em);

            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = new float2(1f, 0f),
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.2f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.2f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCleanupMotionConstraintConfigComponent
            {
                LockFacingWhileActive = 1,
                ActiveMoveSpeedScale = 0.5f,
            });

            world.GetOrCreateSystem<PlayerIntentMovementSystem>().Update(world.Unmanaged);

            Assert.That(em.GetComponentData<LocalTransform>(player).Position.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(em.GetComponentData<PlayerGoSyncComponent>(player).Position.x, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void ActiveLegacyAction_KeepsLegacyMovementAndAim()
        {
            using var world = new World("PlayerIntentMovementSystem_LegacyAction");
            var em = world.EntityManager;
            CreateFixedTickRuntime(em, deltaTime: 1f);
            var player = CreatePlayer(em, selectedActionId: PlayerCleanupActionId.RadialRing);

            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = new float2(1f, 0f),
                AimWorldXZ = new float2(6f, 10f),
                HasAimWorldPoint = 1,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.2f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.2f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = new float2(1f, 0f),
                HasLockedFacing = 1,
                ActivationFrame = 3u,
            });

            world.GetOrCreateSystem<PlayerIntentMovementSystem>().Update(world.Unmanaged);

            Assert.That(em.GetComponentData<LocalTransform>(player).Position.x, Is.EqualTo(6f).Within(0.001f));
            AssertForward(em.GetComponentData<LocalTransform>(player).Rotation, new float3(0f, 0f, 1f));
        }

        private static void CreateFixedTickRuntime(EntityManager em, float deltaTime)
        {
            var entity = em.CreateEntity(typeof(FixedTickStepRuntimeComponent));
            em.SetComponentData(entity, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = math.max(0f, deltaTime),
                LogicDeltaTime = math.max(0f, deltaTime),
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
                CurrentLogicFrame = 0u,
            });
        }

        private static Entity CreatePlayer(EntityManager em, PlayerCleanupActionId selectedActionId = PlayerCleanupActionId.BroomSweep)
        {
            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerInputIntentComponent),
                typeof(LocalTransform),
                typeof(PlayerGoSyncComponent),
                typeof(VacuumRuntimeStateComponent),
                typeof(PlayerCleanupActionStateComponent),
                typeof(PlayerCleanupMotionConstraintConfigComponent),
                typeof(PlayerCleanupSweepRuntimeStateComponent));

            var initialRotation = quaternion.identity;
            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = float2.zero,
                AimWorldXZ = float2.zero,
                HasAimWorldPoint = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                Sequence = 0u,
            });
            em.SetComponentData(player, LocalTransform.FromPositionRotationScale(float3.zero, initialRotation, 1f));
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = float3.zero,
                Rotation = initialRotation,
                SyncRotation = 1,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0f,
                CooldownTimer = 0f,
                IsActive = 0,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCleanupActionStateComponent
            {
                SelectedActionId = selectedActionId,
                PendingActionId = PlayerCleanupActionId.None,
                Version = 0u,
            });
            em.SetComponentData(player, new PlayerCleanupMotionConstraintConfigComponent
            {
                LockFacingWhileActive = 1,
                ActiveMoveSpeedScale = 0.5f,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = float2.zero,
                HasLockedFacing = 0,
                ActivationFrame = 0u,
            });
            return player;
        }

        private static void AssertForward(quaternion rotation, float3 expectedForward)
        {
            float3 actualForward = math.forward(rotation);
            Assert.That(actualForward.x, Is.EqualTo(expectedForward.x).Within(0.001f));
            Assert.That(actualForward.y, Is.EqualTo(expectedForward.y).Within(0.001f));
            Assert.That(actualForward.z, Is.EqualTo(expectedForward.z).Within(0.001f));
        }
    }
}
