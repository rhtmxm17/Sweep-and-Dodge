using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class HazardEmitterRuntimePathTests
    {
        [SetUp]
        public void SetUp() => ForceDisposeSharedContainersIfNeeded();

        [TearDown]
        public void TearDown() => ForceDisposeSharedContainersIfNeeded();

        [Test]
        public void HazardActorAuthoringValidation_RequiresParentSourceAuthoring()
        {
            var root = new GameObject("hazard_actor_root");
            var actorGo = new GameObject("hazard_actor");
            actorGo.transform.SetParent(root.transform);
            var authoring = actorGo.AddComponent<HazardActorAuthoring>();

            try
            {
                bool ok = HazardActorAuthoringValidationUtility.TryValidate(authoring, out var sourceAuthoring, out var error);
                Assert.That(ok, Is.False);
                Assert.That(sourceAuthoring, Is.Null);
                Assert.That(error, Does.Contain("parent SourceRuntimeTemplateAuthoringBase"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardActorAuthoringValidation_AcceptsValidSourceParent()
        {
            var root = new GameObject("source_root");
            var sourceAuthoring = root.AddComponent<SourceRuntimeTemplateAuthoring>();
            var actorGo = new GameObject("hazard_actor");
            actorGo.transform.SetParent(root.transform);
            var authoring = actorGo.AddComponent<HazardActorAuthoring>();

            try
            {
                bool ok = HazardActorAuthoringValidationUtility.TryValidate(authoring, out var owner, out var error);
                Assert.That(ok, Is.True);
                Assert.That(owner, Is.EqualTo(sourceAuthoring));
                Assert.That(error, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardActorAuthoringValidation_RejectsNegativePresenceDurations()
        {
            var root = new GameObject("source_root");
            root.AddComponent<SourceRuntimeTemplateAuthoring>();
            var actorGo = new GameObject("hazard_actor");
            actorGo.transform.SetParent(root.transform);
            var authoring = actorGo.AddComponent<HazardActorAuthoring>();
            authoring.ActivationDurationSec = -0.1f;

            try
            {
                bool ok = HazardActorAuthoringValidationUtility.TryValidate(authoring, out var owner, out var error);
                Assert.That(ok, Is.False);
                Assert.That(owner, Is.EqualTo(root.GetComponent<SourceRuntimeTemplateAuthoring>()));
                Assert.That(error, Does.Contain("ActivationDurationSec >= 0"));

                authoring.ActivationDurationSec = 0f;
                authoring.RetireDurationSec = -0.25f;

                ok = HazardActorAuthoringValidationUtility.TryValidate(authoring, out owner, out error);
                Assert.That(ok, Is.False);
                Assert.That(owner, Is.EqualTo(root.GetComponent<SourceRuntimeTemplateAuthoring>()));
                Assert.That(error, Does.Contain("RetireDurationSec >= 0"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardEmitterAuthoringValidation_RequiresParentActorAuthoring()
        {
            var root = new GameObject("hazard_emitter_root");
            var emitterGo = new GameObject("hazard_emitter");
            emitterGo.transform.SetParent(root.transform);
            var authoring = emitterGo.AddComponent<HazardEmitterAuthoring>();
            var telegraph = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            var emission = CreateEmissionProfile();
            authoring.Slots = CreatePatternSlots((1, telegraph, emission, 1f, 0u));

            try
            {
                bool ok = HazardEmitterAuthoringValidationUtility.TryValidate(authoring, out var actorAuthoring, out var sourceAuthoring, out var error);
                Assert.That(ok, Is.False);
                Assert.That(actorAuthoring, Is.Null);
                Assert.That(sourceAuthoring, Is.Null);
                Assert.That(error, Does.Contain("parent HazardActorAuthoring"));
            }
            finally
            {
                Object.DestroyImmediate(telegraph);
                Object.DestroyImmediate(emission.Bullet);
                Object.DestroyImmediate(emission);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardEmitterAuthoringValidation_RejectsUnsupportedPolicy()
        {
            var root = new GameObject("source_root");
            root.AddComponent<SourceRuntimeTemplateAuthoring>();
            var actorGo = new GameObject("hazard_actor");
            actorGo.transform.SetParent(root.transform);
            var actorAuthoring = actorGo.AddComponent<HazardActorAuthoring>();
            var emitterGo = new GameObject("hazard_emitter");
            emitterGo.transform.SetParent(actorGo.transform);
            var authoring = emitterGo.AddComponent<HazardEmitterAuthoring>();
            authoring.ActivationPolicy = HazardEmitterActivationPolicyId.ProgressReactive;
            var telegraph = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            var emission = CreateEmissionProfile();
            authoring.Slots = CreatePatternSlots((1, telegraph, emission, 1f, 0u));

            try
            {
                bool ok = HazardEmitterAuthoringValidationUtility.TryValidate(authoring, out var owner, out var sourceOwner, out var error);
                Assert.That(ok, Is.False);
                Assert.That(owner, Is.EqualTo(actorAuthoring));
                Assert.That(sourceOwner, Is.Not.Null);
                Assert.That(error, Does.Contain(nameof(HazardEmitterActivationPolicyId.AlwaysCycle)));
            }
            finally
            {
                Object.DestroyImmediate(telegraph);
                Object.DestroyImmediate(emission.Bullet);
                Object.DestroyImmediate(emission);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardEmitterAuthoringValidation_RejectsDuplicatePatternSlotIds()
        {
            var root = new GameObject("source_root");
            root.AddComponent<SourceRuntimeTemplateAuthoring>();
            var actorGo = new GameObject("hazard_actor");
            actorGo.transform.SetParent(root.transform);
            actorGo.AddComponent<HazardActorAuthoring>();
            var emitterGo = new GameObject("hazard_emitter");
            emitterGo.transform.SetParent(actorGo.transform);
            var authoring = emitterGo.AddComponent<HazardEmitterAuthoring>();
            var telegraphA = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            var emissionA = CreateEmissionProfile();
            var telegraphB = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            var emissionB = CreateEmissionProfile();
            authoring.Slots = CreatePatternSlots(
                (1, telegraphA, emissionA, 1f, 0u),
                (1, telegraphB, emissionB, 1f, 0u));

            try
            {
                bool ok = HazardEmitterAuthoringValidationUtility.TryValidate(authoring, out _, out _, out var error);
                Assert.That(ok, Is.False);
                Assert.That(error, Does.Contain("duplicate PatternSlotId"));
            }
            finally
            {
                Object.DestroyImmediate(telegraphA);
                Object.DestroyImmediate(emissionA.Bullet);
                Object.DestroyImmediate(emissionA);
                Object.DestroyImmediate(telegraphB);
                Object.DestroyImmediate(emissionB.Bullet);
                Object.DestroyImmediate(emissionB);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardActorPresence_DefaultImmediatePolicy_TransitionsHiddenToActive()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_DefaultImmediate", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7000);
            var actor = CreateActor(em, source, actorId: 7000);

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Active));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardActorPresence_ActivationDuration_KeepsActorInActivatingUntilElapsed()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_ActivationDuration", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.25f);

            var source = CreateSourceWithActiveCountBuffer(em, 7001);
            var actor = CreateActor(em, source, actorId: 7001);
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                ActivationDurationSec = 0.5f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });

            var system = world.GetOrCreateSystem<HazardActorPresenceSystem>();

            system.Update(world.Unmanaged);
            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Activating));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));

            system.Update(world.Unmanaged);
            runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Activating));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0.25f).Within(0.0001f));

            system.Update(world.Unmanaged);
            runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Active));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardActorPresence_SourceDepletedRetireTrigger_TransitionsActiveToHiddenThroughRetiring()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_SourceDepletedRetire", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.5f);

            var source = CreateSourceWithDirector(
                em,
                7002,
                RunDirectorSourceStateId.Baseline,
                0f,
                thresholdDepleted: 5,
                collectedCount: 5);
            var actor = CreateActor(em, source, actorId: 7002);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.SourceDepleted,
                RetireDurationSec = 0.5f,
            });

            var system = world.GetOrCreateSystem<HazardActorPresenceSystem>();

            system.Update(world.Unmanaged);
            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Retiring));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));

            system.Update(world.Unmanaged);
            runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardActorPresence_SourceAvailableActivation_DoesNotFireWhenSourceIsMissing()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_SourceAvailableMissingSource", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var actor = CreateActor(em, Entity.Null, actorId: 7003);
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.SourceAvailable,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardActorPresence_SourceOccupiedActivation_DoesNotFireWithoutOccupancyInput()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_SourceOccupiedMissingInput", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70031);
            var actor = CreateActor(em, source, actorId: 70031);
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.SourceOccupied,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardActorPresence_SourceOccupiedActivation_FiresWhenOccupancyInputIsOne()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_SourceOccupiedActivation", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70032);
            var pressureInputs = em.AddBuffer<SourceDirectorPressureInputBuffer>(source);
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceOccupancy,
                Value = 1f,
            });
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceHoldSec,
                Value = 0f,
            });

            var actor = CreateActor(em, source, actorId: 70032);
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.SourceOccupied,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Active));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardActorPresence_ZeroDurationActivation_EmitsActivationStartedSignalOnce()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_ActivationSignal", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70033);
            var actor = CreateActor(em, source, actorId: 70033);
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            var signal = em.GetComponentData<HazardActorPresencePresentationSignalComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Active));
            Assert.That(signal.Version, Is.EqualTo(1u));
            Assert.That(signal.Cue, Is.EqualTo(HazardActorPresencePresentationCueId.ActivationStarted));

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            signal = em.GetComponentData<HazardActorPresencePresentationSignalComponent>(actor);
            Assert.That(signal.Version, Is.EqualTo(1u));
            Assert.That(signal.Cue, Is.EqualTo(HazardActorPresencePresentationCueId.ActivationStarted));
        }

        [Test]
        public void HazardEmitterPatternSetCompatibility_CreateEmitter_SeedsSingleMirrorSlot()
        {
            using var world = CreateDefaultTestWorld("HazardEmitterPatternSetCompatibility_CreateEmitter", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7095);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7095,
                telegraphDurationSec: 0.25f,
                cooldownSec: 1.5f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: new float3(0.5f, 0f, -0.25f));

            var applied = em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitter);
            var slots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);

            Assert.That(slots.Length, Is.EqualTo(1));
            Assert.That(slots[0].PatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
            Assert.That(slots[0].TelegraphProfileRefId, Is.EqualTo(applied.TelegraphProfileRefId));
            Assert.That(slots[0].EmissionProfileRefId, Is.EqualTo(applied.EmissionProfileRefId));
            Assert.That(slots[0].BaseWeight, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityBaseWeight));
            Assert.That(slots[0].AvailabilityFlags, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityAvailabilityFlags));
        }

        [Test]
        public void HazardEmitterEmitBuild_AlwaysCycle_ZeroTelegraph_AppendsHazardEmitterDiscreteRequest()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_ZeroTelegraph", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 701);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 701,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: new float3(3f, 0f, 4f),
                localOffset: new float3(1f, 0f, -2f));

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
            CreateFrameCounter(em, 2u);
            world.SetTime(new TimeData(2d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            var requests = GetDiscreteEmitRequests(em);
            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].ProducerKind, Is.EqualTo(DiscreteEmitProducerKind.HazardEmitter));
            Assert.That(requests[0].SourceEntity, Is.EqualTo(source));
            Assert.That(requests[0].ProducerEntity, Is.EqualTo(emitter));
            Assert.That(requests[0].AnchorMode, Is.EqualTo(DiscreteEmitAnchorMode.FixedWorld));
            Assert.That(requests[0].AnchorPosition.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(requests[0].AnchorPosition.z, Is.EqualTo(2f).Within(0.0001f));

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));
        }

        [Test]
        public void HazardEmitterEmitBuild_CooldownBlocksAdditionalAppends_AndDisabledStaysDormant()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Cooldown", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.5f);

            var source = CreateSourceWithActiveCountBuffer(em, 702);
            var activeEmitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 702,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var disabledEmitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 702,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: false,
                isSuppressed: false,
                position: new float3(1f, 0f, 1f),
                localOffset: float3.zero);

            var system = world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>();
            var coordinatorSystem = world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>();
            var selectorSystem = world.GetOrCreateSystem<HazardActorPatternSelectorSystem>();

            world.SetTime(new TimeData(0.5d, 0.5f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            system.Update(world.Unmanaged);
            CreateFrameCounter(em, 2u);
            world.SetTime(new TimeData(1d, 0.5f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            system.Update(world.Unmanaged);
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(1));

            CreateFrameCounter(em, 3u);
            world.SetTime(new TimeData(1.5d, 0.5f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            system.Update(world.Unmanaged);
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(1));

            CreateFrameCounter(em, 4u);
            world.SetTime(new TimeData(2d, 0.5f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            system.Update(world.Unmanaged);
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(2));

            var disabledState = em.GetComponentData<HazardEmitterRuntimeStateComponent>(disabledEmitter);
            Assert.That(disabledState.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));

            var activeState = em.GetComponentData<HazardEmitterRuntimeStateComponent>(activeEmitter);
            Assert.That(activeState.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));
        }

        [Test]
        public void HazardEmitterDiscreteEmit_ConsumesThroughExecutionSystem()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Consume", out _);
            var em = world.EntityManager;

            InitializeSharedContainers();
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);
            var channel = GetDiscreteChannel(em, budgetPerFrame: 8, maxPendingCount: 16, maxPendingAgeFrames: 120u);
            var source = CreateSourceWithActiveCountBuffer(em, 703);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 703,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var pooledBullet = CreatePooledBullet(em, 703, 5f, 7f);
            BulletFieldShared.FreeByKey.Add(703, pooledBullet);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
            CreateFrameCounter(em, 2u);
            world.SetTime(new TimeData(2d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletActiveTag>(pooledBullet), Is.True);
            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(source)[0].ActiveCount, Is.EqualTo(1));
            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(0));
            Assert.That(em.GetComponentData<DiscreteEmitBacklogMetricsComponent>(channel).LastFrameBudgetUsed, Is.EqualTo(1));

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));
        }

        [Test]
        public void HazardEmitterCoordinator_NoGates_AllowsEnabledEmitter()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_NoGates", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 704);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 704,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(1));
            Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
            Assert.That(coordinator.LastPlayerDistanceSq, Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void HazardEmitterCoordinator_ActorAppliedConfigFlags_BlockActivation()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_ActorAppliedFlags", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 705);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 705,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 0,
                IsSuppressed = 1,
            });

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.DisabledByActorConfig, Is.Not.EqualTo(0u));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.SuppressedByActorConfig, Is.Not.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterCoordinator_MissingActor_BlocksActivation()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_MissingActor", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7051);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7051,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var emitterConfig = em.GetComponentData<HazardEmitterComponent>(emitter);
            emitterConfig.ActorEntity = Entity.Null;
            em.SetComponentData(emitter, emitterConfig);

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo((uint)HazardEmitterSuppressionReasonFlags.MissingActor));
        }

        [Test]
        public void HazardEmitterCoordinator_HiddenPresenceState_BlocksActivation()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_HiddenPresenceBlocked", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7052);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7052,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Hidden,
                StateElapsedSec = 5f,
            });

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceHidden, Is.Not.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterCoordinator_ActivatingPresenceState_BlocksActivation()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_ActivatingPresenceBlocked", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7053);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7053,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Activating,
                StateElapsedSec = 0.1f,
            });

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceActivating, Is.Not.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterCoordinator_RetiringPresenceState_BlocksActivation()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_RetiringPresenceBlocked", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7054);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7054,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Retiring,
                StateElapsedSec = 0.1f,
            });

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceRetiring, Is.Not.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterCoordinator_ActivePresenceState_AllowsExistingGatePath()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_ActivePresenceAllows", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7055);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7055,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(1));
            Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterCoordinator_AppliedConfigFlags_BlockActivation()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_AppliedFlags", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 705);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 705,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: false,
                isSuppressed: true,
                position: float3.zero,
                localOffset: float3.zero);

            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.DisabledByAppliedConfig, Is.Not.EqualTo(0u));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.SuppressedByAppliedConfig, Is.Not.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterCoordinator_PressureGate_RequiresPressureAndHold()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_PressureGate", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithDirector(em, 706, RunDirectorSourceStateId.Baseline, 0.1f, thresholdDepleted: 10, collectedCount: 0);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 706,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            em.AddComponentData(emitter, new HazardEmitterSourcePressureGateComponent
            {
                Enabled = 1,
                RequirePressureState = 1,
                MinPressureOccupancySec = 0.25f,
            });

            var system = world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>();

            system.Update(world.Unmanaged);
            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.SourcePressureBlocked, Is.Not.EqualTo(0u));

            em.SetComponentData(source, new SourceRunDirectorStateComponent
            {
                State = RunDirectorSourceStateId.Pressure,
                SelectedClipState = SourceStateId.Normal,
                PressureOccupancySec = 0.3f,
                DensityScale = 1f,
                Version = 1u,
            });

            system.Update(world.Unmanaged);
            coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(1));
            Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterCoordinator_PlayerDistanceGate_UsesPlayerPositionAndMissingPlayerReason()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_DistanceGate", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 707);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 707,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: new float3(1f, 0f, 1f),
                localOffset: float3.zero);
            em.AddComponentData(emitter, new HazardEmitterPlayerDistanceGateComponent
            {
                Enabled = 1,
                MinDistanceSq = 0f,
                MaxDistanceSq = 4f,
            });

            var system = world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>();

            system.Update(world.Unmanaged);
            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.MissingPlayer, Is.Not.EqualTo(0u));
            Assert.That(coordinator.LastPlayerDistanceSq, Is.EqualTo(float.MaxValue));

            CreatePlayer(em, new float3(2f, 0f, 1f));
            system.Update(world.Unmanaged);
            coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(1));
            Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
            Assert.That(coordinator.LastPlayerDistanceSq, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void HazardEmitterCoordinator_SourceProgressGate_UsesNormalizedProgressAndSafeThreshold()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_Coordinator_ProgressGate", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithDirector(em, 708, RunDirectorSourceStateId.Baseline, 0f, thresholdDepleted: 0, collectedCount: 0);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 708,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            em.AddComponentData(emitter, new HazardEmitterSourceProgressGateComponent
            {
                Enabled = 1,
                MinProgress01 = 0.5f,
                MaxProgress01 = 1f,
            });

            var system = world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>();

            system.Update(world.Unmanaged);
            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.SourceProgressBlocked, Is.Not.EqualTo(0u));

            var sourceData = em.GetComponentData<SourceSpawnComponent>(source);
            sourceData.CollectedCount = 1;
            em.SetComponentData(source, sourceData);

            system.Update(world.Unmanaged);
            coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(1));
            Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterEmitBuild_BlockedCoordinatorState_StaysDormantAndSkipsAppend()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_BlockedCoordinator", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 709);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 709,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            em.SetComponentData(emitter, new HazardEmitterCoordinatorStateComponent
            {
                ActivationAllowed = 0,
                SuppressionReasonMask = (uint)HazardEmitterSuppressionReasonFlags.GroupSuppressed,
                LastPlayerDistanceSq = float.MaxValue,
            });

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(0));
            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardEmitterEmitBuild_DisabledActor_StaysDormantAndSkipsAppend()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_DisabledActor", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7091);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7091,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 0,
                IsSuppressed = 0,
            });

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.DisabledByActorConfig, Is.Not.EqualTo(0u));
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(0));

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardActorPresence_DisabledActor_ClampsToHiddenAndResetsSelector()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_DisabledActorClamp", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7092);
            var actor = CreateActor(em, source, actorId: 7092);
            em.SetComponentData(actor, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 0,
                IsSuppressed = 0,
            });
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 3f,
            });
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 4,
                CurrentPatternSlotId = 5,
                LastPatternSlotId = 6,
                SelectionSequence = 7u,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.TargetEmitterId, Is.EqualTo(-1));
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.LastPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.SelectionSequence, Is.EqualTo(0u));
        }

        [Test]
        public void HazardActorPresence_DisabledActorClamp_DoesNotBumpPresentationSignalVersion()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_DisabledClampNoSignal", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70921);
            var actor = CreateActor(em, source, actorId: 70921);
            em.SetComponentData(actor, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 0,
                IsSuppressed = 0,
            });
            em.SetComponentData(actor, new HazardActorPresencePresentationSignalComponent
            {
                Version = 3u,
                Cue = HazardActorPresencePresentationCueId.ActivationStarted,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var signal = em.GetComponentData<HazardActorPresencePresentationSignalComponent>(actor);
            Assert.That(signal.Version, Is.EqualTo(3u));
            Assert.That(signal.Cue, Is.EqualTo(HazardActorPresencePresentationCueId.ActivationStarted));
        }

        [Test]
        public void HazardActorPresence_NonActiveState_ResetsSelector()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_NonActiveSelectorReset", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.1f);

            var source = CreateSourceWithActiveCountBuffer(em, 7093);
            var actor = CreateActor(em, source, actorId: 7093);
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                ActivationDurationSec = 1f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 1,
                CurrentPatternSlotId = 2,
                LastPatternSlotId = 3,
                SelectionSequence = 4u,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Activating));

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.TargetEmitterId, Is.EqualTo(-1));
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.LastPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.SelectionSequence, Is.EqualTo(0u));
        }

        [Test]
        public void HazardActorPresence_SourceDepletedRetireTrigger_EmitsRetireStartedSignalOnce()
        {
            using var world = CreateDefaultTestWorld("HazardActorPresence_RetireSignal", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.5f);

            var source = CreateSourceWithDirector(
                em,
                70931,
                RunDirectorSourceStateId.Baseline,
                0f,
                thresholdDepleted: 5,
                collectedCount: 5);
            var actor = CreateActor(em, source, actorId: 70931);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.None,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.SourceDepleted,
                RetireDurationSec = 0f,
            });

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            var signal = em.GetComponentData<HazardActorPresencePresentationSignalComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
            Assert.That(signal.Version, Is.EqualTo(1u));
            Assert.That(signal.Cue, Is.EqualTo(HazardActorPresencePresentationCueId.RetireStarted));

            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);

            signal = em.GetComponentData<HazardActorPresencePresentationSignalComponent>(actor);
            Assert.That(signal.Version, Is.EqualTo(1u));
            Assert.That(signal.Cue, Is.EqualTo(HazardActorPresencePresentationCueId.RetireStarted));
        }

        [Test]
        public void HazardActorPatternSelector_ActiveAllowedEmitter_SelectsCompatibilitySlot()
        {
            using var world = CreateDefaultTestWorld("HazardActorPatternSelector_SelectsCompatibilitySlot", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70941);
            var actor = CreateActor(em, source, actorId: 70941);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });

            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70941,
                emitterId: 11,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.TargetEmitterId, Is.EqualTo(11));
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
            Assert.That(selector.LastPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.SelectionSequence, Is.EqualTo(1u));
        }

        [Test]
        public void HazardActorPatternSelector_SameSelection_DoesNotIncrementSequence()
        {
            using var world = CreateDefaultTestWorld("HazardActorPatternSelector_SameSelectionStable", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70942);
            var actor = CreateActor(em, source, actorId: 70942);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });

            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70942,
                emitterId: 9,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            var selectorSystem = world.GetOrCreateSystem<HazardActorPatternSelectorSystem>();
            selectorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.TargetEmitterId, Is.EqualTo(9));
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
            Assert.That(selector.SelectionSequence, Is.EqualTo(1u));
        }

        [Test]
        public void HazardActorPatternSelector_MultipleEligibleEmitters_SelectsLowestEmitterId()
        {
            using var world = CreateDefaultTestWorld("HazardActorPatternSelector_LowestEmitterId", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70943);
            var actor = CreateActor(em, source, actorId: 70943);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });

            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70943,
                emitterId: 22,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: new float3(1f, 0f, 0f),
                localOffset: float3.zero);
            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70943,
                emitterId: 3,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: new float3(-1f, 0f, 0f),
                localOffset: float3.zero);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.TargetEmitterId, Is.EqualTo(3));
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
        }

        [Test]
        public void HazardActorPatternSelector_NoEligibleEmitter_ClearsCurrentAndPreservesRecentLast()
        {
            using var world = CreateDefaultTestWorld("HazardActorPatternSelector_NoEligibleClearsCurrent", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70944);
            var actor = CreateActor(em, source, actorId: 70944);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 7,
                CurrentPatternSlotId = 1,
                LastPatternSlotId = 99,
                SelectionSequence = 4u,
            });

            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70944,
                emitterId: 7,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: false,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.TargetEmitterId, Is.EqualTo(-1));
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.LastPatternSlotId, Is.EqualTo(1));
            Assert.That(selector.SelectionSequence, Is.EqualTo(5u));
        }

        [Test]
        public void HazardActorPatternSelector_NonActiveActor_DoesNotOverwritePresenceResetResult()
        {
            using var world = CreateDefaultTestWorld("HazardActorPatternSelector_NonActiveNoOp", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.25f);

            var source = CreateSourceWithActiveCountBuffer(em, 70945);
            var actor = CreateActor(em, source, actorId: 70945);
            em.SetComponentData(actor, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                ActivationDurationSec = 1f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 5,
                CurrentPatternSlotId = 6,
                LastPatternSlotId = 7,
                SelectionSequence = 8u,
            });

            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70945,
                emitterId: 5,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);

            world.SetTime(new TimeData(0.25d, 0.25f));
            world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
            Assert.That(runtime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Activating));

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.TargetEmitterId, Is.EqualTo(-1));
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.LastPatternSlotId, Is.EqualTo(-1));
            Assert.That(selector.SelectionSequence, Is.EqualTo(0u));
        }

        [Test]
        public void HazardEmitterEmitBuild_HiddenActorPresence_StaysDormantAndSkipsAppend()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_HiddenPresence", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 7094);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 7094,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Hidden,
                StateElapsedSec = 0f,
            });

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
            Assert.That(coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceHidden, Is.Not.EqualTo(0u));
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(0));

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardEmitterEmitBuild_NonSelectedEligibleEmitter_StaysDormantAndSkipsAppend()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_NonSelectedEligible", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70946);
            var actor = CreateActor(em, source, actorId: 70946);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });

            var lowerEmitter = CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70946,
                emitterId: 4,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: new float3(-1f, 0f, 0f),
                localOffset: float3.zero);
            var higherEmitter = CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70946,
                emitterId: 8,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: new float3(1f, 0f, 0f),
                localOffset: float3.zero);

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
            CreateFrameCounter(em, 2u);
            world.SetTime(new TimeData(2d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            var requests = GetDiscreteEmitRequests(em);
            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].ProducerEntity, Is.EqualTo(lowerEmitter));

            var nonSelectedRuntime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(higherEmitter);
            Assert.That(nonSelectedRuntime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(nonSelectedRuntime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardEmitterEmitBuild_InvalidSelectorState_BlocksEvenWhenCoordinatorAllows()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_InvalidSelectorState", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70947);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 70947,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = -1,
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                SelectionSequence = 0u,
            });

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
            Assert.That(coordinator.ActivationAllowed, Is.EqualTo(1));
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(0));

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardEmitterEmitBuild_MissingSelectedSlot_BlocksAndForcesDormant()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_MissingSelectedSlot", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 70948);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 70948,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 1,
                CurrentPatternSlotId = 99,
                LastPatternSlotId = -1,
                SelectionSequence = 1u,
            });

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(0));
            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardEmitterEmitBuild_SelectedSlotExecutionCutover_AppliesSnapshotAndResetsDormant()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_SelectedSlotCutover", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 1f / 60f);

            var source = CreateSourceWithActiveCountBuffer(em, 709481);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 709481,
                telegraphDurationSec: 0.1f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            ReplaceEmitterPatternSlots(
                em,
                emitter,
                CreateExecutionSlot(em, emitter, 1, telegraphProfileRefId: 1, emissionProfileRefId: 1, telegraphDurationSec: 0.1f, bulletTypeKey: 709481, cooldownSec: 1f, baseAngleDeg: 0f),
                CreateExecutionSlot(em, emitter, 2, telegraphProfileRefId: 2, emissionProfileRefId: 2, telegraphDurationSec: 0.6f, bulletTypeKey: 709482, cooldownSec: 2f, baseAngleDeg: 35f));
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 1,
                CurrentPatternSlotId = 2,
                LastPatternSlotId = -1,
                SelectionSequence = 1u,
            });

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);

            var telegraph = em.GetComponentData<HazardEmitterTelegraphProfileComponent>(emitter);
            var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitter);
            var selectedPattern = em.GetComponentData<HazardEmitterSelectedPatternRuntimeComponent>(emitter);
            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);

            Assert.That(telegraph.ProfileId, Is.EqualTo(2));
            Assert.That(telegraph.TelegraphDurationSec, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(emission.ProfileId, Is.EqualTo(2));
            Assert.That(emission.BulletTypeKey, Is.EqualTo(709482));
            Assert.That(emission.BaseAngleDeg, Is.EqualTo(35f).Within(0.0001f));
            Assert.That(emission.CooldownSec, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(selectedPattern.AppliedPatternSlotId, Is.EqualTo(2));
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
            Assert.That(GetDiscreteEmitRequests(em).Length, Is.EqualTo(0));
        }

        [Test]
        public void HazardEmitterEmitBuild_SameEmitterSlotChange_DuringCooldown_ForcesImmediateDormant()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_SameEmitterSlotChange", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.1f);

            var source = CreateSourceWithActiveCountBuffer(em, 709482);
            var emitter = CreateEmitter(
                em,
                source,
                bulletTypeKey: 709482,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            var actor = em.GetComponentData<HazardEmitterComponent>(emitter).ActorEntity;
            ReplaceEmitterPatternSlots(
                em,
                emitter,
                CreateExecutionSlot(em, emitter, 1, telegraphProfileRefId: 1, emissionProfileRefId: 1, telegraphDurationSec: 0f, bulletTypeKey: 709482, cooldownSec: 1f, baseAngleDeg: 0f),
                CreateExecutionSlot(em, emitter, 2, telegraphProfileRefId: 2, emissionProfileRefId: 2, telegraphDurationSec: 0.25f, bulletTypeKey: 709483, cooldownSec: 2f, baseAngleDeg: 55f));
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 1,
                CurrentPatternSlotId = 1,
                LastPatternSlotId = -1,
                SelectionSequence = 1u,
            });

            var coordinatorSystem = world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>();
            var emitSystem = world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>();

            world.SetTime(new TimeData(0.1d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged); // apply slot 1 snapshot, dormant

            world.SetTime(new TimeData(0.2d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged); // start cycle for slot 1 -> cooldown

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));

            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = 1,
                CurrentPatternSlotId = 2,
                LastPatternSlotId = 1,
                SelectionSequence = 2u,
            });

            world.SetTime(new TimeData(0.3d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged);

            var selectedPattern = em.GetComponentData<HazardEmitterSelectedPatternRuntimeComponent>(emitter);
            var telegraph = em.GetComponentData<HazardEmitterTelegraphProfileComponent>(emitter);
            var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitter);
            runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);

            Assert.That(selectedPattern.AppliedPatternSlotId, Is.EqualTo(2));
            Assert.That(telegraph.ProfileId, Is.EqualTo(2));
            Assert.That(emission.ProfileId, Is.EqualTo(2));
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardEmitterEmitBuild_DeselectionDuringTelegraph_ForcesImmediateDormant()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_DeselectTelegraph", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.1f);

            var source = CreateSourceWithActiveCountBuffer(em, 70949);
            var actor = CreateActor(em, source, actorId: 70949);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });

            var selectedEmitter = CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70949,
                emitterId: 2,
                telegraphDurationSec: 0.5f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70949,
                emitterId: 1,
                telegraphDurationSec: 0.5f,
                cooldownSec: 1f,
                isEnabled: false,
                isSuppressed: false,
                position: new float3(1f, 0f, 0f),
                localOffset: float3.zero);

            var coordinatorSystem = world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>();
            var selectorSystem = world.GetOrCreateSystem<HazardActorPatternSelectorSystem>();
            var emitSystem = world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>();

            world.SetTime(new TimeData(0.1d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged);
            CreateFrameCounter(em, 2u);
            world.SetTime(new TimeData(0.2d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(selectedEmitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Telegraph));

            var lowerEmitter = FindActorEmitterById(em, actor, 1);
            var lowerApplied = em.GetComponentData<HazardEmitterAppliedConfigComponent>(lowerEmitter);
            lowerApplied.IsEnabled = 1;
            em.SetComponentData(lowerEmitter, lowerApplied);

            world.SetTime(new TimeData(0.3d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged);

            runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(selectedEmitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        [Test]
        public void HazardEmitterEmitBuild_DeselectionDuringCooldown_ForcesImmediateDormant()
        {
            using var world = CreateDefaultTestWorld("HazardEmitter_EmitBuild_DeselectCooldown", out _);
            var em = world.EntityManager;
            InitializeBuildWorld(em, RunDirectorStageStateId.Running, 0.1f);

            var source = CreateSourceWithActiveCountBuffer(em, 70950);
            var actor = CreateActor(em, source, actorId: 70950);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });

            var selectedEmitter = CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70950,
                emitterId: 2,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: true,
                isSuppressed: false,
                position: float3.zero,
                localOffset: float3.zero);
            CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey: 70950,
                emitterId: 1,
                telegraphDurationSec: 0f,
                cooldownSec: 1f,
                isEnabled: false,
                isSuppressed: false,
                position: new float3(1f, 0f, 0f),
                localOffset: float3.zero);

            var coordinatorSystem = world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>();
            var selectorSystem = world.GetOrCreateSystem<HazardActorPatternSelectorSystem>();
            var emitSystem = world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>();

            world.SetTime(new TimeData(0.1d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged);
            CreateFrameCounter(em, 2u);
            world.SetTime(new TimeData(0.2d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged);

            var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(selectedEmitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Cooldown));

            var lowerEmitter = FindActorEmitterById(em, actor, 1);
            var lowerApplied = em.GetComponentData<HazardEmitterAppliedConfigComponent>(lowerEmitter);
            lowerApplied.IsEnabled = 1;
            em.SetComponentData(lowerEmitter, lowerApplied);

            world.SetTime(new TimeData(0.3d, 0.1f));
            coordinatorSystem.Update(world.Unmanaged);
            selectorSystem.Update(world.Unmanaged);
            emitSystem.Update(world.Unmanaged);

            runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(selectedEmitter);
            Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
            Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
        }

        private static HazardEmitterEmissionProfileSO CreateEmissionProfile()
        {
            var bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            bullet.Editor_SetDefinitionId(9001);
            var profile = ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();
            profile.Bullet = bullet;
            profile.PositionPattern = new SinglePointPositionPatternAuthoring();
            profile.Aim = new FixedAimAuthoring { BaseAngleDeg = 90f };
            profile.ShotPattern = new SingleShotPatternAuthoring();
            profile.EventRepeatCount = 1;
            profile.EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
            profile.EventShotIntervalSec = 0f;
            profile.CooldownSec = 1f;
            return profile;
        }

        private static World CreateDefaultTestWorld(string worldName, out SimulationSystemGroup simGroup)
        {
            var world = new World(worldName);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null);
            return world;
        }

        private static void InitializeBuildWorld(EntityManager em, RunDirectorStageStateId stageState, float deltaTime)
        {
            CreateFrameCounter(em, 1u);

            var discreteEntity = GetDiscreteChannel(em, budgetPerFrame: 256, maxPendingCount: 8192, maxPendingAgeFrames: 120u);
            em.SetComponentData(discreteEntity, default(DiscreteEmitBacklogMetricsComponent));

            var tickEntity = GetOrCreateSingletonEntity<FixedTickStepRuntimeComponent>(em);
            em.SetComponentData(tickEntity, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = deltaTime,
                LogicDeltaTime = deltaTime,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });

            var stageEntity = GetOrCreateSingletonEntity<RunDirectorStageStateComponent>(em);
            em.SetComponentData(stageEntity, new RunDirectorStageStateComponent
            {
                State = stageState,
                StateElapsedSec = 0f,
            });
        }

        private static void CreateFrameCounter(EntityManager em, uint frame)
        {
            var entity = GetOrCreateSingletonEntity<BulletFrameCounterComponent>(em);
            em.SetComponentData(entity, new BulletFrameCounterComponent { Value = frame });
        }

        private static Entity GetDiscreteChannel(EntityManager em, int budgetPerFrame, int maxPendingCount, uint maxPendingAgeFrames)
        {
            var entity = GetOrCreateSingletonEntity<DiscreteEmitChannelSingletonTag>(em);
            if (!em.HasBuffer<DiscreteEmitRequestBuffer>(entity))
                em.AddBuffer<DiscreteEmitRequestBuffer>(entity);
            if (!em.HasComponent<DiscreteEmitPolicyComponent>(entity))
                em.AddComponentData(entity, new DiscreteEmitPolicyComponent());
            if (!em.HasComponent<DiscreteEmitBacklogMetricsComponent>(entity))
                em.AddComponentData(entity, default(DiscreteEmitBacklogMetricsComponent));

            em.SetComponentData(entity, new DiscreteEmitPolicyComponent
            {
                BudgetPerFrame = budgetPerFrame,
                MaxPendingCount = maxPendingCount,
                MaxPendingAgeFrames = maxPendingAgeFrames,
            });
            return entity;
        }

        private static Entity CreateSourceWithActiveCountBuffer(EntityManager em, int bulletTypeKey)
        {
            var entity = em.CreateEntity();
            var counts = em.AddBuffer<SourceActiveBulletCountBuffer>(entity);
            counts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = bulletTypeKey,
                ActiveCount = 0,
            });
            return entity;
        }

        private static Entity CreateEmitter(
            EntityManager em,
            Entity source,
            int bulletTypeKey,
            float telegraphDurationSec,
            float cooldownSec,
            bool isEnabled,
            bool isSuppressed,
            float3 position,
            float3 localOffset)
        {
            var actor = CreateActor(em, source, actorId: bulletTypeKey);
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });
            return CreateEmitterForActor(
                em,
                actor,
                bulletTypeKey,
                emitterId: 1,
                telegraphDurationSec,
                cooldownSec,
                isEnabled,
                isSuppressed,
                position,
                localOffset);
        }

        private static Entity CreateEmitterForActor(
            EntityManager em,
            Entity actor,
            int bulletTypeKey,
            int emitterId,
            float telegraphDurationSec,
            float cooldownSec,
            bool isEnabled,
            bool isSuppressed,
            float3 position,
            float3 localOffset)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(HazardEmitterComponent),
                typeof(HazardEmitterAppliedConfigBaselineComponent),
                typeof(HazardEmitterAppliedConfigComponent),
                typeof(HazardEmitterTelegraphProfileBaselineComponent),
                typeof(HazardEmitterTelegraphProfileComponent),
                typeof(HazardEmitterEmissionProfileBaselineComponent),
                typeof(HazardEmitterEmissionProfileComponent),
                typeof(HazardEmitterRuntimeStateComponent),
                typeof(HazardEmitterSelectedPatternRuntimeComponent),
                typeof(HazardEmitterCoordinatorStateComponent));

            em.SetComponentData(entity, LocalTransform.FromPosition(position));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            em.SetComponentData(entity, new HazardEmitterComponent
            {
                EmitterId = emitterId,
                ActorEntity = actor,
                ActivationPolicy = HazardEmitterActivationPolicyId.AlwaysCycle,
                InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                AnchorKind = HazardEmitterAnchorKindId.ObjectBound,
                Mobility = HazardEmitterMobilityId.Static,
            });
            em.SetComponentData(entity, new HazardEmitterAppliedConfigBaselineComponent
            {
                IsEnabled = isEnabled ? (byte)1 : (byte)0,
                IsSuppressed = isSuppressed ? (byte)1 : (byte)0,
                LocalOffset = localOffset,
                TelegraphProfileRefId = 1,
                EmissionProfileRefId = 1,
            });
            em.SetComponentData(entity, new HazardEmitterAppliedConfigComponent
            {
                IsEnabled = isEnabled ? (byte)1 : (byte)0,
                IsSuppressed = isSuppressed ? (byte)1 : (byte)0,
                LocalOffset = localOffset,
                TelegraphProfileRefId = 1,
                EmissionProfileRefId = 1,
            });
            em.SetComponentData(entity, new HazardEmitterTelegraphProfileBaselineComponent
            {
                ProfileId = 1,
                TelegraphDurationSec = telegraphDurationSec,
            });
            em.SetComponentData(entity, new HazardEmitterTelegraphProfileComponent
            {
                ProfileId = 1,
                TelegraphDurationSec = telegraphDurationSec,
            });
            em.SetComponentData(entity, new HazardEmitterEmissionProfileBaselineComponent
            {
                ProfileId = 1,
                BulletTypeKey = bulletTypeKey,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                EventRepeatCount = 1,
                CooldownSec = cooldownSec,
            });
            em.SetComponentData(entity, new HazardEmitterEmissionProfileComponent
            {
                ProfileId = 1,
                BulletTypeKey = bulletTypeKey,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                EventRepeatCount = 1,
                CooldownSec = cooldownSec,
            });
            em.SetComponentData(entity, new HazardEmitterRuntimeStateComponent
            {
                LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(entity, new HazardEmitterSelectedPatternRuntimeComponent
            {
                AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId,
            });
            em.SetComponentData(entity, new HazardEmitterCoordinatorStateComponent
            {
                ActivationAllowed = 0,
                SuppressionReasonMask = 0u,
                LastPlayerDistanceSq = float.MaxValue,
            });
            em.AddBuffer<HazardEmitterPatternSlotBuffer>(entity);
            em.AddBuffer<HazardEmitterPatternExecutionSlotBuffer>(entity);
            var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(entity);
            var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(entity);
            var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(entity);
            HazardEmitterPatternSetCompatibilityUtility.ReseedSingleCompatibilitySlot(
                ref patternSlots,
                ref executionSlots,
                telegraphProfileRefId: 1,
                telegraphDurationSec,
                in emission);
            var emitterRefs = em.GetBuffer<HazardActorEmitterRefBuffer>(actor);
            emitterRefs.Add(new HazardActorEmitterRefBuffer
            {
                EmitterEntity = entity,
                EmitterId = emitterId,
            });
            return entity;
        }

        private static Entity CreateActor(EntityManager em, Entity source, int actorId)
        {
            var entity = em.CreateEntity(
                typeof(HazardActorComponent),
                typeof(HazardActorAppliedConfigBaselineComponent),
                typeof(HazardActorAppliedConfigComponent),
                typeof(HazardActorPresencePolicyComponent),
                typeof(HazardActorRuntimeBaselineComponent),
                typeof(HazardActorRuntimeStateComponent),
                typeof(HazardActorPatternSelectorStateComponent),
                typeof(HazardActorPresencePresentationSignalComponent));

            em.SetComponentData(entity, new HazardActorComponent
            {
                ActorId = actorId,
                SourceEntity = source,
            });
            em.SetComponentData(entity, new HazardActorAppliedConfigBaselineComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(entity, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(entity, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });
            em.SetComponentData(entity, new HazardActorRuntimeBaselineComponent
            {
                InitialPresenceState = HazardActorPresenceStateId.Hidden,
            });
            em.SetComponentData(entity, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Hidden,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(entity, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = -1,
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                SelectionSequence = 0u,
            });
            em.SetComponentData(entity, new HazardActorPresencePresentationSignalComponent
            {
                Version = 0u,
                Cue = HazardActorPresencePresentationCueId.None,
            });
            em.AddBuffer<HazardActorEmitterRefBuffer>(entity);
            return entity;
        }

        private static Entity FindActorEmitterById(EntityManager em, Entity actor, int emitterId)
        {
            var emitterRefs = em.GetBuffer<HazardActorEmitterRefBuffer>(actor);
            for (int i = 0; i < emitterRefs.Length; i++)
            {
                if (emitterRefs[i].EmitterId == emitterId)
                    return emitterRefs[i].EmitterEntity;
            }

            return Entity.Null;
        }

        private static HazardEmitterPatternSlotAuthoring[] CreatePatternSlots(
            params (int patternSlotId, HazardEmitterTelegraphProfileSO telegraph, HazardEmitterEmissionProfileSO emission, float baseWeight, uint availabilityFlags)[] slots)
        {
            var result = new HazardEmitterPatternSlotAuthoring[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                result[i] = new HazardEmitterPatternSlotAuthoring
                {
                    PatternSlotId = slots[i].patternSlotId,
                    TelegraphProfile = slots[i].telegraph,
                    EmissionProfile = slots[i].emission,
                    BaseWeight = slots[i].baseWeight,
                    AvailabilityFlags = slots[i].availabilityFlags,
                };
            }

            return result;
        }

        private static void ReplaceEmitterPatternSlots(
            EntityManager em,
            Entity emitter,
            params HazardEmitterPatternExecutionSlotBuffer[] executionSlots)
        {
            var metadataSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
            var runtimeExecutionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
            metadataSlots.Clear();
            runtimeExecutionSlots.Clear();

            for (int i = 0; i < executionSlots.Length; i++)
            {
                metadataSlots.Add(new HazardEmitterPatternSlotBuffer
                {
                    PatternSlotId = executionSlots[i].PatternSlotId,
                    TelegraphProfileRefId = executionSlots[i].TelegraphProfileRefId,
                    EmissionProfileRefId = executionSlots[i].EmissionProfileRefId,
                    BaseWeight = 1f,
                    AvailabilityFlags = 0u,
                });
                runtimeExecutionSlots.Add(executionSlots[i]);
            }
        }

        private static HazardEmitterPatternExecutionSlotBuffer CreateExecutionSlot(
            EntityManager em,
            Entity emitter,
            int patternSlotId,
            int telegraphProfileRefId,
            int emissionProfileRefId,
            float telegraphDurationSec,
            int bulletTypeKey,
            float cooldownSec,
            float baseAngleDeg)
        {
            var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitter);
            emission.ProfileId = emissionProfileRefId;
            emission.BulletTypeKey = bulletTypeKey;
            emission.BaseAngleDeg = baseAngleDeg;
            emission.CooldownSec = cooldownSec;
            return HazardEmitterPatternSetCompatibilityUtility.CreateExecutionSlot(
                patternSlotId,
                telegraphProfileRefId,
                telegraphDurationSec,
                in emission);
        }

        private static Entity CreateSourceWithDirector(
            EntityManager em,
            int bulletTypeKey,
            RunDirectorSourceStateId directorState,
            float pressureOccupancySec,
            int thresholdDepleted,
            int collectedCount)
        {
            var entity = CreateSourceWithActiveCountBuffer(em, bulletTypeKey);
            em.AddComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 0,
                ThresholdDepleted = thresholdDepleted,
                CollectedCount = collectedCount,
                State = SourceStateId.Normal,
            });
            em.AddComponentData(entity, new SourceRunDirectorStateComponent
            {
                State = directorState,
                SelectedClipState = SourceStateId.Normal,
                PressureOccupancySec = pressureOccupancySec,
                DensityScale = 1f,
                Version = 1u,
            });
            return entity;
        }

        private static Entity CreatePlayer(EntityManager em, float3 position)
        {
            var player = em.CreateEntity(typeof(PlayerTag), typeof(PlayerGoSyncComponent));
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = position,
                Rotation = quaternion.identity,
                SyncRotation = 1,
            });
            return player;
        }

        private static Entity CreatePooledBullet(EntityManager em, int bulletTypeKey, float speed, float lifetime)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(BulletVelocityComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletSpeedComponent),
                typeof(BulletLifetimeMaxComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletLifecycleTraceComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(entity, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 0f });
            em.SetComponentData(entity, new BulletSpeedComponent { Value = speed });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, default(BulletLifecycleRequestComponent));
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = bulletTypeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(entity, default(BulletLifecycleTraceComponent));
            em.SetComponentEnabled<BulletActiveTag>(entity, false);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, false);
            return entity;
        }

        private static DynamicBuffer<DiscreteEmitRequestBuffer> GetDiscreteEmitRequests(EntityManager em)
        {
            var channel = em.CreateEntityQuery(ComponentType.ReadOnly<DiscreteEmitChannelSingletonTag>()).GetSingletonEntity();
            return em.GetBuffer<DiscreteEmitRequestBuffer>(channel);
        }

        private static Entity GetOrCreateSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
                return em.CreateEntity(typeof(T));
            return query.GetSingletonEntity();
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

            Unity.Jobs.JobHandle.CombineDependencies(BulletFieldShared.PoolFence, BulletFieldShared.CellMapFence).Complete();
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
