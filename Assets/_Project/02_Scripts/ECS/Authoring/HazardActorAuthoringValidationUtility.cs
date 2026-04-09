using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public static class HazardActorAuthoringValidationUtility
    {
        public static bool TryValidate(
            HazardActorAuthoring authoring,
            out SourceRuntimeTemplateAuthoringBase sourceAuthoring,
            out string error)
        {
            sourceAuthoring = null;
            error = string.Empty;

            if (authoring == null)
            {
                error = "HazardActorAuthoring is null.";
                return false;
            }

            sourceAuthoring = authoring.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>(includeInactive: true);
            if (sourceAuthoring == null)
            {
                error = "HazardActorAuthoring requires a parent SourceRuntimeTemplateAuthoringBase.";
                return false;
            }

            if (authoring.ActorId < 1)
            {
                error = "HazardActorAuthoring requires ActorId >= 1.";
                return false;
            }

            return true;
        }
    }
}
