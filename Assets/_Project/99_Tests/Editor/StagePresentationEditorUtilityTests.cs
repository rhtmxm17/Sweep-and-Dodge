using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StagePresentationEditorUtilityTests
    {
        [Test]
        public void ResolveCatalog_FromNearestRoot_ReturnsAssignedCatalog()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var markerGo = new GameObject("presentation");
            var catalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();

            try
            {
                stageGo.transform.SetParent(rootGo.transform);
                markerGo.transform.SetParent(stageGo.transform);

                var root = rootGo.AddComponent<StageLayoutRootMarker>();
                root.TargetPresentationCatalog = catalog;
                var marker = markerGo.AddComponent<StagePresentationMarker>();

                Assert.That(StagePresentationEditorUtility.TryResolveCatalog(marker, out var resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(catalog));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void FindOwningStage_ReturnsNearestStageMarker()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var markerGo = new GameObject("presentation");

            try
            {
                stageGo.transform.SetParent(rootGo.transform);
                markerGo.transform.SetParent(stageGo.transform);

                var stage = stageGo.AddComponent<StageLayoutStageMarker>();
                var marker = markerGo.AddComponent<StagePresentationMarker>();

                Assert.That(StagePresentationEditorUtility.FindOwningStage(marker), Is.EqualTo(stage));
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void GetPresentationKeys_ReturnsDistinctSortedKeys()
        {
            var catalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();
            try
            {
                catalog.Entries = new[]
                {
                    new StagePresentationCatalogEntry { PresentationKey = "b_key" },
                    new StagePresentationCatalogEntry { PresentationKey = "a_key" },
                    new StagePresentationCatalogEntry { PresentationKey = "b_key" },
                    new StagePresentationCatalogEntry { PresentationKey = " " },
                };

                var keys = StagePresentationEditorUtility.GetPresentationKeys(catalog);
                Assert.That(keys, Is.EqualTo(new[] { "a_key", "b_key" }));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TryFindLinkedParent_DoesNotResolveNonTopologyParent()
        {
            var rootGo = new GameObject("root");
            var helperGo = new GameObject("helper");
            var markerGo = new GameObject("presentation");

            try
            {
                helperGo.transform.SetParent(rootGo.transform);
                markerGo.transform.SetParent(helperGo.transform);

                Assert.That(StagePresentationEditorUtility.TryFindLinkedParent(markerGo.transform, out _, out _, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void TryFindLinkedParent_ResolvesRegionAnchorViaSlotMapping()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var gridGo = new GameObject("grid");
            var markerGo = new GameObject("presentation");

            try
            {
                stageGo.transform.SetParent(rootGo.transform);
                gridGo.transform.SetParent(stageGo.transform);

                stageGo.AddComponent<StageLayoutStageMarker>();
                var grid = gridGo.AddComponent<Grid>();
                var authoring = stageGo.AddComponent<StageGridAuthoring>();
                authoring.Grid = grid;
                authoring.SourceRegionMappings = new[]
                {
                    new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 1001u },
                };

                var anchorGo = new GameObject("source_anchor");
                anchorGo.transform.SetParent(stageGo.transform);
                var anchor = anchorGo.AddComponent<StageRegionAnchorMarker>();
                anchor.RegionKind = StageRegionKind.Source;
                anchor.RegionSlotIndex = 1;

                markerGo.transform.SetParent(anchorGo.transform);

                Assert.That(StagePresentationEditorUtility.TryFindLinkedParent(markerGo.transform, out var kind, out var stableId, out var parent), Is.True);
                Assert.That(kind, Is.EqualTo(StagePresentationLinkKind.Source));
                Assert.That(stableId, Is.EqualTo(1001u));
                Assert.That(parent, Is.EqualTo(anchorGo.transform));
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void GetPreviewMatrix_UsesMarkerWorldTransform()
        {
            var go = new GameObject("presentation");
            try
            {
                go.transform.position = new Vector3(3f, 4f, 5f);
                go.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
                go.transform.localScale = new Vector3(2f, 3f, 4f);
                var marker = go.AddComponent<StagePresentationMarker>();

                var matrix = StagePresentationEditorUtility.GetPreviewMatrix(marker);
                var origin = matrix.MultiplyPoint3x4(Vector3.zero);
                Assert.That(origin.x, Is.EqualTo(3f).Within(0.001f));
                Assert.That(origin.y, Is.EqualTo(4f).Within(0.001f));
                Assert.That(origin.z, Is.EqualTo(5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryComputePrefabBounds_WithPrimitivePrefab_ReturnsNonZeroBounds()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.That(StagePresentationEditorUtility.TryComputePrefabBounds(prefab, out var bounds), Is.True);
                Assert.That(bounds.size.x, Is.GreaterThan(0f));
                Assert.That(bounds.size.y, Is.GreaterThan(0f));
                Assert.That(bounds.size.z, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
