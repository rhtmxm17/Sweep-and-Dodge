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
        public IEnumerator PlayMode_DedicatedScene_StressSwitch_BurstRequest_ImpactsBacklogAndHud()
        {
            SceneManager.LoadScene(DedicatedScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "DefaultGameObjectInjectionWorld must exist in PlayMode");

            var em = world.EntityManager;
            yield return WaitForCondition(
                () =>
                    CountByComponentType<PlayerTag>(em) > 0 &&
                    CountByComponentType<SourceSpawnComponent>(em) > 0 &&
                    CountByComponentType<BulletFrameCounterComponent>(em) > 0 &&
                    HasSingleton<StressSwitchStateComponent>(em) &&
                    HasSingleton<SpawnBacklogMetricsComponent>(em) &&
                    HasSingleton<DebugHudMetricsComponent>(em),
                300,
                "ECS singleton setup for stress/HUD was not ready within timeout.");

            int baselineMaxPending = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                var baselineMetrics = GetSingleton<SpawnBacklogMetricsComponent>(em);
                baselineMaxPending = Mathf.Max(baselineMaxPending, baselineMetrics.PendingCount);
            }

            var stressEntity = GetSingletonEntity<StressSwitchStateComponent>(em);
            var stress = em.GetComponentData<StressSwitchStateComponent>(stressEntity);
            stress.Mode = (byte)StressSwitchModeId.BurstOnce;
            stress.BurstCount = 20000;
            stress.PreferredBulletTypeKey = -1;
            stress.RequestExecute = 1;
            em.SetComponentData(stressEntity, stress);

            int postMaxPending = 0;
            int postMaxHudSpawned = 0;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                var postMetrics = GetSingleton<SpawnBacklogMetricsComponent>(em);
                var hud = GetSingleton<DebugHudMetricsComponent>(em);
                postMaxPending = Mathf.Max(postMaxPending, postMetrics.PendingCount);
                postMaxHudSpawned = Mathf.Max(postMaxHudSpawned, hud.SpawnedThisFrame);
            }

            var stressAfter = GetSingleton<StressSwitchStateComponent>(em);
            Assert.That(stressAfter.RequestExecute, Is.EqualTo(0), "Stress request flag must be consumed");
            Assert.That(stressAfter.Mode, Is.EqualTo((byte)StressSwitchModeId.None), "Burst mode must finish as one-shot request");
            Assert.That(postMaxPending, Is.GreaterThan(baselineMaxPending + 1000), "Burst request should noticeably increase pending backlog");
            Assert.That(postMaxHudSpawned, Is.GreaterThan(0), "HUD spawned metric should be updated during burst run");
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
            int maxGhostInactiveRendered = 0;
            int maxRequestedRendered = 0;
            int maxActiveHidden = 0;
            int maxNonPositiveLifeRendered = 0;

            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
                int activeCount = CountByComponentType<BulletActiveTag>(em);
                if (activeCount > 0)
                    framesWithActiveBullets++;
                if (activeCount > maxActiveBullets)
                    maxActiveBullets = activeCount;

                if (HasSingleton<DebugHudMetricsComponent>(em))
                {
                    var hud = GetSingleton<DebugHudMetricsComponent>(em);
                    maxGhostInactiveRendered = Mathf.Max(maxGhostInactiveRendered, hud.GhostInactiveRendered);
                    maxRequestedRendered = Mathf.Max(maxRequestedRendered, hud.RequestedRendered);
                    maxActiveHidden = Mathf.Max(maxActiveHidden, hud.ActiveHidden);
                    maxNonPositiveLifeRendered = Mathf.Max(maxNonPositiveLifeRendered, hud.NonPositiveLifeRendered);
                }
            }

            Assert.That(framesWithActiveBullets, Is.GreaterThan(0), $"Core loop should produce active bullets. scene={sceneLabel}");
            Assert.That(maxActiveBullets, Is.GreaterThan(0), $"At least one active bullet must be observed. scene={sceneLabel}");

            Debug.Log(
                $"[PlayModeSmoke] scene={sceneLabel} frames={frameCount} maxActiveBullets={maxActiveBullets} framesWithActiveBullets={framesWithActiveBullets} " +
                $"traceGhostInactiveRendered={maxGhostInactiveRendered} traceRequestedRendered={maxRequestedRendered} " +
                $"traceActiveHidden={maxActiveHidden} traceNonPositiveLifeRendered={maxNonPositiveLifeRendered}");
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

        private static bool HasSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static Entity GetSingletonEntity<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingletonEntity();
        }

        private static T GetSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }
    }
}
