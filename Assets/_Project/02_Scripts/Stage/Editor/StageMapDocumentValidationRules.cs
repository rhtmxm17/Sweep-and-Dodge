using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapDocumentValidationRules
    {
        public static void ValidateDocumentWithTargets(
            StageMapDocument document,
            string locationPrefix,
            List<StageMapDocumentIssue> issues)
        {
            if (issues == null)
                return;

            var rawIssues = new List<ContentValidationIssue>(32);
            ValidateDocument(document, locationPrefix, rawIssues);
            StageMapDocumentIssueMapper.Map(document, rawIssues, issues);
        }

        public static void ValidateDocumentRecords(IReadOnlyList<ContentValidationRecord<StageMapDocument>> documents, List<ContentValidationIssue> issues)
        {
            if (documents == null || issues == null)
                return;

            for (int i = 0; i < documents.Count; i++)
            {
                var record = documents[i];
                if (record.Value == null)
                    continue;

                ValidateDocument(record.Value, record.Location, issues);
            }
        }

        public static void ValidateDocument(StageMapDocument document, string locationPrefix, List<ContentValidationIssue> issues)
        {
            if (document == null || issues == null)
                return;

            string location = BuildDocumentLocation(locationPrefix, document.StageId);
            ValidateDocumentHeader(document, location, issues);
            ValidateVisualTileKeys(document, location, issues);
            ValidateGeneratedAssetTargets(document, location, issues);

            var layout = StageMapDocumentExporter.BuildLayoutSnapshot(document);
            try
            {
                StageGridLayoutValidationRules.ValidateLayout(layout, locationPrefix, issues);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(layout);
            }

            ValidateHazardPlacements(document, location, issues);
            ValidateHazardOrchestrationRules(document, location, issues);
            ValidatePresentationLinks(document, location, issues);
        }

        private static void ValidateVisualTileKeys(StageMapDocument document, string location, List<ContentValidationIssue> issues)
        {
            if (document.VisualTileKeys == null || document.VisualTileKeys.Length == 0)
                return;

            int expected = document.Grid.Width > 0 && document.Grid.Height > 0
                ? document.Grid.Width * document.Grid.Height
                : 0;
            if (document.VisualTileKeys.Length != expected)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD010",
                    location,
                    $"VisualTileKeys length must be empty or equal Grid.Width * Grid.Height. keys={document.VisualTileKeys.Length}, expected={expected}"));
            }
        }

        private static void ValidateDocumentHeader(StageMapDocument document, string location, List<ContentValidationIssue> issues)
        {
            if (document.SchemaVersion == 1)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD001",
                    location,
                    "StageMapDocument schema v1 requires explicit migration preview/apply before validation or export."));
            }
            else if (document.SchemaVersion != StageMapDocument.CurrentSchemaVersion)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD001",
                    location,
                    $"Unsupported StageMapDocument SchemaVersion. current={document.SchemaVersion}, expected={StageMapDocument.CurrentSchemaVersion}"));
            }

            if (document.StageId <= 0)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD002",
                    location,
                    $"StageId must be >= 1. current={document.StageId}"));
            }

            if (document.StageTimeLimitSec <= 0f)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD003",
                    location,
                    $"StageTimeLimitSec must be > 0. current={document.StageTimeLimitSec}"));
            }
        }

        private static void ValidateGeneratedAssetTargets(StageMapDocument document, string location, List<ContentValidationIssue> issues)
        {
            if (document.TargetLayout == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD900",
                    location,
                    "TargetLayout is required before the document can be applied."));
            }

            if (document.TargetDefinition == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD901",
                    location,
                    "TargetDefinition is required before the document can be applied."));
            }

            if (document.TargetCatalog == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD902",
                    location,
                    "TargetCatalog is required as the v1 apply target regardless of IncludeInCatalog."));
            }

            if (document.PresentationCatalog == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD903",
                    location,
                    "PresentationCatalog is required as the explicit presentation validation target."));
            }
        }

        private static void ValidateHazardPlacements(StageMapDocument document, string location, List<ContentValidationIssue> issues)
        {
            var placements = document.HazardActorPlacements ?? Array.Empty<StageMapHazardActorPlacementData>();
            if (placements.Length == 0)
                return;

            var activeSourceIds = BuildActiveSourceIdSet(document.SourceRegions);
            var ownersByPlacementId = new Dictionary<int, List<string>>();
            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                string placementLocation = $"{location}/HazardActorPlacements[{i}]";

                if (!ownersByPlacementId.TryGetValue(placement.PlacementInstanceId, out var owners))
                {
                    owners = new List<string>(2);
                    ownersByPlacementId.Add(placement.PlacementInstanceId, owners);
                }

                owners.Add(placementLocation);

                if (placement.PlacementInstanceId <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD020",
                        placementLocation,
                        "PlacementInstanceId must be >= 1."));
                }

                if (placement.OwningSourceStableId == 0u)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD021",
                        placementLocation,
                        "OwningSourceStableId must be >= 1."));
                }
                else if (!activeSourceIds.Contains(placement.OwningSourceStableId))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD022",
                        placementLocation,
                        $"OwningSourceStableId must reference an active source region. stableId={placement.OwningSourceStableId}"));
                }

                if (placement.ActorArchetypePrefab == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD023",
                        placementLocation,
                        "ActorArchetypePrefab is required."));
                }

                ValidateHazardPlacementThroughCatalogRules(placement, placementLocation, issues);
            }

            foreach (var pair in ownersByPlacementId)
            {
                if (pair.Key <= 0 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD024",
                        pair.Value[i],
                        $"Duplicate HazardActor PlacementInstanceId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidatePresentationLinks(StageMapDocument document, string location, List<ContentValidationIssue> issues)
        {
            var links = document.PresentationLinks ?? Array.Empty<StageMapPresentationLinkData>();
            if (links.Length == 0)
                return;

            var sourceIds = BuildActiveSourceIdSet(document.SourceRegions);
            var depositIds = BuildActiveRegionIdSet(document.DepositRegions);
            var ownersByStableId = new Dictionary<uint, List<string>>();
            bool hasKeyToResolve = false;
            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                string linkLocation = $"{location}/PresentationLinks[{i}]";
                if (!ownersByStableId.TryGetValue(link.StableId, out var owners))
                {
                    owners = new List<string>(2);
                    ownersByStableId.Add(link.StableId, owners);
                }

                owners.Add(linkLocation);

                if (!link.Active)
                    continue;

                if (string.IsNullOrWhiteSpace(link.PresentationKey))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "STL007",
                        linkLocation,
                        "PresentationKey is empty."));
                }
                else
                {
                    hasKeyToResolve = true;
                }

                if (link.PlacementMode != StagePresentationPlacementMode.LinkedToParent)
                    continue;

                if (link.LinkKind == StagePresentationLinkKind.Source && !sourceIds.Contains(link.LinkedStableId))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD030",
                        linkLocation,
                        $"Source-linked presentation must reference an active source region. stableId={link.LinkedStableId}"));
                }
                else if (link.LinkKind == StagePresentationLinkKind.Deposit && !depositIds.Contains(link.LinkedStableId))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD031",
                        linkLocation,
                        $"Deposit-linked presentation must reference an active deposit region. stableId={link.LinkedStableId}"));
                }
            }

            foreach (var pair in ownersByStableId)
            {
                if (pair.Key == 0u || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD032",
                        pair.Value[i],
                        $"Duplicate StageMapPresentationLinkData.StableId detected: {pair.Key}. Owners: {joined}"));
                }
            }

            if (hasKeyToResolve)
                ValidatePresentationKeysAgainstCatalog(document, links, location, issues);
        }

        private static void ValidateHazardOrchestrationRules(StageMapDocument document, string location, List<ContentValidationIssue> issues)
        {
            var rules = document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            if (rules.Length == 0)
                return;

            var activeSourceIds = BuildActiveSourceIdSet(document.SourceRegions);
            var placementsBySource = BuildPlacementLookup(document.HazardActorPlacements);
            var ruleKeys = new HashSet<string>();
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                string ruleLocation = $"{location}/HazardActorOrchestrationRules[{i}]";
                if (rule.OwningSourceStableId == 0u)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD040", ruleLocation, "OwningSourceStableId must be >= 1."));
                }
                else if (!activeSourceIds.Contains(rule.OwningSourceStableId))
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD041", ruleLocation, $"OwningSourceStableId must reference an active source region. stableId={rule.OwningSourceStableId}"));
                }

                if (rule.RuleId <= 0)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD042", ruleLocation, "RuleId must be >= 1."));
                }
                else if (!ruleKeys.Add($"{rule.OwningSourceStableId}:{rule.RuleId}"))
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD043", ruleLocation, $"Duplicate source-local HazardActor rule id. source={rule.OwningSourceStableId}, ruleId={rule.RuleId}"));
                }

                if (rule.ActionType != HazardActorOrchestrationActionId.Spawn
                    && rule.ActionType != HazardActorOrchestrationActionId.PhaseSet
                    && rule.ActionType != HazardActorOrchestrationActionId.Retire)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD044", ruleLocation, $"Unsupported ActionType {rule.ActionType}."));
                }

                if (rule.TriggerType != HazardActorOrchestrationTriggerId.OnStageStart
                    && rule.TriggerType != HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD045", ruleLocation, $"Unsupported TriggerType {rule.TriggerType}."));
                }

                if (rule.TriggerThresholdNormalized < 0f || rule.TriggerThresholdNormalized > 1f)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD046", ruleLocation, "TriggerThresholdNormalized must be within [0, 1]."));
                }

                var targets = rule.TargetPlacementInstanceIds ?? Array.Empty<int>();
                if (targets.Length == 0)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD047", ruleLocation, "At least one target placement id is required."));
                    continue;
                }

                var targetSet = new HashSet<int>();
                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    int placementId = targets[targetIndex];
                    if (placementId <= 0)
                    {
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD048", ruleLocation, "TargetPlacementInstanceIds must be >= 1."));
                        continue;
                    }

                    if (!targetSet.Add(placementId))
                    {
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD049", ruleLocation, $"Duplicate target placement id {placementId} in rule."));
                        continue;
                    }

                    if (!placementsBySource.TryGetValue(rule.OwningSourceStableId, out var sourcePlacements)
                        || !sourcePlacements.TryGetValue(placementId, out var placement))
                    {
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD050", ruleLocation, $"Target placement {placementId} is missing or belongs to another source."));
                        continue;
                    }

                    if (rule.ActionType == HazardActorOrchestrationActionId.PhaseSet
                        && !ActorDefinesPhase(placement.ActorArchetypePrefab, rule.TargetPhaseId))
                    {
                        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMD051", ruleLocation, $"TargetPhaseId {rule.TargetPhaseId} is not defined by all target actor archetypes."));
                    }
                }
            }
        }

        private static void ValidateHazardPlacementThroughCatalogRules(
            StageMapHazardActorPlacementData placement,
            string placementLocation,
            List<ContentValidationIssue> issues)
        {
            var binding = new StageSourceBinding
            {
                SourceStableId = Math.Max(1u, placement.OwningSourceStableId),
                HazardActorPlacements = new[]
                {
                    new HazardActorPlacementBinding
                    {
                        PlacementInstanceId = placement.PlacementInstanceId,
                        ActorArchetypePrefab = placement.ActorArchetypePrefab,
                        LocalOffset = placement.SourceLocalOffset,
                        LocalYawDeg = placement.LocalYawDeg,
                    }
                },
                HazardActorOrchestrationRules = Array.Empty<HazardActorOrchestrationRuleBinding>(),
                SustainSlots = Array.Empty<SustainSlotBinding>(),
                EventSlots = Array.Empty<EventSlotBinding>(),
            };

            StageCatalogValidationRules.ValidateHazardActorData(
                binding,
                placementLocation,
                issues,
                enforceOperationalReferenceRestrictions: true);
        }

        private static void ValidatePresentationKeysAgainstCatalog(
            StageMapDocument document,
            StageMapPresentationLinkData[] links,
            string location,
            List<ContentValidationIssue> issues)
        {
            var catalog = document.PresentationCatalog;
            if (catalog == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMD033",
                    location,
                    "StagePresentationCatalogSO is not assigned for StageMapDocument presentation key validation."));
                return;
            }

            for (int i = 0; i < links.Length; i++)
            {
                var link = links[i];
                if (!link.Active || string.IsNullOrWhiteSpace(link.PresentationKey))
                    continue;

                if (!StagePresentationEditorUtility.TryResolveEntry(catalog, link.PresentationKey, out _))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMD034",
                        $"{location}/PresentationLinks[{i}]",
                        $"PresentationKey is not present in the resolved StagePresentationCatalogSO. key={link.PresentationKey.Trim()}"));
                }
            }
        }

        private static HashSet<uint> BuildActiveSourceIdSet(StageMapRegionData[] regions)
        {
            return BuildActiveRegionIdSet(regions);
        }

        private static HashSet<uint> BuildActiveRegionIdSet(StageMapRegionData[] regions)
        {
            var ids = new HashSet<uint>();
            if (regions == null)
                return ids;

            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i].Active && regions[i].StableId > 0u)
                    ids.Add(regions[i].StableId);
            }

            return ids;
        }

        private static Dictionary<uint, Dictionary<int, StageMapHazardActorPlacementData>> BuildPlacementLookup(StageMapHazardActorPlacementData[] placements)
        {
            var result = new Dictionary<uint, Dictionary<int, StageMapHazardActorPlacementData>>();
            if (placements == null)
                return result;
            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                if (placement.OwningSourceStableId == 0u || placement.PlacementInstanceId <= 0)
                    continue;
                if (!result.TryGetValue(placement.OwningSourceStableId, out var source))
                {
                    source = new Dictionary<int, StageMapHazardActorPlacementData>();
                    result.Add(placement.OwningSourceStableId, source);
                }
                if (!source.ContainsKey(placement.PlacementInstanceId))
                    source.Add(placement.PlacementInstanceId, placement);
            }
            return result;
        }

        private static bool ActorDefinesPhase(GameObject actorPrefab, int phaseId)
        {
            if (actorPrefab == null || phaseId <= 0)
                return false;
            var actor = actorPrefab.GetComponentInChildren<HazardActorAuthoring>(true);
            if (actor == null)
                return false;
            if (!HazardActorAuthoringValidationUtility.TryValidateStandalone(actor, out var seed, out _, out _, out _))
                return false;
            var policies = seed.Policies ?? Array.Empty<HazardActorPhaseSelectorPolicyBuffer>();
            for (int i = 0; i < policies.Length; i++)
            {
                if (policies[i].PhaseId == phaseId)
                    return true;
            }
            return false;
        }

        private static string BuildDocumentLocation(string prefix, int stageId)
        {
            return $"{prefix}::StageMapDocument(StageId={stageId})";
        }
    }
}
