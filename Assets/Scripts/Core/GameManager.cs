using UnityEngine;
using System.Collections.Generic;

public enum GameState { Idle, Playing, Paused, GameOver, LevelComplete, Practicing }

public class GameManager : Singleton<GameManager>
{
    public GameState CurrentState { get; private set; } = GameState.Idle;
    public LevelConfigSO CurrentLevel { get; private set; }

    /// <summary>Null-safe accessor for the active level config. Null when no GameManager/level is active.</summary>
    public static LevelConfigSO CurrentLevelConfig => Instance != null ? Instance.CurrentLevel : null;
    public int LastDefeatHearts { get; private set; }
    public BossController CurrentBoss { get; private set; }
    internal void SetCurrentBoss(BossController boss) => CurrentBoss = boss;

    // When true, drawing input is suppressed even while Playing/Practicing (e.g. a modal
    // level-start "New Character Unlocked!" reveal is open). General-purpose, not tutorial-scoped.
    private bool _drawingSuppressed;

    public bool AcceptsDrawingInput =>
        (CurrentState == GameState.Playing || CurrentState == GameState.Practicing) && !_drawingSuppressed;

    /// <summary>Suppress/allow drawing input regardless of game state. Callers must always release it.</summary>
    public void SuppressDrawingInput(bool suppressed) => _drawingSuppressed = suppressed;

    // GameState.Paused has two independent sources — the player's pause menu and a
    // dialogue/cutscene beat — and the state alone cannot tell them apart. Without
    // this split, Resume lifts a dialogue pause (dropping the player into gameplay
    // underneath an open dialogue box) and ExitDialoguePause lifts a user pause.
    // Invariant, enforced in SetState: a latch is only ever set while Paused.
    private bool _userPauseActive;
    private bool _dialoguePauseActive;

    // Set for the duration of a level-attempt teardown (restart / leave level) so a
    // late defeat or completion event cannot re-enter an attempt that is being
    // discarded. Cleared when the next attempt starts.
    private bool _attemptAbortInProgress;

    /// <summary>True while the current level attempt is being torn down (SALIN-141).</summary>
    public bool IsAttemptAbortInProgress => _attemptAbortInProgress;

private bool _hasPausedRunSnapshot;
    private int _pausedRunLevelId = -1;
    private int _pausedRunHearts = -1;
    private int _pausedRunWaveIndex = -1;
    private int _pausedRunWaveSpawnedCount = 0;
    private readonly List<PausedEnemySnapshot> _pausedEnemies = new();
    private GameState _stateBeforeDialogue;

    public readonly struct PausedEnemySnapshot
    {
        public PausedEnemySnapshot(
            EnemyDataSO enemyData,
            BaybayinCharacterSO character,
            Vector3 position,
            int currentHealth)
        {
            EnemyData = enemyData;
            Character = character;
            Position = position;
            CurrentHealth = currentHealth;
        }

        public EnemyDataSO EnemyData { get; }
        public BaybayinCharacterSO Character { get; }
        public Vector3 Position { get; }
        public int CurrentHealth { get; }
    }

    protected override void Awake() => base.Awake();

    private void OnEnable()
    {
        EventBus.OnGameOver += HandleGameOver;
        EventBus.OnLevelComplete += HandleLevelComplete;
    }

    private void OnDisable()
    {
        EventBus.OnGameOver -= HandleGameOver;
        EventBus.OnLevelComplete -= HandleLevelComplete;
    }

    public void StartGame()
    {
        _attemptAbortInProgress = false;
        Time.timeScale = 1f;
        SetState(GameState.Playing);
    }

    public void EnterPractice()
    {
        _attemptAbortInProgress = false;
        Time.timeScale = 1f;
        SetState(GameState.Practicing);
    }

    public void ExitPractice()
    {
        if (CurrentState != GameState.Practicing) return;
        SetState(GameState.Idle);
    }

