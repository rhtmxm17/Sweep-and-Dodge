using System;
using System.Collections.Generic;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class InWorldDialogueCatalogValidationRules
    {
        public static void ValidateCatalogRecords(
            IReadOnlyList<ContentValidationRecord<InWorldDialogueCatalogSO>> dialogueCatalogs,
            IReadOnlyList<ContentValidationRecord<InWorldDialogueSpeakerCatalogSO>> speakerCatalogs,
            List<ContentValidationIssue> issues)
        {
            if (issues == null)
                return;

            var speakerProfilesByKey = new Dictionary<string, InWorldDialogueSpeakerProfile>(StringComparer.Ordinal);
            var speakerKeyOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var entryOwnersByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var enabledTargetOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            if (dialogueCatalogs != null && dialogueCatalogs.Count > 1)
            {
                for (int i = 0; i < dialogueCatalogs.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD001",
                        dialogueCatalogs[i].Location,
                        "Only one InWorldDialogueCatalogSO asset is supported in v1."));
                }
            }

            if (speakerCatalogs != null && speakerCatalogs.Count > 1)
            {
                for (int i = 0; i < speakerCatalogs.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD002",
                        speakerCatalogs[i].Location,
                        "Only one InWorldDialogueSpeakerCatalogSO asset is supported in v1."));
                }
            }

            CollectSpeakerProfiles(speakerCatalogs, issues, speakerProfilesByKey, speakerKeyOwners);
            ValidateSpeakerDuplicates(issues, speakerKeyOwners);
            ValidateDialogueCatalogs(dialogueCatalogs, issues, speakerProfilesByKey, entryOwnersByKey, enabledTargetOwners);
            ValidateEntryKeyDuplicates(issues, entryOwnersByKey);
            ValidateEnabledTargetDuplicates(issues, enabledTargetOwners);
        }

        private static void CollectSpeakerProfiles(
            IReadOnlyList<ContentValidationRecord<InWorldDialogueSpeakerCatalogSO>> speakerCatalogs,
            List<ContentValidationIssue> issues,
            Dictionary<string, InWorldDialogueSpeakerProfile> speakerProfilesByKey,
            Dictionary<string, List<string>> speakerKeyOwners)
        {
            if (speakerCatalogs == null)
                return;

            for (int i = 0; i < speakerCatalogs.Count; i++)
            {
                var record = speakerCatalogs[i];
                if (record.Value == null)
                    continue;

                var profiles = record.Value.Profiles ?? Array.Empty<InWorldDialogueSpeakerProfile>();
                for (int p = 0; p < profiles.Length; p++)
                {
                    var profile = profiles[p];
                    string location = $"{record.Location}::Profiles[{p}]";
                    if (string.IsNullOrWhiteSpace(profile.SpeakerKey))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "IWD003",
                            location,
                            "SpeakerKey is empty."));
                        continue;
                    }

                    string speakerKey = profile.SpeakerKey.Trim();
                    if (!speakerKeyOwners.TryGetValue(speakerKey, out var owners))
                    {
                        owners = new List<string>(2);
                        speakerKeyOwners.Add(speakerKey, owners);
                    }

                    owners.Add(location);
                    if (!speakerProfilesByKey.ContainsKey(speakerKey))
                        speakerProfilesByKey.Add(speakerKey, profile);
                }
            }
        }

        private static void ValidateDialogueCatalogs(
            IReadOnlyList<ContentValidationRecord<InWorldDialogueCatalogSO>> dialogueCatalogs,
            List<ContentValidationIssue> issues,
            Dictionary<string, InWorldDialogueSpeakerProfile> speakerProfilesByKey,
            Dictionary<string, List<string>> entryOwnersByKey,
            Dictionary<string, List<string>> enabledTargetOwners)
        {
            if (dialogueCatalogs == null)
                return;

            for (int i = 0; i < dialogueCatalogs.Count; i++)
            {
                var record = dialogueCatalogs[i];
                if (record.Value == null)
                    continue;

                var entries = record.Value.Entries ?? Array.Empty<InWorldDialogueCatalogEntry>();
                for (int e = 0; e < entries.Length; e++)
                {
                    var entry = entries[e];
                    string location = $"{record.Location}::Entries[{e}]";
                    ValidateEntryIdentity(entry, location, issues, entryOwnersByKey, enabledTargetOwners);
                    ValidateEntryTarget(entry, location, issues);
                    ValidateEntryBlockingMode(entry, location, issues);
                    ValidateSequenceVariant(entry.FullVariant, $"{location}/FullVariant", issues, speakerProfilesByKey, requireLines: true);
                    ValidateSequenceVariant(entry.RetryVariant, $"{location}/RetryVariant", issues, speakerProfilesByKey, requireLines: false);

                    if (entry.RetryPolicy == InWorldDialogueRetryPolicy.ShortOnRetry && !entry.RetryVariant.HasLines)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Warning,
                            "IWD013",
                            location,
                            "ShortOnRetry uses FullVariant fallback when RetryVariant is empty."));
                    }
                }
            }
        }

        private static void ValidateEntryIdentity(
            in InWorldDialogueCatalogEntry entry,
            string location,
            List<ContentValidationIssue> issues,
            Dictionary<string, List<string>> entryOwnersByKey,
            Dictionary<string, List<string>> enabledTargetOwners)
        {
            if (string.IsNullOrWhiteSpace(entry.EntryKey))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "IWD004",
                    location,
                    "EntryKey is empty."));
            }
            else
            {
                string entryKey = entry.EntryKey.Trim();
                if (!entryOwnersByKey.TryGetValue(entryKey, out var owners))
                {
                    owners = new List<string>(2);
                    entryOwnersByKey.Add(entryKey, owners);
                }

                owners.Add(location);
            }

            if (!entry.Enabled)
                return;

            string targetIdentity = BuildTargetIdentity(entry);
            if (!enabledTargetOwners.TryGetValue(targetIdentity, out var targetOwners))
            {
                targetOwners = new List<string>(2);
                enabledTargetOwners.Add(targetIdentity, targetOwners);
            }

            targetOwners.Add(location);
        }

        private static void ValidateEntryTarget(
            in InWorldDialogueCatalogEntry entry,
            string location,
            List<ContentValidationIssue> issues)
        {
            if (IsInterventionTrigger(entry.Trigger) && entry.TargetKind == InWorldDialogueTargetKind.Theme)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "IWD019",
                    location,
                    $"Intervention trigger only allows Stage or Global target. trigger={entry.Trigger}, targetKind={entry.TargetKind}"));
                return;
            }

            bool hasThemeKey = !string.IsNullOrWhiteSpace(entry.ThemeKey);
            switch (entry.TargetKind)
            {
                case InWorldDialogueTargetKind.Stage:
                    if (entry.StageId < 1 || hasThemeKey)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "IWD005",
                            location,
                            $"Stage target requires StageId >= 1 and empty ThemeKey. stageId={entry.StageId}, hasThemeKey={hasThemeKey}"));
                    }
                    break;
                case InWorldDialogueTargetKind.Theme:
                    if (entry.StageId != 0 || !hasThemeKey)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "IWD006",
                            location,
                            $"Theme target requires StageId == 0 and non-empty ThemeKey. stageId={entry.StageId}, hasThemeKey={hasThemeKey}"));
                    }
                    break;
                case InWorldDialogueTargetKind.Global:
                    if (entry.StageId != 0 || hasThemeKey)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "IWD007",
                            location,
                            $"Global target requires StageId == 0 and empty ThemeKey. stageId={entry.StageId}, hasThemeKey={hasThemeKey}"));
                    }
                    break;
                default:
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD008",
                        location,
                        $"Unsupported TargetKind value={entry.TargetKind}."));
                    break;
            }
        }

        private static void ValidateEntryBlockingMode(
            in InWorldDialogueCatalogEntry entry,
            string location,
            List<ContentValidationIssue> issues)
        {
            if (IsInterventionTrigger(entry.Trigger))
            {
                if (entry.BlockingMode != InWorldDialogueBlockingMode.OverlayOnly)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD020",
                        location,
                        $"Intervention trigger only allows OverlayOnly. trigger={entry.Trigger}, blockingMode={entry.BlockingMode}"));
                }

                return;
            }

            if (entry.BlockingMode == InWorldDialogueBlockingMode.GateClear
                && entry.Trigger != InWorldDialogueTriggerId.StageClear)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "IWD014",
                    location,
                    $"GateClear is only valid for StageClear. trigger={entry.Trigger}"));
            }

            if (entry.BlockingMode == InWorldDialogueBlockingMode.GateIntro
                && entry.Trigger != InWorldDialogueTriggerId.StageStart)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "IWD015",
                    location,
                    $"GateIntro is only valid for StageStart. trigger={entry.Trigger}"));
            }
        }

        private static void ValidateSequenceVariant(
            in InWorldDialogueSequenceVariant variant,
            string location,
            List<ContentValidationIssue> issues,
            Dictionary<string, InWorldDialogueSpeakerProfile> speakerProfilesByKey,
            bool requireLines)
        {
            var lines = variant.Lines ?? Array.Empty<InWorldDialogueLine>();
            if (requireLines && (lines.Length < 1 || lines.Length > 3))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "IWD009",
                    location,
                    $"Variant must contain 1..3 lines. current={lines.Length}"));
            }

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                string lineLocation = $"{location}/Lines[{i}]";
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD010",
                        lineLocation,
                        "Text is empty."));
                }

                if (string.IsNullOrWhiteSpace(line.SpeakerKey))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD011",
                        lineLocation,
                        "SpeakerKey is empty."));
                    continue;
                }

                string speakerKey = line.SpeakerKey.Trim();
                if (!speakerProfilesByKey.ContainsKey(speakerKey))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD012",
                        lineLocation,
                        $"SpeakerKey was not found in InWorldDialogueSpeakerCatalogSO. key={speakerKey}"));
                }
            }
        }

        private static void ValidateSpeakerDuplicates(
            List<ContentValidationIssue> issues,
            Dictionary<string, List<string>> speakerKeyOwners)
        {
            foreach (var pair in speakerKeyOwners)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD016",
                        pair.Value[i],
                        $"Duplicate SpeakerKey detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateEntryKeyDuplicates(
            List<ContentValidationIssue> issues,
            Dictionary<string, List<string>> entryOwnersByKey)
        {
            foreach (var pair in entryOwnersByKey)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD017",
                        pair.Value[i],
                        $"Duplicate EntryKey detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateEnabledTargetDuplicates(
            List<ContentValidationIssue> issues,
            Dictionary<string, List<string>> enabledTargetOwners)
        {
            foreach (var pair in enabledTargetOwners)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "IWD018",
                        pair.Value[i],
                        $"Duplicate enabled dialogue target detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static string BuildTargetIdentity(in InWorldDialogueCatalogEntry entry)
        {
            return entry.TargetKind switch
            {
                InWorldDialogueTargetKind.Stage => $"{entry.Trigger}|Stage|{entry.StageId}",
                InWorldDialogueTargetKind.Theme => $"{entry.Trigger}|Theme|{entry.ThemeKey?.Trim() ?? string.Empty}",
                InWorldDialogueTargetKind.Global => $"{entry.Trigger}|Global",
                _ => $"{entry.Trigger}|Unknown|{(int)entry.TargetKind}",
            };
        }

        private static bool IsInterventionTrigger(InWorldDialogueTriggerId trigger)
        {
            return trigger == InWorldDialogueTriggerId.InterventionCarryFull
                || trigger == InWorldDialogueTriggerId.InterventionFirstHit;
        }
    }
}
