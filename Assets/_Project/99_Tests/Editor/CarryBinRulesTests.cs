using NUnit.Framework;

namespace SweepNDodge.DotsBullets.Tests
{
    public class CarryBinRulesTests
    {
        [Test]
        public void AddLoadClamped_CapsAtCapacity()
        {
            int result = CarryBinRules.AddLoadClamped(load: 95, add: 20, capacity: 100);
            Assert.That(result, Is.EqualTo(100));
        }

        [Test]
        public void IsFull_ReturnsTrueWhenLoadReachesCapacity()
        {
            var carry = new PlayerCarryBinComponent
            {
                Load = 300,
                Capacity = 300
            };

            Assert.That(CarryBinRules.IsFull(in carry), Is.True);
        }

        [Test]
        public void ComputeHazardLoss_UsesConfiguredFormulaBounds()
        {
            // 기본값: frac=0.15, min=5, max=30
            int lowLoadLoss = CarryBinRules.ComputeHazardLoss(load: 20, lossFrac: 0.15f, lossMin: 5, lossMax: 30);   // floor(3) -> min 5
            int midLoadLoss = CarryBinRules.ComputeHazardLoss(load: 100, lossFrac: 0.15f, lossMin: 5, lossMax: 30); // floor(15)
            int highLoadLoss = CarryBinRules.ComputeHazardLoss(load: 400, lossFrac: 0.15f, lossMin: 5, lossMax: 30); // floor(60) -> max 30

            Assert.That(lowLoadLoss, Is.EqualTo(5));
            Assert.That(midLoadLoss, Is.EqualTo(15));
            Assert.That(highLoadLoss, Is.EqualTo(30));
        }

        [Test]
        public void IsHazardHitBlocked_ReturnsTrueWhenIFrameActive()
        {
            var state = new PlayerHazardPenaltyStateComponent
            {
                IFrameTimer = 0.25f,
                VacuumLockTimer = 0f
            };

            Assert.That(CarryBinRules.IsHazardHitBlocked(in state), Is.True);
        }

        [Test]
        public void ApplyVacuumLock_DisablesVacuumWhileLocked()
        {
            var vacuum = new VacuumBurstComponent
            {
                IsActive = 1,
                ActiveTimer = 0.2f,
                CaptureActiveTimer = 0.1f,
                ActivateRequested = 1
            };

            var penalty = new PlayerHazardPenaltyStateComponent
            {
                IFrameTimer = 0f,
                VacuumLockTimer = 0.3f
            };

            bool locked = CarryBinRules.ApplyVacuumLock(ref vacuum, in penalty);

            Assert.That(locked, Is.True);
            Assert.That(vacuum.IsActive, Is.EqualTo(0));
            Assert.That(vacuum.ActiveTimer, Is.EqualTo(0f));
            Assert.That(vacuum.CaptureActiveTimer, Is.EqualTo(0f));
            Assert.That(vacuum.ActivateRequested, Is.EqualTo(0));
        }
    }
}