    public void PauseGame()
    {
        if (_userPauseActive || _dialoguePauseActive) return;
        if (CurrentState != GameState.Playing) return;
        _userPauseActive = true;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
        EventBus.RaiseGamePaused();
    }

    public void ResumeGame()
    {
        // Only the user's own pause is liftable here. Resuming a dialogue-driven
        // pause would restart combat underneath an open dialogue box.
        if (!_userPauseActive || CurrentState != GameState.Paused) return;
        _userPauseActive = false;
        Time.timeScale = 1f;
        SetState(GameState.Playing);
        EventBus.RaiseGameResumed();
    }

    public void EnterDialoguePause()
    {
        if (_userPauseActive || _dialoguePauseActive) return;
        if (CurrentState != GameState.Playing && CurrentState != GameState.LevelComplete) return;
        _dialoguePauseActive = true;
        _stateBeforeDialogue = CurrentState;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
    }

    public void ExitDialoguePause()
    {
        // Mirror of ResumeGame: a beat unwinding must not lift the player's pause.
        if (!_dialoguePauseActive || CurrentState != GameState.Paused) return;
        _dialoguePauseActive = false;
        Time.timeScale = 1f;
        SetState(_stateBeforeDialogue);
    }

    /// <summary>
    /// SALIN-141. Ends the current level attempt as a transaction: every pause source
    /// is released, drawing suppression is dropped, the clock is restored, and one
    /// <see cref="EventBus.OnLevelAttemptAborted"/> is raised so each system that owns
    /// per-attempt state tears it down while the gameplay objects still exist.
    /// Idempotent. Deliberately does NOT touch the paused-run snapshot — restart and
    /// leave have different snapshot policies and decide that at their own call site.
    /// </summary>
    public void AbortCurrentLevelAttempt()
    {
        if (_attemptAbortInProgress) return;
        _attemptAbortInProgress = true;

        _userPauseActive = false;
        _dialoguePauseActive = false;
        _drawingSuppressed = false;
        _stateBeforeDialogue = GameState.Idle;
        Time.timeScale = 1f;
        SetState(GameState.Idle);

        DebugLogger.Log("GameManager: Level attempt aborted. No progress will be committed.");
        EventBus.RaiseLevelAttemptAborted();
    }

    private void HandleGameOver()
    {
        // A straggler defeat from an attempt already being torn down must not
        // re-open the defeat flow on top of the abort.
        if (_attemptAbortInProgress) return;

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        LastDefeatHearts = heartSystem != null ? heartSystem.GetCurrentHearts() : 0;

        ClearPausedRunSnapshot();
        SetState(GameState.GameOver);
        DebugLogger.Log("GameManager: GameOver state set. Defeat overlay will handle UI.");
    }

    private void HandleLevelComplete()
    {
        // An aborted attempt can never complete; see HandleGameOver.
        if (_attemptAbortInProgress) return;

        ClearPausedRunSnapshot();
        SetState(GameState.LevelComplete);
    }

    private void SetState(GameState newState)
    {
        if (newState == GameState.GameOver || newState == GameState.LevelComplete)
            _drawingSuppressed = false;

        // Keeps the pause latches derived from state: leaving Paused by any route
        // (StartGame after a teaching beat, defeat, completion, abort) releases both,
        // so neither source can strand a latch and block the next PauseGame.
        if (newState != GameState.Paused)
        {
            _userPauseActive = false;
            _dialoguePauseActive = false;
        }

        CurrentState = newState;
        DebugLogger.Log($"GameState -> {newState}");
    }

    public void SetLevel(LevelConfigSO level)
    {
        CurrentLevel = level;
        DebugLogger.Log($"CurrentLevel -> {level?.name ?? "null"}");
    }

