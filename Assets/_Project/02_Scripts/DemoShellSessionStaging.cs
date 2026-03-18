using System.Collections.Generic;

namespace SweepNDodge.DotsBullets
{
    public readonly struct DemoShellStartupRequest
    {
        public DemoShellStartupRequest(DemoShellScreenId screen, int stageIndex)
        {
            Screen = screen;
            StageIndex = stageIndex;
        }

        public DemoShellScreenId Screen { get; }
        public int StageIndex { get; }
    }

    public static class DemoShellSessionStaging
    {
        private static bool _hasPendingRequest;
        private static DemoShellStartupRequest _request;
        private static bool _hasSessionMetrics;
        private static DemoShellSessionMetrics _sessionMetrics;
        private static ulong _hintSessionSeenMask;
        private static bool _hasActiveStageSeen;
        private static int _activeStageSeenStageId;
        private static ulong _activeStageSeenMask;
        private static readonly HashSet<string> DialogueSeenEntryKeys = new(System.StringComparer.Ordinal);
        private static readonly Dictionary<int, int> DialogueStageAttemptCounts = new();
        private static bool _hasActiveDialogueRunSeen;
        private static int _activeDialogueRunStageId;
        private static ulong _activeDialogueRunSeenMask;

        public static bool IsStartupPending => _hasPendingRequest;
        public static ulong HintSessionSeenMask => _hintSessionSeenMask;

        public static void StageLobby()
        {
            _request = new DemoShellStartupRequest(DemoShellScreenId.Lobby, -1);
            _hasPendingRequest = true;
        }

        public static void StageStagePlay(int stageIndex)
        {
            _request = new DemoShellStartupRequest(DemoShellScreenId.StagePlay, stageIndex);
            _hasPendingRequest = true;
        }

        public static void ResetSessionMetrics()
        {
            _sessionMetrics = default;
            _hasSessionMetrics = true;
        }

        public static bool HasSessionSeenHint(HintId id)
        {
            if (id == HintId.None)
                return false;

            ulong flag = 1UL << (int)id;
            return (_hintSessionSeenMask & flag) != 0UL;
        }

        public static void MarkSessionSeenHint(HintId id)
        {
            if (id == HintId.None)
                return;

            _hintSessionSeenMask |= 1UL << (int)id;
        }

        public static void ResetHintSessionState()
        {
            _hintSessionSeenMask = 0UL;
            ClearActiveStageSeen();
        }

        public static void IncrementDialogueStageAttempt(int stageId)
        {
            if (stageId <= 0)
                return;

            if (DialogueStageAttemptCounts.TryGetValue(stageId, out int attemptCount))
            {
                DialogueStageAttemptCounts[stageId] = attemptCount + 1;
                return;
            }

            DialogueStageAttemptCounts.Add(stageId, 1);
        }

        public static void ResetDialogueStageAttempts()
        {
            DialogueStageAttemptCounts.Clear();
        }

        public static void BeginDialogueStageRun(int stageId)
        {
            if (stageId <= 0)
            {
                ClearDialogueRunSeen();
                return;
            }

            _activeDialogueRunStageId = stageId;
            _activeDialogueRunSeenMask = 0UL;
            _hasActiveDialogueRunSeen = true;
        }

        public static void ClearDialogueRunSeen()
        {
            _hasActiveDialogueRunSeen = false;
            _activeDialogueRunStageId = 0;
            _activeDialogueRunSeenMask = 0UL;
        }

        public static bool HasSeenDialogueTriggerThisRun(int stageId, InWorldDialogueTriggerId trigger)
        {
            if (!_hasActiveDialogueRunSeen || _activeDialogueRunStageId != stageId)
                return false;

            ulong flag = GetDialogueRunSeenFlag(trigger);
            return flag != 0UL && (_activeDialogueRunSeenMask & flag) != 0UL;
        }

        public static void MarkSeenDialogueTriggerThisRun(int stageId, InWorldDialogueTriggerId trigger)
        {
            if (!_hasActiveDialogueRunSeen || _activeDialogueRunStageId != stageId)
                return;

            ulong flag = GetDialogueRunSeenFlag(trigger);
            if (flag == 0UL)
                return;

            _activeDialogueRunSeenMask |= flag;
        }

        public static int GetDialogueStageAttemptCount(int stageId)
        {
            if (stageId <= 0)
                return 0;

            return DialogueStageAttemptCounts.TryGetValue(stageId, out int attemptCount)
                ? attemptCount
                : 0;
        }

        public static bool HasSeenDialogueEntry(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
                return false;

            return DialogueSeenEntryKeys.Contains(entryKey.Trim());
        }

        public static void MarkSeenDialogueEntry(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
                return;

            DialogueSeenEntryKeys.Add(entryKey.Trim());
        }

        public static void ResetDialogueSessionState()
        {
            DialogueSeenEntryKeys.Clear();
            ResetDialogueStageAttempts();
            ClearDialogueRunSeen();
        }

        public static void SetActiveStageSeen(int stageId, ulong stageSeenMask)
        {
            if (stageId <= 0 || stageSeenMask == 0UL)
            {
                ClearActiveStageSeen();
                return;
            }

            _activeStageSeenStageId = stageId;
            _activeStageSeenMask = stageSeenMask;
            _hasActiveStageSeen = true;
        }

        public static bool TryGetActiveStageSeen(int stageId, out ulong stageSeenMask)
        {
            stageSeenMask = 0UL;
            if (!_hasActiveStageSeen || _activeStageSeenStageId != stageId)
                return false;

            stageSeenMask = _activeStageSeenMask;
            return true;
        }

        public static void ClearActiveStageSeen()
        {
            _hasActiveStageSeen = false;
            _activeStageSeenStageId = 0;
            _activeStageSeenMask = 0UL;
        }

        public static void AccumulateSuccessfulStage(in DemoShellStageResultMetrics result)
        {
            if (result.Outcome != DemoShellStageOutcomeId.Clear)
                return;

            if (!_hasSessionMetrics)
                _sessionMetrics = default;

            _sessionMetrics.TotalElapsedSec += result.ElapsedSec > 0f ? result.ElapsedSec : 0f;
            _sessionMetrics.TotalCollectValue += result.CollectValue > 0 ? result.CollectValue : 0;
            _sessionMetrics.TotalCleanupValue += result.CleanupValue > 0 ? result.CleanupValue : 0;
            _sessionMetrics.TotalHitValue += result.HitValue > 0 ? result.HitValue : 0;
            _sessionMetrics.ClearedStageCount += 1;
            _hasSessionMetrics = true;
        }

        public static bool TryGetSessionMetrics(out DemoShellSessionMetrics metrics)
        {
            metrics = default;
            if (!_hasSessionMetrics)
                return false;

            metrics = _sessionMetrics;
            return true;
        }

        public static bool TryConsume(out DemoShellStartupRequest request)
        {
            request = default;
            if (!_hasPendingRequest)
                return false;

            request = _request;
            _request = default;
            _hasPendingRequest = false;
            return true;
        }

        private static ulong GetDialogueRunSeenFlag(InWorldDialogueTriggerId trigger)
        {
            return trigger switch
            {
                InWorldDialogueTriggerId.InterventionCarryFull => 1UL << 0,
                InWorldDialogueTriggerId.InterventionFirstHit => 1UL << 1,
                _ => 0UL,
            };
        }
    }
}
