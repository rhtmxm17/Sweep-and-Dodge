using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageGridSceneVisualizationRendererTests
    {
        [TearDown]
        public void TearDown()
        {
            StageGridSceneVisualizationRenderer.ClearCaches();
        }

        [Test]
        public void GetOrBuildCacheStats_UnchangedAuthoring_DoesNotRescanTiles()
        {
            var root = new GameObject("stage");
            StageMovementTile movementTile = null;
            StageRegionTile regionTile = null;
            try
            {
                var grid = root.AddComponent<Grid>();
                var movement = CreateTilemap(root.transform, "movement");
                var region = CreateTilemap(root.transform, "region");
                var authoring = root.AddComponent<StageGridAuthoring>();
                authoring.Grid = grid;
                authoring.MovementTilemap = movement;
                authoring.RegionTilemap = region;
                authoring.BoundsSize = new Vector2Int(2, 2);
                authoring.SourceRegionMappings = new[]
                {
                    new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 1001u },
                };

                movementTile = ScriptableObject.CreateInstance<StageMovementTile>();
                movementTile.MovementFlags = StageCellMovementFlags.BlockPlayer;
                movement.SetTile(Vector3Int.zero, movementTile);
                regionTile = ScriptableObject.CreateInstance<StageRegionTile>();
                regionTile.RegionKind = StageRegionKind.Source;
                regionTile.RegionSlotIndex = 1;
                region.SetTile(Vector3Int.zero, regionTile);

                var first = StageGridSceneVisualizationRenderer.GetOrBuildCacheStats(authoring);
                var second = StageGridSceneVisualizationRenderer.GetOrBuildCacheStats(authoring);

                Assert.That(first.CellCount, Is.EqualTo(4));
                Assert.That(first.TileLookupCount, Is.EqualTo(8));
                Assert.That(first.VertexCount, Is.GreaterThan(0));
                Assert.That(first.RebuildCount, Is.EqualTo(1));
                Assert.That(second.RebuildCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(movementTile);
                Object.DestroyImmediate(regionTile);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GetOrBuildCacheStats_AuthoringSignatureChange_RebuildsCache()
        {
            var root = new GameObject("stage");
            try
            {
                var grid = root.AddComponent<Grid>();
                var authoring = root.AddComponent<StageGridAuthoring>();
                authoring.Grid = grid;
                authoring.MovementTilemap = CreateTilemap(root.transform, "movement");
                authoring.RegionTilemap = CreateTilemap(root.transform, "region");
                authoring.BoundsSize = new Vector2Int(2, 2);

                var first = StageGridSceneVisualizationRenderer.GetOrBuildCacheStats(authoring);
                authoring.ShowGridGizmo = false;
                var second = StageGridSceneVisualizationRenderer.GetOrBuildCacheStats(authoring);
                root.transform.position = new Vector3(50f, 0f, 0f);
                var afterWorkspaceMove = StageGridSceneVisualizationRenderer.GetOrBuildCacheStats(authoring);

                Assert.That(first.RebuildCount, Is.EqualTo(1));
                Assert.That(second.RebuildCount, Is.EqualTo(2));
                Assert.That(second.VertexCount, Is.LessThan(first.VertexCount));
                Assert.That(afterWorkspaceMove.RebuildCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GetOrBuildCacheStats_TilemapChange_RebuildsCache()
        {
            var root = new GameObject("stage");
            StageMovementTile movementTile = null;
            try
            {
                var grid = root.AddComponent<Grid>();
                var movement = CreateTilemap(root.transform, "movement");
                var authoring = root.AddComponent<StageGridAuthoring>();
                authoring.Grid = grid;
                authoring.MovementTilemap = movement;
                authoring.RegionTilemap = CreateTilemap(root.transform, "region");
                authoring.BoundsSize = new Vector2Int(2, 2);

                var first = StageGridSceneVisualizationRenderer.GetOrBuildCacheStats(authoring);
                movementTile = ScriptableObject.CreateInstance<StageMovementTile>();
                movementTile.MovementFlags = StageCellMovementFlags.BlockBullet;
                movement.SetTile(Vector3Int.zero, movementTile);
                var afterTileChange = StageGridSceneVisualizationRenderer.GetOrBuildCacheStats(authoring);

                Assert.That(first.RebuildCount, Is.EqualTo(1));
                Assert.That(afterTileChange.RebuildCount, Is.EqualTo(2));
                Assert.That(afterTileChange.VertexCount, Is.GreaterThan(first.VertexCount));
            }
            finally
            {
                Object.DestroyImmediate(movementTile);
                Object.DestroyImmediate(root);
            }
        }

        private static Tilemap CreateTilemap(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<Tilemap>();
        }
    }
}
