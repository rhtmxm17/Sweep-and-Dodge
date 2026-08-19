using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapApplyPlanner
    {
        public static StageMapApplyPlan BuildPlan(StageMapDocument document)
        {
            var issues = new List<ContentValidationIssue>(32);
            var changes = new List<StageMapApplyPlanChange>(16);
            if (document == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD999", "(null)", "StageMapDocument is null."));
                return new StageMapApplyPlan(
                    null, null, null, null,
                    string.Empty, string.Empty, string.Empty, string.Empty,
                    null, string.Empty, string.Empty, false,
                    issues, changes);
            }

            string documentPath = AssetDatabase.GetAssetPath(document);
            string location = string.IsNullOrEmpty(documentPath) ? document.name : documentPath;
            StageMapDocumentValidationRules.ValidateDocument(document, location, issues);

            var layoutSnapshot = StageMapDocumentExporter.BuildLayoutSnapshot(document);
            var definitionSnapshot = StageMapDocumentExporter.BuildDefinitionSnapshot(document);
            CatalogCandidate catalogCandidate;
            try
            {
                CollectLayoutChanges(document.TargetLayout, layoutSnapshot, changes);
                CollectDefinitionChanges(document.TargetDefinition, definitionSnapshot, changes);
                catalogCandidate = BuildCatalogCandidate(document, location, issues, changes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(layoutSnapshot);
                UnityEngine.Object.DestroyImmediate(definitionSnapshot);
            }

            return new StageMapApplyPlan(
                document,
                document.TargetLayout,
                document.TargetDefinition,
                document.TargetCatalog,
                ComputeSignature(document),
                ComputeSignature(document.TargetLayout),
                ComputeSignature(document.TargetDefinition),
                ComputeSignature(document.TargetCatalog),
                catalogCandidate?.Entries,
                catalogCandidate?.IdentityKey,
                catalogCandidate?.ResultingLastAppliedKey,
                catalogCandidate != null && catalogCandidate.IdentityChanged,
                issues,
                changes);
        }

        public static bool TryApplyPlan(StageMapApplyPlan plan, bool saveAssets, out string error)
        {
            return TryApplyPlan(plan, saveAssets, confirmed: false, out error);
        }

        public static bool TryApplyPlan(StageMapApplyPlan plan, bool saveAssets, bool confirmed, out string error)
        {
            error = null;
            if (plan == null || plan.Document == null)
            {
                error = "Stage map apply plan is invalid.";
                return false;
            }

            var currentPlan = BuildPlan(plan.Document);
            if (currentPlan.DocumentSignature != plan.DocumentSignature
                || currentPlan.LayoutSignature != plan.LayoutSignature
                || currentPlan.DefinitionSignature != plan.DefinitionSignature
                || currentPlan.CatalogSignature != plan.CatalogSignature)
            {
                error = "Document or generated asset data changed after dry-run. Rebuild the apply plan before applying.";
                return false;
            }

            if (currentPlan.HasErrors)
            {
                error = "Stage map document or candidate catalog validation failed. Fix errors before applying.";
                return false;
            }

            if (currentPlan.RequiresConfirmation && !confirmed)
            {
                error = "Stage map apply plan contains destructive catalog changes and requires confirmation.";
                return false;
            }

            if (plan.TargetLayout == null || plan.TargetDefinition == null || plan.TargetCatalog == null)
            {
                error = "Generated Layout, Definition, and Catalog targets are required.";
                return false;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Stage Map Document");
            Undo.RecordObjects(
                new UnityEngine.Object[] { plan.TargetLayout, plan.TargetDefinition, plan.TargetCatalog, plan.Document },
                "Apply Stage Map Document");
            ApplyLayout(plan.Document, plan.TargetLayout);
            ApplyDefinition(plan.Document, plan.TargetDefinition);
            ApplyCatalog(plan.TargetCatalog, currentPlan.CandidateCatalogEntries);
            plan.Document.SetLastAppliedCatalogEntryKey(currentPlan.ResultingLastAppliedCatalogEntryKey);
            EditorUtility.SetDirty(plan.Document);
            Undo.CollapseUndoOperations(undoGroup);

            if (saveAssets)
                AssetDatabase.SaveAssets();
            return true;
        }

        public static string ComputeSignature(UnityEngine.Object target)
        {
            if (target == null)
                return string.Empty;

            string json = EditorJsonUtility.ToJson(target);
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                return BitConverter.ToString(bytes).Replace("-", string.Empty);
            }
        }

        private static void ApplyLayout(StageMapDocument document, StageLayoutSO target)
        {
            var snapshot = StageMapDocumentExporter.BuildLayoutSnapshot(document);
            try
            {
                target.SchemaVersion = snapshot.SchemaVersion;
                target.StageId = snapshot.StageId;
                target.Grid = snapshot.Grid;
                target.Cells = snapshot.Cells;
                target.SourceRegions = snapshot.SourceRegions;
                target.DepositRegions = snapshot.DepositRegions;
                target.PlayerStart = snapshot.PlayerStart;
                target.Presentations = snapshot.Presentations;
                EditorUtility.SetDirty(target);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        private static void ApplyDefinition(StageMapDocument document, StageDefinitionSO target)
        {
            var snapshot = StageMapDocumentExporter.BuildDefinitionSnapshot(document);
            try
            {
                target.StageId = snapshot.StageId;
                target.DisplayName = snapshot.DisplayName;
                target.IsFinalStage = snapshot.IsFinalStage;
                target.StageTimeLimitSec = snapshot.StageTimeLimitSec;
                target.SourceBindings = snapshot.SourceBindings;
                EditorUtility.SetDirty(target);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        private static void ApplyCatalog(StageCatalogSO target, StageCatalogEntry[] candidateEntries)
        {
            target.SchemaVersion = Mathf.Max(1, target.SchemaVersion);
            target.Entries = candidateEntries != null
                ? (StageCatalogEntry[])candidateEntries.Clone()
                : Array.Empty<StageCatalogEntry>();
            EditorUtility.SetDirty(target);
        }

        private static CatalogCandidate BuildCatalogCandidate(
            StageMapDocument document,
            string location,
            List<ContentValidationIssue> issues,
            List<StageMapApplyPlanChange> changes)
        {
            if (document.TargetCatalog == null)
                return null;

            var entries = document.TargetCatalog.Entries != null
                ? (StageCatalogEntry[])document.TargetCatalog.Entries.Clone()
                : Array.Empty<StageCatalogEntry>();
            if (!TryResolveCatalogIdentity(document, entries, location, issues, out int identityIndex, out string identityKey))
                return new CatalogCandidate(entries, identityKey, document.LastAppliedCatalogEntryKey, false);

            string nextKey = StageMapDocumentExporter.BuildCatalogEntryKey(document);
            bool identityChanged = identityIndex >= 0
                && !string.Equals(identityKey, nextKey, StringComparison.Ordinal);
            string resultingLastAppliedKey;
            if (!document.IncludeInCatalog)
            {
                if (identityIndex >= 0)
                {
                    changes.Add(new StageMapApplyPlanChange(
                        StageMapApplyChangeKind.Remove,
                        "StageCatalogSO",
                        nameof(StageCatalogSO.Entries),
                        $"Remove identified catalog entry '{identityKey}'."));
                    RemoveAt(ref entries, identityIndex);
                }
                resultingLastAppliedKey = string.Empty;
            }
            else
            {
                var expected = StageMapDocumentExporter.BuildCatalogEntry(document);
                if (identityIndex < 0)
                {
                    Array.Resize(ref entries, entries.Length + 1);
                    entries[entries.Length - 1] = expected;
                    changes.Add(new StageMapApplyPlanChange(
                        StageMapApplyChangeKind.Add,
                        "StageCatalogSO",
                        nameof(StageCatalogSO.Entries),
                        $"Add catalog entry '{nextKey}'."));
                }
                else
                {
                    if (identityChanged)
                    {
                        changes.Add(new StageMapApplyPlanChange(
                            StageMapApplyChangeKind.Remove,
                            "StageCatalogSO",
                            "CatalogEntryIdentity",
                            $"Rename catalog entry identity '{identityKey}' to '{nextKey}'."));
                    }

                    if (!JsonEqual(entries[identityIndex], expected))
                    {
                        changes.Add(new StageMapApplyPlanChange(
                            StageMapApplyChangeKind.Update,
                            "StageCatalogSO",
                            nameof(StageCatalogSO.Entries),
                            $"Update identified catalog entry '{identityKey}'."));
                    }
                    entries[identityIndex] = expected;
                }
                resultingLastAppliedKey = nextKey;
            }

            ValidateCandidateCatalog(document, document.TargetCatalog, entries, location, issues);
            return new CatalogCandidate(entries, identityKey, resultingLastAppliedKey, identityChanged);
        }

        private static bool TryResolveCatalogIdentity(
            StageMapDocument document,
            StageCatalogEntry[] entries,
            string location,
            List<ContentValidationIssue> issues,
            out int index,
            out string identityKey)
        {
            index = -1;
            identityKey = string.Empty;
            string appliedKey = document.LastAppliedCatalogEntryKey;
            if (!string.IsNullOrWhiteSpace(appliedKey))
            {
                int count = CountEntryKeyMatches(entries, appliedKey, out index);
                if (count == 1)
                {
                    identityKey = entries[index].EntryKey != null ? entries[index].EntryKey.Trim() : string.Empty;
                    return true;
                }

                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMC100",
                    location,
                    count == 0
                        ? $"Last-applied catalog entry identity was not found. key={appliedKey}"
                        : $"Last-applied catalog entry identity is ambiguous. key={appliedKey}, count={count}"));
                return false;
            }

            int pairCount = CountPairMatches(entries, document.TargetDefinition, document.TargetLayout, out index);
            if (pairCount > 1)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMC101",
                    location,
                    $"Catalog entry identity is ambiguous because multiple entries reference TargetDefinition/TargetLayout. count={pairCount}"));
                return false;
            }

            if (pairCount == 1)
            {
                identityKey = entries[index].EntryKey != null ? entries[index].EntryKey.Trim() : string.Empty;
                return true;
            }

            string currentKey = StageMapDocumentExporter.BuildCatalogEntryKey(document);
            int keyCount = CountEntryKeyMatches(entries, currentKey, out index);
            if (keyCount > 1)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMC102",
                    location,
                    $"Initial catalog entry identity is ambiguous. key={currentKey}, count={keyCount}"));
                return false;
            }

            if (keyCount == 1)
                identityKey = entries[index].EntryKey != null ? entries[index].EntryKey.Trim() : string.Empty;
            return true;
        }

        private static void ValidateCandidateCatalog(
            StageMapDocument document,
            StageCatalogSO source,
            StageCatalogEntry[] entries,
            string location,
            List<ContentValidationIssue> issues)
        {
            var candidate = ScriptableObject.CreateInstance<StageCatalogSO>();
            var layoutSnapshot = StageMapDocumentExporter.BuildLayoutSnapshot(document);
            var definitionSnapshot = StageMapDocumentExporter.BuildDefinitionSnapshot(document);
            try
            {
                candidate.SchemaVersion = Mathf.Max(1, source.SchemaVersion);
                candidate.Entries = entries != null ? (StageCatalogEntry[])entries.Clone() : Array.Empty<StageCatalogEntry>();
                for (int i = 0; i < candidate.Entries.Length; i++)
                {
                    if (candidate.Entries[i].Definition != document.TargetDefinition
                        || candidate.Entries[i].Layout != document.TargetLayout)
                    {
                        continue;
                    }

                    var entry = candidate.Entries[i];
                    entry.Definition = definitionSnapshot;
                    entry.Layout = layoutSnapshot;
                    candidate.Entries[i] = entry;
                }
                StageCatalogValidationRules.ValidateCatalog(candidate, location + "::CandidateCatalog", issues);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
                UnityEngine.Object.DestroyImmediate(layoutSnapshot);
                UnityEngine.Object.DestroyImmediate(definitionSnapshot);
            }
        }

        private static void CollectLayoutChanges(StageLayoutSO target, StageLayoutSO snapshot, List<StageMapApplyPlanChange> changes)
        {
            if (target == null)
                return;
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.SchemaVersion), target.SchemaVersion, snapshot.SchemaVersion);
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.StageId), target.StageId, snapshot.StageId);
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.Grid), target.Grid, snapshot.Grid);
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.Cells), target.Cells, snapshot.Cells);
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.SourceRegions), target.SourceRegions, snapshot.SourceRegions);
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.DepositRegions), target.DepositRegions, snapshot.DepositRegions);
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.PlayerStart), target.PlayerStart, snapshot.PlayerStart);
            AddIfDifferent(changes, "StageLayoutSO", nameof(StageLayoutSO.Presentations), target.Presentations, snapshot.Presentations);
        }

        private static void CollectDefinitionChanges(StageDefinitionSO target, StageDefinitionSO snapshot, List<StageMapApplyPlanChange> changes)
        {
            if (target == null)
                return;
            AddIfDifferent(changes, "StageDefinitionSO", nameof(StageDefinitionSO.StageId), target.StageId, snapshot.StageId);
            AddIfDifferent(changes, "StageDefinitionSO", nameof(StageDefinitionSO.DisplayName), target.DisplayName, snapshot.DisplayName);
            AddIfDifferent(changes, "StageDefinitionSO", nameof(StageDefinitionSO.IsFinalStage), target.IsFinalStage, snapshot.IsFinalStage);
            AddIfDifferent(changes, "StageDefinitionSO", nameof(StageDefinitionSO.StageTimeLimitSec), target.StageTimeLimitSec, snapshot.StageTimeLimitSec);
            AddIfDifferent(changes, "StageDefinitionSO", nameof(StageDefinitionSO.SourceBindings), target.SourceBindings, snapshot.SourceBindings);
        }

        private static int CountEntryKeyMatches(StageCatalogEntry[] entries, string key, out int index)
        {
            index = -1;
            int count = 0;
            string normalized = key != null ? key.Trim() : string.Empty;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!string.Equals(entries[i].EntryKey != null ? entries[i].EntryKey.Trim() : string.Empty, normalized, StringComparison.Ordinal))
                    continue;
                index = i;
                count++;
            }
            return count;
        }

        private static int CountPairMatches(StageCatalogEntry[] entries, StageDefinitionSO definition, StageLayoutSO layout, out int index)
        {
            index = -1;
            int count = 0;
            if (definition == null || layout == null)
                return 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Definition != definition || entries[i].Layout != layout)
                    continue;
                index = i;
                count++;
            }
            return count;
        }

        private static void RemoveAt(ref StageCatalogEntry[] entries, int index)
        {
            for (int i = index + 1; i < entries.Length; i++)
                entries[i - 1] = entries[i];
            Array.Resize(ref entries, entries.Length - 1);
        }

        private static void AddIfDifferent<T>(List<StageMapApplyPlanChange> changes, string target, string field, T current, T next)
        {
            if (!JsonEqual(current, next))
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, target, field, $"Update {target}.{field}."));
        }

        private static bool JsonEqual<T>(T current, T next)
        {
            return string.Equals(JsonUtility.ToJson(new JsonBox<T>(current)), JsonUtility.ToJson(new JsonBox<T>(next)), StringComparison.Ordinal);
        }

        private sealed class CatalogCandidate
        {
            public CatalogCandidate(StageCatalogEntry[] entries, string identityKey, string resultingLastAppliedKey, bool identityChanged)
            {
                Entries = entries;
                IdentityKey = identityKey ?? string.Empty;
                ResultingLastAppliedKey = resultingLastAppliedKey ?? string.Empty;
                IdentityChanged = identityChanged;
            }

            public StageCatalogEntry[] Entries { get; }
            public string IdentityKey { get; }
            public string ResultingLastAppliedKey { get; }
            public bool IdentityChanged { get; }
        }

        [Serializable]
        private struct JsonBox<T>
        {
            public T Value;
            public JsonBox(T value) { Value = value; }
        }
    }
}
