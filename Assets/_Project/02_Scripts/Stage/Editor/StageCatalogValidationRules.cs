using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageCatalogValidationRules
    {
        private const string TestDataRootPath = "Assets/_Project/99_Tests/";

        public static void ValidateCatalogRecords(
            IReadOnlyList<ContentValidationRecord<StageCatalogSO>> catalogs,
            List<ContentValidationIssue> issues)
        {
            if (catalogs == null || issues == null)
                return;

            for (int i = 0; i < catalogs.Count; i++)
            {
                var record = catalogs[i];
                if (record.Value == null)
                    continue;

                ValidateCatalog(record.Value, record.Location, issues);
            }
        }

        public static void ValidateCatalog(
            StageCatalogSO catalog,
            string locationPrefix,
            List<ContentValidationIssue> issues)
        {
            if (catalog == null || issues == null)
                return;

            string catalogAssetPath = AssetDatabase.GetAssetPath(catalog);
            bool enforceOperationalReferenceRestrictions =
                !string.IsNullOrWhiteSpace(catalogAssetPath) &&
                !IsTestOnlyPath(catalogAssetPath);
            var entries = catalog.Entries ?? Array.Empty<StageCatalogEntry>();
            var entryKeyOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var enabledStageOwners = new Dictionary<int, List<string>>();

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string location = BuildEntryLocation(locationPrefix, i, entry.EntryKey);

                if (string.IsNullOrWhiteSpace(entry.EntryKey))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC001",
                        location,
                        "EntryKey is empty."));
                }
                else
                {
                    string key = entry.EntryKey.Trim();
                    if (!entryKeyOwners.TryGetValue(key, out var owners))
                    {
                        owners = new List<string>(2);
                        entryKeyOwners.Add(key, owners);
                    }

                    owners.Add(location);
                }

                if (entry.Definition == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC002",
                        location,
                        "Definition reference is null."));
                }

                if (entry.Layout == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC003",
                        location,
                        "Layout reference is null."));
                }

                int definitionStageId = entry.Definition != null ? entry.Definition.StageId : 0;
                int layoutStageId = entry.Layout != null ? entry.Layout.StageId : 0;

                if (entry.Definition != null && definitionStageId <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC004",
                        location,
                        $"Definition.StageId must be >= 1. current={definitionStageId}"));
                }

                if (entry.Layout != null && layoutStageId <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC005",
                        location,
                        $"Layout.StageId must be >= 1. current={layoutStageId}"));
                }

                if (entry.Definition != null && entry.Layout != null && definitionStageId != layoutStageId)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC006",
                        location,
                        $"Definition/Layout StageId mismatch. definition={definitionStageId}, layout={layoutStageId}"));
                }

                if (entry.Enabled && entry.Definition != null && definitionStageId > 0)
                {
                    if (!enabledStageOwners.TryGetValue(definitionStageId, out var owners))
                    {
                        owners = new List<string>(2);
                        enabledStageOwners.Add(definitionStageId, owners);
                    }

                    owners.Add(location);
                }

                if (entry.Definition != null)
                {
                    if (enforceOperationalReferenceRestrictions)
                    {
                        ValidateOperationalReference(
                            entry.Definition,
                            location,
                            "STC023",
                            "Operational StageCatalog cannot reference test-only StageDefinition assets.",
                            issues);
                    }

                    ValidateDefinition(entry.Definition, location, issues, enforceOperationalReferenceRestrictions);
                }

                if (entry.Definition != null && entry.Layout != null)
                {
                    if (enforceOperationalReferenceRestrictions)
                    {
                        ValidateOperationalReference(
                            entry.Layout,
                            location,
                            "STC024",
                            "Operational StageCatalog cannot reference test-only StageLayout assets.",
                            issues);
                    }

                    ValidateSourceCrossMapping(entry.Definition, entry.Layout, location, issues);
                }
            }

            foreach (var pair in entryKeyOwners)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC007",
                        pair.Value[i],
                        $"Duplicate EntryKey detected: {pair.Key}. Owners: {joined}"));
                }
            }

            foreach (var pair in enabledStageOwners)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC008",
                        pair.Value[i],
                        $"Duplicate enabled StageId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateDefinition(
            StageDefinitionSO definition,
            string location,
            List<ContentValidationIssue> issues,
            bool enforceOperationalReferenceRestrictions)
        {
            if (definition.StageTimeLimitSec <= 0f)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STC009",
                    location,
                    $"StageTimeLimitSec must be > 0. current={definition.StageTimeLimitSec}"));
            }

            var bindings = definition.SourceBindings ?? Array.Empty<StageSourceBinding>();
            var sourceOwners = new Dictionary<uint, List<int>>();
            var placementOwners = new Dictionary<int, List<string>>();

            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                string bindingLocation = $"{location}/SourceBindings[{i}]";
                if (binding.SourceStableId == 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC010",
                        bindingLocation,
                        "SourceStableId must be >= 1."));
                }

                if (!sourceOwners.TryGetValue(binding.SourceStableId, out var owners))
                {
                    owners = new List<int>(2);
                    sourceOwners.Add(binding.SourceStableId, owners);
                }
                owners.Add(i);

                if (binding.ThresholdWeakened < 0 || binding.ThresholdDepleted < binding.ThresholdWeakened)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC011",
                        bindingLocation,
                        $"Invalid thresholds. weakened={binding.ThresholdWeakened}, depleted={binding.ThresholdDepleted}"));
                }

                ValidateSustainSlots(binding.SustainSlots, bindingLocation, issues, enforceOperationalReferenceRestrictions);
                ValidateEventSlots(binding.EventSlots, bindingLocation, issues, enforceOperationalReferenceRestrictions);
                bool hasLegacyHazards = binding.HazardActors != null && binding.HazardActors.Length > 0;
                bool hasPlacements = binding.HazardActorPlacements != null && binding.HazardActorPlacements.Length > 0;
                if (hasLegacyHazards && hasPlacements)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC035",
                        bindingLocation,
                        "StageSourceBinding cannot use HazardActors and HazardActorPlacements together."));
                }

                ValidateHazardBindings(binding.HazardActors, bindingLocation, issues, enforceOperationalReferenceRestrictions);
                ValidateHazardPlacements(binding.HazardActorPlacements, bindingLocation, issues, enforceOperationalReferenceRestrictions, placementOwners);
            }

            foreach (var pair in sourceOwners)
            {
                if (pair.Key == 0 || pair.Value.Count <= 1)
                    continue;

                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STC012",
                    location,
                    $"Duplicate SourceStableId in StageDefinition. stableId={pair.Key}"));
            }

            foreach (var pair in placementOwners)
            {
                if (pair.Key < 1 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC036",
                        pair.Value[i],
                        $"Duplicate HazardActorPlacementBinding.PlacementInstanceId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateSustainSlots(
            SustainSlotBinding[] slots,
            string location,
            List<ContentValidationIssue> issues,
            bool enforceOperationalReferenceRestrictions)
        {
            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                string slotLocation = $"{location}/SustainSlots[{i}]";
                var slot = slots[i];
                if (slot.Clips == null || slot.Clips.Length <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC013",
                        slotLocation,
                        "Sustain slot clips are null or empty."));
                    continue;
                }

                if (slot.Weights != null && slot.Weights.Length != slot.Clips.Length)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC014",
                        slotLocation,
                        $"Weights length mismatch. weights={slot.Weights.Length}, clips={slot.Clips.Length}"));
                }

                for (int c = 0; c < slot.Clips.Length; c++)
                {
                    var clip = slot.Clips[c];
                    if (clip == null)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "STC015",
                            slotLocation,
                            $"Sustain clip is null at index={c}."));
                        continue;
                    }

                    if (clip.Phase != SourceWavePhaseId.Sustain)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "STC016",
                            slotLocation,
                            $"Sustain slot references non-sustain clip. clipId={clip.ClipId}, phase={clip.Phase}"));
                    }

                    if (enforceOperationalReferenceRestrictions)
                    {
                        ValidateOperationalReference(
                            clip,
                            slotLocation,
                            "STC025",
                            "Operational StageCatalog cannot reference test-only WaveClip assets.",
                            issues);
                    }

                    if (slot.Weights != null && c < slot.Weights.Length && slot.Weights[c] <= 0f)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "STC017",
                            slotLocation,
                            $"Weight must be > 0 at index={c}. current={slot.Weights[c]}"));
                    }
                }
            }
        }

        private static void ValidateEventSlots(
            EventSlotBinding[] slots,
            string location,
            List<ContentValidationIssue> issues,
            bool enforceOperationalReferenceRestrictions)
        {
            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                string slotLocation = $"{location}/EventSlots[{i}]";
                var slot = slots[i];
                if (slot.EventClips == null || slot.EventClips.Length <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC018",
                        slotLocation,
                        "Event slot clips are null or empty."));
                    continue;
                }

                for (int c = 0; c < slot.EventClips.Length; c++)
                {
                    var clip = slot.EventClips[c];
                    if (clip == null)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "STC019",
                            slotLocation,
                            $"Event clip is null at index={c}."));
                        continue;
                    }

                    if (clip.Phase != SourceWavePhaseId.OnStateEnterOnce)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "STC020",
                            slotLocation,
                            $"Event slot references non-event clip. clipId={clip.ClipId}, phase={clip.Phase}"));
                    }

                    if (enforceOperationalReferenceRestrictions)
                    {
                        ValidateOperationalReference(
                            clip,
                            slotLocation,
                            "STC025",
                            "Operational StageCatalog cannot reference test-only WaveClip assets.",
                            issues);
                    }
                }
            }
        }

        private static void ValidateHazardBindings(
            HazardActorBinding[] actors,
            string location,
            List<ContentValidationIssue> issues,
            bool enforceOperationalReferenceRestrictions)
        {
            if (actors == null)
                return;

            var actorOwners = new Dictionary<int, List<string>>();

            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                string actorLocation = $"{location}/HazardActors[{i}]";
                if (actor.ActorId < 1)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC026",
                        actorLocation,
                        "HazardActorBinding.ActorId must be >= 1."));
                }

                if (!actorOwners.TryGetValue(actor.ActorId, out var owners))
                {
                    owners = new List<string>(2);
                    actorOwners.Add(actor.ActorId, owners);
                }

                owners.Add(actorLocation);
                ValidateHazardEmitterBindings(actor.Emitters, actorLocation, issues, enforceOperationalReferenceRestrictions);
            }

            foreach (var pair in actorOwners)
            {
                if (pair.Key < 1 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC027",
                        pair.Value[i],
                        $"Duplicate HazardActorBinding.ActorId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateHazardEmitterBindings(
            HazardEmitterBinding[] emitters,
            string location,
            List<ContentValidationIssue> issues,
            bool enforceOperationalReferenceRestrictions)
        {
            if (emitters == null)
                return;

            var emitterOwners = new Dictionary<int, List<string>>();

            for (int i = 0; i < emitters.Length; i++)
            {
                var emitter = emitters[i];
                string emitterLocation = $"{location}/Emitters[{i}]";
                if (emitter.EmitterId < 1)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC028",
                        emitterLocation,
                        "HazardEmitterBinding.EmitterId must be >= 1."));
                }

                if (!emitterOwners.TryGetValue(emitter.EmitterId, out var owners))
                {
                    owners = new List<string>(2);
                    emitterOwners.Add(emitter.EmitterId, owners);
                }

                owners.Add(emitterLocation);

                if (!enforceOperationalReferenceRestrictions)
                    continue;

                ValidateOperationalReference(
                    emitter.TelegraphProfileOverride,
                    emitterLocation,
                    "STC030",
                    "Operational StageCatalog cannot reference test-only HazardEmitterTelegraphProfile assets through emitter overrides.",
                    issues);
                ValidateOperationalReference(
                    emitter.EmissionProfileOverride,
                    emitterLocation,
                    "STC031",
                    "Operational StageCatalog cannot reference test-only HazardEmitterEmissionProfile assets through emitter overrides.",
                    issues);
            }

            foreach (var pair in emitterOwners)
            {
                if (pair.Key < 1 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC029",
                        pair.Value[i],
                        $"Duplicate HazardEmitterBinding.EmitterId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateHazardPlacements(
            HazardActorPlacementBinding[] placements,
            string location,
            List<ContentValidationIssue> issues,
            bool enforceOperationalReferenceRestrictions,
            Dictionary<int, List<string>> placementOwners)
        {
            if (placements == null)
                return;

            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                string placementLocation = $"{location}/HazardActorPlacements[{i}]";

                if (placement.PlacementInstanceId < 1)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC032",
                        placementLocation,
                        "HazardActorPlacementBinding.PlacementInstanceId must be >= 1."));
                }

                if (!placementOwners.TryGetValue(placement.PlacementInstanceId, out var owners))
                {
                    owners = new List<string>(2);
                    placementOwners.Add(placement.PlacementInstanceId, owners);
                }
                owners.Add(placementLocation);

                if (placement.ActorArchetypePrefab == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC033",
                        placementLocation,
                        "HazardActorPlacementBinding.ActorArchetypePrefab is null."));
                    continue;
                }

                if (enforceOperationalReferenceRestrictions)
                {
                    ValidateOperationalReference(
                        placement.ActorArchetypePrefab,
                        placementLocation,
                        "STC037",
                        "Operational StageCatalog cannot reference test-only HazardActor archetype prefabs through placements.",
                        issues);
                }

                var actorAuthorings = placement.ActorArchetypePrefab.GetComponentsInChildren<HazardActorAuthoring>(true);
                if (actorAuthorings == null || actorAuthorings.Length != 1)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC034",
                        placementLocation,
                        $"HazardActorPlacementBinding.ActorArchetypePrefab must contain exactly one HazardActorAuthoring. found={actorAuthorings?.Length ?? 0}"));
                    continue;
                }

                if (!HazardActorAuthoringValidationUtility.TryValidateStandalone(
                        actorAuthorings[0],
                        out _,
                        out _,
                        out _,
                        out var error))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "STC034",
                        placementLocation,
                        error));
                }
            }
        }

        private static void ValidateSourceCrossMapping(
            StageDefinitionSO definition,
            StageLayoutSO layout,
            string location,
            List<ContentValidationIssue> issues)
        {
            var definitionSet = new HashSet<uint>();
            var layoutSet = new HashSet<uint>();

            var bindings = definition.SourceBindings ?? Array.Empty<StageSourceBinding>();
            for (int i = 0; i < bindings.Length; i++)
            {
                uint stableId = bindings[i].SourceStableId;
                if (stableId > 0)
                    definitionSet.Add(stableId);
            }

            if (StageGridLayoutValidationRules.UsesGridSchema(layout))
            {
                var sourceRegions = layout.SourceRegions ?? Array.Empty<StageSourceRegionLayoutData>();
                for (int i = 0; i < sourceRegions.Length; i++)
                {
                    uint stableId = sourceRegions[i].StableId;
                    if (sourceRegions[i].Active && stableId > 0)
                        layoutSet.Add(stableId);
                }
            }

            foreach (uint stableId in definitionSet)
            {
                if (layoutSet.Contains(stableId))
                    continue;

                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Warning,
                    "STC021",
                    location,
                    $"Definition SourceStableId is not present in active Layout source regions. stableId={stableId}"));
            }

            foreach (uint stableId in layoutSet)
            {
                if (definitionSet.Contains(stableId))
                    continue;

                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Warning,
                    "STC022",
                    location,
                    $"Active Layout source region StableId is not present in Definition.SourceBindings. stableId={stableId}"));
            }
        }

        private static void ValidateOperationalReference(
            UnityEngine.Object asset,
            string location,
            string code,
            string message,
            List<ContentValidationIssue> issues)
        {
            if (asset == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!IsTestOnlyPath(assetPath))
                return;

            issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Error,
                code,
                location,
                $"{message} asset={assetPath}"));
        }

        private static bool IsTestOnlyPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return assetPath.Replace('\\', '/').StartsWith(TestDataRootPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildEntryLocation(string prefix, int index, string entryKey)
        {
            string safeKey = string.IsNullOrWhiteSpace(entryKey) ? "(empty)" : entryKey.Trim();
            return $"{prefix}::Entries[{index}] (EntryKey={safeKey})";
        }
    }
}
