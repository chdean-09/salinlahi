using UnityEngine;
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
            HeartSystem heartSystem = FindObjectOfType<HeartSystem>();
            if (heartSystem != null)
            {
                int selectedLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
                GameManager.Instance.CachePausedRunSnapshot(selectedLevel, heartSystem.GetCurrentHearts());
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            GameManager.Instance.ResumeGame();

        // SceneLoader.LoadRoutine always resets Time.timeScale at the start of every load.
        SceneLoader.Instance.LoadMainMenu();
    }
}
