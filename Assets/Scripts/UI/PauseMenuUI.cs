using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private SettingsPanel _settingsPanel;

    [Header("Level Exit Controls")]
    [SerializeField] private Button _restartButton;
    [SerializeField] private GameObject _confirmationPanel;
    [SerializeField] private Button _confirmationConfirmButton;
    [SerializeField] private Button _confirmationCancelButton;

    private enum PendingAction { None, Restart, Leave }

    private PendingAction _pendingAction;
    private bool _transitionRequested;

    private void Awake()
    {
        ValidateReferences();
        Hide();
        HideConfirmation();
    }

    private void ValidateReferences()
    {
        if (_panel == null
            || _resumeButton == null
            || _quitButton == null
            || _restartButton == null
            || _confirmationPanel == null
            || _confirmationConfirmButton == null
            || _confirmationCancelButton == null)
        {
            DebugLogger.LogError(
                "PauseMenuUI: Pause, restart, and confirmation references must be wired in the scene.");
        }
    }

    private void OnEnable()
    {
        EventBus.OnGamePaused += Show;
        EventBus.OnGameResumed += Hide;
        EventBus.OnLevelAttemptAborted += HideAll;

        if (_resumeButton != null)
            _resumeButton.onClick.AddListener(OnResumePressed);
        if (_quitButton != null)
            _quitButton.onClick.AddListener(OnLeavePressed);
        if (_restartButton != null)
            _restartButton.onClick.AddListener(OnRestartPressed);
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OnSettingsPressed);
        if (_confirmationConfirmButton != null)
            _confirmationConfirmButton.onClick.AddListener(OnConfirmationConfirmed);
        if (_confirmationCancelButton != null)
            _confirmationCancelButton.onClick.AddListener(OnConfirmationCancelled);
    }

    private void OnDisable()
    {
        EventBus.OnGamePaused -= Show;
        EventBus.OnGameResumed -= Hide;
        EventBus.OnLevelAttemptAborted -= HideAll;

        if (_resumeButton != null)
            _resumeButton.onClick.RemoveListener(OnResumePressed);
        if (_quitButton != null)
            _quitButton.onClick.RemoveListener(OnLeavePressed);
        if (_restartButton != null)
            _restartButton.onClick.RemoveListener(OnRestartPressed);
        if (_settingsButton != null)
            _settingsButton.onClick.RemoveListener(OnSettingsPressed);
        if (_confirmationConfirmButton != null)
            _confirmationConfirmButton.onClick.RemoveListener(OnConfirmationConfirmed);
        if (_confirmationCancelButton != null)
            _confirmationCancelButton.onClick.RemoveListener(OnConfirmationCancelled);
    }

    private void Show()
    {
        _transitionRequested = false;
        HideConfirmation();
        if (_panel != null) _panel.SetActive(true);
    }

    private void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
        HideConfirmation();
    }

    private void HideAll()
    {
        Hide();
        _pendingAction = PendingAction.None;
    }

    private void OnResumePressed()
    {
        if (_transitionRequested) return;
        AudioManager.Instance?.PlayMenuButtonClick();
        GameManager.Instance?.ResumeGame();
    }

    private void OnRestartPressed()
    {
        if (_transitionRequested) return;
        AudioManager.Instance?.PlayMenuButtonClick();
        ShowConfirmation(PendingAction.Restart);
    }

    private void OnLeavePressed()
    {
        if (_transitionRequested) return;
        AudioManager.Instance?.PlayMenuExitButtonClick();
        ShowConfirmation(PendingAction.Leave);
    }

    private void ShowConfirmation(PendingAction action)
    {
        _pendingAction = action;
        if (_panel != null) _panel.SetActive(false);
        if (_confirmationPanel != null)
        {
            _confirmationPanel.SetActive(true);
        }
        else
        {
            // A scene without the legacy pause panel cannot present a confirmation.
            // Keep the action safe by requiring an explicitly wired confirmation view.
            DebugLogger.LogWarning("PauseMenuUI: Confirmation panel is not available.");
            _pendingAction = PendingAction.None;
            Show();
        }
    }

    private void HideConfirmation()
    {
        if (_confirmationPanel != null)
            _confirmationPanel.SetActive(false);
    }

    private void OnConfirmationCancelled()
    {
        if (_transitionRequested) return;
        _pendingAction = PendingAction.None;
        HideConfirmation();
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            Show();
    }

    private void OnConfirmationConfirmed()
    {
        if (_transitionRequested || _pendingAction == PendingAction.None)
            return;

        _transitionRequested = true;
        PendingAction action = _pendingAction;
        _pendingAction = PendingAction.None;
        HideAll();

        if (SceneLoader.Instance == null)
        {
            _transitionRequested = false;
            DebugLogger.LogError("PauseMenuUI: SceneLoader is required for level lifecycle transitions.");
            return;
        }

        if (action == PendingAction.Restart)
            SceneLoader.Instance.RestartCurrentLevel();
        else
            SceneLoader.Instance.LeaveToLevelSelect();
    }

    private void OnSettingsPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("PauseMenuUI: Settings pressed");
        if (_settingsPanel != null)
            _settingsPanel.Show();
    }

    // Retained for sandbox/editor tooling and the shared resume-flow contract.
    // SALIN-141 Leave Level does not use this path; it explicitly discards the attempt.
    public static bool ShouldCachePausedRunSnapshot()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss != null)
            return false;

#if UNITY_EDITOR || SALINLAHI_SANDBOX
        return !SandboxMode.IsActive;
#else
        return true;
#endif
    }
}
