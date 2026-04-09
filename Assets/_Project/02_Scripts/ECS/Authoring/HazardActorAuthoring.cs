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
                var emitterRefs = AddBuffer<HazardActorEmitterRefBuffer>(actorEntity);
                emitterRefs.Clear();
                var emitters = authoring.GetComponentsInChildren<HazardEmitterAuthoring>(includeInactive: true);
                for (int i = 0; i < emitters.Length; i++)
                {
                    var emitter = emitters[i];
                    if (emitter == null)
                        continue;

                    var parentActor = emitter.GetComponentInParent<HazardActorAuthoring>(includeInactive: true);
                    if (parentActor != authoring)
                        continue;

                    emitterRefs.Add(new HazardActorEmitterRefBuffer
                    {
                        EmitterEntity = GetEntity(emitter.gameObject, TransformUsageFlags.Dynamic),
                        EmitterId = math.max(1, emitter.EmitterId),
                    });
                }
            }
        }
    }
}
