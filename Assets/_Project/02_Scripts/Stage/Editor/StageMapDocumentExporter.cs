using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapDocumentExporter
    {
        public static StageLayoutSO BuildLayoutSnapshot(StageMapDocument document)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            if (document == null)
                return layout;

            layout.SchemaVersion = 2;
            layout.StageId = Mathf.Max(1, document.StageId);
            layout.Grid = document.Grid;
            layout.Cells = CloneCells(document.Cells);
            layout.SourceRegions = ToSourceRegions(document.SourceRegions);
            layout.DepositRegions = ToDepositRegions(document.DepositRegions);
            layout.PlayerStart = document.PlayerStart;
            layout.Presentations = ToPresentations(document.PresentationLinks);
            layout.GridVisualPrefab = document.TargetLayout != null ? document.TargetLayout.GridVisualPrefab : null;
            return layout;
        }

        public static StageDefinitionSO BuildDefinitionSnapshot(StageMapDocument document)
        {
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            if (document == null)
                return definition;

            definition.StageId = Mathf.Max(1, document.StageId);
            definition.DisplayName = !string.IsNullOrWhiteSpace(document.DisplayName)
                ? document.DisplayName.Trim()
                : $"Stage {Mathf.Max(1, document.StageId)}";
            definition.IsFinalStage = document.IsFinalStage;
            definition.StageTimeLimitSec = Mathf.Max(0.01f, document.StageTimeLimitSec);
            definition.SourceBindings = BuildSourceBindings(document);
            return definition;
        }

        public static StageCatalogEntry BuildCatalogEntry(StageMapDocument document)
        {
            return new StageCatalogEntry
            {
                Enabled = document != null && document.EnabledInCatalog,
                EntryKey = BuildCatalogEntryKey(document),
                Definition = document != null ? document.TargetDefinition : null,
                Layout = document != null ? document.TargetLayout : null,
            };
        }

        public static string BuildCatalogEntryKey(StageMapDocument document)
        {
            if (document == null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(document.CatalogEntryKey)
                ? $"stage_{Mathf.Max(1, document.StageId):00}"
                : document.CatalogEntryKey.Trim();
        }

        internal static StageCellLayoutData[] CloneCells(StageCellLayoutData[] cells)
        {
            return cells != null ? (StageCellLayoutData[])cells.Clone() : Array.Empty<StageCellLayoutData>();
        }

        internal static StageSourceRegionLayoutData[] ToSourceRegions(StageMapRegionData[] regions)
        {
            if (regions == null || regions.Length == 0)
                return Array.Empty<StageSourceRegionLayoutData>();

            var result = new StageSourceRegionLayoutData[regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                result[i] = new StageSourceRegionLayoutData
                {
                    StableId = regions[i].StableId,
                    Active = regions[i].Active,
                    AnchorCell = regions[i].AnchorCell,
                    AnchorOffset = regions[i].AnchorOffset,
                };
            }

            return result;
        }

        internal static StageDepositRegionLayoutData[] ToDepositRegions(StageMapRegionData[] regions)
        {
            if (regions == null || regions.Length == 0)
                return Array.Empty<StageDepositRegionLayoutData>();

            var result = new StageDepositRegionLayoutData[regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                result[i] = new StageDepositRegionLayoutData
                {
                    StableId = regions[i].StableId,
                    Active = regions[i].Active,
                    AnchorCell = regions[i].AnchorCell,
                    AnchorOffset = regions[i].AnchorOffset,
                };
            }

            return result;
        }

        internal static StagePresentationLayoutData[] ToPresentations(StageMapPresentationLinkData[] links)
        {
            if (links == null || links.Length == 0)
                return Array.Empty<StagePresentationLayoutData>();

            var result = new StagePresentationLayoutData[links.Length];
            for (int i = 0; i < links.Length; i++)
            {
                bool linked = links[i].PlacementMode == StagePresentationPlacementMode.LinkedToParent;
                result[i] = new StagePresentationLayoutData
                {
                    StableId = links[i].StableId,
                    Active = links[i].Active,
                    PlacementMode = links[i].PlacementMode,
                    LinkKind = linked ? links[i].LinkKind : StagePresentationLinkKind.None,
                    LinkedStableId = linked ? links[i].LinkedStableId : 0u,
                    PresentationKey = links[i].PresentationKey != null ? links[i].PresentationKey.Trim() : string.Empty,
                    Position = links[i].Position,
                    Euler = links[i].Euler,
                    Scale = links[i].Scale,
                };
            }

            return result;
        }

        private static StageSourceBinding[] BuildSourceBindings(StageMapDocument document)
        {
            var existing = document.TargetDefinition != null
                ? document.TargetDefinition.SourceBindings ?? Array.Empty<StageSourceBinding>()
                : Array.Empty<StageSourceBinding>();
            var existingById = new Dictionary<uint, StageSourceBinding>();
            for (int i = 0; i < existing.Length; i++)
            {
                uint stableId = Math.Max(1u, existing[i].SourceStableId);
                if (!existingById.ContainsKey(stableId))
                    existingById.Add(stableId, existing[i]);
            }

            var sourceIds = CollectSourceStableIds(document.SourceRegions);
            var hazardBySource = BuildHazardsBySource(document.HazardActorPlacements);
            var bindings = new List<StageSourceBinding>(sourceIds.Count);
            for (int i = 0; i < sourceIds.Count; i++)
            {
                uint stableId = sourceIds[i];
                var binding = existingById.TryGetValue(stableId, out var preserved)
                    ? preserved
                    : CreateDefaultBinding(stableId);

                binding.SourceStableId = stableId;
                binding.HazardActorPlacements = hazardBySource.TryGetValue(stableId, out var placements)
                    ? placements
                    : Array.Empty<HazardActorPlacementBinding>();
                if (binding.HazardActorOrchestrationRules == null)
                    binding.HazardActorOrchestrationRules = Array.Empty<HazardActorOrchestrationRuleBinding>();
                if (binding.SustainSlots == null)
                    binding.SustainSlots = Array.Empty<SustainSlotBinding>();
                if (binding.EventSlots == null)
                    binding.EventSlots = Array.Empty<EventSlotBinding>();
                bindings.Add(binding);
            }

            return bindings.ToArray();
        }

        private static List<uint> CollectSourceStableIds(StageMapRegionData[] sourceRegions)
        {
            var ids = new List<uint>();
            var unique = new HashSet<uint>();
            if (sourceRegions != null)
            {
                for (int i = 0; i < sourceRegions.Length; i++)
                {
                    if (!sourceRegions[i].Active || sourceRegions[i].StableId == 0u)
                        continue;
                    if (unique.Add(sourceRegions[i].StableId))
                        ids.Add(sourceRegions[i].StableId);
                }
            }

            ids.Sort();
            return ids;
        }

        private static Dictionary<uint, HazardActorPlacementBinding[]> BuildHazardsBySource(StageMapHazardActorPlacementData[] placements)
        {
            var grouped = new Dictionary<uint, List<HazardActorPlacementBinding>>();
            if (placements != null)
            {
                for (int i = 0; i < placements.Length; i++)
                {
                    var placement = placements[i];
                    if (placement.OwningSourceStableId == 0u || placement.PlacementInstanceId <= 0)
                        continue;

                    if (!grouped.TryGetValue(placement.OwningSourceStableId, out var list))
                    {
                        list = new List<HazardActorPlacementBinding>();
                        grouped.Add(placement.OwningSourceStableId, list);
                    }

                    list.Add(new HazardActorPlacementBinding
                    {
                        PlacementInstanceId = placement.PlacementInstanceId,
                        ActorArchetypePrefab = placement.ActorArchetypePrefab,
                        LocalOffset = placement.SourceLocalOffset,
                        LocalYawDeg = placement.LocalYawDeg,
                    });
                }
            }

            return grouped.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.OrderBy(x => x.PlacementInstanceId).ToArray());
        }

        private static StageSourceBinding CreateDefaultBinding(uint stableId)
        {
            return new StageSourceBinding
            {
                SourceStableId = Math.Max(1u, stableId),
                InitialSourceState = SourceStateId.Normal,
                ThresholdWeakened = 2000,
                ThresholdDepleted = 4000,
                SustainSlots = Array.Empty<SustainSlotBinding>(),
                EventSlots = Array.Empty<EventSlotBinding>(),
                HazardActorPlacements = Array.Empty<HazardActorPlacementBinding>(),
                HazardActorOrchestrationRules = Array.Empty<HazardActorOrchestrationRuleBinding>(),
            };
        }
    }
}
