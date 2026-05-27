using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : Singleton<AudioManager>
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip _chainLightningSfxClip;
    [SerializeField] private AudioClip _chainLightningZapSfxClip;
    [SerializeField] private AudioClip _menuButtonClickClip;
    [SerializeField] private AudioClip _menuExitButtonClickClip;
    [SerializeField] private AudioClip[] _baseHitClips;

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
    }

    private void OnEnable()
    {
        EventBus.OnPronunciationRequested += PlayPronunciationClip;
        EventBus.OnBaseDamageApplied += PlayBaseHitSound;
        EventBus.OnChainAttackHit += PlayChainLightningSfx;
    }

    private void OnDisable()
    {
        EventBus.OnPronunciationRequested -= PlayPronunciationClip;
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
    }

    private void PlayPronunciationClip(BaybayinCharacterSO character)
    {
        PlayPronunciation(character?.pronunciationClip);
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
        _bgmSource.volume = _masterVolume * _bgmVolume;
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
            _bgmSource.volume = _masterVolume * _bgmVolume * _bgmScale;
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
            _bgmSource.volume = _masterVolume * _bgmVolume;
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
            float target = _masterVolume * _bgmVolume * _bgmScale;
            _bgmSource.volume = Mathf.Lerp(0f, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        _bgmSource.volume = _masterVolume * _bgmVolume * _bgmScale;
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
            float upper = _masterVolume * _bgmVolume * _bgmScale;
            float from = Mathf.Min(startVolume, upper);
            _bgmSource.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        _bgmSource.Stop();
        _bgmScale = 1f;
        _bgmSource.volume = _masterVolume * _bgmVolume;
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
            _bgmSource.volume = _masterVolume * _bgmVolume * _bgmScale;

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
