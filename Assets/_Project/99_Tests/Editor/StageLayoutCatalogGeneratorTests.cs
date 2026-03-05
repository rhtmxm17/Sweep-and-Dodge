using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageLayoutCatalogGeneratorTests
    {
        [Test]
        public void GenerateForRoot_SortsByStageIdAndStableId()
        {
            var rootGo = new GameObject("root");
            var stage2Go = new GameObject("stage2");
            var stage1Go = new GameObject("stage1");
            var srcA = new GameObject("srcA");
            var srcB = new GameObject("srcB");
            var dep1 = new GameObject("dep1");
            var dep2 = new GameObject("dep2");
            var catalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();

            try
            {
                stage2Go.transform.SetParent(rootGo.transform);
                stage1Go.transform.SetParent(rootGo.transform);
                srcA.transform.SetParent(stage1Go.transform);
                srcB.transform.SetParent(stage1Go.transform);
                dep1.transform.SetParent(stage1Go.transform);
                dep2.transform.SetParent(stage2Go.transform);

                var root = rootGo.AddComponent<StageLayoutRootMarker>();
                root.TargetCatalog = catalog;
                root.SortByStageId = true;

                var stage2 = stage2Go.AddComponent<StageLayoutStageMarker>();
                stage2.StageId = 2;
                var stage1 = stage1Go.AddComponent<StageLayoutStageMarker>();
                stage1.StageId = 1;

                var sourceA = srcA.AddComponent<StageSourceMarker>();
                sourceA.StableId = 20;
                sourceA.FieldShape = BulletFieldShapeId.Circle;
                sourceA.FieldRadius = 4f;

                var sourceB = srcB.AddComponent<StageSourceMarker>();
                sourceB.StableId = 10;
                sourceB.FieldShape = BulletFieldShapeId.Circle;
                sourceB.FieldRadius = 4f;

                var deposit1 = dep1.AddComponent<StageDepositMarker>();
                deposit1.StableId = 30;
                deposit1.Radius = 1f;
                var deposit2 = dep2.AddComponent<StageDepositMarker>();
                deposit2.StableId = 40;
                deposit2.Radius = 1f;

                bool ok = StageLayoutCatalogGenerator.TryGenerateForRoot(root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(catalog.Stages, Is.Not.Null);
                Assert.That(catalog.Stages.Length, Is.EqualTo(2));
                Assert.That(catalog.Stages[0].StageId, Is.EqualTo(1));
                Assert.That(catalog.Stages[1].StageId, Is.EqualTo(2));
                Assert.That(catalog.Stages[0].Sources[0].StableId, Is.EqualTo(10u));
                Assert.That(catalog.Stages[0].Sources[1].StableId, Is.EqualTo(20u));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void GenerateForRoot_WithoutTargetCatalog_FailsWithError()
        {
            var rootGo = new GameObject("root");
            try
            {
                var root = rootGo.AddComponent<StageLayoutRootMarker>();
                bool ok = StageLayoutCatalogGenerator.TryGenerateForRoot(root, out var issues, saveAssets: false);

                Assert.That(ok, Is.False);
                Assert.That(issues.Any(x => x.Code == "STG901"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }
    }
}

