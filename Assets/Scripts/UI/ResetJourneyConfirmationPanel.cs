using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Destructive-action confirmation modal for the intentional Reset Journey flow
/// (SALIN-142). Runtime-builds its own UI when references are not wired, following
/// the SettingsPanel self-building pattern, so no scene edits are required.
/// </summary>
public sealed class ResetJourneyConfirmationPanel : MonoBehaviour
{
    private enum PanelState
    {
        Confirming,
        Succeeded,
        Failed,
    }

    private static readonly Color BackdropColor = new(0.02f, 0.03f, 0.06f, 0.94f);
    private static readonly Color CardColor = new(0.07f, 0.1f, 0.17f, 1f);
    private static readonly Color DestructiveButtonColor = new(0.72f, 0.18f, 0.15f, 1f);
    private static readonly Color NeutralButtonColor = new(0.2f, 0.23f, 0.3f, 1f);

    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _cancelLabel;

    private Func<ResetJourneyOutcome> _execute;
    private Action _onContinueAfterSuccess;
    private PanelState _state;
    private bool _listenersAttached;

    public bool HasRequiredReferences => _overlayRoot != null && _titleText != null &&
        _bodyText != null && _confirmButton != null && _cancelButton != null;

    private void Awake()
    {
        AttachListeners();
        if (_execute == null && _overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        DetachListeners();
    }

    public void Present(Func<ResetJourneyOutcome> execute, Action onContinueAfterSuccess)
    {
        if (execute == null)
            return;
        EnsureBuilt();
        if (!HasRequiredReferences)
            return;
        AttachListeners();
        _execute = execute;
        _onContinueAfterSuccess = onContinueAfterSuccess;
        ApplyState(PanelState.Confirming);
        _overlayRoot.SetActive(true);
    }

    public void Hide()
    {
        _execute = null;
        _onContinueAfterSuccess = null;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void HandleConfirmPressed()
    {
        if (_state == PanelState.Succeeded)
        {
            _onContinueAfterSuccess?.Invoke();
            return;
        }
        if (_execute == null)
            return;
        SetButtonsInteractable(false);
        ResetJourneyOutcome outcome = _execute();
        SetButtonsInteractable(true);
        ApplyState(outcome == ResetJourneyOutcome.Succeeded
            ? PanelState.Succeeded
            : PanelState.Failed);
    }

    private void HandleCancelPressed()
    {
        AudioManager.Instance?.PlayMenuExitButtonClick();
        Hide();
    }

    private void ApplyState(PanelState state)
    {
        _state = state;
        if (state == PanelState.Confirming)
        {
            _titleText.text = ResetJourneyFlow.ConfirmTitle;
            _bodyText.text = ResetJourneyFlow.ConfirmBody;
            SetLabel(_confirmLabel, ResetJourneyFlow.ConfirmButtonLabel);
            SetLabel(_cancelLabel, ResetJourneyFlow.CancelButtonLabel);
            _cancelButton.gameObject.SetActive(true);
        }
        else if (state == PanelState.Succeeded)
        {
            _titleText.text = ResetJourneyFlow.SuccessTitle;
            _bodyText.text = ResetJourneyFlow.SuccessBody;
            SetLabel(_confirmLabel, ResetJourneyFlow.ContinueButtonLabel);
            _cancelButton.gameObject.SetActive(false);
        }
        else
        {
            _titleText.text = ResetJourneyFlow.FailureTitle;
            _bodyText.text = ResetJourneyFlow.FailureBody;
            SetLabel(_confirmLabel, ResetJourneyFlow.RetryButtonLabel);
            SetLabel(_cancelLabel, ResetJourneyFlow.CloseButtonLabel);
            _cancelButton.gameObject.SetActive(true);
        }
    }

    private static void SetLabel(TMP_Text label, string text)
    {
        if (label != null)
            label.text = text;
    }

    private void SetButtonsInteractable(bool isInteractable)
    {
        if (_confirmButton != null) _confirmButton.interactable = isInteractable;
        if (_cancelButton != null) _cancelButton.interactable = isInteractable;
    }

    private void AttachListeners()
    {
        if (_listenersAttached)
            return;
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(HandleConfirmPressed);
        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(HandleCancelPressed);
        _listenersAttached = _confirmButton != null && _cancelButton != null;
    }

    private void DetachListeners()
    {
        if (!_listenersAttached)
            return;
        if (_confirmButton != null)
            _confirmButton.onClick.RemoveListener(HandleConfirmPressed);
        if (_cancelButton != null)
            _cancelButton.onClick.RemoveListener(HandleCancelPressed);
        _listenersAttached = false;
    }

    private void EnsureBuilt()
    {
        if (HasRequiredReferences)
            return;

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = gameObject.AddComponent<RectTransform>();
        Stretch(rootRect);

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 300;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (_overlayRoot == null)
        {
            _overlayRoot = new GameObject("Overlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _overlayRoot.transform.SetParent(transform, false);
            Stretch(_overlayRoot.GetComponent<RectTransform>());
            Image backdrop = _overlayRoot.GetComponent<Image>();
            backdrop.color = BackdropColor;
            backdrop.raycastTarget = true;
        }

        GameObject card = new GameObject("Card",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(_overlayRoot.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.08f, 0.3f);
        cardRect.anchorMax = new Vector2(0.92f, 0.74f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = CardColor;
        cardImage.raycastTarget = false;

        if (_titleText == null)
            _titleText = BuildText("Title", cardRect,
                new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.95f),
                40f, TextAlignmentOptions.Center);
        if (_bodyText == null)
            _bodyText = BuildText("Body", cardRect,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.74f),
                30f, TextAlignmentOptions.TopLeft);
        if (_cancelButton == null)
            _cancelButton = BuildButton("CancelButton", cardRect,
                new Vector2(0.08f, 0.06f), new Vector2(0.48f, 0.22f),
                NeutralButtonColor, out _cancelLabel);
        if (_confirmButton == null)
            _confirmButton = BuildButton("ConfirmButton", cardRect,
                new Vector2(0.52f, 0.06f), new Vector2(0.92f, 0.22f),
                DestructiveButtonColor, out _confirmLabel);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text BuildText(
        string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button BuildButton(
        string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Color color, out TMP_Text label)
    {
        GameObject buttonObject = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        GameObject labelObject = new GameObject("Label",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Stretch(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.fontSize = 32f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        label = labelText;

        return buttonObject.GetComponent<Button>();
    }
}
