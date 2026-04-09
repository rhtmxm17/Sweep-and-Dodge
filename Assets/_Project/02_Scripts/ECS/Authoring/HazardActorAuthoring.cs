using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class HazardActorAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        [Min(1)] public int ActorId = 1;

        [Header("Contract")]
        public bool Enabled = true;
        public bool StartSuppressed = false;
        public HazardActorPresenceStateId InitialPresenceState = HazardActorPresenceStateId.Hidden;

        private sealed class Baker : Baker<HazardActorAuthoring>
        {
            public override void Bake(HazardActorAuthoring authoring)
            {
                if (!HazardActorAuthoringValidationUtility.TryValidate(authoring, out var sourceAuthoring, out var error))
                {
                    Debug.LogError($"[HazardActorAuthoring] {error}", authoring);
                    return;
                }

                var actorEntity = GetEntity(TransformUsageFlags.Dynamic);
                var sourceEntity = GetEntity(sourceAuthoring.gameObject, TransformUsageFlags.Dynamic);
                int actorId = math.max(1, authoring.ActorId);
                byte isEnabled = authoring.Enabled ? (byte)1 : (byte)0;
                byte isSuppressed = authoring.StartSuppressed ? (byte)1 : (byte)0;

                AddComponent(actorEntity, new HazardActorComponent
                {
                    ActorId = actorId,
                    SourceEntity = sourceEntity,
                });

                var baselineConfig = new HazardActorAppliedConfigBaselineComponent
                {
                    IsEnabled = isEnabled,
                    IsSuppressed = isSuppressed,
                };
                AddComponent(actorEntity, baselineConfig);
                AddComponent(actorEntity, new HazardActorAppliedConfigComponent
                {
                    IsEnabled = baselineConfig.IsEnabled,
                    IsSuppressed = baselineConfig.IsSuppressed,
                });

                AddComponent(actorEntity, new HazardActorRuntimeBaselineComponent
                {
                    InitialPresenceState = authoring.InitialPresenceState,
                });
                AddComponent(actorEntity, new HazardActorRuntimeStateComponent
                {
                    PresenceState = authoring.InitialPresenceState,
                    StateElapsedSec = 0f,
                });
                AddComponent(actorEntity, new HazardActorPatternSelectorStateComponent
                {
                    TargetEmitterId = -1,
                    CurrentPatternSlotId = -1,
                    LastPatternSlotId = -1,
                    SelectionSequence = 0u,
                });
                AddBuffer<HazardActorEmitterRefBuffer>(actorEntity).Clear();

                AppendToBuffer(sourceEntity, new SourceHazardActorRefBuffer
                {
                    ActorEntity = actorEntity,
                    ActorId = actorId,
                });
            }
        }
    }
}
