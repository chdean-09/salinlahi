using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the short level-start contract at the END of the story phase — after the
/// before-level cutscene and intro dialogue, immediately before the focus words,
/// symbol cards and defense. The panel is built at runtime so the shared Gameplay
/// scenes do not need another serialized UI dependency.
/// </summary>
public sealed class LevelReadyScreenController : MonoBehaviour
{
    private const int OverlaySortingOrder = 320;

    [Header("Optional authored wiring")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _objectiveText;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _backButton;

    private bool _startRequested;
    private bool _backRequested;
    private bool _runtimePanelBuilt;

    public bool IsPresenting { get; private set; }
    public string RenderedText { get; private set; }

    /// <summary>
    /// Shows the level identity and its first restoration goals, then waits for Start.
    /// A terminal-flow predicate lets defeat/restart/leave close the gate safely.
    /// </summary>
    public IEnumerator Present(
        LevelConfigSO config,
        Func<bool> shouldCancel = null,
        Action onBack = null)
    {
        if (config == null)
            yield break;

        EnsurePanel();
        RenderedText = BuildObjectiveText(config);

        if (_titleText != null)
            _titleText.text = BuildTitle(config);
        if (_objectiveText != null)
            _objectiveText.text = RenderedText;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        _startRequested = false;
        _backRequested = false;
        IsPresenting = true;

        yield return new WaitUntil(() =>
            _startRequested || _backRequested || (shouldCancel != null && shouldCancel()));

        IsPresenting = false;
        Hide();

        if (_backRequested)
            onBack?.Invoke();
    }

    /// <summary>Called by the Start button.</summary>
    public void StartLevel()
    {
        _startRequested = true;
    }

    /// <summary>Called by the Back button.</summary>
    public void Back()
    {
        _backRequested = true;
    }

    /// <summary>Closes the panel when the owning level attempt is torn down.</summary>
    public void Hide()
    {
        IsPresenting = false;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    public static string BuildTitle(LevelConfigSO config)
    {
        if (config == null)
            return string.Empty;

        string chapter = string.IsNullOrWhiteSpace(config.chapterName)
            ? string.Empty
            : config.chapterName.Trim();
        string levelName = string.IsNullOrWhiteSpace(config.levelName)
            ? $"Level {config.levelNumber}"
            : config.levelName.Trim();

        return string.IsNullOrEmpty(chapter)
            ? $"Level {config.levelNumber}: {levelName}"
            : $"{chapter} · Level {config.levelNumber}: {levelName}";
    }

    /// <summary>
    /// Keeps Ready distinct from Focus Words: this is the one-line level contract,
    /// while the following screen owns the authored words and meanings.
    /// </summary>
    public static string BuildObjectiveText(LevelConfigSO config)
    {
        return config == null ? string.Empty : "Learn the symbols, then defend the shrine.";
    }

    private void EnsurePanel()
    {
        if (_panelRoot != null || _runtimePanelBuilt)
            return;

        _runtimePanelBuilt = true;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        bool createdFallbackCanvas = false;
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] LevelReadyCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            // Keep the runtime canvas under the flow owner so a retry/scene teardown
            // cannot leave a detached modal behind.
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            createdFallbackCanvas = true;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (createdFallbackCanvas)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder;
        }
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        GameObject overlay = new GameObject(
            "[Runtime] LevelReadyOverlay",
            typeof(RectTransform),
            typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0.015f, 0.02f, 0.045f, 0.88f);
        overlayImage.raycastTarget = true;

        _panelRoot = new GameObject("[Runtime] LevelReadyPanel", typeof(RectTransform), typeof(Image));
        _panelRoot.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.24f);
        panelRect.anchorMax = new Vector2(0.92f, 0.76f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        Image panelImage = _panelRoot.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.035f, 0.08f, 0.98f);
        panelImage.raycastTarget = true;
        bool onParchment = ScrollPanelArt.ApplyFull(panelImage);

        _titleText = CreateText("Title", _panelRoot.transform, 68f,
            new Vector2(0.17f, 0.55f), new Vector2(0.83f, 0.88f));
        _objectiveText = CreateText("Objective", _panelRoot.transform, 44f,
            new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.57f));
        _objectiveText.alignment = TextAlignmentOptions.Center;
        if (onParchment)
        {
            ScrollPanelArt.Inkify(_titleText);
            ScrollPanelArt.Inkify(_objectiveText);
        }

        GameObject buttonObject = new GameObject(
            "StartButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.66f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.66f, 0.18f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(300f, 88f);
        buttonObject.GetComponent<Image>().color = new Color(0.85f, 0.72f, 0.35f, 1f);
        _startButton = buttonObject.GetComponent<Button>();
        _startButton.onClick.AddListener(StartLevel);

        GameObject labelObject = new GameObject("StartLabel", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Start";
        label.fontSize = UITextScale.Body;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        TutorialFontProvider.ApplyTo(label);
        if (onParchment)
            ScrollPanelArt.Inkify(label);

        GameObject backObject = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        backObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform backRect = backObject.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.34f, 0.18f);
        backRect.anchorMax = new Vector2(0.34f, 0.18f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.sizeDelta = new Vector2(260f, 88f);
        backObject.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.34f, 1f);
        _backButton = backObject.GetComponent<Button>();
        _backButton.onClick.AddListener(Back);

        GameObject backLabelObject = new GameObject("BackLabel", typeof(RectTransform));
        backLabelObject.transform.SetParent(backObject.transform, false);
        RectTransform backLabelRect = backLabelObject.GetComponent<RectTransform>();
        backLabelRect.anchorMin = Vector2.zero;
        backLabelRect.anchorMax = Vector2.one;
        backLabelRect.offsetMin = backLabelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI backLabel = backLabelObject.AddComponent<TextMeshProUGUI>();
        backLabel.text = "Back";
        backLabel.fontSize = UITextScale.Body;
        backLabel.alignment = TextAlignmentOptions.Center;
        backLabel.raycastTarget = false;
        TutorialFontProvider.ApplyTo(backLabel);

        overlay.SetActive(false);
        _panelRoot = overlay;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float fontSize,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(UITextScale.AutoSizeFloor, fontSize * 0.55f);
        text.fontSizeMax = fontSize;
        TutorialFontProvider.ApplyTo(text);
        return text;
    }
}
