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
            state.RequireForUpdate<HazardActorPatternSelectorStateComponent>();
            state.RequireForUpdate<HazardActorEmitterRefBuffer>();
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
            var coordinatorLookup = SystemAPI.GetComponentLookup<HazardEmitterCoordinatorStateComponent>(true);
            var slotLookup = SystemAPI.GetBufferLookup<HazardEmitterPatternSlotBuffer>(true);
            emitterLookup.Update(ref state);
            coordinatorLookup.Update(ref state);
            slotLookup.Update(ref state);

            foreach (var (runtime, selector, emitterRefs) in SystemAPI.Query<
                RefRO<HazardActorRuntimeStateComponent>,
                RefRW<HazardActorPatternSelectorStateComponent>,
                DynamicBuffer<HazardActorEmitterRefBuffer>>())
            {
                if (runtime.ValueRO.PresenceState != HazardActorPresenceStateId.Active)
                    continue;

                int selectedEmitterId = -1;
                int selectedSlotId = -1;

                for (int i = 0; i < emitterRefs.Length; i++)
                {
                    Entity emitterEntity = emitterRefs[i].EmitterEntity;
                    if (emitterEntity == Entity.Null
                        || !emitterLookup.HasComponent(emitterEntity)
                        || !coordinatorLookup.HasComponent(emitterEntity)
                        || !slotLookup.HasBuffer(emitterEntity))
                    {
                        continue;
                    }

                    var coordinator = coordinatorLookup[emitterEntity];
                    if (coordinator.ActivationAllowed == 0)
                        continue;

                    var emitter = emitterLookup[emitterEntity];
                    var slots = slotLookup[emitterEntity];
                    if (slots.Length <= 0)
                        continue;

                    int candidateSlotId = GetLowestPatternSlotId(slots);
                    if (candidateSlotId < 0)
                        continue;

                    if (selectedEmitterId < 0
                        || emitter.EmitterId < selectedEmitterId
                        || (emitter.EmitterId == selectedEmitterId && candidateSlotId < selectedSlotId))
                    {
                        selectedEmitterId = emitter.EmitterId;
                        selectedSlotId = candidateSlotId;
                    }
                }

                ref var selectorState = ref selector.ValueRW;
                int previousEmitterId = selectorState.TargetEmitterId;
                int previousCurrentSlotId = selectorState.CurrentPatternSlotId;

                if (selectedEmitterId >= 0 && selectedSlotId >= 0)
                {
                    bool changed = previousEmitterId != selectedEmitterId || previousCurrentSlotId != selectedSlotId;
                    if (!changed)
                        continue;

                    selectorState.TargetEmitterId = selectedEmitterId;
                    selectorState.CurrentPatternSlotId = selectedSlotId;
                    selectorState.LastPatternSlotId = previousCurrentSlotId;
                    selectorState.SelectionSequence = selectorState.SelectionSequence >= uint.MaxValue
                        ? 1u
                        : selectorState.SelectionSequence + 1u;
                    continue;
                }

                if (previousEmitterId < 0 && previousCurrentSlotId < 0)
                    continue;

                selectorState.TargetEmitterId = -1;
                selectorState.CurrentPatternSlotId = -1;
                if (previousCurrentSlotId >= 0)
                    selectorState.LastPatternSlotId = previousCurrentSlotId;
                selectorState.SelectionSequence = selectorState.SelectionSequence >= uint.MaxValue
                    ? 1u
                    : selectorState.SelectionSequence + 1u;
            }
        }

        private static int GetLowestPatternSlotId(DynamicBuffer<HazardEmitterPatternSlotBuffer> slots)
        {
            int selectedSlotId = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                int candidate = slots[i].PatternSlotId;
                if (candidate < 0)
                    continue;

                if (selectedSlotId < 0 || candidate < selectedSlotId)
                    selectedSlotId = candidate;
            }

            return selectedSlotId;
        }
    }
}
