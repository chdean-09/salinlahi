using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DefeatScreenUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI _heartCountText;
    [SerializeField] private TextMeshProUGUI _explanationText;

    [Header("Buttons")]
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _reviewLessonButton;
    [SerializeField] private Button _levelSelectButton;

    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    [Tooltip("Gameplay HUD root, hidden while the defeat overlay is up. Safe to leave unwired.")]
    [SerializeField] private GameObject _hudRoot;

    private void Awake()
    {
        EnsureGuidanceAndActions();
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (_retryButton != null)
            _retryButton.onClick.AddListener(OnRetryPressed);
        if (_reviewLessonButton != null)
            _reviewLessonButton.onClick.AddListener(OnReviewLessonPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.AddListener(OnLevelSelectPressed);
    }

    private void OnDisable()
    {
        if (_retryButton != null)
            _retryButton.onClick.RemoveListener(OnRetryPressed);
        if (_reviewLessonButton != null)
            _reviewLessonButton.onClick.RemoveListener(OnReviewLessonPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.RemoveListener(OnLevelSelectPressed);
    }

    public void Show()
    {
        EnsureGuidanceAndActions();
        if (_panel != null)
            _panel.SetActive(true);

        // The wave label, the spent hearts and the pause button were painting straight through
        // the 85%-opaque defeat background. Sibling order cannot settle it -- the HUD sits under
        // its own Canvas -- so the HUD is taken down instead. Both buttons below leave the scene,
        // so nothing has to put it back: the reload does.
        if (_hudRoot != null)
            _hudRoot.SetActive(false);

        int hearts = GameManager.Instance != null ? GameManager.Instance.LastDefeatHearts : 0;
        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        int maxHearts = heartSystem != null ? heartSystem.GetMaxHearts() : 3;

        if (_heartCountText != null)
            _heartCountText.text = $"{hearts}/{maxHearts}";
        if (_explanationText != null)
        {
            _explanationText.text =
                "An enemy reached the shrine and your last heart was lost. "
                + "Draw the glowing symbol before enemies reach the bottom.";
        }

        DebugLogger.Log($"DefeatScreenUI: Showing defeat. Hearts: {hearts}/{maxHearts}");
    }

    private void OnRetryPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("DefeatScreenUI: Retry combat pressed");

        LevelRetryIntent.RequestCombatOnly();

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("DefeatScreenUI: SceneLoader not available.");
    }

    private void OnReviewLessonPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("DefeatScreenUI: Review lesson pressed");

        LevelRetryIntent.Clear();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("DefeatScreenUI: SceneLoader not available.");
    }

    private void OnLevelSelectPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("DefeatScreenUI: Level Select pressed");

        LevelRetryIntent.Clear();

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("DefeatScreenUI: SceneLoader not available.");
    }

    private void EnsureGuidanceAndActions()
    {
        if (_panel == null)
            return;

        SetButtonLabel(_retryButton, "Retry Combat");

        if (_explanationText == null)
        {
            GameObject explanationObject = new GameObject("DefeatExplanation", typeof(RectTransform));
            explanationObject.transform.SetParent(_panel.transform, false);
            RectTransform rect = explanationObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f, 0.43f);
            rect.anchorMax = new Vector2(0.88f, 0.62f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _explanationText = explanationObject.AddComponent<TextMeshProUGUI>();
            _explanationText.fontSize = 26f;
            _explanationText.alignment = TextAlignmentOptions.Center;
            _explanationText.color = Color.white;
            _explanationText.textWrappingMode = TextWrappingModes.Normal;
            _explanationText.raycastTarget = false;
            TutorialFontProvider.ApplyTo(_explanationText);
        }

        if (_reviewLessonButton == null && _retryButton != null)
        {
            _reviewLessonButton = Instantiate(_retryButton, _retryButton.transform.parent);
            _reviewLessonButton.name = "ReviewLessonButton";
            _reviewLessonButton.onClick.RemoveAllListeners();
            _reviewLessonButton.transform.SetSiblingIndex(_retryButton.transform.GetSiblingIndex() + 1);
            SetButtonLabel(_reviewLessonButton, "Review Lesson");

            RectTransform retryRect = _retryButton.transform as RectTransform;
            RectTransform reviewRect = _reviewLessonButton.transform as RectTransform;
            RectTransform selectRect = _levelSelectButton != null
                ? _levelSelectButton.transform as RectTransform
                : null;
            if (retryRect != null && reviewRect != null)
            {
                reviewRect.anchoredPosition = selectRect != null
                    ? Vector2.Lerp(retryRect.anchoredPosition, selectRect.anchoredPosition, 0.5f)
                    : retryRect.anchoredPosition + Vector2.down * 80f;
            }
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
    }
}

/// <summary>
/// Carries one retry choice across the Gameplay scene reload. Consumption is
/// one-shot so a later level entry always returns to the full authored flow.
/// </summary>
public static class LevelRetryIntent
{
    private static bool s_combatOnlyRequested;

    public static void RequestCombatOnly() => s_combatOnlyRequested = true;

    public static bool ConsumeCombatOnly()
    {
        bool requested = s_combatOnlyRequested;
        s_combatOnlyRequested = false;
        return requested;
    }

    public static void Clear() => s_combatOnlyRequested = false;
}
