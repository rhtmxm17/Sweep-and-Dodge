using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageLayoutCatalogGeneratorTests
    {
        [Test]
        public void GenerateLayoutsForRoot_BuildsDenseGridAndClearsLegacyArrays()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.SourcePaint.SetCell(0, 0, 1001u);
                setup.DepositPaint.SetCell(1, 1, 2001u);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1001u, new Vector2Int(0, 0));
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Deposit, 2001u, new Vector2Int(1, 1));

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(setup.Layout.SchemaVersion, Is.EqualTo(2));
                Assert.That(setup.Layout.Grid.Width, Is.EqualTo(2));
                Assert.That(setup.Layout.Grid.Height, Is.EqualTo(2));
                Assert.That(setup.Layout.Cells, Has.Length.EqualTo(4));
                Assert.That(setup.Layout.Cells[0].SourceRegionId, Is.EqualTo(1001u));
                Assert.That(setup.Layout.Cells[3].DepositRegionId, Is.EqualTo(2001u));
                Assert.That(setup.Layout.Cells[3].MovementFlags, Is.EqualTo(StageCellMovementFlags.None));
                Assert.That(setup.Layout.SourceRegions.Single().StableId, Is.EqualTo(1001u));
                Assert.That(setup.Layout.DepositRegions.Single().StableId, Is.EqualTo(2001u));
                Assert.That(setup.Layout.Sources, Is.Empty);
                Assert.That(setup.Layout.Deposits, Is.Empty);
                Assert.That(setup.Layout.Obstacles, Is.Empty);
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
            try
            {
                setup.SourcePaint.SetCell(0, 0, 1001u);
                var anchor = CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1001u, new Vector2Int(0, 0));
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
                setup.Dispose();
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_MissingAnchor_FailsWithError()
        {
            var setup = CreateStageSetup();
            try
            {
                setup.SourcePaint.SetCell(0, 0, 1001u);

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(setup.Root, out var issues, saveAssets: false);

                Assert.That(ok, Is.False);
                Assert.That(issues.Any(x => x.Code == "STA016"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        private static StageRegionAnchorMarker CreateAnchor(Transform parent, StageRegionKind kind, uint stableId, Vector2Int anchorCell)
        {
            var go = new GameObject($"{kind}_{stableId}");
            go.transform.SetParent(parent);
            var marker = go.AddComponent<StageRegionAnchorMarker>();
            marker.RegionKind = kind;
            marker.StableId = stableId;
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

        private static StageTestSetup CreateStageSetup()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var gridGo = new GameObject("grid");
            var tilemapGo = new GameObject("movement_tilemap");
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var sourcePaint = ScriptableObject.CreateInstance<StageRegionPaintAsset>();
            var depositPaint = ScriptableObject.CreateInstance<StageRegionPaintAsset>();

            stageGo.transform.SetParent(rootGo.transform);
            gridGo.transform.SetParent(stageGo.transform);
            tilemapGo.transform.SetParent(gridGo.transform);

            var root = rootGo.AddComponent<StageLayoutRootMarker>();
            var stage = stageGo.AddComponent<StageLayoutStageMarker>();
            stage.StageId = 1;
            stage.TargetLayout = layout;

            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var tilemap = tilemapGo.AddComponent<Tilemap>();
            tilemapGo.AddComponent<TilemapRenderer>();
            tilemap.origin = Vector3Int.zero;
            tilemap.size = new Vector3Int(2, 2, 1);

            sourcePaint.RegionKind = StageRegionKind.Source;
            sourcePaint.Resize(2, 2);
            depositPaint.RegionKind = StageRegionKind.Deposit;
            depositPaint.Resize(2, 2);

            var authoring = stageGo.AddComponent<StageGridAuthoring>();
            authoring.Grid = grid;
            authoring.MovementTilemap = tilemap;
            authoring.SourceRegionPaint = sourcePaint;
            authoring.DepositRegionPaint = depositPaint;

            return new StageTestSetup(rootGo, stageGo, root, layout, sourcePaint, depositPaint, tilemap);
        }

        private sealed class StageTestSetup
        {
            private readonly Object[] _ownedObjects;

            public StageTestSetup(GameObject rootGo, GameObject stageGo, StageLayoutRootMarker root, StageLayoutSO layout, StageRegionPaintAsset sourcePaint, StageRegionPaintAsset depositPaint, Tilemap movementTilemap)
            {
                _ownedObjects = new Object[] { layout, sourcePaint, depositPaint, rootGo };
                StageGo = stageGo;
                Root = root;
                Layout = layout;
                SourcePaint = sourcePaint;
                DepositPaint = depositPaint;
                MovementTilemap = movementTilemap;
            }

            public GameObject StageGo { get; }
            public StageLayoutRootMarker Root { get; }
            public StageLayoutSO Layout { get; }
            public StageRegionPaintAsset SourcePaint { get; }
            public StageRegionPaintAsset DepositPaint { get; }
            public Tilemap MovementTilemap { get; }

            public void Dispose()
            {
                for (int i = 0; i < _ownedObjects.Length; i++)
                    Object.DestroyImmediate(_ownedObjects[i]);
            }
        }
    }
}
