using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace SweepNDodge.DotsBullets.Editor
{
    public sealed class StageMapDocumentMigrationPlan
    {
        internal StageMapDocumentMigrationPlan(
            StageMapDocument document,
            int sourceVersion,
            string documentSignature,
            StagePresentationCatalogSO presentationCatalog,
            string lastAppliedCatalogEntryKey,
            IReadOnlyList<ContentValidationIssue> issues,
            IReadOnlyList<StageMapApplyPlanChange> changes)
        {
            Document = document;
            SourceVersion = sourceVersion;
            DocumentSignature = documentSignature ?? string.Empty;
            PresentationCatalog = presentationCatalog;
            LastAppliedCatalogEntryKey = lastAppliedCatalogEntryKey ?? string.Empty;
            Issues = issues ?? Array.Empty<ContentValidationIssue>();
            Changes = changes ?? Array.Empty<StageMapApplyPlanChange>();
        }

        public StageMapDocument Document { get; }
        public int SourceVersion { get; }
        public int TargetVersion => StageMapDocument.CurrentSchemaVersion;
        public string DocumentSignature { get; }
        public StagePresentationCatalogSO PresentationCatalog { get; }
        public string LastAppliedCatalogEntryKey { get; }
        public IReadOnlyList<ContentValidationIssue> Issues { get; }
        public IReadOnlyList<StageMapApplyPlanChange> Changes { get; }
        public bool HasErrors => Issues.Any(x => x.Severity == ContentValidationSeverity.Error);
        public bool HasChanges => Changes.Count > 0;
    }

    /// <summary>
    /// Owns explicit StageMapDocument schema migration. Asset load never mutates documents.
    /// </summary>
    public static class StageMapDocumentMigrationUtility
    {
        public static StageMapDocumentMigrationPlan BuildPreview(StageMapDocument document)
        {
            var issues = new List<ContentValidationIssue>(4);
            var changes = new List<StageMapApplyPlanChange>(4);
            if (document == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMM900",
                    "(null)",
                    "StageMapDocument is null."));
                return new StageMapDocumentMigrationPlan(null, 0, string.Empty, null, string.Empty, issues, changes);
            }

            string location = BuildLocation(document);
            int sourceVersion = document.SchemaVersion;
            if (sourceVersion == StageMapDocument.CurrentSchemaVersion)
            {
                return new StageMapDocumentMigrationPlan(
                    document,
                    sourceVersion,
                    StageMapApplyPlanner.ComputeSignature(document),
                    document.PresentationCatalog,
                    document.LastAppliedCatalogEntryKey,
                    issues,
                    changes);
            }

            if (sourceVersion != 1 && sourceVersion != 2)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMM001",
                    location,
                    $"Unsupported StageMapDocument migration. source={sourceVersion}, current={StageMapDocument.CurrentSchemaVersion}"));
                return new StageMapDocumentMigrationPlan(
                    document,
                    sourceVersion,
                    StageMapApplyPlanner.ComputeSignature(document),
                    null,
                    string.Empty,
                    issues,
                    changes);
            }

            StagePresentationCatalogSO presentationCatalog = sourceVersion == 1
                ? ResolvePresentationCatalogCandidate(document, location, issues)
                : document.PresentationCatalog;
            string appliedEntryKey = sourceVersion == 1
                ? ResolveAppliedEntryIdentity(document, location, issues)
                : document.LastAppliedCatalogEntryKey;
            if (presentationCatalog != null)
            {
                changes.Add(new StageMapApplyPlanChange(
                    StageMapApplyChangeKind.Update,
                    "StageMapDocument",
                    nameof(StageMapDocument.PresentationCatalog),
                    $"Assign presentation validation target '{presentationCatalog.name}'."));
            }

            if (!string.IsNullOrEmpty(appliedEntryKey))
            {
                changes.Add(new StageMapApplyPlanChange(
                    StageMapApplyChangeKind.Update,
                    "StageMapDocument",
                    nameof(StageMapDocument.LastAppliedCatalogEntryKey),
                    $"Record existing catalog entry identity '{appliedEntryKey}'."));
            }

            changes.Add(new StageMapApplyPlanChange(
                StageMapApplyChangeKind.Update,
                "StageMapDocument",
                nameof(StageMapDocument.SchemaVersion),
                $"Migrate schema v{sourceVersion} to v{StageMapDocument.CurrentSchemaVersion}."));

            return new StageMapDocumentMigrationPlan(
                document,
                sourceVersion,
                StageMapApplyPlanner.ComputeSignature(document),
                presentationCatalog,
                appliedEntryKey,
                issues,
                changes);
        }

        public static bool TryApply(StageMapDocumentMigrationPlan plan, bool saveAssets, out string error)
        {
            error = null;
            if (plan == null || plan.Document == null)
            {
                error = "Stage map migration plan is invalid.";
                return false;
            }

            if (StageMapApplyPlanner.ComputeSignature(plan.Document) != plan.DocumentSignature)
            {
                error = "StageMapDocument changed after migration preview. Rebuild the migration plan.";
                return false;
            }

            if (plan.HasErrors)
            {
                error = "StageMapDocument migration validation failed.";
                return false;
            }

            if (!plan.HasChanges)
                return true;

            if ((plan.SourceVersion != 1 && plan.SourceVersion != 2) || plan.TargetVersion != StageMapDocument.CurrentSchemaVersion)
            {
                error = "StageMapDocument migration version is no longer supported.";
                return false;
            }

            Undo.RecordObject(plan.Document, "Migrate Stage Map Document Schema");
            plan.Document.PresentationCatalog = plan.PresentationCatalog;
            plan.Document.SetLastAppliedCatalogEntryKey(plan.LastAppliedCatalogEntryKey);
            plan.Document.SchemaVersion = StageMapDocument.CurrentSchemaVersion;
            EditorUtility.SetDirty(plan.Document);
            if (saveAssets && AssetDatabase.Contains(plan.Document))
                AssetDatabase.SaveAssets();
            return true;
        }

        private static StagePresentationCatalogSO ResolvePresentationCatalogCandidate(
            StageMapDocument document,
            string location,
            List<ContentValidationIssue> issues)
        {
            if (document.PresentationCatalog != null)
                return document.PresentationCatalog;

            string[] guids = AssetDatabase.FindAssets("t:StagePresentationCatalogSO");
            if (guids.Length != 1)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMM002",
                    location,
                    guids.Length == 0
                        ? "Migration requires an explicit StagePresentationCatalogSO, but none was found."
                        : $"Migration cannot choose a StagePresentationCatalogSO because multiple assets were found. count={guids.Length}"));
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var catalog = AssetDatabase.LoadAssetAtPath<StagePresentationCatalogSO>(path);
            if (catalog != null)
                return catalog;

            issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Error,
                "SMM003",
                location,
                $"Migration presentation catalog candidate could not be loaded. path={path}"));
            return null;
        }

        private static string ResolveAppliedEntryIdentity(
            StageMapDocument document,
            string location,
            List<ContentValidationIssue> issues)
        {
            if (document.TargetCatalog == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMM004",
                    location,
                    "Migration requires TargetCatalog to derive the existing Definition/Layout entry identity."));
                return string.Empty;
            }

            var entries = document.TargetCatalog.Entries ?? Array.Empty<StageCatalogEntry>();
            string match = string.Empty;
            int matchCount = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Definition != document.TargetDefinition || entries[i].Layout != document.TargetLayout)
                    continue;

                match = entries[i].EntryKey != null ? entries[i].EntryKey.Trim() : string.Empty;
                matchCount++;
            }

            if (matchCount == 1 && !string.IsNullOrEmpty(match))
                return match;

            issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Error,
                "SMM005",
                location,
                matchCount == 0
                    ? "Migration could not derive last-applied identity because no catalog entry matches TargetDefinition/TargetLayout."
                    : $"Migration could not derive last-applied identity because multiple catalog entries match TargetDefinition/TargetLayout. count={matchCount}"));
            return string.Empty;
        }

        private static string BuildLocation(StageMapDocument document)
        {
            string path = AssetDatabase.GetAssetPath(document);
            return string.IsNullOrEmpty(path) ? document.name : path;
        }
    }
}
