using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StagePresentationCatalogValidationRules
    {
        public static void ValidateCatalogRecords(
            IReadOnlyList<ContentValidationRecord<StagePresentationCatalogSO>> catalogs,
            IReadOnlyList<ContentValidationRecord<StageLayoutSO>> layouts,
            List<ContentValidationIssue> issues)
        {
            if (catalogs == null || issues == null)
                return;

            var entriesByKey = new Dictionary<string, StagePresentationCatalogEntry>(StringComparer.Ordinal);
            var ownersByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < catalogs.Count; i++)
            {
                var record = catalogs[i];
                if (record.Value == null)
                    continue;

                ValidateCatalog(record.Value, record.Location, issues, entriesByKey, ownersByKey);
            }

            foreach (var pair in ownersByKey)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SPC001",
                        pair.Value[i],
                        $"Duplicate PresentationKey detected: {pair.Key}. Owners: {joined}"));
                }
            }

            ValidateLayoutUsage(layouts, entriesByKey, issues);
        }

        private static void ValidateCatalog(
            StagePresentationCatalogSO catalog,
            string locationPrefix,
            List<ContentValidationIssue> issues,
            Dictionary<string, StagePresentationCatalogEntry> entriesByKey,
            Dictionary<string, List<string>> ownersByKey)
        {
            var entries = catalog.Entries ?? Array.Empty<StagePresentationCatalogEntry>();
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = $"{locationPrefix}::Entries[{i}]";
                if (string.IsNullOrWhiteSpace(entry.PresentationKey))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SPC002",
                        location,
                        "PresentationKey is empty."));
                    continue;
                }

                string key = entry.PresentationKey.Trim();
                if (!ownersByKey.TryGetValue(key, out var owners))
                {
                    owners = new List<string>(2);
                    ownersByKey.Add(key, owners);
                }

                owners.Add(location);
                if (!entriesByKey.ContainsKey(key))
                    entriesByKey.Add(key, entry);

                if (entry.Prefab == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SPC003",
                        location,
                        $"Prefab is null for PresentationKey={key}."));
                }

                if (entry.Usage == StagePresentationUsageFlags.None)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "SPC004",
                        location,
                        $"UsageFlags is None for PresentationKey={key}."));
                }
            }
        }

        private static void ValidateLayoutUsage(
            IReadOnlyList<ContentValidationRecord<StageLayoutSO>> layouts,
            IReadOnlyDictionary<string, StagePresentationCatalogEntry> entriesByKey,
            List<ContentValidationIssue> issues)
        {
            if (layouts == null)
                return;

            for (int i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i].Value;
                if (layout == null || layout.Presentations == null)
                    continue;

                string prefix = layouts[i].Location;
                for (int p = 0; p < layout.Presentations.Length; p++)
                {
                    var presentation = layout.Presentations[p];
                    if (!presentation.Active || string.IsNullOrWhiteSpace(presentation.PresentationKey))
                        continue;

                    string key = presentation.PresentationKey.Trim();
                    if (!entriesByKey.TryGetValue(key, out var entry))
                        continue;

                    bool allowed = presentation.PlacementMode switch
                    {
                        StagePresentationPlacementMode.Standalone => (entry.Usage & StagePresentationUsageFlags.Standalone) != 0,
                        StagePresentationPlacementMode.LinkedToParent => presentation.LinkKind switch
                        {
                            StagePresentationLinkKind.Source => (entry.Usage & StagePresentationUsageFlags.SourceLinked) != 0,
                            StagePresentationLinkKind.Deposit => (entry.Usage & StagePresentationUsageFlags.DepositLinked) != 0,
                            StagePresentationLinkKind.Obstacle => (entry.Usage & StagePresentationUsageFlags.ObstacleLinked) != 0,
                            _ => false,
                        },
                        _ => false,
                    };

                    if (allowed)
                        continue;

                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "SPC005",
                        $"{prefix}::Presentations[{p}]",
                        $"PresentationKey usage mismatch. key={key}, usage={entry.Usage}, placement={presentation.PlacementMode}, linkKind={presentation.LinkKind}"));
                }
            }
        }
    }
}
