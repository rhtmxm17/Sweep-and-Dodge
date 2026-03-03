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

    public static class ReplayFilePersistence
    {
        public const uint Magic = 0x52504C59u; // "RPLY"
        public const uint CurrentSchemaVersion = 1u;

        private const uint Fnv1aOffsetBasis = 2166136261u;
        private const uint Fnv1aPrime = 16777619u;
        private const int HeaderByteSize = 24;
        private const int FrameByteSize = 57;

        public static bool TrySave(
            string path,
            uint runSeed,
            IReadOnlyList<ReplayInputFrameBufferElement> frames,
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

            if (frames == null)
            {
                reason = ReplayIoError.InvalidState;
                message = "Save failed: replay frames are null.";
                return false;
            }

            string tempPath = $"{path}.tmp";
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var snapshotFrames = CreateFrameSnapshot(frames);
                byte[] payload = SerializePayload(snapshotFrames);
                uint checksum = ComputeChecksum(payload);
                uint normalizedRunSeed = runSeed > 0u ? runSeed : 1u;

                using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                {
                    writer.Write(Magic);
                    writer.Write(CurrentSchemaVersion);
                    writer.Write((uint)snapshotFrames.Count);
                    writer.Write(normalizedRunSeed);
                    writer.Write((uint)payload.Length);
                    writer.Write(checksum);
                    writer.Write(payload);
                    writer.Flush();
                }

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }

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

        public static bool TryLoad(
            string path,
            out uint runSeed,
            out List<ReplayInputFrameBufferElement> frames,
            out ReplayIoError reason,
            out string message)
        {
            runSeed = 1u;
            frames = new List<ReplayInputFrameBufferElement>(0);
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
                uint frameCount = reader.ReadUInt32();
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

                long expectedPayloadByteLength = (long)frameCount * FrameByteSize;
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

                frames = DeserializePayload(payload, frameCount);
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

        public static bool TryLoadAndStagePlayback(string path, out ReplayIoError reason, out string message)
        {
            if (!TryLoad(path, out uint runSeed, out List<ReplayInputFrameBufferElement> frames, out reason, out message))
                return false;

            ReplaySessionStaging.StagePlayback(frames, runSeed);
            return true;
        }

        private static List<ReplayInputFrameBufferElement> CreateFrameSnapshot(IReadOnlyList<ReplayInputFrameBufferElement> source)
        {
            int count = source.Count;
            var snapshot = new List<ReplayInputFrameBufferElement>(count);
            for (int i = 0; i < count; i++)
                snapshot.Add(source[i]);
            return snapshot;
        }

        private static byte[] SerializePayload(IReadOnlyList<ReplayInputFrameBufferElement> frames)
        {
            using var payloadStream = new MemoryStream(Math.Max(0, frames.Count) * FrameByteSize);
            using (var writer = new BinaryWriter(payloadStream, System.Text.Encoding.UTF8, true))
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    var frame = frames[i];
                    writer.Write(frame.Frame);
                    writer.Write(frame.MoveAxis.x);
                    writer.Write(frame.MoveAxis.y);
                    writer.Write(frame.AimWorldXZ.x);
                    writer.Write(frame.AimWorldXZ.y);
                    writer.Write(frame.HasAimWorldPoint);
                    writer.Write(frame.Position.x);
                    writer.Write(frame.Position.y);
                    writer.Write(frame.Position.z);
                    writer.Write(frame.Rotation.value.x);
                    writer.Write(frame.Rotation.value.y);
                    writer.Write(frame.Rotation.value.z);
                    writer.Write(frame.Rotation.value.w);
                    writer.Write(frame.SyncRotation);
                    writer.Write(frame.VacuumRequested);
                    writer.Write(frame.CleanupActionRequested);
                    writer.Write(frame.RequestedCleanupActionSlot);
                    writer.Write(frame.InputSequence);
                }
            }

            return payloadStream.ToArray();
        }

        private static List<ReplayInputFrameBufferElement> DeserializePayload(byte[] payload, uint frameCount)
        {
            int count = frameCount > int.MaxValue ? int.MaxValue : (int)frameCount;
            var frames = new List<ReplayInputFrameBufferElement>(count);

            using var payloadStream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(payloadStream);
            for (int i = 0; i < count; i++)
            {
                uint frameValue = reader.ReadUInt32();
                float moveAxisX = reader.ReadSingle();
                float moveAxisY = reader.ReadSingle();
                float aimX = reader.ReadSingle();
                float aimY = reader.ReadSingle();
                byte hasAim = reader.ReadByte();
                float positionX = reader.ReadSingle();
                float positionY = reader.ReadSingle();
                float positionZ = reader.ReadSingle();
                float rotationX = reader.ReadSingle();
                float rotationY = reader.ReadSingle();
                float rotationZ = reader.ReadSingle();
                float rotationW = reader.ReadSingle();
                byte syncRotation = reader.ReadByte();
                byte vacuumRequested = reader.ReadByte();
                byte cleanupRequested = reader.ReadByte();
                byte requestedCleanupSlot = reader.ReadByte();
                uint inputSequence = reader.ReadUInt32();

                frames.Add(new ReplayInputFrameBufferElement
                {
                    Frame = frameValue,
                    MoveAxis = new float2(moveAxisX, moveAxisY),
                    AimWorldXZ = new float2(aimX, aimY),
                    HasAimWorldPoint = hasAim,
                    Position = new float3(positionX, positionY, positionZ),
                    Rotation = new quaternion(rotationX, rotationY, rotationZ, rotationW),
                    SyncRotation = syncRotation,
                    VacuumRequested = vacuumRequested,
                    CleanupActionRequested = cleanupRequested,
                    RequestedCleanupActionSlot = requestedCleanupSlot,
                    InputSequence = inputSequence,
                });
            }

            if (payloadStream.Position != payloadStream.Length)
                throw new InvalidDataException("Replay payload has trailing bytes after deserialize.");

            return frames;
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
