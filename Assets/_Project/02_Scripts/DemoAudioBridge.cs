using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Demo audio read-only consumer.
    /// - DemoShell / ECS snapshot을 읽어 큐를 라우팅한다.
    /// - ECS writer 경계를 침범하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoAudioBridge : MonoBehaviour
    {
        [Header("References (Read-only)")]
        public DemoShellFlowController DemoShell;

        [Header("Audio Sources")]
        public AudioSource BgmSource;
        public AudioSource SfxSource;
        public AudioSource UiSource;

        [Header("BGM Clips")]
        public AudioClip TitleBgmClip;
        public AudioClip StageBgmClip;
        public AudioClip ResultBgmClip;
        public AudioClip CompleteBgmClip;

        [Header("UI Cues")]
        public AudioClip UiStartClip;
        public AudioClip UiSelectClip;
        public AudioClip UiBackClip;
        public AudioClip UiConfirmClip;

        [Header("Stage Cues")]
        public AudioClip StageEnterClip;
        public AudioClip StageClearClip;
        public AudioClip StageFailClip;
        public AudioClip DemoCompleteClip;

        [Header("Combat Cues")]
        public AudioClip HitClip;
        public AudioClip CollectClip;
        public AudioClip CleanupClip;

        [Header("Mixer (0..1)")]
        [Range(0f, 1f)] public float MasterVolume = 1f;
        [Range(0f, 1f)] public float BgmVolume = 1f;
        [Range(0f, 1f)] public float SfxVolume = 1f;
        [Range(0f, 1f)] public float UiVolume = 1f;

        [Header("Policy")]
        public bool AutoCreateMissingSources = true;
        public bool AutoAssignFallbackClips = true;
        public bool LogMissingAudioBinding = true;
        [Min(0f)] public float CollectCueCooldownSec = 0.05f;
        [Min(0f)] public float CleanupCueCooldownSec = 0.05f;
        [Range(0f, 1f)] public float BgmDuckGain = 0.65f;
        [Min(0f)] public float BgmDuckDurationSec = 0.15f;

        [Header("Debug")]
        public bool LogCue;

        private EntityManager _em;
        private EntityQuery _hudQuery;
        private EntityQuery _feedbackQuery;
        private bool _isBound;
        private bool _warnedBindFailure;

        private bool _hasLastScreen;
        private DemoShellScreenId _lastScreen;
        private AudioClip _activeBgmClip;

        private bool _hasHudBaseline;
        private int _lastTotalCollect;
        private int _lastTotalCleanup;
        private int _lastTotalHit;
        private uint _lastFeedbackVersion;

        private float _lastCollectCueAt = -999f;
        private float _lastCleanupCueAt = -999f;
        private float _bgmDuckUntilTime = -999f;
        private readonly HashSet<DemoAudioCueId> _missingCueWarnings = new HashSet<DemoAudioCueId>();
        private readonly List<AudioClip> _generatedFallbackClips = new List<AudioClip>(16);

        public DemoAudioCueId LastPlayedCue { get; private set; }
        public int PlayedCueCount { get; private set; }

        private void Reset()
        {
            DemoShell = GetComponent<DemoShellFlowController>();
        }

        private void OnEnable()
        {
            EnsureDemoShellReference();
            EnsureAudioSources();
            EnsureFallbackClips();
            LoadVolumePrefs();
            ApplyBusVolumes();
            PrimeScreenState();
        }

        private void Update()
        {
            EnsureDemoShellReference();
            EnsureAudioSources();
            EnsureFallbackClips();
            ProcessScreenTransitionCues();

            if (TryBind())
            {
                ProcessFeedbackCue();
                ProcessCombatTotalCues();
            }

            UpdateBgmForScreen();
            ApplyBusVolumes();
        }

        private void OnDisable()
        {
            SaveVolumePrefs();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _generatedFallbackClips.Count; i++)
            {
                var clip = _generatedFallbackClips[i];
                if (clip == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(clip);
                else
                    DestroyImmediate(clip);
            }

            _generatedFallbackClips.Clear();
            _missingCueWarnings.Clear();
        }

        public void SetBusVolume(DemoAudioBusId bus, float normalized)
        {
            float clamped = Mathf.Clamp01(normalized);
            switch (bus)
            {
                case DemoAudioBusId.Master:
                    MasterVolume = clamped;
                    break;
                case DemoAudioBusId.Bgm:
                    BgmVolume = clamped;
                    break;
                case DemoAudioBusId.Sfx:
                    SfxVolume = clamped;
                    break;
                case DemoAudioBusId.Ui:
                    UiVolume = clamped;
                    break;
            }

            SaveVolumePrefs();
            ApplyBusVolumes();
        }

        public float GetBusVolume(DemoAudioBusId bus)
        {
            return bus switch
            {
                DemoAudioBusId.Master => MasterVolume,
                DemoAudioBusId.Bgm => BgmVolume,
                DemoAudioBusId.Sfx => SfxVolume,
                DemoAudioBusId.Ui => UiVolume,
                _ => 1f,
            };
        }

        private void PrimeScreenState()
        {
            if (DemoShell == null)
                return;

            _hasLastScreen = true;
            _lastScreen = DemoShell.CurrentScreen;
            UpdateBgmForScreen();
        }

        private void ProcessScreenTransitionCues()
        {
            if (DemoShell == null)
                return;

            var currentScreen = DemoShell.CurrentScreen;
            var currentOutcome = DemoShell.CurrentStageOutcome;
            if (!_hasLastScreen)
            {
                _hasLastScreen = true;
                _lastScreen = currentScreen;
                return;
            }

            if (currentScreen != _lastScreen)
                EmitScreenTransitionCues(_lastScreen, currentScreen, currentOutcome);

            _lastScreen = currentScreen;
        }

        private void EmitScreenTransitionCues(
            DemoShellScreenId previousScreen,
            DemoShellScreenId currentScreen,
            DemoShellStageOutcomeId stageOutcome)
        {
            if (previousScreen == DemoShellScreenId.Title && currentScreen == DemoShellScreenId.Lobby)
            {
                TryPlayCue(DemoAudioCueId.UiStart);
                return;
            }

            if (previousScreen == DemoShellScreenId.Lobby && currentScreen == DemoShellScreenId.StagePlay)
            {
                TryPlayCue(DemoAudioCueId.UiSelect);
                TryPlayCue(DemoAudioCueId.StageEnter);
                return;
            }

            if (previousScreen == DemoShellScreenId.StagePlay && currentScreen == DemoShellScreenId.StageResult)
            {
                TryPlayCue(DemoAudioCueId.UiConfirm);
                TryPlayCue(stageOutcome == DemoShellStageOutcomeId.Clear
                    ? DemoAudioCueId.StageClear
                    : DemoAudioCueId.StageFail);
                return;
            }

            if (currentScreen == DemoShellScreenId.DemoComplete)
            {
                TryPlayCue(DemoAudioCueId.DemoComplete);
                return;
            }

            if (currentScreen == DemoShellScreenId.Lobby && previousScreen != DemoShellScreenId.Title)
            {
                TryPlayCue(DemoAudioCueId.UiBack);
            }
        }

        private void ProcessFeedbackCue()
        {
            var feedbackEntity = ResolveFirstEntity(_feedbackQuery);
            if (feedbackEntity == Entity.Null || !_em.HasComponent<PlayerUiFeedbackPresentationSnapshotComponent>(feedbackEntity))
                return;

            var feedback = _em.GetComponentData<PlayerUiFeedbackPresentationSnapshotComponent>(feedbackEntity);
            if (feedback.Version == 0u || feedback.Version == _lastFeedbackVersion)
                return;

            _lastFeedbackVersion = feedback.Version;
            if (feedback.Type != PlayerUiFeedbackEventType.PlayerHazardHit)
                return;

            TryPlayCue(DemoAudioCueId.Hit);
            TriggerBgmDuck();
        }

        private void ProcessCombatTotalCues()
        {
            var hudEntity = ResolveFirstEntity(_hudQuery);
            if (hudEntity == Entity.Null)
                return;

            var snapshot = _em.GetComponentData<PlayerHudSnapshotComponent>(hudEntity);
            if (!_hasHudBaseline)
            {
                _hasHudBaseline = true;
                _lastTotalCollect = Mathf.Max(0, snapshot.TotalCollectValue);
                _lastTotalCleanup = Mathf.Max(0, snapshot.TotalCleanupValue);
                _lastTotalHit = Mathf.Max(0, snapshot.TotalHitValue);
                return;
            }

            float now = Time.unscaledTime;
            int collectNow = Mathf.Max(0, snapshot.TotalCollectValue);
            int cleanupNow = Mathf.Max(0, snapshot.TotalCleanupValue);
            int hitNow = Mathf.Max(0, snapshot.TotalHitValue);

            int collectDelta = Mathf.Max(0, collectNow - _lastTotalCollect);
            int cleanupDelta = Mathf.Max(0, cleanupNow - _lastTotalCleanup);
            int hitDelta = Mathf.Max(0, hitNow - _lastTotalHit);

            if (collectDelta > 0 && now - _lastCollectCueAt >= Mathf.Max(0f, CollectCueCooldownSec))
            {
                TryPlayCue(DemoAudioCueId.Collect);
                _lastCollectCueAt = now;
            }

            if (cleanupDelta > 0 && now - _lastCleanupCueAt >= Mathf.Max(0f, CleanupCueCooldownSec))
            {
                TryPlayCue(DemoAudioCueId.Cleanup);
                _lastCleanupCueAt = now;
            }

            if (hitDelta > 0)
            {
                TriggerBgmDuck();
            }

            _lastTotalCollect = collectNow;
            _lastTotalCleanup = cleanupNow;
            _lastTotalHit = hitNow;
        }

        private void UpdateBgmForScreen()
        {
            if (BgmSource == null)
                return;

            AudioClip targetClip = null;
            var screen = DemoShell != null ? DemoShell.CurrentScreen : DemoShellScreenId.Title;
            switch (screen)
            {
                case DemoShellScreenId.Title:
                case DemoShellScreenId.Lobby:
                    targetClip = TitleBgmClip;
                    break;
                case DemoShellScreenId.StagePlay:
                    targetClip = StageBgmClip;
                    break;
                case DemoShellScreenId.StageResult:
                    targetClip = ResultBgmClip;
                    break;
                case DemoShellScreenId.DemoComplete:
                    targetClip = CompleteBgmClip;
                    break;
            }

            if (_activeBgmClip == targetClip)
                return;

            _activeBgmClip = targetClip;
            if (targetClip == null)
            {
                if (BgmSource.isPlaying)
                    BgmSource.Stop();
                BgmSource.clip = null;
                return;
            }

            BgmSource.loop = true;
            BgmSource.clip = targetClip;
            BgmSource.Play();
        }

        private void TriggerBgmDuck()
        {
            _bgmDuckUntilTime = Mathf.Max(_bgmDuckUntilTime, Time.unscaledTime + Mathf.Max(0f, BgmDuckDurationSec));
        }

        private bool TryPlayCue(DemoAudioCueId cueId)
        {
            if (!TryResolveCue(cueId, out var source, out var clip))
            {
                if (LogMissingAudioBinding && _missingCueWarnings.Add(cueId))
                    Debug.LogWarning($"[DemoAudioBridge] Missing source/clip binding for cue={cueId}");
                return false;
            }

            source.PlayOneShot(clip);
            LastPlayedCue = cueId;
            PlayedCueCount++;
            if (LogCue)
                Debug.Log($"[DemoAudioBridge] cue={cueId}");
            return true;
        }

        private bool TryResolveCue(DemoAudioCueId cueId, out AudioSource source, out AudioClip clip)
        {
            source = null;
            clip = null;
            switch (cueId)
            {
                case DemoAudioCueId.UiStart:
                    source = UiSource;
                    clip = UiStartClip;
                    break;
                case DemoAudioCueId.UiSelect:
                    source = UiSource;
                    clip = UiSelectClip;
                    break;
                case DemoAudioCueId.UiBack:
                    source = UiSource;
                    clip = UiBackClip;
                    break;
                case DemoAudioCueId.UiConfirm:
                    source = UiSource;
                    clip = UiConfirmClip;
                    break;
                case DemoAudioCueId.StageEnter:
                    source = SfxSource;
                    clip = StageEnterClip;
                    break;
                case DemoAudioCueId.StageClear:
                    source = SfxSource;
                    clip = StageClearClip;
                    break;
                case DemoAudioCueId.StageFail:
                    source = SfxSource;
                    clip = StageFailClip;
                    break;
                case DemoAudioCueId.DemoComplete:
                    source = SfxSource;
                    clip = DemoCompleteClip;
                    break;
                case DemoAudioCueId.Hit:
                    source = SfxSource;
                    clip = HitClip;
                    break;
                case DemoAudioCueId.Collect:
                    source = SfxSource;
                    clip = CollectClip;
                    break;
                case DemoAudioCueId.Cleanup:
                    source = SfxSource;
                    clip = CleanupClip;
                    break;
            }

            return source != null && clip != null;
        }

        private void ApplyBusVolumes()
        {
            float master = Mathf.Clamp01(MasterVolume);
            float bgm = Mathf.Clamp01(BgmVolume);
            float sfx = Mathf.Clamp01(SfxVolume);
            float ui = Mathf.Clamp01(UiVolume);
            float duck = Time.unscaledTime < _bgmDuckUntilTime ? Mathf.Clamp01(BgmDuckGain) : 1f;

            if (BgmSource != null)
                BgmSource.volume = master * bgm * duck;
            if (SfxSource != null)
                SfxSource.volume = master * sfx;
            if (UiSource != null)
                UiSource.volume = master * ui;
        }

        private void LoadVolumePrefs()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(DemoAudioPrefsKeys.MasterVolume, MasterVolume));
            BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(DemoAudioPrefsKeys.BgmVolume, BgmVolume));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(DemoAudioPrefsKeys.SfxVolume, SfxVolume));
            UiVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(DemoAudioPrefsKeys.UiVolume, UiVolume));
        }

        private void SaveVolumePrefs()
        {
            PlayerPrefs.SetFloat(DemoAudioPrefsKeys.MasterVolume, Mathf.Clamp01(MasterVolume));
            PlayerPrefs.SetFloat(DemoAudioPrefsKeys.BgmVolume, Mathf.Clamp01(BgmVolume));
            PlayerPrefs.SetFloat(DemoAudioPrefsKeys.SfxVolume, Mathf.Clamp01(SfxVolume));
            PlayerPrefs.SetFloat(DemoAudioPrefsKeys.UiVolume, Mathf.Clamp01(UiVolume));
            PlayerPrefs.Save();
        }

        private bool TryBind()
        {
            if (_isBound)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            _em = world.EntityManager;
            _hudQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<PlayerHudSnapshotComponent>());
            _feedbackQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<PlayerUiFeedbackPresentationSnapshotComponent>());
            _isBound = true;
            _warnedBindFailure = false;
            return true;
        }

        private Entity ResolveFirstEntity(EntityQuery query)
        {
            int count = query.CalculateEntityCount();
            if (count <= 0)
                return Entity.Null;
            if (count == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        private void EnsureDemoShellReference()
        {
            if (DemoShell != null)
                return;

            DemoShell = GetComponent<DemoShellFlowController>();
            if (DemoShell != null)
                return;

#if UNITY_2023_1_OR_NEWER
            DemoShell = FindFirstObjectByType<DemoShellFlowController>();
#else
            DemoShell = FindObjectOfType<DemoShellFlowController>();
#endif

            if (DemoShell != null || _warnedBindFailure)
                return;

            _warnedBindFailure = true;
            Debug.LogWarning("[DemoAudioBridge] DemoShellFlowController was not found. Screen transition cues will be limited.");
        }

        private void EnsureAudioSources()
        {
            BindUnassignedLocalSources();
            BgmSource = EnsureAudioSource(BgmSource, "DemoAudio_BGM", createIfMissing: AutoCreateMissingSources);
            SfxSource = EnsureAudioSource(SfxSource, "DemoAudio_SFX", createIfMissing: AutoCreateMissingSources);
            UiSource = EnsureAudioSource(UiSource, "DemoAudio_UI", createIfMissing: AutoCreateMissingSources);

            ConfigureAudioSource(BgmSource, loop: true, priority: 128);
            ConfigureAudioSource(SfxSource, loop: false, priority: 96);
            ConfigureAudioSource(UiSource, loop: false, priority: 64);
        }

        private void BindUnassignedLocalSources()
        {
            if (BgmSource != null && SfxSource != null && UiSource != null)
                return;

            var localSources = GetComponents<AudioSource>();
            if (localSources == null || localSources.Length == 0)
                return;

            for (int i = 0; i < localSources.Length; i++)
            {
                var source = localSources[i];
                if (source == null)
                    continue;
                if (source == BgmSource || source == SfxSource || source == UiSource)
                    continue;

                if (BgmSource == null)
                {
                    BgmSource = source;
                    continue;
                }

                if (SfxSource == null)
                {
                    SfxSource = source;
                    continue;
                }

                if (UiSource == null)
                {
                    UiSource = source;
                    break;
                }
            }
        }

        private void EnsureFallbackClips()
        {
            if (!AutoAssignFallbackClips)
                return;

            AssignFallbackClip(ref TitleBgmClip, "demo_bgm_title_fallback", frequencyHz: 176f, durationSec: 1.8f, amplitude: 0.04f);
            AssignFallbackClip(ref StageBgmClip, "demo_bgm_stage_fallback", frequencyHz: 196f, durationSec: 1.6f, amplitude: 0.05f);
            AssignFallbackClip(ref ResultBgmClip, "demo_bgm_result_fallback", frequencyHz: 156f, durationSec: 1.6f, amplitude: 0.04f);
            AssignFallbackClip(ref CompleteBgmClip, "demo_bgm_complete_fallback", frequencyHz: 220f, durationSec: 1.8f, amplitude: 0.05f);

            AssignFallbackClip(ref UiStartClip, "demo_ui_start_fallback", frequencyHz: 880f, durationSec: 0.08f, amplitude: 0.2f);
            AssignFallbackClip(ref UiSelectClip, "demo_ui_select_fallback", frequencyHz: 740f, durationSec: 0.07f, amplitude: 0.18f);
            AssignFallbackClip(ref UiBackClip, "demo_ui_back_fallback", frequencyHz: 620f, durationSec: 0.07f, amplitude: 0.18f);
            AssignFallbackClip(ref UiConfirmClip, "demo_ui_confirm_fallback", frequencyHz: 960f, durationSec: 0.09f, amplitude: 0.2f);

            AssignFallbackClip(ref StageEnterClip, "demo_stage_enter_fallback", frequencyHz: 320f, durationSec: 0.12f, amplitude: 0.15f);
            AssignFallbackClip(ref StageClearClip, "demo_stage_clear_fallback", frequencyHz: 520f, durationSec: 0.14f, amplitude: 0.16f);
            AssignFallbackClip(ref StageFailClip, "demo_stage_fail_fallback", frequencyHz: 180f, durationSec: 0.14f, amplitude: 0.16f);
            AssignFallbackClip(ref DemoCompleteClip, "demo_complete_fallback", frequencyHz: 460f, durationSec: 0.18f, amplitude: 0.18f);

            AssignFallbackClip(ref HitClip, "demo_hit_fallback", frequencyHz: 120f, durationSec: 0.08f, amplitude: 0.2f);
            AssignFallbackClip(ref CollectClip, "demo_collect_fallback", frequencyHz: 680f, durationSec: 0.06f, amplitude: 0.16f);
            AssignFallbackClip(ref CleanupClip, "demo_cleanup_fallback", frequencyHz: 420f, durationSec: 0.06f, amplitude: 0.16f);
        }

        private AudioSource EnsureAudioSource(AudioSource source, string childName, bool createIfMissing)
        {
            if (source != null)
                return source;
            if (!createIfMissing)
                return null;

            var child = transform.Find(childName);
            if (child == null)
            {
                var childGo = new GameObject(childName);
                childGo.transform.SetParent(transform, false);
                child = childGo.transform;
            }

            source = child.GetComponent<AudioSource>();
            if (source == null)
                source = child.gameObject.AddComponent<AudioSource>();
            return source;
        }

        private static void ConfigureAudioSource(AudioSource source, bool loop, int priority)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.loop = loop;
            source.priority = Mathf.Clamp(priority, 0, 256);
        }

        private void AssignFallbackClip(ref AudioClip slot, string clipName, float frequencyHz, float durationSec, float amplitude)
        {
            if (slot != null)
                return;

            slot = CreateToneClip(clipName, frequencyHz, durationSec, amplitude);
            if (slot != null)
                _generatedFallbackClips.Add(slot);
        }

        private static AudioClip CreateToneClip(string clipName, float frequencyHz, float durationSec, float amplitude)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.Max(256, Mathf.RoundToInt(sampleRate * Mathf.Max(0.05f, durationSec)));
            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            if (clip == null)
                return null;

            var samples = new float[sampleCount];
            float angularFreq = 2f * Mathf.PI * Mathf.Max(20f, frequencyHz);
            float gain = Mathf.Clamp(amplitude, 0f, 0.35f);
            int fadeSamples = Mathf.Min(sampleCount / 8, 128);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f;
                if (i < fadeSamples)
                    envelope = i / (float)fadeSamples;
                else if (i >= sampleCount - fadeSamples)
                    envelope = (sampleCount - 1 - i) / (float)fadeSamples;

                samples[i] = Mathf.Sin(angularFreq * t) * gain * Mathf.Clamp01(envelope);
            }

            clip.SetData(samples, 0);
            return clip;
        }
    }
}
