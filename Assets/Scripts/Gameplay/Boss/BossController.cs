using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Single MonoBehaviour state machine that drives a boss encounter.
// Co-located with BossEnemy on the boss prefab.
//
// Lifecycle:
//   WaveManager.RunBossEncounter spawns the boss Enemy via WaveSpawner,
//   gets BossController via GetComponent, and calls StartBoss(config, spawner).
//   StartBoss is the lifecycle entry point — OnEnable does NOT begin the
//   encounter, because at OnEnable the controller has no config yet.
//
// States: Intro -> PhaseActive -> [PhaseClearedIntermission ->] PhaseActive -> ... -> Outro -> Defeated
//
// Pause: All coroutines use WaitForSeconds (scaled time). When GameManager
// calls Time.timeScale = 0 the encounter halts automatically.
// DO NOT use WaitForSecondsRealtime in this subsystem.
[RequireComponent(typeof(BossEnemy))]
public class BossController : MonoBehaviour
{
    private enum State { Idle, Intro, PhaseActive, PhaseClearedIntermission, Outro, Defeated }

    public BossConfigSO Config { get; private set; }
    public BossPhase CurrentPhase { get; private set; }
    public int CurrentPhaseIndex { get; private set; } = -1;
    public bool IsTargetable => _state == State.PhaseActive;
    public bool IsDefeated { get; private set; }
    public IReadOnlyList<BaybayinCharacterSO> RequiredCharacters =>
        CurrentPhase != null ? CurrentPhase.requiredCharacters : null;
    public IReadOnlyCollection<BaybayinCharacterSO> DrawnThisPhase => _drawnThisPhase;

    // Local event — fired on every successful Hit. UI listens for per-icon
    // grey-out. Kept local because subscribers need the controller-instance
    // handle to read DrawnThisPhase / RequiredCharacters mid-phase.
    public event Action OnDrawnThisPhaseChanged;

    private State _state = State.Idle;
    private WaveSpawner _spawner;
    private readonly HashSet<BaybayinCharacterSO> _drawnThisPhase = new();
    private Coroutine _stateRoutine;

    // ---- Lifecycle ----

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
        IsDefeated = false;
        CurrentPhaseIndex = -1;
        CurrentPhase = null;
        _drawnThisPhase.Clear();

        // Set CurrentBoss BEFORE raising OnBossStarted so subscribers
        // resolving GameManager.Instance.CurrentBoss in the handler see this
        // controller, not null.
        if (GameManager.Instance != null)
            GameManager.Instance.SetCurrentBoss(this);

        EventBus.RaiseBossStarted(config);

        if (_stateRoutine != null)
            StopCoroutine(_stateRoutine);
        _stateRoutine = StartCoroutine(RunEncounter());
    }

    private void OnDisable()
    {
        if (_stateRoutine != null)
        {
            StopCoroutine(_stateRoutine);
            _stateRoutine = null;
        }
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss == this)
            GameManager.Instance.SetCurrentBoss(null);
    }

    // ---- Hit routing ----

    public BossRouteResult TryRouteDraw(string characterID)
    {
        if (!IsTargetable || CurrentPhase == null)
            return BossRouteResult.NotRouted;
        if (CurrentPhase.requiredCharacters == null
            || CurrentPhase.requiredCharacters.Count == 0)
            return BossRouteResult.NotRouted;

        BaybayinCharacterSO matched = null;
        for (int i = 0; i < CurrentPhase.requiredCharacters.Count; i++)
        {
            BaybayinCharacterSO so = CurrentPhase.requiredCharacters[i];
            if (so == null) continue;
            if (so.characterID == characterID)
            {
                matched = so;
                break;
            }
        }

        if (matched == null)
            return BossRouteResult.NotRouted;

        if (_drawnThisPhase.Contains(matched))
        {
            EventBus.RaiseDrawingFailed();
            return BossRouteResult.Duplicate;
        }

        _drawnThisPhase.Add(matched);
        OnDrawnThisPhaseChanged?.Invoke();

        int requiredCount = 0;
        for (int i = 0; i < CurrentPhase.requiredCharacters.Count; i++)
            if (CurrentPhase.requiredCharacters[i] != null) requiredCount++;

        if (_drawnThisPhase.Count >= requiredCount)
        {
            EventBus.RaiseBossPhaseCleared(CurrentPhaseIndex);
            // The state coroutine watches _drawnThisPhase.Count vs. requiredCount
            // on each frame and advances. Hit signal already raised.
        }

        return BossRouteResult.Hit;
    }

    // ---- State coroutine ----

    private IEnumerator RunEncounter()
    {
        // Intro
        _state = State.Intro;
        yield return new WaitForSeconds(Mathf.Max(0f, Config.introDuration));

        // Phases
        for (int i = 0; i < Config.phases.Count; i++)
        {
            CurrentPhaseIndex = i;
            CurrentPhase = Config.phases[i];
            _drawnThisPhase.Clear();

            _state = State.PhaseActive;
            EventBus.RaiseBossPhaseStarted(i);

            // Wait for the phase to clear (TryRouteDraw raises BossPhaseCleared
            // when the count is met; we observe the same condition here so
            // we don't depend on the order of subscriber invocation).
            yield return new WaitUntil(() =>
                _drawnThisPhase.Count >= CountNonNull(CurrentPhase.requiredCharacters));

            // Intermission (if configured AND this is not the final phase)
            bool isFinalPhase = (i == Config.phases.Count - 1);
            if (!isFinalPhase && CurrentPhase.intermissionWave != null)
            {
                _state = State.PhaseClearedIntermission;
                EventBus.RaiseBossIntermissionStarted();

                yield return StartCoroutine(_spawner.SpawnWave(CurrentPhase.intermissionWave));

                // Wait for adds to clear
                yield return new WaitUntil(() =>
                {
                    ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
                    return tracker == null || tracker.IsClear;
                });

                if (CurrentPhase.postIntermissionDelay > 0f)
                    yield return new WaitForSeconds(CurrentPhase.postIntermissionDelay);

                EventBus.RaiseBossIntermissionCleared();
            }
        }

        // Outro
        _state = State.Outro;
        IsDefeated = true;
        yield return new WaitForSeconds(Mathf.Max(0f, Config.outroDuration));

        _state = State.Defeated;
        EventBus.RaiseBossDefeated();
        EventBus.RaiseLevelComplete();

        // Return the boss Enemy to the pool. ResetForPool clears _data, so the
        // next encounter's spawn re-initializes cleanly.
        BossEnemy bossEnemy = GetComponent<BossEnemy>();
        if (bossEnemy != null)
            bossEnemy.ReturnToPool();

        _stateRoutine = null;
    }

    private static int CountNonNull(List<BaybayinCharacterSO> list)
    {
        if (list == null) return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) n++;
        return n;
    }
}

public enum BossRouteResult
{
    NotRouted,   // characterID not in current phase's required list — caller falls through to AOE/closest-match
    Hit,         // valid required character drawn for the first time this phase
    Duplicate    // required character already drawn this phase — consumed, raises OnDrawingFailed
}
