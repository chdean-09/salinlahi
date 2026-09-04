using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class AudioManager : Singleton<AudioManager>
{
    private enum BgmContext
    {
        None,
        Home,
        Gameplay
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip _chainLightningSfxClip;
    [SerializeField] private AudioClip _chainLightningZapSfxClip;
    [SerializeField] private AudioClip _menuButtonClickClip;
    [SerializeField] private AudioClip _menuExitButtonClickClip;
    [SerializeField] private AudioClip[] _baseHitClips;

    [Header("Context BGM")]
    [SerializeField] private AudioClip _homeScreenBgmClip;
    [SerializeField] private AudioClip _gameplayBgmClip;
    [SerializeField] private float _contextBgmFadeOutSeconds = 0.15f;
    [SerializeField] private float _contextBgmFadeInSeconds = 0.2f;

    [Header("Base Hit Variation")]
    [SerializeField, Min(0f)] private float _baseHitVolumeMin = 0.92f;
    [SerializeField, Min(0f)] private float _baseHitVolumeMax = 1f;
    [SerializeField, Min(0.1f)] private float _baseHitPitchMin = 0.96f;
    [SerializeField, Min(0.1f)] private float _baseHitPitchMax = 1.04f;

    [Header("SFX Playback Polish")]
    [SerializeField, Min(0f)] private float _trimSilenceThreshold = 0.0025f;
    [SerializeField, Min(0f)] private float _maxLeadingSilenceTrimSeconds = 0.12f;
    [Header("Pronunciation Playback Polish")]
    [SerializeField, Min(0f)] private float _pronunciationTrimSilenceThreshold = 0.01f;
    [SerializeField, Min(0f)] private float _maxPronunciationLeadingTrimSeconds = 0.6f;
    [Header("Pronunciation Modulation")]
    [SerializeField] private bool _enablePronunciationModulation = true;
    [SerializeField, Min(0.1f)] private float _pronunciationPitchMin = 0.96f;
    [SerializeField, Min(0.1f)] private float _pronunciationPitchMax = 1.04f;
    [SerializeField, Min(0f)] private float _pronunciationVolumeMin = 0.94f;
    [SerializeField, Min(0f)] private float _pronunciationVolumeMax = 1f;

    [Header("Pronunciation Ducking")]
    [Tooltip("Dip the music while a syllable plays. The pronunciation clip is the game's "
        + "phonological-loop mechanism, and after the loudness pass it still sits only ~2.5 dB "
        + "over the music bed -- too little for a syllable to read clearly.")]
    [SerializeField] private bool _duckBgmDuringPronunciation = true;

    [Tooltip("BGM level while ducked, as a fraction of its normal volume. 0.35 is about -9 dB.")]
    [SerializeField, Range(0.05f, 1f)] private float _pronunciationDuckLevel = 0.35f;

    [Tooltip("Seconds to dip in. Short, so the music is already down before the syllable lands.")]
    [SerializeField, Min(0f)] private float _pronunciationDuckFadeOutSeconds = 0.08f;

    [Tooltip("Seconds to hold the dip after the clip ends, before the music comes back.")]
    [SerializeField, Min(0f)] private float _pronunciationDuckHoldSeconds = 0.15f;

    [Tooltip("Seconds to bring the music back. Longer than the dip so the recovery is unobtrusive.")]
    [SerializeField, Min(0f)] private float _pronunciationDuckFadeInSeconds = 0.45f;

    [Header("Chain Lightning Mix")]
    [SerializeField] private bool _enablePerEnemyChainZap = true;
    [SerializeField, Min(0)] private int _maxChainZapOneShots = 3;
    [SerializeField, Min(0f)] private float _chainZapVolumeScale = 0.3f;
    [SerializeField, Min(0f)] private float _chainAudioStartDelayMin = 0.08f;
    [SerializeField, Min(0f)] private float _chainAudioStartDelayMax = 0.22f;
    [SerializeField, Min(0f)] private float _chainZapInterval = 0.06f;
    [SerializeField, Min(0f)] private float _chainZapIntervalJitter = 0.07f;

    private Coroutine _chainZapRoutine;
    private AudioSource _baseHitSfxSource;
    private AudioSource _pronunciationSfxSource;
    private readonly Dictionary<AudioClip, AudioClip> _trimmedClipCache = new();
    private readonly Dictionary<AudioClip, AudioClip> _trimmedPronunciationClipCache = new();

    private const string PrefKeyMasterVolume = "salinlahi.audio.master_volume";
    private const string PrefKeyBgmVolume = "salinlahi.audio.bgm_volume";
    private const string PrefKeySfxVolume = "salinlahi.audio.sfx_volume";

    private float _masterVolume = 1f;
    private float _bgmVolume = 1f;
    private float _sfxVolume = 1f;

    // Per-clip scale applied to the BGM source on top of master & bgm sliders.
    // Set by FadeInBGM; reused by ApplyVolumes and fade routines so live slider
    // changes during a track preserve the bank's authored level.
    private float _bgmScale = 1f;
    private Coroutine _bgmFadeRoutine;

    // Ducking is deliberately a SEPARATE multiplier from _bgmScale. _bgmScale belongs to the
    // fade/crossfade system, which resets it to 1 at several points; folding the duck into it
    // would let a scene crossfade cancel a duck mid-syllable, or leave the duck stuck on.
    private float _bgmDuck = 1f;
    private Coroutine _bgmDuckRoutine;

    /// <summary>
    /// The one place BGM level is composed. Kept as a property so a live volume-slider change,
    /// an in-flight fade and an active duck all read the same value instead of three call sites
    /// disagreeing about which factors apply.
    /// </summary>
    private float BgmTargetVolume => _masterVolume * _bgmVolume * _bgmScale * _bgmDuck;
    private BgmContext _currentBgmContext = BgmContext.None;

    public float MasterVolume => _masterVolume;
    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        EnsureBaseHitSfxSource();
        EnsurePronunciationSfxSource();
        WarmupSfxClips();
        LoadSavedVolumes();
        ApplyContextBgmForScene(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EventBus.OnPronunciationRequested += PlayPronunciationClip;
        EventBus.OnSpokenPronunciationRequested += PlaySpokenPronunciationClip;
        EventBus.OnBaseDamageApplied += PlayBaseHitSound;
        EventBus.OnChainAttackHit += PlayChainLightningSfx;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        EventBus.OnPronunciationRequested -= PlayPronunciationClip;
        EventBus.OnSpokenPronunciationRequested -= PlaySpokenPronunciationClip;
        EventBus.OnBaseDamageApplied -= PlayBaseHitSound;
        EventBus.OnChainAttackHit -= PlayChainLightningSfx;

        if (_chainZapRoutine != null)
        {
            StopCoroutine(_chainZapRoutine);
            _chainZapRoutine = null;
        }

        if (_bgmFadeRoutine != null)
        {
            StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = null;
        }

        if (_bgmDuckRoutine != null)
        {
            StopCoroutine(_bgmDuckRoutine);
            _bgmDuckRoutine = null;
        }
        _bgmDuck = 1f;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A syllable can be cut off mid-duck by a scene change (leaving a level during a
        // learning card, say). Without this the next scene's music would come up dipped.
        CancelBgmDuck();
        ApplyContextBgmForScene(scene.name);
    }

    private void ApplyContextBgmForScene(string sceneName)
    {
        BgmContext context = ResolveContext(sceneName);
        if (context == BgmContext.None)
            return;

        if (_currentBgmContext == context && _bgmSource != null && _bgmSource.isPlaying)
            return;

        AudioClip clipToPlay = context == BgmContext.Home ? _homeScreenBgmClip : _gameplayBgmClip;
        if (clipToPlay == null)
        {
            DebugLogger.LogError($"AudioManager: Missing BGM clip for context '{context}' in scene '{sceneName}'.");
            return;
        }

        _currentBgmContext = context;
        CrossfadeToBgm(clipToPlay);
    }

    private static BgmContext ResolveContext(string sceneName)
    {
        if (sceneName == "MainMenu")
            return BgmContext.Home;

        if (sceneName == "Gameplay" || sceneName == "Level_01_Tutorial")
            return BgmContext.Gameplay;

        return BgmContext.None;
    }

    private void CrossfadeToBgm(AudioClip clip)
    {
        if (_bgmSource == null || clip == null)
            return;

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            _bgmSource.loop = true;
            return;
        }

        if (_contextBgmFadeOutSeconds <= 0f && _contextBgmFadeInSeconds <= 0f)
        {
            PlayBGM(clip);
            return;
        }

        StartCoroutine(CrossfadeRoutine(clip));
    }

    private IEnumerator CrossfadeRoutine(AudioClip clip)
    {
        if (_contextBgmFadeOutSeconds > 0f && _bgmSource.isPlaying)
            yield return FadeOutBGM(_contextBgmFadeOutSeconds);

        FadeInBGM(clip, _contextBgmFadeInSeconds);
    }

    private void PlayPronunciationClip(BaybayinCharacterSO character)
    {
        PlayPronunciation(character?.pronunciationClip);
    }

    /// <summary>
    /// SALIN-157: plays the approved clip for one spoken value (E/I, O/U, DA/RA
    /// follow the level context), falling back to the character-level clip. A
    /// null resolution is a silent no-op — the learning card keeps everything
    /// essential visible on its own.
    /// </summary>
    private void PlaySpokenPronunciationClip(BaybayinCharacterSO character, string spokenValueId)
    {
        PlayPronunciation(SpokenValueResolver.ResolveClip(character, spokenValueId));
    }

    public void PlayPronunciation(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsurePronunciationSfxSource();
        if (_pronunciationSfxSource == null)
            return;

        AudioClip prepared = PreparePronunciationClipForImmediateAttack(clip);
        if (prepared == null)
            return;

        float volumeScale = 1f;
        float pitch = 1f;
        if (_enablePronunciationModulation)
        {
            float pitchMin = Mathf.Min(_pronunciationPitchMin, _pronunciationPitchMax);
            float pitchMax = Mathf.Max(_pronunciationPitchMin, _pronunciationPitchMax);
            float volumeMin = Mathf.Min(_pronunciationVolumeMin, _pronunciationVolumeMax);
            float volumeMax = Mathf.Max(_pronunciationVolumeMin, _pronunciationVolumeMax);
            pitch = Random.Range(pitchMin, pitchMax);
            volumeScale = Random.Range(volumeMin, volumeMax);
        }

        _pronunciationSfxSource.pitch = pitch;
        _pronunciationSfxSource.PlayOneShot(prepared, volumeScale);

        // pitch is a playback-rate change, so the audible length is the clip divided by it.
        DuckBgmForPronunciation(prepared.length / Mathf.Max(0.01f, pitch));
    }

    /// <summary>
    /// Dips the music under a syllable and brings it back.
    ///
    /// After the loudness pass the pronunciation clips sit at -17.5 LUFS against a -20 LUFS
    /// music bed -- only about +2.5 dB, and they could not be pushed louder because they were
    /// already near full scale. Ducking is what actually makes the syllable read, and the
    /// syllable is the phonological-loop mechanism the whole learning model rests on.
    ///
    /// Retriggering restarts the envelope from wherever it currently is rather than stacking,
    /// so a rapid run of cards holds one continuous dip instead of stepping the music down.
    /// </summary>
    private void DuckBgmForPronunciation(float clipSeconds)
    {
        if (!_duckBgmDuringPronunciation || _bgmSource == null)
            return;

        if (_bgmDuckRoutine != null)
            StopCoroutine(_bgmDuckRoutine);

        _bgmDuckRoutine = StartCoroutine(DuckBgmRoutine(clipSeconds));
    }

    private IEnumerator DuckBgmRoutine(float clipSeconds)
    {
        float target = Mathf.Clamp(_pronunciationDuckLevel, 0.05f, 1f);

        // Unscaled throughout: learning cards pause the game, and a syllable that plays behind
        // a paused screen must still duck and recover.
        yield return FadeBgmDuckTo(target, _pronunciationDuckFadeOutSeconds);

        float hold = Mathf.Max(0f, clipSeconds) + Mathf.Max(0f, _pronunciationDuckHoldSeconds);
        float elapsed = 0f;
        while (elapsed < hold)
        {
            elapsed += Time.unscaledDeltaTime;
            // Re-apply every tick so a volume-slider change mid-duck is picked up.
            ApplyVolumes();
            yield return null;
        }

        yield return FadeBgmDuckTo(1f, _pronunciationDuckFadeInSeconds);
        _bgmDuckRoutine = null;
    }

    private IEnumerator FadeBgmDuckTo(float target, float seconds)
    {
        float from = _bgmDuck;
        if (seconds <= 0f)
        {
            _bgmDuck = target;
            ApplyVolumes();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _bgmDuck = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / seconds));
            ApplyVolumes();
            yield return null;
        }

        _bgmDuck = target;
        ApplyVolumes();
    }

    /// <summary>
    /// Drops any duck immediately. A duck left half-applied would quietly hold the music down
    /// for the rest of the session, so anything that tears down or re-points the BGM clears it.
    /// </summary>
    private void CancelBgmDuck()
    {
        if (_bgmDuckRoutine != null)
        {
            StopCoroutine(_bgmDuckRoutine);
            _bgmDuckRoutine = null;
        }

        if (Mathf.Approximately(_bgmDuck, 1f))
            return;

        _bgmDuck = 1f;
        ApplyVolumes();
    }

    private void PlayBaseHitSound(int appliedDamage)
    {
        if (appliedDamage <= 0 || _baseHitClips == null || _baseHitClips.Length == 0)
            return;

        EnsureBaseHitSfxSource();
        if (_baseHitSfxSource == null)
            return;

        int clipIndex = Random.Range(0, _baseHitClips.Length);
        AudioClip clip = PrepareClipForImmediateAttack(_baseHitClips[clipIndex]);
        if (clip == null)
            return;

        float volumeMin = Mathf.Min(_baseHitVolumeMin, _baseHitVolumeMax);
        float volumeMax = Mathf.Max(_baseHitVolumeMin, _baseHitVolumeMax);
        float pitchMin = Mathf.Min(_baseHitPitchMin, _baseHitPitchMax);
        float pitchMax = Mathf.Max(_baseHitPitchMin, _baseHitPitchMax);
        float volume = Random.Range(volumeMin, volumeMax);
        _baseHitSfxSource.pitch = Random.Range(pitchMin, pitchMax);
        _baseHitSfxSource.PlayOneShot(clip, volume);
    }

    private void PlayChainLightningSfx(IReadOnlyList<Enemy> targets)
    {
        if (targets == null || targets.Count == 0)
            return;

        if (_chainZapRoutine != null)
            StopCoroutine(_chainZapRoutine);

        int zapCount = Mathf.Min(Mathf.Max(0, _maxChainZapOneShots), targets.Count);
        _chainZapRoutine = StartCoroutine(PlayChainLightningBurst(targets.Count, zapCount));
    }

    private IEnumerator PlayChainLightningBurst(int targetCount, int zapCount)
    {
        float startMin = Mathf.Min(_chainAudioStartDelayMin, _chainAudioStartDelayMax);
        float startMax = Mathf.Max(_chainAudioStartDelayMin, _chainAudioStartDelayMax);
        float startDelay = startMax > 0f ? Random.Range(startMin, startMax) : 0f;
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (_chainLightningSfxClip != null)
            _sfxSource.PlayOneShot(_chainLightningSfxClip);

        if (!_enablePerEnemyChainZap || _chainLightningZapSfxClip == null || zapCount <= 0 || targetCount <= 0)
        {
            _chainZapRoutine = null;
            yield break;
        }

        for (int i = 0; i < zapCount; i++)
        {
            float volume = _chainZapVolumeScale * Random.Range(0.85f, 1f);
            _sfxSource.PlayOneShot(_chainLightningZapSfxClip, volume);

            if (i < zapCount - 1)
            {
                float jitter = _chainZapIntervalJitter > 0f
                    ? Random.Range(0f, _chainZapIntervalJitter)
                    : 0f;
                yield return new WaitForSeconds(_chainZapInterval + jitter);
            }
        }

        _chainZapRoutine = null;
    }

    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f);
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayMenuButtonClick()
    {
        PlayMenuButtonClip(_menuButtonClickClip);
    }

    public void PlayMenuExitButtonClick()
    {
        AudioClip clip = _menuExitButtonClickClip != null ? _menuExitButtonClickClip : _menuButtonClickClip;
        PlayMenuButtonClip(clip);
    }

    private void PlayMenuButtonClip(AudioClip clip)
    {
        if (_sfxSource == null || clip == null)
            return;

        AudioClip prepared = PrepareClipForImmediateAttack(clip);
        if (prepared == null)
            return;

        _sfxSource.PlayOneShot(prepared);
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || _bgmSource == null || _bgmSource.clip == clip) return;
        _bgmScale = 1f;
        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.volume = BgmTargetVolume;
        _bgmSource.Play();
    }

    public void StopBGM()
    {
        if (_bgmSource == null)
            return;

        _bgmScale = 1f;
        _bgmSource.Stop();
    }

    // Fades from the current BGM (if any) to the given clip over `seconds`.
    // If `clip` is null, no-ops. If already playing `clip`, no-ops.
    // `seconds <= 0` snaps to the new clip at full target volume (no fade).
    // Cancels any in-flight fade before starting a new one.
    public Coroutine FadeInBGM(AudioClip clip, float seconds, float volumeScale = 1f)
    {
        if (clip == null) return null;
        if (_bgmSource == null) return null;
        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);

        _bgmScale = Mathf.Clamp01(volumeScale);

        if (seconds <= 0f)
        {
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.volume = BgmTargetVolume;
            _bgmSource.Play();
            _bgmFadeRoutine = null;
            return null;
        }

        _bgmFadeRoutine = StartCoroutine(FadeBgmTo(clip, seconds));
        return _bgmFadeRoutine;
    }

    // Fades the current BGM out over `seconds`, then stops the source.
    // `seconds <= 0` is equivalent to StopBGM(). Cancels any in-flight fade.
    public Coroutine FadeOutBGM(float seconds)
    {
        if (_bgmSource == null) return null;
        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);

        if (seconds <= 0f)
        {
            _bgmSource.Stop();
            _bgmScale = 1f;
            _bgmSource.volume = BgmTargetVolume;
            _bgmFadeRoutine = null;
            return null;
        }

        _bgmFadeRoutine = StartCoroutine(FadeBgmOut(seconds));
        return _bgmFadeRoutine;
    }

    private IEnumerator FadeBgmTo(AudioClip clip, float seconds)
    {
        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.volume = 0f;
        _bgmSource.Play();

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float target = BgmTargetVolume;
            _bgmSource.volume = Mathf.Lerp(0f, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        _bgmSource.volume = BgmTargetVolume;
        _bgmFadeRoutine = null;
    }

    private IEnumerator FadeBgmOut(float seconds)
    {
        float startVolume = _bgmSource.volume;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            // Recompute the upper bound each tick so volume slider changes
            // mid-fade are respected.
            float upper = BgmTargetVolume;
            float from = Mathf.Min(startVolume, upper);
            _bgmSource.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        _bgmSource.Stop();
        _bgmScale = 1f;
        _bgmSource.volume = BgmTargetVolume;
        _bgmFadeRoutine = null;
    }

    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
        PlayerPrefs.SetFloat(PrefKeyMasterVolume, _masterVolume);
        PlayerPrefs.Save();
    }

    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
        PlayerPrefs.SetFloat(PrefKeyBgmVolume, _bgmVolume);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
        PlayerPrefs.SetFloat(PrefKeySfxVolume, _sfxVolume);
        PlayerPrefs.Save();
    }

    private void ApplyVolumes()
    {
        if (_bgmSource != null)
            _bgmSource.volume = BgmTargetVolume;

        float sfxVolume = _masterVolume * _sfxVolume;
        if (_sfxSource != null)
            _sfxSource.volume = sfxVolume;
        if (_baseHitSfxSource != null)
            _baseHitSfxSource.volume = sfxVolume;
        if (_pronunciationSfxSource != null)
            _pronunciationSfxSource.volume = sfxVolume;
    }

    private void LoadSavedVolumes()
    {
        _masterVolume = PlayerPrefs.GetFloat(PrefKeyMasterVolume, 1f);
        _bgmVolume = PlayerPrefs.GetFloat(PrefKeyBgmVolume, 1f);
        _sfxVolume = PlayerPrefs.GetFloat(PrefKeySfxVolume, 1f);
        ApplyVolumes();
        DebugLogger.Log($"AudioManager: Loaded volumes — Master={_masterVolume:F2}, BGM={_bgmVolume:F2}, SFX={_sfxVolume:F2}");
    }

    private void EnsureBaseHitSfxSource()
    {
        if (_baseHitSfxSource != null)
            return;

        if (_sfxSource == null)
            return;

        _baseHitSfxSource = gameObject.AddComponent<AudioSource>();
        _baseHitSfxSource.outputAudioMixerGroup = _sfxSource.outputAudioMixerGroup;
        _baseHitSfxSource.playOnAwake = false;
        _baseHitSfxSource.loop = false;
        _baseHitSfxSource.mute = _sfxSource.mute;
        _baseHitSfxSource.bypassEffects = _sfxSource.bypassEffects;
        _baseHitSfxSource.bypassListenerEffects = _sfxSource.bypassListenerEffects;
        _baseHitSfxSource.bypassReverbZones = _sfxSource.bypassReverbZones;
        _baseHitSfxSource.priority = _sfxSource.priority;
        _baseHitSfxSource.volume = _sfxSource.volume;
        _baseHitSfxSource.panStereo = _sfxSource.panStereo;
        _baseHitSfxSource.spatialBlend = _sfxSource.spatialBlend;
        _baseHitSfxSource.reverbZoneMix = _sfxSource.reverbZoneMix;
        _baseHitSfxSource.dopplerLevel = _sfxSource.dopplerLevel;
        _baseHitSfxSource.spread = _sfxSource.spread;
        _baseHitSfxSource.rolloffMode = _sfxSource.rolloffMode;
        _baseHitSfxSource.minDistance = _sfxSource.minDistance;
        _baseHitSfxSource.maxDistance = _sfxSource.maxDistance;
    }

    private void EnsurePronunciationSfxSource()
    {
        if (_pronunciationSfxSource != null)
            return;

        if (_sfxSource == null)
            return;

        _pronunciationSfxSource = gameObject.AddComponent<AudioSource>();
        _pronunciationSfxSource.outputAudioMixerGroup = _sfxSource.outputAudioMixerGroup;
        _pronunciationSfxSource.playOnAwake = false;
        _pronunciationSfxSource.loop = false;
        _pronunciationSfxSource.mute = _sfxSource.mute;
        _pronunciationSfxSource.bypassEffects = _sfxSource.bypassEffects;
        _pronunciationSfxSource.bypassListenerEffects = _sfxSource.bypassListenerEffects;
        _pronunciationSfxSource.bypassReverbZones = _sfxSource.bypassReverbZones;
        _pronunciationSfxSource.priority = _sfxSource.priority;
        _pronunciationSfxSource.volume = _sfxSource.volume;
        _pronunciationSfxSource.panStereo = _sfxSource.panStereo;
        _pronunciationSfxSource.spatialBlend = _sfxSource.spatialBlend;
        _pronunciationSfxSource.reverbZoneMix = _sfxSource.reverbZoneMix;
        _pronunciationSfxSource.dopplerLevel = _sfxSource.dopplerLevel;
        _pronunciationSfxSource.spread = _sfxSource.spread;
        _pronunciationSfxSource.rolloffMode = _sfxSource.rolloffMode;
        _pronunciationSfxSource.minDistance = _sfxSource.minDistance;
        _pronunciationSfxSource.maxDistance = _sfxSource.maxDistance;
    }

    private void WarmupSfxClips()
    {
        PrepareClipForImmediateAttack(_menuButtonClickClip);
        PrepareClipForImmediateAttack(_menuExitButtonClickClip);

        if (_baseHitClips == null)
            return;

        for (int i = 0; i < _baseHitClips.Length; i++)
            PrepareClipForImmediateAttack(_baseHitClips[i]);
    }

    private AudioClip PrepareClipForImmediateAttack(AudioClip clip)
    {
        if (clip == null)
            return null;

        if (_trimmedClipCache.TryGetValue(clip, out AudioClip cached))
            return cached;

        clip.LoadAudioData();
        AudioClip trimmed = TrimLeadingSilence(clip);
        _trimmedClipCache[clip] = trimmed;
        return trimmed;
    }

    private AudioClip PreparePronunciationClipForImmediateAttack(AudioClip clip)
    {
        if (clip == null)
            return null;

        if (_trimmedPronunciationClipCache.TryGetValue(clip, out AudioClip cached))
            return cached;

        clip.LoadAudioData();
        AudioClip trimmed = TrimLeadingSilence(
            clip,
            Mathf.Max(0f, _pronunciationTrimSilenceThreshold),
            Mathf.Max(0f, _maxPronunciationLeadingTrimSeconds));
        _trimmedPronunciationClipCache[clip] = trimmed;
        return trimmed;
    }

    private AudioClip TrimLeadingSilence(AudioClip source)
    {
        return TrimLeadingSilence(
            source,
            Mathf.Max(0f, _trimSilenceThreshold),
            Mathf.Max(0f, _maxLeadingSilenceTrimSeconds));
    }

    private AudioClip TrimLeadingSilence(
        AudioClip source,
        float silenceThreshold,
        float maxLeadingTrimSeconds)
    {
        if (source == null)
            return null;

        int channels = source.channels;
        int totalSamples = source.samples;
        if (channels <= 0 || totalSamples <= 0)
            return source;

        float[] data = new float[totalSamples * channels];
        if (!source.GetData(data, 0))
            return source;

        int maxTrimFrames = Mathf.Min(
            totalSamples,
            Mathf.FloorToInt(maxLeadingTrimSeconds * source.frequency));

        int firstAudibleFrame = 0;
        bool found = false;
        float threshold = Mathf.Max(0f, silenceThreshold);

        for (int frame = 0; frame < maxTrimFrames; frame++)
        {
            int baseIndex = frame * channels;
            for (int c = 0; c < channels; c++)
            {
                if (Mathf.Abs(data[baseIndex + c]) > threshold)
                {
                    firstAudibleFrame = frame;
                    found = true;
                    break;
                }
            }

            if (found)
                break;
        }

        if (!found || firstAudibleFrame <= 0)
            return source;

        int trimmedSamples = totalSamples - firstAudibleFrame;
        if (trimmedSamples <= 0)
            return source;

        float[] trimmedData = new float[trimmedSamples * channels];
        System.Array.Copy(
            data,
            firstAudibleFrame * channels,
            trimmedData,
            0,
            trimmedData.Length);

        AudioClip trimmed = AudioClip.Create(
            $"{source.name}_trimmed",
            trimmedSamples,
            channels,
            source.frequency,
            false);

        trimmed.SetData(trimmedData, 0);
        return trimmed;
    }
}
