#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
using TMPro;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private const string SceneGameplay = "Gameplay";
    private const string SceneLevelSelect = "LevelSelect";

    [SerializeField] private Button _endlessModeButton;

    [Header("Overlay Panels")]
    [SerializeField] private SettingsPanel _settingsPanel;
    [SerializeField] private CreditsPanel _creditsPanel;

    private void Start()
    {
        if (_endlessModeButton != null)
            _endlessModeButton.interactable = IsStoryComplete();

        EnsureSandboxEntryPoint();
    }

    public void OnPlayButtonPressed()
    {
        DebugLogger.Log("MainMenuUI: Play button pressed");

        int selectedLevel = 1;
        if (GameManager.Instance != null
            && GameManager.Instance.TryGetPausedRunLevelId(out int pausedLevelId))
        {
            selectedLevel = pausedLevelId;
            DebugLogger.Log($"MainMenuUI: Resuming paused run on level {selectedLevel}.");
        }

        PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, selectedLevel);
        PlayerPrefs.Save();

        if (GameManager.Instance != null)
            GameManager.Instance.SetLevel(null);

        LoadGameplay();
    }

    public void OnLevelSelectPressed()
    {
        DebugLogger.Log("MainMenuUI: Level Select pressed");
        LoadLevelSelect();
    }

    public void OnEndlessModePressed()
    {
        if (!IsStoryComplete())
        {
            DebugLogger.LogWarning("MainMenuUI: Endless Mode is locked until story is complete.");
            return;
        }

        LoadGameplay();
    }

    public void OnTracingDojoPressed()
    {
        DebugLogger.Log("MainMenuUI: Tracing Dojo pressed (not yet implemented)");
    }

public void OnSettingsPressed()
    {
        DebugLogger.Log("MainMenuUI: Settings pressed");
        if (_settingsPanel != null)
            _settingsPanel.Show();
    }

    public void OnCreditsPressed()
    {
        DebugLogger.Log("MainMenuUI: Credits pressed");
        if (_creditsPanel != null)
            _creditsPanel.Show();
    }

#if UNITY_EDITOR || SALINLAHI_SANDBOX
    public void OnSandboxModePressed()
    {
        if (!SandboxMode.IsAvailable)
        {
            DebugLogger.LogWarning("MainMenuUI: Sandbox mode is not available in this build.");
            return;
        }

        DebugLogger.Log("MainMenuUI: Sandbox mode pressed");
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadSandboxGameplay();
        else
            LoadSandboxGameplayDirect();
    }

    private void EnsureSandboxEntryPoint()
    {
        if (!SandboxMode.IsAvailable)
            return;

        Button sandboxButton = CreateSandboxButton();
        if (sandboxButton == null)
            return;

        sandboxButton.onClick.RemoveAllListeners();
        sandboxButton.onClick.AddListener(OnSandboxModePressed);
        sandboxButton.interactable = true;
        sandboxButton.gameObject.SetActive(true);
    }

    private Button CreateSandboxButton()
    {
        Transform parent = _endlessModeButton != null
            ? _endlessModeButton.transform.parent
            : transform;

        if (parent.Find("SandboxModeButton") is Transform existing)
            return existing.GetComponent<Button>();

        var buttonObject = new GameObject("SandboxModeButton");
        buttonObject.transform.SetParent(parent, false);
        buttonObject.AddComponent<Image>().color = new Color(0.25f, 0.5f, 0.85f, 1f);
        Button button = buttonObject.AddComponent<Button>();

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(600f, 120f);
        rect.anchoredPosition = new Vector2(0f, 24f);

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Sandbox";
        label.fontSize = 52f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private static void LoadSandboxGameplayDirect()
    {
        if (!SandboxMode.TryActivate())
        {
            DebugLogger.LogWarning("MainMenuUI: Sandbox mode is not available in this build.");
            return;
        }

        GameManager.Instance?.DiscardPausedRunSnapshot();
        EnemyPool.Instance?.ReturnAllCheckedOut();
        LoadSceneDirect(SceneGameplay);
    }
#else
    private void EnsureSandboxEntryPoint() { }
#endif

    private static void LoadGameplay()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
        {
            CleanupDirectGameplayState();
            LoadSceneDirect(SceneGameplay);
        }
    }

    private static void LoadLevelSelect()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            LoadSceneDirect(SceneLevelSelect);
    }

    private static void LoadSceneDirect(string sceneName)
    {
        DebugLogger.LogWarning(
            $"MainMenuUI: SceneLoader not available. Loading '{sceneName}' directly. "
            + "Open from Bootstrap for normal transitions.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private static void CleanupDirectGameplayState()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        SandboxMode.Deactivate();
#endif
        EnemyPool.Instance?.ReturnAllCheckedOut();
    }

private bool IsStoryComplete()
    {
        if (ProgressManager.Instance == null)
            return false;

        return ProgressManager.Instance.IsEndlessModeUnlocked();
    }
}
