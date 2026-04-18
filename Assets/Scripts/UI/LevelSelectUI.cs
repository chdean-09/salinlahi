using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level Select UI with lock/unlock and completion states.
/// Reads progress from ProgressManager. Completion check shown when IsLevelCompleted returns true;
/// the 3-star visual is intentionally deferred to SALIN-66 polish pass.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("Level Configs")]
    [SerializeField] private LevelConfigSO _level1Config;
    [SerializeField] private LevelConfigSO _level2Config;
    [SerializeField] private LevelConfigSO _level3Config;
    [SerializeField] private LevelConfigSO _level4Config;
    [SerializeField] private LevelConfigSO _level5Config;

    [Header("Level Buttons")]
    [SerializeField] private Button _level1Button;
    [SerializeField] private Button _level2Button;
    [SerializeField] private Button _level3Button;
    [SerializeField] private Button _level4Button;
    [SerializeField] private Button _level5Button;

    [Header("Lock Overlays")]
    [SerializeField] private GameObject _level1LockOverlay;
    [SerializeField] private GameObject _level2LockOverlay;
    [SerializeField] private GameObject _level3LockOverlay;
    [SerializeField] private GameObject _level4LockOverlay;
    [SerializeField] private GameObject _level5LockOverlay;

    [Header("Completion Checkmarks (hidden until level is beaten)")]
    [SerializeField] private GameObject _level1CompletionCheck;
    [SerializeField] private GameObject _level2CompletionCheck;
    [SerializeField] private GameObject _level3CompletionCheck;
    [SerializeField] private GameObject _level4CompletionCheck;
    [SerializeField] private GameObject _level5CompletionCheck;

    [Header("Navigation")]
    [SerializeField] private Button _backButton;

    private Button[] _levelButtons;
    private GameObject[] _lockOverlays;
    private GameObject[] _completionChecks;
    private LevelConfigSO[] _levelConfigs;

    private void Awake()
    {
        _levelConfigs = new[] { _level1Config, _level2Config, _level3Config, _level4Config, _level5Config };
        _levelButtons = new[] { _level1Button, _level2Button, _level3Button, _level4Button, _level5Button };
        _lockOverlays = new[] { _level1LockOverlay, _level2LockOverlay, _level3LockOverlay, _level4LockOverlay, _level5LockOverlay };
        _completionChecks = new[] { _level1CompletionCheck, _level2CompletionCheck, _level3CompletionCheck, _level4CompletionCheck, _level5CompletionCheck };
    }

    private void Start()
    {
        RefreshLevelButtons();

        for (int i = 0; i < _levelButtons.Length; i++)
        {
            int levelNumber = i + 1; // capture for closure
            if (_levelButtons[i] != null)
                _levelButtons[i].onClick.AddListener(() => OnLevelSelected(levelNumber));
        }

        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackPressed);

        DebugLogger.Log("LevelSelectUI: Initialized");
    }

    private void OnDestroy()
    {
        if (_levelButtons != null)
        {
            foreach (var button in _levelButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
        }

        if (_backButton != null)
            _backButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Refreshes all level buttons based on current progress.
    /// Public so other systems can trigger a refresh after progress changes.
    /// </summary>
    public void RefreshLevelButtons()
    {
        for (int i = 0; i < _levelButtons.Length; i++)
        {
            UpdateLevelButtonState(i + 1);
        }
    }

    private void UpdateLevelButtonState(int levelNumber)
    {
        int index = levelNumber - 1;
        if (index < 0 || index >= _levelButtons.Length)
            return;

        Button button = _levelButtons[index];
        if (button == null)
            return;

        GameObject lockOverlay = index < _lockOverlays.Length ? _lockOverlays[index] : null;
        GameObject completionCheck = index < _completionChecks.Length ? _completionChecks[index] : null;

        bool unlocked = true;
        bool completed = false;

        if (ProgressManager.Instance != null)
        {
            unlocked = ProgressManager.Instance.IsLevelUnlocked(levelNumber);
            completed = ProgressManager.Instance.IsLevelCompleted(levelNumber);
        }
        else
        {
            DebugLogger.LogWarning("LevelSelectUI: ProgressManager not available. Defaulting all levels to unlocked.");
        }

        button.interactable = unlocked;

        if (lockOverlay != null)
            lockOverlay.SetActive(!unlocked);

        if (completionCheck != null)
            completionCheck.SetActive(unlocked && completed);
    }

    private void OnLevelSelected(int levelNumber)
    {
        // Defensive check: guard against stale button state (e.g. mid-refresh).
        if (ProgressManager.Instance != null && !ProgressManager.Instance.IsLevelUnlocked(levelNumber))
        {
            DebugLogger.Log($"LevelSelectUI: Level {levelNumber} is locked. Ignoring click.");
            return;
        }

        DebugLogger.Log($"LevelSelectUI: Level {levelNumber} selected");

        PlayerPrefs.SetInt("SelectedLevel", levelNumber);
        PlayerPrefs.Save();

        int index = levelNumber - 1;
        if (GameManager.Instance != null && index >= 0 && index < _levelConfigs.Length && _levelConfigs[index] != null)
        {
            GameManager.Instance.DiscardPausedRunSnapshot();
            GameManager.Instance.SetLevel(_levelConfigs[index]);
        }
        else
        {
            DebugLogger.LogWarning($"LevelSelectUI: Could not set GameManager level for level {levelNumber} — config missing or GameManager unavailable.");
        }

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("LevelSelectUI: SceneLoader not available. Cannot load Gameplay.");
    }

    private void OnBackPressed()
    {
        DebugLogger.Log("LevelSelectUI: Back to main menu");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
            DebugLogger.LogError("LevelSelectUI: SceneLoader not available. Cannot load MainMenu.");
    }
}
