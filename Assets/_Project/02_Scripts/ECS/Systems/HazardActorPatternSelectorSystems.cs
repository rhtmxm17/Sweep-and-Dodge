using Unity.Collections;
using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(HazardEmitterCoordinatorSystem))]
    [UpdateBefore(typeof(HazardEmitterEmitBuildSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct HazardActorPatternSelectorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<HazardActorRuntimeStateComponent>();
            state.RequireForUpdate<HazardActorBehaviorPhaseStateComponent>();
            state.RequireForUpdate<HazardActorPatternSelectorStateComponent>();
            state.RequireForUpdate<HazardActorEmitterRefBuffer>();
            state.RequireForUpdate<HazardActorPhaseSelectorPolicyBuffer>();
            state.RequireForUpdate<HazardActorPhaseSelectorCandidateBuffer>();
            state.RequireForUpdate<HazardEmitterComponent>();
            state.RequireForUpdate<HazardEmitterCoordinatorStateComponent>();
            state.RequireForUpdate<HazardEmitterPatternSlotBuffer>();
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

            var emitterLookup = SystemAPI.GetComponentLookup<HazardEmitterComponent>(true);
            var runtimeLookup = SystemAPI.GetComponentLookup<HazardActorRuntimeStateComponent>(true);
            var phaseStateLookup = SystemAPI.GetComponentLookup<HazardActorBehaviorPhaseStateComponent>(true);
            var selectorStateLookup = SystemAPI.GetComponentLookup<HazardActorPatternSelectorStateComponent>(false);
            var coordinatorLookup = SystemAPI.GetComponentLookup<HazardEmitterCoordinatorStateComponent>(true);
            var cycleSignalLookup = SystemAPI.GetComponentLookup<HazardEmitterCycleSignalComponent>(true);
            var emitterRefLookup = SystemAPI.GetBufferLookup<HazardActorEmitterRefBuffer>(true);
            var policyLookup = SystemAPI.GetBufferLookup<HazardActorPhaseSelectorPolicyBuffer>(true);
            var candidateLookup = SystemAPI.GetBufferLookup<HazardActorPhaseSelectorCandidateBuffer>(true);
            var slotLookup = SystemAPI.GetBufferLookup<HazardEmitterPatternSlotBuffer>(true);
            emitterLookup.Update(ref state);
            runtimeLookup.Update(ref state);
            phaseStateLookup.Update(ref state);
            selectorStateLookup.Update(ref state);
            coordinatorLookup.Update(ref state);
            cycleSignalLookup.Update(ref state);
            emitterRefLookup.Update(ref state);
            policyLookup.Update(ref state);
            candidateLookup.Update(ref state);
            slotLookup.Update(ref state);

            using var actorQuery = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HazardActorRuntimeStateComponent>(),
                ComponentType.ReadOnly<HazardActorBehaviorPhaseStateComponent>(),
                ComponentType.ReadWrite<HazardActorPatternSelectorStateComponent>());
            using var actorEntities = actorQuery.ToEntityArray(Allocator.Temp);

            for (int actorIndex = 0; actorIndex < actorEntities.Length; actorIndex++)
            {
                Entity actorEntity = actorEntities[actorIndex];
                if (!runtimeLookup.HasComponent(actorEntity)
                    || !phaseStateLookup.HasComponent(actorEntity)
                    || !selectorStateLookup.HasComponent(actorEntity)
                    || !emitterRefLookup.HasBuffer(actorEntity)
                    || !policyLookup.HasBuffer(actorEntity)
                    || !candidateLookup.HasBuffer(actorEntity))
                {
                    continue;
                }

                if (runtimeLookup[actorEntity].PresenceState != HazardActorPresenceStateId.Active)
                    continue;

                ref var selectorState = ref selectorStateLookup.GetRefRW(actorEntity).ValueRW;
                var phaseState = phaseStateLookup[actorEntity];
                var emitterRefs = emitterRefLookup[actorEntity];
                var policies = policyLookup[actorEntity];
                var candidates = candidateLookup[actorEntity];

                if (!TryFindPolicy(policies, phaseState.CurrentPhaseId, out var policy))
                {
                    InvalidateSelection(ref selectorState, preserveLastPattern: false);
                    selectorState.LastResolvedPhaseVersion = phaseState.PhaseVersion;
                    continue;
                }

                bool phaseChanged = selectorState.LastResolvedPhaseVersion != phaseState.PhaseVersion;
                bool currentSelectionEligible = TryResolveCurrentCandidate(
                    in selectorState,
                    phaseState.CurrentPhaseId,
                    emitterRefs,
                    candidates,
                    emitterLookup,
                    coordinatorLookup,
                    slotLookup,
                    out var currentCandidate,
                    out var currentEmitterEntity);

                switch (policy.SelectionMode)
                {
                    case HazardActorSelectionModeId.OrderedPriority:
                    {
                        bool found = TryFindFirstEligibleCandidate(
                            phaseState.CurrentPhaseId,
                            emitterRefs,
                            candidates,
                            emitterLookup,
                            coordinatorLookup,
                            slotLookup,
                            -1,
                            out var selectedCandidate);

                        if (!found)
                        {
                            InvalidateSelection(ref selectorState, preserveLastPattern: selectorState.CurrentPatternSlotId >= 0);
                            selectorState.LastResolvedPhaseVersion = phaseState.PhaseVersion;
                            break;
                        }

                        ApplyCandidateSelection(
                            ref selectorState,
                            selectedCandidate,
                            phaseState.PhaseVersion);
                        break;
                    }

                    case HazardActorSelectionModeId.OrderedCycle:
                    {
                        if (phaseChanged || !currentSelectionEligible)
                        {
                            bool found = TryFindFirstEligibleCandidate(
                                phaseState.CurrentPhaseId,
                                emitterRefs,
                                candidates,
                                emitterLookup,
                                coordinatorLookup,
                                slotLookup,
                                -1,
                                out var selectedCandidate);

                            if (!found)
                            {
                                InvalidateSelection(ref selectorState, preserveLastPattern: selectorState.CurrentPatternSlotId >= 0);
                                selectorState.LastResolvedPhaseVersion = phaseState.PhaseVersion;
                                break;
                            }

                            ApplyCandidateSelection(
                                ref selectorState,
                                selectedCandidate,
                                phaseState.PhaseVersion);
                            break;
                        }

                        uint completedVersion = cycleSignalLookup.HasComponent(currentEmitterEntity)
                            ? cycleSignalLookup[currentEmitterEntity].CompletedVersion
                            : 0u;
                        bool shouldAdvance = completedVersion > selectorState.LastConsumedCycleVersion;
                        if (!shouldAdvance)
                        {
                            selectorState.LastResolvedPhaseVersion = phaseState.PhaseVersion;
                            break;
                        }

                        bool foundNext = TryFindFirstEligibleCandidate(
                            phaseState.CurrentPhaseId,
                            emitterRefs,
                            candidates,
                            emitterLookup,
                            coordinatorLookup,
                            slotLookup,
                            selectorState.CurrentCandidateOrder,
                            out var nextCandidate);
                        if (!foundNext)
                        {
                            InvalidateSelection(ref selectorState, preserveLastPattern: true);
                            selectorState.LastResolvedPhaseVersion = phaseState.PhaseVersion;
                            selectorState.LastConsumedCycleVersion = completedVersion;
                            break;
                        }

                        ApplyCandidateSelection(
                            ref selectorState,
                            nextCandidate,
                            phaseState.PhaseVersion,
                            completedVersion);
                        break;
                    }

                    default:
                        InvalidateSelection(ref selectorState, preserveLastPattern: selectorState.CurrentPatternSlotId >= 0);
                        selectorState.LastResolvedPhaseVersion = phaseState.PhaseVersion;
                        break;
                }
            }
        }

        private static bool TryFindPolicy(
            DynamicBuffer<HazardActorPhaseSelectorPolicyBuffer> policies,
            int phaseId,
            out HazardActorPhaseSelectorPolicyBuffer policy)
        {
            for (int i = 0; i < policies.Length; i++)
            {
                if (policies[i].PhaseId != phaseId)
                    continue;

                policy = policies[i];
                return true;
            }

            policy = default;
            return false;
        }

        private static bool TryResolveCurrentCandidate(
            in HazardActorPatternSelectorStateComponent selectorState,
            int phaseId,
            DynamicBuffer<HazardActorEmitterRefBuffer> emitterRefs,
            DynamicBuffer<HazardActorPhaseSelectorCandidateBuffer> candidates,
            ComponentLookup<HazardEmitterComponent> emitterLookup,
            ComponentLookup<HazardEmitterCoordinatorStateComponent> coordinatorLookup,
            BufferLookup<HazardEmitterPatternSlotBuffer> slotLookup,
            out HazardActorPhaseSelectorCandidateBuffer candidate,
            out Entity emitterEntity)
        {
            candidate = default;
            emitterEntity = Entity.Null;
            if (selectorState.TargetEmitterId < 0 || selectorState.CurrentPatternSlotId < 0)
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                var current = candidates[i];
                if (current.PhaseId != phaseId
                    || current.EmitterId != selectorState.TargetEmitterId
                    || current.PatternSlotId != selectorState.CurrentPatternSlotId)
                {
                    continue;
                }

                if (!TryResolveEligibleCandidate(
                        current,
                        emitterRefs,
                        emitterLookup,
                        coordinatorLookup,
                        slotLookup,
                        out emitterEntity))
                {
                    return false;
                }

                candidate = current;
                return true;
            }

            return false;
        }

        private static bool TryFindFirstEligibleCandidate(
            int phaseId,
            DynamicBuffer<HazardActorEmitterRefBuffer> emitterRefs,
            DynamicBuffer<HazardActorPhaseSelectorCandidateBuffer> candidates,
            ComponentLookup<HazardEmitterComponent> emitterLookup,
            ComponentLookup<HazardEmitterCoordinatorStateComponent> coordinatorLookup,
            BufferLookup<HazardEmitterPatternSlotBuffer> slotLookup,
            int minOrderExclusive,
            out HazardActorPhaseSelectorCandidateBuffer candidate)
        {
            candidate = default;
            int bestIndex = int.MaxValue;
            bool found = false;

            for (int pass = 0; pass < 2; pass++)
            {
                int minOrder = pass == 0 ? minOrderExclusive : int.MinValue;
                for (int i = 0; i < candidates.Length; i++)
                {
                    var current = candidates[i];
                    if (current.PhaseId != phaseId || current.OrderIndex <= minOrder || current.OrderIndex >= bestIndex)
                        continue;

                    if (!TryResolveEligibleCandidate(
                            current,
                            emitterRefs,
                            emitterLookup,
                            coordinatorLookup,
                            slotLookup,
                            out _))
                    {
                        continue;
                    }

                    bestIndex = current.OrderIndex;
                    candidate = current;
                    found = true;
                }

                if (found || minOrderExclusive < 0)
                    break;
            }

            return found;
        }

        private static bool TryResolveEligibleCandidate(
            in HazardActorPhaseSelectorCandidateBuffer candidate,
            DynamicBuffer<HazardActorEmitterRefBuffer> emitterRefs,
            ComponentLookup<HazardEmitterComponent> emitterLookup,
            ComponentLookup<HazardEmitterCoordinatorStateComponent> coordinatorLookup,
            BufferLookup<HazardEmitterPatternSlotBuffer> slotLookup,
            out Entity emitterEntity)
        {
            emitterEntity = Entity.Null;
            for (int i = 0; i < emitterRefs.Length; i++)
            {
                if (emitterRefs[i].EmitterId != candidate.EmitterId)
                    continue;

                emitterEntity = emitterRefs[i].EmitterEntity;
                break;
            }

            if (emitterEntity == Entity.Null
                || !emitterLookup.HasComponent(emitterEntity)
                || !coordinatorLookup.HasComponent(emitterEntity)
                || !slotLookup.HasBuffer(emitterEntity))
            {
                return false;
            }

            if (coordinatorLookup[emitterEntity].ActivationAllowed == 0)
                return false;

            var slots = slotLookup[emitterEntity];
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].PatternSlotId == candidate.PatternSlotId)
                    return true;
            }

            return false;
        }

        private static void ApplyCandidateSelection(
            ref HazardActorPatternSelectorStateComponent selectorState,
            in HazardActorPhaseSelectorCandidateBuffer candidate,
            uint phaseVersion,
            uint consumedCycleVersion = 0u)
        {
            bool changed = selectorState.TargetEmitterId != candidate.EmitterId
                || selectorState.CurrentPatternSlotId != candidate.PatternSlotId;

            int previousCurrentSlotId = selectorState.CurrentPatternSlotId;
            selectorState.TargetEmitterId = candidate.EmitterId;
            selectorState.CurrentPatternSlotId = candidate.PatternSlotId;
            selectorState.CurrentCandidateOrder = candidate.OrderIndex;
            selectorState.LastResolvedPhaseVersion = phaseVersion;
            if (consumedCycleVersion > 0u)
                selectorState.LastConsumedCycleVersion = consumedCycleVersion;

            if (!changed)
                return;

            if (previousCurrentSlotId >= 0)
                selectorState.LastPatternSlotId = previousCurrentSlotId;
            selectorState.SelectionSequence = selectorState.SelectionSequence >= uint.MaxValue
                ? 1u
                : selectorState.SelectionSequence + 1u;
        }

        private static void InvalidateSelection(
            ref HazardActorPatternSelectorStateComponent selectorState,
            bool preserveLastPattern)
        {
            int previousCurrentSlotId = selectorState.CurrentPatternSlotId;
            selectorState.TargetEmitterId = -1;
            selectorState.CurrentPatternSlotId = -1;
            selectorState.CurrentCandidateOrder = -1;

            if (preserveLastPattern && previousCurrentSlotId >= 0)
                selectorState.LastPatternSlotId = previousCurrentSlotId;

            selectorState.SelectionSequence = selectorState.SelectionSequence >= uint.MaxValue
                ? 1u
                : selectorState.SelectionSequence + 1u;
        }
    }
}
