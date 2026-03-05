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
