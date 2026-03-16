using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public readonly struct HintResolveContext
    {
        public HintResolveContext(
            DemoShellScreenId screen,
            int stageId,
            bool paused,
            in PlayerHudSnapshotComponent hudSnapshot,
            DemoShellStageOutcomeId stageOutcome,
            in DemoShellStageResultMetrics stageResult,
            bool hasStageResult,
            float stageTimeLimitSec,
            NotificationId currentNotificationId,
            ulong stageSeenMask,
            ulong sessionSeenMask,
            float deltaSec)
        {
            Screen = screen;
            StageId = stageId;
            Paused = paused;
            HudSnapshot = hudSnapshot;
            StageOutcome = stageOutcome;
            StageResult = stageResult;
            HasStageResult = hasStageResult;
            StageTimeLimitSec = stageTimeLimitSec;
            CurrentNotificationId = currentNotificationId;
            StageSeenMask = stageSeenMask;
            SessionSeenMask = sessionSeenMask;
            DeltaSec = deltaSec;
        }

        public DemoShellScreenId Screen { get; }
        public int StageId { get; }
        public bool Paused { get; }
        public PlayerHudSnapshotComponent HudSnapshot { get; }
        public DemoShellStageOutcomeId StageOutcome { get; }
        public DemoShellStageResultMetrics StageResult { get; }
        public bool HasStageResult { get; }
        public float StageTimeLimitSec { get; }
        public NotificationId CurrentNotificationId { get; }
        public ulong StageSeenMask { get; }
        public ulong SessionSeenMask { get; }
        public float DeltaSec { get; }
    }

    public static class HintResolver
    {
        private readonly struct HintCandidate
        {
            public HintCandidate(HintId id, string message, int priority, float durationSec)
            {
                Id = id;
                Message = message;
                Priority = priority;
                DurationSec = durationSec;
            }

            public HintId Id { get; }
            public string Message { get; }
            public int Priority { get; }
            public float DurationSec { get; }
        }

        public static HintResolvedState Resolve(
            in HintResolveContext context,
            ref HintStageState stageState)
        {
            if (context.Paused)
            {
                return stageState.CurrentId == HintId.None || stageState.RemainingSec <= 0f
                    ? default
                    : new HintResolvedState
                    {
                        Id = stageState.CurrentId,
                        Message = GetMessage(stageState.CurrentId),
                        RemainingSec = stageState.RemainingSec,
                        Visible = context.Screen == DemoShellScreenId.StagePlay,
                    };
            }

            float deltaSec = Mathf.Max(0f, context.DeltaSec);
            if (stageState.RemainingSec > 0f)
                stageState.RemainingSec = Mathf.Max(0f, stageState.RemainingSec - deltaSec);
            if (stageState.RemainingSec <= 0f)
            {
                stageState.CurrentId = HintId.None;
                stageState.RemainingSec = 0f;
            }

            int carryCapacity = Mathf.Max(0, context.HudSnapshot.CarryCapacity);
            bool carryFull = carryCapacity > 0 && context.HudSnapshot.CarryLoad >= carryCapacity;
            bool hitVisible = context.HudSnapshot.HitFlashRemainingSec > 0f && context.HudSnapshot.LastHitLossValue > 0;

            HintCandidate bestCandidate = default;
            bool hasCandidate = false;

            if (context.Screen == DemoShellScreenId.StagePlay)
            {
                if (carryFull
                    && !HasSeen(context.StageSeenMask, HintId.CarryFullGoToDeposit)
                    && context.CurrentNotificationId != NotificationId.CarryFull)
                {
                    Consider(new HintCandidate(HintId.CarryFullGoToDeposit, GetMessage(HintId.CarryFullGoToDeposit), 1, 4f), ref bestCandidate, ref hasCandidate);
                }

                if (context.HudSnapshot.DepletedSourceCount >= context.HudSnapshot.TotalSourceCount
                    && context.HudSnapshot.TotalSourceCount > 0
                    && context.HudSnapshot.CarryLoad > 0
                    && !HasSeen(context.StageSeenMask, HintId.DepositRemainingTrash))
                {
                    Consider(new HintCandidate(HintId.DepositRemainingTrash, GetMessage(HintId.DepositRemainingTrash), 2, 4f), ref bestCandidate, ref hasCandidate);
                }

                if (context.HudSnapshot.DepletedSourceCount < context.HudSnapshot.TotalSourceCount
                    && !carryFull
                    && !HasSeen(context.StageSeenMask, HintId.CollectFromSources))
                {
                    Consider(new HintCandidate(HintId.CollectFromSources, GetMessage(HintId.CollectFromSources), 3, 4f), ref bestCandidate, ref hasCandidate);
                }

                if (hitVisible
                    && !HasSeen(context.SessionSeenMask, HintId.FirstHitAvoidHazards)
                    && context.CurrentNotificationId != NotificationId.HitCarryLost)
                {
                    Consider(new HintCandidate(HintId.FirstHitAvoidHazards, GetMessage(HintId.FirstHitAvoidHazards), 4, 4f), ref bestCandidate, ref hasCandidate);
                }
            }
            else if (context.Screen == DemoShellScreenId.StageResult
                     && context.HasStageResult
                     && context.StageOutcome == DemoShellStageOutcomeId.Fail)
            {
                bool timeUp = context.StageTimeLimitSec > 0f
                    && context.StageResult.ElapsedSec >= context.StageTimeLimitSec;

                if (timeUp)
                {
                    Consider(new HintCandidate(HintId.FailTimeoutMoveFaster, GetMessage(HintId.FailTimeoutMoveFaster), 5, 4f), ref bestCandidate, ref hasCandidate);
                }
                else if (context.StageResult.HitValue >= 3)
                {
                    Consider(new HintCandidate(HintId.FailHighHitKeepDistance, GetMessage(HintId.FailHighHitKeepDistance), 5, 4f), ref bestCandidate, ref hasCandidate);
                }
            }

            if (hasCandidate)
            {
                bool shouldReplace = stageState.CurrentId == HintId.None || stageState.RemainingSec <= 0f || bestCandidate.Priority <= GetPriority(stageState.CurrentId);
                if (shouldReplace)
                {
                    stageState.CurrentId = bestCandidate.Id;
                    stageState.RemainingSec = bestCandidate.DurationSec;
                }
            }

            stageState.PreviousCarryFull = carryFull;
            stageState.PreviousHitVisible = hitVisible;
            stageState.LastScreen = context.Screen;

            if (stageState.CurrentId == HintId.None || stageState.RemainingSec <= 0f)
                return default;

            return new HintResolvedState
            {
                Id = stageState.CurrentId,
                Message = GetMessage(stageState.CurrentId),
                RemainingSec = stageState.RemainingSec,
                Visible = context.Screen == DemoShellScreenId.StagePlay,
            };
        }

        public static bool HasSeen(ulong mask, HintId id)
        {
            if (id == HintId.None)
                return false;

            ulong flag = 1UL << (int)id;
            return (mask & flag) != 0UL;
        }

        public static ulong MarkSeen(ulong mask, HintId id)
        {
            if (id == HintId.None)
                return mask;

            return mask | (1UL << (int)id);
        }

        public static int GetPriority(HintId id)
        {
            return id switch
            {
                HintId.CarryFullGoToDeposit => 1,
                HintId.DepositRemainingTrash => 2,
                HintId.CollectFromSources => 3,
                HintId.FirstHitAvoidHazards => 4,
                HintId.FailTimeoutMoveFaster or HintId.FailHighHitKeepDistance => 5,
                _ => int.MaxValue,
            };
        }

        public static bool IsSessionScoped(HintId id)
        {
            return id == HintId.FirstHitAvoidHazards;
        }

        public static bool IsStageScoped(HintId id)
        {
            return id == HintId.CarryFullGoToDeposit
                || id == HintId.CollectFromSources
                || id == HintId.DepositRemainingTrash
                || id == HintId.StageStartMoveAndCollect;
        }

        public static string GetMessage(HintId id)
        {
            return id switch
            {
                HintId.StageStartMoveAndCollect => "Collect trash from active sources.",
                HintId.CarryFullGoToDeposit => "Carry is full. Head to Deposit.",
                HintId.CollectFromSources => "Collect trash from active sources.",
                HintId.DepositRemainingTrash => "Return remaining trash to Deposit.",
                HintId.FirstHitAvoidHazards => "Avoid hazards to keep your carry.",
                HintId.FailTimeoutMoveFaster => "Move faster between Source and Deposit.",
                HintId.FailHighHitKeepDistance => "Keep distance from hazards while carrying.",
                _ => string.Empty,
            };
        }

        private static void Consider(in HintCandidate candidate, ref HintCandidate bestCandidate, ref bool hasCandidate)
        {
            if (candidate.Id == HintId.None)
                return;

            if (!hasCandidate || candidate.Priority < bestCandidate.Priority)
            {
                bestCandidate = candidate;
                hasCandidate = true;
            }
        }
    }
}
