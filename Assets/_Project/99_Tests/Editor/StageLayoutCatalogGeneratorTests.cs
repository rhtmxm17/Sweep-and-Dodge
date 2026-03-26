using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageLayoutCatalogGeneratorTests
    {
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
        public void GenerateLayoutsForRoot_RotatedGrid_NormalizesToWorldXZOrigin()
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
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(1, 1));

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.Grid.Origin.x, Is.EqualTo(1f));
                Assert.That(setup.Layout.Grid.Origin.z, Is.EqualTo(9f));
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
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(0, 0));
                movementTile = CreateMovementTile(StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet);
                setup.MovementTilemap.SetTile(new Vector3Int(-3, -2, 0), movementTile);

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
        public void BuildRuntimeGridSpec_AnchorPreviewMath_RespectsBoundsMin()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.StageGo.transform.GetChild(0).position = new Vector3(10f, 2f, -4f);
                setup.Authoring.BoundsMinCell = new Vector2Int(-2, 5);
                setup.Authoring.BoundsSize = new Vector2Int(4, 3);

                var grid = setup.Authoring.BuildRuntimeGridSpec();
                Vector3 world = StageRuntimeGridUtility.GetAnchorWorldPosition(
                    in grid,
                    new int2(1, 2),
                    new float2(0f, 0f),
                    setup.Authoring.Grid.transform.position.y);

                Assert.That(world.x, Is.EqualTo(9.5f));
                Assert.That(world.z, Is.EqualTo(3.5f));
                Assert.That(world.y, Is.EqualTo(2f));
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

            return new StageTestSetup(rootGo, stageGo, root, layout, movementTilemap, regionTilemap, authoring);
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

            public StageTestSetup(GameObject rootGo, GameObject stageGo, StageLayoutRootMarker root, StageLayoutSO layout, Tilemap movementTilemap, Tilemap regionTilemap, StageGridAuthoring authoring)
            {
                _ownedObjects = new Object[] { layout, rootGo };
                StageGo = stageGo;
                Root = root;
                Layout = layout;
                MovementTilemap = movementTilemap;
                RegionTilemap = regionTilemap;
                Authoring = authoring;
            }

            public GameObject StageGo { get; }
            public StageLayoutRootMarker Root { get; }
            public StageLayoutSO Layout { get; }
            public Tilemap MovementTilemap { get; }
            public Tilemap RegionTilemap { get; }
            public StageGridAuthoring Authoring { get; }

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
