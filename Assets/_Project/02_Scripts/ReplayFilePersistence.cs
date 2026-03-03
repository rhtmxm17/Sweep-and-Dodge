using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum ReplayIoError
    {
        None = 0,
        InvalidState = 1,
        InvalidMagic = 2,
        UnsupportedVersion = 3,
        CorruptedPayload = 4,
        IoFailure = 5,
    }

    public struct ReplayTickInputElement
    {
        public uint Tick;
        public float2 MoveAxis;
        public float2 AimWorldXZ;
        public byte HasAimWorldPoint;
        public byte VacuumRequested;
        public byte CleanupActionRequested;
        public byte RequestedCleanupActionSlot;
        public uint InputSequence;
    }

    public static class ReplayFilePersistence
    {
        public const uint Magic = 0x52504C59u; // "RPLY"
        public const uint CurrentSchemaVersion = 2u;

        private const uint Fnv1aOffsetBasis = 2166136261u;
        private const uint Fnv1aPrime = 16777619u;
        private const int HeaderByteSize = 24;
        private const int TickInputByteSize = 28;

        public static bool TrySave(
            string path,
            uint runSeed,
            IReadOnlyList<ReplayTickInputElement> tickInputs,
            out ReplayIoError reason,
            out string message)
        {
            reason = ReplayIoError.None;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                reason = ReplayIoError.InvalidState;
                message = "Save failed: path is null or empty.";
                return false;
            }

            if (tickInputs == null)
            {
                reason = ReplayIoError.InvalidState;
                message = "Save failed: replay tick inputs are null.";
                return false;
            }

            string tempPath = $"{path}.tmp";
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var snapshotInputs = CreateTickInputSnapshot(tickInputs);
                byte[] payload = SerializePayload(snapshotInputs);
                uint checksum = ComputeChecksum(payload);
                uint normalizedRunSeed = runSeed > 0u ? runSeed : 1u;

                using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                {
                    writer.Write(Magic);
                    writer.Write(CurrentSchemaVersion);
                    writer.Write((uint)snapshotInputs.Count);
                    writer.Write(normalizedRunSeed);
                    writer.Write((uint)payload.Length);
                    writer.Write(checksum);
                    writer.Write(payload);
                    writer.Flush();
                }

                if (File.Exists(path))
                    File.Replace(tempPath, path, null, true);
                else
                    File.Move(tempPath, path);

                return true;
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is NotSupportedException ||
                ex is ArgumentException ||
                ex is PathTooLongException)
            {
                reason = ReplayIoError.IoFailure;
                message = $"Save failed: path='{path}', reason='{ex.Message}'.";
                return false;
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }

        public static bool TrySave(
            string path,
            uint runSeed,
            IReadOnlyList<ReplayInputFrameBufferElement> frames,
            out ReplayIoError reason,
            out string message)
        {
            if (frames == null)
            {
                reason = ReplayIoError.InvalidState;
                message = "Save failed: replay frames are null.";
                return false;
            }

            return TrySave(path, runSeed, CreateTickInputsFromFrameSnapshots(frames), out reason, out message);
        }

        public static bool TryLoad(
            string path,
            out uint runSeed,
            out List<ReplayTickInputElement> tickInputs,
            out ReplayIoError reason,
            out string message)
        {
            runSeed = 1u;
            tickInputs = new List<ReplayTickInputElement>(0);
            reason = ReplayIoError.None;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                reason = ReplayIoError.InvalidState;
                message = "Load failed: path is null or empty.";
                return false;
            }

            try
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (file.Length < HeaderByteSize)
                {
                    reason = ReplayIoError.CorruptedPayload;
                    message = $"Load failed: header too short, path='{path}', bytes={file.Length}.";
                    return false;
                }

                using var reader = new BinaryReader(file);
                uint magic = reader.ReadUInt32();
                uint version = reader.ReadUInt32();
                uint tickCount = reader.ReadUInt32();
                uint fileRunSeed = reader.ReadUInt32();
                uint payloadByteLength = reader.ReadUInt32();
                uint expectedChecksum = reader.ReadUInt32();

                if (magic != Magic)
                {
                    reason = ReplayIoError.InvalidMagic;
                    message = $"Load failed: invalid magic, expected={Magic}, actual={magic}, path='{path}'.";
                    return false;
                }

                if (version != CurrentSchemaVersion)
                {
                    reason = ReplayIoError.UnsupportedVersion;
                    message = $"Load failed: unsupported schema version, fileVersion={version}, currentVersion={CurrentSchemaVersion}, path='{path}'.";
                    return false;
                }

                long expectedPayloadByteLength = (long)tickCount * TickInputByteSize;
                if (expectedPayloadByteLength != payloadByteLength)
                {
                    reason = ReplayIoError.CorruptedPayload;
                    message = $"Load failed: payload size mismatch, expected={expectedPayloadByteLength}, actual={payloadByteLength}, path='{path}'.";
                    return false;
                }

                if (payloadByteLength > int.MaxValue)
                {
                    reason = ReplayIoError.CorruptedPayload;
                    message = $"Load failed: payload too large, bytes={payloadByteLength}, path='{path}'.";
                    return false;
                }

                byte[] payload = reader.ReadBytes((int)payloadByteLength);
                if (payload.Length != payloadByteLength || file.Position != file.Length)
                {
                    reason = ReplayIoError.CorruptedPayload;
                    message = $"Load failed: payload read length mismatch, expected={payloadByteLength}, actual={payload.Length}, path='{path}'.";
                    return false;
                }

                uint actualChecksum = ComputeChecksum(payload);
                if (actualChecksum != expectedChecksum)
                {
                    reason = ReplayIoError.CorruptedPayload;
                    message = $"Load failed: checksum mismatch, expected={expectedChecksum}, actual={actualChecksum}, path='{path}'.";
                    return false;
                }

                tickInputs = DeserializePayload(payload, tickCount);
                runSeed = fileRunSeed > 0u ? fileRunSeed : 1u;
                return true;
            }
            catch (InvalidDataException ex)
            {
                reason = ReplayIoError.CorruptedPayload;
                message = $"Load failed: corrupted payload, path='{path}', reason='{ex.Message}'.";
                return false;
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is NotSupportedException ||
                ex is ArgumentException ||
                ex is PathTooLongException)
            {
                reason = ReplayIoError.IoFailure;
                message = $"Load failed: path='{path}', reason='{ex.Message}'.";
                return false;
            }
        }

        public static bool TryLoad(
            string path,
            out uint runSeed,
            out List<ReplayInputFrameBufferElement> frames,
            out ReplayIoError reason,
            out string message)
        {
            if (!TryLoad(path, out runSeed, out List<ReplayTickInputElement> tickInputs, out reason, out message))
            {
                frames = new List<ReplayInputFrameBufferElement>(0);
                return false;
            }

            frames = ConvertToFrameSnapshots(tickInputs);
            return true;
        }

        public static bool TryLoadAndStagePlayback(string path, out ReplayIoError reason, out string message)
        {
            reason = ReplayIoError.InvalidState;
            message = "Load-and-stage is not supported for tick-input replay schema yet. Integrate with FixedTick playback pipeline first.";
            return false;
        }

        private static List<ReplayTickInputElement> CreateTickInputSnapshot(IReadOnlyList<ReplayTickInputElement> source)
        {
            int count = source.Count;
            var snapshot = new List<ReplayTickInputElement>(count);
            for (int i = 0; i < count; i++)
                snapshot.Add(source[i]);
            return snapshot;
        }

        private static List<ReplayTickInputElement> CreateTickInputsFromFrameSnapshots(IReadOnlyList<ReplayInputFrameBufferElement> frames)
        {
            int count = frames.Count;
            var tickInputs = new List<ReplayTickInputElement>(count);
            for (int i = 0; i < count; i++)
            {
                var frame = frames[i];
                tickInputs.Add(new ReplayTickInputElement
                {
                    Tick = frame.Frame,
                    MoveAxis = frame.MoveAxis,
                    AimWorldXZ = frame.AimWorldXZ,
                    HasAimWorldPoint = frame.HasAimWorldPoint,
                    VacuumRequested = frame.VacuumRequested,
                    CleanupActionRequested = frame.CleanupActionRequested,
                    RequestedCleanupActionSlot = frame.RequestedCleanupActionSlot,
                    InputSequence = frame.InputSequence,
                });
            }

            return tickInputs;
        }

        private static List<ReplayInputFrameBufferElement> ConvertToFrameSnapshots(IReadOnlyList<ReplayTickInputElement> tickInputs)
        {
            int count = tickInputs.Count;
            var frames = new List<ReplayInputFrameBufferElement>(count);
            for (int i = 0; i < count; i++)
            {
                var tickInput = tickInputs[i];
                frames.Add(new ReplayInputFrameBufferElement
                {
                    Frame = tickInput.Tick,
                    MoveAxis = tickInput.MoveAxis,
                    AimWorldXZ = tickInput.AimWorldXZ,
                    HasAimWorldPoint = tickInput.HasAimWorldPoint,
                    Position = float3.zero,
                    Rotation = quaternion.identity,
                    SyncRotation = 0,
                    VacuumRequested = tickInput.VacuumRequested,
                    CleanupActionRequested = tickInput.CleanupActionRequested,
                    RequestedCleanupActionSlot = tickInput.RequestedCleanupActionSlot,
                    InputSequence = tickInput.InputSequence,
                });
            }

            return frames;
        }

        private static byte[] SerializePayload(IReadOnlyList<ReplayTickInputElement> tickInputs)
        {
            using var payloadStream = new MemoryStream(Math.Max(0, tickInputs.Count) * TickInputByteSize);
            using (var writer = new BinaryWriter(payloadStream, System.Text.Encoding.UTF8, true))
            {
                for (int i = 0; i < tickInputs.Count; i++)
                {
                    var input = tickInputs[i];
                    writer.Write(input.Tick);
                    writer.Write(input.MoveAxis.x);
                    writer.Write(input.MoveAxis.y);
                    writer.Write(input.AimWorldXZ.x);
                    writer.Write(input.AimWorldXZ.y);
                    writer.Write(input.HasAimWorldPoint);
                    writer.Write(input.VacuumRequested);
                    writer.Write(input.CleanupActionRequested);
                    writer.Write(input.RequestedCleanupActionSlot);
                    writer.Write(input.InputSequence);
                }
            }

            return payloadStream.ToArray();
        }

        private static List<ReplayTickInputElement> DeserializePayload(byte[] payload, uint tickCount)
        {
            int count = tickCount > int.MaxValue ? int.MaxValue : (int)tickCount;
            var tickInputs = new List<ReplayTickInputElement>(count);

            using var payloadStream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(payloadStream);
            for (int i = 0; i < count; i++)
            {
                tickInputs.Add(new ReplayTickInputElement
                {
                    Tick = reader.ReadUInt32(),
                    MoveAxis = new float2(reader.ReadSingle(), reader.ReadSingle()),
                    AimWorldXZ = new float2(reader.ReadSingle(), reader.ReadSingle()),
                    HasAimWorldPoint = reader.ReadByte(),
                    VacuumRequested = reader.ReadByte(),
                    CleanupActionRequested = reader.ReadByte(),
                    RequestedCleanupActionSlot = reader.ReadByte(),
                    InputSequence = reader.ReadUInt32(),
                });
            }

            if (payloadStream.Position != payloadStream.Length)
                throw new InvalidDataException("Replay payload has trailing bytes after deserialize.");

            return tickInputs;
        }

        private static uint ComputeChecksum(byte[] payload)
        {
            uint hash = Fnv1aOffsetBasis;
            for (int i = 0; i < payload.Length; i++)
            {
                hash ^= payload[i];
                hash *= Fnv1aPrime;
            }

            return hash;
        }

        private static void TryDeleteTempFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (!File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch
            {
                // no-op
            }
        }
    }
}
