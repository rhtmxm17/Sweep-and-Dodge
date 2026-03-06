using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageLayoutValidationRules
    {
        public static void ValidateLayoutRecords(IReadOnlyList<ContentValidationRecord<StageLayoutSO>> layouts, List<ContentValidationIssue> issues)
        {
            if (layouts == null || issues == null)
                return;

            for (int i = 0; i < layouts.Count; i++)
            {
                var record = layouts[i];
                if (record.Value == null)
                    continue;

                ValidateLayout(record.Value, record.Location, issues);
            }
        }

        public static void ValidateLayout(StageLayoutSO layout, string locationPrefix, List<ContentValidationIssue> issues)
        {
            if (layout == null || issues == null)
                return;

            string stageLocation = BuildStageLocation(locationPrefix, layout.StageId);
            if (layout.StageId <= 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL001", stageLocation, $"StageId must be >= 1. current={layout.StageId}"));
            }

            ValidateSourceEntries(layout.Sources, stageLocation, issues);
            ValidateDepositEntries(layout.Deposits, stageLocation, issues);
            ValidateObstacleEntries(layout.Obstacles, stageLocation, issues);
            ValidateVisualEntries(layout.Visuals, stageLocation, issues);

            bool hasSource = layout.Sources != null && layout.Sources.Length > 0;
            bool hasDeposit = layout.Deposits != null && layout.Deposits.Length > 0;
            if (!hasSource || !hasDeposit)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STL006", stageLocation, "Stage should include at least one Source and one Deposit."));
            }

            bool hasAnyElement = (layout.Sources?.Length ?? 0)
                + (layout.Deposits?.Length ?? 0)
                + (layout.Obstacles?.Length ?? 0)
                + (layout.Visuals?.Length ?? 0) > 0;
            if (hasAnyElement && IsAllInactive(layout))
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STL008", stageLocation, "Stage has layout elements but all are inactive."));
            }
        }

        private static void ValidateSourceEntries(StageSourceLayoutData[] entries, string stageLocation, List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Source", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Sources[{i}]";
                if (entry.StableId == 0)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL004", location, "StableId must be >= 1."));
                if (entry.FieldRadius < 0f || entry.FieldSize.x < 0f || entry.FieldSize.y < 0f)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL005", location, "Source field radius/size must be >= 0."));
            }
        }

        private static void ValidateDepositEntries(StageDepositLayoutData[] entries, string stageLocation, List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Deposit", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Deposits[{i}]";
                if (entry.StableId == 0)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL004", location, "StableId must be >= 1."));
                if (entry.Radius < 0f)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL005", location, "Deposit radius must be >= 0."));
            }
        }

        private static void ValidateObstacleEntries(StageObstacleLayoutData[] entries, string stageLocation, List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Obstacle", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Obstacles[{i}]";
                if (entry.StableId == 0)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL004", location, "StableId must be >= 1."));

                bool invalid = entry.Shape == StageMapElementShape.Circle
                    ? entry.Radius < 0f
                    : (entry.Size.x < 0f || entry.Size.y < 0f);
                if (invalid)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL005", location, "Obstacle shape parameters must be >= 0."));
            }
        }

        private static void ValidateVisualEntries(StageVisualLayoutData[] entries, string stageLocation, List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Visual", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Visuals[{i}]";
                if (entry.StableId == 0)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL004", location, "StableId must be >= 1."));
                if (string.IsNullOrWhiteSpace(entry.VisualKey))
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STL007", location, "VisualKey is empty."));
            }
        }

        private static void ValidateStableIdUniqueness<T>(T[] entries, string stageLocation, string category, List<ContentValidationIssue> issues) where T : struct
        {
            if (entries == null || entries.Length <= 1)
                return;

            var ownersById = new Dictionary<uint, List<string>>();
            for (int i = 0; i < entries.Length; i++)
            {
                uint stableId = ResolveStableId(entries[i]);
                string location = $"{stageLocation}/{category}s[{i}]";
                if (!ownersById.TryGetValue(stableId, out var owners))
                {
                    owners = new List<string>(2);
                    ownersById.Add(stableId, owners);
                }

                owners.Add(location);
            }

            foreach (var pair in ownersById)
            {
                if (pair.Key == 0 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL003", pair.Value[i], $"Duplicate {category} StableId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static uint ResolveStableId<T>(in T entry) where T : struct
        {
            return entry switch
            {
                StageSourceLayoutData source => source.StableId,
                StageDepositLayoutData deposit => deposit.StableId,
                StageObstacleLayoutData obstacle => obstacle.StableId,
                StageVisualLayoutData visual => visual.StableId,
                _ => 0u,
            };
        }

        private static bool IsAllInactive(StageLayoutSO layout)
        {
            return AreAllInactive(layout.Sources)
                && AreAllInactive(layout.Deposits)
                && AreAllInactive(layout.Obstacles)
                && AreAllInactive(layout.Visuals);
        }

        private static bool AreAllInactive(StageSourceLayoutData[] entries)
        {
            if (entries == null || entries.Length == 0)
                return true;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Active)
                    return false;
            }
            return true;
        }

        private static bool AreAllInactive(StageDepositLayoutData[] entries)
        {
            if (entries == null || entries.Length == 0)
                return true;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Active)
                    return false;
            }
            return true;
        }

        private static bool AreAllInactive(StageObstacleLayoutData[] entries)
        {
            if (entries == null || entries.Length == 0)
                return true;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Active)
                    return false;
            }
            return true;
        }

        private static bool AreAllInactive(StageVisualLayoutData[] entries)
        {
            if (entries == null || entries.Length == 0)
                return true;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Active)
                    return false;
            }
            return true;
        }

        private static string BuildStageLocation(string prefix, int stageId)
        {
            return $"{prefix}::StageLayout(StageId={stageId})";
        }
    }
}
