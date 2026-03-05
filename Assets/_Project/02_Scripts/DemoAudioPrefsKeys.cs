namespace SweepNDodge.DotsBullets
{
    public static class DemoAudioPrefsKeys
    {
        public const string MasterVolume = "demo.audio.master";
        public const string BgmVolume = "demo.audio.bgm";
        public const string SfxVolume = "demo.audio.sfx";
        public const string UiVolume = "demo.audio.ui";

        public static readonly string[] AllVolumeKeys =
        {
            MasterVolume,
            BgmVolume,
            SfxVolume,
            UiVolume,
        };
    }
}
