using NUnit.Framework;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class PlayModeMcpNoiseFilterFixtureTests
    {
        [Test]
        public void IsAllowedMcpDisposedStreamNoise_ReturnsTrueForKnownMcpNoise()
        {
            const string condition =
                "<b><color=#cc3333>MCP-FOR-UNITY</color></b>: Client handler error: Cannot access a disposed object.\r\n"
                + "Object name: 'System.Net.Sockets.NetworkStream'.";

            bool allowed = PlayModeMcpNoiseFilterUtility.IsAllowedMcpDisposedStreamNoise(condition);

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void IsAllowedMcpDisposedStreamNoise_ReturnsFalseForGeneralErrors()
        {
            const string condition = "InvalidOperationException: unexpected runtime error";

            bool allowed = PlayModeMcpNoiseFilterUtility.IsAllowedMcpDisposedStreamNoise(condition);

            Assert.That(allowed, Is.False);
        }

        [Test]
        public void IsFailingLogType_OnlyTreatsErrorAssertExceptionAsFailing()
        {
            Assert.That(PlayModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Error), Is.True);
            Assert.That(PlayModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Assert), Is.True);
            Assert.That(PlayModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Exception), Is.True);
            Assert.That(PlayModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Warning), Is.False);
            Assert.That(PlayModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Log), Is.False);
        }
    }
}
