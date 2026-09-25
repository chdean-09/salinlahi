using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-231. The confirm-before-you-spend gate in front of ChallengeSession.RequestHint.
///
/// WHAT THIS REPLACES: the Hint button called RequestHint() directly
/// (ChallengeModeUI.cs:102 before this ticket), which spent the hint with no cost shown,
/// no confirmation and no cancel, and — once the tier-5 budget was gone — silently did
/// nothing at all (ChallengeSession.cs:225-227, a bare `return`).
///
/// ⚠️ NOT SCENE-WIRED, AND DELIBERATELY SO. Nothing in this stack is: ChallengeModeUI and
/// ChallengeFlowController appear in no .unity file and are built at runtime
/// (ChallengeFlowController.EnsureRuntimeReferences, ChallengeModeUI.BuildIfNeeded). This
/// panel follows the same pattern as LevelContentMissingPanel — it builds its own canvas
/// and card and carries no [SerializeField]. A SerializedObject scene-wiring guard would
/// therefore assert nothing here and report a false green; the correct guard is
/// construct-and-inspect (see HintModalTests).
///
/// ⚠️ TEARDOWN. Close() hides rather than destroys. ChallengeModeUI.ClearChoiceButtons uses
/// Destroy, which is DEFERRED in EditMode — a modal that destroyed on close would leave
/// objects alive through an EditMode test while the test still passed. Hiding behaves
/// identically in both modes.
/// </summary>
public sealed class HintModal : MonoBehaviour
{
    /// <summary>What the card is currently showing.</summary>
    public enum Mode
    {
        Closed,
        /// <summary>Cost disclosed, confirm and cancel offered (AC-1, AC-2).</summary>
        Confirm,
        /// <summary>Budget spent: explanation and Retry, no confirm (AC-4).</summary>
        Exhausted,
        /// <summary>The hint the player paid for (AC-5: one clue, never the answer).</summary>
        Revealed,
    }

    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _bodyText;
    private TextMeshProUGUI _costText;
    private Button _confirmButton;
    private Button _cancelButton;
    private TextMeshProUGUI _confirmLabel;
    private TextMeshProUGUI _cancelLabel;
    private RectTransform _card;

    private Action _onConfirm;
    private Action _onRetry;
    private string _pendingLabel;
    private string _pendingMeaning;

    public Mode CurrentMode { get; private set; } = Mode.Closed;
    public bool IsOpen => CurrentMode != Mode.Closed;

    // Inspection surface for the construct-and-inspect guard. Read-only by design:
    // tests drive the modal through Confirm()/Cancel(), the same entry points the
    // buttons use, so a broken button wiring cannot pass by being bypassed.
    public string TitleText => _titleText == null ? string.Empty : _titleText.text;
    public string BodyText => _bodyText == null ? string.Empty : _bodyText.text;
    public string CostText => _costText == null ? string.Empty : _costText.text;
    public string ConfirmLabelText => _confirmLabel == null ? string.Empty : _confirmLabel.text;
    public string CancelLabelText => _cancelLabel == null ? string.Empty : _cancelLabel.text;
    public bool ConfirmIsInteractable => _confirmButton != null && _confirmButton.interactable;
    public bool ConfirmIsVisible => _confirmButton != null && _confirmButton.gameObject.activeSelf;
    public bool CancelIsInteractable => _cancelButton != null && _cancelButton.interactable;

