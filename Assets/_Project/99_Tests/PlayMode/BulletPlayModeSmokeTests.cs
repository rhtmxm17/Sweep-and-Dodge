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
        private const string OperationalScenePath = "Assets/_Project/01_Scenes/SampleScene.unity";

        [UnityTest]
        public IEnumerator PlayMode_OperationalScene_PipelineBootAndCoreLoop_RunWithoutHardErrors()
        {
            SceneManager.LoadScene(OperationalScenePath, LoadSceneMode.Single);
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
                "Operational scene ECS setup was not ready within timeout");

            var pipeline = world.GetExistingSystemManaged<BulletFramePipelineGroup>();
            Assert.That(pipeline, Is.Not.Null, "BulletFramePipelineGroup must exist in default world");

            int maxActiveBullets = 0;
            int framesWithActiveBullets = 0;

            for (int frame = 0; frame < 180; frame++)
            {
                yield return null;
                int activeCount = CountByComponentType<BulletActiveTag>(em);
                if (activeCount > 0)
                    framesWithActiveBullets++;
                if (activeCount > maxActiveBullets)
                    maxActiveBullets = activeCount;
            }

            Assert.That(framesWithActiveBullets, Is.GreaterThan(0), "Core loop should produce active bullets in operational scene");
            Assert.That(maxActiveBullets, Is.GreaterThan(0), "At least one active bullet must be observed");

            Debug.Log(
                $"[PlayModeSmoke] scene=SampleScene frames=180 maxActiveBullets={maxActiveBullets} framesWithActiveBullets={framesWithActiveBullets}");
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
