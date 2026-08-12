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
            transform.SetParent(canvas.transform, false);
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 250);

        RectTransform panel = gameObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.06f, 0.05f);
        panel.anchorMax = new Vector2(0.94f, 0.36f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;

        Image panelImage = GetComponent<Image>();
        if (panelImage == null)
            panelImage = gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.03f, 0.05f, 0.1f, 0.9f);
        panelImage.raycastTarget = false;

        _progressText = CreateLabel("Progress", 24, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.98f));
        _promptText = CreateLabel("Prompt", 30, new Vector2(0.04f, 0.55f), new Vector2(0.96f, 0.78f));
        _statusText = CreateLabel("Status", 20, new Vector2(0.04f, 0.38f), new Vector2(0.72f, 0.54f));
        _timerText = CreateLabel("Timer", 20, new Vector2(0.74f, 0.38f), new Vector2(0.96f, 0.54f));

        GameObject choices = new GameObject("AnswerChoices", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        choices.transform.SetParent(transform, false);
        _choicesRoot = choices.GetComponent<RectTransform>();
        _choicesRoot.anchorMin = new Vector2(0.04f, 0.17f);
        _choicesRoot.anchorMax = new Vector2(0.96f, 0.35f);
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
        _actionsRoot.anchorMin = new Vector2(0.04f, 0.02f);
        _actionsRoot.anchorMax = new Vector2(0.96f, 0.15f);
        _actionsRoot.offsetMin = _actionsRoot.offsetMax = Vector2.zero;
        HorizontalLayoutGroup actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = 12f;
        actionsLayout.childAlignment = TextAnchor.MiddleCenter;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = true;

        CreateActionButton("Hint", () => _controller?.RequestHint());
        CreateActionButton("Retry", () => _controller?.Retry());
        CreateActionButton("Exit", () => _controller?.Exit());
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
        return unit.timerSeconds > 0f ? $"Time {session.RemainingTime:0.0}" : "No timer";
    }

    private static string BuildStatusText(ChallengeSession session)
    {
        string progress = $"Clues: {session.CluePolicy}    Slots: {session.CurrentSlotIndex}/{session.RequiredSlotCount}";
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
        string status = string.IsNullOrEmpty(feedback) ? progress : $"{feedback}\n{progress}";
        return string.IsNullOrEmpty(hint) ? status : $"{status}\n{hint}";
    }

    private static string BuildHintText(ChallengeSession session)
    {
        if (string.IsNullOrEmpty(session.HintOccurrenceId) || session.CurrentUnitDefinition == null)
            return string.Empty;

        foreach (ChallengeTokenDefinition token in session.CurrentUnitDefinition.tokens ?? new ChallengeTokenDefinition[0])
        {
            if (token != null && token.occurrenceId == session.HintOccurrenceId)
                return $"Hint: {token.displayText}";
        }
        return $"Hint: choose slot {session.CurrentSlotIndex + 1}";
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
        text.fontSizeMin = Mathf.Max(12f, size * 0.55f);
        text.fontSizeMax = size;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
        return text;
    }

    private void CreateActionButton(string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(label, _actionsRoot, action);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 150f;
        layout.preferredHeight = 56f;
        _actionButtons.Add(button);
    }

    private void SetActionInteractivity(ChallengeSession session)
    {
        bool active = session != null && session.State == ChallengeSessionState.Active;
        foreach (Button button in _actionButtons)
        {
            if (button != null)
                button.interactable = active;
        }
    }

    private Button CreateChoiceButton(string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(label, _choicesRoot, action);
        LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 150f;
        layout.preferredHeight = 60f;
        return button;
    }

    private static Button CreateButton(string label, Transform parent, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.12f, 0.42f, 0.62f, 0.95f);
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
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
        return button;
    }
}
