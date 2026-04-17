using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(RunProgressDirectorSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateAfter(typeof(SourceClipDiscreteEmitBuildSystem))]
    [UpdateAfter(typeof(HazardActorPatternSelectorSystem))]
    [UpdateBefore(typeof(SourceClipRequestBuildSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct HazardActorEmitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
            state.RequireForUpdate<DiscreteEmitChannelSingletonTag>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<HazardActorComponent>();
            state.RequireForUpdate<HazardActorAppliedConfigComponent>();
            state.RequireForUpdate<HazardActorRuntimeStateComponent>();
            state.RequireForUpdate<HazardActorPhaseTransitionRuntimeComponent>();
            state.RequireForUpdate<HazardActorPatternSelectorStateComponent>();
            state.RequireForUpdate<HazardActorPatternExecutionSlotBuffer>();
            state.RequireForUpdate<HazardActorEmitStateComponent>();
            state.RequireForUpdate<HazardActorEmitActiveTelegraphComponent>();
            state.RequireForUpdate<HazardActorEmitActiveEmissionComponent>();
            state.RequireForUpdate<HazardActorEmitCycleSignalComponent>();
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
            var appliedLookup = SystemAPI.GetComponentLookup<HazardActorAppliedConfigComponent>(true);
            var runtimeLookup = SystemAPI.GetComponentLookup<HazardActorRuntimeStateComponent>(true);
            var transitionLookup = SystemAPI.GetComponentLookup<HazardActorPhaseTransitionRuntimeComponent>(true);
            var selectorLookup = SystemAPI.GetComponentLookup<HazardActorPatternSelectorStateComponent>(true);
            var telegraphLookup = SystemAPI.GetComponentLookup<HazardActorEmitActiveTelegraphComponent>(false);
            var emissionLookup = SystemAPI.GetComponentLookup<HazardActorEmitActiveEmissionComponent>(false);
            var cycleSignalLookup = SystemAPI.GetComponentLookup<HazardActorEmitCycleSignalComponent>(false);
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var executionSlotLookup = SystemAPI.GetBufferLookup<HazardActorPatternExecutionSlotBuffer>(true);
            actorLookup.Update(ref state);
            appliedLookup.Update(ref state);
            runtimeLookup.Update(ref state);
            transitionLookup.Update(ref state);
            selectorLookup.Update(ref state);
            telegraphLookup.Update(ref state);
            emissionLookup.Update(ref state);
            cycleSignalLookup.Update(ref state);
            localTransformLookup.Update(ref state);
            localToWorldLookup.Update(ref state);
            executionSlotLookup.Update(ref state);

            foreach (var (emitState, entity) in SystemAPI.Query<RefRW<HazardActorEmitStateComponent>>().WithEntityAccess())
            {
                if (!actorLookup.HasComponent(entity)
                    || !appliedLookup.HasComponent(entity)
                    || !runtimeLookup.HasComponent(entity)
                    || !transitionLookup.HasComponent(entity)
                    || !selectorLookup.HasComponent(entity)
                    || !telegraphLookup.HasComponent(entity)
                    || !emissionLookup.HasComponent(entity)
                    || !cycleSignalLookup.HasComponent(entity)
                    || !executionSlotLookup.HasBuffer(entity))
                {
                    continue;
                }

                ref var runtimeState = ref emitState.ValueRW;
                ref var telegraph = ref telegraphLookup.GetRefRW(entity).ValueRW;
                ref var emission = ref emissionLookup.GetRefRW(entity).ValueRW;
                ref var cycleSignal = ref cycleSignalLookup.GetRefRW(entity).ValueRW;

                bool suppressed = IsSuppressed(
                    entity,
                    appliedLookup,
                    runtimeLookup,
                    transitionLookup,
                    selectorLookup);
                if (suppressed)
                {
                    ResetDormant(ref runtimeState, ref telegraph, ref emission);
                    continue;
                }

                var selector = selectorLookup[entity];
                var executionSlots = executionSlotLookup[entity];
                if (selector.CurrentPatternSlotId < 0
                    || !TryFindExecutionSlot(executionSlots, selector.CurrentPatternSlotId, out var executionSlot))
                {
                    ResetDormant(ref runtimeState, ref telegraph, ref emission);
                    continue;
                }

                if (telegraph.AppliedPatternSlotId != selector.CurrentPatternSlotId
                    || emission.AppliedPatternSlotId != selector.CurrentPatternSlotId)
                {
                    HazardActorPatternRuntimeUtility.ApplyExecutionSlotToRuntime(
                        in executionSlot,
                        ref telegraph,
                        ref emission);
                    runtimeState.LifecycleState = HazardActorEmitLifecycleStateId.Dormant;
                    runtimeState.StateElapsedSec = 0f;
                    continue;
                }

                if (runtimeState.LifecycleState != HazardActorEmitLifecycleStateId.Dormant)
                    runtimeState.StateElapsedSec = math.max(0f, runtimeState.StateElapsedSec + deltaTime);

                bool emittedThisFrame = false;
                for (int guard = 0; guard < 4; guard++)
                {
                    switch (runtimeState.LifecycleState)
                    {
                        case HazardActorEmitLifecycleStateId.Dormant:
                            if (emittedThisFrame)
                            {
                                guard = 4;
                            }
                            else
                            {
                                runtimeState.LifecycleState = HazardActorEmitLifecycleStateId.Telegraph;
                                runtimeState.StateElapsedSec = 0f;
                            }
                            break;

                        case HazardActorEmitLifecycleStateId.Telegraph:
                        {
                            float telegraphDuration = math.max(0f, telegraph.TelegraphDurationSec);
                            if (runtimeState.StateElapsedSec < telegraphDuration)
                            {
                                guard = 4;
                                break;
                            }

                            runtimeState.LifecycleState = HazardActorEmitLifecycleStateId.Emit;
                            Entity sourceEntity = actorLookup[entity].SourceEntity;
                            float3 anchorPosition = ResolveWorldAnchorPosition(
                                entity,
                                executionSlot.LocalOffset,
                                localTransformLookup,
                                localToWorldLookup);
                            var seed = DiscreteEmitRequestUtility.BuildDiscreteEmitSeedFromHazardActor(
                                sourceEntity,
                                entity,
                                selector.CurrentPatternSlotId,
                                in emission,
                                anchorPosition,
                                priority: 0);
                            discreteRequests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(seed, frame));
                            emittedThisFrame = true;
                            runtimeState.LifecycleState = HazardActorEmitLifecycleStateId.Cooldown;
                            runtimeState.StateElapsedSec = 0f;

                            float cooldownSec = math.max(0f, emission.CooldownSec);
                            if (cooldownSec <= 0f)
                            {
                                runtimeState.LifecycleState = HazardActorEmitLifecycleStateId.Dormant;
                                runtimeState.StateElapsedSec = 0f;
                                IncrementCycleCompletedVersion(ref cycleSignal);
                            }

                            guard = 4;
                            break;
                        }

                        case HazardActorEmitLifecycleStateId.Emit:
                            runtimeState.LifecycleState = HazardActorEmitLifecycleStateId.Cooldown;
                            runtimeState.StateElapsedSec = 0f;
                            break;

                        case HazardActorEmitLifecycleStateId.Cooldown:
                        {
                            float cooldownSec = math.max(0f, emission.CooldownSec);
                            if (runtimeState.StateElapsedSec < cooldownSec)
                            {
                                guard = 4;
                                break;
                            }

                            runtimeState.LifecycleState = HazardActorEmitLifecycleStateId.Dormant;
                            runtimeState.StateElapsedSec = 0f;
                            IncrementCycleCompletedVersion(ref cycleSignal);
                            if (emittedThisFrame)
                                guard = 4;
                            break;
                        }

                        default:
                            ResetDormant(ref runtimeState, ref telegraph, ref emission);
                            guard = 4;
                            break;
                    }
                }
            }
        }

        private static bool IsSuppressed(
            Entity actorEntity,
            ComponentLookup<HazardActorAppliedConfigComponent> appliedLookup,
            ComponentLookup<HazardActorRuntimeStateComponent> runtimeLookup,
            ComponentLookup<HazardActorPhaseTransitionRuntimeComponent> transitionLookup,
            ComponentLookup<HazardActorPatternSelectorStateComponent> selectorLookup)
        {
            var applied = appliedLookup[actorEntity];
            if (applied.IsEnabled == 0 || applied.IsSuppressed != 0)
                return true;

            var runtime = runtimeLookup[actorEntity];
            if (runtime.PresenceState != HazardActorPresenceStateId.Active)
                return true;

            if (transitionLookup[actorEntity].State == HazardActorPhaseTransitionStateId.Preparing)
                return true;

            return selectorLookup[actorEntity].CurrentPatternSlotId < 0;
        }

        private static void ResetDormant(
            ref HazardActorEmitStateComponent emitState,
            ref HazardActorEmitActiveTelegraphComponent telegraph,
            ref HazardActorEmitActiveEmissionComponent emission)
        {
            emitState.LifecycleState = HazardActorEmitLifecycleStateId.Dormant;
            emitState.StateElapsedSec = 0f;
            telegraph.AppliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
            emission.AppliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId;
        }

        private static float3 ResolveWorldAnchorPosition(
            Entity actorEntity,
            float3 localOffset,
            ComponentLookup<LocalTransform> localTransformLookup,
            ComponentLookup<LocalToWorld> localToWorldLookup)
        {
            if (localToWorldLookup.HasComponent(actorEntity))
                return math.transform(localToWorldLookup[actorEntity].Value, localOffset);

            if (localTransformLookup.HasComponent(actorEntity))
                return localTransformLookup[actorEntity].Position + localOffset;

            return localOffset;
        }

        private static bool TryFindExecutionSlot(
            DynamicBuffer<HazardActorPatternExecutionSlotBuffer> executionSlots,
            int patternSlotId,
            out HazardActorPatternExecutionSlotBuffer slot)
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

        private static void IncrementCycleCompletedVersion(ref HazardActorEmitCycleSignalComponent cycleSignal)
        {
            cycleSignal.CompletedVersion = cycleSignal.CompletedVersion >= uint.MaxValue
                ? 1u
                : cycleSignal.CompletedVersion + 1u;
        }
    }
}