    public void CachePausedRunSnapshot(
        int levelId,
        int currentHearts,
        int currentWaveIndex = -1,
        int currentWaveSpawnedCount = 0,
        IReadOnlyList<Enemy> activeEnemies = null)
    {
        if (levelId <= 0)
        {
            DebugLogger.LogWarning("GameManager: Cannot cache paused run snapshot with invalid level id.");
            return;
        }

        _hasPausedRunSnapshot = true;
        _pausedRunLevelId = levelId;
        _pausedRunHearts = Mathf.Max(0, currentHearts);
        _pausedRunWaveIndex = currentWaveIndex;
        _pausedRunWaveSpawnedCount = Mathf.Max(0, currentWaveSpawnedCount);
        _pausedEnemies.Clear();

        if (activeEnemies != null)
        {
            // PausedEnemySnapshot carries no identity and WaveManager restores enemies in list
            // order. Capture by threat distance so a resumed run re-derives the same mark.
            List<Enemy> ordered = new List<Enemy>(activeEnemies.Count);
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                Enemy enemy = activeEnemies[i];
                if (enemy == null || enemy.Data == null)
                    continue;

                ordered.Add(enemy);
            }

            ordered.Sort((left, right) =>
            {
                int byDistance = left.transform.position.y.CompareTo(right.transform.position.y);
                return byDistance != 0
                    ? byDistance
                    : left.SpawnSequence.CompareTo(right.SpawnSequence);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                Enemy enemy = ordered[i];
                _pausedEnemies.Add(new PausedEnemySnapshot(
                    enemy.Data,
                    enemy.Character,
                    enemy.transform.position,
                    enemy.CurrentHealth));
            }
        }

        DebugLogger.Log(
            $"GameManager: Cached paused run snapshot for level {levelId} "
            + $"at wave {_pausedRunWaveIndex} after {_pausedRunWaveSpawnedCount} spawned "
            + $"with {_pausedRunHearts} hearts "
            + $"and {_pausedEnemies.Count} enemies.");
    }

    public bool TryConsumePausedRunHearts(int levelId, int maxHearts, out int restoredHearts)
    {
        restoredHearts = maxHearts;

        if (!_hasPausedRunSnapshot)
            return false;

        bool shouldRestore = _pausedRunLevelId == levelId;
        if (shouldRestore)
        {
            restoredHearts = Mathf.Clamp(_pausedRunHearts, 0, maxHearts);
        }

        return shouldRestore;
    }

    public bool TryGetPausedRunEnemies(int levelId, out IReadOnlyList<PausedEnemySnapshot> enemies)
    {
        enemies = _pausedEnemies;

        return _hasPausedRunSnapshot
            && _pausedRunLevelId == levelId;
    }

    public bool TryGetPausedRunWaveIndex(int levelId, out int waveIndex)
    {
        waveIndex = _pausedRunWaveIndex;

        return _hasPausedRunSnapshot
            && _pausedRunLevelId == levelId
            && _pausedRunWaveIndex >= 0;
    }

    public bool TryGetPausedRunWaveProgress(int levelId, out int waveIndex, out int spawnedCount)
    {
        waveIndex = _pausedRunWaveIndex;
        spawnedCount = _pausedRunWaveSpawnedCount;

        return _hasPausedRunSnapshot
            && _pausedRunLevelId == levelId
            && _pausedRunWaveIndex >= 0;
    }

    public void ClearPausedRunSnapshotForLevel(int levelId)
    {
        if (_hasPausedRunSnapshot && _pausedRunLevelId == levelId)
            ClearPausedRunSnapshot();
    }

    public void DiscardPausedRunSnapshot()
    {
        ClearPausedRunSnapshot();
    }

    public bool TryGetPausedRunLevelId(out int levelId)
    {
        levelId = _pausedRunLevelId;
        return _hasPausedRunSnapshot && _pausedRunLevelId > 0;
    }

    private void ClearPausedRunSnapshot()
    {
        _hasPausedRunSnapshot = false;
        _pausedRunLevelId = -1;
        _pausedRunHearts = -1;
        _pausedRunWaveIndex = -1;
        _pausedRunWaveSpawnedCount = 0;
        _pausedEnemies.Clear();
    }
}
