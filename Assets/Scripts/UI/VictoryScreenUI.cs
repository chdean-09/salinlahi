using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryScreenUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI _starCountText;
    [SerializeField] private GameObject[] _starIcons;

    [Header("Buttons")]
    [SerializeField] private Button _nextLevelButton;
    [SerializeField] private Button _levelSelectButton;

    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (_nextLevelButton != null)
            _nextLevelButton.onClick.AddListener(OnNextLevelPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.AddListener(OnLevelSelectPressed);
    }

    private void OnDisable()
    {
        if (_nextLevelButton != null)
            _nextLevelButton.onClick.RemoveListener(OnNextLevelPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.RemoveListener(OnLevelSelectPressed);
    }

    public void Show()
    {
        if (_panel != null)
            _panel.SetActive(true);

        int currentLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
        int stars = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetStars(currentLevel)
            : 0;

        if (_starCountText != null)
            _starCountText.text = $"{stars}/3";

        if (_starIcons != null)
        {
            for (int i = 0; i < _starIcons.Length; i++)
            {
                if (_starIcons[i] != null)
                    _starIcons[i].SetActive(i < stars);
            }
        }

        bool isLastLevel = currentLevel >= 15;
        if (_nextLevelButton != null)
            _nextLevelButton.gameObject.SetActive(!isLastLevel);

        DebugLogger.Log($"VictoryScreenUI: Level {currentLevel} complete with {stars} stars.");
    }

    /// <summary>
    /// SALIN-202: renders the learning-outcome summary on the victory panel. The
    /// summary object is created at runtime when the scene does not author one,
    /// mirroring the other no-Inspector-wiring fallbacks.
    /// </summary>
    public void ShowResultsSummary(string summaryText)
    {
        if (_panel == null || string.IsNullOrWhiteSpace(summaryText))
            return;

        Transform existing = _panel.transform.Find("[Runtime] ResultsSummary");
        GameObject summaryObject;
        if (existing != null)
        {
            summaryObject = existing.gameObject;
        }
        else
        {
            summaryObject = new GameObject("[Runtime] ResultsSummary", typeof(RectTransform));
            summaryObject.transform.SetParent(_panel.transform, false);
            RectTransform rect = summaryObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(520f, 200f);
        }

        TextMeshProUGUI text = summaryObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = summaryObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = summaryText;
    }

    private void OnNextLevelPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        int currentLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
        int nextLevel = currentLevel + 1;

        if (nextLevel > 15)
        {
            DebugLogger.LogWarning("VictoryScreenUI: No next level. Navigating to Level Select.");
            OnLevelSelectPressed();
            return;
        }

        if (ProgressManager.Instance == null || !ProgressManager.Instance.TrySetSelectedLevelNumber(nextLevel))
        {
            DebugLogger.LogWarning("VictoryScreenUI: Next level could not be persisted.");
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.SetLevel(null);

        DebugLogger.Log($"VictoryScreenUI: Advancing to Level {nextLevel}");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("VictoryScreenUI: SceneLoader not available.");
    }

    private void OnLevelSelectPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("VictoryScreenUI: Level Select pressed");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("VictoryScreenUI: SceneLoader not available.");
    }
}
