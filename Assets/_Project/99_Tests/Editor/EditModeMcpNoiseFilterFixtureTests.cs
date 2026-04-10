using NUnit.Framework;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class EditModeMcpNoiseFilterFixtureTests
    {
        [Test]
        public void IsAllowedMcpDisposedStreamNoise_ReturnsTrueForKnownMcpNoise()
        {
            const string condition =
                "<b><color=#cc3333>MCP-FOR-UNITY</color></b>: Client handler error: Cannot access a disposed object.\r\n"
                + "Object name: 'System.Net.Sockets.NetworkStream'.";

            bool allowed = EditModeMcpNoiseFilterUtility.IsAllowedMcpDisposedStreamNoise(condition);

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void IsAllowedMcpDisposedStreamNoise_ReturnsFalseForGeneralErrors()
        {
            const string condition = "NullReferenceException: unexpected runtime error";

            bool allowed = EditModeMcpNoiseFilterUtility.IsAllowedMcpDisposedStreamNoise(condition);

            Assert.That(allowed, Is.False);
        }

        [Test]
        public void IsFailingLogType_OnlyTreatsErrorAssertExceptionAsFailing()
        {
            Assert.That(EditModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Error), Is.True);
            Assert.That(EditModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Assert), Is.True);
            Assert.That(EditModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Exception), Is.True);
            Assert.That(EditModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Warning), Is.False);
            Assert.That(EditModeMcpNoiseFilterUtility.IsFailingLogType(LogType.Log), Is.False);
        }
    }
}
