using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public enum StressSwitchModeId : byte
    {
        None = 0,
        BurstOnce = 1,
        Sustain = 2,
        StopSustain = 3,
    }

    // Runtime-only metrics for debug HUD display.
    public struct DebugHudMetricsComponent : IComponentData
    {
        public int ActiveBullets;
        public int PreviousActiveBullets;
        public int SpawnedThisFrame;
        public int DespawnedThisFrame;
        public int PendingBacklog;
        public int DeferredByBudget;
        public int DeferredByPool;
        public int DroppedThisFrame;
        public int ExpiredThisFrame;
        public float FrameTimeMs;
    }

    // Debug stress command + state singleton.
    public struct StressSwitchStateComponent : IComponentData
    {
        public byte RequestExecute;
        public byte Mode;
        public int BurstCount;
        public int SustainFrames;
        public int SustainPerFrame;
        public int PreferredBulletTypeKey; // -1: auto
        public int RemainingFrames;
    }
}
