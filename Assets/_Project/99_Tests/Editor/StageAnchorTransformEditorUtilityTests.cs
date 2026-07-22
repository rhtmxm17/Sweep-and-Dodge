using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageAnchorTransformEditorUtilityTests
    {
        [Test]
        public void SyncRegionDataFromTransform_SnapsNegativeCellAndClearsOffset()
        {
            var setup = CreateSetup(new Vector3(10f, 2f, -4f), 2f);
            try
            {
                var marker = CreateRegionMarker(setup.Stage.transform);
                marker.AnchorOffset = new Vector2(0.25f, -0.1f);
                marker.transform.position = new Vector3(6.2f, 8f, 3.1f);

                bool changed = StageAnchorTransformEditorUtility.SyncRegionDataFromTransform(marker, recordUndo: false);

                Assert.That(changed, Is.True);
                Assert.That(marker.AnchorCell, Is.EqualTo(new Vector2Int(-2, 3)));
                Assert.That(marker.AnchorOffset, Is.EqualTo(Vector2.zero));
                AssertVector3(marker.transform.position, new Vector3(7f, 2f, 3f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void SyncRegionTransformFromData_PreservesExplicitOffset()
        {
            var setup = CreateSetup(new Vector3(10f, 2f, -4f), 2f);
            try
            {
                var marker = CreateRegionMarker(setup.Stage.transform);
                marker.AnchorCell = new Vector2Int(-2, 3);
                marker.AnchorOffset = new Vector2(0.25f, -0.1f);
                marker.transform.position = Vector3.zero;

                bool changed = StageAnchorTransformEditorUtility.SyncRegionTransformFromData(marker, recordUndo: false);

                Assert.That(changed, Is.True);
                Assert.That(marker.AnchorCell, Is.EqualTo(new Vector2Int(-2, 3)));
                Assert.That(marker.AnchorOffset, Is.EqualTo(new Vector2(0.25f, -0.1f)));
                AssertVector3(marker.transform.position, new Vector3(7.5f, 2f, 2.8f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void SyncRegionDataFromTransformIfChanged_IgnoresIdleSceneGuiAndPreservesExplicitOffset()
        {
            var setup = CreateSetup(new Vector3(10f, 2f, -4f), 2f);
            try
            {
                var marker = CreateRegionMarker(setup.Stage.transform);
                marker.AnchorCell = new Vector2Int(-2, 3);
                marker.AnchorOffset = new Vector2(0.25f, -0.1f);
                StageAnchorTransformEditorUtility.SyncRegionTransformFromData(marker, recordUndo: false);

                bool idleChanged = StageAnchorTransformEditorUtility.SyncRegionDataFromTransformIfChanged(marker, recordUndo: false);

                Assert.That(idleChanged, Is.False);
                Assert.That(marker.AnchorCell, Is.EqualTo(new Vector2Int(-2, 3)));
                Assert.That(marker.AnchorOffset, Is.EqualTo(new Vector2(0.25f, -0.1f)));

                marker.transform.position += new Vector3(1.2f, 5f, 0.8f);
                bool movedChanged = StageAnchorTransformEditorUtility.SyncRegionDataFromTransformIfChanged(marker, recordUndo: false);

                Assert.That(movedChanged, Is.True);
                Assert.That(marker.AnchorOffset, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void PlayerStart_RoundTrip_SnapsTransformAndAppliesExplicitData()
        {
            var setup = CreateSetup(new Vector3(50f, 1f, 0f), 1f);
            try
            {
                var markerGo = new GameObject("player_start");
                markerGo.transform.SetParent(setup.Stage.transform, false);
                var marker = markerGo.AddComponent<StagePlayerStartMarker>();
                marker.AnchorOffset = new Vector2(0.2f, 0.2f);
                marker.transform.SetPositionAndRotation(
                    new Vector3(55.2f, 7f, -2.8f),
                    Quaternion.Euler(0f, 135f, 0f));

                bool snapped = StageAnchorTransformEditorUtility.SyncPlayerDataFromTransform(marker, recordUndo: false);

                Assert.That(snapped, Is.True);
                Assert.That(marker.AnchorCell, Is.EqualTo(new Vector2Int(5, -3)));
                Assert.That(marker.AnchorOffset, Is.EqualTo(Vector2.zero));
                Assert.That(marker.YawDeg, Is.EqualTo(135f).Within(0.001f));
                AssertVector3(marker.transform.position, new Vector3(55.5f, 1f, -2.5f));

                marker.AnchorCell = new Vector2Int(-1, 2);
                marker.AnchorOffset = new Vector2(0.25f, -0.25f);
                marker.YawDeg = 270f;
                bool applied = StageAnchorTransformEditorUtility.SyncPlayerTransformFromData(marker, recordUndo: false);

                Assert.That(applied, Is.True);
                AssertVector3(marker.transform.position, new Vector3(49.75f, 1f, 2.25f));
                Assert.That(Quaternion.Angle(marker.transform.rotation, Quaternion.Euler(0f, 270f, 0f)), Is.LessThan(0.001f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void SyncPlayerDataFromTransformIfChanged_IgnoresIdleSceneGuiAndPreservesExplicitOffset()
        {
            var setup = CreateSetup(new Vector3(50f, 1f, 0f), 1f);
            try
            {
                var markerGo = new GameObject("player_start");
                markerGo.transform.SetParent(setup.Stage.transform, false);
                var marker = markerGo.AddComponent<StagePlayerStartMarker>();
                marker.AnchorCell = new Vector2Int(-1, 2);
                marker.AnchorOffset = new Vector2(0.25f, -0.25f);
                marker.YawDeg = 270f;
                StageAnchorTransformEditorUtility.SyncPlayerTransformFromData(marker, recordUndo: false);

                bool idleChanged = StageAnchorTransformEditorUtility.SyncPlayerDataFromTransformIfChanged(marker, recordUndo: false);

                Assert.That(idleChanged, Is.False);
                Assert.That(marker.AnchorCell, Is.EqualTo(new Vector2Int(-1, 2)));
                Assert.That(marker.AnchorOffset, Is.EqualTo(new Vector2(0.25f, -0.25f)));
                Assert.That(marker.YawDeg, Is.EqualTo(270f).Within(0.001f));

                marker.transform.SetPositionAndRotation(
                    marker.transform.position + new Vector3(1.2f, 5f, -0.8f),
                    Quaternion.Euler(0f, 135f, 0f));
                bool movedChanged = StageAnchorTransformEditorUtility.SyncPlayerDataFromTransformIfChanged(marker, recordUndo: false);

                Assert.That(movedChanged, Is.True);
                Assert.That(marker.AnchorOffset, Is.EqualTo(Vector2.zero));
                Assert.That(marker.YawDeg, Is.EqualTo(135f).Within(0.001f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void SyncRegionDataFromTransform_CollapsedSceneMoveUndo_RestoresDataAndTransform()
        {
            var setup = CreateSetup(Vector3.zero, 1f);
            try
            {
                var marker = CreateRegionMarker(setup.Stage.transform);
                marker.AnchorCell = Vector2Int.zero;
                marker.AnchorOffset = Vector2.zero;
                StageAnchorTransformEditorUtility.SyncRegionTransformFromData(marker, recordUndo: false);
                Vector3 initialPosition = marker.transform.position;

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Move Stage Region Anchor");
                Undo.RecordObject(marker.transform, "Move Stage Region Anchor");
                marker.transform.position = new Vector3(2.2f, 4f, 1.2f);
                StageAnchorTransformEditorUtility.SyncRegionDataFromTransform(marker, recordUndo: true);
                Undo.CollapseUndoOperations(undoGroup);
                Undo.FlushUndoRecordObjects();

                Assert.That(marker.AnchorCell, Is.EqualTo(new Vector2Int(2, 1)));
                AssertVector3(marker.transform.position, new Vector3(2.5f, 0f, 1.5f));

                Undo.PerformUndo();
                Assert.That(marker.AnchorCell, Is.EqualTo(Vector2Int.zero));
                AssertVector3(marker.transform.position, initialPosition);

                Undo.PerformRedo();
                Assert.That(marker.AnchorCell, Is.EqualTo(new Vector2Int(2, 1)));
                AssertVector3(marker.transform.position, new Vector3(2.5f, 0f, 1.5f));
            }
            finally
            {
                Undo.ClearAll();
                setup.Dispose();
            }
        }

        private static TestSetup CreateSetup(Vector3 gridWorldPosition, float cellSize)
        {
            var stageGo = new GameObject("stage");
            stageGo.transform.position = new Vector3(20f, 0f, 10f);
            stageGo.AddComponent<StageLayoutStageMarker>();
            var gridGo = new GameObject("grid");
            gridGo.transform.SetParent(stageGo.transform, false);
            gridGo.transform.SetPositionAndRotation(gridWorldPosition, Quaternion.Euler(90f, 0f, 0f));
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(cellSize, cellSize, 0f);
            var authoring = stageGo.AddComponent<StageGridAuthoring>();
            authoring.Grid = grid;
            return new TestSetup(stageGo, authoring);
        }

        private static StageRegionAnchorMarker CreateRegionMarker(Transform parent)
        {
            var markerGo = new GameObject("anchor");
            markerGo.transform.SetParent(parent, false);
            return markerGo.AddComponent<StageRegionAnchorMarker>();
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private sealed class TestSetup
        {
            public TestSetup(GameObject stage, StageGridAuthoring authoring)
            {
                Stage = stage;
                Authoring = authoring;
            }

            public GameObject Stage { get; }
            public StageGridAuthoring Authoring { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Stage);
            }
        }
    }
}
