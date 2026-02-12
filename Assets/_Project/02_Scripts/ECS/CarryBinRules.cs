using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public static class CarryBinRules
    {
        public static int AddLoadClamped(int load, int add, int capacity)
        {
            int safeCapacity = math.max(0, capacity);
            int safeLoad = math.clamp(load, 0, safeCapacity);
            int safeAdd = math.max(0, add);
            return math.min(safeCapacity, safeLoad + safeAdd);
        }

        public static int ComputeHazardLoss(int load, float lossFrac, int lossMin, int lossMax)
        {
            int safeLoad = math.max(0, load);
            float safeFrac = math.clamp(lossFrac, 0f, 1f);
            int safeMin = math.max(0, lossMin);
            int safeMax = math.max(0, lossMax);

            int rawLoss = (int)math.floor(safeLoad * safeFrac);
            int clampedLoss = math.clamp(rawLoss, safeMin, safeMax);
            return math.min(safeLoad, math.max(0, clampedLoss));
        }

        public static void TickPenaltyTimers(ref PlayerHazardPenaltyStateComponent penalty, float dt)
        {
            if (penalty.IFrameTimer > 0f)
                penalty.IFrameTimer = math.max(0f, penalty.IFrameTimer - dt);
            if (penalty.VacuumLockTimer > 0f)
                penalty.VacuumLockTimer = math.max(0f, penalty.VacuumLockTimer - dt);
        }

        public static bool IsHazardHitBlocked(in PlayerHazardPenaltyStateComponent penalty)
        {
            return penalty.IFrameTimer > 0f;
        }

        public static bool ApplyVacuumLock(ref VacuumBurstComponent vacuum, in PlayerHazardPenaltyStateComponent penalty)
        {
            if (penalty.VacuumLockTimer <= 0f)
                return false;

            vacuum.IsActive = 0;
            vacuum.ActiveTimer = 0f;
            vacuum.CaptureActiveTimer = 0f;
            vacuum.ActivateRequested = 0;
            return true;
        }
    }
}
