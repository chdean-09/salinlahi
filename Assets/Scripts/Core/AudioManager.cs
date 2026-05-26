using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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
    private readonly Dictionary<AudioClip, AudioClip> _trimmedClipCache = new();

    private const string PrefKeyMasterVolume = "salinlahi.audio.master_volume";
    private const string PrefKeyBgmVolume = "salinlahi.audio.bgm_volume";
    private const string PrefKeySfxVolume = "salinlahi.audio.sfx_volume";

    private float _masterVolume = 1f;
    private float _bgmVolume = 1f;
    private float _sfxVolume = 1f;

    public float MasterVolume => _masterVolume;
    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        EnsureBaseHitSfxSource();
        WarmupSfxClips();
        LoadSavedVolumes();
    }

    private void OnEnable()
    {
        EventBus.OnEnemyDefeated += PlayPronunciationClip;
        EventBus.OnBaseDamageApplied += PlayBaseHitSound;
        EventBus.OnChainAttackHit += PlayChainLightningSfx;
    }

    private void OnDisable()
    {
        EventBus.OnEnemyDefeated -= PlayPronunciationClip;
        EventBus.OnBaseDamageApplied -= PlayBaseHitSound;
        EventBus.OnChainAttackHit -= PlayChainLightningSfx;

        if (_chainZapRoutine != null)
        {
            StopCoroutine(_chainZapRoutine);
            _chainZapRoutine = null;
        }
    }

    // Sprint 2: Replace stubs with real implementations
    private void PlayPronunciationClip(BaybayinCharacterSO character)
    {
        if (character?.pronunciationClip != null)
            _sfxSource.PlayOneShot(character.pronunciationClip);
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
        if (clip == null) return;
        _sfxSource.PlayOneShot(clip);
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
        if (clip == null || _bgmSource.clip == clip) return;
        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.Play();
    }

    public void StopBGM() => _bgmSource.Stop();

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
            _bgmSource.volume = _masterVolume * _bgmVolume;
        if (_sfxSource != null)
            _sfxSource.volume = _masterVolume * _sfxVolume;
        if (_baseHitSfxSource != null)
            _baseHitSfxSource.volume = _masterVolume * _sfxVolume;
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

    private AudioClip TrimLeadingSilence(AudioClip source)
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
            Mathf.FloorToInt(_maxLeadingSilenceTrimSeconds * source.frequency));

        int firstAudibleFrame = 0;
        bool found = false;
        float threshold = Mathf.Max(0f, _trimSilenceThreshold);

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
