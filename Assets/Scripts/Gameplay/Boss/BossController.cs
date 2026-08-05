using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BossEnemy))]
public class BossController : MonoBehaviour
{
    private enum State { Idle, Intro, SummoningPhase, WindingDown, Vulnerable, Damaged, Outro, Defeated }

    public BossConfigSO Config { get; private set; }
    public int CurrentPhaseIndex { get; private set; } = -1;
    public BossPhase CurrentPhase =>
        (Config != null && CurrentPhaseIndex >= 0 && CurrentPhaseIndex < Config.phases.Count)
            ? Config.phases[CurrentPhaseIndex]
            : null;
    public int HPRemaining { get; private set; }
    public bool IsTargetable => _state == State.Vulnerable && _isVulnerableActiveWindow;
    public bool IsDefeated { get; private set; }

    public string CurrentExpectedCharacterID =>
        _currentExpectedCharacter != null ? _currentExpectedCharacter.characterID : null;
    public virtual BaybayinCharacterSO CurrentExpectedCharacter => _currentExpectedCharacter;
    public virtual int CorrectDrawsThisWindow => _correctDrawsThisWindow;
    public virtual int RequiredCharactersForCurrentPhase =>
        CurrentPhase != null ? CurrentPhase.requiredCharacterCount : 0;

    public event Action OnDrawnThisPhaseChanged;

    protected void RaiseOnDrawnThisPhaseChanged() => OnDrawnThisPhaseChanged?.Invoke();

    private State _state = State.Idle;
    private bool _isVulnerableActiveWindow;
    private WaveSpawner _spawner;
    private BossSummonTicker _summonTicker;
    private BossStateVisuals _stateVisuals;
    private PhaseBasedMovement _phaseMovement;
    private Coroutine _stateRoutine;
    private BaybayinCharacterSO _currentExpectedCharacter;
    private int _correctDrawsThisWindow;

    public void StartBoss(BossConfigSO config, WaveSpawner spawner)
    {
        if (config == null)
        {
            DebugLogger.LogError("BossController.StartBoss: config is null. Aborting.");
            return;
        }
        if (spawner == null)
        {
            DebugLogger.LogError("BossController.StartBoss: spawner is null. Aborting.");
            return;
        }
        if (config.phases == null || config.phases.Count == 0)
        {
            DebugLogger.LogError("BossController.StartBoss: config has no phases. Aborting.");
            return;
        }

        Config = config;
        _spawner = spawner;
        _summonTicker = GetComponent<BossSummonTicker>();
        _stateVisuals = GetComponent<BossStateVisuals>();
        _phaseMovement = GetComponent<PhaseBasedMovement>();

        HPRemaining = config.phases.Count;
        IsDefeated = false;
        CurrentPhaseIndex = -1;
        _state = State.Idle;
        _isVulnerableActiveWindow = false;
        _currentExpectedCharacter = null;
        _correctDrawsThisWindow = 0;

        if (GameManager.Instance != null)
            GameManager.Instance.SetCurrentBoss(this);

        EventBus.RaiseBossStarted(config);

        if (_stateRoutine != null)
            StopCoroutine(_stateRoutine);
        _stateRoutine = StartCoroutine(RunEncounter());
    }

