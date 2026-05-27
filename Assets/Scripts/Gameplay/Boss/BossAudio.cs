using System.Collections;
using UnityEngine;

// Plays per-boss audio in response to EventBus events. Lives on the boss
// prefab as a sibling of BossController. Resolves its BossAudioBankSO
// lazily from the BossConfigSO passed to OnBossStarted, so swapping the
// bank field on BossConfigSO is the only wiring needed for a different
// sonic identity.
//
// Owns:
//   - The footstep cadence coroutine (Pace-pattern phases only).
//   - The no-immediate-repeat picker for variant pools.
//
// Defensive nulls: every handler silently skips if the bank or specific
// clip/array is missing. A partially-filled bank does not break gameplay.
[RequireComponent(typeof(BossController))]
public class BossAudio : MonoBehaviour
{
    private BossAudioBankSO _bank;
    private BossSummonTicker _summonTicker;
    private BossDamageFeedback _dmgFeedback;
    private Coroutine _footstepRoutine;

    private int _lastHitIdx = -1;
    private int _lastDamagedIdx = -1;
    private int _lastFootstepIdx = -1;
    private int _lastTeleportIdx = -1;
    private bool _bgmFadeOutRequested;

    private void Awake()
    {
        _summonTicker = GetComponent<BossSummonTicker>();
        _dmgFeedback = GetComponent<BossDamageFeedback>();
    }

    private void OnEnable()
    {
        EventBus.OnBossStarted += HandleBossStarted;
        EventBus.OnBossPhaseStarted += HandleBossPhaseStarted;
        EventBus.OnBossSummonTick += HandleBossSummonTick;
        EventBus.OnBossTeleport += HandleBossTeleport;
        EventBus.OnBossExhausted += HandleBossExhausted;
        EventBus.OnBossDrawHit += HandleBossDrawHit;
        EventBus.OnBossDamaged += HandleBossDamaged;
        EventBus.OnBossVulnerabilityExpired += HandleBossVulnerabilityExpired;
        EventBus.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossPhaseStarted -= HandleBossPhaseStarted;
        EventBus.OnBossSummonTick -= HandleBossSummonTick;
        EventBus.OnBossTeleport -= HandleBossTeleport;
        EventBus.OnBossExhausted -= HandleBossExhausted;
        EventBus.OnBossDrawHit -= HandleBossDrawHit;
        EventBus.OnBossDamaged -= HandleBossDamaged;
        EventBus.OnBossVulnerabilityExpired -= HandleBossVulnerabilityExpired;
        EventBus.OnBossDefeated -= HandleBossDefeated;

        StopFootsteps();

        // Player can quit mid-boss; OnBossDefeated never fires in that case
        // and the boss BGM would otherwise keep looping on the DontDestroyOnLoad
        // AudioManager all the way back to MainMenu. Fade it out here unless
        // HandleBossDefeated already requested the fade.
        if (!_bgmFadeOutRequested && _bank != null && _bank.bgm != null
            && AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeOutBGM(_bank.bgmFadeOutSeconds);
        }

        _bank = null;
        _bgmFadeOutRequested = false;
    }

    private void HandleBossStarted(BossConfigSO config)
    {
        _bank = config != null ? config.audioBank : null;
        _bgmFadeOutRequested = false;
        if (_bank == null) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.FadeInBGM(_bank.bgm, _bank.bgmFadeInSeconds, _bank.bgmVolume);
        PlaySfx(_bank.introGrowl, _bank.introGrowlVolume);
    }

    private void HandleBossPhaseStarted(int phaseIndex)
    {
        // Footsteps are tied to the Pace pattern only. Hover/Teleport phases
        // get silence (Teleport has its own SFX on each snap).
        BossController controller = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (controller == null || controller.Config == null) { StopFootsteps(); return; }
        if (phaseIndex < 0 || phaseIndex >= controller.Config.phases.Count) { StopFootsteps(); return; }

        BossPhase phase = controller.Config.phases[phaseIndex];
        if (phase.movementPattern == BossMovementPattern.Pace && _bank != null
            && _bank.footsteps != null && _bank.footsteps.Length > 0)
        {
            StartFootsteps();
        }
        else
        {
            StopFootsteps();
        }
    }

    private void HandleBossSummonTick()
    {
        if (_bank != null) PlaySfx(_bank.summonTick, _bank.summonTickVolume);
    }

    private void HandleBossTeleport()
    {
        if (_bank != null) PlaySfx(PickNoRepeat(_bank.teleports, ref _lastTeleportIdx), _bank.teleportsVolume);
    }

    private void HandleBossExhausted(int phaseIndex)
    {
        StopFootsteps();
        if (_bank != null) PlaySfx(_bank.bodyFall, _bank.bodyFallVolume);
    }

    private void HandleBossDrawHit()
    {
        if (_bank != null) PlaySfx(PickNoRepeat(_bank.hitGrowls, ref _lastHitIdx), _bank.hitGrowlsVolume);
    }

    private void HandleBossDamaged(int phaseIndex, int hpRemaining)
    {
        if (_bank != null) PlaySfx(PickNoRepeat(_bank.damagedGrowls, ref _lastDamagedIdx), _bank.damagedGrowlsVolume);
    }

    private void HandleBossVulnerabilityExpired(int phaseIndex)
    {
        if (_bank != null) PlaySfx(_bank.vulnerabilityExpiredLaugh, _bank.vulnerabilityExpiredLaughVolume);
    }

    private void HandleBossDefeated()
    {
        StopFootsteps();
        if (_bank == null) return;
        PlaySfx(_bank.defeat, _bank.defeatVolume);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeOutBGM(_bank.bgmFadeOutSeconds);
            _bgmFadeOutRequested = true;
        }
    }

    private void StartFootsteps()
    {
        if (_footstepRoutine != null) StopCoroutine(_footstepRoutine);
        _footstepRoutine = StartCoroutine(FootstepLoop());
    }

    private void StopFootsteps()
    {
        if (_footstepRoutine != null)
        {
            StopCoroutine(_footstepRoutine);
            _footstepRoutine = null;
        }
    }

    private IEnumerator FootstepLoop()
    {
        while (true)
        {
            float interval = _bank != null ? _bank.footstepInterval : 0.45f;
            yield return new WaitForSeconds(interval);

            // Match PhaseBasedMovement.Pace gating: don't play footsteps
            // while the boss is mid-summon-animation or mid-hurt-pause —
            // it isn't visually moving, so it shouldn't sound like it is.
            bool gated =
                (_summonTicker != null && _summonTicker.IsPlayingSummonAnimation)
                || (_dmgFeedback != null && _dmgFeedback.IsHurtPaused);
            if (gated) continue;

            if (_bank != null)
                PlaySfx(PickNoRepeat(_bank.footsteps, ref _lastFootstepIdx), _bank.footstepsVolume);
        }
    }

    private AudioClip PickNoRepeat(AudioClip[] pool, ref int lastIdx)
    {
        if (pool == null || pool.Length == 0) return null;
        if (pool.Length == 1) { lastIdx = 0; return pool[0]; }
        int idx;
        do { idx = Random.Range(0, pool.Length); } while (idx == lastIdx);
        lastIdx = idx;
        return pool[idx];
    }

    private void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip, volumeScale);
    }
}
