using Unity.Collections;
using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateBefore(typeof(HazardActorEmitSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct HazardActorPatternSelectorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<HazardActorRuntimeStateComponent>();
            state.RequireForUpdate<HazardActorBehaviorPhaseStateComponent>();
            state.RequireForUpdate<HazardActorPatternSelectorStateComponent>();
            state.RequireForUpdate<HazardActorPhaseTransitionRuntimeComponent>();
            state.RequireForUpdate<HazardActorPhaseSelectorPolicyBuffer>();
            state.RequireForUpdate<HazardActorPhaseSelectorCandidateBuffer>();
            state.RequireForUpdate<HazardActorPatternSlotBuffer>();
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

            var runtimeLookup = SystemAPI.GetComponentLookup<HazardActorRuntimeStateComponent>(true);
            var phaseStateLookup = SystemAPI.GetComponentLookup<HazardActorBehaviorPhaseStateComponent>(true);
            var transitionRuntimeLookup = SystemAPI.GetComponentLookup<HazardActorPhaseTransitionRuntimeComponent>(true);
            var selectorStateLookup = SystemAPI.GetComponentLookup<HazardActorPatternSelectorStateComponent>(false);
            var cycleSignalLookup = SystemAPI.GetComponentLookup<HazardActorEmitCycleSignalComponent>(true);
            var policyLookup = SystemAPI.GetBufferLookup<HazardActorPhaseSelectorPolicyBuffer>(true);
            var candidateLookup = SystemAPI.GetBufferLookup<HazardActorPhaseSelectorCandidateBuffer>(true);
            var slotLookup = SystemAPI.GetBufferLookup<HazardActorPatternSlotBuffer>(true);
            runtimeLookup.Update(ref state);
            phaseStateLookup.Update(ref state);
            transitionRuntimeLookup.Update(ref state);
            selectorStateLookup.Update(ref state);
            cycleSignalLookup.Update(ref state);
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
                    || !transitionRuntimeLookup.HasComponent(actorEntity)
                    || !selectorStateLookup.HasComponent(actorEntity)
                    || !policyLookup.HasBuffer(actorEntity)
                    || !candidateLookup.HasBuffer(actorEntity)
                    || !slotLookup.HasBuffer(actorEntity))
                {
                    continue;
                }

                if (runtimeLookup[actorEntity].PresenceState != HazardActorPresenceStateId.Active)
                    continue;

                if (transitionRuntimeLookup[actorEntity].State == HazardActorPhaseTransitionStateId.Preparing)
                    continue;

                ref var selectorState = ref selectorStateLookup.GetRefRW(actorEntity).ValueRW;
                var phaseState = phaseStateLookup[actorEntity];
                var policies = policyLookup[actorEntity];
                var candidates = candidateLookup[actorEntity];
                var patternSlots = slotLookup[actorEntity];

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
                    candidates,
                    patternSlots,
                    out var currentCandidate);

                switch (policy.SelectionMode)
                {
                    case HazardActorSelectionModeId.OrderedPriority:
                    {
                        bool found = TryFindFirstEligibleCandidate(
                            phaseState.CurrentPhaseId,
                            candidates,
                            patternSlots,
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
                                candidates,
                                patternSlots,
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

                        uint completedVersion = cycleSignalLookup.HasComponent(actorEntity)
                            ? cycleSignalLookup[actorEntity].CompletedVersion
                            : 0u;
                        bool shouldAdvance = completedVersion > selectorState.LastConsumedCycleVersion;
                        if (!shouldAdvance)
                        {
                            selectorState.LastResolvedPhaseVersion = phaseState.PhaseVersion;
                            break;
                        }

                        bool foundNext = TryFindFirstEligibleCandidate(
                            phaseState.CurrentPhaseId,
                            candidates,
                            patternSlots,
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
            DynamicBuffer<HazardActorPhaseSelectorCandidateBuffer> candidates,
            DynamicBuffer<HazardActorPatternSlotBuffer> patternSlots,
            out HazardActorPhaseSelectorCandidateBuffer candidate)
        {
            candidate = default;
            if (selectorState.CurrentPatternSlotId < 0)
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                var current = candidates[i];
                if (current.PhaseId != phaseId
                    || current.PatternSlotId != selectorState.CurrentPatternSlotId)
                {
                    continue;
                }

                if (!ContainsPatternSlot(patternSlots, current.PatternSlotId))
                    return false;

                candidate = current;
                return true;
            }

            return false;
        }

        private static bool TryFindFirstEligibleCandidate(
            int phaseId,
            DynamicBuffer<HazardActorPhaseSelectorCandidateBuffer> candidates,
            DynamicBuffer<HazardActorPatternSlotBuffer> patternSlots,
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

                    if (!ContainsPatternSlot(patternSlots, current.PatternSlotId))
                        continue;

                    bestIndex = current.OrderIndex;
                    candidate = current;
                    found = true;
                }

                if (found || minOrderExclusive < 0)
                    break;
            }

            return found;
        }

        private static bool ContainsPatternSlot(
            DynamicBuffer<HazardActorPatternSlotBuffer> patternSlots,
            int patternSlotId)
        {
            for (int i = 0; i < patternSlots.Length; i++)
            {
                if (patternSlots[i].PatternSlotId == patternSlotId)
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
            bool changed = selectorState.CurrentPatternSlotId != candidate.PatternSlotId;

            int previousCurrentSlotId = selectorState.CurrentPatternSlotId;
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
