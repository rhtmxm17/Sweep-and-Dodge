using System.Collections.Generic;

namespace SweepNDodge.DotsBullets.Editor
{
    public sealed class StageMapLegacyImportPlan
    {
        internal StageMapLegacyImportPlan(
            StageLayoutStageMarker sourceStage,
            StageMapDocument document,
            string sourceSignature,
            string documentSignature,
            IReadOnlyList<ContentValidationIssue> validationIssues,
            IReadOnlyList<StageMapApplyPlanChange> changes)
        {
            SourceStage = sourceStage;
            Document = document;
            SourceSignature = sourceSignature ?? string.Empty;
            DocumentSignature = documentSignature ?? string.Empty;
            ValidationIssues = validationIssues ?? new List<ContentValidationIssue>();
            Changes = changes ?? new List<StageMapApplyPlanChange>();
        }

        public StageLayoutStageMarker SourceStage { get; }
        public StageMapDocument Document { get; }
        public string SourceSignature { get; }
        public string DocumentSignature { get; }
        public IReadOnlyList<ContentValidationIssue> ValidationIssues { get; }
        public IReadOnlyList<StageMapApplyPlanChange> Changes { get; }
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < ValidationIssues.Count; i++)
                {
                    if (ValidationIssues[i].Severity == ContentValidationSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public bool HasChanges => Changes.Count > 0;
    }
}
