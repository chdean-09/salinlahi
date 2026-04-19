using UnityEngine;
using System.Collections.Generic;

public enum GameState { Idle, Playing, Paused, GameOver, LevelComplete }

public class GameManager : Singleton<GameManager>
{
    public GameState CurrentState { get; private set; } = GameState.Idle;
    public LevelConfigSO CurrentLevel { get; private set; }

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
        Time.timeScale = 1f;
        SetState(GameState.Playing);
    }

    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
        EventBus.RaiseGamePaused();
    }

    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;
        Time.timeScale = 1f;
        SetState(GameState.Playing);
        EventBus.RaiseGameResumed();
    }

    public void EnterDialoguePause()
    {
        if (CurrentState != GameState.Playing) return;
        _stateBeforeDialogue = CurrentState;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
    }

    public void ExitDialoguePause()
    {
        if (CurrentState != GameState.Paused) return;
        Time.timeScale = 1f;
        SetState(_stateBeforeDialogue);
    }

    private void HandleGameOver()
    {
        ClearPausedRunSnapshot();
        SetState(GameState.GameOver);
        SceneLoader.Instance.LoadGameOver();
    }

    private void HandleLevelComplete()
    {
        ClearPausedRunSnapshot();
        SetState(GameState.LevelComplete);
    }

    private void SetState(GameState newState)
    {
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
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                Enemy enemy = activeEnemies[i];
                if (enemy == null || enemy.Data == null)
                    continue;

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
