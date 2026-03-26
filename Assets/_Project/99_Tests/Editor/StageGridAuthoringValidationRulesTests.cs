using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageGridAuthoringValidationRulesTests
    {
        [Test]
        public void NegativeBoundsMin_IsAllowed()
        {
            var setup = CreateSetup();
            StageRegionTile sourceTile = null;
            try
            {
                setup.Authoring.BoundsMinCell = new Vector2Int(-4, -3);
                sourceTile = CreateRegionTile(StageRegionKind.Source, 1);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(0, 0), sourceTile);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(0, 0));

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA010"), Is.False);
                Assert.That(issues.Any(x => x.Code == "STA021"), Is.False);
            }
            finally
            {
                DestroyTile(sourceTile);
                setup.Dispose();
            }
        }

        [Test]
        public void MissingRegionTilemap_IsReported()
        {
            var setup = CreateSetup();
            try
            {
                setup.Authoring.RegionTilemap = null;

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA005"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void NonStageRegionTile_InRegionTilemap_IsReported()
        {
            var setup = CreateSetup();
            StageMovementTile wrongTile = null;
            try
            {
                wrongTile = CreateMovementTile(StageCellMovementFlags.BlockPlayer);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(0, 0), wrongTile);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(0, 0));

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA015"), Is.True);
            }
            finally
            {
                DestroyTile(wrongTile);
                setup.Dispose();
            }
        }

        [Test]
        public void UsedSlotWithoutMapping_IsReported()
        {
            var setup = CreateSetup();
            StageRegionTile sourceTile = null;
            try
            {
                sourceTile = CreateRegionTile(StageRegionKind.Source, 7);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(0, 0), sourceTile);

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA024"), Is.True);
            }
            finally
            {
                DestroyTile(sourceTile);
                setup.Dispose();
            }
        }

        [Test]
        public void DuplicateSlotAndStableIdMappings_AreReported()
        {
            var setup = CreateSetup();
            try
            {
                setup.Authoring.SourceRegionMappings = new[]
                {
                    new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 1001u },
                    new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 1002u },
                    new StageRegionSlotMapping { RegionSlotIndex = 2, StableId = 1001u },
                };

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA026"), Is.True);
                Assert.That(issues.Any(x => x.Code == "STA027"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void AnchorSlotWithoutMapping_IsReported()
        {
            var setup = CreateSetup();
            try
            {
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 7, new Vector2Int(0, 0));

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA024"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void AnchorCellNotPaintedWithSameSlot_IsReported()
        {
            var setup = CreateSetup();
            StageRegionTile sourceTile = null;
            try
            {
                sourceTile = CreateRegionTile(StageRegionKind.Source, 1);
                setup.RegionTilemap.SetTile(setup.Authoring.GetTilemapCell(0, 0), sourceTile);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1, new Vector2Int(1, 1));

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA018"), Is.True);
            }
            finally
            {
                DestroyTile(sourceTile);
                setup.Dispose();
            }
        }

        [Test]
        public void UsedTileOutsideAuthoringBounds_IsReportedAsWarning()
        {
            var setup = CreateSetup();
            StageMovementTile tile = null;
            try
            {
                tile = CreateMovementTile(StageCellMovementFlags.BlockPlayer);
                setup.MovementTilemap.SetTile(new Vector3Int(4, 1, 0), tile);

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA022" && x.Severity == ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                DestroyTile(tile);
                setup.Dispose();
            }
        }

        [Test]
        public void NonCanonicalGridRotation_IsReported()
        {
            var setup = CreateSetup();
            try
            {
                setup.Grid.transform.rotation = Quaternion.Euler(90f, 15f, 0f);

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA021"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        private static System.Collections.Generic.List<ContentValidationIssue> Validate(StageLayoutStageMarker stage)
        {
            var issues = new System.Collections.Generic.List<ContentValidationIssue>();
            StageGridAuthoringValidationRules.Validate(stage, issues);
            return issues;
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
            return marker;
        }

        private static ValidationSetup CreateSetup()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var gridGo = new GameObject("grid");
            var movementGo = new GameObject("movement_tilemap");
            var regionGo = new GameObject("region_tilemap");
            stageGo.transform.SetParent(rootGo.transform);
            gridGo.transform.SetParent(stageGo.transform);
            movementGo.transform.SetParent(gridGo.transform);
            regionGo.transform.SetParent(gridGo.transform);

            rootGo.AddComponent<StageLayoutRootMarker>();
            var stage = stageGo.AddComponent<StageLayoutStageMarker>();
            stage.TargetLayout = ScriptableObject.CreateInstance<StageLayoutSO>();

            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var movementTilemap = AddTilemap(movementGo);
            var regionTilemap = AddTilemap(regionGo);

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

            return new ValidationSetup(rootGo, stageGo, stage, movementTilemap, regionTilemap, authoring);
        }

        private static Tilemap AddTilemap(GameObject go)
        {
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            tilemap.origin = Vector3Int.zero;
            tilemap.size = new Vector3Int(2, 2, 1);
            return tilemap;
        }

        private sealed class ValidationSetup
        {
            private readonly Object[] _ownedObjects;

            public ValidationSetup(GameObject rootGo, GameObject stageGo, StageLayoutStageMarker stage, Tilemap movementTilemap, Tilemap regionTilemap, StageGridAuthoring authoring)
            {
                _ownedObjects = new Object[]
                {
                    stage.TargetLayout,
                    rootGo,
                };
                StageGo = stageGo;
                Stage = stage;
                MovementTilemap = movementTilemap;
                RegionTilemap = regionTilemap;
                Authoring = authoring;
            }

            public GameObject StageGo { get; }
            public StageLayoutStageMarker Stage { get; }
            public Tilemap MovementTilemap { get; }
            public Tilemap RegionTilemap { get; }
            public Grid Grid => (Grid)MovementTilemap.layoutGrid;
            public StageGridAuthoring Authoring { get; }

            public void Dispose()
            {
                for (int i = 0; i < _ownedObjects.Length; i++)
                    Object.DestroyImmediate(_ownedObjects[i]);
            }
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

        private static void DestroyTile(Object tile)
        {
            if (tile != null)
                Object.DestroyImmediate(tile);
        }
    }
}
