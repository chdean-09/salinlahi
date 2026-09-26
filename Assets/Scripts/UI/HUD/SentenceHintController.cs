using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The sentence-hint affordance on the combat HUD: a small scroll chip below the
/// pause button that opens a parchment scroll of the level's sentence hints
/// (the blanked restoration context plus each focus word's meaning and
/// descriptor line with every answer withheld, assembled by
/// <see cref="SentenceHintContent"/>). Levels 6-15 carry no authored objective
/// subtext above the restoration rail, so this scroll is how the player asks for
/// the sentence context levels 1-5 print by default.
///
/// Opening the scroll takes a dialogue pause — the same freeze the intro beats
/// use — so reading a hint never costs a heart. The pause latch is released on
/// every exit path: Close, tap-outside, terminal state, attempt abort, teardown.
/// </summary>
[DisallowMultipleComponent]
public sealed class SentenceHintController : MonoBehaviour
{
    [Header("Hint Chip")]
    [Tooltip("Chip size in canvas units. Square, roughly 2x the authored 80x80 "
             + "pause button it parks under.")]
    [SerializeField] private Vector2 _chipSize = new Vector2(160f, 160f);

    [Tooltip("Gap between the pause button's bottom edge and the chip's top edge.")]
    [SerializeField, Min(0f)] private float _chipGapBelowPause = 12f;

    [Tooltip("Fallback top-right offset used when the pause button cannot be found: "
             + "(-20, -112) sits the chip under the authored 80x80 pause button at "
             + "(-20, -20).")]
    [SerializeField] private Vector2 _chipFallbackPosition = new Vector2(-20f, -112f);

    [Header("Copy")]
    [Tooltip("Fallback chip text, used only when the parchment or hint icon art "
             + "fails to load.")]
    [SerializeField] private string _chipLabel = "Sentences";
    [SerializeField] private string _panelTitle = "Sentence Hints";
    [SerializeField] private string _closeLabel = "Close";

    // The almanac's ink "?" glyph — a Resources copy of Assets/Art/UI/Almanac/
    // Questionmark.png so the runtime-built chip can resolve it.
    private const string IconResourcePath = "Art/UI/Almanac/Questionmark";

    private List<SentenceHintContent.Entry> _entries = new List<SentenceHintContent.Entry>();
    private string _bodyText = string.Empty;

    private GameObject _chipRoot;
    private GameObject _overlayRoot;
    private TextMeshProUGUI _bodyLabel;
    private bool _pauseHeld;

    // Same suppression funnel the restoration rail polls: intro modals raise no
    // events, so visibility is re-evaluated per frame over tiny cached arrays.
    private CutscenePlayer[] _cutscenePlayers;
    private FocusWordPreviewController[] _focusWordPreviews;
    private SymbolLearningCardController[] _symbolCards;
    private LevelReadyScreenController[] _readyScreens;

    /// <summary>True while the hint scroll is up. Queried by other HUD suppression funnels.</summary>
    public bool IsPresenting { get; private set; }

    /// <summary>Applies a level's hint content. Empty content keeps the chip hidden.</summary>
    public void ApplyLevel(LevelConfigSO level)
    {
        _entries = SentenceHintContent.Build(level);
        _bodyText = ComposeBody(_entries);
        if (_bodyLabel != null)
            _bodyLabel.text = _bodyText;
        if (_entries.Count == 0)
            Close();
    }

    /// <summary>Chip click: freezes gameplay and raises the scroll.</summary>
    public void Open()
    {
        if (IsPresenting || _entries.Count == 0)
            return;

        GameManager game = GameManager.Instance;
        if (game == null || game.CurrentState != GameState.Playing)
            return;
        if (ChallengeRuntimeState.IsActive || IsAnyCutscenePlaying() || IsAnyIntroModalPresenting())
            return;

        game.EnterDialoguePause();
        _pauseHeld = true;
        EnsureOverlay();
        _overlayRoot.SetActive(true);
        IsPresenting = true;
    }

    /// <summary>Dismisses the scroll and releases the dialogue pause this controller took.</summary>
    public void Close()
    {
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
        IsPresenting = false;
        ReleasePause();
    }

