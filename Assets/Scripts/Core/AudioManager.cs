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

    [Header("Chain Lightning Mix")]
    [SerializeField] private bool _enablePerEnemyChainZap = true;
    [SerializeField, Min(0)] private int _maxChainZapOneShots = 3;
    [SerializeField, Min(0f)] private float _chainZapVolumeScale = 0.3f;
    [SerializeField, Min(0f)] private float _chainAudioStartDelayMin = 0.08f;
    [SerializeField, Min(0f)] private float _chainAudioStartDelayMax = 0.22f;
    [SerializeField, Min(0f)] private float _chainZapInterval = 0.06f;
    [SerializeField, Min(0f)] private float _chainZapIntervalJitter = 0.07f;

    private Coroutine _chainZapRoutine;

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
        LoadSavedVolumes();
    }

    private void OnEnable()
    {
        EventBus.OnEnemyDefeated += PlayPronunciationClip;
        EventBus.OnBaseHit += PlayBaseHitSound;
        EventBus.OnChainAttackHit += PlayChainLightningSfx;
    }

    private void OnDisable()
    {
        EventBus.OnEnemyDefeated -= PlayPronunciationClip;
        EventBus.OnBaseHit -= PlayBaseHitSound;
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

    private void PlayBaseHitSound(int _)
    {
        // Sprint 2: assign a base hit sfx clip via Inspector
        DebugLogger.Log("AudioManager: Base hit sound (stub)");
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
    }

    private void LoadSavedVolumes()
    {
        _masterVolume = PlayerPrefs.GetFloat(PrefKeyMasterVolume, 1f);
        _bgmVolume = PlayerPrefs.GetFloat(PrefKeyBgmVolume, 1f);
        _sfxVolume = PlayerPrefs.GetFloat(PrefKeySfxVolume, 1f);
        ApplyVolumes();
        DebugLogger.Log($"AudioManager: Loaded volumes — Master={_masterVolume:F2}, BGM={_bgmVolume:F2}, SFX={_sfxVolume:F2}");
    }
}
