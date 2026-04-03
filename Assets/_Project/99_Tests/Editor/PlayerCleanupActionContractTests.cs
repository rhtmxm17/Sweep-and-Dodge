using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class PlayerCleanupActionContractTests
    {
        [SetUp]
        public void SetUp()
        {
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();
        }

        [TearDown]
        public void TearDown()
        {
            ForceDisposeSharedContainersIfNeeded();
        }

        [Test]
        public void CleanupActionSet_DefaultsToBroomSweepAndMotionConstraints()
        {
            var asset = ScriptableObject.CreateInstance<PlayerCleanupActionSetSO>();
            try
            {
                Assert.That(asset.InitialSelectedAction, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
                Assert.That(asset.PrimarySlotAction, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
                Assert.That(asset.SecondarySlotAction, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
                Assert.That(asset.LockFacingWhileActive, Is.True);
                Assert.That(asset.ActiveMoveSpeedScale, Is.EqualTo(0.5f));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(asset);
            }
        }

        [Test]
        public void PlayerProxyAuthoring_RequiresCleanupActionSetForBake()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                PlayerProxyAuthoring.RequireCleanupActionSet(null));
        }

        [Test]
        public void DefaultCleanupActionAsset_UsesSingleBroomSweepProfile()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PlayerCleanupActionSetSO>(
                "Assets/_Project/03_Datas/PlayerActionSet/pas_default.asset");

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.InitialSelectedAction, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
            Assert.That(asset.PrimarySlotAction, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
            Assert.That(asset.SecondarySlotAction, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
            Assert.That(asset.LockFacingWhileActive, Is.True);
            Assert.That(asset.ActiveMoveSpeedScale, Is.EqualTo(0.5f));
            Assert.That(asset.Profiles, Has.Length.EqualTo(1));
            Assert.That(asset.Profiles[0].ActionId, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
            Assert.That(asset.Profiles[0].CaptureActiveTime, Is.EqualTo(0.20f));
            Assert.That(asset.Profiles[0].CaptureCooldown, Is.EqualTo(0f));
            Assert.That(asset.Profiles[0].ActiveTime, Is.EqualTo(0.22f));
            Assert.That(asset.Profiles[0].Cooldown, Is.EqualTo(1.8f));
            Assert.That(asset.Profiles[0].TrashRange, Is.EqualTo(3.2f));
            Assert.That(asset.Profiles[0].TrashSweepInnerRadius, Is.EqualTo(1f));
            Assert.That(asset.Profiles[0].HazardRectHalfWidth, Is.EqualTo(0.55f));
            Assert.That(asset.Profiles[0].HazardForwardWindowAngleDeg, Is.EqualTo(7f));
        }

        [Test]
        public void FallbackBroomSweepProfile_PopulatesLegacyAndBroomFields()
        {
            var profile = PlayerCleanupActionContractUtility.CreateFallbackBroomSweepProfile(
                range: 3.2f,
                captureRingRadius: 2.88f,
                captureRingWidth: 0.8f);

            Assert.That(profile.ActionId, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
            Assert.That(profile.CaptureActiveTime, Is.EqualTo(0.20f));
            Assert.That(profile.CaptureCooldown, Is.EqualTo(0f));
            Assert.That(profile.ActiveTime, Is.EqualTo(0.22f));
            Assert.That(profile.Cooldown, Is.EqualTo(1.8f));
            Assert.That(profile.TrashRange, Is.EqualTo(3.2f));
            Assert.That(profile.TrashFanHalfAngleDeg, Is.EqualTo(180f));
            Assert.That(profile.HazardRingRadius, Is.EqualTo(2.88f));
            Assert.That(profile.HazardRingWidth, Is.EqualTo(0.8f));
            Assert.That(profile.HazardLineLength, Is.EqualTo(0f));
            Assert.That(profile.HazardLineHalfWidth, Is.EqualTo(0f));
            Assert.That(profile.TrashSweepInnerRadius, Is.EqualTo(1f));
            Assert.That(profile.TrashSweepOuterRadius, Is.EqualTo(3.2f));
            Assert.That(profile.TrashSweepHalfAngleDeg, Is.EqualTo(12f));
            Assert.That(profile.TrashSweepStartAngleDeg, Is.EqualTo(-20f));
            Assert.That(profile.TrashSweepEndAngleDeg, Is.EqualTo(80f));
            Assert.That(profile.HazardRectLength, Is.EqualTo(3.2f));
            Assert.That(profile.HazardRectHalfWidth, Is.EqualTo(0.55f));
            Assert.That(profile.HazardForwardWindowAngleDeg, Is.EqualTo(7f));
        }

        [Test]
        public void BroomSweepGeometryUtility_ActivationFrame_UsesStartAngleAndSearchRadius()
        {
            var profile = CreateBroomSweepTimingProfile();
            var vacuum = new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.25f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.25f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            };
            var sweep = new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = -1,
                ActiveSweepDirectionSign = 1,
                LockedFacingXZ = new float2(0f, 1f),
                HasLockedFacing = 1,
                ActivationFrame = 1u,
            };

            var geometry = PlayerCleanupActionDebugGeometryUtility.ResolveBroomSweepFrameGeometry(
                PlayerCleanupActionId.BroomSweep,
                in vacuum,
                in sweep,
                in profile);

            float expectedSearchRadius = math.sqrt((3.2f * 3.2f) + (0.55f * 0.55f));
            Assert.That(geometry.CaptureReady, Is.EqualTo(1));
            Assert.That(geometry.Progress01, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(geometry.CurrentSweepCenterAngleDeg, Is.EqualTo(-20f).Within(0.001f));
            Assert.That(geometry.HazardWindowActive, Is.EqualTo(0));
            Assert.That(geometry.SearchRadius, Is.EqualTo(expectedSearchRadius).Within(0.001f));
        }

        [Test]
        public void BroomSweepGeometryUtility_RightToLeft_MirrorsSweepAngle()
        {
            var profile = CreateBroomSweepTimingProfile();
            var vacuum = new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.25f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.25f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            };
            var sweep = new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = -1,
                LockedFacingXZ = new float2(0f, 1f),
                HasLockedFacing = 1,
                ActivationFrame = 1u,
            };

            var geometry = PlayerCleanupActionDebugGeometryUtility.ResolveBroomSweepFrameGeometry(
                PlayerCleanupActionId.BroomSweep,
                in vacuum,
                in sweep,
                in profile);

            Assert.That(geometry.CaptureReady, Is.EqualTo(1));
            Assert.That(geometry.CurrentSweepCenterAngleDeg, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void BroomSweepGeometryUtility_HazardWindowAndRectCapture_ActivateNearForward()
        {
            var profile = CreateBroomSweepTimingProfile();
            var vacuum = new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 12f / 60f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 12f / 60f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            };
            var sweep = new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = -1,
                ActiveSweepDirectionSign = 1,
                LockedFacingXZ = new float2(0f, 1f),
                HasLockedFacing = 1,
                ActivationFrame = 1u,
            };

            var geometry = PlayerCleanupActionDebugGeometryUtility.ResolveBroomSweepFrameGeometry(
                PlayerCleanupActionId.BroomSweep,
                in vacuum,
                in sweep,
                in profile);

            bool hazardHit = PlayerCleanupActionDebugGeometryUtility.EvaluateBroomHazardCapture(
                dxp: 0f,
                dzp: 2.88f,
                bulletRadius: 0.2f,
                in profile,
                in geometry);

            Assert.That(geometry.HazardWindowActive, Is.EqualTo(1));
            Assert.That(geometry.CurrentSweepCenterAngleDeg, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hazardHit, Is.True);
        }

        [Test]
        public void BroomSweepGeometryUtility_TrashCapture_UsesCurrentBand()
        {
            var profile = CreateBroomSweepTimingProfile();
            var vacuum = new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.25f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.25f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            };
            var sweep = new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = -1,
                ActiveSweepDirectionSign = 1,
                LockedFacingXZ = new float2(0f, 1f),
                HasLockedFacing = 1,
                ActivationFrame = 1u,
            };

            var geometry = PlayerCleanupActionDebugGeometryUtility.ResolveBroomSweepFrameGeometry(
                PlayerCleanupActionId.BroomSweep,
                in vacuum,
                in sweep,
                in profile);

            float3 capturedPoint = BroomPolarPosition(1.2f, -20f);
            float3 missedPoint = BroomPolarPosition(1.2f, 20f);
            bool captured = PlayerCleanupActionDebugGeometryUtility.EvaluateBroomTrashCapture(
                distSq: capturedPoint.x * capturedPoint.x + capturedPoint.z * capturedPoint.z,
                dxp: capturedPoint.x,
                dzp: capturedPoint.z,
                bulletRadius: 0.2f,
                in profile,
                in geometry);
            bool missed = PlayerCleanupActionDebugGeometryUtility.EvaluateBroomTrashCapture(
                distSq: missedPoint.x * missedPoint.x + missedPoint.z * missedPoint.z,
                dxp: missedPoint.x,
                dzp: missedPoint.z,
                bulletRadius: 0.2f,
                in profile,
                in geometry);

            Assert.That(captured, Is.True);
            Assert.That(missed, Is.False);
        }

        [Test]
        public void PlayerCleanupActionSelectSystem_NormalizesInvalidActionsToBroomSweep()
        {
            using var world = new World("PlayerCleanupActionSelectSystem_Normalize");
            var em = world.EntityManager;

            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerCleanupActionStateComponent),
                typeof(VacuumRuntimeStateComponent));

            em.SetComponentData(player, new PlayerCleanupActionStateComponent
            {
                SelectedActionId = (PlayerCleanupActionId)254,
                PendingActionId = (PlayerCleanupActionId)255,
                Version = 0u,
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

            world.GetOrCreateSystem<PlayerCleanupActionSelectSystem>().Update(world.Unmanaged);

            var state = em.GetComponentData<PlayerCleanupActionStateComponent>(player);
            Assert.That(state.SelectedActionId, Is.EqualTo(PlayerCleanupActionId.BroomSweep));
            Assert.That(state.PendingActionId, Is.EqualTo(PlayerCleanupActionId.None));
            Assert.That(state.Version, Is.EqualTo(0u));
        }

        [Test]
        public void BulletVacuumRequestSystem_BroomSweepActivationEdge_ConsumesDirectionAndFlipsNextSweep()
        {
            using var world = new World("BulletVacuumRequestSystem_BroomSweepActivationEdge");
            var em = world.EntityManager;

            CreateVacuumSystemPrerequisites(em, frame: 7u);

            var player = CreateVacuumContractPlayer(em);
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = float3.zero,
                Rotation = quaternion.RotateY(math.radians(90f)),
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
                ActivateRequested = 1,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = -1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = float2.zero,
                HasLockedFacing = 0,
                ActivationFrame = 0u,
            });

            world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var vacuum = em.GetComponentData<VacuumRuntimeStateComponent>(player);
            var sweepRuntime = em.GetComponentData<PlayerCleanupSweepRuntimeStateComponent>(player);

            Assert.That(vacuum.IsActive, Is.EqualTo(1));
            Assert.That(sweepRuntime.ActiveSweepDirectionSign, Is.EqualTo(-1));
            Assert.That(sweepRuntime.NextSweepDirectionSign, Is.EqualTo(1));
            Assert.That(sweepRuntime.HasLockedFacing, Is.EqualTo(1));
            Assert.That(sweepRuntime.ActivationFrame, Is.EqualTo(7u));
            Assert.That(sweepRuntime.LockedFacingXZ.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(sweepRuntime.LockedFacingXZ.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BulletVacuumRequestSystem_BroomSweepLeftToRight_TrashBandCapturesOnlyWithinCurrentSweep()
        {
            using var world = new World("BulletVacuumRequestSystem_BroomSweepLeftToRightTrash");
            var em = world.EntityManager;

            CreateVacuumSystemPrerequisites(em, frame: 1u);

            var player = CreateVacuumContractPlayer(em);
            SetBroomSweepActivationRequest(em, player, nextSweepDirectionSign: 1);
            var capturedBullet = CreateCollectibleBullet(em, BroomPolarPosition(1.2f, -20f), scoreValue: 2);
            var missedBullet = CreateCollectibleBullet(em, BroomPolarPosition(1.2f, 20f), scoreValue: 2);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var sweepRuntime = em.GetComponentData<PlayerCleanupSweepRuntimeStateComponent>(player);
            Assert.That(sweepRuntime.ActiveSweepDirectionSign, Is.EqualTo(1));
            Assert.That(sweepRuntime.NextSweepDirectionSign, Is.EqualTo(-1));
            Assert.That(em.GetComponentData<PlayerCarryBinComponent>(player).Load, Is.EqualTo(2));
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(capturedBullet).Reason, Is.EqualTo(BulletLifecycleReasonId.VacuumCollected));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(capturedBullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(missedBullet), Is.False);
        }

        [Test]
        public void BulletVacuumRequestSystem_BroomSweepRightToLeft_TrashBandMirrorsCapture()
        {
            using var world = new World("BulletVacuumRequestSystem_BroomSweepRightToLeftTrash");
            var em = world.EntityManager;

            CreateVacuumSystemPrerequisites(em, frame: 2u);

            var player = CreateVacuumContractPlayer(em);
            SetBroomSweepActivationRequest(em, player, nextSweepDirectionSign: -1);
            var capturedBullet = CreateCollectibleBullet(em, BroomPolarPosition(1.2f, 20f), scoreValue: 3);
            var missedBullet = CreateCollectibleBullet(em, BroomPolarPosition(1.2f, -20f), scoreValue: 3);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var sweepRuntime = em.GetComponentData<PlayerCleanupSweepRuntimeStateComponent>(player);
            Assert.That(sweepRuntime.ActiveSweepDirectionSign, Is.EqualTo(-1));
            Assert.That(sweepRuntime.NextSweepDirectionSign, Is.EqualTo(1));
            Assert.That(em.GetComponentData<PlayerCarryBinComponent>(player).Load, Is.EqualTo(3));
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(capturedBullet).Reason, Is.EqualTo(BulletLifecycleReasonId.VacuumCollected));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(capturedBullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(missedBullet), Is.False);
        }

        [Test]
        public void BulletVacuumRequestSystem_BroomSweepHazardRect_CapturesOnlyInsideForwardWindow()
        {
            using var world = new World("BulletVacuumRequestSystem_BroomSweepHazardWindow");
            var em = world.EntityManager;

            CreateVacuumSystemPrerequisites(em, frame: 9u);

            var player = CreateVacuumContractPlayer(em);
            var capturedHazard = CreateHazardBullet(em, new float3(0f, 0f, 2.88f), scoreValue: 4);
            PrimeBroomSweepForwardWindow(em, player, activeSweepDirectionSign: 1);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetComponentData<PlayerCarryBinComponent>(player).Load, Is.EqualTo(4));
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(capturedHazard).Reason, Is.EqualTo(BulletLifecycleReasonId.VacuumCollected));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(capturedHazard), Is.True);
        }

        [Test]
        public void BulletVacuumRequestSystem_BroomSweepHazardRect_MissesOutsideForwardWindow()
        {
            using var world = new World("BulletVacuumRequestSystem_BroomSweepHazardWindowMiss");
            var em = world.EntityManager;

            CreateVacuumSystemPrerequisites(em, frame: 10u);

            var player = CreateVacuumContractPlayer(em);
            var missedHazard = CreateHazardBullet(em, new float3(0f, 0f, 2.88f), scoreValue: 4);
            SetBroomSweepActivationRequest(em, player, nextSweepDirectionSign: 1);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetComponentData<PlayerCarryBinComponent>(player).Load, Is.EqualTo(0));
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(missedHazard).Reason, Is.EqualTo(BulletLifecycleReasonId.None));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(missedHazard), Is.False);
        }

        [Test]
        public void BulletVacuumRequestSystem_ClearsLockedFacingWhenVacuumLockDisablesVacuum()
        {
            using var world = new World("BulletVacuumRequestSystem_BroomSweepLockClear");
            var em = world.EntityManager;

            CreateVacuumSystemPrerequisites(em, frame: 11u);
            var player = CreateVacuumContractPlayer(em);

            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.2f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.2f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerHazardPenaltyStateComponent
            {
                IFrameTimer = 0f,
                VacuumLockTimer = 0.5f,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = new float2(1f, 0f),
                HasLockedFacing = 1,
                ActivationFrame = 10u,
            });

            world.GetOrCreateSystem<BulletVacuumRequestSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var vacuum = em.GetComponentData<VacuumRuntimeStateComponent>(player);
            var sweepRuntime = em.GetComponentData<PlayerCleanupSweepRuntimeStateComponent>(player);

            Assert.That(vacuum.IsActive, Is.EqualTo(0));
            Assert.That(sweepRuntime.HasLockedFacing, Is.EqualTo(0));
            Assert.That(sweepRuntime.ActivationFrame, Is.EqualTo(0u));
            Assert.That(sweepRuntime.LockedFacingXZ, Is.EqualTo(float2.zero));
        }

        private static Entity CreateVacuumContractPlayer(EntityManager em)
        {
            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(LocalTransform),
                typeof(PlayerGoSyncComponent),
                typeof(VacuumRuntimeStateComponent),
                typeof(PlayerCarryBinComponent),
                typeof(PlayerHazardRiskConfigComponent),
                typeof(PlayerHazardRiskStateComponent),
                typeof(PlayerHazardRiskRequestComponent),
                typeof(PlayerHazardPenaltyStateComponent),
                typeof(PlayerCleanupActionStateComponent),
                typeof(PlayerCleanupActionSlotMapComponent),
                typeof(PlayerCleanupSweepRuntimeStateComponent),
                typeof(PlayerCleanupMotionConstraintConfigComponent));

            em.SetComponentData(player, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                SyncRotation = 0,
                VacuumRequested = 0,
                CleanupActionRequested = 0,
                RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.25f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0.25f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCarryBinComponent
            {
                Load = 0,
                Capacity = 10,
            });
            em.SetComponentData(player, new PlayerHazardRiskConfigComponent
            {
                HazardBonusRate = 0.1f,
                HazardStackMax = 5,
            });
            em.SetComponentData(player, new PlayerHazardRiskStateComponent
            {
                HazardStack = 0,
            });
            em.SetComponentData(player, new PlayerHazardRiskRequestComponent
            {
                PendingHazardCapturedCount = 0,
                ResetRequested = 0,
            });
            em.SetComponentData(player, new PlayerHazardPenaltyStateComponent
            {
                IFrameTimer = 0f,
                VacuumLockTimer = 0f,
            });
            em.SetComponentData(player, new PlayerCleanupActionStateComponent
            {
                SelectedActionId = PlayerCleanupActionId.BroomSweep,
                PendingActionId = PlayerCleanupActionId.None,
                Version = 0u,
            });
            em.SetComponentData(player, new PlayerCleanupActionSlotMapComponent
            {
                PrimaryActionId = PlayerCleanupActionId.BroomSweep,
                SecondaryActionId = PlayerCleanupActionId.BroomSweep,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = 1,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = float2.zero,
                HasLockedFacing = 0,
                ActivationFrame = 0u,
            });
            em.SetComponentData(player, new PlayerCleanupMotionConstraintConfigComponent
            {
                LockFacingWhileActive = 1,
                ActiveMoveSpeedScale = 0.5f,
            });

            var profiles = em.AddBuffer<PlayerCleanupActionProfileBufferElement>(player);
            profiles.Add(CreateBroomSweepTimingProfile());

            var uiBuffer = em.AddBuffer<PlayerUiFeedbackEventBufferElement>(player);
            uiBuffer.EnsureCapacity(16);
            em.AddBuffer<PlayerImpulseEventBufferElement>(player);

            return player;
        }

        private static Entity CreateCollectibleBullet(EntityManager em, float3 position, int scoreValue)
        {
            var bullet = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletVelocityComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletRadiusComponent),
                typeof(BulletScoreValueComponent),
                typeof(BulletCaptureRuleComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(bullet, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(bullet, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(bullet, new BulletLifetimeComponent { Value = 8f });
            em.SetComponentData(bullet, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.None,
                Priority = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(bullet, default(BulletLifecycleContactComponent));
            em.SetComponentData(bullet, new BulletRadiusComponent { Value = 0.2f });
            em.SetComponentData(bullet, new BulletScoreValueComponent { Value = math.max(0, scoreValue) });
            em.SetComponentData(bullet, new BulletCaptureRuleComponent { Value = BulletCaptureRuleId.StandardCollectible });
            em.SetComponentEnabled<BulletActiveTag>(bullet, true);
            em.SetComponentEnabled<BulletDespawnRequestTag>(bullet, false);
            return bullet;
        }

        private static Entity CreateHazardBullet(EntityManager em, float3 position, int scoreValue)
        {
            var bullet = CreateCollectibleBullet(em, position, scoreValue);
            em.SetComponentData(bullet, new BulletCaptureRuleComponent { Value = BulletCaptureRuleId.RiskTimedResolve });
            return bullet;
        }

        private static void SetBroomSweepActivationRequest(
            EntityManager em,
            Entity player,
            sbyte nextSweepDirectionSign)
        {
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 0f,
                CooldownTimer = 0f,
                IsActive = 0,
                ActivateRequested = 1,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = nextSweepDirectionSign,
                ActiveSweepDirectionSign = 0,
                LockedFacingXZ = float2.zero,
                HasLockedFacing = 0,
                ActivationFrame = 0u,
            });
        }

        private static void PrimeBroomSweepForwardWindow(
            EntityManager em,
            Entity player,
            sbyte activeSweepDirectionSign)
        {
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 13f / 60f,
                CaptureCooldownTimer = 0f,
                ActiveTimer = 13f / 60f,
                CooldownTimer = 0f,
                IsActive = 1,
                ActivateRequested = 0,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = (sbyte)(-activeSweepDirectionSign),
                ActiveSweepDirectionSign = activeSweepDirectionSign,
                LockedFacingXZ = new float2(0f, 1f),
                HasLockedFacing = 1,
                ActivationFrame = 1u,
            });
        }

        private static float3 BroomPolarPosition(float radius, float angleDeg)
        {
            float rad = math.radians(angleDeg);
            return new float3(
                radius * math.sin(rad),
                0f,
                radius * math.cos(rad));
        }

        private static PlayerCleanupActionProfileBufferElement CreateBroomSweepTimingProfile(
            float activeTime = 0.25f,
            float cooldown = 0f,
            float captureActiveTime = 0.25f,
            float captureCooldown = 0f)
        {
            return PlayerCleanupActionContractUtility.CreateFallbackBroomSweepProfile(
                3.2f,
                2.88f,
                0.8f,
                captureActiveTime,
                captureCooldown,
                activeTime,
                cooldown);
        }

        private static void CreateSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }

        private static void CreateVacuumSystemPrerequisites(EntityManager em, uint frame)
        {
            CreateSingleton(em, new BulletFieldConfigComponent
            {
                PoolSize = 32,
                InvCellSize = 1f,
            });
            CreateSingleton(em, new BulletFrameCounterComponent
            {
                Value = frame,
            });
            CreateSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });
        }

        private static void InitializeSharedContainers(int capacity = 128)
        {
            BulletFieldShared.FreeByKey = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.CellMap = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.HazardCellMap = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.PoolFence = default;
            BulletFieldShared.CellMapFence = default;
            BulletFieldShared.MarkInitialized();
        }

        private static void ForceDisposeSharedContainersIfNeeded()
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            JobHandle.CombineDependencies(BulletFieldShared.PoolFence, BulletFieldShared.CellMapFence).Complete();
            if (BulletFieldShared.CellMap.IsCreated)
                BulletFieldShared.CellMap.Dispose();
            if (BulletFieldShared.HazardCellMap.IsCreated)
                BulletFieldShared.HazardCellMap.Dispose();
            if (BulletFieldShared.FreeByKey.IsCreated)
                BulletFieldShared.FreeByKey.Dispose();

            BulletFieldShared.PoolFence = default;
            BulletFieldShared.CellMapFence = default;
            BulletFieldShared.MarkUninitialized();
        }
    }
}
