using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public readonly struct NotificationResolveContext
    {
        public NotificationResolveContext(
            DemoShellScreenId screen,
            float stageTimeLimitSec,
            in PlayerHudSnapshotComponent hudSnapshot,
            bool hasFeedbackSnapshot,
            in PlayerUiFeedbackPresentationSnapshotComponent feedbackSnapshot,
            string feedbackLine,
            bool justStageClear,
            bool justTimeUp,
            float deltaSec)
        {
            Screen = screen;
            StageTimeLimitSec = stageTimeLimitSec;
            HudSnapshot = hudSnapshot;
            HasFeedbackSnapshot = hasFeedbackSnapshot;
            FeedbackSnapshot = feedbackSnapshot;
            FeedbackLine = feedbackLine;
            JustStageClear = justStageClear;
            JustTimeUp = justTimeUp;
            DeltaSec = deltaSec;
        }

        public DemoShellScreenId Screen { get; }
        public float StageTimeLimitSec { get; }
        public PlayerHudSnapshotComponent HudSnapshot { get; }
        public bool HasFeedbackSnapshot { get; }
        public PlayerUiFeedbackPresentationSnapshotComponent FeedbackSnapshot { get; }
        public string FeedbackLine { get; }
        public bool JustStageClear { get; }
        public bool JustTimeUp { get; }
        public float DeltaSec { get; }
    }

    public static class NotificationResolver
    {
        private readonly struct NotificationCandidate
        {
            public NotificationCandidate(
                NotificationId id,
                string message,
                NotificationSeverity severity,
                int priority,
                float durationSec,
                bool isFeedbackCandidate)
            {
                Id = id;
                Message = message;
                Severity = severity;
                Priority = priority;
                DurationSec = durationSec;
                IsFeedbackCandidate = isFeedbackCandidate;
            }

            public NotificationId Id { get; }
            public string Message { get; }
            public NotificationSeverity Severity { get; }
            public int Priority { get; }
            public float DurationSec { get; }
            public bool IsFeedbackCandidate { get; }
        }

        public static NotificationResolvedState Resolve(
            in NotificationResolveContext context,
            ref NotificationRuntimeState runtimeState)
        {
            float deltaSec = Mathf.Max(0f, context.DeltaSec);
            if (runtimeState.RemainingSec > 0f)
                runtimeState.RemainingSec = Mathf.Max(0f, runtimeState.RemainingSec - deltaSec);
            if (runtimeState.CooldownUntilSec > 0f)
                runtimeState.CooldownUntilSec = Mathf.Max(0f, runtimeState.CooldownUntilSec - deltaSec);

            UpdateLatches(in context, ref runtimeState);

            bool justStageResultTransition = context.JustStageClear || context.JustTimeUp;
            if (context.Screen != DemoShellScreenId.StagePlay && !justStageResultTransition)
            {
                runtimeState.CurrentId = NotificationId.None;
                runtimeState.RemainingSec = 0f;
                runtimeState.LastScreen = context.Screen;
                return default;
            }

            NotificationCandidate bestCandidate = default;
            bool hasCandidate = false;

            AddStageResultCandidate(in context, ref bestCandidate, ref hasCandidate);
            AddDerivedCandidates(in context, ref runtimeState, ref bestCandidate, ref hasCandidate);
            AddFeedbackCandidate(in context, ref runtimeState, ref bestCandidate, ref hasCandidate);

            if (hasCandidate)
            {
                bool forceReplace = justStageResultTransition;
                bool shouldReplace = forceReplace
                    || runtimeState.CurrentId == NotificationId.None
                    || runtimeState.RemainingSec <= 0f
                    || bestCandidate.Priority <= GetPriority(runtimeState.CurrentId);

                if (shouldReplace)
                {
                    runtimeState.CurrentId = bestCandidate.Id;
                    runtimeState.LastShownId = bestCandidate.Id;
                    runtimeState.RemainingSec = bestCandidate.DurationSec;
                    if (bestCandidate.IsFeedbackCandidate && context.HasFeedbackSnapshot)
                        runtimeState.LastFeedbackVersion = context.FeedbackSnapshot.Version;
                }
            }

            runtimeState.LastScreen = context.Screen;
            if (runtimeState.CurrentId == NotificationId.None || runtimeState.RemainingSec <= 0f)
            {
                runtimeState.CurrentId = NotificationId.None;
                runtimeState.RemainingSec = 0f;
                return default;
            }

            return new NotificationResolvedState
            {
                Id = runtimeState.CurrentId,
                Message = GetMessage(runtimeState.CurrentId),
                Severity = GetSeverity(runtimeState.CurrentId),
                RemainingSec = runtimeState.RemainingSec,
                Visible = true,
            };
        }

        private static void UpdateLatches(in NotificationResolveContext context, ref NotificationRuntimeState runtimeState)
        {
            float remainingSec = ResolveRemainingSec(context.StageTimeLimitSec, context.HudSnapshot.StageStateElapsedSec);
            if (remainingSec < 0f || remainingSec > 30f)
                runtimeState.TimeLowLatched = false;
            if (remainingSec < 0f || remainingSec > 10f)
                runtimeState.TimeCriticalLatched = false;

            int carryCapacity = Mathf.Max(0, context.HudSnapshot.CarryCapacity);
            bool carryFull = carryCapacity > 0 && context.HudSnapshot.CarryLoad >= carryCapacity;
            if (!carryFull)
                runtimeState.CarryFullLatched = false;
        }

        private static void AddStageResultCandidate(
            in NotificationResolveContext context,
            ref NotificationCandidate bestCandidate,
            ref bool hasCandidate)
        {
            if (context.JustTimeUp)
            {
                ConsiderCandidate(
                    new NotificationCandidate(NotificationId.TimeUp, GetMessage(NotificationId.TimeUp), NotificationSeverity.Danger, GetPriority(NotificationId.TimeUp), 1.4f, false),
                    ref bestCandidate,
                    ref hasCandidate);
                return;
            }

            if (context.JustStageClear)
            {
                ConsiderCandidate(
                    new NotificationCandidate(NotificationId.StageClear, GetMessage(NotificationId.StageClear), NotificationSeverity.Info, GetPriority(NotificationId.StageClear), 1.4f, false),
                    ref bestCandidate,
                    ref hasCandidate);
            }
        }

        private static void AddDerivedCandidates(
            in NotificationResolveContext context,
            ref NotificationRuntimeState runtimeState,
            ref NotificationCandidate bestCandidate,
            ref bool hasCandidate)
        {
            int carryCapacity = Mathf.Max(0, context.HudSnapshot.CarryCapacity);
            bool carryFull = carryCapacity > 0 && context.HudSnapshot.CarryLoad >= carryCapacity;
            float remainingSec = ResolveRemainingSec(context.StageTimeLimitSec, context.HudSnapshot.StageStateElapsedSec);

            if (context.HudSnapshot.HitFlashRemainingSec > 0f && context.HudSnapshot.LastHitLossValue > 0)
            {
                ConsiderCandidate(
                    new NotificationCandidate(NotificationId.HitCarryLost, GetMessage(NotificationId.HitCarryLost), NotificationSeverity.Danger, GetPriority(NotificationId.HitCarryLost), 1.5f, false),
                    ref bestCandidate,
                    ref hasCandidate);
            }

            if (remainingSec >= 0f && remainingSec <= 10f && !runtimeState.TimeCriticalLatched)
            {
                runtimeState.TimeCriticalLatched = true;
                ConsiderCandidate(
                    new NotificationCandidate(NotificationId.TimeCritical, GetMessage(NotificationId.TimeCritical), NotificationSeverity.Danger, GetPriority(NotificationId.TimeCritical), 1.5f, false),
                    ref bestCandidate,
                    ref hasCandidate);
            }

            if (carryFull && !runtimeState.CarryFullLatched)
            {
                runtimeState.CarryFullLatched = true;
                ConsiderCandidate(
                    new NotificationCandidate(NotificationId.CarryFull, GetMessage(NotificationId.CarryFull), NotificationSeverity.Danger, GetPriority(NotificationId.CarryFull), 1.5f, false),
                    ref bestCandidate,
                    ref hasCandidate);
            }

            if (remainingSec > 10f && remainingSec <= 30f && !runtimeState.TimeLowLatched)
            {
                runtimeState.TimeLowLatched = true;
                ConsiderCandidate(
                    new NotificationCandidate(NotificationId.TimeLow, GetMessage(NotificationId.TimeLow), NotificationSeverity.Warning, GetPriority(NotificationId.TimeLow), 1.5f, false),
                    ref bestCandidate,
                    ref hasCandidate);
            }
        }

        private static void AddFeedbackCandidate(
            in NotificationResolveContext context,
            ref NotificationRuntimeState runtimeState,
            ref NotificationCandidate bestCandidate,
            ref bool hasCandidate)
        {
            if (!context.HasFeedbackSnapshot
                || context.FeedbackSnapshot.Version == 0u
                || context.FeedbackSnapshot.RemainingSec <= 0f
                || context.FeedbackSnapshot.Version == runtimeState.LastFeedbackVersion)
            {
                return;
            }

            var candidate = ResolveFeedbackCandidate(context.FeedbackSnapshot);
            if (candidate.Id == NotificationId.None)
                return;

            ConsiderCandidate(candidate, ref bestCandidate, ref hasCandidate);
        }

        private static NotificationCandidate ResolveFeedbackCandidate(in PlayerUiFeedbackPresentationSnapshotComponent snapshot)
        {
            return snapshot.Type switch
            {
                PlayerUiFeedbackEventType.PlayerHazardHit => new NotificationCandidate(NotificationId.HitCarryLost, GetMessage(NotificationId.HitCarryLost), NotificationSeverity.Danger, GetPriority(NotificationId.HitCarryLost), 1.5f, true),
                PlayerUiFeedbackEventType.SourceStateChanged when snapshot.Reason == (byte)PlayerUiFeedbackReasonId.SourceToWeakened
                    => new NotificationCandidate(NotificationId.SourceWeakened, GetMessage(NotificationId.SourceWeakened), NotificationSeverity.Warning, GetPriority(NotificationId.SourceWeakened), 1.3f, true),
                PlayerUiFeedbackEventType.SourceStateChanged when snapshot.Reason == (byte)PlayerUiFeedbackReasonId.SourceToDepleted
                    => new NotificationCandidate(NotificationId.SourceCleared, GetMessage(NotificationId.SourceCleared), NotificationSeverity.Info, GetPriority(NotificationId.SourceCleared), 1.3f, true),
                PlayerUiFeedbackEventType.HazardCaptured
                    => new NotificationCandidate(NotificationId.HazardCaptured, GetMessage(NotificationId.HazardCaptured), NotificationSeverity.Info, GetPriority(NotificationId.HazardCaptured), 1.2f, true),
                PlayerUiFeedbackEventType.HazardRemoved
                    => new NotificationCandidate(NotificationId.HazardRemoved, GetMessage(NotificationId.HazardRemoved), NotificationSeverity.Info, GetPriority(NotificationId.HazardRemoved), 1.2f, true),
                PlayerUiFeedbackEventType.VacuumStartBlocked when snapshot.Reason == (byte)PlayerUiFeedbackReasonId.CarryBinFull
                    => new NotificationCandidate(NotificationId.CarryFull, GetMessage(NotificationId.CarryFull), NotificationSeverity.Danger, GetPriority(NotificationId.CarryFull), 1.5f, true),
                PlayerUiFeedbackEventType.VacuumStartBlocked when snapshot.Reason == (byte)PlayerUiFeedbackReasonId.VacuumLocked
                    => new NotificationCandidate(NotificationId.VacuumLocked, GetMessage(NotificationId.VacuumLocked), NotificationSeverity.Warning, GetPriority(NotificationId.VacuumLocked), 1.2f, true),
                PlayerUiFeedbackEventType.VacuumStartBlocked when snapshot.Reason == (byte)PlayerUiFeedbackReasonId.CooldownActive
                    => new NotificationCandidate(NotificationId.VacuumCooldown, GetMessage(NotificationId.VacuumCooldown), NotificationSeverity.Warning, GetPriority(NotificationId.VacuumCooldown), 1.2f, true),
                _ => default,
            };
        }

        private static void ConsiderCandidate(
            in NotificationCandidate candidate,
            ref NotificationCandidate bestCandidate,
            ref bool hasCandidate)
        {
            if (candidate.Id == NotificationId.None)
                return;

            if (!hasCandidate || candidate.Priority < bestCandidate.Priority)
            {
                bestCandidate = candidate;
                hasCandidate = true;
            }
        }

        public static float ResolveRemainingSec(float stageTimeLimitSec, float elapsedSec)
        {
            if (stageTimeLimitSec <= 0f)
                return -1f;

            return Mathf.Max(0f, stageTimeLimitSec - elapsedSec);
        }

        public static int GetPriority(NotificationId id)
        {
            return id switch
            {
                NotificationId.HitCarryLost => 1,
                NotificationId.TimeCritical => 2,
                NotificationId.CarryFull => 3,
                NotificationId.TimeLow => 4,
                NotificationId.SourceWeakened => 5,
                NotificationId.SourceCleared => 6,
                NotificationId.VacuumLocked or NotificationId.VacuumCooldown => 7,
                NotificationId.HazardCaptured or NotificationId.HazardRemoved => 8,
                NotificationId.StageClear or NotificationId.TimeUp => 9,
                _ => int.MaxValue,
            };
        }

        public static NotificationSeverity GetSeverity(NotificationId id)
        {
            return id switch
            {
                NotificationId.HitCarryLost or NotificationId.TimeCritical or NotificationId.CarryFull or NotificationId.TimeUp => NotificationSeverity.Danger,
                NotificationId.TimeLow or NotificationId.SourceWeakened or NotificationId.VacuumLocked or NotificationId.VacuumCooldown => NotificationSeverity.Warning,
                _ => NotificationSeverity.Info,
            };
        }

        public static string GetMessage(NotificationId id)
        {
            return id switch
            {
                NotificationId.HitCarryLost => "Hit! Carry lost",
                NotificationId.TimeLow => "Time is running out",
                NotificationId.TimeCritical => "Time critical",
                NotificationId.CarryFull => "Carry full - deposit now",
                NotificationId.SourceWeakened => "Source weakened",
                NotificationId.SourceCleared => "Source cleared",
                NotificationId.HazardCaptured => "Hazard captured",
                NotificationId.HazardRemoved => "Hazard removed",
                NotificationId.VacuumLocked => "Vacuum locked",
                NotificationId.VacuumCooldown => "Vacuum cooling down",
                NotificationId.StageClear => "Stage clear",
                NotificationId.TimeUp => "Time up",
                _ => string.Empty,
            };
        }
    }
}