    private void OnDisable()
    {
        EventBus.OnLevelAttemptAborted -= HandleLevelAttemptAborted;
        if (_stateRoutine != null)
        {
            StopCoroutine(_stateRoutine);
            _stateRoutine = null;
        }
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss == this)
            GameManager.Instance.SetCurrentBoss(null);
    }

    private void OnEnable()
    {
        EventBus.OnLevelAttemptAborted += HandleLevelAttemptAborted;
    }

    private void HandleLevelAttemptAborted()
    {
        if (_stateRoutine != null)
        {
            StopCoroutine(_stateRoutine);
            _stateRoutine = null;
        }

        IsDefeated = true;
        _state = State.Defeated;
        _isVulnerableActiveWindow = false;
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss == this)
            GameManager.Instance.SetCurrentBoss(null);
    }

    public BossRouteResult TryRouteDraw(string characterID)
    {
        if (!IsTargetable || _currentExpectedCharacter == null)
            return BossRouteResult.NotRouted;

        if (characterID == _currentExpectedCharacter.characterID)
        {
            _correctDrawsThisWindow++;
            // Sample before notifying: UI subscribers read CurrentExpectedCharacter
            // in their handler and must see the next glyph, not the one just matched.
            SampleNextExpectedCharacter();
            RaiseOnDrawnThisPhaseChanged();
            EventBus.RaiseBossDrawHit();
            return BossRouteResult.Hit;
        }

        EventBus.RaiseDrawingFailed();
        return BossRouteResult.WrongGlyph;
    }

    private void SampleNextExpectedCharacter()
    {
        LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
        if (level == null
            || level.allowedCharacters == null
            || level.allowedCharacters.Count == 0)
        {
            DebugLogger.LogWarning("BossController: LevelConfigSO.allowedCharacters is empty — cannot sample glyph.");
            _currentExpectedCharacter = null;
            return;
        }
        int idx = UnityEngine.Random.Range(0, level.allowedCharacters.Count);
        _currentExpectedCharacter = level.allowedCharacters[idx];
    }

    private IEnumerator RunEncounter()
    {
        yield return RunIntro();

        for (int i = 0; i < Config.phases.Count; i++)
        {
            CurrentPhaseIndex = i;
            bool phaseCleared = false;

            while (!phaseCleared)
            {
                yield return RunSummoningPhase(i);
                yield return RunWindingDown(i);

                bool didDamage = false;
                yield return RunVulnerable(i, hit => didDamage = hit);

                if (didDamage)
                {
                    yield return RunDamaged(i);
                    phaseCleared = true;
                }
            }
        }

        yield return RunOutro();
    }

    private IEnumerator RunIntro()
    {
        _state = State.Intro;
        yield return new WaitForSeconds(Mathf.Max(0f, Config.introDuration));
    }

    private IEnumerator RunSummoningPhase(int i)
    {
        BossPhase phase = Config.phases[i];
        _state = State.SummoningPhase;
        EventBus.RaiseBossPhaseStarted(i);

        if (_phaseMovement != null)
            _phaseMovement.StartPattern(phase);

        if (phase.delayBetweenSummons > 0f && phase.summonPhaseDuration > 0f)
        {
            float elapsed = 0f;
            float nextTickAt = phase.delayBetweenSummons;
            while (elapsed < phase.summonPhaseDuration)
            {
                if (elapsed >= nextTickAt)
                {
                    if (phase.movementPattern == BossMovementPattern.Teleport
                        && _phaseMovement != null)
                    {
                        _phaseMovement.TeleportNow(phase);
                    }

                    if (_summonTicker != null)
                        yield return _summonTicker.PlayTickAndSpawn(phase, Config, _spawner);

                    nextTickAt += phase.delayBetweenSummons;
                }
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        if (_phaseMovement != null)
            _phaseMovement.StopPattern();
    }

    private IEnumerator RunWindingDown(int i)
    {
        _state = State.WindingDown;
        EventBus.RaiseBossExhausted(i);

        if (_stateVisuals != null)
            _stateVisuals.BeginPanting();

        yield return new WaitUntil(() =>
        {
            ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
            return tracker == null || !tracker.HasActiveNonBossEnemies;
        });
    }

    private IEnumerator RunVulnerable(int i, Action<bool> onComplete)
    {
        BossPhase phase = Config.phases[i];
        _state = State.Vulnerable;
        _isVulnerableActiveWindow = false;
        _correctDrawsThisWindow = 0;
        _currentExpectedCharacter = null;

        EventBus.RaiseBossVulnerable(i);

        if (_stateVisuals != null)
            yield return _stateVisuals.PlayCollapse();

        _isVulnerableActiveWindow = true;
        SampleNextExpectedCharacter();
        RaiseOnDrawnThisPhaseChanged();
        EventBus.RaiseBossVulnerabilityWindowActive(i);

        float elapsed = 0f;
        while (elapsed < phase.vulnerabilityTimer
            && _correctDrawsThisWindow < phase.requiredCharacterCount)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        _isVulnerableActiveWindow = false;

        if (_correctDrawsThisWindow >= phase.requiredCharacterCount)
        {
            onComplete?.Invoke(true);
        }
        else
        {
            EventBus.RaiseBossVulnerabilityExpired(i);
            if (_stateVisuals != null)
                yield return _stateVisuals.PlayStandUp();
            onComplete?.Invoke(false);
        }
    }

    private IEnumerator RunDamaged(int i)
    {
        _state = State.Damaged;
        HPRemaining--;
        EventBus.RaiseBossDamaged(i, HPRemaining);

        if (_stateVisuals != null)
            yield return _stateVisuals.PlayStandUp();
    }

    private IEnumerator RunOutro()
    {
        _state = State.Outro;
        IsDefeated = true;

        // Play the boss's death animation (if frames are configured on its
        // EnemyDataSO) before the outro buffer. The normal Enemy.Defeat path
        // is bypassed for the boss because BossEnemy.TakeDamage no-ops —
        // damage is gated by BossController — so this is the only path that
        // can drive the death frames for the boss.
        BossEnemy bossEnemy = GetComponent<BossEnemy>();
        if (bossEnemy != null)
            yield return bossEnemy.PlayDeathAnimationFrames();

        yield return new WaitForSeconds(Mathf.Max(0f, Config.outroDuration));

        _state = State.Defeated;
        _stateRoutine = null;  // Clear before ReturnToPool triggers OnDisable
        EventBus.RaiseBossDefeated();
        EventBus.RaiseLevelComplete();

        if (bossEnemy != null)
            bossEnemy.ReturnToPool();
    }
}

public enum BossRouteResult
{
    NotRouted,    // boss not targetable; caller falls through to AOE/closest-match
    Hit,          // correct glyph drawn during Vulnerable; advances queue
    WrongGlyph    // incorrect glyph drawn during Vulnerable; consumed (no fall-through)
}
