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
        buttonObject.AddComponent<Image>().color = new Color(0.19f, 0.42f, 0.72f, 1f);
        Button button = buttonObject.AddComponent<Button>();

        RectTransform rect = button.GetComponent<RectTransform>();
        if (_endlessModeButton != null)
        {
            RectTransform sourceRect = _endlessModeButton.GetComponent<RectTransform>();
            if (sourceRect != null)
            {
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                rect.pivot = sourceRect.pivot;
                rect.sizeDelta = sourceRect.sizeDelta;
                rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -70f);
            }
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 56f);
            rect.anchoredPosition = new Vector2(0f, -250f);
        }

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Sandbox";
        label.fontSize = 24f;
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
