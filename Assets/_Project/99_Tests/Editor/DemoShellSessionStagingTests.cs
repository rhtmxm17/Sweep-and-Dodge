using NUnit.Framework;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DemoShellSessionStagingTests
    {
        [SetUp]
        public void SetUp()
        {
            while (DemoShellSessionStaging.TryConsume(out _))
            {
            }

            DemoShellSessionStaging.ResetSessionMetrics();
        }

        [Test]
        public void SessionMetrics_AccumulatesOnlyClearOutcome()
        {
            DemoShellSessionStaging.AccumulateSuccessfulStage(new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Fail,
                ElapsedSec = 999f,
                CollectValue = 99,
                CleanupValue = 99,
                HitValue = 99,
            });

            DemoShellSessionStaging.AccumulateSuccessfulStage(new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 81.5f,
                CollectValue = 42,
                CleanupValue = 17,
                HitValue = 6,
            });

            Assert.That(DemoShellSessionStaging.TryGetSessionMetrics(out var metrics), Is.True);
            Assert.That(metrics.ClearedStageCount, Is.EqualTo(1));
            Assert.That(metrics.TotalElapsedSec, Is.EqualTo(81.5f).Within(1e-4f));
            Assert.That(metrics.TotalCollectValue, Is.EqualTo(42));
            Assert.That(metrics.TotalCleanupValue, Is.EqualTo(17));
            Assert.That(metrics.TotalHitValue, Is.EqualTo(6));
        }

        [Test]
        public void SessionMetrics_Reset_ClearsTotals()
        {
            DemoShellSessionStaging.AccumulateSuccessfulStage(new DemoShellStageResultMetrics
            {
                StageId = 2,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 33f,
                CollectValue = 10,
                CleanupValue = 20,
                HitValue = 30,
            });

            DemoShellSessionStaging.ResetSessionMetrics();

            Assert.That(DemoShellSessionStaging.TryGetSessionMetrics(out var metrics), Is.True);
            Assert.That(metrics.ClearedStageCount, Is.EqualTo(0));
            Assert.That(metrics.TotalElapsedSec, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(metrics.TotalCollectValue, Is.EqualTo(0));
            Assert.That(metrics.TotalCleanupValue, Is.EqualTo(0));
            Assert.That(metrics.TotalHitValue, Is.EqualTo(0));
        }
    }
}
