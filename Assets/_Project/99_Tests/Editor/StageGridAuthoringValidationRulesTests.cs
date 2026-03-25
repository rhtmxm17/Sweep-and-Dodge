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
        public void MovementTilemapBoundsMismatch_IsReported()
        {
            var setup = CreateSetup();
            try
            {
                setup.Tilemap.size = new Vector3Int(3, 2, 1);

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA011"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void WrongRegionKind_IsReported()
        {
            var setup = CreateSetup();
            try
            {
                setup.SourcePaint.RegionKind = StageRegionKind.Deposit;

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA006"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void MarkerWithoutPaintedCells_IsReported()
        {
            var setup = CreateSetup();
            try
            {
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1001u, new Vector2Int(0, 0));

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA019"), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void AnchorNotOnRegionCell_IsReported()
        {
            var setup = CreateSetup();
            try
            {
                setup.SourcePaint.SetCell(0, 0, 1001u);
                CreateAnchor(setup.StageGo.transform, StageRegionKind.Source, 1001u, new Vector2Int(1, 1));

                var issues = Validate(setup.Stage);
                Assert.That(issues.Any(x => x.Code == "STA018"), Is.True);
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

        private static StageRegionAnchorMarker CreateAnchor(Transform parent, StageRegionKind kind, uint stableId, Vector2Int anchorCell)
        {
            var go = new GameObject($"{kind}_{stableId}");
            go.transform.SetParent(parent);
            var marker = go.AddComponent<StageRegionAnchorMarker>();
            marker.RegionKind = kind;
            marker.StableId = stableId;
            marker.Active = true;
            marker.AnchorCell = anchorCell;
            return marker;
        }

        private static ValidationSetup CreateSetup()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var gridGo = new GameObject("grid");
            var tilemapGo = new GameObject("tilemap");
            stageGo.transform.SetParent(rootGo.transform);
            gridGo.transform.SetParent(stageGo.transform);
            tilemapGo.transform.SetParent(gridGo.transform);

            rootGo.AddComponent<StageLayoutRootMarker>();
            var stage = stageGo.AddComponent<StageLayoutStageMarker>();
            stage.TargetLayout = ScriptableObject.CreateInstance<StageLayoutSO>();

            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var tilemap = tilemapGo.AddComponent<Tilemap>();
            tilemapGo.AddComponent<TilemapRenderer>();
            tilemap.origin = Vector3Int.zero;
            tilemap.size = new Vector3Int(2, 2, 1);

            var sourcePaint = ScriptableObject.CreateInstance<StageRegionPaintAsset>();
            sourcePaint.RegionKind = StageRegionKind.Source;
            sourcePaint.Resize(2, 2);
            var depositPaint = ScriptableObject.CreateInstance<StageRegionPaintAsset>();
            depositPaint.RegionKind = StageRegionKind.Deposit;
            depositPaint.Resize(2, 2);

            var authoring = stageGo.AddComponent<StageGridAuthoring>();
            authoring.Grid = grid;
            authoring.MovementTilemap = tilemap;
            authoring.SourceRegionPaint = sourcePaint;
            authoring.DepositRegionPaint = depositPaint;

            return new ValidationSetup(rootGo, stageGo, stage, tilemap, sourcePaint, depositPaint);
        }

        private sealed class ValidationSetup
        {
            private readonly Object[] _ownedObjects;

            public ValidationSetup(GameObject rootGo, GameObject stageGo, StageLayoutStageMarker stage, Tilemap tilemap, StageRegionPaintAsset sourcePaint, StageRegionPaintAsset depositPaint)
            {
                _ownedObjects = new Object[]
                {
                    stage.TargetLayout,
                    sourcePaint,
                    depositPaint,
                    rootGo,
                };
                StageGo = stageGo;
                Stage = stage;
                Tilemap = tilemap;
                SourcePaint = sourcePaint;
                DepositPaint = depositPaint;
            }

            public GameObject StageGo { get; }
            public StageLayoutStageMarker Stage { get; }
            public Tilemap Tilemap { get; }
            public StageRegionPaintAsset SourcePaint { get; }
            public StageRegionPaintAsset DepositPaint { get; }

            public void Dispose()
            {
                for (int i = 0; i < _ownedObjects.Length; i++)
                    Object.DestroyImmediate(_ownedObjects[i]);
            }
        }
    }
}
