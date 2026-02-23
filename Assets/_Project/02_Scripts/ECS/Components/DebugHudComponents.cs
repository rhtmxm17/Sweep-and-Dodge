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
        public int GhostInactiveRendered;
        public int RequestedRendered;
        public int ActiveHidden;
        public int NonPositiveLifeRendered;
        public float FrameTimeMs;
    }

    // 렌더-활성 불일치 추적 설정 싱글톤.
    public struct BulletRenderTraceConfigComponent : IComponentData
    {
        public byte EnableInvariantLog;
        public int MaxLogsPerFrame;
        public int MaxEntitiesToScanPerFrame; // 0 이하: 전체 스캔
    }

    // 렌더-활성 불일치 추적 결과 싱글톤.
    public struct BulletRenderTraceMetricsComponent : IComponentData
    {
        public uint Frame;
        public int Scanned;
        public int Logged;
        public int GhostInactiveRendered;
        public int RequestedRendered;
        public int ActiveHidden;
        public int NonPositiveLifeRendered;
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
