using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum DemoShellScreenId : byte
    {
        Title = 0,
        Lobby = 1,
        StagePlay = 2,
        StageResult = 3,
        DemoComplete = 4,
    }

    public enum DemoShellResultActionId : byte
    {
        NextStage = 0,
        Retry = 1,
        ReturnToLobby = 2,
    }

    public enum DemoShellStageOutcomeId : byte
    {
        Clear = 0,
        Fail = 1,
    }

    public enum DemoShellStagePlayPhaseId : byte
    {
        None = 0,
        Starting = 1,
        Running = 2,
        ClearPresentation = 3,
        AwaitingClearCompleted = 4,
    }

    public enum DemoShellPauseActionId : byte
    {
        Resume = 0,
        OpenSettings = 1,
        RestartStage = 2,
        ReturnToLobby = 3,
        QuitApplication = 4,
    }

    public enum GameplayPauseReasonId : byte
    {
        None = 0,
        PauseMenu = 1,
        DialogueGate = 2,
        Cutscene = 3,
        Debug = 4,
    }

    [Flags]
    public enum GameplayPauseFlags : byte
    {
        None = 0,
        PauseSimulation = 1 << 0,
        BlockGameplayInput = 1 << 1,
        ExclusivePresentationInput = 1 << 2,
        BlockPauseMenuOpen = 1 << 3,
    }

    [Serializable]
    public struct GameplayPauseHandle
    {
        public int Id;
        public GameplayPauseReasonId Reason;
        public GameplayPauseFlags Flags;
        public uint VersionToken;

        public bool IsValid => Id > 0 && VersionToken != 0;

        public static GameplayPauseHandle Invalid => default;
    }

    [Serializable]
    public struct GameplayPauseSnapshot
    {
        public bool IsSimulationPaused;
        public bool IsGameplayInputBlocked;
        public bool IsPresentationInputExclusive;
        public bool IsPauseMenuOpenBlocked;
        public uint ReasonMask;
        public int ActiveHandleCount;
        public uint Version;

        public static GameplayPauseSnapshot Default => default;
    }

    [Serializable]
    public struct DemoShellStageProfile
    {
        [Min(1)] public int StageId;
        public string DisplayName;
        public bool IsFinalStage;
        [Min(0f)] public float StageTimeLimitSec;
    }

    [Serializable]
    public struct DemoShellStageResultMetrics
    {
        [Min(1)] public int StageId;
        public DemoShellStageOutcomeId Outcome;
        [Min(0f)] public float ElapsedSec;
        public int CollectValue;
        public int CleanupValue;
        public int HitValue;
    }

    [Serializable]
    public struct DemoShellSessionMetrics
    {
        [Min(0f)] public float TotalElapsedSec;
        public int TotalCollectValue;
        public int TotalCleanupValue;
        public int TotalHitValue;
        public int ClearedStageCount;
    }
}
