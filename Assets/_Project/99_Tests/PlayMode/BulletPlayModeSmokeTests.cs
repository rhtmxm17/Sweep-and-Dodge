using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletPlayModeSmokeTests
    {
        private const string DedicatedScenePath = "Assets/_Project/01_Scenes/PlayModeTests/PlayModeSmoke_Dedicated.unity";
        private const string OperationalScenePath = "Assets/_Project/01_Scenes/SampleScene.unity";

        [UnityTest]
        public IEnumerator PlayMode_DedicatedScene_PipelineBootAndCoreLoop_RunWithoutHardErrors()
        {
            yield return RunSceneSmoke(
                scenePath: DedicatedScenePath,
                sceneLabel: "PlayModeSmoke_Dedicated",
                frameCount: 120);
        }

        [UnityTest]
        [Category("PeriodicOperationalScene")]
        public IEnumerator PlayMode_OperationalScene_PipelineBootAndCoreLoop_RunWithoutHardErrors()
        {
            yield return RunSceneSmoke(
                scenePath: OperationalScenePath,
                sceneLabel: "SampleScene",
                frameCount: 180);
        }

        private static IEnumerator RunSceneSmoke(string scenePath, string sceneLabel, int frameCount)
        {
            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    CountByComponentType<PlayerTag>(em) > 0 &&
                    CountByComponentType<SourceSpawnComponent>(em) > 0 &&
                    CountByComponentType<BulletFrameCounterComponent>(em) > 0,
                300,
                $"ECS setup was not ready within timeout. scene={sceneLabel}");

            var pipeline = world.GetExistingSystemManaged<BulletFramePipelineGroup>();
            Assert.That(pipeline, Is.Not.Null, "BulletFramePipelineGroup must exist in default world");

            int maxActiveBullets = 0;
            int framesWithActiveBullets = 0;

            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
                int activeCount = CountByComponentType<BulletActiveTag>(em);
                if (activeCount > 0)
                    framesWithActiveBullets++;
                if (activeCount > maxActiveBullets)
                    maxActiveBullets = activeCount;
            }

            Assert.That(framesWithActiveBullets, Is.GreaterThan(0), $"Core loop should produce active bullets. scene={sceneLabel}");
            Assert.That(maxActiveBullets, Is.GreaterThan(0), $"At least one active bullet must be observed. scene={sceneLabel}");

            Debug.Log(
                $"[PlayModeSmoke] scene={sceneLabel} frames={frameCount} maxActiveBullets={maxActiveBullets} framesWithActiveBullets={framesWithActiveBullets}");
        }

        private static IEnumerator WaitForCondition(System.Func<bool> predicate, int timeoutFrames, string failMessage)
        {
            for (int i = 0; i < timeoutFrames; i++)
            {
                if (predicate())
                    yield break;
                yield return null;
            }

            Assert.Fail(failMessage);
        }

        private static int CountByComponentType<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }
    }
}
