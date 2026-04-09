using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public static class HazardEmitterAuthoringValidationUtility
    {
        public static bool TryValidate(
            HazardEmitterAuthoring authoring,
            out HazardActorAuthoring actorAuthoring,
            out SourceRuntimeTemplateAuthoringBase sourceAuthoring,
            out string error)
        {
            actorAuthoring = null;
            sourceAuthoring = null;
            error = string.Empty;

            if (authoring == null)
            {
                error = "HazardEmitterAuthoring is null.";
                return false;
            }

            actorAuthoring = authoring.GetComponentInParent<HazardActorAuthoring>(includeInactive: true);
            if (actorAuthoring == null)
            {
                error = "HazardEmitterAuthoring requires a parent HazardActorAuthoring.";
                return false;
            }

            sourceAuthoring = actorAuthoring.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>(includeInactive: true);
            if (sourceAuthoring == null)
            {
                error = "HazardEmitterAuthoring requires its parent HazardActorAuthoring to be under a SourceRuntimeTemplateAuthoringBase.";
                return false;
            }

            if (authoring.ActivationPolicy != HazardEmitterActivationPolicyId.AlwaysCycle)
            {
                error = $"Plan D only supports ActivationPolicy={HazardEmitterActivationPolicyId.AlwaysCycle}.";
                return false;
            }

            if (authoring.AnchorKind != HazardEmitterAnchorKindId.ObjectBound)
            {
                error = $"Plan D only supports AnchorKind={HazardEmitterAnchorKindId.ObjectBound}.";
                return false;
            }

            if (authoring.Mobility != HazardEmitterMobilityId.Static)
            {
                error = $"Plan D only supports Mobility={HazardEmitterMobilityId.Static}.";
                return false;
            }

            if (authoring.TelegraphProfile == null)
            {
                error = "HazardEmitterAuthoring is missing TelegraphProfile.";
                return false;
            }

            if (authoring.EmissionProfile == null)
            {
                error = "HazardEmitterAuthoring is missing EmissionProfile.";
                return false;
            }

            if (authoring.TelegraphProfile.TelegraphDurationSec < 0f)
            {
                error = "Hazard emitter telegraph duration must be non-negative.";
                return false;
            }

            if (!HazardEmitterProfileResolver.TryResolve(authoring.EmissionProfile, out _, out error))
                return false;

            return true;
        }
    }
}
