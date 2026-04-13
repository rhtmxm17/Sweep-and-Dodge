using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum ContentValidationSeverity : byte
    {
        Warning = 0,
        Error = 1,
    }

    public readonly struct ContentValidationIssue
    {
        public readonly ContentValidationSeverity Severity;
        public readonly string Code;
        public readonly string Location;
        public readonly string Message;

        public ContentValidationIssue(ContentValidationSeverity severity, string code, string location, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct ContentValidationRecord<T> where T : UnityEngine.Object
    {
        public readonly T Value;
        public readonly string Location;

        public ContentValidationRecord(T value, string location)
        {
            Value = value;
            Location = location ?? string.Empty;
        }
    }

    public readonly struct ContentValidationInput
    {
        public readonly IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> Definitions;
        public readonly IReadOnlyList<ContentValidationRecord<WaveClipSO>> WaveClips;
        public readonly IReadOnlyList<ContentValidationRecord<StageTopologyPrefabCatalogSO>> TopologyPrefabCatalogs;
        public readonly IReadOnlyList<ContentValidationRecord<PlayerCleanupActionSetSO>> CleanupActionSets;
        public readonly IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> VisualAuthorings;
        public readonly IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> SourceAuthorings;
        public readonly IReadOnlyList<ContentValidationRecord<BulletAuthoring>> BulletAuthorings;
        public readonly IReadOnlyList<ContentValidationRecord<PlayerProxyAuthoring>> PlayerProxyAuthorings;

        public ContentValidationInput(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveClipSO>> waveClips,
            IReadOnlyList<ContentValidationRecord<StageTopologyPrefabCatalogSO>> topologyPrefabCatalogs,
            IReadOnlyList<ContentValidationRecord<PlayerCleanupActionSetSO>> cleanupActionSets,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings,
            IReadOnlyList<ContentValidationRecord<PlayerProxyAuthoring>> playerProxyAuthorings)
        {
            Definitions = definitions ?? Array.Empty<ContentValidationRecord<BulletDefinitionSO>>();
            WaveClips = waveClips ?? Array.Empty<ContentValidationRecord<WaveClipSO>>();
            TopologyPrefabCatalogs = topologyPrefabCatalogs ?? Array.Empty<ContentValidationRecord<StageTopologyPrefabCatalogSO>>();
            CleanupActionSets = cleanupActionSets ?? Array.Empty<ContentValidationRecord<PlayerCleanupActionSetSO>>();
            VisualAuthorings = visualAuthorings ?? Array.Empty<ContentValidationRecord<BulletVisualPrefabAuthoring>>();
            SourceAuthorings = sourceAuthorings ?? Array.Empty<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>>();
            BulletAuthorings = bulletAuthorings ?? Array.Empty<ContentValidationRecord<BulletAuthoring>>();
            PlayerProxyAuthorings = playerProxyAuthorings ?? Array.Empty<ContentValidationRecord<PlayerProxyAuthoring>>();
        }

        public ContentValidationInput(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveClipSO>> waveClips,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings)
            : this(definitions, waveClips, null, null, visualAuthorings, sourceAuthorings, bulletAuthorings, null)
        {
        }

        public ContentValidationInput(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveClipSO>> waveClips,
            IReadOnlyList<ContentValidationRecord<StageTopologyPrefabCatalogSO>> topologyPrefabCatalogs,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings)
            : this(definitions, waveClips, topologyPrefabCatalogs, null, visualAuthorings, sourceAuthorings, bulletAuthorings, null)
        {
        }
    }

    public static class ContentValidationRules
    {
        private const string TestDataRootPath = "Assets/_Project/99_Tests/";

        public static List<ContentValidationIssue> Validate(in ContentValidationInput input)
        {
            var issues = new List<ContentValidationIssue>(64);

            ValidateDefinitionUniqueness(input.Definitions, issues);
            ValidateDefinitionPrefabReferences(input.Definitions, issues);
            ValidateDefinitionBehaviorContracts(input.Definitions, issues);
            ValidateStageTopologyPrefabCatalogContracts(input.TopologyPrefabCatalogs, issues);
            ValidateCleanupActionSetContracts(input.CleanupActionSets, issues);
            ValidatePlayerProxyAuthoringContracts(input.PlayerProxyAuthorings, issues);
            ValidateVisualAuthoringContracts(input.VisualAuthorings, issues);
            ValidateWaveClipContracts(input.Definitions, input.WaveClips, issues);
            ValidateBulletAuthoringRenderContracts(input.BulletAuthorings, issues);
            ValidateSourceTemplateHazardHierarchy(input.SourceAuthorings, issues);
            ValidateAutoCorrectionWarnings(input.Definitions, input.VisualAuthorings, input.SourceAuthorings, issues);

            return issues;
        }

        private static void ValidateDefinitionUniqueness(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            List<ContentValidationIssue> issues)
        {
            var ownersById = new Dictionary<int, List<string>>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null || def.DefinitionId == 0)
                    continue;

                if (!ownersById.TryGetValue(def.DefinitionId, out var locations))
                {
                    locations = new List<string>(2);
                    ownersById.Add(def.DefinitionId, locations);
                }

                locations.Add(definitions[i].Location);
            }

            foreach (var pair in ownersById)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV001",
                        pair.Value[i],
                        $"Duplicate DefinitionId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateDefinitionPrefabReferences(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null)
                    continue;

                if (def.Prefab == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV002",
                        definitions[i].Location,
                        "BulletDefinitionSO.Prefab is null."));
                }
            }
        }

        private static void ValidateDefinitionBehaviorContracts(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            List<ContentValidationIssue> issues)
        {
            var knownKeys = new HashSet<int>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def != null && def.DefinitionId > 0)
                    knownKeys.Add(def.DefinitionId);
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null)
                    continue;

                string location = definitions[i].Location;
                ValidateMovementDefinition(def, location, issues);
                ValidateReactionDefinition(def.OnMotionCompletedExplode, knownKeys, location, "OnMotionCompletedExplode", issues);
                ValidateReactionDefinition(def.OnCleanupRemovedSpawnSecondary, knownKeys, location, "OnCleanupRemovedSpawnSecondary", issues);
                ValidateForbiddenOptionalAuthorings(def, location, issues);
            }
        }

        private static void ValidateVisualAuthoringContracts(
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> authorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < authorings.Count; i++)
            {
                var authoring = authorings[i].Value;
                if (authoring == null)
                    continue;

                if (authoring.Definitions == null || authoring.Definitions.Length <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV004",
                        authorings[i].Location,
                        "BulletVisualPrefabAuthoring.Definitions buffer is null or empty."));
                    continue;
                }

                var duplicateCheck = new HashSet<int>();
                for (int j = 0; j < authoring.Definitions.Length; j++)
                {
                    var def = authoring.Definitions[j];
                    if (def == null)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV005",
                            authorings[i].Location,
                            $"BulletVisualPrefabAuthoring.Definitions[{j}] is null."));
                        continue;
                    }

                    if (def.DefinitionId <= 0)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV027",
                            authorings[i].Location,
                            $"BulletVisualPrefabAuthoring.Definitions[{j}] has invalid DefinitionId {def.DefinitionId}."));
                        continue;
                    }

                    if (!duplicateCheck.Add(def.DefinitionId))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV003",
                            authorings[i].Location,
                            $"BulletVisualPrefabAuthoring contains duplicate DefinitionId {def.DefinitionId}."));
                    }
                }
            }
        }

        private static void ValidateWaveClipContracts(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveClipSO>> clips,
            List<ContentValidationIssue> issues)
        {
            var knownKeys = new HashSet<int>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def != null && def.DefinitionId != 0)
                    knownKeys.Add(def.DefinitionId);
            }

            var clipOwnersByClipId = new Dictionary<int, List<string>>();
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i].Value;
                if (clip == null || clip.ClipId <= 0)
                    continue;

                if (!clipOwnersByClipId.TryGetValue(clip.ClipId, out var owners))
                {
                    owners = new List<string>(2);
                    clipOwnersByClipId.Add(clip.ClipId, owners);
                }

                owners.Add(clips[i].Location);
            }

            foreach (var pair in clipOwnersByClipId)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV009",
                        pair.Value[i],
                        $"Duplicate ClipId detected: {pair.Key}. Owners: {joined}"));
                }
            }

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i].Value;
                if (clip == null)
                    continue;

                var sharedManagedReferenceIssues = WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip);
                for (int issueIndex = 0; issueIndex < sharedManagedReferenceIssues.Count; issueIndex++)
                {
                    var issue = sharedManagedReferenceIssues[issueIndex];
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV041",
                        $"{clips[i].Location}::{issue.DuplicateLocation}",
                        $"Shared SerializeReference graph detected for {issue.SlotName}. First owner: {clips[i].Location}::{issue.FirstLocation}. Duplicate owner: {clips[i].Location}::{issue.DuplicateLocation}. Use 'Repair Shared References' to uniquify managed nodes."));
                }

                if (clip.Segments == null || clip.Segments.Length <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV008",
                        clips[i].Location,
                        "WaveClipSO.Segments buffer is null or empty."));
                    continue;
                }

                for (int s = 0; s < clip.Segments.Length; s++)
                {
                    var seg = clip.Segments[s];
                    string segmentLocation = $"{clips[i].Location}::Segments[{s}]";
                    float rawDurationSec = seg.DurationSec;
                    float startSec = seg.StartSec;
                    float clipDurationSec = clip.DurationSec;
                    float effectiveDurationSec = ResolveEffectiveSegmentDurationSec(startSec, rawDurationSec, clipDurationSec);
                    if (rawDurationSec <= 0f || effectiveDurationSec <= 0f)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV010",
                            segmentLocation,
                            $"Clip segment has invalid active duration at segmentIndex={s}. StartSec={startSec}, DurationSec={rawDurationSec}, ClipDurationSec={clipDurationSec}."));
                        continue;
                    }

                    int entryCount = seg.Directives?.Length ?? 0;
                    if (entryCount <= 0)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV012",
                            segmentLocation,
                            $"Clip segment has no entries at segmentIndex={s}."));
                    }
                    else
                    {
                        for (int e = 0; e < entryCount; e++)
                        {
                            if (!TryBuildWaveClipValidationEntry(
                                    in seg,
                                    e,
                                    segmentLocation,
                                    out var validationEntry,
                                    out string authoringError))
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV040",
                                    $"{segmentLocation}/Directives[{e}]",
                                    authoringError));
                                continue;
                            }

                            var bullet = validationEntry.Snapshot.Bullet;
                            if (bullet == null)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV013",
                                    validationEntry.EntryLocation,
                                    $"Clip segment has null bullet entry at segmentIndex={s}, entryIndex={e}."));
                                continue;
                            }

                            if (bullet.DefinitionId <= 0)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV027",
                                    validationEntry.EntryLocation,
                                    $"Clip segment references invalid DefinitionId {bullet.DefinitionId} at segmentIndex={s}, entryIndex={e}."));
                                continue;
                            }

                            if (!knownKeys.Contains(bullet.DefinitionId))
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV014",
                                    validationEntry.EntryLocation,
                                    $"Clip segment references unknown DefinitionId {bullet.DefinitionId} at segmentIndex={s}, entryIndex={e}."));
                            }

                            var snapshot = validationEntry.Snapshot;
                            var emissionMode = snapshot.EmissionMode;
                            if (emissionMode == SourceSpawnEmissionModeId.RateField && validationEntry.RawRatePerSecPerArea < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV015",
                                    validationEntry.EntryLocation,
                                    $"Clip segment has negative RatePerSecPerArea at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (emissionMode == SourceSpawnEmissionModeId.Poisson && validationEntry.RawMeanEventsPerSec < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV017",
                                    validationEntry.EntryLocation,
                                    $"Clip segment has negative MeanEventsPerSec at segmentIndex={s}, entryIndex={e}."));
                            }

                            if ((emissionMode == SourceSpawnEmissionModeId.Poisson
                                  || emissionMode == SourceSpawnEmissionModeId.EventBurst)
                                && validationEntry.RawEventRepeatCount <= 0)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV022",
                                    validationEntry.EntryLocation,
                                    $"Clip segment has invalid EventRepeatCount at segmentIndex={s}, entryIndex={e}."));
                            }

                            if ((emissionMode == SourceSpawnEmissionModeId.Poisson
                                  || emissionMode == SourceSpawnEmissionModeId.EventBurst)
                                && snapshot.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed
                                && validationEntry.RawEventShotIntervalSec <= 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV025",
                                    validationEntry.EntryLocation,
                                    $"Clip segment uses Timed EventShotSchedule with non-positive EventShotIntervalSec at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (emissionMode == SourceSpawnEmissionModeId.EventBurst)
                            {
                                if (validationEntry.RawBurstIntervalSec <= 0f)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV020",
                                        validationEntry.EntryLocation,
                                        $"Clip segment has non-positive BurstIntervalSec at segmentIndex={s}, entryIndex={e}."));
                                }

                                int repeatCount = validationEntry.RawBurstRepeatCount;
                                if (repeatCount == 0 || repeatCount < -1)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV021",
                                        validationEntry.EntryLocation,
                                        $"Clip segment has invalid BurstRepeatCount at segmentIndex={s}, entryIndex={e}. Use -1 or >= 1."));
                                }

                                int maxReachableBurstEvents = ResolveMaxReachableBurstEvents(
                                    effectiveDurationSec,
                                    validationEntry.RawBurstIntervalSec);
                                if (repeatCount >= 1
                                    && validationEntry.RawBurstIntervalSec > 0f
                                    && maxReachableBurstEvents >= 0
                                    && repeatCount > maxReachableBurstEvents)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Warning,
                                        "CVW040",
                                        validationEntry.EntryLocation,
                                        $"Clip segment configures BurstRepeatCount={repeatCount} but only {maxReachableBurstEvents} burst events are reachable within segment duration; tail bursts may never trigger."));
                                }
                            }

                            if (snapshot.SpawnMode == SourceSpawnModeId.CapAndMaxDensity && validationEntry.RawMaxActiveDensityPerArea < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV016",
                                    validationEntry.EntryLocation,
                                    $"Clip segment has negative MaxActiveDensityPerArea for CapAndMaxDensity at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (validationEntry.RawSpawnSampleBudget <= 0)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV018",
                                    validationEntry.EntryLocation,
                                    $"Clip segment has non-positive SpawnSampleBudget at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (validationEntry.RawPlayerNoSpawnRadius < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV019",
                                    validationEntry.EntryLocation,
                                    $"Clip segment has negative PlayerNoSpawnRadius at segmentIndex={s}, entryIndex={e}."));
                            }

                            var shotPatternMode = snapshot.ShotPatternMode;
                            if (shotPatternMode == WaveShotPatternModeId.NWay
                                && (validationEntry.RawShotCount < 2 || validationEntry.RawNWayAngleSpacingDeg <= 0f))
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV023",
                                    validationEntry.EntryLocation,
                                    $"Clip segment uses NWay ShotPattern with ShotCount < 2 or AngleSpacingDeg <= 0 at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (shotPatternMode == WaveShotPatternModeId.Radial && validationEntry.RawShotCount < 2)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV024",
                                    validationEntry.EntryLocation,
                                    $"Clip segment uses Radial ShotPattern with ShotCount < 2 at segmentIndex={s}, entryIndex={e}."));
                            }

                            var aimMode = snapshot.AimMode;
                            if (aimMode == WaveAimModeId.Spiral && Mathf.Abs(validationEntry.RawSpiralStepDeg) < 0.0001f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Warning,
                                    "CVW032",
                                    validationEntry.EntryLocation,
                                    $"Clip segment uses Spiral with near-zero SpiralStepDeg at segmentIndex={s}, entryIndex={e}."));
                            }

                            var positionPatternMode = snapshot.PositionPatternMode;
                            if (aimMode == WaveAimModeId.LineNormal
                                && positionPatternMode != WavePositionPatternModeId.LineEven)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV042",
                                    validationEntry.EntryLocation,
                                    $"Clip segment uses LineNormalAim without LineEven PositionPattern at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (positionPatternMode == WavePositionPatternModeId.LineEven)
                            {
                                if (validationEntry.RawLineLength <= 0f || validationEntry.RawLineSampleSpacing <= 0f)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV026",
                                        validationEntry.EntryLocation,
                                        $"Clip segment has invalid LineEven parameters at segmentIndex={s}, entryIndex={e}."));
                                }
                            }

                            if (positionPatternMode == WavePositionPatternModeId.PointSet)
                            {
                                if (validationEntry.RawPointCount <= 0)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV028",
                                        validationEntry.EntryLocation,
                                        $"Clip segment uses PointSet with PointCount <= 0 at segmentIndex={s}, entryIndex={e}."));
                                }

                                if (validationEntry.RawPointCount > PointSetPositionPatternAuthoring.MaxPointCount)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Warning,
                                        "CVW033",
                                        validationEntry.EntryLocation,
                                        $"Clip segment PointSet PointCount exceeds max({PointSetPositionPatternAuthoring.MaxPointCount}) and will be clamped at segmentIndex={s}, entryIndex={e}."));
                                }
                            }
                        }
                    }
                }
            }
        }

        private readonly struct WaveClipValidationEntry
        {
            public readonly string EntryLocation;
            public readonly ResolvedWaveSpawnDirectiveSnapshot Snapshot;
            public readonly float RawRatePerSecPerArea;
            public readonly float RawMeanEventsPerSec;
            public readonly int RawBurstRepeatCount;
            public readonly float RawBurstIntervalSec;
            public readonly int RawEventRepeatCount;
            public readonly float RawEventShotIntervalSec;
            public readonly float RawMaxActiveDensityPerArea;
            public readonly int RawSpawnSampleBudget;
            public readonly float RawPlayerNoSpawnRadius;
            public readonly float RawSpiralStepDeg;
            public readonly WaveAimSnapshotTimingId RawAimSnapshotTiming;
            public readonly int RawShotCount;
            public readonly float RawNWayAngleSpacingDeg;
            public readonly float RawLineLength;
            public readonly float RawLineSampleSpacing;
            public readonly int RawPointCount;

            public WaveClipValidationEntry(
                string entryLocation,
                in ResolvedWaveSpawnDirectiveSnapshot snapshot,
                float rawRatePerSecPerArea,
                float rawMeanEventsPerSec,
                int rawBurstRepeatCount,
                float rawBurstIntervalSec,
                int rawEventRepeatCount,
                float rawEventShotIntervalSec,
                float rawMaxActiveDensityPerArea,
                int rawSpawnSampleBudget,
                float rawPlayerNoSpawnRadius,
                float rawSpiralStepDeg,
                WaveAimSnapshotTimingId rawAimSnapshotTiming,
                int rawShotCount,
                float rawNWayAngleSpacingDeg,
                float rawLineLength,
                float rawLineSampleSpacing,
                int rawPointCount)
            {
                EntryLocation = entryLocation;
                Snapshot = snapshot;
                RawRatePerSecPerArea = rawRatePerSecPerArea;
                RawMeanEventsPerSec = rawMeanEventsPerSec;
                RawBurstRepeatCount = rawBurstRepeatCount;
                RawBurstIntervalSec = rawBurstIntervalSec;
                RawEventRepeatCount = rawEventRepeatCount;
                RawEventShotIntervalSec = rawEventShotIntervalSec;
                RawMaxActiveDensityPerArea = rawMaxActiveDensityPerArea;
                RawSpawnSampleBudget = rawSpawnSampleBudget;
                RawPlayerNoSpawnRadius = rawPlayerNoSpawnRadius;
                RawSpiralStepDeg = rawSpiralStepDeg;
                RawAimSnapshotTiming = rawAimSnapshotTiming;
                RawShotCount = rawShotCount;
                RawNWayAngleSpacingDeg = rawNWayAngleSpacingDeg;
                RawLineLength = rawLineLength;
                RawLineSampleSpacing = rawLineSampleSpacing;
                RawPointCount = rawPointCount;
            }
        }

        private static bool TryBuildWaveClipValidationEntry(
            in WaveClipSO.ClipSegment segment,
            int entryIndex,
            string segmentLocation,
            out WaveClipValidationEntry validationEntry,
            out string error)
        {
            string entryLocation = $"{segmentLocation}/Directives[{entryIndex}]";
            var typedEntry = segment.Directives[entryIndex];
            if (!WaveClipAuthoringResolver.TryResolveTypedEntry(typedEntry, out var snapshot, out error))
            {
                validationEntry = default;
                return false;
            }

            float rawRate = 0f;
            float rawMean = 0f;
            int rawBurstRepeatCount = 1;
            float rawBurstIntervalSec = 1f;
            int rawEventRepeatCount = 1;
            float rawEventShotIntervalSec = 0f;
            float rawMaxActiveDensity = typedEntry.Emission.MaxActiveDensityPerArea;
            switch (typedEntry.Emission)
            {
                case RateFieldEmissionAuthoring rateField:
                    rawRate = rateField.RatePerSecPerArea;
                    break;
                case PoissonEmissionAuthoring poisson:
                    rawMean = poisson.MeanEventsPerSec;
                    rawEventRepeatCount = poisson.EventRepeatCount;
                    rawEventShotIntervalSec = poisson.EventShotIntervalSec;
                    break;
                case EventBurstEmissionAuthoring eventBurst:
                    rawBurstRepeatCount = eventBurst.BurstRepeatCount;
                    rawBurstIntervalSec = eventBurst.BurstIntervalSec;
                    rawEventRepeatCount = eventBurst.EventRepeatCount;
                    rawEventShotIntervalSec = eventBurst.EventShotIntervalSec;
                    break;
            }

            int rawSpawnSampleBudget = typedEntry.Sampling.SpawnSampleBudget;
            float rawPlayerNoSpawnRadius = typedEntry.Sampling.PlayerNoSpawnRadius;
            float rawLineLength = 0f;
            float rawLineSampleSpacing = 0f;
            int rawPointCount = 0;
            switch (typedEntry.PositionPattern)
            {
                case LineEvenPositionPatternAuthoring lineEven:
                    rawLineLength = (lineEven.LineEnd - lineEven.LineStart).magnitude;
                    rawLineSampleSpacing = lineEven.SampleSpacing;
                    break;
                case PointSetPositionPatternAuthoring pointSet:
                    rawPointCount = pointSet.Points?.Length ?? 0;
                    break;
            }

            int rawShotCount = 1;
            float rawNWayAngleSpacingDeg = 0f;
            float rawSpiralStepDeg = 0f;
            WaveAimSnapshotTimingId rawAimSnapshotTiming = WaveAimSnapshotTimingId.EventStart;
            switch (typedEntry.Aim)
            {
                case SpiralAimAuthoring spiral:
                    rawSpiralStepDeg = spiral.SpiralStepDeg;
                    break;
                case PlayerPositionAimAuthoring playerPositionAim:
                    rawAimSnapshotTiming = playerPositionAim.SnapshotTiming;
                    break;
            }

            switch (typedEntry.ShotPattern)
            {
                case NWayShotPatternAuthoring nWay:
                    rawShotCount = nWay.ShotCount;
                    rawNWayAngleSpacingDeg = nWay.AngleSpacingDeg;
                    break;
                case RadialShotPatternAuthoring radial:
                    rawShotCount = radial.ShotCount;
                    break;
            }

            validationEntry = new WaveClipValidationEntry(
                entryLocation,
                in snapshot,
                rawRate,
                rawMean,
                rawBurstRepeatCount,
                rawBurstIntervalSec,
                rawEventRepeatCount,
                rawEventShotIntervalSec,
                rawMaxActiveDensity,
                rawSpawnSampleBudget,
                rawPlayerNoSpawnRadius,
                rawSpiralStepDeg,
                rawAimSnapshotTiming,
                rawShotCount,
                rawNWayAngleSpacingDeg,
                rawLineLength,
                rawLineSampleSpacing,
                rawPointCount);
            error = string.Empty;
            return true;
        }

        private static float ResolveEffectiveSegmentDurationSec(float startSec, float durationSec, float clipDurationSec)
        {
            float safeStartSec = Mathf.Max(0f, startSec);
            float safeDurationSec = Mathf.Max(0f, durationSec);
            float endSec = safeStartSec + safeDurationSec;
            if (clipDurationSec > 0f)
                endSec = Mathf.Min(endSec, clipDurationSec);

            return Mathf.Max(0f, endSec - safeStartSec);
        }

        private static int ResolveMaxReachableBurstEvents(float effectiveDurationSec, float burstIntervalSec)
        {
            if (effectiveDurationSec <= 0f || burstIntervalSec <= 0f)
                return -1;

            const float burstScheduleEpsilon = 1e-5f;
            float safeDurationSec = Mathf.Max(0f, effectiveDurationSec);
            float safeIntervalSec = Mathf.Max(0.001f, burstIntervalSec);
            return 1 + Mathf.FloorToInt(Mathf.Max(0f, safeDurationSec - burstScheduleEpsilon) / safeIntervalSec);
        }

        private static void ValidateAutoCorrectionWarnings(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null)
                    continue;

                WarnIf(def.PoolSize < 0, "CVW001", definitions[i].Location, "PoolSize < 0 will be clamped to 0.", issues);
                WarnIf(def.Speed < 0f, "CVW002", definitions[i].Location, "Speed < 0 will be clamped to 0.", issues);
                WarnIf(def.Lifetime < 0f, "CVW003", definitions[i].Location, "Lifetime < 0 will be clamped to 0.", issues);
                WarnIf(def.Radius < 0f, "CVW004", definitions[i].Location, "Radius < 0 will be clamped to 0.", issues);
                WarnIf(def.ScoreValue < 0, "CVW005", definitions[i].Location, "ScoreValue < 0 will be clamped to 0.", issues);
            }

            for (int i = 0; i < visualAuthorings.Count; i++)
            {
                var authoring = visualAuthorings[i].Value;
                if (authoring == null || authoring.Definitions == null)
                    continue;

                for (int d = 0; d < authoring.Definitions.Length; d++)
                {
                    var def = authoring.Definitions[d];
                    if (def == null)
                        continue;

                    WarnIf(def.PoolSize < 0, "CVW011", visualAuthorings[i].Location, "Definition PoolSize < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.Speed < 0f, "CVW012", visualAuthorings[i].Location, "Definition Speed < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.Lifetime < 0f, "CVW013", visualAuthorings[i].Location, "Definition Lifetime < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.Radius < 0f, "CVW014", visualAuthorings[i].Location, "Definition Radius < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.ScoreValue < 0, "CVW015", visualAuthorings[i].Location, "Definition ScoreValue < 0 will be clamped to 0 at bake.", issues);
                }
            }

            for (int i = 0; i < sourceAuthorings.Count; i++)
            {
                var authoring = sourceAuthorings[i].Value;
                if (authoring == null)
                    continue;

                string location = sourceAuthorings[i].Location;

                WarnIf(authoring.Radius < 0f, "CVW024", location, "Radius < 0 will be clamped to 0.", issues);
                WarnIf(authoring.Size.x < 0f || authoring.Size.y < 0f, "CVW025", location, "Size contains negative values and will be clamped to >= 0.", issues);
                WarnIf(authoring.PollutionCellSize < 0.1f, "CVW026", location, "PollutionCellSize < 0.1 will be clamped to 0.1.", issues);
                WarnIf(authoring.PollutionMin < 0f, "CVW027", location, "PollutionMin < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionMax < authoring.PollutionMin, "CVW028", location, "PollutionMax < PollutionMin will be clamped up to PollutionMin.", issues);
                WarnIf(authoring.PollutionRegenPerSec < 0f, "CVW029", location, "PollutionRegenPerSec < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionDropPerCollect < 0f, "CVW030", location, "PollutionDropPerCollect < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionTopKSampleCount < 1, "CVW031", location, "PollutionTopKSampleCount < 1 will be clamped to 1.", issues);
                WarnIf(authoring.PollutionActiveRatioThreshold < 0f || authoring.PollutionActiveRatioThreshold > 1f, "CVW034", location, "PollutionActiveRatioThreshold will be clamped to [0, 1].", issues);
                WarnIf(authoring.PollutionRecoveryCooldownFrames < 0, "CVW035", location, "PollutionRecoveryCooldownFrames < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionRecoveryWaveSeedCount < 1, "CVW036", location, "PollutionRecoveryWaveSeedCount < 1 will be clamped to 1.", issues);
                WarnIf(authoring.PollutionRecoveryWaveClusterSize < 1, "CVW037", location, "PollutionRecoveryWaveClusterSize < 1 will be clamped to 1.", issues);
                WarnIf(authoring.PollutionRecoveryRestoreValue < authoring.PollutionMin || authoring.PollutionRecoveryRestoreValue > authoring.PollutionMax, "CVW038", location, "PollutionRecoveryRestoreValue will be clamped to [PollutionMin, PollutionMax].", issues);
                WarnIf(authoring.PollutionRecoveryRecentCleanBiasFrames < 0, "CVW039", location, "PollutionRecoveryRecentCleanBiasFrames < 0 will be clamped to 0.", issues);
            }
        }

        private static void ValidateStageTopologyPrefabCatalogContracts(
            IReadOnlyList<ContentValidationRecord<StageTopologyPrefabCatalogSO>> catalogs,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < catalogs.Count; i++)
            {
                var catalog = catalogs[i].Value;
                if (catalog == null)
                    continue;

                if (catalog.SourceTemplatePrefab == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV030",
                        catalogs[i].Location,
                        "StageTopologyPrefabCatalogSO.SourceTemplatePrefab is null."));
                    continue;
                }

                string catalogPath = AssetDatabase.GetAssetPath(catalog);
                if (!IsTestOnlyPath(catalogPath))
                {
                    string prefabPath = AssetDatabase.GetAssetPath(catalog.SourceTemplatePrefab);
                    if (IsTestOnlyPath(prefabPath))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV045",
                            catalogs[i].Location,
                            $"Operational StageTopologyPrefabCatalogSO cannot reference test-only SourceTemplatePrefab. asset={prefabPath}"));
                    }
                }
            }
        }

        private static void ValidateSourceTemplateHazardHierarchy(
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < sourceAuthorings.Count; i++)
            {
                var sourceAuthoring = sourceAuthorings[i].Value;
                if (sourceAuthoring == null)
                    continue;

                string location = sourceAuthorings[i].Location;
                var actors = sourceAuthoring.GetComponentsInChildren<HazardActorAuthoring>(true);
                var emitters = sourceAuthoring.GetComponentsInChildren<HazardEmitterAuthoring>(true);
                var actorOwners = new Dictionary<int, List<string>>();

                for (int actorIndex = 0; actorIndex < actors.Length; actorIndex++)
                {
                    var actor = actors[actorIndex];
                    if (actor == null)
                        continue;

                    string actorLocation = $"{location}::HazardActor[{actor.name}]";
                    if (!actorOwners.TryGetValue(actor.ActorId, out var owners))
                    {
                        owners = new List<string>(2);
                        actorOwners.Add(actor.ActorId, owners);
                    }

                    owners.Add(actorLocation);

                    if (!HazardActorAuthoringValidationUtility.TryValidate(actor, out _, out _, out _, out var actorErrorKind, out var actorError))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            actorErrorKind == HazardActorAuthoringValidationErrorKind.PhaseTransition ? "CV092" : "CV091",
                            actorLocation,
                            actorError));
                    }

                    var emitterOwners = new Dictionary<int, List<string>>();
                    for (int emitterIndex = 0; emitterIndex < emitters.Length; emitterIndex++)
                    {
                        var emitter = emitters[emitterIndex];
                        if (emitter == null)
                            continue;

                        var parentActor = emitter.GetComponentInParent<HazardActorAuthoring>(true);
                        if (parentActor != actor)
                            continue;

                        string emitterLocation = $"{actorLocation}/HazardEmitter[{emitter.name}]";
                        if (!emitterOwners.TryGetValue(emitter.EmitterId, out var emitterOwnerLocations))
                        {
                            emitterOwnerLocations = new List<string>(2);
                            emitterOwners.Add(emitter.EmitterId, emitterOwnerLocations);
                        }

                        emitterOwnerLocations.Add(emitterLocation);
                        ValidateHazardEmitterSlots(emitter, emitterLocation, issues);
                    }

                    foreach (var pair in emitterOwners)
                    {
                        if (pair.Value.Count <= 1)
                            continue;

                        string joined = string.Join(", ", pair.Value);
                        for (int ownerIndex = 0; ownerIndex < pair.Value.Count; ownerIndex++)
                        {
                            issues.Add(new ContentValidationIssue(
                                ContentValidationSeverity.Error,
                                "CV043",
                                pair.Value[ownerIndex],
                                $"Duplicate HazardEmitterAuthoring.EmitterId detected under one HazardActorAuthoring. emitterId={pair.Key}, owners={joined}"));
                        }
                    }
                }

                foreach (var pair in actorOwners)
                {
                    if (pair.Value.Count <= 1)
                        continue;

                    string joined = string.Join(", ", pair.Value);
                    for (int ownerIndex = 0; ownerIndex < pair.Value.Count; ownerIndex++)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV042",
                            pair.Value[ownerIndex],
                            $"Duplicate HazardActorAuthoring.ActorId detected under one source template. actorId={pair.Key}, owners={joined}"));
                    }
                }

                for (int emitterIndex = 0; emitterIndex < emitters.Length; emitterIndex++)
                {
                    var emitter = emitters[emitterIndex];
                    if (emitter == null)
                        continue;

                    var parentActor = emitter.GetComponentInParent<HazardActorAuthoring>(true);
                    if (parentActor != null)
                        continue;

                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV044",
                        $"{location}::HazardEmitter[{emitter.name}]",
                        "HazardEmitterAuthoring under a source template requires a parent HazardActorAuthoring."));
                }
            }
        }

        private static void ValidateHazardEmitterSlots(
            HazardEmitterAuthoring emitter,
            string location,
            List<ContentValidationIssue> issues)
        {
            var slots = emitter.Slots ?? Array.Empty<HazardEmitterPatternSlotAuthoring>();
            if (slots.Length <= 0)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV046",
                    location,
                    "HazardEmitterAuthoring requires at least one pattern slot."));
                return;
            }

            var slotOwners = new Dictionary<int, List<string>>();
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var slot = slots[slotIndex];
                string slotLocation = $"{location}/Slots[{slotIndex}]";

                if (slot.PatternSlotId < 1)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV047",
                        slotLocation,
                        $"HazardEmitter pattern slot requires PatternSlotId >= 1. current={slot.PatternSlotId}"));
                }

                if (slot.BaseWeight < 0f)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV049",
                        slotLocation,
                        $"HazardEmitter pattern slot requires BaseWeight >= 0. current={slot.BaseWeight}"));
                }

                if (slot.TelegraphProfile == null || slot.EmissionProfile == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV049",
                        slotLocation,
                        "HazardEmitter pattern slot requires non-null TelegraphProfile and EmissionProfile."));
                }

                if (!slotOwners.TryGetValue(slot.PatternSlotId, out var owners))
                {
                    owners = new List<string>(2);
                    slotOwners.Add(slot.PatternSlotId, owners);
                }

                owners.Add(slotLocation);
            }

            foreach (var pair in slotOwners)
            {
                if (pair.Key < 1 || pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int ownerIndex = 0; ownerIndex < pair.Value.Count; ownerIndex++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV048",
                        pair.Value[ownerIndex],
                        $"Duplicate HazardEmitter pattern slot id detected under one emitter. patternSlotId={pair.Key}, owners={joined}"));
                }
            }
        }

        private static void ValidateCleanupActionSetContracts(
            IReadOnlyList<ContentValidationRecord<PlayerCleanupActionSetSO>> actionSets,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < actionSets.Count; i++)
            {
                var actionSet = actionSets[i].Value;
                if (actionSet == null)
                    continue;

                string location = actionSets[i].Location;
                if (actionSet.Profiles == null || actionSet.Profiles.Length <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV039",
                        location,
                        "PlayerCleanupActionSetSO.Profiles is null or empty."));
                    continue;
                }

                var keyOwners = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int p = 0; p < actionSet.Profiles.Length; p++)
                {
                    var profile = actionSet.Profiles[p];
                    string entryLocation = $"{location}::Profiles[{p}]";
                    if (profile == null)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV039",
                            entryLocation,
                            "Cleanup action profile reference is null."));
                        continue;
                    }

                    if (!PlayerCleanupActionContractUtility.IsValidProfileKey(profile.ProfileKey, out string reason))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV037",
                            entryLocation,
                            reason));
                    }
                    else if (keyOwners.TryGetValue(profile.ProfileKey, out string ownerLocation))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV037",
                            entryLocation,
                            $"Duplicate cleanup action ProfileKey '{profile.ProfileKey}'. First owner: {ownerLocation}"));
                    }
                    else
                    {
                        keyOwners.Add(profile.ProfileKey, entryLocation);
                    }

                    if (profile.CaptureActiveTime < 0f
                        || profile.CaptureCooldown < 0f
                        || profile.ActiveTime < 0f
                        || profile.Cooldown < 0f
                        || profile.ActiveMoveSpeedScale < 0f)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV039",
                            entryLocation,
                            "Cleanup action timing/motion values must be non-negative."));
                    }

                    if (profile is BroomSweepCleanupActionProfileSO broomProfile)
                    {
                        if (broomProfile.TrashSweepOuterRadius <= 0f
                            || broomProfile.TrashSweepHalfAngleDeg <= 0f
                            || broomProfile.HazardRectLength <= 0f)
                        {
                            issues.Add(new ContentValidationIssue(
                                ContentValidationSeverity.Error,
                                "CV039",
                                entryLocation,
                                "BroomSweep profile requires positive TrashSweepOuterRadius, TrashSweepHalfAngleDeg, and HazardRectLength."));
                        }
                    }
                    else if (profile is RadialRingCleanupActionProfileSO radialProfile)
                    {
                        if (radialProfile.TrashRange <= 0f)
                        {
                            issues.Add(new ContentValidationIssue(
                                ContentValidationSeverity.Error,
                                "CV039",
                                entryLocation,
                                "RadialRing profile requires positive TrashRange."));
                        }
                    }
                    else if (profile is ForwardFanLineCleanupActionProfileSO forwardProfile)
                    {
                        if (forwardProfile.TrashRange <= 0f || forwardProfile.HazardLineLength <= 0f)
                        {
                            issues.Add(new ContentValidationIssue(
                                ContentValidationSeverity.Error,
                                "CV039",
                                entryLocation,
                                "ForwardFanLine profile requires positive TrashRange and HazardLineLength."));
                        }
                    }
                    else
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV039",
                            entryLocation,
                            $"Unsupported cleanup action profile type '{profile.GetType().Name}'."));
                    }
                }

                ValidateCleanupActionSetRootKey(location, nameof(actionSet.InitialSelectedProfileKey), actionSet.InitialSelectedProfileKey, keyOwners, issues);
                ValidateCleanupActionSetRootKey(location, nameof(actionSet.PrimarySlotProfileKey), actionSet.PrimarySlotProfileKey, keyOwners, issues);
                ValidateCleanupActionSetRootKey(location, nameof(actionSet.SecondarySlotProfileKey), actionSet.SecondarySlotProfileKey, keyOwners, issues);
            }
        }

        private static void ValidateCleanupActionSetRootKey(
            string location,
            string fieldName,
            string profileKey,
            Dictionary<string, string> keyOwners,
            List<ContentValidationIssue> issues)
        {
            if (!PlayerCleanupActionContractUtility.IsValidProfileKey(profileKey, out string reason))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV038",
                    location,
                    $"{fieldName} is invalid. {reason}"));
                return;
            }

            if (!keyOwners.ContainsKey(profileKey))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV038",
                    location,
                    $"{fieldName} references missing ProfileKey '{profileKey}'."));
            }
        }

        private static void ValidatePlayerProxyAuthoringContracts(
            IReadOnlyList<ContentValidationRecord<PlayerProxyAuthoring>> authorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < authorings.Count; i++)
            {
                var authoring = authorings[i].Value;
                if (authoring == null)
                    continue;

                if (authoring.CleanupActionSet == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV036",
                        authorings[i].Location,
                        "PlayerProxyAuthoring.CleanupActionSet is null."));
                }
            }
        }

        private static bool IsTestOnlyPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return assetPath.Replace('\\', '/').StartsWith(TestDataRootPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateMovementDefinition(
            BulletDefinitionSO definition,
            string location,
            List<ContentValidationIssue> issues)
        {
            switch (definition.MovementFamily)
            {
                case BulletMovementFamilyId.DampedLinear:
                    if (definition.DampedLinear.DampingPerSec < 0f || definition.DampedLinear.StopSpeedThreshold < 0f)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV031",
                            location,
                            "DampedLinear movement requires non-negative DampingPerSec and StopSpeedThreshold."));
                    }
                    break;

                case BulletMovementFamilyId.HomingLite:
                    if (definition.HomingLite.TurnRateDegPerSec < 0f
                        || definition.HomingLite.MaxAcquireDistance < 0f
                        || definition.HomingLite.MinRetargetDistance < 0f
                        || definition.HomingLite.MinRetargetDistance > definition.HomingLite.MaxAcquireDistance)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV032",
                            location,
                            "HomingLite movement requires non-negative parameters and MinRetargetDistance <= MaxAcquireDistance."));
                    }
                    break;
            }
        }

        private static void ValidateReactionDefinition(
            in BulletSecondarySpawnReactionDefinition reaction,
            HashSet<int> knownKeys,
            string location,
            string reactionName,
            List<ContentValidationIssue> issues)
        {
            if (!reaction.Enabled)
                return;

            if (reaction.SpawnCount <= 0
                || reaction.SpreadAngleDeg < 0f
                || reaction.SpawnRadius < 0f
                || reaction.SpawnDelaySec < 0f
                || reaction.SecondaryBullet == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV033",
                    location,
                    $"{reactionName} has invalid enabled reaction parameters."));
                return;
            }

            if (reaction.SecondaryBullet.DefinitionId <= 0)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV035",
                    location,
                    $"{reactionName} references invalid SecondaryBullet definition id {reaction.SecondaryBullet.DefinitionId}."));
                return;
            }

            if (!knownKeys.Contains(reaction.SecondaryBullet.DefinitionId))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV035",
                    location,
                    $"{reactionName} references unknown SecondaryBullet definition id {reaction.SecondaryBullet.DefinitionId}."));
            }
        }

        private static void ValidateForbiddenOptionalAuthorings(
            BulletDefinitionSO definition,
            string location,
            List<ContentValidationIssue> issues)
        {
            if (definition.Prefab == null)
                return;

            var prefab = definition.Prefab;
            if (prefab.GetComponent<BulletDampedMotionAuthoring>() != null
                || prefab.GetComponent<BulletHomingLiteMotionAuthoring>() != null
                || prefab.GetComponent<BulletOnMotionCompletedExplodeReactionAuthoring>() != null
                || prefab.GetComponent<BulletOnCleanupRemovedSpawnSecondaryReactionAuthoring>() != null
                || prefab.GetComponent<BulletOnCollectedSpawnSecondaryReactionAuthoring>() != null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV034",
                    location,
                    "BulletDefinitionSO.Prefab contains forbidden optional bullet behavior authoring. Use BulletDefinitionSO schema instead."));
            }
        }

        private static void WarnIf(bool condition, string code, string location, string message, List<ContentValidationIssue> issues)
        {
            if (!condition)
                return;

            issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, code, location, message));
        }

        private static void ValidateBulletAuthoringRenderContracts(
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < bulletAuthorings.Count; i++)
            {
                var bullet = bulletAuthorings[i].Value;
                if (bullet == null)
                    continue;

                var meshRenderers = bullet.GetComponentsInChildren<MeshRenderer>(true);
                var skinnedRenderers = bullet.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool hasRenderablePart = (meshRenderers != null && meshRenderers.Length > 0)
                    || (skinnedRenderers != null && skinnedRenderers.Length > 0);

                if (!hasRenderablePart)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV007",
                        bulletAuthorings[i].Location,
                        "BulletAuthoring has no MeshRenderer/SkinnedMeshRenderer render parts."));
                }
            }
        }
    }
}

