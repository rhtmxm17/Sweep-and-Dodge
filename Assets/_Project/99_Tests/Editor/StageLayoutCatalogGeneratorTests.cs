using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageLayoutCatalogGeneratorTests
    {
        [Test]
        public void GenerateLayoutsForRoot_SortsByStageIdAndStableId()
        {
            var rootGo = new GameObject("root");
            var stage2Go = new GameObject("stage2");
            var stage1Go = new GameObject("stage1");
            var srcA = new GameObject("srcA");
            var srcB = new GameObject("srcB");
            var dep1 = new GameObject("dep1");
            var dep2 = new GameObject("dep2");
            var layout1 = ScriptableObject.CreateInstance<StageLayoutSO>();
            var layout2 = ScriptableObject.CreateInstance<StageLayoutSO>();

            try
            {
                stage2Go.transform.SetParent(rootGo.transform);
                stage1Go.transform.SetParent(rootGo.transform);
                srcA.transform.SetParent(stage1Go.transform);
                srcB.transform.SetParent(stage1Go.transform);
                dep1.transform.SetParent(stage1Go.transform);
                dep2.transform.SetParent(stage2Go.transform);

                var root = rootGo.AddComponent<StageLayoutRootMarker>();
                root.SortByStageId = true;

                var stage2 = stage2Go.AddComponent<StageLayoutStageMarker>();
                stage2.StageId = 2;
                stage2.TargetLayout = layout2;
                var stage1 = stage1Go.AddComponent<StageLayoutStageMarker>();
                stage1.StageId = 1;
                stage1.TargetLayout = layout1;

                var sourceA = srcA.AddComponent<StageSourceMarker>();
                sourceA.StableId = 20;
                sourceA.Shape = Shape2DKind.Circle;
                sourceA.Radius = 4f;

                var sourceB = srcB.AddComponent<StageSourceMarker>();
                sourceB.StableId = 10;
                sourceB.Shape = Shape2DKind.Circle;
                sourceB.Radius = 4f;

                var deposit1 = dep1.AddComponent<StageDepositMarker>();
                deposit1.StableId = 30;
                deposit1.Radius = 1f;
                var deposit2 = dep2.AddComponent<StageDepositMarker>();
                deposit2.StableId = 40;
                deposit2.Radius = 1f;

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True, string.Join("\n", issues.Select(x => x.Code + ":" + x.Message)));
                Assert.That(layout1.StageId, Is.EqualTo(1));
                Assert.That(layout2.StageId, Is.EqualTo(2));
                Assert.That(layout1.Sources[0].StableId, Is.EqualTo(10u));
                Assert.That(layout1.Sources[1].StableId, Is.EqualTo(20u));
                Assert.That(layout2.Deposits[0].StableId, Is.EqualTo(40u));
            }
            finally
            {
                Object.DestroyImmediate(layout1);
                Object.DestroyImmediate(layout2);
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void GenerateLayoutsForRoot_WithoutTargetLayout_FailsWithError()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            try
            {
                stageGo.transform.SetParent(rootGo.transform);
                rootGo.AddComponent<StageLayoutRootMarker>();
                var stage = stageGo.AddComponent<StageLayoutStageMarker>();
                stage.StageId = 1;

                bool ok = StageLayoutCatalogGenerator.TryGenerateLayoutsForRoot(rootGo.GetComponent<StageLayoutRootMarker>(), out var issues, saveAssets: false);

                Assert.That(ok, Is.False);
                Assert.That(issues.Any(x => x.Code == "STL901"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }
    }
}
