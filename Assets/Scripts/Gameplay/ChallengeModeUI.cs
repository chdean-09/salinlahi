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
        _progressText.text = $"Challenge {session.CurrentUnitIndex + 1} | Errors {session.Errors} | Hearts {session.HeartsRemaining}";
        _promptText.text = BuildPrompt(unit, session);
        _timerText.text = BuildTimerText(unit, session);
        _statusText.text = BuildStatusText(session);
        RebuildChoices(unit, session);
        SetActionInteractivity(session);
    }

    public void ShowFeedback(string message)
    {
        BuildIfNeeded();
        _statusText.text = message ?? string.Empty;
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

        RectTransform panel = gameObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.06f, 0.04f);
        panel.anchorMax = new Vector2(0.94f, 0.40f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;

        Image panelImage = GetComponent<Image>();
        if (panelImage == null)
            panelImage = gameObject.AddComponent<Image>();
        panelImage.color = ScrollPanelArt.FlatPanelColor;
        panelImage.raycastTarget = false;
        bool onParchment = ScrollPanelArt.ApplyFull(panelImage);

        _progressText = CreateLabel("Progress", UITextScale.Secondary, new Vector2(0.17f, 0.66f), new Vector2(0.79f, 0.80f));
        _progressText.textWrappingMode = TextWrappingModes.NoWrap;
        _timerText = CreateLabel("Timer", UITextScale.Caption, new Vector2(0.72f, 0.66f), new Vector2(0.86f, 0.80f));
        _timerText.alignment = TextAlignmentOptions.Right;
        _timerText.textWrappingMode = TextWrappingModes.NoWrap;
        _promptText = CreateLabel("Prompt", UITextScale.Body, new Vector2(0.20f, 0.42f), new Vector2(0.80f, 0.64f));
        _statusText = CreateLabel("Status", UITextScale.Caption, new Vector2(0.20f, 0.30f), new Vector2(0.80f, 0.41f));

        GameObject choices = new GameObject("AnswerChoices", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        choices.transform.SetParent(transform, false);
        _choicesRoot = choices.GetComponent<RectTransform>();
        _choicesRoot.anchorMin = new Vector2(0.18f, 0.17f);
        _choicesRoot.anchorMax = new Vector2(0.82f, 0.29f);
        _choicesRoot.offsetMin = _choicesRoot.offsetMax = Vector2.zero;
        HorizontalLayoutGroup choicesLayout = choices.GetComponent<HorizontalLayoutGroup>();
        choicesLayout.spacing = 12f;
        choicesLayout.padding = new RectOffset(8, 8, 4, 4);
        choicesLayout.childAlignment = TextAnchor.MiddleCenter;
        choicesLayout.childForceExpandWidth = false;
        choicesLayout.childForceExpandHeight = true;

        GameObject actions = new GameObject("ChallengeActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        actions.transform.SetParent(transform, false);
        _actionsRoot = actions.GetComponent<RectTransform>();
        _actionsRoot.anchorMin = new Vector2(0.18f, 0.025f);
        _actionsRoot.anchorMax = new Vector2(0.82f, 0.15f);
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
        }
    }

    private void ClearChoiceButtons()
    {
        foreach (Button button in _choiceButtons.Values)
        {
            if (button != null)
                Destroy(button.gameObject);
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

    private static string BuildPrompt(ChallengeUnitDefinition unit, ChallengeSession session)
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
            return $"{unit.prompt}\nRemember: {string.Join("  ", memoryTokens)}";
        }
        return unit.prompt;
    }

    private static string BuildTimerText(ChallengeUnitDefinition unit, ChallengeSession session)
    {
        if (unit == null)
            return string.Empty;
        if (session.IsMemoryRevealActive)
            return $"Remember {session.MemoryRevealRemaining:0.0}";
        return unit.timerSeconds > 0f ? $"Time {session.RemainingTime:0.0}" : string.Empty;
    }

    private string BuildStatusText(ChallengeSession session)
    {
        string hint = BuildHintText(session);
        string feedback = session.LastEvent switch
        {
            ChallengeSessionEvent.SupportiveRetry => "Try again. Correct progress is safe.",
            ChallengeSessionEvent.RetryOpened => "Try again with the current clues.",
            ChallengeSessionEvent.HintShown => "Hint shown.",
            ChallengeSessionEvent.HintApplied => "Hint shown. The next clue is available.",
            ChallengeSessionEvent.TimedOut => "Time expired.",
            ChallengeSessionEvent.PenaltyApplied => "Heart spent. Returning to checkpoint.",
            ChallengeSessionEvent.CheckpointReset => "Checkpoint restored with full clues.",
            ChallengeSessionEvent.CheckpointReopened => "Checkpoint restored. Try again.",
            ChallengeSessionEvent.MemoryRevealStarted => "Remember the sequence.",
            ChallengeSessionEvent.MemoryRecallStarted => "Recall phase started.",
            ChallengeSessionEvent.UnitSucceeded => "Unit complete.",
            ChallengeSessionEvent.Completed => "Challenge complete.",
            ChallengeSessionEvent.Exited => "Challenge exited.",
            ChallengeSessionEvent.Failed => "Challenge failed.",
            _ => string.Empty
        };
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
    /// the focus word's MEANING, which explains the word without placing anything, so the
    /// player still has to assemble it.
    ///
    /// The line persists after the modal closes rather than living only inside the card:
    /// a hint is information, not decaying scaffolding (D-014), so its visual form stays.
    /// </summary>
    private string BuildHintText(ChallengeSession session)
    {
        if (string.IsNullOrEmpty(session.HintOccurrenceId) || session.CurrentUnitDefinition == null)
            return string.Empty;

        FocusWordDefinition focus = _controller == null
            ? null
            : _controller.ResolveFocusWord(session.CurrentUnitDefinition);

        return focus != null && !string.IsNullOrEmpty(focus.meaning)
            ? HintModalCopy.HintStatusLine(focus.displayLabel, focus.meaning)
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
        text.fontSizeMin = Mathf.Max(UITextScale.AutoSizeFloor, size * 0.55f);
        text.fontSizeMax = size;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
        return text;
    }

    private Button CreateActionButton(string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(label, _actionsRoot, action, SlateButtonFill, Color.white);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 190f;
        layout.preferredHeight = 60f;
        _actionButtons.Add(button);
        return button;
    }

    /// <summary>
    /// SALIN-231. Opens the cost-disclosure modal (AC-1). Reads the session; spends
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

        FocusWordDefinition focus = _controller.ResolveFocusWord(session.CurrentUnitDefinition);
        _hintModal.Open(
            session,
            focus == null ? string.Empty : focus.displayLabel,
            focus == null ? string.Empty : focus.meaning,
            () => _controller?.RequestHint(),
            () => _controller?.Retry());
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

    // The scroll-family button convention, sourced from ScrollPanelArt so the board,
    // the modals and the end screens cannot drift apart: gold carries the gameplay
    // actions, dark slate the utilities — the pair the ready screen ships.
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
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = UITextScale.Caption;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        // The exhausted "No Hints Left" label outgrows Caption on a fixed size: shrink
        // toward the floor instead of wrapping a clipped second line.
        text.enableAutoSizing = true;
        text.fontSizeMin = UITextScale.AutoSizeFloor;
        text.fontSizeMax = UITextScale.Caption;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        TutorialFontProvider.ApplyTo(text);
        if (labelColor == ScrollPanelArt.InkColor)
            ScrollPanelArt.Inkify(text);
        else
            text.color = labelColor;
        return button;
    }
}
