using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateAfter(typeof(RunProgressDirectorSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateBefore(typeof(HazardActorPresenceSystem))]
    [UpdateBefore(typeof(HazardActorPhaseTransitionSystem))]
    [UpdateBefore(typeof(HazardActorPatternSelectorSystem))]
    public partial struct HazardActorOrchestrationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceHazardActorPlacementRefBuffer>();
            state.RequireForUpdate<SourceHazardActorOrchestrationRuleBuffer>();
            state.RequireForUpdate<SourceHazardActorOrchestrationRuleStateBuffer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            var stageState = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            if (hasTopologyState
                && !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState))
            {
                return;
            }

            if (stageState.State != RunDirectorStageStateId.Running)
                return;

            var actorAppliedLookup = SystemAPI.GetComponentLookup<HazardActorAppliedConfigComponent>(true);
            var actorRuntimeLookup = SystemAPI.GetComponentLookup<HazardActorRuntimeStateComponent>(true);
            var actorPhaseLookup = SystemAPI.GetComponentLookup<HazardActorBehaviorPhaseStateComponent>(true);
            var actorTransitionLookup = SystemAPI.GetComponentLookup<HazardActorPhaseTransitionRuntimeComponent>(true);
            var signalLookup = SystemAPI.GetComponentLookup<HazardActorOrchestrationRequestSignalComponent>(false);

            actorAppliedLookup.Update(ref state);
            actorRuntimeLookup.Update(ref state);
            actorPhaseLookup.Update(ref state);
            actorTransitionLookup.Update(ref state);
            signalLookup.Update(ref state);

            var sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    SourceSpawnComponent,
                    SourceHazardActorPlacementRefBuffer,
                    SourceHazardActorOrchestrationRuleBuffer,
                    SourceHazardActorOrchestrationRuleStateBuffer>()
                .Build();
            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);

            for (int sourceIndex = 0; sourceIndex < sourceEntities.Length; sourceIndex++)
            {
                var sourceEntity = sourceEntities[sourceIndex];
                var source = state.EntityManager.GetComponentData<SourceSpawnComponent>(sourceEntity);
                var placementRefs = state.EntityManager.GetBuffer<SourceHazardActorPlacementRefBuffer>(sourceEntity);
                var rules = state.EntityManager.GetBuffer<SourceHazardActorOrchestrationRuleBuffer>(sourceEntity);
                var ruleStates = state.EntityManager.GetBuffer<SourceHazardActorOrchestrationRuleStateBuffer>(sourceEntity);

                if (rules.Length <= 0)
                    continue;

                AlignRuleStates(rules, ruleStates);
                var emittedPlacements = new HashSet<int>();

                for (int i = 0; i < rules.Length; i++)
                {
                    var rule = rules[i];
                    if (i >= ruleStates.Length || ruleStates[i].HasFired != 0)
                        continue;

                    if (emittedPlacements.Contains(rule.TargetPlacementInstanceId))
                        continue;

                    if (!IsRuleEligible(rule, in source))
                        continue;

                    if (!TryResolveActorEntity(placementRefs, rule.TargetPlacementInstanceId, out var actorEntity))
                        continue;

                    if (!CanEmitRequest(
                            actorEntity,
                            in rule,
                            actorAppliedLookup,
                            actorRuntimeLookup,
                            actorPhaseLookup,
                            actorTransitionLookup,
                            signalLookup))
                    {
                        continue;
                    }

                    EmitRequest(actorEntity, in rule, signalLookup);
                    ruleStates[i] = new SourceHazardActorOrchestrationRuleStateBuffer
                    {
                        RuleId = rule.RuleId,
                        HasFired = 1,
                    };
                    emittedPlacements.Add(rule.TargetPlacementInstanceId);
                }
            }
        }

        private static void AlignRuleStates(
            DynamicBuffer<SourceHazardActorOrchestrationRuleBuffer> rules,
            DynamicBuffer<SourceHazardActorOrchestrationRuleStateBuffer> states)
        {
            bool aligned = rules.Length == states.Length;
            if (aligned)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    if (rules[i].RuleId == states[i].RuleId)
                        continue;

                    aligned = false;
                    break;
                }
            }

            if (aligned)
                return;

            states.Clear();
            for (int i = 0; i < rules.Length; i++)
            {
                states.Add(new SourceHazardActorOrchestrationRuleStateBuffer
                {
                    RuleId = rules[i].RuleId,
                    HasFired = 0,
                });
            }
        }

        private static bool IsRuleEligible(
            in SourceHazardActorOrchestrationRuleBuffer rule,
            in SourceSpawnComponent source)
        {
            switch (rule.TriggerType)
            {
                case HazardActorOrchestrationTriggerId.OnStageStart:
                    return true;

                case HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove:
                {
                    float progress01 = math.saturate((float)source.CollectedCount / math.max(1, source.ThresholdDepleted));
                    return progress01 >= rule.TriggerThresholdNormalized;
                }

                default:
                    return false;
            }
        }

        private static bool TryResolveActorEntity(
            DynamicBuffer<SourceHazardActorPlacementRefBuffer> placementRefs,
            int placementInstanceId,
            out Entity actorEntity)
        {
            for (int i = 0; i < placementRefs.Length; i++)
            {
                if (placementRefs[i].PlacementInstanceId != placementInstanceId)
                    continue;

                actorEntity = placementRefs[i].ActorEntity;
                return actorEntity != Entity.Null;
            }

            actorEntity = Entity.Null;
            return false;
        }

        private static bool CanEmitRequest(
            Entity actorEntity,
            in SourceHazardActorOrchestrationRuleBuffer rule,
            ComponentLookup<HazardActorAppliedConfigComponent> actorAppliedLookup,
            ComponentLookup<HazardActorRuntimeStateComponent> actorRuntimeLookup,
            ComponentLookup<HazardActorBehaviorPhaseStateComponent> actorPhaseLookup,
            ComponentLookup<HazardActorPhaseTransitionRuntimeComponent> actorTransitionLookup,
            ComponentLookup<HazardActorOrchestrationRequestSignalComponent> signalLookup)
        {
            if (actorEntity == Entity.Null
                || !actorAppliedLookup.HasComponent(actorEntity)
                || !actorRuntimeLookup.HasComponent(actorEntity)
                || !signalLookup.HasComponent(actorEntity))
            {
                return false;
            }

            var applied = actorAppliedLookup[actorEntity];
            if (applied.IsEnabled == 0 || applied.IsSuppressed != 0)
                return false;

            var runtime = actorRuntimeLookup[actorEntity];
            switch (rule.ActionType)
            {
                case HazardActorOrchestrationActionId.Spawn:
                    return runtime.PresenceState == HazardActorPresenceStateId.Hidden;

                case HazardActorOrchestrationActionId.Retire:
                    return runtime.PresenceState == HazardActorPresenceStateId.Active;

                case HazardActorOrchestrationActionId.PhaseSet:
                    if (runtime.PresenceState != HazardActorPresenceStateId.Active
                        || !actorPhaseLookup.HasComponent(actorEntity)
                        || !actorTransitionLookup.HasComponent(actorEntity))
                    {
                        return false;
                    }

                    if (actorTransitionLookup[actorEntity].State != HazardActorPhaseTransitionStateId.Idle)
                        return false;

                    return actorPhaseLookup[actorEntity].CurrentPhaseId != math.max(1, rule.TargetPhaseId);

                default:
                    return false;
            }
        }

        private static void EmitRequest(
            Entity actorEntity,
            in SourceHazardActorOrchestrationRuleBuffer rule,
            ComponentLookup<HazardActorOrchestrationRequestSignalComponent> signalLookup)
        {
            ref var signal = ref signalLookup.GetRefRW(actorEntity).ValueRW;
            signal.Version = NextVersion(signal.Version);
            signal.ActionType = rule.ActionType;
            signal.TargetPhaseId = rule.ActionType == HazardActorOrchestrationActionId.PhaseSet
                ? math.max(1, rule.TargetPhaseId)
                : -1;
        }

        private static uint NextVersion(uint value)
        {
            return value >= uint.MaxValue ? 1u : value + 1u;
        }
    }
}
