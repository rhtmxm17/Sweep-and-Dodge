namespace SweepNDodge.DotsBullets
{
    public enum NotificationId : byte
    {
        None = 0,
        HitCarryLost = 1,
        TimeLow = 2,
        TimeCritical = 3,
        CarryFull = 4,
        SourceWeakened = 5,
        SourceCleared = 6,
        HazardCaptured = 7,
        HazardRemoved = 8,
        VacuumLocked = 9,
        VacuumCooldown = 10,
        StageClear = 11,
        TimeUp = 12,
    }

    public enum NotificationSeverity : byte
    {
        Info = 0,
        Warning = 1,
        Danger = 2,
    }

    public struct NotificationResolvedState
    {
        public NotificationId Id;
        public string Message;
        public NotificationSeverity Severity;
        public float RemainingSec;
        public bool Visible;
    }

    public struct NotificationRuntimeState
    {
        public NotificationId CurrentId;
        public NotificationId LastShownId;
        public float RemainingSec;
        public float CooldownUntilSec;
        public bool TimeLowLatched;
        public bool TimeCriticalLatched;
        public bool CarryFullLatched;
        public DemoShellScreenId LastScreen;
        public uint LastFeedbackVersion;
    }

    public enum HintId : byte
    {
        None = 0,
        StageStartMoveAndCollect = 1,
        CarryFullGoToDeposit = 2,
        CollectFromSources = 3,
        DepositRemainingTrash = 4,
        FirstHitAvoidHazards = 5,
        FailTimeoutMoveFaster = 6,
        FailHighHitKeepDistance = 7,
    }

    public struct HintResolvedState
    {
        public HintId Id;
        public string Message;
        public float RemainingSec;
        public bool Visible;
    }

    public struct HintStageState
    {
        public int ActiveStageId;
        public ulong StageSeenMask;
        public bool PreviousCarryFull;
        public bool PreviousHitVisible;
        public HintId LastFailureHint;
        public float RemainingSec;
        public HintId CurrentId;
        public DemoShellScreenId LastScreen;
    }
}
