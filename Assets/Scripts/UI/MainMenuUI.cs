using Salinlahi.Debug.Sandbox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button _endlessModeButton;

    private void Start()
    {
        if (_endlessModeButton != null)
            _endlessModeButton.interactable = IsStoryComplete();

        EnsureSandboxEntryPoint();
    }

    public void OnPlayButtonPressed()
    {
        DebugLogger.Log("MainMenuUI: Play button pressed");
        // Default to Level 1 when pressing Play
        PlayerPrefs.SetInt("SelectedLevel", 1);
        PlayerPrefs.Save();
        SceneLoader.Instance.LoadGameplay();
    }

    public void OnLevelSelectPressed()
    {
        DebugLogger.Log("MainMenuUI: Level Select pressed");
        SceneLoader.Instance.LoadLevelSelect();
    }

    public void OnEndlessModePressed()
    {
        if (!IsStoryComplete())
        {
            DebugLogger.LogWarning("MainMenuUI: Endless Mode is locked until story is complete.");
            return;
        }

        SceneLoader.Instance.LoadGameplay();
    }

    public void OnTracingDojoPressed()
    {
        DebugLogger.Log("MainMenuUI: Tracing Dojo pressed (not yet implemented)");
    }

    public void OnSettingsPressed()
    {
        DebugLogger.Log("MainMenuUI: Settings pressed (not yet implemented)");
    }

    public void OnSandboxModePressed()
    {
        if (!SandboxMode.IsAvailable)
        {
            DebugLogger.LogWarning("MainMenuUI: Sandbox mode is not available in this build.");
            return;
        }

        DebugLogger.Log("MainMenuUI: Sandbox mode pressed");
        SceneLoader.Instance.LoadSandboxGameplay();
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

    // TODO: Replace with actual story progression check when save system is implemented
    private bool IsStoryComplete()
    {
        return false;
    }
}
