using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

    protected override void Awake() => base.Awake();

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

    private void PlayBaseHitSound()
    {
        // Sprint 2: assign a base hit sfx clip via Inspector
        DebugLogger.Log("AudioManager: Base hit sound (stub)");
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
}
