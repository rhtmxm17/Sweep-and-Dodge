using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StagePresentationPreviewManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            StagePresentationPreviewManager.ClearAllPreviews();
            StagePresentationPreviewManager.SetPreviewScope(StagePresentationPreviewScope.SelectedStageOnly);
            Selection.activeGameObject = null;
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeGameObject = null;
            StagePresentationPreviewManager.ClearAllPreviews();
        }

        [Test]
        public void SelectedStageOnly_ShowsOnlySelectedStagePreview()
        {
            var fixture = new PreviewFixture();
            try
            {
                Selection.activeGameObject = fixture.Stage1Marker.gameObject;
                StagePresentationPreviewManager.ForceRefresh();

                Assert.That(StagePresentationPreviewManager.HasPreviewForStage(fixture.Stage1), Is.True);
                Assert.That(StagePresentationPreviewManager.HasPreviewForStage(fixture.Stage2), Is.False);
                Assert.That(StagePresentationPreviewManager.GetPreviewStageCount(), Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SelectionChange_RebuildsForNewStage_AndClearsPrevious()
        {
            var fixture = new PreviewFixture();
            try
            {
                Selection.activeGameObject = fixture.Stage1Marker.gameObject;
                StagePresentationPreviewManager.ForceRefresh();
                Selection.activeGameObject = fixture.Stage2Marker.gameObject;
                StagePresentationPreviewManager.ForceRefresh();

                Assert.That(StagePresentationPreviewManager.HasPreviewForStage(fixture.Stage1), Is.False);
                Assert.That(StagePresentationPreviewManager.HasPreviewForStage(fixture.Stage2), Is.True);
                Assert.That(StagePresentationPreviewManager.GetPreviewStageCount(), Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SelectionNone_KeepsLastActiveStagePreview()
        {
            var fixture = new PreviewFixture();
            try
            {
                Selection.activeGameObject = fixture.Stage2Marker.gameObject;
                StagePresentationPreviewManager.ForceRefresh();
                Selection.activeGameObject = null;
                StagePresentationPreviewManager.ForceRefresh();

                Assert.That(StagePresentationPreviewManager.HasPreviewForStage(fixture.Stage2), Is.True);
                Assert.That(StagePresentationPreviewManager.GetPreviewStageCount(), Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void MissingRootCatalog_SkipsPreviewCreation()
        {
            var fixture = new PreviewFixture(assignCatalog: false);
            try
            {
                Selection.activeGameObject = fixture.Stage1Marker.gameObject;
                StagePresentationPreviewManager.ForceRefresh();

                Assert.That(StagePresentationPreviewManager.GetPreviewStageCount(), Is.EqualTo(0));
                Assert.That(StagePresentationPreviewManager.GetPreviewInstanceCount(), Is.EqualTo(0));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void LinkedPresentation_UsesMarkerWorldTransform()
        {
            var fixture = new PreviewFixture();
            try
            {
                fixture.Stage1Marker.transform.localPosition = new Vector3(0.5f, 0f, 1.25f);
                fixture.Stage1Marker.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                fixture.Stage1Marker.transform.localScale = new Vector3(1.2f, 1f, 0.8f);

                Selection.activeGameObject = fixture.Stage1Marker.gameObject;
                StagePresentationPreviewManager.ForceRefresh();

                var preview = StagePresentationPreviewManager.FindPreviewInstance(fixture.Stage1, fixture.Stage1Marker.StableId);
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.transform.position.x, Is.EqualTo(fixture.Stage1Marker.transform.position.x).Within(0.001f));
                Assert.That(preview.transform.position.z, Is.EqualTo(fixture.Stage1Marker.transform.position.z).Within(0.001f));
                Assert.That(preview.transform.rotation.eulerAngles.y, Is.EqualTo(fixture.Stage1Marker.transform.rotation.eulerAngles.y).Within(0.001f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void PreviewInstance_IsTransientAndNotSceneOwned()
        {
            var fixture = new PreviewFixture();
            try
            {
                Selection.activeGameObject = fixture.Stage1Marker.gameObject;
                StagePresentationPreviewManager.ForceRefresh();

                var preview = StagePresentationPreviewManager.FindPreviewInstance(fixture.Stage1, fixture.Stage1Marker.StableId);
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private sealed class PreviewFixture
        {
            public readonly GameObject RootGo;
            public readonly GameObject Stage1Go;
            public readonly GameObject Stage2Go;
            public readonly StageLayoutRootMarker Root;
            public readonly StageLayoutStageMarker Stage1;
            public readonly StageLayoutStageMarker Stage2;
            public readonly StagePresentationCatalogSO Catalog;
            public readonly StagePresentationMarker Stage1Marker;
            public readonly StagePresentationMarker Stage2Marker;

            public PreviewFixture(bool assignCatalog = true)
            {
                RootGo = new GameObject("root");
                Stage1Go = new GameObject("Stage1");
                Stage2Go = new GameObject("Stage2");
                Stage1Go.transform.SetParent(RootGo.transform);
                Stage2Go.transform.SetParent(RootGo.transform);

                Root = RootGo.AddComponent<StageLayoutRootMarker>();
                Stage1 = Stage1Go.AddComponent<StageLayoutStageMarker>();
                Stage2 = Stage2Go.AddComponent<StageLayoutStageMarker>();
                Stage1.StageId = 1;
                Stage2.StageId = 2;

                var obstacleGo = new GameObject("Obstacle");
                obstacleGo.transform.SetParent(Stage1Go.transform);
                var obstacle = obstacleGo.AddComponent<StageObstacleMarker>();
                obstacle.StableId = 3001;

                var linkedGo = new GameObject("LinkedPresentation");
                linkedGo.transform.SetParent(obstacleGo.transform);
                Stage1Marker = linkedGo.AddComponent<StagePresentationMarker>();
                Stage1Marker.StableId = 1001;
                Stage1Marker.PlacementMode = StagePresentationPlacementMode.LinkedToParent;
                Stage1Marker.PresentationKey = "preview_a";

                var standaloneGo = new GameObject("StandalonePresentation");
                standaloneGo.transform.SetParent(Stage2Go.transform);
                standaloneGo.transform.position = new Vector3(10f, 0f, 2f);
                Stage2Marker = standaloneGo.AddComponent<StagePresentationMarker>();
                Stage2Marker.StableId = 1002;
                Stage2Marker.PlacementMode = StagePresentationPlacementMode.Standalone;
                Stage2Marker.PresentationKey = "preview_b";

                Catalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();
                Catalog.Entries = new[]
                {
                    new StagePresentationCatalogEntry
                    {
                        PresentationKey = "preview_a",
                        Prefab = GameObject.CreatePrimitive(PrimitiveType.Cube),
                        Usage = StagePresentationUsageFlags.ObstacleLinked,
                    },
                    new StagePresentationCatalogEntry
                    {
                        PresentationKey = "preview_b",
                        Prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere),
                        Usage = StagePresentationUsageFlags.Standalone,
                    },
                };

                if (assignCatalog)
                    Root.TargetPresentationCatalog = Catalog;
            }

            public void Dispose()
            {
                for (int i = 0; i < Catalog.Entries.Length; i++)
                {
                    if (Catalog.Entries[i].Prefab != null)
                        Object.DestroyImmediate(Catalog.Entries[i].Prefab);
                }

                Object.DestroyImmediate(Catalog);
                Object.DestroyImmediate(RootGo);
            }
        }
    }
}
