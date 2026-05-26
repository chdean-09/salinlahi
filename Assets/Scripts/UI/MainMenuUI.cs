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
    private const string SceneTracingDojo = "TracingDojo";
    private static readonly string[] MainMenuButtonNames =
    {
        "PlayButton",
        "LevelSelectButton",
        "EndlessModeButton",
        "TracingDojoButton",
        "SettingsButton"
    };

    private static readonly Color ActiveTextColor = new(0.7019608f, 0.5019608f, 0.07450981f, 1f);
    private static readonly Color LockedTextColor = new(0.38f, 0.34f, 0.26f, 1f);
    private static readonly Color ActiveButtonColor = Color.white;
    private static readonly Color LockedButtonColor = new(0.42f, 0.39f, 0.32f, 0.75f);
    private static readonly Color TextShadowColor = new(0.06f, 0.035f, 0.01f, 1f);
    private static readonly Vector2 TextShadowOffset = new(5f, -5f);

    [SerializeField] private Button _endlessModeButton;

    [Header("Overlay Panels")]
    [SerializeField] private SettingsPanel _settingsPanel;
    [SerializeField] private CreditsPanel _creditsPanel;

    private void Start()
    {
        ApplyMainMenuTextEffects();

        if (_endlessModeButton != null)
        {
            bool isEndlessUnlocked = IsStoryComplete();
            _endlessModeButton.interactable = isEndlessUnlocked;
            ApplyButtonVisualState(_endlessModeButton, isEndlessUnlocked);
        }

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
        LoadTracingDojo();
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

    private static void LoadTracingDojo()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(SceneTracingDojo);
        else
            LoadSceneDirect(SceneTracingDojo);
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

    private static void ApplyButtonVisualState(Button button, bool isUnlocked)
    {
        if (button.targetGraphic != null)
            button.targetGraphic.color = isUnlocked ? ActiveButtonColor : LockedButtonColor;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = isUnlocked ? ActiveTextColor : LockedTextColor;
            EnsureTextShadow(label);
        }
    }

    private void ApplyMainMenuTextEffects()
    {
        Text[] labels = GetComponentsInChildren<Text>(true);
        foreach (Text label in labels)
        {
            if (label == null)
                continue;

            if (IsPrimaryMenuLabel(label))
                EnsureTextShadow(label);
        }
    }

    private static bool IsPrimaryMenuLabel(Text label)
    {
        if (label.name == "TitleText" || label.text == "Salinlahi")
            return true;

        return label.transform.parent != null && IsMainMenuButton(label.transform.parent.name);
    }

    private static bool IsMainMenuButton(string objectName)
    {
        foreach (string buttonName in MainMenuButtonNames)
        {
            if (objectName == buttonName)
                return true;
        }

        return false;
    }

    private static void EnsureTextShadow(Text label)
    {
        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null)
            shadow = label.gameObject.AddComponent<Shadow>();

        shadow.effectColor = TextShadowColor;
        shadow.effectDistance = TextShadowOffset;
        shadow.useGraphicAlpha = true;
    }

    private bool IsStoryComplete()
    {
        if (ProgressManager.Instance == null)
            return false;

        return ProgressManager.Instance.IsEndlessModeUnlocked();
    }
}
