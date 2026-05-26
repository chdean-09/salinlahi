using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

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
    }

    private void OnDisable()
    {
        EventBus.OnEnemyDefeated -= PlayPronunciationClip;
        EventBus.OnBaseHit -= PlayBaseHitSound;
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

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || _bgmSource.clip == clip) return;
        _bgmScale = 1f;
        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.volume = _masterVolume * _bgmVolume;
        _bgmSource.Play();
    }

    public void StopBGM()
    {
        _bgmScale = 1f;
        _bgmSource.Stop();
    }

    private Coroutine _bgmFadeRoutine;

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

    private System.Collections.IEnumerator FadeBgmTo(AudioClip clip, float seconds)
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

    private System.Collections.IEnumerator FadeBgmOut(float seconds)
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