    private void OnEnable()
    {
        // Terminal/abort paths clear GameManager's pause latches themselves, so these
        // only tear down visuals — calling ExitDialoguePause there would be a no-op.
        EventBus.OnLevelAttemptAborted += HandleExternalDismiss;
        EventBus.OnLevelComplete += HandleExternalDismiss;
        EventBus.OnGameOver += HandleExternalDismiss;
    }

    private void OnDisable()
    {
        EventBus.OnLevelAttemptAborted -= HandleExternalDismiss;
        EventBus.OnLevelComplete -= HandleExternalDismiss;
        EventBus.OnGameOver -= HandleExternalDismiss;

        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
        IsPresenting = false;
        ReleasePause();
    }

    private void OnDestroy()
    {
        // The overlay lives on the shared modal canvas, not this object — destroy it
        // explicitly or a scene teardown strands it over whatever loads next.
        if (_overlayRoot != null)
            Destroy(_overlayRoot);
        ReleasePause();
    }

    private void LateUpdate()
    {
        bool show = ShouldShowChip();
        if (show)
        {
            EnsureChip();
            if (!_chipRoot.activeSelf)
                _chipRoot.SetActive(true);
        }
        else if (_chipRoot != null && _chipRoot.activeSelf)
        {
            _chipRoot.SetActive(false);
        }
    }

    private bool ShouldShowChip()
    {
        if (_entries.Count == 0 || IsPresenting)
            return false;

        GameManager game = GameManager.Instance;
        if (game == null || game.CurrentState != GameState.Playing)
            return false;

        // The challenge board already shows these prompts as its working text, and a
        // dialogue pause would not stop a TimedMemory clock (it ticks unscaled).
        if (ChallengeRuntimeState.IsActive)
            return false;

        if (IsAnyCutscenePlaying() || IsAnyIntroModalPresenting())
            return false;

        return true;
    }

    private void HandleExternalDismiss()
    {
        _pauseHeld = false;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
        IsPresenting = false;
    }

    private void ReleasePause()
    {
        if (!_pauseHeld)
            return;

        _pauseHeld = false;
        GameManager game = GameManager.Instance;
        if (game != null)
            game.ExitDialoguePause();
    }

