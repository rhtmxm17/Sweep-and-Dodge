namespace SweepNDodge.DotsBullets
{
    public readonly struct DemoShellStartupRequest
    {
        public DemoShellStartupRequest(DemoShellScreenId screen, int stageIndex)
        {
            Screen = screen;
            StageIndex = stageIndex;
        }

        public DemoShellScreenId Screen { get; }
        public int StageIndex { get; }
    }

    public static class DemoShellSessionStaging
    {
        private static bool _hasPendingRequest;
        private static DemoShellStartupRequest _request;
        private static bool _hasSessionMetrics;
        private static DemoShellSessionMetrics _sessionMetrics;

        public static bool IsStartupPending => _hasPendingRequest;

        public static void StageLobby()
        {
            _request = new DemoShellStartupRequest(DemoShellScreenId.Lobby, -1);
            _hasPendingRequest = true;
        }

        public static void StageStagePlay(int stageIndex)
        {
            _request = new DemoShellStartupRequest(DemoShellScreenId.StagePlay, stageIndex);
            _hasPendingRequest = true;
        }

        public static void ResetSessionMetrics()
        {
            _sessionMetrics = default;
            _hasSessionMetrics = true;
        }

        public static void AccumulateSuccessfulStage(in DemoShellStageResultMetrics result)
        {
            if (result.Outcome != DemoShellStageOutcomeId.Clear)
                return;

            if (!_hasSessionMetrics)
                _sessionMetrics = default;

            _sessionMetrics.TotalElapsedSec += result.ElapsedSec > 0f ? result.ElapsedSec : 0f;
            _sessionMetrics.TotalCollectValue += result.CollectValue > 0 ? result.CollectValue : 0;
            _sessionMetrics.TotalCleanupValue += result.CleanupValue > 0 ? result.CleanupValue : 0;
            _sessionMetrics.TotalHitValue += result.HitValue > 0 ? result.HitValue : 0;
            _sessionMetrics.ClearedStageCount += 1;
            _hasSessionMetrics = true;
        }

        public static bool TryGetSessionMetrics(out DemoShellSessionMetrics metrics)
        {
            metrics = default;
            if (!_hasSessionMetrics)
                return false;

            metrics = _sessionMetrics;
            return true;
        }

        public static bool TryConsume(out DemoShellStartupRequest request)
        {
            request = default;
            if (!_hasPendingRequest)
                return false;

            request = _request;
            _request = default;
            _hasPendingRequest = false;
            return true;
        }
    }
}
