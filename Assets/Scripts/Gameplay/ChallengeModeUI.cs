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
    private Transform _choicesRoot;

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
        _promptText.text = unit == null ? "" : unit.prompt;
        _timerText.text = unit != null && unit.timerSeconds > 0f ? $"Time {session.RemainingTime:0.0}" : "No timer";
        _statusText.text = $"Clues: {session.CluePolicy}    Slots: {session.CurrentSlotIndex}/{session.RequiredSlotCount}";
        RebuildChoices(unit, session);
    }

    public void ShowFeedback(string message)
    {
        BuildIfNeeded();
        _statusText.text = message;
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
            canvas.sortingOrder = 250;
            transform.SetParent(canvas.transform, false);
        }
        RectTransform panel = gameObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.12f, 0.68f);
        panel.anchorMax = new Vector2(0.88f, 0.96f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;

        _progressText = CreateLabel("Progress", 22, new Vector2(0.02f, 0.72f), new Vector2(0.98f, 0.98f));
        _promptText = CreateLabel("Prompt", 30, new Vector2(0.02f, 0.42f), new Vector2(0.98f, 0.72f));
        _statusText = CreateLabel("Status", 20, new Vector2(0.02f, 0.12f), new Vector2(0.7f, 0.42f));
        _timerText = CreateLabel("Timer", 20, new Vector2(0.72f, 0.12f), new Vector2(0.98f, 0.42f));

        GameObject choices = new GameObject("AnswerChoices", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        choices.transform.SetParent(transform, false);
        RectTransform choicesRect = choices.GetComponent<RectTransform>();
        choicesRect.anchorMin = new Vector2(0.02f, -0.8f);
        choicesRect.anchorMax = new Vector2(0.98f, 0.1f);
        choicesRect.offsetMin = choicesRect.offsetMax = Vector2.zero;
        _choicesRoot = choices.transform;

        CreateButton("Hint", () => _controller?.RequestHint());
        CreateButton("Retry", () => _controller?.Retry());
        CreateButton("Exit", () => _controller?.Exit());
    }

    private void RebuildChoices(ChallengeUnitDefinition unit, ChallengeSession session)
    {
        if (_choicesRoot == null)
            return;
        for (int i = _choicesRoot.childCount - 1; i >= 0; i--)
            Destroy(_choicesRoot.GetChild(i).gameObject);
        if (unit == null || (unit.mode != ChallengeMode.WordPlacement && unit.mode != ChallengeMode.SentenceRestoration && unit.mode != ChallengeMode.ParagraphRestoration))
            return;

        foreach (string occurrenceId in unit.candidateOccurrenceIds ?? new string[0])
        {
            string captured = occurrenceId;
            ChallengeTokenDefinition token = FindToken(unit, captured);
            string label = token == null ? captured : token.displayText;
            CreateChoiceButton(label, () => _controller?.SubmitPlacement(captured));
        }
    }

    private ChallengeTokenDefinition FindToken(ChallengeUnitDefinition unit, string occurrenceId)
    {
        foreach (ChallengeTokenDefinition token in unit.tokens ?? new ChallengeTokenDefinition[0])
            if (token != null && token.occurrenceId == occurrenceId)
                return token;
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
        return text;
    }

    private void CreateButton(string label, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Button), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.02f + transform.childCount * 0.08f, -1.25f);
        rect.anchorMax = new Vector2(0.12f + transform.childCount * 0.08f, -0.82f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Button>().onClick.AddListener(action);
        TextMeshProUGUI text = CreateLabel(label + "Label", 18, Vector2.zero, Vector2.one);
        text.transform.SetParent(go.transform, false);
        text.text = label;
    }

    private void CreateChoiceButton(string label, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Button), typeof(Image));
        go.transform.SetParent(_choicesRoot, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 60f);
        go.GetComponent<Button>().onClick.AddListener(action);
        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(go.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }
}
