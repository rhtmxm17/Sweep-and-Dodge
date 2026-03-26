using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageRegionTilemapMigrationUtility
    {
        private const string SampleScenePath = "Assets/_Project/01_Scenes/StageLayoutEditingSampleV1.unity";
        private const string RegionTileAssetFolder = "Assets/_Project/03_Datas/StageCatalog";
        private const string ReferenceMovementTilePath = "Assets/_Project/03_Datas/StageCatalog/smt_block_walkable.asset";
        private const string AutoRunSentinelPath = "Temp/stage_region_tilemap_migration.request";

        [MenuItem("Tools/Project/Stage Layout/Migrate Sample Scene To Unified Region Tilemap")]
        public static void MigrateSampleSceneToUnifiedRegionTilemapMenu()
        {
            MigrateSampleSceneToUnifiedRegionTilemap();
        }

        public static void MigrateSampleSceneToUnifiedRegionTilemapBatch()
        {
            MigrateSampleSceneToUnifiedRegionTilemap();
        }

        [DidReloadScripts]
        private static void RunIfRequested()
        {
            if (!File.Exists(AutoRunSentinelPath))
                return;

            try
            {
                File.Delete(AutoRunSentinelPath);
                MigrateSampleSceneToUnifiedRegionTilemap();
                Debug.Log("[StageLayout] Sample scene migrated to unified RegionTilemap path.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StageLayout] Failed to migrate sample scene to unified RegionTilemap path. {ex}");
                throw;
            }
        }

        public static void MigrateSampleSceneToUnifiedRegionTilemap()
        {
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new InvalidOperationException($"Failed to open sample scene: {SampleScenePath}");

            var assetCache = new RegionTileAssetCache(LoadReferenceSprite());
            var stages = UnityEngine.Object.FindObjectsByType<StageLayoutStageMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(x => x.StageId)
                .ToArray();

            if (stages.Length <= 0)
                throw new InvalidOperationException("No StageLayoutStageMarker found in sample scene.");

            for (int i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                if (stage == null || !stage.TryGetComponent(out StageGridAuthoring authoring) || authoring == null)
                    throw new InvalidOperationException($"StageId={stage?.StageId.ToString() ?? "(null)"} is missing StageGridAuthoring.");

                MigrateStage(stage, authoring, assetCache);
            }

            var roots = UnityEngine.Object.FindObjectsByType<StageLayoutRootMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(roots, (a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
            for (int i = 0; i < roots.Length; i++)
            {
                if (!StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(roots[i], out var issues, saveAssets: true))
                    throw new InvalidOperationException($"StageLayout generation failed for root '{roots[i].name}'. {FormatIssues(issues)}");

                if (issues != null && issues.Any(x => x.Severity == ContentValidationSeverity.Error))
                    throw new InvalidOperationException($"StageLayout generation reported errors for root '{roots[i].name}'. {FormatIssues(issues)}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Failed to save scene: {SampleScenePath}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void MigrateStage(StageLayoutStageMarker stage, StageGridAuthoring authoring, RegionTileAssetCache assetCache)
        {
            var regionTilemap = EnsureRegionTilemap(stage, authoring);
            regionTilemap.ClearAllTiles();

            var sourceMappings = BuildMappings(stage, authoring, StageRegionKind.Source);
            var depositMappings = BuildMappings(stage, authoring, StageRegionKind.Deposit);

            authoring.SourceRegionMappings = sourceMappings
                .Select(x => new StageRegionSlotMapping { RegionSlotIndex = x.SlotIndex, StableId = x.StableId })
                .ToArray();
            authoring.DepositRegionMappings = depositMappings
                .Select(x => new StageRegionSlotMapping { RegionSlotIndex = x.SlotIndex, StableId = x.StableId })
                .ToArray();
            authoring.RegionTilemap = regionTilemap;
            authoring.SourceTilemap = null;
            authoring.DepositTilemap = null;
            EditorUtility.SetDirty(authoring);

            var sourceSlotByStableId = sourceMappings.ToDictionary(x => x.StableId, x => x.SlotIndex);
            var depositSlotByStableId = depositMappings.ToDictionary(x => x.StableId, x => x.SlotIndex);

            SyncAnchors(stage, authoring, StageRegionKind.Source, sourceSlotByStableId);
            SyncAnchors(stage, authoring, StageRegionKind.Deposit, depositSlotByStableId);

            int width = Mathf.Max(1, authoring.BoundsSize.x);
            int height = Mathf.Max(1, authoring.BoundsSize.y);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint sourceStableId = authoring.SourceRegionPaint != null ? authoring.SourceRegionPaint.GetCell(x, y) : 0u;
                    uint depositStableId = authoring.DepositRegionPaint != null ? authoring.DepositRegionPaint.GetCell(x, y) : 0u;

                    if (sourceStableId != 0u && depositStableId != 0u)
                    {
                        throw new InvalidOperationException(
                            $"StageId={stage.StageId} has overlapping source/deposit paint at local cell ({x}, {y}).");
                    }

                    TileBase tile = null;
                    if (sourceStableId != 0u)
                    {
                        int slot = sourceSlotByStableId[sourceStableId];
                        tile = assetCache.GetOrCreateTile(StageRegionKind.Source, slot);
                    }
                    else if (depositStableId != 0u)
                    {
                        int slot = depositSlotByStableId[depositStableId];
                        tile = assetCache.GetOrCreateTile(StageRegionKind.Deposit, slot);
                    }

                    regionTilemap.SetTile(authoring.GetTilemapCell(x, y), tile);
                }
            }

            EditorUtility.SetDirty(regionTilemap);
            EditorUtility.SetDirty(regionTilemap.gameObject);
        }

        private static Tilemap EnsureRegionTilemap(StageLayoutStageMarker stage, StageGridAuthoring authoring)
        {
            if (authoring.RegionTilemap != null)
                return authoring.RegionTilemap;

            string tilemapName = $"RegionTilemap_Stage{stage.StageId}";
            Transform parent = authoring.Grid != null ? authoring.Grid.transform : stage.transform;
            Transform child = parent.Find(tilemapName);
            GameObject tilemapGo;
            if (child == null)
            {
                tilemapGo = new GameObject(tilemapName);
                Undo.RegisterCreatedObjectUndo(tilemapGo, "Create RegionTilemap");
                tilemapGo.transform.SetParent(parent, false);
                tilemapGo.transform.localPosition = Vector3.zero;
                tilemapGo.transform.localRotation = Quaternion.identity;
                tilemapGo.transform.localScale = Vector3.one;
            }
            else
            {
                tilemapGo = child.gameObject;
            }

            var tilemap = tilemapGo.GetComponent<Tilemap>();
            if (tilemap == null)
                tilemap = Undo.AddComponent<Tilemap>(tilemapGo);

            var renderer = tilemapGo.GetComponent<TilemapRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<TilemapRenderer>(tilemapGo);

            renderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
            renderer.sortingOrder = 50;
            EditorUtility.SetDirty(tilemapGo);
            return tilemap;
        }

        private static List<RegionMappingData> BuildMappings(StageLayoutStageMarker stage, StageGridAuthoring authoring, StageRegionKind kind)
        {
            var result = new List<RegionMappingData>();
            var usedSlots = new HashSet<int>();
            var slotByStableId = new Dictionary<uint, int>();

            var existingMappings = authoring.GetMappings(kind);
            if (existingMappings != null)
            {
                for (int i = 0; i < existingMappings.Length; i++)
                {
                    var entry = existingMappings[i];
                    if (entry.RegionSlotIndex <= 0 || entry.StableId == 0u)
                        continue;

                    if (slotByStableId.ContainsKey(entry.StableId))
                        continue;

                    slotByStableId.Add(entry.StableId, entry.RegionSlotIndex);
                    usedSlots.Add(entry.RegionSlotIndex);
                }
            }

            var anchors = stage.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || anchor.RegionKind != kind || anchor.StableId == 0u)
                    continue;

                if (!slotByStableId.ContainsKey(anchor.StableId) && anchor.RegionSlotIndex > 0 && !usedSlots.Contains(anchor.RegionSlotIndex))
                {
                    slotByStableId.Add(anchor.StableId, anchor.RegionSlotIndex);
                    usedSlots.Add(anchor.RegionSlotIndex);
                }
            }

            var stableIds = CollectStableIds(authoring, kind, anchors);
            stableIds.Sort();
            for (int i = 0; i < stableIds.Count; i++)
            {
                uint stableId = stableIds[i];
                if (slotByStableId.ContainsKey(stableId))
                    continue;

                int nextSlot = 1;
                while (usedSlots.Contains(nextSlot))
                    nextSlot++;

                slotByStableId.Add(stableId, nextSlot);
                usedSlots.Add(nextSlot);
            }

            foreach (var pair in slotByStableId.OrderBy(x => x.Value))
                result.Add(new RegionMappingData(pair.Key, pair.Value));

            return result;
        }

        private static List<uint> CollectStableIds(StageGridAuthoring authoring, StageRegionKind kind, StageRegionAnchorMarker[] anchors)
        {
            var stableIds = new HashSet<uint>();
            var paint = kind == StageRegionKind.Source ? authoring.SourceRegionPaint : authoring.DepositRegionPaint;
            if (paint != null)
            {
                int width = Mathf.Min(Mathf.Max(0, paint.Width), Mathf.Max(0, authoring.BoundsSize.x));
                int height = Mathf.Min(Mathf.Max(0, paint.Height), Mathf.Max(0, authoring.BoundsSize.y));
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        uint stableId = paint.GetCell(x, y);
                        if (stableId != 0u)
                            stableIds.Add(stableId);
                    }
                }
            }

            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor != null && anchor.RegionKind == kind && anchor.StableId != 0u)
                    stableIds.Add(anchor.StableId);
            }

            return stableIds.ToList();
        }

        private static void SyncAnchors(
            StageLayoutStageMarker stage,
            StageGridAuthoring authoring,
            StageRegionKind kind,
            IReadOnlyDictionary<uint, int> slotByStableId)
        {
            var anchors = stage.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || anchor.RegionKind != kind)
                    continue;

                uint stableId = anchor.StableId;
                if (stableId == 0u)
                {
                    var paint = kind == StageRegionKind.Source ? authoring.SourceRegionPaint : authoring.DepositRegionPaint;
                    stableId = paint != null ? paint.GetCell(anchor.AnchorCell.x, anchor.AnchorCell.y) : 0u;
                }

                if (stableId == 0u || !slotByStableId.TryGetValue(stableId, out int slot))
                    throw new InvalidOperationException($"StageId={stage.StageId} anchor '{anchor.name}' cannot resolve slot for {kind} stableId={stableId}.");

                anchor.RegionSlotIndex = slot;
                anchor.StableId = stableId;
                EditorUtility.SetDirty(anchor);
            }
        }

        private static Sprite LoadReferenceSprite()
        {
            var referenceTile = AssetDatabase.LoadAssetAtPath<StageMovementTile>(ReferenceMovementTilePath);
            if (referenceTile == null || referenceTile.sprite == null)
                throw new InvalidOperationException($"Failed to load reference sprite from '{ReferenceMovementTilePath}'.");

            return referenceTile.sprite;
        }

        private static string FormatIssues(IReadOnlyList<ContentValidationIssue> issues)
        {
            if (issues == null || issues.Count <= 0)
                return string.Empty;

            return string.Join(" | ", issues.Select(x => $"{x.Severity}:{x.Code}:{x.Location}:{x.Message}"));
        }

        private readonly struct RegionMappingData
        {
            public RegionMappingData(uint stableId, int slotIndex)
            {
                StableId = stableId;
                SlotIndex = slotIndex;
            }

            public uint StableId { get; }
            public int SlotIndex { get; }
        }

        private sealed class RegionTileAssetCache
        {
            private readonly Sprite _referenceSprite;
            private readonly Dictionary<(StageRegionKind Kind, int SlotIndex), StageRegionTile> _cache = new();

            public RegionTileAssetCache(Sprite referenceSprite)
            {
                _referenceSprite = referenceSprite;
            }

            public StageRegionTile GetOrCreateTile(StageRegionKind kind, int slotIndex)
            {
                if (_cache.TryGetValue((kind, slotIndex), out var cached) && cached != null)
                    return cached;

                string kindName = kind.ToString().ToLowerInvariant();
                string assetPath = $"{RegionTileAssetFolder}/srt_{kindName}_{slotIndex:00}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<StageRegionTile>(assetPath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<StageRegionTile>();
                    tile.name = Path.GetFileNameWithoutExtension(assetPath);
                    ConfigureTile(tile, kind, slotIndex, _referenceSprite);
                    AssetDatabase.CreateAsset(tile, assetPath);
                }
                else
                {
                    ConfigureTile(tile, kind, slotIndex, _referenceSprite);
                    EditorUtility.SetDirty(tile);
                    AssetDatabase.SaveAssetIfDirty(tile);
                }

                EditorUtility.SetDirty(tile);
                _cache[(kind, slotIndex)] = tile;
                return tile;
            }

            private static void ConfigureTile(StageRegionTile tile, StageRegionKind kind, int slotIndex, Sprite referenceSprite)
            {
                tile.RegionKind = kind;
                tile.RegionSlotIndex = slotIndex;
                tile.sprite = referenceSprite;
                tile.color = ResolveColor(kind, slotIndex);
                tile.flags = TileFlags.LockColor;
                tile.colliderType = Tile.ColliderType.None;
            }

            private static Color ResolveColor(StageRegionKind kind, int slotIndex)
            {
                float hue = kind == StageRegionKind.Source ? 0.53f : 0.11f;
                hue = Mathf.Repeat(hue + ((slotIndex - 1) * 0.07f), 1f);
                return Color.HSVToRGB(hue, 0.65f, 1f).WithAlpha(0.45f);
            }
        }

        private static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
