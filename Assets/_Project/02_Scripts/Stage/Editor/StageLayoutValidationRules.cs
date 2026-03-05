using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageLayoutValidationRules
    {
        public static void ValidateCatalogRecords(
            IReadOnlyList<ContentValidationRecord<StageMapCatalogSO>> catalogs,
            List<ContentValidationIssue> issues)
        {
            if (catalogs == null || issues == null)
                return;

            for (int i = 0; i < catalogs.Count; i++)
            {
                var record = catalogs[i];
                if (record.Value == null)
                    continue;

                ValidateDefinitions(record.Value.Stages, record.Location, issues);
            }
        }

        public static void ValidateDefinitions(
            IReadOnlyList<StageMapDefinition> definitions,
            string locationPrefix,
            List<ContentValidationIssue> issues)
        {
            if (issues == null)
                return;

            if (definitions == null)
                definitions = Array.Empty<StageMapDefinition>();

            var stageOwnersById = new Dictionary<int, List<string>>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var stage = definitions[i];
                string stageLocation = BuildStageLocation(locationPrefix, i, stage.StageId);

                if (stage.StageId <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG001",
                        stageLocation,
                        $"StageId must be >= 1. current={stage.StageId}"));
                }

                if (!stageOwnersById.TryGetValue(stage.StageId, out var owners))
                {
                    owners = new List<string>(2);
                    stageOwnersById.Add(stage.StageId, owners);
                }

                owners.Add(stageLocation);

                ValidateSourceEntries(stage.Sources, stageLocation, issues);
                ValidateDepositEntries(stage.Deposits, stageLocation, issues);
                ValidateObstacleEntries(stage.Obstacles, stageLocation, issues);
                ValidateVisualEntries(stage.Visuals, stageLocation, issues);

                bool hasSource = stage.Sources != null && stage.Sources.Length > 0;
                bool hasDeposit = stage.Deposits != null && stage.Deposits.Length > 0;
                if (!hasSource || !hasDeposit)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "STG006",
                        stageLocation,
                        "Stage should include at least one Source and one Deposit."));
                }

                bool hasAnyElement = (stage.Sources?.Length ?? 0)
                    + (stage.Deposits?.Length ?? 0)
                    + (stage.Obstacles?.Length ?? 0)
                    + (stage.Visuals?.Length ?? 0) > 0;
                if (hasAnyElement && IsAllInactive(stage))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "STG008",
                        stageLocation,
                        "Stage has layout elements but all are inactive."));
                }
            }

            foreach (var pair in stageOwnersById)
            {
                if (pair.Key <= 0 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG002",
                        pair.Value[i],
                        $"Duplicate StageId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateSourceEntries(
            StageSourceLayoutData[] entries,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Source", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Sources[{i}]";
                if (entry.StableId == 0)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG004", location, "StableId must be >= 1."));
                }

                if (entry.FieldRadius < 0f || entry.FieldSize.x < 0f || entry.FieldSize.y < 0f)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG005",
                        location,
                        "Source field radius/size must be >= 0."));
                }
            }
        }

        private static void ValidateDepositEntries(
            StageDepositLayoutData[] entries,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Deposit", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Deposits[{i}]";
                if (entry.StableId == 0)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG004", location, "StableId must be >= 1."));
                }

                if (entry.Radius < 0f)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG005",
                        location,
                        "Deposit radius must be >= 0."));
                }
            }
        }

        private static void ValidateObstacleEntries(
            StageObstacleLayoutData[] entries,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Obstacle", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Obstacles[{i}]";
                if (entry.StableId == 0)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG004", location, "StableId must be >= 1."));
                }

                bool invalid = entry.Shape == StageMapElementShape.Circle
                    ? entry.Radius < 0f
                    : (entry.Size.x < 0f || entry.Size.y < 0f);
                if (invalid)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG005",
                        location,
                        "Obstacle shape parameters must be >= 0."));
                }
            }
        }

        private static void ValidateVisualEntries(
            StageVisualLayoutData[] entries,
            string stageLocation,
            List<ContentValidationIssue> issues)
        {
            ValidateStableIdUniqueness(entries, stageLocation, "Visual", issues);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{stageLocation}/Visuals[{i}]";
                if (entry.StableId == 0)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STG004", location, "StableId must be >= 1."));
                }

                if (string.IsNullOrWhiteSpace(entry.VisualKey))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "STG007",
                        location,
                        "VisualKey is empty."));
                }
            }
        }

        private static void ValidateStableIdUniqueness<T>(
            T[] entries,
            string stageLocation,
            string category,
            List<ContentValidationIssue> issues) where T : struct
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
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STG003",
                        pair.Value[i],
                        $"Duplicate StableId detected in same stage and category. stableId={pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static bool IsAllInactive(in StageMapDefinition stage)
        {
            if (stage.Sources != null)
            {
                for (int i = 0; i < stage.Sources.Length; i++)
                {
                    if (stage.Sources[i].Active)
                        return false;
                }
            }

            if (stage.Deposits != null)
            {
                for (int i = 0; i < stage.Deposits.Length; i++)
                {
                    if (stage.Deposits[i].Active)
                        return false;
                }
            }

            if (stage.Obstacles != null)
            {
                for (int i = 0; i < stage.Obstacles.Length; i++)
                {
                    if (stage.Obstacles[i].Active)
                        return false;
                }
            }

            if (stage.Visuals != null)
            {
                for (int i = 0; i < stage.Visuals.Length; i++)
                {
                    if (stage.Visuals[i].Active)
                        return false;
                }
            }

            return true;
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

        private static string BuildStageLocation(string prefix, int stageIndex, int stageId)
        {
            return $"{prefix}::Stage[{stageIndex}] (StageId={stageId})";
        }
    }
}
