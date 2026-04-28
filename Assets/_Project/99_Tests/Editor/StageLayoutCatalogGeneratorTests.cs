using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageLayoutCatalogGeneratorTests
    {
        private const string GeneratedVisualPrefabPath = "Assets/_Project/04_Prefabs/StageVisual/GridVisual_Stage901.prefab";
        private const string GeneratedNoSaveVisualPrefabPath = "Assets/_Project/04_Prefabs/StageVisual/GridVisual_Stage902.prefab";

        [Test]
        public void GenerateLayoutsForRoot_BuildsDenseGrid()
        {
            var setup = CreateStageSetup();
            StageRegionTile sourceTile = null;
            StageRegionTile depositTile = null;
            try
            {
                sourceTile = CreateRegionTile(StageRegionKind.Source, 1);
                depositTile = CreateRegionTile(StageRegionKind.Deposit, 1);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(0, 0), sourceTile);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(1, 1), depositTile);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(0, 0));
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Deposit, 1, new Vector2Int(1, 1));

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.SchemaVersion, Is.EqualTo(2));
                Assert.That(setup.Layout.Grid.Width, Is.EqualTo(2));
                Assert.That(setup.Layout.Grid.Height, Is.EqualTo(2));
                Assert.That(setup.Layout.Cells, Has.Length.EqualTo(4));
                Assert.That(setup.Layout.Cells[0].SourceRegionId, Is.EqualTo(1001u));
                Assert.That(setup.Layout.Cells[3].DepositRegionId, Is.EqualTo(2001u));
                Assert.That(setup.Layout.SourceRegions.Single().StableId, Is.EqualTo(1001u));
                Assert.That(setup.Layout.DepositRegions.Single().StableId, Is.EqualTo(2001u));
            }
            finally
            {
                DestroyTile(sourceTile);
                DestroyTile(depositTile);
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_SaveAssetsTrue_CreatesGridVisualPrefab()
        {
            AssetDatabase.DeleteAsset(GeneratedVisualPrefabPath);
            var setup = CreateStageSetup();
            try
            {
                setup.Stage.StageId = 901;
                setup.Authoring.Grid.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var ground = AddTilemapChild(setup.Authoring.Grid.transform, "ground_visual");
                var wall = AddTilemapChild(setup.Authoring.Grid.transform, "wall_visual");
                setup.Authoring.GroundVisualTilemap = ground;
                setup.Authoring.WallVisualTilemap = wall;

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: true);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.GridVisualPrefab, Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedVisualPrefabPath), Is.EqualTo(setup.Layout.GridVisualPrefab));
                Assert.That(setup.Layout.GridVisualPrefab.GetComponent<Grid>(), Is.Not.Null);
                Assert.That(setup.Layout.GridVisualPrefab.transform.rotation.eulerAngles.x, Is.EqualTo(90f).Within(0.5f));
                Assert.That(setup.Layout.GridVisualPrefab.transform.Find("ground_visual"), Is.Not.Null);
                Assert.That(setup.Layout.GridVisualPrefab.transform.Find("wall_visual"), Is.Not.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(GeneratedVisualPrefabPath);
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_SaveAssetsFalse_DoesNotCreateGridVisualPrefab()
        {
            AssetDatabase.DeleteAsset(GeneratedNoSaveVisualPrefabPath);
            var setup = CreateStageSetup();
            try
            {
                setup.Stage.StageId = 902;
                setup.Authoring.GroundVisualTilemap = AddTilemapChild(setup.Authoring.Grid.transform, "ground_visual");

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.GridVisualPrefab, Is.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedNoSaveVisualPrefabPath), Is.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(GeneratedNoSaveVisualPrefabPath);
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_WithoutRegionTilemap_FailsWithError()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.Authoring.RegionTilemap = null;

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.False);
                Assert.That(issues.Any(x => x.Code == "STA005"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_WithoutStageGridAuthoring_FailsWithError()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            try
            {
                stageGo.transform.SetParent(rootGo.transform);
                var root = rootGo.AddComponent<StageLayoutRootMarker>();
                var stage = stageGo.AddComponent<StageLayoutStageMarker>();
                stage.StageId = 1;
                stage.TargetLayout = layout;

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(root, out var issues, saveAssets: false);

                Assert.That(ok, Is.False);
                Assert.That(issues.Any(x => x.Code == "STA001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_LinkedPresentation_ResolvesAnchorParentLink()
        {
            var setup = CreateStageSetup();
            StageRegionTile sourceTile = null;
            try
            {
                sourceTile = CreateRegionTile(StageRegionKind.Source, 1);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(0, 0), sourceTile);
                var anchor = CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(0, 0));
                var presentationGo = new GameObject("presentation");
                presentationGo.transform.SetParent(anchor.transform);
                var presentation = presentationGo.AddComponent<StagePresentationMarker>();
                presentation.StableId = 4001u;
                presentation.PlacementMode = StagePresentationPlacementMode.LinkedToParent;
                presentation.PresentationKey = "source_basic";
                presentationGo.transform.localPosition = new Vector3(1f, 2f, 3f);

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.Presentations, Has.Length.EqualTo(1));
                Assert.That(setup.Layout.Presentations[0].LinkKind, Is.EqualTo(StagePresentationLinkKind.Source));
                Assert.That(setup.Layout.Presentations[0].LinkedStableId, Is.EqualTo(1001u));
            }
            finally
            {
                DestroyTile(sourceTile);
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_RotatedGrid_IgnoresWorkspaceTransformForRuntimeOrigin()
        {
            var setup = CreateStageSetup();
            StageRegionTile sourceTile = null;
            try
            {
                setup.StageGo.transform.GetChild(0).rotation = Quaternion.Euler(90f, 0f, 0f);
                setup.StageGo.transform.GetChild(0).position = new Vector3(3f, 2f, 5f);
                setup.Authoring.BoundsMinCell = new Vector2Int(-2, 4);
                sourceTile = CreateRegionTile(StageRegionKind.Source, 1);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(1, 1), sourceTile);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(-1, 5));
                setup.PlayerStart.AnchorCell = new Vector2Int(-2, 5);

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.Grid.Origin.x, Is.EqualTo(-2f));
                Assert.That(setup.Layout.Grid.Origin.z, Is.EqualTo(4f));
                Assert.That(setup.Layout.SourceRegions.Single().AnchorCell, Is.EqualTo(new Vector2Int(1, 1)));
            }
            finally
            {
                DestroyTile(sourceTile);
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_NegativeBoundsMin_ReadsMovementFromTilemapCoordinates()
        {
            var setup = CreateStageSetup();
            StageMovementTile movementTile = null;
            StageRegionTile sourceTile = null;
            try
            {
                setup.Authoring.BoundsMinCell = new Vector2Int(-3, -2);
                sourceTile = CreateRegionTile(StageRegionKind.Source, 1);
                setup.RegionTilemap.SetTile(new Vector3Int(-3, -2, 0), sourceTile);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(-3, -2));
                movementTile = CreateMovementTile(StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet);
                setup.MovementTilemap.SetTile(new Vector3Int(-3, -2, 0), movementTile);
                setup.PlayerStart.AnchorCell = new Vector2Int(-3, -1);

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.Grid.Width, Is.EqualTo(2));
                Assert.That(setup.Layout.Grid.Height, Is.EqualTo(2));
                Assert.That(setup.Layout.Grid.Origin.x, Is.EqualTo(-3f));
                Assert.That(setup.Layout.Grid.Origin.z, Is.EqualTo(-2f));
                Assert.That(setup.Layout.Cells[0].MovementFlags, Is.EqualTo(StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet));
                Assert.That(setup.Layout.Cells[0].SourceRegionId, Is.EqualTo(1001u));
            }
            finally
            {
                DestroyTile(movementTile);
                DestroyTile(sourceTile);
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_PlayerStartMarker_ExportsAnchorOffsetAndYaw()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.PlayerStart.AnchorCell = new Vector2Int(1, 0);
                setup.PlayerStart.AnchorOffset = new Vector2(0.25f, -0.1f);
                setup.PlayerStart.YawDeg = 135f;

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.PlayerStart.Active, Is.True);
                Assert.That(setup.Layout.PlayerStart.AnchorCell, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(setup.Layout.PlayerStart.AnchorOffset, Is.EqualTo(new Vector2(0.25f, -0.1f)));
                Assert.That(setup.Layout.PlayerStart.YawDeg, Is.EqualTo(135f).Within(0.001f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_TileCellMarkers_AreNormalizedByBoundsMin()
        {
            var setup = CreateStageSetup();
            StageRegionTile sourceTile = null;
            try
            {
                setup.Authoring.BoundsMinCell = new Vector2Int(-2, -2);
                setup.Authoring.BoundsSize = new Vector2Int(4, 4);
                sourceTile = CreateRegionTile(StageRegionKind.Source, 1);
                setup.RegionTilemap.SetTile(new Vector3Int(0, 1, 0), sourceTile);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(0, 1));
                setup.PlayerStart.AnchorCell = new Vector2Int(-1, 0);

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.SourceRegions.Single().AnchorCell, Is.EqualTo(new Vector2Int(2, 3)));
                Assert.That(setup.Layout.PlayerStart.AnchorCell, Is.EqualTo(new Vector2Int(1, 2)));
            }
            finally
            {
                DestroyTile(sourceTile);
                setup.Dispose();
            }
        }

        [Test]
        public void BuildRuntimeGridSpec_IgnoresWorkspaceTransform()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.StageGo.transform.GetChild(0).position = new Vector3(10f, 2f, -4f);
                setup.Authoring.BoundsMinCell = new Vector2Int(-2, 5);
                setup.Authoring.BoundsSize = new Vector2Int(4, 3);

                var grid = setup.Authoring.BuildRuntimeGridSpec();
                Assert.That(grid.Origin.x, Is.EqualTo(-2f));
                Assert.That(grid.Origin.z, Is.EqualTo(5f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void BuildEditorPreviewGridSpec_AnchorPreviewMath_UsesWorkspaceTransformAndTileCell()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.StageGo.transform.GetChild(0).position = new Vector3(10f, 2f, -4f);
                setup.Authoring.BoundsMinCell = new Vector2Int(-2, 5);
                setup.Authoring.BoundsSize = new Vector2Int(4, 3);

                var grid = setup.Authoring.BuildEditorPreviewGridSpec();
                Vector3 world = StageRuntimeGridUtility.GetAnchorWorldPosition(
                    in grid,
                    new int2(-1, 7),
                    new float2(0f, 0f),
                    setup.Authoring.GetEditorPreviewPlaneY());

                Assert.That(world.x, Is.EqualTo(9.5f));
                Assert.That(world.z, Is.EqualTo(3.5f));
                Assert.That(world.y, Is.EqualTo(2f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void BuildEditorPreviewGridSpec_BoundsShift_DoesNotMoveSameTileCellPreview()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.StageGo.transform.GetChild(0).position = new Vector3(10f, 2f, -4f);
                var baselineGrid = setup.Authoring.BuildEditorPreviewGridSpec();
                Vector3 baseline = StageRuntimeGridUtility.GetAnchorWorldPosition(
                    in baselineGrid,
                    new int2(1, 2),
                    float2.zero,
                    setup.Authoring.GetEditorPreviewPlaneY());

                setup.Authoring.BoundsMinCell = new Vector2Int(-5, 7);
                var shiftedGrid = setup.Authoring.BuildEditorPreviewGridSpec();
                Vector3 shifted = StageRuntimeGridUtility.GetAnchorWorldPosition(
                    in shiftedGrid,
                    new int2(1, 2),
                    float2.zero,
                    setup.Authoring.GetEditorPreviewPlaneY());

                Assert.That(shifted, Is.EqualTo(baseline));
            }
            finally
            {
                setup.Dispose();
            }
        }

        private static StageRegionAnchorMarker CreateAnchor(Transform parent, StageRegionKind kind, int regionSlotIndex, Vector2Int anchorCell)
        {
            var go = new GameObject($"{kind}_slot_{regionSlotIndex}");
            go.transform.SetParent(parent);
            var marker = go.AddComponent<StageRegionAnchorMarker>();
            marker.RegionKind = kind;
            marker.RegionSlotIndex = regionSlotIndex;
            marker.Active = true;
            marker.AnchorCell = anchorCell;
            marker.AnchorOffset = Vector2.zero;
            return marker;
        }

        private static StageMovementTile CreateMovementTile(StageCellMovementFlags flags)
        {
            var tile = ScriptableObject.CreateInstance<StageMovementTile>();
            tile.MovementFlags = flags;
            return tile;
        }

        private static StagePlayerStartMarker CreatePlayerStart(Transform parent, Vector2Int anchorCell, Vector2 anchorOffset, float yawDeg)
        {
            var go = new GameObject("player_start");
            go.transform.SetParent(parent);
            var marker = go.AddComponent<StagePlayerStartMarker>();
            marker.Active = true;
            marker.AnchorCell = anchorCell;
            marker.AnchorOffset = anchorOffset;
            marker.YawDeg = yawDeg;
            return marker;
        }

        private static StageRegionTile CreateRegionTile(StageRegionKind kind, int slot)
        {
            var tile = ScriptableObject.CreateInstance<StageRegionTile>();
            tile.RegionKind = kind;
            tile.RegionSlotIndex = slot;
            return tile;
        }

        private static StageTestSetup CreateStageSetup()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var gridGo = new GameObject("grid");
            var movementTilemapGo = new GameObject("movement_tilemap");
            var regionTilemapGo = new GameObject("region_tilemap");
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            stageGo.transform.SetParent(rootGo.transform);
            gridGo.transform.SetParent(stageGo.transform);
            movementTilemapGo.transform.SetParent(gridGo.transform);
            regionTilemapGo.transform.SetParent(gridGo.transform);

            var root = rootGo.AddComponent<StageLayoutRootMarker>();
            var stage = stageGo.AddComponent<StageLayoutStageMarker>();
            stage.StageId = 1;
            stage.TargetLayout = layout;

            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var movementTilemap = AddTilemap(movementTilemapGo);
            var regionTilemap = AddTilemap(regionTilemapGo);

            var authoring = stageGo.AddComponent<StageGridAuthoring>();
            authoring.Grid = grid;
            authoring.MovementTilemap = movementTilemap;
            authoring.RegionTilemap = regionTilemap;
            authoring.SourceRegionMappings = new[]
            {
                new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 1001u },
                new StageRegionSlotMapping { RegionSlotIndex = 2, StableId = 1002u },
            };
            authoring.DepositRegionMappings = new[]
            {
                new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 2001u },
            };
            authoring.BoundsMinCell = new Vector2Int(0, 0);
            authoring.BoundsSize = new Vector2Int(2, 2);

            var playerStart = CreatePlayerStart(stageGo.transform, new Vector2Int(0, 1), Vector2.zero, 0f);
            return new StageTestSetup(rootGo, stageGo, root, layout, movementTilemap, regionTilemap, authoring, playerStart);
        }

        private static Tilemap AddTilemapChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return AddTilemap(go);
        }

        private static Tilemap AddTilemap(GameObject go)
        {
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            tilemap.origin = Vector3Int.zero;
            tilemap.size = new Vector3Int(2, 2, 1);
            return tilemap;
        }

        private sealed class StageTestSetup
        {
            private readonly Object[] _ownedObjects;

            public StageTestSetup(GameObject rootGo, GameObject stageGo, StageLayoutRootMarker root, StageLayoutSO layout, Tilemap movementTilemap, Tilemap regionTilemap, StageGridAuthoring authoring, StagePlayerStartMarker playerStart)
            {
                _ownedObjects = new Object[] { layout, rootGo };
                StageGo = stageGo;
                Root = root;
                Layout = layout;
                MovementTilemap = movementTilemap;
                RegionTilemap = regionTilemap;
                Authoring = authoring;
                PlayerStart = playerStart;
            }

            public GameObject StageGo { get; }
            public StageLayoutRootMarker Root { get; }
            public StageLayoutStageMarker Stage => StageGo.GetComponent<StageLayoutStageMarker>();
            public StageLayoutSO Layout { get; }
            public Tilemap MovementTilemap { get; }
            public Tilemap RegionTilemap { get; }
            public StageGridAuthoring Authoring { get; }
            public StagePlayerStartMarker PlayerStart { get; }

            public void Dispose()
            {
                for (int i = 0; i < _ownedObjects.Length; i++)
                    Object.DestroyImmediate(_ownedObjects[i]);
            }
        }

        private static void DestroyTile(Object tile)
        {
            if (tile != null)
                Object.DestroyImmediate(tile);
        }
    }
}
