using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DemoAudioBridgeTests
    {
        [SetUp]
        public void SetUp()
        {
            ClearVolumePrefs();
        }

        [TearDown]
        public void TearDown()
        {
            ClearVolumePrefs();
        }

        [Test]
        public void SetBusVolume_ClampsAndReadsBack()
        {
            var go = new GameObject("DemoAudioBridge_Test");
            try
            {
                var bridge = go.AddComponent<DemoAudioBridge>();
                bridge.SetBusVolume(DemoAudioBusId.Master, 2f);
                bridge.SetBusVolume(DemoAudioBusId.Bgm, -1f);
                bridge.SetBusVolume(DemoAudioBusId.Sfx, 0.25f);
                bridge.SetBusVolume(DemoAudioBusId.Ui, 0.75f);

                Assert.That(bridge.GetBusVolume(DemoAudioBusId.Master), Is.EqualTo(1f).Within(1e-6f));
                Assert.That(bridge.GetBusVolume(DemoAudioBusId.Bgm), Is.EqualTo(0f).Within(1e-6f));
                Assert.That(bridge.GetBusVolume(DemoAudioBusId.Sfx), Is.EqualTo(0.25f).Within(1e-6f));
                Assert.That(bridge.GetBusVolume(DemoAudioBusId.Ui), Is.EqualTo(0.75f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetBusVolume_AppliesToAudioSourcesImmediately()
        {
            var go = new GameObject("DemoAudioBridge_Test");
            var bgmSource = go.AddComponent<AudioSource>();
            var sfxSource = go.AddComponent<AudioSource>();
            var uiSource = go.AddComponent<AudioSource>();
            try
            {
                var bridge = go.AddComponent<DemoAudioBridge>();
                bridge.BgmSource = bgmSource;
                bridge.SfxSource = sfxSource;
                bridge.UiSource = uiSource;
                bridge.BgmDuckDurationSec = 0f;

                bridge.SetBusVolume(DemoAudioBusId.Master, 0.5f);
                bridge.SetBusVolume(DemoAudioBusId.Bgm, 0.4f);
                bridge.SetBusVolume(DemoAudioBusId.Sfx, 0.6f);
                bridge.SetBusVolume(DemoAudioBusId.Ui, 0.7f);

                Assert.That(bridge.BgmSource.volume, Is.EqualTo(0.2f).Within(1e-4f));
                Assert.That(bridge.SfxSource.volume, Is.EqualTo(0.3f).Within(1e-4f));
                Assert.That(bridge.UiSource.volume, Is.EqualTo(0.35f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnsureAudioSources_AutoCreatesMissingSources_WithDefaultPolicy()
        {
            var go = new GameObject("DemoAudioBridge_Test");
            try
            {
                var bridge = go.AddComponent<DemoAudioBridge>();
                bridge.AutoCreateMissingSources = true;
                InvokePrivate(bridge, "EnsureAudioSources");

                Assert.That(bridge.BgmSource, Is.Not.Null);
                Assert.That(bridge.SfxSource, Is.Not.Null);
                Assert.That(bridge.UiSource, Is.Not.Null);

                Assert.That(bridge.BgmSource.loop, Is.True);
                Assert.That(bridge.SfxSource.loop, Is.False);
                Assert.That(bridge.UiSource.loop, Is.False);

                Assert.That(bridge.BgmSource.playOnAwake, Is.False);
                Assert.That(bridge.SfxSource.playOnAwake, Is.False);
                Assert.That(bridge.UiSource.playOnAwake, Is.False);
                Assert.That(bridge.BgmSource.spatialBlend, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(bridge.SfxSource.spatialBlend, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(bridge.UiSource.spatialBlend, Is.EqualTo(0f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnsureFallbackClips_AssignsFallbackClips_WithoutOverwritingExistingClip()
        {
            var go = new GameObject("DemoAudioBridge_Test");
            var customClip = AudioClip.Create("demo_audio_custom_clip", 64, 1, 44100, false);
            try
            {
                var bridge = go.AddComponent<DemoAudioBridge>();
                bridge.AutoAssignFallbackClips = true;
                bridge.UiStartClip = customClip;
                InvokePrivate(bridge, "EnsureFallbackClips");

                Assert.That(bridge.UiStartClip, Is.EqualTo(customClip), "Existing clip assignment must not be overwritten.");
                Assert.That(bridge.TitleBgmClip, Is.Not.Null);
                Assert.That(bridge.StageBgmClip, Is.Not.Null);
                Assert.That(bridge.ResultBgmClip, Is.Not.Null);
                Assert.That(bridge.CompleteBgmClip, Is.Not.Null);
                Assert.That(bridge.HitClip, Is.Not.Null);
                Assert.That(bridge.CollectClip, Is.Not.Null);
                Assert.That(bridge.CleanupClip, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(customClip);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ScreenTransition_TitleToLobby_PlaysUiStartCue()
        {
            var go = new GameObject("DemoAudioBridge_Test");
            var clip = AudioClip.Create("demo_audio_test_clip", 64, 1, 44100, false);
            try
            {
                var bridge = go.AddComponent<DemoAudioBridge>();
                bridge.UiSource = go.AddComponent<AudioSource>();
                bridge.SfxSource = go.AddComponent<AudioSource>();
                bridge.UiStartClip = clip;

                InvokeScreenTransition(
                    bridge,
                    DemoShellScreenId.Title,
                    DemoShellScreenId.Lobby,
                    DemoShellStageOutcomeId.Clear);

                Assert.That(bridge.LastPlayedCue, Is.EqualTo(DemoAudioCueId.UiStart));
                Assert.That(bridge.PlayedCueCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ScreenTransition_StagePlayToResultFail_PlaysStageFailCue()
        {
            var go = new GameObject("DemoAudioBridge_Test");
            var clip = AudioClip.Create("demo_audio_test_clip", 64, 1, 44100, false);
            try
            {
                var bridge = go.AddComponent<DemoAudioBridge>();
                bridge.UiSource = go.AddComponent<AudioSource>();
                bridge.SfxSource = go.AddComponent<AudioSource>();
                bridge.UiConfirmClip = clip;
                bridge.StageFailClip = clip;

                InvokeScreenTransition(
                    bridge,
                    DemoShellScreenId.StagePlay,
                    DemoShellScreenId.StageResult,
                    DemoShellStageOutcomeId.Fail);

                Assert.That(bridge.LastPlayedCue, Is.EqualTo(DemoAudioCueId.StageFail));
                Assert.That(bridge.PlayedCueCount, Is.EqualTo(2), "StageResult transition should emit UI confirm + result cue.");
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(go);
            }
        }

        private static void InvokeScreenTransition(
            DemoAudioBridge bridge,
            DemoShellScreenId previous,
            DemoShellScreenId current,
            DemoShellStageOutcomeId outcome)
        {
            var method = typeof(DemoAudioBridge).GetMethod(
                "EmitScreenTransitionCues",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "DemoAudioBridge.EmitScreenTransitionCues was not found.");
            method.Invoke(bridge, new object[] { previous, current, outcome });
        }

        private static void InvokePrivate(DemoAudioBridge bridge, string methodName)
        {
            var method = typeof(DemoAudioBridge).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"DemoAudioBridge.{methodName} was not found.");
            method.Invoke(bridge, null);
        }

        private static void ClearVolumePrefs()
        {
            for (int i = 0; i < DemoAudioPrefsKeys.AllVolumeKeys.Length; i++)
                PlayerPrefs.DeleteKey(DemoAudioPrefsKeys.AllVolumeKeys[i]);
            PlayerPrefs.Save();
        }
    }
}
