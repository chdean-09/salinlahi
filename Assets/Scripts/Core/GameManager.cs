using Salinlahi.Debug.Sandbox;
using UnityEngine;

public enum GameState { Idle, Playing, Paused, GameOver, LevelComplete }

public class GameManager : Singleton<GameManager>
{
    public GameState CurrentState { get; private set; } = GameState.Idle;
    public LevelConfigSO CurrentLevel { get; private set; }

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
        if (SandboxMode.IsActive)
        {
            DebugLogger.Log("GameManager: Ignored GameOver while sandbox mode is active.");
            return;
        }

        SetState(GameState.GameOver);
        SceneLoader.Instance.LoadGameOver();
    }

    private void HandleLevelComplete()
    {
        if (SandboxMode.IsActive)
        {
            DebugLogger.Log("GameManager: Ignored LevelComplete while sandbox mode is active.");
            return;
        }

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
}