    private bool IsAnyCutscenePlaying()
    {
        if (_cutscenePlayers == null || _cutscenePlayers.Length == 0)
            _cutscenePlayers = FindObjectsByType<CutscenePlayer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < _cutscenePlayers.Length; i++)
        {
            if (_cutscenePlayers[i] != null && _cutscenePlayers[i].IsPlaying)
                return true;
        }
        return false;
    }

    private bool IsAnyIntroModalPresenting()
    {
        if (_focusWordPreviews == null || _focusWordPreviews.Length == 0)
            _focusWordPreviews = FindObjectsByType<FocusWordPreviewController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < _focusWordPreviews.Length; i++)
        {
            if (_focusWordPreviews[i] != null && _focusWordPreviews[i].IsPresenting)
                return true;
        }

        if (_symbolCards == null || _symbolCards.Length == 0)
            _symbolCards = FindObjectsByType<SymbolLearningCardController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < _symbolCards.Length; i++)
        {
            if (_symbolCards[i] != null && _symbolCards[i].IsPresenting)
                return true;
        }

        if (_readyScreens == null || _readyScreens.Length == 0)
            _readyScreens = FindObjectsByType<LevelReadyScreenController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < _readyScreens.Length; i++)
        {
            if (_readyScreens[i] != null && _readyScreens[i].IsPresenting)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Builds the chip under HUDLayer, top-right, parked under the pause button. The
    /// pause button is found live so the chip follows if it is ever moved; when it is
    /// absent (test scenes) the authored offset is reproduced instead.
    /// </summary>
    private void EnsureChip()
    {
        if (_chipRoot != null)
            return;

        Transform parent = ResolveHudParent();
        _chipRoot = new GameObject(
            "[Runtime] SentenceHintChip", typeof(RectTransform), typeof(Image), typeof(Button));
        _chipRoot.transform.SetParent(parent, false);
        _chipRoot.transform.SetAsLastSibling();

        RectTransform chipRect = (RectTransform)_chipRoot.transform;
        chipRect.anchorMin = chipRect.anchorMax = Vector2.one;
        chipRect.pivot = Vector2.one;
        chipRect.sizeDelta = _chipSize;
        chipRect.anchoredPosition = ChipPosition(parent);

        Image background = _chipRoot.GetComponent<Image>();
        background.color = ScrollPanelArt.FlatPanelColor;
        bool onParchment = ScrollPanelArt.ApplyTop(background);

        // The almanac "?" reads as the hint affordance in ink on the parchment;
        // it only rides the scroll skin — on the dark flat fallback the glyph
        // would be illegible, so that path keeps the text label.
        Sprite icon = LoadIconSprite();
        if (onParchment && icon != null)
        {
            GameObject iconObject = new GameObject(
                "[Runtime] SentenceHintChipIcon", typeof(RectTransform));
            iconObject.transform.SetParent(_chipRoot.transform, false);
            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            ScrollPanelArt.SetAnchors(iconImage.rectTransform, ScrollPanelArt.TopSafeArea);
        }
        else
        {
            GameObject labelObject = new GameObject(
                "[Runtime] SentenceHintChipLabel", typeof(RectTransform));
            labelObject.transform.SetParent(_chipRoot.transform, false);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = _chipLabel;
            label.fontSize = UITextScale.Caption;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            TutorialFontProvider.ApplyTo(label);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-12f, -6f);

            if (onParchment)
                ScrollPanelArt.Inkify(label);
        }

        _chipRoot.GetComponent<Button>().onClick.AddListener(Open);
    }

    /// <summary>
    /// Loads the almanac "?" sprite, falling back to the first sub-sprite for
    /// the Multiple-mode import. Null-safe: missing art leaves the text label.
    /// </summary>
    private static Sprite LoadIconSprite()
    {
        Sprite single = Resources.Load<Sprite>(IconResourcePath);
        if (single != null)
            return single;
        Sprite[] all = Resources.LoadAll<Sprite>(IconResourcePath);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    private Vector2 ChipPosition(Transform parent)
    {
        RectTransform pause = FindDescendant(parent as RectTransform, "PauseButton");
        if (pause == null)
            return _chipFallbackPosition;

        // The pause button is pivot (1,1): its right edge is anchoredPosition.x and its
        // bottom edge is anchoredPosition.y - height. The chip pivots top-right too, so
        // it parks flush under the button's right edge.
        float rightEdge = pause.anchoredPosition.x + pause.sizeDelta.x * (1f - pause.pivot.x);
        float bottomEdge = pause.anchoredPosition.y - pause.sizeDelta.y * pause.pivot.y;
        return new Vector2(rightEdge, bottomEdge - _chipGapBelowPause);
    }

    private Transform ResolveHudParent()
    {
        GameObject hudLayer = GameObject.Find("HUDLayer");
        if (hudLayer != null)
            return hudLayer.transform;

        Canvas canvas = ScrollPanelArt.ResolveModalCanvas(this);
        return canvas != null ? canvas.transform : transform;
    }

    private static RectTransform FindDescendant(RectTransform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform found = FindDescendant(root.GetChild(i) as RectTransform, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// The modal: the same dim overlay + full scroll the ready screen and focus-word
    /// preview present. Title on top, sentences in the paper, Close on the bottom rod.
    /// Tapping the dim outside the scroll dismisses it too.
    /// </summary>
    private void EnsureOverlay()
    {
        if (_overlayRoot != null)
            return;

        Canvas canvas = ScrollPanelArt.ResolveModalCanvas(this);
        if (canvas == null)
        {
            // Same fallback the ready screen builds: a standalone overlay canvas keeps
            // the modal renderable in scenes that have no HUD (edit-mode/bootstrap
            // scenes). Parented to this controller so teardown takes it along.
            GameObject canvasObject = new GameObject(
                "[Runtime] SentenceHintCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 320;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        _overlayRoot = ScrollPanelArt.CreateDimOverlay(
            canvas.transform, "[Runtime] SentenceHintOverlay");

        // Tap-outside dismiss lives on a sibling BEHIND the panel, not on the shared
        // overlay: a click on the scroll would otherwise bubble up the panel's parent
        // chain and fire the dismiss button while the player is still reading.
        GameObject dismissObject = new GameObject(
            "[Runtime] SentenceHintDismiss", typeof(RectTransform), typeof(Image));
        dismissObject.transform.SetParent(_overlayRoot.transform, false);
        RectTransform dismissRect = (RectTransform)dismissObject.transform;
        dismissRect.anchorMin = Vector2.zero;
        dismissRect.anchorMax = Vector2.one;
        dismissRect.offsetMin = dismissRect.offsetMax = Vector2.zero;
        Image dismissImage = dismissObject.GetComponent<Image>();
        dismissImage.color = new Color(0f, 0f, 0f, 0f);
        dismissImage.raycastTarget = true;
        Button dismiss = dismissObject.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(Close);

        RectTransform panelRect = ScrollPanelArt.CreateScrollPanel(
            _overlayRoot.transform, "[Runtime] SentenceHintScroll");
        bool onParchment = ScrollPanelArt.ApplyFull(panelRect.GetComponent<Image>());

        GameObject titleObject = new GameObject("[Runtime] SentenceHintTitle", typeof(RectTransform));
        titleObject.transform.SetParent(panelRect, false);
        TextMeshProUGUI title = titleObject.AddComponent<TextMeshProUGUI>();
        title.text = _panelTitle;
        title.raycastTarget = false;
        TutorialFontProvider.ApplyTo(title);
        ScrollPanelArt.PlaceText(title, Rect.MinMaxRect(0.10f, 0.76f, 0.90f, 0.90f), UITextScale.AutoSizeFloor, UITextScale.Title);

        GameObject bodyObject = new GameObject("[Runtime] SentenceHintBody", typeof(RectTransform));
        bodyObject.transform.SetParent(panelRect, false);
        _bodyLabel = bodyObject.AddComponent<TextMeshProUGUI>();
        _bodyLabel.text = _bodyText;
        _bodyLabel.raycastTarget = false;
        TutorialFontProvider.ApplyTo(_bodyLabel);
        // Secondary floor, not AutoSizeFloor: the scroll is trimmed to context +
        // descriptor lines precisely so the body can hold reading size.
        ScrollPanelArt.PlaceText(_bodyLabel, Rect.MinMaxRect(0.12f, 0.26f, 0.88f, 0.72f), UITextScale.Secondary, UITextScale.Body);

        GameObject closeObject = new GameObject(
            "[Runtime] SentenceHintClose", typeof(RectTransform), typeof(Image));
        closeObject.transform.SetParent(panelRect, false);
        closeObject.GetComponent<Image>().color = new Color(0.85f, 0.72f, 0.35f, 1f);
        Button close = closeObject.AddComponent<Button>();
        close.onClick.AddListener(Close);

        GameObject closeLabelObject = new GameObject("[Runtime] SentenceHintCloseLabel", typeof(RectTransform));
        closeLabelObject.transform.SetParent(closeObject.transform, false);
        TextMeshProUGUI closeLabel = closeLabelObject.AddComponent<TextMeshProUGUI>();
        closeLabel.text = _closeLabel;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.raycastTarget = false;
        TutorialFontProvider.ApplyTo(closeLabel);

        // Same bottom-band seat the ready screen's Start/Back buttons take.
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0.18f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.sizeDelta = new Vector2(280f, 88f);
        ScrollPanelArt.SizeButtonLabel(close);

        if (onParchment)
        {
            ScrollPanelArt.InkifyRecursive(panelRect.transform);
            ScrollPanelArt.Inkify(closeLabel);
        }

        _overlayRoot.SetActive(false);
    }

    /// <summary>Composes the scroll body: bold meaning headings over their sentences.</summary>
    private static string ComposeBody(List<SentenceHintContent.Entry> entries)
    {
        if (entries == null || entries.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            SentenceHintContent.Entry entry = entries[i];
            if (entry == null || entry.Lines.Count == 0)
                continue;

            if (builder.Length > 0)
                builder.Append("\n\n");

            if (!string.IsNullOrEmpty(entry.Label))
            {
                builder.Append("<b>");
                builder.Append(entry.Label);
                builder.Append("</b>\n");
            }

            for (int line = 0; line < entry.Lines.Count; line++)
            {
                if (line > 0)
                    builder.Append('\n');
                builder.Append(entry.Lines[line]);
            }
        }
        return builder.ToString();
    }
}
