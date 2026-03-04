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
