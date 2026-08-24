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
    [SerializeField] private GameObject _replayAudioButton;
    [SerializeField] private Button _continueButton;

    /// <summary>
    /// Mirrors ActiveCluePresenter: suppresses an announcement that lands on top
    /// of one something else just made. AudioManager uses PlayOneShot, so
    /// pronunciation clips overlap rather than interrupt.
    /// </summary>
    private const float PronunciationDebounceSeconds = 0.5f;

    private readonly List<ContentRequirement> _cards = new();
    private bool _continueRequested;
    private bool _runtimePanelBuilt;
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
        BindButtons();
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        IsPresenting = true;

        for (int index = 0; index < _cards.Count; index++)
        {
            PresentCard(index);
            _continueRequested = false;
            yield return new WaitUntil(() => _continueRequested);
        }

        IsPresenting = false;
        CurrentCardIndex = -1;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
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
        if (config == null || config.learningRequirements == null)
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
        if (config == null || config.learningRequirements == null)
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

        // AC3: glyph and label always render, whatever the audio state — zeroed
        // volume sliders or a missing clip leave every essential element visual.
        CurrentLabel = SpokenValueResolver.ResolveLabel(symbol, spokenValueId);
        if (_labelText != null)
            _labelText.text = CurrentLabel;
        if (_glyphImage != null)
        {
            _glyphImage.sprite = symbol.displaySprite;
            _glyphImage.gameObject.SetActive(_glyphImage.sprite != null);
        }

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
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        _panelRoot = new GameObject("[Runtime] SymbolLearningCard", typeof(RectTransform), typeof(Image));
        _panelRoot.transform.SetParent(parent, false);
        RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 480f);

        // No builtin-sprite lookup: it logs an assert in batch mode; a flat tinted
        // quad is the approved unstyled fallback (see FocusWordPreviewController).
        Image background = _panelRoot.GetComponent<Image>();
        background.color = new Color(0.04f, 0.06f, 0.12f, 0.94f);

        GameObject glyphObject = new GameObject("[Runtime] SymbolLearningGlyph", typeof(RectTransform), typeof(Image));
        glyphObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform glyphRect = glyphObject.GetComponent<RectTransform>();
        glyphRect.anchorMin = new Vector2(0.5f, 1f);
        glyphRect.anchorMax = new Vector2(0.5f, 1f);
        glyphRect.pivot = new Vector2(0.5f, 1f);
        glyphRect.anchoredPosition = new Vector2(0f, -32f);
        glyphRect.sizeDelta = new Vector2(220f, 220f);
        _glyphImage = glyphObject.GetComponent<Image>();
        _glyphImage.preserveAspect = true;
        _glyphImage.raycastTarget = false;

        GameObject labelObject = new GameObject("[Runtime] SymbolLearningLabel", typeof(RectTransform));
        labelObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, -40f);
        labelRect.sizeDelta = new Vector2(-48f, 80f);
        _labelText = labelObject.AddComponent<TextMeshProUGUI>();
        _labelText.fontSize = 48f;
        _labelText.alignment = TextAlignmentOptions.Center;
        _labelText.raycastTarget = false;

        GameObject replayObject = new GameObject(
            "[Runtime] SymbolLearningReplay", typeof(RectTransform), typeof(Image), typeof(Button));
        replayObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform replayRect = replayObject.GetComponent<RectTransform>();
        replayRect.anchorMin = new Vector2(0.5f, 0f);
        replayRect.anchorMax = new Vector2(0.5f, 0f);
        replayRect.pivot = new Vector2(0.5f, 0f);
        replayRect.anchoredPosition = new Vector2(0f, 104f);
        replayRect.sizeDelta = new Vector2(200f, 48f);
        Image replayImage = replayObject.GetComponent<Image>();
        replayImage.color = new Color(0.18f, 0.45f, 0.76f, 1f);
        replayObject.GetComponent<Button>().targetGraphic = replayImage;
        _replayAudioButton = replayObject;

        GameObject replayLabelObject = new GameObject("[Runtime] SymbolLearningReplayLabel", typeof(RectTransform));
        replayLabelObject.transform.SetParent(replayObject.transform, false);
        TextMeshProUGUI replayLabel = replayLabelObject.AddComponent<TextMeshProUGUI>();
        replayLabel.text = "Pakinggan";
        replayLabel.fontSize = 22f;
        replayLabel.alignment = TextAlignmentOptions.Center;
        replayLabel.raycastTarget = false;

        GameObject continueObject = new GameObject(
            "[Runtime] SymbolLearningContinue", typeof(RectTransform), typeof(Image));
        continueObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform continueRect = continueObject.GetComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(0.5f, 0f);
        continueRect.anchorMax = new Vector2(0.5f, 0f);
        continueRect.pivot = new Vector2(0.5f, 0f);
        continueRect.anchoredPosition = new Vector2(0f, 32f);
        continueRect.sizeDelta = new Vector2(240f, 56f);
        continueObject.GetComponent<Image>().color = new Color(0.85f, 0.72f, 0.35f, 1f);
        _continueButton = continueObject.AddComponent<Button>();

        GameObject continueLabelObject = new GameObject("[Runtime] SymbolLearningContinueLabel", typeof(RectTransform));
        continueLabelObject.transform.SetParent(continueObject.transform, false);
        TextMeshProUGUI continueLabel = continueLabelObject.AddComponent<TextMeshProUGUI>();
        continueLabel.text = "Magpatuloy";
        continueLabel.fontSize = 26f;
        continueLabel.alignment = TextAlignmentOptions.Center;
        continueLabel.raycastTarget = false;

        _panelRoot.SetActive(false);
    }
}
