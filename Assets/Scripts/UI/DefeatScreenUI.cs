using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DefeatScreenUI : MonoBehaviour
{
    private const string HeartFullPath = "Art/UI/Heart/ui_heart_full";
    private const string HeartEmptyPath = "Art/UI/Heart/ui_heart_empty";
    private const string FramePath = "Art/UI/Frames/border";

    private const string RuntimeSubtitleName = "[Runtime] DefeatSubtitle";
    private const string RuntimeTipPanelName = "[Runtime] TipPanel";

    private static bool _spritesLoaded;
    private static Sprite _heartFull;
    private static Sprite _heartEmpty;
    private static Sprite _frame;

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
        {
            _panel.SetActive(true);
            // Translucent dim over the live frame — same treatment as the victory
            // screen, tinted a shade redder to sit under the DEFEAT banner.
            EndScreenDim.Apply(_panel, EndScreenDim.DefeatTint);
        }

        // The wave label, the spent hearts and the pause button were painting straight through
        // the 85%-opaque defeat background. Sibling order cannot settle it -- the HUD sits under
        // its own Canvas -- so the HUD is taken down instead. Both buttons below leave the scene,
        // so nothing has to put it back: the reload does. The tutorial scene leaves the field
        // unwired, so the root is resolved by name when the Inspector never supplied it.
        if (_hudRoot == null)
            _hudRoot = GameObject.Find("HUDRoot");
        if (_hudRoot != null)
            _hudRoot.SetActive(false);

        int hearts = GameManager.Instance != null ? GameManager.Instance.LastDefeatHearts : 0;
        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        int maxHearts = heartSystem != null ? heartSystem.GetMaxHearts() : 3;

        EnsureDefeatReadout(hearts, maxHearts);

        if (_heartCountText != null)
            _heartCountText.text = $"{hearts}/{maxHearts}";
        if (_explanationText != null)
            // dev turned this line off deliberately. It previously rendered behind the button
            // stack, and this branch had fixed that by re-parenting it to the top on every Show —
            // but a hidden element cannot have a layering bug, so that fix is moot and the removal
            // wins. If the explanation is ever brought back, it needs SetAsLastSibling() here:
            // ReviewLessonButton is cloned at Show time and inserts itself into the same stack, so
            // paint order cannot be left to whoever was created last.
            _explanationText.gameObject.SetActive(false);

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

        SetButtonLabel(_retryButton, DefeatScreenCopy.RetryLabel);

        if (_explanationText == null)
        {
            GameObject explanationObject = new GameObject("DefeatExplanation", typeof(RectTransform));
            explanationObject.transform.SetParent(_panel.transform, false);
            RectTransform rect = explanationObject.GetComponent<RectTransform>();
            // 0.43-0.62 of the panel is the button stack: Retry sits at the panel's centre and the
            // other two hang below it, so the explanation was laid straight over three opaque
            // buttons and could not be read at all. This band is the gap between the DEFEAT banner
            // above and the topmost button below.
            rect.anchorMin = new Vector2(0.12f, 0.570f);
            rect.anchorMax = new Vector2(0.88f, 0.680f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _explanationText = explanationObject.AddComponent<TextMeshProUGUI>();
            _explanationText.fontSize = UITextScale.Body;
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
            SetButtonLabel(_reviewLessonButton, DefeatScreenCopy.ReviewLessonLabel);
        }

        EnsureSpritesLoaded();
        EnsureSubtitle();
        EnsureTipPanel();
        PositionDefeatButtons();
    }

    /// <summary>
    /// One-line subheading under the DEFEAT banner, filling the dead band between
    /// banner and content — the defeat counterpart of victory's "Level Complete!".
    /// </summary>
    private void EnsureSubtitle()
    {
        GameObject subtitleObject = FindOrCreateChild(_panel.transform, RuntimeSubtitleName);
        RectTransform rect = subtitleObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 300f);
        rect.sizeDelta = new Vector2(800f, 90f);

        TextMeshProUGUI text = subtitleObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = subtitleObject.AddComponent<TextMeshProUGUI>();
        text.text = DefeatScreenCopy.Subtitle;
        text.fontSize = UITextScale.Title;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.93f, 0.89f, 0.78f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
    }

    /// <summary>
    /// The spent-hearts row and its caption, at the same band the victory screen
    /// gives its hearts — one visual language across both end screens.
    /// </summary>
    private void EnsureDefeatReadout(int hearts, int maxHearts)
    {
        if (_panel == null)
            return;

        EnsureSpritesLoaded();

        GameObject row = FindOrCreateChild(_panel.transform, "HeartsRow");
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, 160f);
        rowRect.sizeDelta = new Vector2(Mathf.Max(1, maxHearts) * 96f, 84f);

        for (int i = 0; i < maxHearts; i++)
        {
            Transform heartTransform = null;
            foreach (Transform child in row.transform)
                if (child.name == "Heart_" + i)
                    heartTransform = child;
            GameObject heartObject;
            if (heartTransform != null)
            {
                heartObject = heartTransform.gameObject;
            }
            else
            {
                heartObject = new GameObject("Heart_" + i, typeof(RectTransform));
                heartObject.transform.SetParent(row.transform, false);
            }
            heartObject.SetActive(true);

            RectTransform heartRect = heartObject.GetComponent<RectTransform>();
            heartRect.anchorMin = heartRect.anchorMax = new Vector2(0f, 0.5f);
            heartRect.pivot = new Vector2(0f, 0.5f);
            heartRect.anchoredPosition = new Vector2(i * 96f + 6f, 0f);
            heartRect.sizeDelta = new Vector2(84f, 84f);
            Image image = heartObject.GetComponent<Image>();
            if (image == null)
                image = heartObject.AddComponent<Image>();
            bool filled = i < hearts;
            image.sprite = filled ? _heartFull : _heartEmpty;
            image.color = image.sprite != null
                ? Color.white
                : (filled ? new Color32(190, 60, 60, 255) : new Color32(70, 60, 60, 160));
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        foreach (Transform child in row.transform)
        {
            if (!child.name.StartsWith("Heart_"))
                continue;
            if (int.TryParse(child.name.Substring(6), out int index) && index >= maxHearts)
                child.gameObject.SetActive(false);
        }

        GameObject captionObject = FindOrCreateChild(_panel.transform, "HeartsCaption");
        RectTransform captionRect = captionObject.GetComponent<RectTransform>();
        captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 0.5f);
        captionRect.pivot = new Vector2(0.5f, 0.5f);
        captionRect.anchoredPosition = new Vector2(0f, 90f);
        captionRect.sizeDelta = new Vector2(560f, 56f);

        TextMeshProUGUI caption = captionObject.GetComponent<TextMeshProUGUI>();
        if (caption == null)
            caption = captionObject.AddComponent<TextMeshProUGUI>();
        caption.text = hearts <= 0
            ? DefeatScreenCopy.NoHeartsLeftLabel
            : DefeatScreenCopy.HeartsLeftLabel;
        caption.fontSize = UITextScale.Body;
        caption.color = new Color(0.93f, 0.89f, 0.78f, 1f);
        caption.alignment = TextAlignmentOptions.Center;
        caption.raycastTarget = false;
        TutorialFontProvider.ApplyTo(caption);
    }

    /// <summary>
    /// A framed guidance box — same sprite and proportions as the victory stats
    /// frame — restating the loss mechanic and pointing at the recovery action.
    /// </summary>
    private void EnsureTipPanel()
    {
        GameObject tipPanel = FindOrCreateChild(_panel.transform, RuntimeTipPanelName);
        RectTransform rect = tipPanel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -110f);
        rect.sizeDelta = new Vector2(620f, 170f);

        Image frame = tipPanel.GetComponent<Image>();
        if (frame == null)
            frame = tipPanel.AddComponent<Image>();
        if (_frame != null)
        {
            frame.sprite = _frame;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
        }
        else
        {
            frame.sprite = null;
            frame.color = new Color(0.05f, 0.06f, 0.12f, 0.85f);
        }
        frame.raycastTarget = false;

        GameObject textObject = FindOrCreateChild(tipPanel.transform, "TipText");
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0f);
        textRect.anchorMax = new Vector2(0.92f, 1f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(0f, 24f);
        textRect.offsetMax = new Vector2(0f, -24f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = DefeatScreenCopy.TipLine1 + "\n" + DefeatScreenCopy.TipLine2;
        text.fontSize = UITextScale.Body;
        text.color = new Color(0.93f, 0.89f, 0.78f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
    }

    /// <summary>
    /// Same arrangement as the victory screen: the primary action full-width at
    /// -430, the secondary pair side by side at -660.
    /// </summary>
    private void PositionDefeatButtons()
    {
        PositionButton(_retryButton, new Vector2(0f, -430f), new Vector2(446f, 200f));
        PositionButton(_levelSelectButton, new Vector2(-180f, -660f), new Vector2(340f, 170f));
        PositionButton(_reviewLessonButton, new Vector2(180f, -660f), new Vector2(340f, 170f));
    }

    private static void PositionButton(Button button, Vector2 position, Vector2 size)
    {
        if (button == null)
            return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static GameObject FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing.gameObject;

        GameObject created = new GameObject(childName, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static void EnsureSpritesLoaded()
    {
        if (_spritesLoaded)
            return;
        _spritesLoaded = true;
        _heartFull = LoadSprite(HeartFullPath);
        _heartEmpty = LoadSprite(HeartEmptyPath);
        _frame = LoadSprite(FramePath);
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        Sprite single = Resources.Load<Sprite>(resourcePath);
        if (single != null)
            return single;
        Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpriteCache()
    {
        _spritesLoaded = false;
        _heartFull = _heartEmpty = null;
        _frame = null;
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
