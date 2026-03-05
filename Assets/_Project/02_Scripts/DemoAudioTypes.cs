namespace SweepNDodge.DotsBullets
{
    public enum DemoAudioBusId : byte
    {
        Master = 0,
        Bgm = 1,
        Sfx = 2,
        Ui = 3,
    }

    public enum DemoAudioCueId : byte
    {
        None = 0,
        UiStart = 1,
        UiSelect = 2,
        UiBack = 3,
        UiConfirm = 4,
        StageEnter = 5,
        StageClear = 6,
        StageFail = 7,
        DemoComplete = 8,
        Hit = 9,
        Collect = 10,
        Cleanup = 11,
    }
}
