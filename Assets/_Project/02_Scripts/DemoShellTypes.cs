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

    [Serializable]
    public struct DemoShellStageProfile
    {
        [Min(1)] public int StageId;
        public string DisplayName;
        public bool IsFinalStage;
    }
}
