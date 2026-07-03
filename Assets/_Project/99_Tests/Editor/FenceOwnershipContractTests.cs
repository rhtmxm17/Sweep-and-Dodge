using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SweepNDodge.DotsBullets.Tests
{
    public class FenceOwnershipContractTests
    {
        private const string SystemsRoot = "Assets/_Project/02_Scripts/ECS/Systems";

        [Test]
        public void CellMapFence_RuntimePublish_OnlyOccursInBulletSimulationSystems()
        {
            var files = FindFilesContaining("BulletFieldShared.CellMapFence = state.Dependency;");
            CollectionAssert.AreEquivalent(
                new[] { "BulletSimulationSystems.cs" },
                files);

            var combineFiles = FindFilesContaining("BulletFieldShared.CellMapFence = JobHandle.CombineDependencies(");
            CollectionAssert.AreEquivalent(
                new[] { "BulletSimulationSystems.cs" },
                combineFiles);
        }

        [Test]
        public void PoolFence_RuntimePublishers_AreSpawnAndDespawnOwnersOnly()
        {
            var files = FindFilesContaining("BulletFieldShared.PoolFence = state.Dependency;");
            CollectionAssert.AreEquivalent(
                new[] { "SpawnRequestSystems.cs", "DiscreteEmitExecutionSystems.cs", "BulletPoolOwnerSystems.cs" },
                files);
        }

        [Test]
        public void RequestCellMapReaders_CombineCellMapFence_BeforeScheduling()
        {
            var vacuum = ReadSystemFile("BulletVacuumRequestSystem.cs");
            StringAssert.Contains(
                "JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence)",
                vacuum);
            StringAssert.DoesNotContain("BulletFieldShared.CellMapFence =", vacuum);

            var hazard = ReadSystemFile("PlayerHazardCollisionSystem.cs");
            StringAssert.Contains(
                "JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence)",
                hazard);
            StringAssert.DoesNotContain("BulletFieldShared.CellMapFence =", hazard);
        }

        [Test]
        public void SpawnOwner_CombinesPoolFence_AndCompletesBeforeFreeByKeyAccess()
        {
            var spawn = ReadSystemFile("SpawnRequestSystems.cs");
            StringAssert.Contains(
                "var poolDeps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.PoolFence);",
                spawn);
            StringAssert.Contains("poolDeps.Complete();", spawn);

            var discreteEmit = ReadSystemFile("DiscreteEmitExecutionSystems.cs");
            StringAssert.Contains(
                "var poolDeps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.PoolFence);",
                discreteEmit);
            StringAssert.Contains("poolDeps.Complete();", discreteEmit);
        }

        private static string ReadSystemFile(string fileName)
        {
            var path = Path.Combine(GetProjectRoot(), SystemsRoot, fileName);
            Assert.That(File.Exists(path), Is.True, $"Missing file: {path}");
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static string[] FindFilesContaining(string snippet)
        {
            var root = Path.Combine(GetProjectRoot(), SystemsRoot);
            Assert.That(Directory.Exists(root), Is.True, $"Missing directory: {root}");

            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var owners = new List<string>();
            foreach (var file in files)
            {
                var text = File.ReadAllText(file, Encoding.UTF8);
                if (text.Contains(snippet))
                    owners.Add(Path.GetFileName(file));
            }

            return owners.ToArray();
        }

        private static string GetProjectRoot()
        {
            // Unity EditMode 테스트 기준으로 현재 작업 디렉터리는 프로젝트 루트다.
            return Directory.GetCurrentDirectory();
        }
    }
}
