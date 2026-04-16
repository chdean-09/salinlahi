using UnityEngine;

public enum GameState { Idle, Playing, Paused, GameOver, LevelComplete }

public class GameManager : Singleton<GameManager>
{
    public GameState CurrentState { get; private set; } = GameState.Idle;
    public LevelConfigSO CurrentLevel { get; private set; }

    private bool _hasPausedRunSnapshot;
    private int _pausedRunLevelId = -1;
    private int _pausedRunHearts = -1;

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

    public void CachePausedRunSnapshot(int levelId, int currentHearts)
    {
        if (levelId <= 0)
        {
            DebugLogger.LogWarning("GameManager: Cannot cache paused run snapshot with invalid level id.");
            return;
        }

        _hasPausedRunSnapshot = true;
        _pausedRunLevelId = levelId;
        _pausedRunHearts = Mathf.Max(0, currentHearts);
        DebugLogger.Log($"GameManager: Cached paused run snapshot for level {levelId} with {_pausedRunHearts} hearts.");
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
            ClearPausedRunSnapshot();
        }

        return shouldRestore;
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
    }
}
