using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class ReplayFilePersistenceTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "SweepNDodge-ReplayPersistenceTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrWhiteSpace(_tempDirectory) || !Directory.Exists(_tempDirectory))
                return;

            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // no-op
            }
        }

        [Test]
        public void SaveThenLoad_SameVersion_Succeeds()
        {
            string path = BuildPath("roundtrip.rply");
            uint expectedRunSeed = 0x1234u;
            var expectedInputs = CreateSampleInputs();

            bool saved = ReplayFilePersistence.TrySave(
                path,
                expectedRunSeed,
                expectedInputs,
                out var saveError,
                out var saveMessage);

            Assert.That(saved, Is.True, saveMessage);
            Assert.That(saveError, Is.EqualTo(ReplayIoError.None));
            Assert.That(File.Exists(path), Is.True);

            bool loaded = ReplayFilePersistence.TryLoad(
                path,
                out uint loadedRunSeed,
                out List<ReplayTickInputElement> loadedInputs,
                out var loadError,
                out var loadMessage);

            Assert.That(loaded, Is.True, loadMessage);
            Assert.That(loadError, Is.EqualTo(ReplayIoError.None));
            Assert.That(loadedRunSeed, Is.EqualTo(expectedRunSeed));
            Assert.That(loadedInputs.Count, Is.EqualTo(expectedInputs.Count));

            for (int i = 0; i < expectedInputs.Count; i++)
                AssertInputEqual(expectedInputs[i], loadedInputs[i], i);
        }

        [Test]
        public void Save_EmptyFrames_SucceedsWithZeroCount()
        {
            string path = BuildPath("empty.rply");
            var emptyInputs = new List<ReplayTickInputElement>(0);

            bool saved = ReplayFilePersistence.TrySave(
                path,
                777u,
                emptyInputs,
                out var saveError,
                out var saveMessage);

            Assert.That(saved, Is.True, saveMessage);
            Assert.That(saveError, Is.EqualTo(ReplayIoError.None));

            bool loaded = ReplayFilePersistence.TryLoad(
                path,
                out uint loadedRunSeed,
                out List<ReplayTickInputElement> loadedInputs,
                out var loadError,
                out var loadMessage);

            Assert.That(loaded, Is.True, loadMessage);
            Assert.That(loadError, Is.EqualTo(ReplayIoError.None));
            Assert.That(loadedRunSeed, Is.EqualTo(777u));
            Assert.That(loadedInputs.Count, Is.EqualTo(0));
        }

        [Test]
        public void Load_VersionMismatch_FailsFast()
        {
            string path = BuildPath("version-mismatch.rply");
            SaveFixture(path);
            OverwriteUInt32AtOffset(path, offset: 4, value: ReplayFilePersistence.CurrentSchemaVersion + 1u);

            bool loaded = ReplayFilePersistence.TryLoad(
                path,
                out _,
                out List<ReplayTickInputElement> _,
                out var reason,
                out var message);

            Assert.That(loaded, Is.False);
            Assert.That(reason, Is.EqualTo(ReplayIoError.UnsupportedVersion));
            StringAssert.Contains("fileVersion", message);
            StringAssert.Contains("currentVersion", message);
        }

        [Test]
        public void Load_InvalidMagic_FailsFast()
        {
            string path = BuildPath("invalid-magic.rply");
            SaveFixture(path);
            OverwriteUInt32AtOffset(path, offset: 0, value: 0xDEADBEEFu);

            bool loaded = ReplayFilePersistence.TryLoad(
                path,
                out _,
                out List<ReplayTickInputElement> _,
                out var reason,
                out _);

            Assert.That(loaded, Is.False);
            Assert.That(reason, Is.EqualTo(ReplayIoError.InvalidMagic));
        }

        [Test]
        public void Load_CorruptedPayload_FailsFast()
        {
            string path = BuildPath("corrupt-payload.rply");
            SaveFixture(path);
            CorruptLastByte(path);

            bool loaded = ReplayFilePersistence.TryLoad(
                path,
                out _,
                out List<ReplayTickInputElement> _,
                out var reason,
                out _);

            Assert.That(loaded, Is.False);
            Assert.That(reason, Is.EqualTo(ReplayIoError.CorruptedPayload));
        }

        private string BuildPath(string fileName)
        {
            return Path.Combine(_tempDirectory, fileName);
        }

        private static List<ReplayTickInputElement> CreateSampleInputs()
        {
            return new List<ReplayTickInputElement>
            {
                new ReplayTickInputElement
                {
                    Tick = 3u,
                    MoveAxis = new float2(0.25f, -0.5f),
                    AimWorldXZ = new float2(4f, 8f),
                    HasAimWorldPoint = 1,
                    VacuumRequested = 1,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = 0,
                    InputSequence = 10u,
                },
                new ReplayTickInputElement
                {
                    Tick = 4u,
                    MoveAxis = new float2(-1f, 1f),
                    AimWorldXZ = new float2(-5f, 9f),
                    HasAimWorldPoint = 0,
                    VacuumRequested = 0,
                    CleanupActionRequested = 1,
                    RequestedCleanupActionSlot = 2,
                    InputSequence = 11u,
                }
            };
        }

        private static void AssertInputEqual(
            ReplayTickInputElement expected,
            ReplayTickInputElement actual,
            int index)
        {
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick), $"input[{index}].Tick");
            Assert.That(actual.MoveAxis, Is.EqualTo(expected.MoveAxis), $"input[{index}].MoveAxis");
            Assert.That(actual.AimWorldXZ, Is.EqualTo(expected.AimWorldXZ), $"input[{index}].AimWorldXZ");
            Assert.That(actual.HasAimWorldPoint, Is.EqualTo(expected.HasAimWorldPoint), $"input[{index}].HasAimWorldPoint");
            Assert.That(actual.VacuumRequested, Is.EqualTo(expected.VacuumRequested), $"input[{index}].VacuumRequested");
            Assert.That(actual.CleanupActionRequested, Is.EqualTo(expected.CleanupActionRequested), $"input[{index}].CleanupActionRequested");
            Assert.That(actual.RequestedCleanupActionSlot, Is.EqualTo(expected.RequestedCleanupActionSlot), $"input[{index}].RequestedCleanupActionSlot");
            Assert.That(actual.InputSequence, Is.EqualTo(expected.InputSequence), $"input[{index}].InputSequence");
        }

        private static void SaveFixture(string path)
        {
            bool saved = ReplayFilePersistence.TrySave(
                path,
                0xABCDu,
                CreateSampleInputs(),
                out var reason,
                out var message);

            Assert.That(saved, Is.True, message);
            Assert.That(reason, Is.EqualTo(ReplayIoError.None));
        }

        private static void OverwriteUInt32AtOffset(string path, int offset, uint value)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            file.Position = offset;
            using var writer = new BinaryWriter(file);
            writer.Write(value);
            writer.Flush();
        }

        private static void CorruptLastByte(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.GreaterThan(0));
            bytes[bytes.Length - 1] ^= 0xFF;
            File.WriteAllBytes(path, bytes);
        }
    }
}
