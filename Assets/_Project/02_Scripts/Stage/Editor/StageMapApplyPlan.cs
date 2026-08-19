using System.Collections.Generic;
using System.Linq;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum StageMapApplyChangeKind : byte
    {
        Add = 0,
        Update = 1,
        Remove = 2,
    }

    public readonly struct StageMapApplyPlanChange
    {
        public readonly StageMapApplyChangeKind Kind;
        public readonly string Target;
        public readonly string Field;
        public readonly string Description;

        public StageMapApplyPlanChange(StageMapApplyChangeKind kind, string target, string field, string description)
        {
            Kind = kind;
            Target = target ?? string.Empty;
            Field = field ?? string.Empty;
            Description = description ?? string.Empty;
        }
    }

    public sealed class StageMapApplyPlan
    {
        internal StageMapApplyPlan(
            StageMapDocument document,
            StageLayoutSO targetLayout,
            StageDefinitionSO targetDefinition,
            StageCatalogSO targetCatalog,
            string documentSignature,
            string layoutSignature,
            string definitionSignature,
            string catalogSignature,
            StageCatalogEntry[] candidateCatalogEntries,
            string catalogIdentityKey,
            string resultingLastAppliedCatalogEntryKey,
            bool catalogIdentityChanged,
            IReadOnlyList<ContentValidationIssue> validationIssues,
            IReadOnlyList<StageMapApplyPlanChange> changes)
        {
            Document = document;
            TargetLayout = targetLayout;
            TargetDefinition = targetDefinition;
            TargetCatalog = targetCatalog;
            DocumentSignature = documentSignature ?? string.Empty;
            LayoutSignature = layoutSignature ?? string.Empty;
            DefinitionSignature = definitionSignature ?? string.Empty;
            CatalogSignature = catalogSignature ?? string.Empty;
            CandidateCatalogEntries = candidateCatalogEntries;
            CatalogIdentityKey = catalogIdentityKey ?? string.Empty;
            ResultingLastAppliedCatalogEntryKey = resultingLastAppliedCatalogEntryKey ?? string.Empty;
            CatalogIdentityChanged = catalogIdentityChanged;
            ValidationIssues = validationIssues ?? new List<ContentValidationIssue>();
            Changes = changes ?? new List<StageMapApplyPlanChange>();
        }

        public StageMapDocument Document { get; }
        public StageLayoutSO TargetLayout { get; }
        public StageDefinitionSO TargetDefinition { get; }
        public StageCatalogSO TargetCatalog { get; }
        public string DocumentSignature { get; }
        public string LayoutSignature { get; }
        public string DefinitionSignature { get; }
        public string CatalogSignature { get; }
        internal StageCatalogEntry[] CandidateCatalogEntries { get; }
        public string CatalogIdentityKey { get; }
        public string ResultingLastAppliedCatalogEntryKey { get; }
        public bool CatalogIdentityChanged { get; }
        public IReadOnlyList<ContentValidationIssue> ValidationIssues { get; }
        public IReadOnlyList<StageMapApplyPlanChange> Changes { get; }
        public bool HasErrors => ValidationIssues.Any(x => x.Severity == ContentValidationSeverity.Error);
        public bool HasChanges => Changes.Count > 0;
        public bool RequiresConfirmation => CatalogIdentityChanged || Changes.Any(x => x.Kind == StageMapApplyChangeKind.Remove);
    }
}
