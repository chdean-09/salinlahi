using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-157: the SymbolLearning phase surface — one card per Instruction-kind
/// learning requirement, presenting the glyph and the approved level-context
/// label (E/I, O/U, DA/RA follow the requirement's spokenValueId) with a
/// replay-audio control. When a card becomes active its pronunciation plays
/// once, debounced against anything already on the pronunciation bus so clips
/// never stack (AudioManager PlayOneShots). A card whose spoken value has no
/// approved clip stays fully readable and simply hides the replay control —
/// audio disabled or missing never gates essential information.
/// </summary>
public class SymbolLearningCardController : MonoBehaviour
{
    [Header("Authored wiring (optional — a runtime panel is built when absent)")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Image _glyphImage;
    [SerializeField] private TextMeshProUGUI _labelText;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private GameObject _replayAudioButton;
    [SerializeField] private Button _continueButton;

    /// <summary>
    /// Mirrors ActiveCluePresenter: suppresses an announcement that lands on top
    /// of one something else just made. AudioManager uses PlayOneShot, so
    /// pronunciation clips overlap rather than interrupt.
    /// </summary>
    private const float PronunciationDebounceSeconds = 0.5f;

    // Content bands inside the shared scroll rect (ScrollPanelArt.ScrollArea),
    // all kept inside ScrollPanelArt.FullSafeArea. Text and button bands are a
    // tenth of the panel height so auto-sizing can actually reach the title
    // tier — the old 8% bands capped every label near the body floor no matter
    // how high fontSizeMax was set.
    private static readonly Rect ProgressBand = Rect.MinMaxRect(0.20f, 0.70f, 0.80f, 0.80f);
    private static readonly Rect LabelBand = Rect.MinMaxRect(0.20f, 0.59f, 0.80f, 0.69f);
    private static readonly Rect GlyphBand = Rect.MinMaxRect(0.30f, 0.38f, 0.70f, 0.58f);
    private static readonly Rect ReplayBand = Rect.MinMaxRect(0.30f, 0.27f, 0.70f, 0.37f);
    private static readonly Rect ContinueBand = Rect.MinMaxRect(0.30f, 0.16f, 0.70f, 0.26f);

    private readonly List<ContentRequirement> _cards = new();
    private GameObject _overlayRoot;
    private bool _continueRequested;
    private bool _runtimePanelBuilt;
    private bool _onParchment;
    private Button _replayAudioButtonComponent;
    private float _lastPronunciationTime = float.NegativeInfinity;

    /// <summary>True while a card is up and waiting for Continue.</summary>
    public bool IsPresenting { get; private set; }

    /// <summary>Instruction-kind cards collected from the current presentation.</summary>
    public int CardCount => _cards.Count;

    /// <summary>Index of the active card, or -1 outside a presentation.</summary>
    public int CurrentCardIndex { get; private set; } = -1;

    /// <summary>The visible level-context label of the active card.</summary>
    public string CurrentLabel { get; private set; }

    /// <summary>True when the active card resolved an approved clip to replay.</summary>
    public bool IsReplayAvailable { get; private set; }

    private ContentRequirement CurrentCard =>
        CurrentCardIndex >= 0 && CurrentCardIndex < _cards.Count
            ? _cards[CurrentCardIndex]
            : null;

    private void OnEnable()
    {
        // Stamp every pronunciation on the bus, whoever raised it, so this card's
        // own announcement can tell when it would collide with one already playing.
        EventBus.OnPronunciationRequested += HandlePronunciationRequested;
        EventBus.OnSpokenPronunciationRequested += HandleSpokenPronunciationRequested;
        BindButtons();
    }

    private void OnDisable()
    {
        EventBus.OnPronunciationRequested -= HandlePronunciationRequested;
        EventBus.OnSpokenPronunciationRequested -= HandleSpokenPronunciationRequested;

        if (_replayAudioButtonComponent != null)
            _replayAudioButtonComponent.onClick.RemoveListener(ReplayAudio);
        _replayAudioButtonComponent = null;

        if (_continueButton != null)
            _continueButton.onClick.RemoveListener(Continue);
    }

    /// <summary>
    /// Presents one card per Instruction-kind learning requirement and completes
    /// when the player has advanced past every card. Yields nothing when the
    /// level has no presentable requirement, so the flow driver auto-completes
    /// the phase and nothing can deadlock.
    /// </summary>
    public IEnumerator Present(LevelConfigSO config)
    {
        CollectCards(config);
        if (_cards.Count == 0)
            yield break;

        EnsurePanel();
        EnsureModalChrome();
        EnsureProgressText();
        BindButtons();
        EnsureParchmentArt();
        SetVisible(true);
        IsPresenting = true;

        for (int index = 0; index < _cards.Count; index++)
        {
            PresentCard(index);
            _continueRequested = false;
            yield return new WaitUntil(() => _continueRequested);
        }

        IsPresenting = false;
        CurrentCardIndex = -1;
        SetVisible(false);
    }

    /// <summary>Advances past the active card; the last card ends the presentation.</summary>
    public void Continue()
    {
        _continueRequested = true;
    }

    /// <summary>Replays the active card's pronunciation on demand.</summary>
    public void ReplayAudio()
    {
        ContentRequirement card = CurrentCard;
        if (card?.symbolValue?.symbol != null)
        {
            EventBus.RaiseSpokenPronunciationRequested(
                card.symbolValue.symbol, card.symbolValue.spokenValueId);
        }
    }

    /// <summary>
    /// True when the config carries at least one card this surface would present.
    /// The flow executor uses this so a level with only malformed requirements
    /// skips the phase without ever taking drawing suppression.
    /// </summary>
    public static bool HasPresentableRequirement(LevelConfigSO config)
    {
        if (config == null || config.suppressSymbolLearningCards || config.learningRequirements == null)
            return false;

        for (int i = 0; i < config.learningRequirements.Count; i++)
        {
            if (IsPresentable(config.learningRequirements[i]))
                return true;
        }

        return false;
    }

    private static bool IsPresentable(ContentRequirement requirement)
    {
        return requirement != null
            && requirement.kind == ContentRequirementKind.Instruction
            && requirement.symbolValue?.symbol != null;
    }

    private void CollectCards(LevelConfigSO config)
    {
        _cards.Clear();
        if (config == null || config.suppressSymbolLearningCards || config.learningRequirements == null)
            return;

        for (int i = 0; i < config.learningRequirements.Count; i++)
        {
            ContentRequirement requirement = config.learningRequirements[i];
            if (IsPresentable(requirement))
                _cards.Add(requirement);
        }
    }

    private void PresentCard(int index)
    {
        CurrentCardIndex = index;
        ContentRequirement card = _cards[index];
        BaybayinCharacterSO symbol = card.symbolValue.symbol;
        string spokenValueId = card.symbolValue.spokenValueId;

        // Match the glyph art the player sees above enemies. Badge art has no romanised
        // label, so the value label remains visible as an overlay on the badge.
        CurrentLabel = SpokenValueResolver.ResolveLabel(symbol, spokenValueId);
        Sprite glyphSprite = symbol.badgeSprite != null ? symbol.badgeSprite : symbol.displaySprite;
        if (_labelText != null)
        {
            _labelText.text = CurrentLabel;
            _labelText.gameObject.SetActive(true);
        }
        if (_glyphImage != null)
        {
            _glyphImage.sprite = glyphSprite;
            _glyphImage.gameObject.SetActive(glyphSprite != null);
        }
        if (_progressText != null)
            _progressText.text = BuildProgressText(index, _cards.Count);

        // The replay control only offers what can actually play (mirrors
        // ActiveCluePresenter's _replayAudioButton gating).
        AudioClip clip = SpokenValueResolver.ResolveClip(symbol, spokenValueId);
        IsReplayAvailable = clip != null;
        if (_replayAudioButton != null)
            _replayAudioButton.SetActive(IsReplayAvailable);

        // AC1: announce the card once as it becomes active — only when nothing
        // else just announced, so unrelated clips never overlap. The replay
        // control covers a debounced card on demand.
        if (clip != null
            && Time.unscaledTime - _lastPronunciationTime > PronunciationDebounceSeconds)
        {
            EventBus.RaiseSpokenPronunciationRequested(symbol, spokenValueId);
        }
    }

    public static string BuildProgressText(int zeroBasedIndex, int total)
    {
        int safeTotal = Mathf.Max(1, total);
        int position = Mathf.Clamp(zeroBasedIndex + 1, 1, safeTotal);
        return $"Symbol {position} of {safeTotal}";
    }

    private void HandlePronunciationRequested(BaybayinCharacterSO character)
    {
        _lastPronunciationTime = Time.unscaledTime;
    }

    private void HandleSpokenPronunciationRequested(BaybayinCharacterSO character, string spokenValueId)
    {
        _lastPronunciationTime = Time.unscaledTime;
    }

    private void BindButtons()
    {
        Button nextReplay = _replayAudioButton != null
            ? _replayAudioButton.GetComponent<Button>()
            : null;
        if (_replayAudioButtonComponent != nextReplay)
        {
            if (_replayAudioButtonComponent != null)
                _replayAudioButtonComponent.onClick.RemoveListener(ReplayAudio);

            _replayAudioButtonComponent = nextReplay;
            if (_replayAudioButtonComponent != null)
                _replayAudioButtonComponent.onClick.AddListener(ReplayAudio);
        }

        if (_continueButton != null)
        {
            // Rebinding after a remove is idempotent; UnityEvents tolerate the
            // remove of a listener that was never added.
            _continueButton.onClick.RemoveListener(Continue);
            _continueButton.onClick.AddListener(Continue);
        }
    }

    private void EnsurePanel()
    {
        if (_panelRoot != null || _runtimePanelBuilt)
            return;

        _runtimePanelBuilt = true;

        // Prefer the HUD canvas so the authored scene renders the card; a bare
        // test scene parents under this controller (card data still observable).
        Canvas canvas = ScrollPanelArt.ResolveModalCanvas(this);
        Transform parent = canvas != null ? canvas.transform : transform;

        _overlayRoot = ScrollPanelArt.CreateDimOverlay(
            parent, "[Runtime] SymbolLearningCardOverlay");
        RectTransform panelRect = ScrollPanelArt.CreateScrollPanel(
            _overlayRoot.transform, "[Runtime] SymbolLearningCard");
        _panelRoot = panelRect.gameObject;

        // No builtin-sprite lookup: it logs an assert in batch mode; a flat tinted
        // quad is the approved unstyled fallback (see FocusWordPreviewController).
        Image background = panelRect.GetComponent<Image>();
        _onParchment = ScrollPanelArt.ApplyFull(background);

        GameObject glyphObject = new GameObject("[Runtime] SymbolLearningGlyph", typeof(RectTransform), typeof(Image));
        glyphObject.transform.SetParent(panelRect.transform, false);
        ScrollPanelArt.SetAnchors(
            glyphObject.GetComponent<RectTransform>(), GlyphBand);
        _glyphImage = glyphObject.GetComponent<Image>();
        _glyphImage.preserveAspect = true;
        _glyphImage.raycastTarget = false;

        GameObject labelObject = new GameObject("[Runtime] SymbolLearningLabel", typeof(RectTransform));
        labelObject.transform.SetParent(panelRect.transform, false);
        ScrollPanelArt.SetAnchors(
            labelObject.GetComponent<RectTransform>(), LabelBand);
        _labelText = labelObject.AddComponent<TextMeshProUGUI>();
        _labelText.fontSize = UITextScale.Title;
        _labelText.alignment = TextAlignmentOptions.Center;
        _labelText.raycastTarget = false;
        TutorialFontProvider.ApplyTo(_labelText);

        GameObject replayObject = new GameObject(
            "[Runtime] SymbolLearningReplay", typeof(RectTransform), typeof(Image), typeof(Button));
        replayObject.transform.SetParent(panelRect.transform, false);
        ScrollPanelArt.SetAnchors(
            replayObject.GetComponent<RectTransform>(), ReplayBand);
        Image replayImage = replayObject.GetComponent<Image>();
        replayImage.color = new Color(0.18f, 0.45f, 0.76f, 1f);
        replayObject.GetComponent<Button>().targetGraphic = replayImage;
        _replayAudioButton = replayObject;

        GameObject replayLabelObject = new GameObject("[Runtime] SymbolLearningReplayLabel", typeof(RectTransform));
        replayLabelObject.transform.SetParent(replayObject.transform, false);
        TextMeshProUGUI replayLabel = replayLabelObject.AddComponent<TextMeshProUGUI>();
        replayLabel.text = "Listen";
        replayLabel.fontSize = UITextScale.Body;
        replayLabel.alignment = TextAlignmentOptions.Center;
        replayLabel.raycastTarget = false;
        TutorialFontProvider.ApplyTo(replayLabel);

        GameObject continueObject = new GameObject(
            "[Runtime] SymbolLearningContinue", typeof(RectTransform), typeof(Image));
        continueObject.transform.SetParent(panelRect.transform, false);
        ScrollPanelArt.SetAnchors(
            continueObject.GetComponent<RectTransform>(), ContinueBand);
        continueObject.GetComponent<Image>().color = new Color(0.85f, 0.72f, 0.35f, 1f);
        _continueButton = continueObject.AddComponent<Button>();

        GameObject continueLabelObject = new GameObject("[Runtime] SymbolLearningContinueLabel", typeof(RectTransform));
        continueLabelObject.transform.SetParent(continueObject.transform, false);
        TextMeshProUGUI continueLabel = continueLabelObject.AddComponent<TextMeshProUGUI>();
        continueLabel.text = "Continue";
        continueLabel.fontSize = UITextScale.Body;
        continueLabel.alignment = TextAlignmentOptions.Center;
        continueLabel.raycastTarget = false;
        TutorialFontProvider.ApplyTo(continueLabel);

        if (_onParchment)
        {
            ScrollPanelArt.InkifyRecursive(panelRect.transform);
            ScrollPanelArt.Inkify(continueLabel);
        }

        _overlayRoot.SetActive(false);
    }

    /// <summary>
    /// Wraps a scene-authored panel in the same dim overlay the runtime path
    /// builds, then seats it at the shared scroll rect so an authored
    /// <c>SymbolLearningPanel</c> presents identically.
    /// </summary>
    private void EnsureModalChrome()
    {
        if (_panelRoot == null || _overlayRoot != null)
            return;

        Canvas canvas = ScrollPanelArt.ResolveModalCanvas(this);
        Transform parent = canvas != null ? canvas.transform : _panelRoot.transform.parent;

        _overlayRoot = ScrollPanelArt.CreateDimOverlay(
            parent, "[Runtime] SymbolLearningCardOverlay");
        RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
        panelRect.SetParent(_overlayRoot.transform, false);
        panelRect.SetAsLastSibling();
        ScrollPanelArt.SetAnchors(panelRect, ScrollPanelArt.ScrollArea);
    }

    private void SetVisible(bool visible)
    {
        // Authored panels can be serialized inactive, so the panel itself is
        // toggled alongside the overlay rather than relying on the hierarchy.
        if (_overlayRoot != null)
            _overlayRoot.SetActive(visible);
        if (_panelRoot != null)
            _panelRoot.SetActive(visible);
    }

    /// <summary>
    /// Seats the children inside the paper band of the shared scroll rect.
    /// A missing sprite keeps the flat fallback panel at the same rect.
    /// </summary>
    private void EnsureParchmentArt()
    {
        if (_panelRoot == null)
            return;

        Image background = _panelRoot.GetComponent<Image>();
        if (!_onParchment && !ScrollPanelArt.ApplyFull(background))
            return;
        _onParchment = true;

        ScrollPanelArt.InkifyRecursive(_panelRoot.transform);
        if (_continueButton != null)
            ScrollPanelArt.Inkify(_continueButton.GetComponentInChildren<TMP_Text>(true));

        ScrollPanelArt.PlaceText(
            _progressText, ProgressBand,
            UITextScale.AutoSizeFloor, UITextScale.Title);
        ScrollPanelArt.PlaceText(
            _labelText, LabelBand,
            UITextScale.AutoSizeFloor, UITextScale.Display);
        if (_glyphImage != null)
            ScrollPanelArt.SetAnchors(_glyphImage.rectTransform, GlyphBand);
        if (_replayAudioButton != null)
        {
            ScrollPanelArt.PlaceButton(
                _replayAudioButton.GetComponent<Button>(), ReplayBand);
        }
        ScrollPanelArt.PlaceButton(_continueButton, ContinueBand);
    }

    private void EnsureProgressText()
    {
        if (_progressText != null || _panelRoot == null)
            return;

        GameObject progressObject = new GameObject(
            "[Runtime] SymbolLearningProgress", typeof(RectTransform));
        progressObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform rect = progressObject.GetComponent<RectTransform>();
        ScrollPanelArt.SetAnchors(rect, ProgressBand);

        _progressText = progressObject.AddComponent<TextMeshProUGUI>();
        _progressText.fontSize = UITextScale.Title;
        _progressText.color = new Color(0.82f, 0.86f, 0.94f, 1f);
        _progressText.alignment = TextAlignmentOptions.Center;
        _progressText.raycastTarget = false;
        TutorialFontProvider.ApplyTo(_progressText);
        if (_onParchment)
            ScrollPanelArt.Inkify(_progressText);
    }
}