    /// <summary>
    /// Builds a modal under <paramref name="parent"/>, or under its own overlay canvas when
    /// none is given. Mirrors ChallengeFlowController's "[Runtime] ChallengeModeUI" naming.
    /// </summary>
    public static HintModal CreateRuntime(Transform parent = null)
    {
        var go = new GameObject("[Runtime] HintModal", typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go.AddComponent<HintModal>();
    }

    /// <summary>
    /// Opens the pre-confirm card (AC-1): the option, its cost stated BEFORE use, and the
    /// remaining budget. Reads the session; spends nothing. When the budget is already gone
    /// this opens the exhausted card instead of the confirm card (AC-4).
    /// </summary>
    /// <param name="session">Source of the effective policy. Never re-read a level asset's
    /// serialized emergencyHintEnabled — see ChallengeSession's SALIN-231 block.</param>
    /// <param name="displayLabel">Focus word label, e.g. "IBA". May be empty.</param>
    /// <param name="meaning">FocusWordDefinition.meaning, e.g. "different". Empty when the
    /// unit has no focus word, which disables confirm rather than charging for nothing.</param>
    /// <param name="onConfirm">Invoked ONLY from Confirm(). Cancel never touches it.</param>
    /// <param name="onRetry">Invoked from the exhausted card's Retry control.</param>
    public void Open(
        ChallengeSession session,
        string displayLabel,
        string meaning,
        Action onConfirm,
        Action onRetry = null)
    {
        if (session == null)
            return;

        BuildIfNeeded();
        _onConfirm = onConfirm;
        _onRetry = onRetry;
        _pendingLabel = displayLabel;
        _pendingMeaning = meaning;

        if (session.IsHintExhausted)
        {
            ShowExhausted();
            return;
        }

        CurrentMode = Mode.Confirm;
        gameObject.SetActive(true);
        _titleText.text = HintModalCopy.Title;

        bool hasMeaning = !string.IsNullOrEmpty(meaning);
        _bodyText.text = hasMeaning
            ? HintModalCopy.MeaningOptionLabel
            : HintModalCopy.NoHintAvailableBody;

        // Cost BEFORE use. On tiers 1-4 ForTier leaves the budget disabled, so the honest
        // line is "no cost" rather than a hidden modal.
        _costText.text = session.HintBudgetIsLimited
            ? HintModalCopy.CostLine(HintModalCopy.ScorePointsFromFraction(session.EmergencyHintCostFraction))
              + "  " + HintModalCopy.RemainingLine(session.EmergencyHintsRemaining)
            : HintModalCopy.FreeLine;

        _confirmButton.gameObject.SetActive(true);
        _confirmLabel.text = HintModalCopy.ConfirmLabel;
        // The guard and the disclosure are one expression by construction.
        _confirmButton.interactable = session.CanRequestHint && hasMeaning;
        _cancelLabel.text = HintModalCopy.CancelLabel;
    }

    /// <summary>
    /// AC-2. Closes without consuming a hint, degrading a clue, recording a penalty or
    /// touching the session in any way. The confirm callback is dropped unfired.
    /// </summary>
    public void Cancel()
    {
        _onConfirm = null;
        _onRetry = null;
        Close();
    }

    /// <summary>
    /// AC-1 / AC-5. Spends exactly one hint through the supplied callback, then shows the
    /// single clue it bought. The meaning explains the word; it never fills a slot, so it
    /// cannot finish a required word on the player's behalf.
    /// </summary>
    public void Confirm()
    {
        if (CurrentMode != Mode.Confirm || !ConfirmIsInteractable)
            return;

        Action confirm = _onConfirm;
        _onConfirm = null;
        confirm?.Invoke();

        CurrentMode = Mode.Revealed;
        _titleText.text = HintModalCopy.MeaningOptionLabel;
        _bodyText.text = HintModalCopy.MeaningReveal(_pendingLabel, _pendingMeaning);
        _costText.text = string.Empty;
        _confirmButton.gameObject.SetActive(false);
        _cancelLabel.text = HintModalCopy.CloseLabel;
    }

    /// <summary>Exhausted card's Retry control — stays inside the encounter, per D-004.</summary>
    public void Retry()
    {
        Action retry = _onRetry;
        _onRetry = null;
        Close();
        retry?.Invoke();
    }

    /// <summary>Hides the card. Never destroys — see the teardown note on the class.</summary>
    public void Close()
    {
        CurrentMode = Mode.Closed;
        if (this != null && gameObject != null)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// AC-4. The label the Hint control shows: "No Hints Left" once the budget is spent,
    /// "Hint" while one is available. A null session keeps the default.
    /// </summary>
    public static string HintControlLabel(ChallengeSession session) =>
        session != null && session.IsHintExhausted
            ? HintModalCopy.ExhaustedButtonLabel
            : HintModalCopy.AvailableButtonLabel;

    private void ShowExhausted()
    {
        CurrentMode = Mode.Exhausted;
        gameObject.SetActive(true);
        _titleText.text = HintModalCopy.ExhaustedButtonLabel;
        _bodyText.text = HintModalCopy.ExhaustedBody;
        _costText.text = string.Empty;
        // No confirm control at all: there is nothing left to buy, and an inert-but-present
        // button is how the old silent no-op read to the player.
        _confirmButton.gameObject.SetActive(true);
        _confirmButton.interactable = true;
        _confirmLabel.text = HintModalCopy.RetryLabel;
        _cancelLabel.text = HintModalCopy.CloseLabel;
    }

    private void BuildIfNeeded()
    {
        if (_titleText != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            var canvasObject = new GameObject(
                "HintModalCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            transform.SetParent(canvas.transform, false);
        }
        // Above ChallengeModeUI's own 250 so the card is never rendered behind the HUD.
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 300);

        RectTransform root = gameObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        // Shared modal chrome: the navy dim every scroll modal sits on. It doubles as the
        // tap-outside dismiss — a click that lands off the card is a Cancel in every mode
        // (drops the unfired confirm, closes the card, spends nothing), which is exactly
        // what the Cancel control does, so both paths run the same public method.
        GameObject overlay = ScrollPanelArt.CreateDimOverlay(transform, "DimOverlay");
        Button dismissArea = overlay.AddComponent<Button>();
        dismissArea.transition = Selectable.Transition.None;
        dismissArea.targetGraphic = overlay.GetComponent<Image>();
        dismissArea.onClick.AddListener(Cancel);

        // The card is a SIBLING created after the overlay, so it renders above the dim and
        // — just as important — is not its descendant: a click on the card does not walk
        // up to the dismiss control.
        _card = ScrollPanelArt.CreateScrollPanel(transform, "Card");
        bool onParchment = ScrollPanelArt.ApplyFull(_card.GetComponent<Image>());

        // Everything inside FullSafeArea — the paper, not the rods.
        _titleText = CreateLabel("Title", UITextScale.Title, new Vector2(0.16f, 0.66f), new Vector2(0.84f, 0.80f));
        _bodyText = CreateLabel("Body", UITextScale.Body, new Vector2(0.16f, 0.40f), new Vector2(0.84f, 0.64f));
        _costText = CreateLabel("Cost", UITextScale.Caption, new Vector2(0.16f, 0.30f), new Vector2(0.84f, 0.40f));

        var actions = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        actions.transform.SetParent(_card, false);
        RectTransform actionsRect = actions.GetComponent<RectTransform>();
        actionsRect.anchorMin = new Vector2(0.16f, 0.17f);
        actionsRect.anchorMax = new Vector2(0.84f, 0.30f);
        actionsRect.offsetMin = actionsRect.offsetMax = Vector2.zero;
        HorizontalLayoutGroup layout = actions.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        _confirmButton = CreateButton(
            HintModalCopy.ConfirmLabel, actions.transform, out _confirmLabel, HandleConfirmPressed,
            GoldButton, ScrollPanelArt.InkColor);
        _cancelButton = CreateButton(
            HintModalCopy.CancelLabel, actions.transform, out _cancelLabel, Cancel,
            SlateButton, Color.white);

        if (onParchment)
            ScrollPanelArt.InkifyRecursive(_card);
    }

    // The scroll-family button convention: gold is the forward action, dark slate the
    // retreating one — the same pair LevelReadyScreenController ships.
    private static readonly Color GoldButton = new Color(0.85f, 0.72f, 0.35f, 1f);
    private static readonly Color SlateButton = new Color(0.18f, 0.24f, 0.34f, 1f);

    // One control serves confirm and retry: the exhausted card has nothing to confirm, so
    // the same button carries the only forward action that state offers.
    private void HandleConfirmPressed()
    {
        if (CurrentMode == Mode.Exhausted)
            Retry();
        else
            Confirm();
    }

    private TextMeshProUGUI CreateLabel(string name, float size, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(_card, false);
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

    private static Button CreateButton(
        string label,
        Transform parent,
        out TextMeshProUGUI labelText,
        UnityEngine.Events.UnityAction action,
        Color fill,
        Color labelColor)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = fill;
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(action);

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 250f;
        layout.preferredHeight = 64f;

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(go.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        labelText = textObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = UITextScale.Body;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = labelColor;
        labelText.raycastTarget = false;
        // "Use This Hint" is wider than the button at Body size: shrink into the floor
        // rather than wrap a second line that would clip the 64px button.
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = UITextScale.AutoSizeFloor;
        labelText.fontSizeMax = UITextScale.Body;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        TutorialFontProvider.ApplyTo(labelText);
        if (labelColor == ScrollPanelArt.InkColor)
            ScrollPanelArt.Inkify(labelText);
        return button;
    }
}
