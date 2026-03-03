using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public static class ReplayInputSuppressionUtility
    {
        public static bool IsLiveInputSuppressed(EntityManager entityManager, EntityQuery replayQuery)
        {
            if (ReplaySessionStaging.IsPlaybackStartupPending)
                return true;
            if (replayQuery.IsEmptyIgnoreFilter)
                return false;

            var control = entityManager.GetComponentData<ReplayInputControlComponent>(replayQuery.GetSingletonEntity());
            return control.Mode == ReplayInputModeId.Playback;
        }
    }
}
