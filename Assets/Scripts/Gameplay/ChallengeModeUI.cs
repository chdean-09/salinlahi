using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChallengeModeUI : MonoBehaviour
{
    private ChallengeFlowController _controller;
    private TextMeshProUGUI _progressText;
    private TextMeshProUGUI _promptText;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _timerText;
    private RectTransform _choicesRoot;
    private RectTransform _actionsRoot;
    private readonly Dictionary<string, Button> _choiceButtons = new Dictionary<string, Button>();
    private readonly List<Button> _actionButtons = new List<Button>();
    private string _choiceCacheKey;
    private Button _hintButton;
    private TextMeshProUGUI _hintButtonLabel;
    private HintModal _hintModal;
    private ChallengeSession _hintModalSession;
    private ChallengeUnitDefinition _hintModalUnit;
    private int _hintModalSlotIndex;
    private string _feedbackText = string.Empty;
    private ScrollRect _promptScroll;
    private string _renderedUnitId;

    public void Bind(ChallengeFlowController controller)
    {
        _controller = controller;
        BuildIfNeeded();
    }

    public void Render(ChallengeSession session)
    {
        if (session == null)
            return;

        BuildIfNeeded();
        ChallengeUnitDefinition unit = session.CurrentUnitDefinition;
        if (_hintModal != null && _hintModal.IsOpen
            && (session != _hintModalSession || unit != _hintModalUnit
                || session.State == ChallengeSessionState.Completed
                || session.State == ChallengeSessionState.Exited
                || session.State == ChallengeSessionState.Failed
                || session.State == ChallengeSessionState.CheckpointReset
                || (_hintModal.CurrentMode == HintModal.Mode.Confirm
                    && session.CurrentSlotIndex != _hintModalSlotIndex)))
            _hintModal.Cancel();
        if (unit != null)
        {
            _progressText.text = $"Hamon {session.CurrentUnitIndex + 1}  |  Mga Mali {session.Errors}  |  Puso {session.HeartsRemaining}";
            _promptText.text = BuildPrompt(unit, session);
            if (_renderedUnitId != unit.unitId || session.LastEvent == ChallengeSessionEvent.Entered)
            {
                _renderedUnitId = unit.unitId;
                Canvas.ForceUpdateCanvases();
                _promptScroll.verticalNormalizedPosition = 1f;
            }
        }
        _timerText.text = BuildTimerText(unit, session);
        _statusText.text = BuildStatusText(session);
        RebuildChoices(unit, session);
        SetActionInteractivity(session);
    }

    public void ShowFeedback(string message)
    {
        BuildIfNeeded();
        _statusText.text = message ?? string.Empty;
        _feedbackText = _statusText.text;
    }

    private void BuildIfNeeded()
    {
        if (_progressText != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("ChallengeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            transform.SetParent(canvas.transform, false);
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 250);
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform panel = gameObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.06f, 0.05f);
        panel.anchorMax = new Vector2(0.94f, 0.95f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;

        Image panelImage = GetComponent<Image>();
        if (panelImage == null)
            panelImage = gameObject.AddComponent<Image>();
        panelImage.color = ScrollPanelArt.FlatPanelColor;
        panelImage.raycastTarget = false;
        bool onParchment = ScrollPanelArt.ApplyFull(panelImage);

        _progressText = CreateLabel("Progress", UITextScale.Caption, new Vector2(0.18f, 0.70f), new Vector2(0.68f, 0.80f));
        _progressText.textWrappingMode = TextWrappingModes.Normal;
        _timerText = CreateLabel("Timer", UITextScale.Caption, new Vector2(0.70f, 0.70f), new Vector2(0.82f, 0.80f));
        _timerText.textWrappingMode = TextWrappingModes.Normal;
        GameObject viewport = new GameObject("PromptViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.18f, 0.58f);
        viewportRect.anchorMax = new Vector2(0.82f, 0.69f);
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.clear;
        _promptText = CreateLabel("Prompt", UITextScale.Title, Vector2.zero, Vector2.one);
        _promptText.transform.SetParent(viewport.transform, false);
        RectTransform promptRect = _promptText.rectTransform;
        promptRect.anchorMin = new Vector2(0f, 1f);
        promptRect.anchorMax = Vector2.one;
        promptRect.pivot = new Vector2(0.5f, 1f);
        promptRect.sizeDelta = Vector2.zero;
        promptRect.anchoredPosition = Vector2.zero;
        _promptText.alignment = TextAlignmentOptions.TopLeft;
        _promptText.enableAutoSizing = false;
        _promptText.textWrappingMode = TextWrappingModes.Normal;
        _promptText.lineSpacing = 8f;
        ContentSizeFitter fitter = _promptText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _promptScroll = viewport.GetComponent<ScrollRect>();
        _promptScroll.viewport = viewportRect;
        _promptScroll.content = promptRect;
        _promptScroll.horizontal = false;
        _promptScroll.movementType = ScrollRect.MovementType.Clamped;
        ScrollPanelArt.EnsureVerticalScrollbar(_promptScroll);
        _statusText = CreateLabel("Status", UITextScale.Body, new Vector2(0.18f, 0.50f), new Vector2(0.82f, 0.57f));
        _statusText.textWrappingMode = TextWrappingModes.Normal;
        _statusText.overflowMode = TextOverflowModes.Ellipsis;

        GameObject choices = new GameObject("AnswerChoices", typeof(RectTransform), typeof(GridLayoutGroup));
        choices.transform.SetParent(transform, false);
        _choicesRoot = choices.GetComponent<RectTransform>();
        _choicesRoot.anchorMin = new Vector2(0.18f, 0.30f);
        _choicesRoot.anchorMax = new Vector2(0.82f, 0.49f);
        _choicesRoot.offsetMin = _choicesRoot.offsetMax = Vector2.zero;
        GridLayoutGroup choicesLayout = choices.GetComponent<GridLayoutGroup>();
        choicesLayout.spacing = new Vector2(12f, 12f);
        choicesLayout.padding = new RectOffset(8, 8, 4, 4);
        choicesLayout.childAlignment = TextAnchor.MiddleCenter;
        choicesLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        choicesLayout.constraintCount = 2;

        GameObject actions = new GameObject("ChallengeActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        actions.transform.SetParent(transform, false);
        _actionsRoot = actions.GetComponent<RectTransform>();
        _actionsRoot.anchorMin = new Vector2(0.18f, 0.16f);
        _actionsRoot.anchorMax = new Vector2(0.82f, 0.29f);
        _actionsRoot.offsetMin = _actionsRoot.offsetMax = Vector2.zero;
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleCenter;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;

        // SALIN-231. The Hint button no longer spends the hint: it opens a modal that
        // discloses the cost first and offers confirm/cancel. Its label also carries the
        // exhausted state ("No Hints Left"), replacing the silent no-op that used to be
        // the only feedback once the tier-5 budget was gone.
        //
        // The board deliberately offers no Retry or Exit. Retry only re-ran
        // Session.Retry -> ResetToCheckpoint, which the error paths already do for free
        // (supportive retry on tiers 1-2, checkpoint reset on 3-5), and Exit abandoned the
        // whole level run — the one way the game could end while the choices were still
        // unanswered. The only way off the board is answering; quitting the level stays on
        // the pause menu, where its cost is honest.
        _hintButton = CreateActionButton(HintModalCopy.AvailableButtonLabel, OpenHintModal);
        _hintButtonLabel = _hintButton.GetComponentInChildren<TextMeshProUGUI>();

        if (onParchment)
            ScrollPanelArt.InkifyRecursive(transform);
    }

    private void RebuildChoices(ChallengeUnitDefinition unit, ChallengeSession session)
    {
        if (_choicesRoot == null)
            return;

        bool isSelectableMode = IsSelectableMode(unit);
        string cacheKey = BuildChoiceCacheKey(unit);
        if (!string.Equals(cacheKey, _choiceCacheKey, System.StringComparison.Ordinal))
        {
            ClearChoiceButtons();
            _choiceCacheKey = cacheKey;

            if (isSelectableMode)
            {
                foreach (string occurrenceId in unit.candidateOccurrenceIds ?? new string[0])
                {
                    string captured = occurrenceId;
                    ChallengeTokenDefinition token = FindToken(unit, captured);
                    string label = token == null ? captured : token.displayText;
                    _choiceButtons[captured] = CreateChoiceButton(label, () => _controller?.SubmitPlacement(captured));
                }
            }
        }

        bool choicesVisible = isSelectableMode
            && session.State == ChallengeSessionState.Active
            && !session.IsMemoryRevealActive;
        _choicesRoot.gameObject.SetActive(choicesVisible);

        foreach (KeyValuePair<string, Button> choice in _choiceButtons)
        {
            bool alreadyPlaced = ContainsOccurrence(session.CurrentProgress, choice.Key);
            choice.Value.gameObject.SetActive(choicesVisible);
            choice.Value.interactable = choicesVisible && !alreadyPlaced;
            ColorBlock colors = choice.Value.colors;
            colors.disabledColor = alreadyPlaced ? new Color(0.45f, 0.68f, 0.42f) : Color.gray;
            choice.Value.colors = colors;
        }
        ConfigureChoiceGrid();
    }

    private void OnRectTransformDimensionsChange() => ConfigureChoiceGrid();

    private void ConfigureChoiceGrid()
    {
        if (_choicesRoot == null)
            return;
        GridLayoutGroup layout = _choicesRoot.GetComponent<GridLayoutGroup>();
        int rows = Mathf.Max(1, Mathf.CeilToInt(_choiceButtons.Count / 2f));
        layout.cellSize = new Vector2(
            Mathf.Max(0f, (_choicesRoot.rect.width - layout.padding.horizontal - layout.spacing.x) / 2f),
            Mathf.Min(176f, Mathf.Max(0f, (_choicesRoot.rect.height - layout.padding.vertical - layout.spacing.y * (rows - 1)) / rows)));
    }

    private void ClearChoiceButtons()
    {
        foreach (Button button in _choiceButtons.Values)
        {
            if (button != null)
            {
                button.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(button.gameObject);
                else
                    DestroyImmediate(button.gameObject);
            }
        }
        _choiceButtons.Clear();
    }

    private static bool IsSelectableMode(ChallengeUnitDefinition unit)
    {
        return unit != null
            && (unit.mode == ChallengeMode.WordPlacement
                || unit.mode == ChallengeMode.SentenceRestoration
                || unit.mode == ChallengeMode.ParagraphRestoration
                || unit.mode == ChallengeMode.TimedMemory);
    }

    private static string BuildChoiceCacheKey(ChallengeUnitDefinition unit)
    {
        if (!IsSelectableMode(unit))
            return string.Empty;

        return $"{unit.unitId}|{unit.mode}|{string.Join("|", unit.candidateOccurrenceIds ?? new string[0])}";
    }

    private static bool ContainsOccurrence(IReadOnlyCollection<string> occurrences, string occurrenceId)
    {
        foreach (string occurrence in occurrences)
        {
            if (occurrence == occurrenceId)
                return true;
        }
        return false;
    }

    private string BuildPrompt(ChallengeUnitDefinition unit, ChallengeSession session)
    {
        if (unit == null)
            return string.Empty;
        if (session.IsMemoryRevealActive)
        {
            List<string> memoryTokens = new List<string>();
            foreach (ChallengeSlotDefinition slot in unit.slots ?? new ChallengeSlotDefinition[0])
            {
                ChallengeTokenDefinition token = slot == null ? null : FindToken(unit, slot.expectedOccurrenceId);
                if (token != null)
                    memoryTokens.Add(token.displayText);
            }
            return $"{unit.prompt}\nTandaan: {string.Join("  ", memoryTokens)}";
        }
        int slotIndex = 0;
        return System.Text.RegularExpressions.Regex.Replace(unit.prompt ?? string.Empty, @"_{2,}", match =>
        {
            ChallengeSlotDefinition[] slots = unit.slots ?? new ChallengeSlotDefinition[0];
            if (slotIndex >= slots.Length)
                return match.Value;
            ChallengeSlotDefinition slot = slots[slotIndex++];
            if (slot == null)
                return match.Value;
            ChallengeTokenDefinition token = FindToken(unit, slot.expectedOccurrenceId);
            if (token == null || string.IsNullOrEmpty(token.displayText))
                return match.Value;
            bool placed = ContainsOccurrence(session.CurrentProgress, slot.expectedOccurrenceId)
                || ContainsOccurrence(session.CommittedOccurrenceIds, slot.expectedOccurrenceId);
            string answer = $"<b>{token.displayText}</b>";
            if (placed)
                return $"<nobr>{answer}</nobr>";

            // Match the blank to the answer's natural font width, without padding the
            // filled word or spreading its letters. Both remain unbroken when wrapping.
            // Include a following word in both measurements so TMP's last-glyph
            // bearing is cancelled out and the slot uses the inline advance width.
            const string suffix = " x";
            float width = _promptText.GetPreferredValues(answer + suffix, Mathf.Infinity, Mathf.Infinity).x
                - _promptText.GetPreferredValues(suffix, Mathf.Infinity, Mathf.Infinity).x;
            string spacing = (width / token.displayText.Length).ToString(
                "0.###", System.Globalization.CultureInfo.InvariantCulture);
            return $"<nobr><mspace={spacing}>{new string('_', token.displayText.Length)}</mspace></nobr>";
        });
    }

    private static string BuildTimerText(ChallengeUnitDefinition unit, ChallengeSession session)
    {
        if (unit == null)
            return string.Empty;
        if (session.IsMemoryRevealActive)
            return $"Tandaan {session.MemoryRevealRemaining:0.0}";
        return unit.timerSeconds > 0f ? $"Oras {session.RemainingTime:0.0}" : string.Empty;
    }

    private string BuildStatusText(ChallengeSession session)
    {
        string hint = BuildHintText(session);
        string feedback = session.LastEvent switch
        {
            ChallengeSessionEvent.Entered => string.Empty,
            ChallengeSessionEvent.PlacementAccepted => "Tama! Susunod na salita.",
            ChallengeSessionEvent.TraceAccepted => "Tama! Ipagpatuloy ang pagguhit.",
            ChallengeSessionEvent.SupportiveRetry => "Subukan muli. Ligtas ang tamang progreso mo.",
            ChallengeSessionEvent.RetryOpened => "Subukan muli gamit ang mga kasalukuyang pahiwatig.",
            ChallengeSessionEvent.HintShown => string.Empty,
            ChallengeSessionEvent.HintApplied => string.Empty,
            ChallengeSessionEvent.TimedOut => "Naubos na ang oras.",
            ChallengeSessionEvent.PenaltyApplied => "Nabawasan ka ng isang puso. Babalik sa checkpoint.",
            ChallengeSessionEvent.CheckpointReset => "Naibalik ang checkpoint at kumpleto ang mga pahiwatig.",
            ChallengeSessionEvent.CheckpointReopened => "Naibalik ang checkpoint. Subukan muli.",
            ChallengeSessionEvent.MemoryRevealStarted => "Tandaan ang pagkakasunod-sunod.",
            ChallengeSessionEvent.MemoryRecallStarted => "Nagsimula na ang yugto ng pag-alala.",
            ChallengeSessionEvent.UnitSucceeded => "Tama! Naibalik na.",
            ChallengeSessionEvent.Completed => "Tama! Tapos na ang hamon.",
            ChallengeSessionEvent.Exited => "Lumabas na sa hamon.",
            ChallengeSessionEvent.Failed => "Nabigo sa hamon.",
            _ => _feedbackText
        };
        _feedbackText = feedback;
        if (string.IsNullOrEmpty(hint))
            return feedback;
        return string.IsNullOrEmpty(feedback) ? hint : $"{feedback}\n{hint}";
    }

    /// <summary>
    /// SALIN-231 AC-5. The in-encounter record of a hint the player PAID for.
    ///
    /// ⚠️ WHAT CHANGED AND WHY IT MATTERS. This used to return "Hint: {token.displayText}"
    /// — the whole correct answer for the slot, free. Gating that behind a modal would have
    /// been cosmetic: the answer was still handed over, and Global "Hints"
    /// (docs/audit/AUDIT.md:169) forbids a hint that finishes a required word. It now shows
    /// Filipino synonyms with the answer withheld, so the player still has to
    /// assemble it.
    ///
    /// The line persists after the modal closes rather than living only inside the card:
    /// a hint is information, not decaying scaffolding (D-014), so its visual form stays.
    /// </summary>
    private string BuildHintText(ChallengeSession session)
    {
        if (string.IsNullOrEmpty(session.HintOccurrenceId) || session.CurrentUnitDefinition == null)
            return string.Empty;

        string hint = _controller == null
            ? string.Empty
            : _controller.ResolveHintText(session.CurrentUnitDefinition, session.HintOccurrenceId);

        return !string.IsNullOrWhiteSpace(hint)
            ? HintModalCopy.HintStatusLine(hint)
            : HintModalCopy.NoHintAvailableBody;
    }

    private static ChallengeTokenDefinition FindToken(ChallengeUnitDefinition unit, string occurrenceId)
    {
        foreach (ChallengeTokenDefinition token in unit.tokens ?? new ChallengeTokenDefinition[0])
        {
            if (token != null && token.occurrenceId == occurrenceId)
                return token;
        }
        return null;
    }

    private TextMeshProUGUI CreateLabel(string name, float size, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = size;
        text.fontSizeMax = size;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
        return text;
    }

    private Button CreateActionButton(string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(label, _actionsRoot, action, SlateButtonFill, Color.white);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 280f;
        layout.preferredHeight = 60f;
        _actionButtons.Add(button);
        return button;
    }

    /// <summary>
    /// SALIN-231. Opens the hint confirmation modal. Reads the session; spends
    /// nothing — only HintModal.Confirm reaches RequestHint. The Hint control stays
    /// interactable when the budget is spent so the exhausted card can explain itself
    /// (AC-4) instead of the button silently doing nothing.
    /// </summary>
    private void OpenHintModal()
    {
        ChallengeSession session = _controller == null ? null : _controller.Session;
        if (session == null)
            return;

        if (_hintModal == null)
            _hintModal = HintModal.CreateRuntime(transform.parent == null ? transform : transform.parent);

        ChallengeUnitDefinition unit = session.CurrentUnitDefinition;
        string occurrenceId = unit?.slots != null && session.CurrentSlotIndex < unit.slots.Length
            ? unit.slots[session.CurrentSlotIndex]?.expectedOccurrenceId
            : unit?.tokens != null && session.CurrentSlotIndex < unit.tokens.Length
                ? unit.tokens[session.CurrentSlotIndex]?.occurrenceId
                : null;
        string hint = _controller.ResolveHintText(unit, occurrenceId);
        _hintModalSession = session;
        _hintModalUnit = unit;
        _hintModalSlotIndex = session.CurrentSlotIndex;
        _hintModal.Open(
            session,
            hint,
            () => _controller?.RequestHint());
    }

    private void SetActionInteractivity(ChallengeSession session)
    {
        bool active = session != null && session.State == ChallengeSessionState.Active;
        foreach (Button button in _actionButtons)
        {
            if (button != null)
                button.interactable = active;
        }

        // SALIN-231 AC-4. BTN-HINT's exhausted copy, verbatim.
        if (_hintButtonLabel != null)
            _hintButtonLabel.text = HintModal.HintControlLabel(session);
    }

    private Button CreateChoiceButton(string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(label, _choicesRoot, action, GoldButtonFill, ScrollPanelArt.InkColor);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 150f;
        layout.preferredHeight = 60f;
        return button;
    }

    // Fallback colors when the shared parchment button artwork is unavailable.
    private static readonly Color GoldButtonFill = ScrollPanelArt.GoldButtonFill;
    private static readonly Color SlateButtonFill = ScrollPanelArt.SlateButtonFill;

    private static Button CreateButton(
        string label,
        Transform parent,
        UnityEngine.Events.UnityAction action,
        Color fill,
        Color labelColor)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = fill;
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(action);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(go.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = UITextScale.Body;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.fontSizeMin = UITextScale.Body;
        text.fontSizeMax = UITextScale.Body;
        text.textWrappingMode = TextWrappingModes.Normal;
        TutorialFontProvider.ApplyTo(text);
        if (labelColor == ScrollPanelArt.InkColor)
            ScrollPanelArt.Inkify(text);
        else
            text.color = labelColor;
        ScrollPanelArt.ApplyButtonSkin(button, labelColor == ScrollPanelArt.InkColor, fill);
        return button;
    }
}
