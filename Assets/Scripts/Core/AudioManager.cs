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

    [Header("Recognition Feedback SFX")]
    [Tooltip("Plays when a drawn glyph passes the recognition threshold. This is the core "
        + "learning loop's success signal -- before it the game answered a correct glyph with "
        + "silence.")]
    [SerializeField] private AudioClip _correctGlyphClip;

    [Tooltip("Kenney's clips are mastered near full scale. 0.31 puts this ~10 dB down, level "
        + "with the pronunciation clips rather than 10 dB over the music bed.")]
    [SerializeField, Range(0f, 1f)] private float _correctGlyphVolume = 0.31f;

    [Tooltip("Plays on a failed submission (below threshold, degenerate stroke, or a wrong "
        + "glyph against the boss). Fires on the commit path only, never on live preview.")]
    [SerializeField] private AudioClip _wrongGlyphClip;

    [SerializeField, Range(0f, 1f)] private float _wrongGlyphVolume = 0.67f;

    [Header("Reward & Threat SFX")]
    [Tooltip("Plays on OnEnemyDefeated. Deliberately quiet: this is the most frequent event in "
        + "the game and belongs under the mix, not on top of it.")]
    [SerializeField] private AudioClip _enemyDefeatedClip;
    [SerializeField, Range(0f, 1f)] private float _enemyDefeatedVolume = 0.29f;

    [Tooltip("A mass clear defeats every enemy in the same frame. Without a cap that is one "
        + "death one-shot per enemy stacked on a single frame, which reads as a burst of noise "
        + "rather than as kills.")]
    [SerializeField, Min(1)] private int _maxEnemyDeathsPerBurst = 3;

    [Tooltip("Window over which the burst cap applies.")]
    [SerializeField, Min(0f)] private float _enemyDeathBurstWindow = 0.14f;

    [SerializeField, Min(0.1f)] private float _enemyDeathPitchMin = 0.94f;
    [SerializeField, Min(0.1f)] private float _enemyDeathPitchMax = 1.06f;

    [Tooltip("Plays when a locked level is pressed. Before this the locked path played the same "
        + "affirmative click as an unlocked one, so a refused press sounded like an accepted one.")]
    [SerializeField] private AudioClip _levelLockedClip;
    [SerializeField, Range(0f, 1f)] private float _levelLockedVolume = 0.5f;

    [Tooltip("Plays on OnCharacterUnlocked -- the game's main reward moment.")]
    [SerializeField] private AudioClip _characterUnlockedClip;
    [SerializeField, Range(0f, 1f)] private float _characterUnlockedVolume = 0.5f;

    [Header("Outcome Stingers")]
    [SerializeField] private AudioClip _victoryStingClip;
    [SerializeField] private AudioClip _defeatStingClip;
    [SerializeField, Range(0f, 1f)] private float _stingVolume = 1f;

    [Tooltip("Neither screen stops the gameplay BGM, so without a dip the sting competes with a "
        + "track that is still looping underneath it.")]
    [SerializeField] private bool _duckBgmDuringSting = true;

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

    [Tooltip("Trailing silence is trimmed as well as leading. The shipped UI clips carry long "
        + "dead tails -- the exit/back clip is 0.39s of sound followed by 7.6s of silence -- and "
        + "PlayOneShot holds a voice for the whole clip, so a back press kept a voice alive for "
        + "eight seconds and bled across the scene load that followed it.")]
    [SerializeField] private bool _trimTrailingSilence = true;

    [Tooltip("Seconds of the fade-out kept after the last audible sample, so a decaying tail is "
        + "not cut to an audible click.")]
    [SerializeField, Min(0f)] private float _trailingSilencePadSeconds = 0.03f;
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

    // Stingers get their own source so a scene change can stop them without touching any other
    // SFX. The victory sting runs ~12s and the player can dismiss the screen in two, so without
    // this it would play on into LevelSelect.
    private AudioSource _stingSfxSource;

    // Enemy deaths are pitch-varied, and pitch is a property of the source rather than of the
    // one-shot. Sharing _baseHitSfxSource would let a death re-pitch a base hit already in flight.
    private AudioSource _enemyDeathSfxSource;
    private float _enemyDeathBurstStartedAt = -1f;
    private int _enemyDeathsThisBurst;
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
        EnsureEnemyDeathSfxSource();
        EnsureStingSfxSource();
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
        EventBus.OnCharacterRecognized += PlayCorrectGlyphSfx;
        EventBus.OnDrawingFailed += PlayWrongGlyphSfx;
        EventBus.OnLevelComplete += PlayVictorySting;
        EventBus.OnGameOver += PlayDefeatSting;
        EventBus.OnEnemyDefeated += PlayEnemyDefeatedSfx;
        EventBus.OnCharacterUnlocked += PlayCharacterUnlockedSfx;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        EventBus.OnPronunciationRequested -= PlayPronunciationClip;
        EventBus.OnSpokenPronunciationRequested -= PlaySpokenPronunciationClip;
        EventBus.OnBaseDamageApplied -= PlayBaseHitSound;
        EventBus.OnChainAttackHit -= PlayChainLightningSfx;
        EventBus.OnCharacterRecognized -= PlayCorrectGlyphSfx;
        EventBus.OnDrawingFailed -= PlayWrongGlyphSfx;
        EventBus.OnLevelComplete -= PlayVictorySting;
        EventBus.OnGameOver -= PlayDefeatSting;
        EventBus.OnEnemyDefeated -= PlayEnemyDefeatedSfx;
        EventBus.OnCharacterUnlocked -= PlayCharacterUnlockedSfx;

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
        StopSting();
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
        if (!_duckBgmDuringPronunciation)
            return;

        DuckBgmFor(clipSeconds);
    }

    /// <summary>
    /// The duck envelope itself, with no feature flag of its own. Pronunciation and the outcome
    /// stingers each own their own toggle and share this.
    /// </summary>
    private void DuckBgmFor(float clipSeconds)
    {
        if (_bgmSource == null)
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

    /// <summary>
    /// SALIN-audit: the success half of the core learning loop. Raised once per submission that
    /// clears the recognition threshold, from gameplay and the Tracing Dojo alike.
    /// </summary>
    private void PlayCorrectGlyphSfx(string characterId)
    {
        PlayFeedbackSfx(_correctGlyphClip, _correctGlyphVolume);
    }

    /// <summary>
    /// The failure half. Bound to OnDrawingFailed rather than OnRecognitionResolved because
    /// PreviewRecognize raises the latter continuously while the player is still drawing --
    /// an error tone on every preview frame would be unusable. OnDrawingFailed is raised only
    /// from the commit path (below threshold, degenerate submission, or a wrong glyph against
    /// the boss), so it fires once per real attempt.
    /// </summary>
    private void PlayWrongGlyphSfx()
    {
        PlayFeedbackSfx(_wrongGlyphClip, _wrongGlyphVolume);
    }

    private void PlayFeedbackSfx(AudioClip clip, float volumeScale)
    {
        if (clip == null || _sfxSource == null)
            return;

        AudioClip prepared = PrepareClipForImmediateAttack(clip);
        if (prepared == null)
            return;

        _sfxSource.PlayOneShot(prepared, Mathf.Clamp01(volumeScale));
    }

    /// <summary>
    /// Enemy defeat. Capped per burst because CombatResolver raises OnAOETriggered alongside one
    /// OnEnemyDefeated per enemy, so a mass clear arrives as N events in a single frame.
    /// </summary>
    private void PlayEnemyDefeatedSfx(BaybayinCharacterSO character)
    {
        if (_enemyDefeatedClip == null)
            return;

        EnsureEnemyDeathSfxSource();
        if (_enemyDeathSfxSource == null)
            return;

        // Unscaled: a mass clear can land on the same frame as a hit-stop.
        float now = Time.unscaledTime;
        if (_enemyDeathBurstStartedAt < 0f || now - _enemyDeathBurstStartedAt > _enemyDeathBurstWindow)
        {
            _enemyDeathBurstStartedAt = now;
            _enemyDeathsThisBurst = 0;
        }

        if (_enemyDeathsThisBurst >= Mathf.Max(1, _maxEnemyDeathsPerBurst))
            return;

        _enemyDeathsThisBurst++;

        AudioClip prepared = PrepareClipForImmediateAttack(_enemyDefeatedClip);
        if (prepared == null)
            return;

        float pitchMin = Mathf.Min(_enemyDeathPitchMin, _enemyDeathPitchMax);
        float pitchMax = Mathf.Max(_enemyDeathPitchMin, _enemyDeathPitchMax);
        _enemyDeathSfxSource.pitch = Random.Range(pitchMin, pitchMax);
        _enemyDeathSfxSource.PlayOneShot(prepared, Mathf.Clamp01(_enemyDefeatedVolume));
    }

    private void PlayCharacterUnlockedSfx(BaybayinCharacterSO character)
    {
        PlayFeedbackSfx(_characterUnlockedClip, _characterUnlockedVolume);
    }

    /// <summary>
    /// Pressing a locked level. Called directly by LevelButton rather than driven from an event,
    /// because a refused press raises nothing on the bus -- it only reports back to its owner.
    /// </summary>
    public void PlayLevelLockedDenied()
    {
        if (_levelLockedClip == null)
        {
            // No dedicated clip assigned: the affirmative click is worse than nothing here, so
            // stay silent rather than tell the player the press was accepted.
            return;
        }

        PlayFeedbackSfx(_levelLockedClip, _levelLockedVolume);
    }

    private void PlayVictorySting()
    {
        PlaySting(_victoryStingClip);
    }

    private void PlayDefeatSting()
    {
        PlaySting(_defeatStingClip);
    }

    /// <summary>
    /// Neither the victory nor the defeat screen stops the gameplay BGM, so the sting is ducked
    /// over a track that keeps looping underneath. Retriggering replaces the previous sting
    /// rather than stacking -- a defeat arriving on the heels of a victory should not play both.
    /// </summary>
    private void PlaySting(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsureStingSfxSource();
        if (_stingSfxSource == null)
            return;

        // Deliberately NOT run through PrepareClipForImmediateAttack. That path caches a decoded
        // PCM copy of the clip, which is worth it for a 0.5s UI blip and wasteful for 10-12s of
        // music -- roughly 4 MB resident per sting, plus a decode hitch on the first victory. The
        // stingers are topped and tailed at authoring time (0.13s and 0.20s of lead-in), so there
        // is nothing for the trim to recover.
        _stingSfxSource.Stop();
        _stingSfxSource.clip = clip;
        _stingSfxSource.Play();

        if (_duckBgmDuringSting)
            DuckBgmFor(clip.length);
    }

    private void StopSting()
    {
        if (_stingSfxSource != null)
            _stingSfxSource.Stop();
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

        if (_sfxSource == null)
        {
            _chainZapRoutine = null;
            yield break;
        }

        if (_chainLightningSfxClip != null)
            _sfxSource.PlayOneShot(_chainLightningSfxClip);

        // The per-enemy layer only works with a clip of its own. Both fields currently point at the
        // same 1.1s lightning strike, and replaying one recording against a 60ms-offset copy of
        // itself comb-filters instead of reading as separate zaps -- it just smears the strike.
        // Skipping the layer is the honest behaviour until a distinct short zap is authored.
        bool zapClipIsDistinct = _chainLightningZapSfxClip != null
            && _chainLightningZapSfxClip != _chainLightningSfxClip;

        if (!_enablePerEnemyChainZap || !zapClipIsDistinct || zapCount <= 0 || targetCount <= 0)
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
        if (clip == null || _bgmSource == null) return;

        // Stop() leaves .clip assigned, so guarding on the clip alone made this a no-op after any
        // fade-out -- the track that was faded down could never be started again by name.
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
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
        if (_enemyDeathSfxSource != null)
            _enemyDeathSfxSource.volume = sfxVolume;
        if (_stingSfxSource != null)
            _stingSfxSource.volume = sfxVolume * Mathf.Clamp01(_stingVolume);
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

    private void EnsureEnemyDeathSfxSource()
    {
        if (_enemyDeathSfxSource != null)
            return;

        if (_sfxSource == null)
            return;

        _enemyDeathSfxSource = gameObject.AddComponent<AudioSource>();
        _enemyDeathSfxSource.outputAudioMixerGroup = _sfxSource.outputAudioMixerGroup;
        _enemyDeathSfxSource.playOnAwake = false;
        _enemyDeathSfxSource.loop = false;
        _enemyDeathSfxSource.mute = _sfxSource.mute;
        _enemyDeathSfxSource.bypassEffects = _sfxSource.bypassEffects;
        _enemyDeathSfxSource.bypassListenerEffects = _sfxSource.bypassListenerEffects;
        _enemyDeathSfxSource.bypassReverbZones = _sfxSource.bypassReverbZones;
        _enemyDeathSfxSource.priority = _sfxSource.priority;
        _enemyDeathSfxSource.panStereo = _sfxSource.panStereo;
        _enemyDeathSfxSource.spatialBlend = _sfxSource.spatialBlend;
        _enemyDeathSfxSource.reverbZoneMix = _sfxSource.reverbZoneMix;
        _enemyDeathSfxSource.dopplerLevel = _sfxSource.dopplerLevel;
        _enemyDeathSfxSource.spread = _sfxSource.spread;
        _enemyDeathSfxSource.rolloffMode = _sfxSource.rolloffMode;
        _enemyDeathSfxSource.minDistance = _sfxSource.minDistance;
        _enemyDeathSfxSource.maxDistance = _sfxSource.maxDistance;
        ApplyVolumes();
    }

    private void EnsureStingSfxSource()
    {
        if (_stingSfxSource != null)
            return;

        if (_sfxSource == null)
            return;

        _stingSfxSource = gameObject.AddComponent<AudioSource>();
        _stingSfxSource.outputAudioMixerGroup = _sfxSource.outputAudioMixerGroup;
        _stingSfxSource.playOnAwake = false;
        _stingSfxSource.loop = false;
        _stingSfxSource.mute = _sfxSource.mute;
        _stingSfxSource.bypassEffects = _sfxSource.bypassEffects;
        _stingSfxSource.bypassListenerEffects = _sfxSource.bypassListenerEffects;
        _stingSfxSource.bypassReverbZones = _sfxSource.bypassReverbZones;
        _stingSfxSource.priority = _sfxSource.priority;
        _stingSfxSource.volume = _sfxSource.volume;
        _stingSfxSource.panStereo = _sfxSource.panStereo;
        _stingSfxSource.spatialBlend = _sfxSource.spatialBlend;
        _stingSfxSource.reverbZoneMix = _sfxSource.reverbZoneMix;
        _stingSfxSource.dopplerLevel = _sfxSource.dopplerLevel;
        _stingSfxSource.spread = _sfxSource.spread;
        _stingSfxSource.rolloffMode = _sfxSource.rolloffMode;
        _stingSfxSource.minDistance = _sfxSource.minDistance;
        _stingSfxSource.maxDistance = _sfxSource.maxDistance;

        // Copying _sfxSource.volume above misses _stingVolume. That self-corrects on the next
        // slider change, but this source can also be created lazily from PlaySting -- in which
        // case the very first sting would play at the wrong level.
        ApplyVolumes();
    }

    private void WarmupSfxClips()
    {
        PrepareClipForImmediateAttack(_menuButtonClickClip);
        PrepareClipForImmediateAttack(_menuExitButtonClickClip);
        PrepareClipForImmediateAttack(_correctGlyphClip);
        PrepareClipForImmediateAttack(_wrongGlyphClip);
        PrepareClipForImmediateAttack(_enemyDefeatedClip);
        PrepareClipForImmediateAttack(_levelLockedClip);
        PrepareClipForImmediateAttack(_characterUnlockedClip);

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

        // The tail is scanned from the end and is deliberately uncapped: a leading trim is bounded
        // because over-trimming would eat the attack, but dead air after the last sample carries no
        // information at all, and it is what holds the PlayOneShot voice open.
        int lastAudibleFrame = totalSamples - 1;
        if (_trimTrailingSilence)
        {
            bool foundTail = false;
            for (int frame = totalSamples - 1; frame >= firstAudibleFrame; frame--)
            {
                int baseIndex = frame * channels;
                for (int c = 0; c < channels; c++)
                {
                    if (Mathf.Abs(data[baseIndex + c]) > threshold)
                    {
                        lastAudibleFrame = frame;
                        foundTail = true;
                        break;
                    }
                }

                if (foundTail)
                    break;
            }

            if (foundTail)
            {
                int padFrames = Mathf.Max(
                    0,
                    Mathf.FloorToInt(Mathf.Max(0f, _trailingSilencePadSeconds) * source.frequency));
                lastAudibleFrame = Mathf.Min(totalSamples - 1, lastAudibleFrame + padFrames);
            }
        }

        int trimmedSamples = lastAudibleFrame - firstAudibleFrame + 1;
        if (trimmedSamples <= 0)
            return source;

        // Nothing to do when neither end moved. Returning the source keeps the cache holding the
        // original clip rather than an identical copy.
        if (firstAudibleFrame <= 0 && trimmedSamples >= totalSamples)
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
