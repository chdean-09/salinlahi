#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _quitButton;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnGamePaused += Show;
        EventBus.OnGameResumed += Hide;

        if (_resumeButton != null)
            _resumeButton.onClick.AddListener(OnResumePressed);
        if (_quitButton != null)
            _quitButton.onClick.AddListener(OnQuitPressed);
    }

    private void OnDisable()
    {
        EventBus.OnGamePaused -= Show;
        EventBus.OnGameResumed -= Hide;

        if (_resumeButton != null)
            _resumeButton.onClick.RemoveListener(OnResumePressed);
        if (_quitButton != null)
            _quitButton.onClick.RemoveListener(OnQuitPressed);
    }

    private void Show()
    {
        if (_panel != null) _panel.SetActive(true);
    }

    private void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnResumePressed()
    {
        GameManager.Instance.ResumeGame();
    }

    private void OnQuitPressed()
    {
        if (GameManager.Instance != null)
        {
            if (ShouldCachePausedRunSnapshot())
            {
                HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
                if (heartSystem != null)
                {
                    int selectedLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
                    var activeEnemies = ActiveEnemyTracker.Instance != null
                        ? ActiveEnemyTracker.Instance.GetActiveEnemiesSnapshot()
                        : null;
                    WaveManager waveManager = FindFirstObjectByType<WaveManager>();
                    int currentWaveIndex = waveManager != null ? waveManager.CurrentWaveIndex : -1;
                    int currentWaveSpawnedCount = waveManager != null ? waveManager.CurrentWaveSpawnedCount : 0;

                    GameManager.Instance.CachePausedRunSnapshot(
                        selectedLevel,
                        heartSystem.GetCurrentHearts(),
                        currentWaveIndex,
                        currentWaveSpawnedCount,
                        activeEnemies);
                }
            }
            else
            {
                GameManager.Instance.DiscardPausedRunSnapshot();
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            GameManager.Instance.ResumeGame();

        // SceneLoader.LoadRoutine always resets Time.timeScale at the start of every load.
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
        {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
            SandboxMode.Deactivate();
#endif
            EnemyPool.Instance?.ReturnAllCheckedOut();
            DebugLogger.LogWarning(
                "PauseMenuUI: SceneLoader not available. Loading MainMenu directly. "
                + "Open from Bootstrap for normal transitions.");
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }

    public static bool ShouldCachePausedRunSnapshot()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        return !SandboxMode.IsActive;
#else
        return true;
#endif
    }
}
