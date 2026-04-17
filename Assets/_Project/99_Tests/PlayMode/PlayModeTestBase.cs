using NUnit.Framework;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    /// <summary>
    /// PlayMode 테스트 공통 베이스.
    /// Unity Test Framework는 테스트마다 새 <c>LogScope</c>를 생성하며 기본값은
    /// <c>IgnoreFailingMessages = false</c>이다. SetUpFixture의 OneTimeSetUp에서
    /// ignoreFailingMessages를 설정해도 테스트 레벨 LogScope에는 반영되지 않으므로,
    /// [SetUp]으로 테스트마다 직접 설정해야 한다.
    ///
    /// MCP 노이즈에 의한 개별 테스트 실패를 억제하고, 예상치 못한 로그는
    /// <see cref="PlayModeMcpNoiseFilterFixture"/>의 OneTimeTearDown에서 스위트 레벨로 보고한다.
    /// </summary>
    public abstract class PlayModeTestBase
    {
        [SetUp]
        public virtual void SuppressPerTestLogFailures()
        {
            LogAssert.ignoreFailingMessages = true;
        }
    }
}
