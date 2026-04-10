using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    [SetUpFixture]
    public sealed class PlayModeMcpNoiseFilterFixture
    {
        private static readonly ConcurrentQueue<UnexpectedLogEntry> UnexpectedLogs = new();
        private static bool _previousIgnoreFailingMessages;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            while (UnexpectedLogs.TryDequeue(out _))
            {
            }

            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceivedThreaded += HandleLogMessage;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Application.logMessageReceivedThreaded -= HandleLogMessage;
            LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;

            var unexpectedLogs = UnexpectedLogs.ToArray();
            if (unexpectedLogs.Length == 0)
                return;

            var builder = new StringBuilder();
            builder.AppendLine($"Unexpected failing logs captured during PlayMode suite: {unexpectedLogs.Length}");

            foreach (var entry in unexpectedLogs.Take(10))
            {
                builder.AppendLine($"[{entry.LogType}] {entry.Condition}");
                if (!string.IsNullOrWhiteSpace(entry.StackTrace))
                    builder.AppendLine(entry.StackTrace);
                builder.AppendLine();
            }

            Assert.Fail(builder.ToString().TrimEnd());
        }

        private static void HandleLogMessage(string condition, string stackTrace, LogType logType)
        {
            if (!PlayModeMcpNoiseFilterUtility.IsFailingLogType(logType))
                return;

            if (PlayModeMcpNoiseFilterUtility.IsAllowedMcpNoise(condition))
                return;

            UnexpectedLogs.Enqueue(new UnexpectedLogEntry(condition, stackTrace, logType));
        }

        private readonly struct UnexpectedLogEntry
        {
            public UnexpectedLogEntry(string condition, string stackTrace, LogType logType)
            {
                Condition = condition;
                StackTrace = stackTrace;
                LogType = logType;
            }

            public string Condition { get; }
            public string StackTrace { get; }
            public LogType LogType { get; }
        }
    }

    internal static class PlayModeMcpNoiseFilterUtility
    {
        private const string MpcPrefix = "MCP-FOR-UNITY";

        public static bool IsFailingLogType(LogType logType)
        {
            return logType is LogType.Error or LogType.Assert or LogType.Exception;
        }

        public static bool IsAllowedMcpNoise(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            return condition.Contains(MpcPrefix);
        }
    }
}
