using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(RunProgressDirectorSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateAfter(typeof(SourceClipDiscreteEmitBuildSystem))]
    [UpdateAfter(typeof(HazardEmitterCoordinatorSystem))]
    [UpdateAfter(typeof(HazardActorPatternSelectorSystem))]
    [UpdateBefore(typeof(SourceClipRequestBuildSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct HazardEmitterEmitBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
            state.RequireForUpdate<DiscreteEmitChannelSingletonTag>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<HazardEmitterComponent>();
            state.RequireForUpdate<HazardEmitterAppliedConfigComponent>();
            state.RequireForUpdate<HazardEmitterTelegraphProfileComponent>();
            state.RequireForUpdate<HazardEmitterEmissionProfileComponent>();
            state.RequireForUpdate<HazardEmitterRuntimeStateComponent>();
            state.RequireForUpdate<HazardEmitterSelectedPatternRuntimeComponent>();
            state.RequireForUpdate<HazardEmitterCoordinatorStateComponent>();
            state.RequireForUpdate<HazardActorPatternSelectorStateComponent>();
            state.RequireForUpdate<HazardEmitterPatternExecutionSlotBuffer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            var stageState = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            if (hasTopologyState
                && !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState))
                return;

            if (stageState.State != RunDirectorStageStateId.Running)
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime))
                return;

            uint frame = FrameSequenceUtility.GetCurrentFrame(SystemAPI.GetSingleton<BulletFrameCounterComponent>());
            var channelEntity = SystemAPI.GetSingletonEntity<DiscreteEmitChannelSingletonTag>();
            var discreteRequests = SystemAPI.GetBuffer<DiscreteEmitRequestBuffer>(channelEntity);

            var actorLookup = SystemAPI.GetComponentLookup<HazardActorComponent>(true);
            var selectorLookup = SystemAPI.GetComponentLookup<HazardActorPatternSelectorStateComponent>(true);
            var cycleSignalLookup = SystemAPI.GetComponentLookup<HazardEmitterCycleSignalComponent>(false);
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var executionSlotLookup = SystemAPI.GetBufferLookup<HazardEmitterPatternExecutionSlotBuffer>(true);
            actorLookup.Update(ref state);
            selectorLookup.Update(ref state);
            cycleSignalLookup.Update(ref state);
            localTransformLookup.Update(ref state);
            localToWorldLookup.Update(ref state);
            executionSlotLookup.Update(ref state);

            foreach (var (emitter, applied, telegraph, emission, selectedPattern, coordinator, runtime, entity) in SystemAPI.Query<
                RefRO<HazardEmitterComponent>,
                RefRO<HazardEmitterAppliedConfigComponent>,
                RefRW<HazardEmitterTelegraphProfileComponent>,
                RefRW<HazardEmitterEmissionProfileComponent>,
                RefRW<HazardEmitterSelectedPatternRuntimeComponent>,
                RefRO<HazardEmitterCoordinatorStateComponent>,
                RefRW<HazardEmitterRuntimeStateComponent>>().WithEntityAccess())
            {
                ref readonly var emitterConfig = ref emitter.ValueRO;
                ref readonly var appliedConfig = ref applied.ValueRO;
                ref var runtimeState = ref runtime.ValueRW;

                bool hasCycleSignal = cycleSignalLookup.HasComponent(entity);

                if (coordinator.ValueRO.ActivationAllowed == 0)
                {
                    selectedPattern.ValueRW.AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId;
                    runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Dormant;
                    runtimeState.StateElapsedSec = 0f;
                    continue;
                }

                int selectedSlotId = GetSelectedPatternSlotIdForEmitter(
                    emitterConfig.ActorEntity,
                    emitterConfig.EmitterId,
                    selectorLookup);
                if (!executionSlotLookup.HasBuffer(entity))
                {
                    selectedPattern.ValueRW.AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId;
                    runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Dormant;
                    runtimeState.StateElapsedSec = 0f;
                    continue;
                }

                var executionSlots = executionSlotLookup[entity];
                if (selectedSlotId < 0
                    || !TryFindExecutionSlot(executionSlots, selectedSlotId, out var executionSlot))
                {
                    selectedPattern.ValueRW.AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId;
                    runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Dormant;
                    runtimeState.StateElapsedSec = 0f;
                    continue;
                }

                if (selectedPattern.ValueRO.AppliedPatternSlotId != selectedSlotId)
                {
                    var telegraphRuntime = telegraph.ValueRO;
                    var emissionRuntime = emission.ValueRO;
                    HazardEmitterPatternSetCompatibilityUtility.ApplyExecutionSlotToRuntime(
                        in executionSlot,
                        ref telegraphRuntime,
                        ref emissionRuntime);
                    telegraph.ValueRW = telegraphRuntime;
                    emission.ValueRW = emissionRuntime;
                    selectedPattern.ValueRW.AppliedPatternSlotId = selectedSlotId;
                    runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Dormant;
                    runtimeState.StateElapsedSec = 0f;
                    continue;
                }

                if (runtimeState.LifecycleState != HazardEmitterLifecycleStateId.Dormant)
                    runtimeState.StateElapsedSec = math.max(0f, runtimeState.StateElapsedSec + deltaTime);

                bool emittedThisFrame = false;
                for (int guard = 0; guard < 4; guard++)
                {
                    switch (runtimeState.LifecycleState)
                    {
                        case HazardEmitterLifecycleStateId.Dormant:
                            if (emittedThisFrame)
                            {
                                guard = 4;
                            }
                            else
                            {
                                runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Telegraph;
                                runtimeState.StateElapsedSec = 0f;
                            }
                            break;

                        case HazardEmitterLifecycleStateId.Telegraph:
                        {
                            float telegraphDuration = math.max(0f, telegraph.ValueRO.TelegraphDurationSec);
                            if (runtimeState.StateElapsedSec < telegraphDuration)
                            {
                                guard = 4;
                                break;
                            }

                            runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Emit;
                            Entity sourceEntity = ResolveSourceEntity(emitterConfig.ActorEntity, actorLookup);
                            float3 anchorPosition = ResolveWorldAnchorPosition(
                                entity,
                                appliedConfig.LocalOffset,
                                localTransformLookup,
                                localToWorldLookup);
                            var seed = DiscreteEmitRequestUtility.BuildDiscreteEmitSeedFromEmitter(
                                sourceEntity,
                                entity,
                                emitterConfig.EmitterId,
                                in emission.ValueRO,
                                anchorPosition,
                                priority: 0);
                            discreteRequests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(seed, frame));
                            emittedThisFrame = true;
                            runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Cooldown;
                            runtimeState.StateElapsedSec = 0f;

                            float cooldownSec = math.max(0f, emission.ValueRO.CooldownSec);
                            if (cooldownSec <= 0f)
                            {
                                runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Dormant;
                                runtimeState.StateElapsedSec = 0f;
                                IncrementCycleCompletedVersion(cycleSignalLookup, entity, hasCycleSignal);
                            }

                            guard = 4;
                            break;
                        }

                        case HazardEmitterLifecycleStateId.Emit:
                            runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Cooldown;
                            runtimeState.StateElapsedSec = 0f;
                            break;

                        case HazardEmitterLifecycleStateId.Cooldown:
                        {
                            float cooldownSec = math.max(0f, emission.ValueRO.CooldownSec);
                            if (runtimeState.StateElapsedSec < cooldownSec)
                            {
                                guard = 4;
                                break;
                            }

                            runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Dormant;
                            runtimeState.StateElapsedSec = 0f;
                            IncrementCycleCompletedVersion(cycleSignalLookup, entity, hasCycleSignal);
                            if (emittedThisFrame)
                                guard = 4;
                            break;
                        }

                        default:
                            runtimeState.LifecycleState = HazardEmitterLifecycleStateId.Dormant;
                            runtimeState.StateElapsedSec = 0f;
                            guard = 4;
                            break;
                    }
                }
            }
        }

        private static Entity ResolveSourceEntity(
            Entity actorEntity,
            ComponentLookup<HazardActorComponent> actorLookup)
        {
            if (actorEntity == Entity.Null || !actorLookup.HasComponent(actorEntity))
                return Entity.Null;

            return actorLookup[actorEntity].SourceEntity;
        }

        private static float3 ResolveWorldAnchorPosition(
            Entity emitterEntity,
            float3 localOffset,
            ComponentLookup<LocalTransform> localTransformLookup,
            ComponentLookup<LocalToWorld> localToWorldLookup)
        {
            if (localToWorldLookup.HasComponent(emitterEntity))
                return math.transform(localToWorldLookup[emitterEntity].Value, localOffset);

            if (localTransformLookup.HasComponent(emitterEntity))
                return localTransformLookup[emitterEntity].Position + localOffset;

            return localOffset;
        }

        private static int GetSelectedPatternSlotIdForEmitter(
            Entity actorEntity,
            int emitterId,
            ComponentLookup<HazardActorPatternSelectorStateComponent> selectorLookup)
        {
            if (actorEntity == Entity.Null || !selectorLookup.HasComponent(actorEntity))
                return HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId;

            var selector = selectorLookup[actorEntity];
            if (selector.TargetEmitterId != emitterId || selector.CurrentPatternSlotId < 0)
                return HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId;

            return selector.CurrentPatternSlotId;
        }

        private static bool TryFindExecutionSlot(
            DynamicBuffer<HazardEmitterPatternExecutionSlotBuffer> executionSlots,
            int patternSlotId,
            out HazardEmitterPatternExecutionSlotBuffer slot)
        {
            for (int i = 0; i < executionSlots.Length; i++)
            {
                if (executionSlots[i].PatternSlotId != patternSlotId)
                    continue;

                slot = executionSlots[i];
                return true;
            }

            slot = default;
            return false;
        }

        private static void IncrementCycleCompletedVersion(
            ComponentLookup<HazardEmitterCycleSignalComponent> cycleSignalLookup,
            Entity entity,
            bool hasCycleSignal)
        {
            if (!hasCycleSignal)
                return;

            var cycleSignal = cycleSignalLookup[entity];
            cycleSignal.CompletedVersion = cycleSignal.CompletedVersion >= uint.MaxValue
                ? 1u
                : cycleSignal.CompletedVersion + 1u;
            cycleSignalLookup[entity] = cycleSignal;
        }
    }
}
