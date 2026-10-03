using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// SALIN-256 (spec BTN-EXIT). Confirmation modal for leaving the game, and the only place in
/// the project that calls Application.Quit.
///
/// Runtime-builds its own UI when references are not wired, following
/// ResetJourneyConfirmationPanel.cs:6-10 and LevelContentMissingPanel.cs:11-14, so no edit to
/// MainMenu.unity is required. That scene is 5231 lines under a merge=unityyamlmerge
/// attribute whose driver is not configured in this repo, so every UI ticket this sprint built
/// its surface at runtime instead.
///
/// WHY THE QUIT SITS BEHIND A SEAM — READ BEFORE "SIMPLIFYING" IT.
/// Application.Quit() is a NO-OP IN THE EDITOR. It does nothing in EditMode, nothing in
/// PlayMode, and in a real player it would take the test runner down with it. So no automated
/// test in this project can ever prove that pressing Exit quits. QuitAction exists so a test
/// can at least prove the CONFIRM PATH REACHES THE QUIT CALL; the quit itself is verifiable
/// only by installing an Android build and pressing the button. Inlining Application.Quit()
/// here would delete the only coverage AC-3 can have.
/// </summary>
public sealed class ExitConfirmationPanel : MonoBehaviour
{
    /// <summary>
    /// The quit seam. Defaults to the real Application.Quit; a test swaps it, asserts the
    /// confirm path fired, and restores it. See the class header for why this is not inlined.
    /// </summary>
    internal static Action QuitAction = Application.Quit;

    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _cancelLabel;

    private Action _onConfirm;
    private Action _onCancel;
    private bool _listenersAttached;
    private bool _quitRequested;
    private GameObject _previousSelection;
    private EventTrigger.Entry _confirmCancelEntry;
    private EventTrigger.Entry _cancelCancelEntry;

    public bool HasRequiredReferences => _overlayRoot != null && _titleText != null &&
        _bodyText != null && _confirmButton != null && _cancelButton != null;

    public bool IsShowing => _overlayRoot != null && _overlayRoot.activeSelf;

    private void Awake()
    {
        AttachListeners();
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        DetachListeners();
    }

    private void Update()
    {
        if (IsShowing && !_quitRequested && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            HandleCancelPressed();
    }

    /// <summary>
    /// Builds the modal if needed and shows it. Both callbacks are optional: the panel quits
    /// and hides on its own, and the callbacks exist so the caller can observe the choice.
    /// </summary>
    /// <returns>False when the surface could not be built, so the caller can log rather than
    /// silently doing nothing — the same contract MemoryArchiveController.Present uses at
    /// MainMenuUI.cs:193-194.</returns>
    public bool Present(Action onConfirm = null, Action onCancel = null)
    {
        if (IsShowing)
            return true;

        EnsureBuilt();
        if (!HasRequiredReferences)
            return false;

        AttachListeners();
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _quitRequested = false;

        _titleText.text = MainMenuProgressCopy.ExitConfirmTitle;
        _bodyText.text = MainMenuProgressCopy.ExitConfirmBody;
        _bodyText.alignment = TextAlignmentOptions.Center;
        SetLabel(_confirmLabel, MainMenuProgressCopy.ExitConfirmButtonLabel);
        SetLabel(_cancelLabel, MainMenuProgressCopy.ExitCancelButtonLabel);

        _confirmButton.interactable = true;
        _cancelButton.interactable = true;
        _previousSelection = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;
        _overlayRoot.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(_cancelButton.gameObject);
        return true;
    }

    public void Hide()
    {
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
        if (_previousSelection != null && _previousSelection.activeInHierarchy)
            EventSystem.current?.SetSelectedGameObject(_previousSelection);
        _previousSelection = null;
    }

    private void HandleConfirmPressed()
    {
        if (!IsShowing || _quitRequested)
            return;
        _quitRequested = true;
        _confirmButton.interactable = false;
        _cancelButton.interactable = false;
        AudioManager.Instance?.PlayMenuExitButtonClick();
        _onConfirm?.Invoke();
        Hide();
        // Invoked through the seam, never as a direct Application.Quit call. See the header.
        QuitAction?.Invoke();
    }

    private void HandleCancelPressed()
    {
        if (!IsShowing || _quitRequested)
            return;
        AudioManager.Instance?.PlayMenuExitButtonClick();
        Hide();
        _onCancel?.Invoke();
    }

    private static void SetLabel(TMP_Text label, string text)
    {
        if (label != null)
            label.text = text;
    }

    private void AttachListeners()
    {
        if (_listenersAttached)
            return;
        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(HandleConfirmPressed);
            _confirmCancelEntry = AttachCancelHandler(_confirmButton);
        }
        if (_cancelButton != null)
        {
            _cancelButton.onClick.AddListener(HandleCancelPressed);
            _cancelCancelEntry = AttachCancelHandler(_cancelButton);
        }
        _listenersAttached = _confirmButton != null && _cancelButton != null;
    }

    private EventTrigger.Entry AttachCancelHandler(Button button)
    {
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.Cancel };
        entry.callback.AddListener(eventData =>
        {
            HandleCancelPressed();
            eventData.Use();
        });
        trigger.triggers.Add(entry);
        return entry;
    }

    private void DetachListeners()
    {
        if (!_listenersAttached)
            return;
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(HandleConfirmPressed);
            _confirmButton.GetComponent<EventTrigger>()?.triggers.Remove(_confirmCancelEntry);
        }
        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(HandleCancelPressed);
            _cancelButton.GetComponent<EventTrigger>()?.triggers.Remove(_cancelCancelEntry);
        }
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
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (_overlayRoot == null)
        {
            _overlayRoot = new GameObject("Overlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _overlayRoot.transform.SetParent(transform, false);
            Stretch(_overlayRoot.GetComponent<RectTransform>());
            Image backdrop = _overlayRoot.GetComponent<Image>();
            backdrop.color = ScrollPanelArt.DimOverlayColor;
            backdrop.raycastTarget = true;
        }

        GameObject card = new GameObject("Card",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(_overlayRoot.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.08f, 0.32f);
        cardRect.anchorMax = new Vector2(0.92f, 0.68f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = ScrollPanelArt.FlatPanelColor;
        cardImage.raycastTarget = false;
        bool onParchment = ScrollPanelArt.ApplyFull(cardImage);

        if (_titleText == null)
            _titleText = BuildText("Title", cardRect,
                new Vector2(0.16f, 0.65f), new Vector2(0.84f, 0.80f),
                UITextScale.Title, TextAlignmentOptions.Center);
        if (_bodyText == null)
            _bodyText = BuildText("Body", cardRect,
                new Vector2(0.16f, 0.40f), new Vector2(0.84f, 0.62f),
                UITextScale.Body, TextAlignmentOptions.Center);
        if (_cancelButton == null)
            _cancelButton = BuildButton("CancelButton", cardRect,
                new Vector2(0.16f, 0.16f), new Vector2(0.48f, 0.34f),
                ScrollPanelArt.SlateButtonFill, out _cancelLabel);
        if (_confirmButton == null)
            _confirmButton = BuildButton("ConfirmButton", cardRect,
                new Vector2(0.52f, 0.16f), new Vector2(0.84f, 0.34f),
                ScrollPanelArt.GoldButtonFill, out _confirmLabel);

        Navigation cancelNavigation = new Navigation { mode = Navigation.Mode.Explicit };
        cancelNavigation.selectOnRight = _confirmButton;
        _cancelButton.navigation = cancelNavigation;
        Navigation confirmNavigation = new Navigation { mode = Navigation.Mode.Explicit };
        confirmNavigation.selectOnLeft = _cancelButton;
        _confirmButton.navigation = confirmNavigation;

        if (onParchment)
            ScrollPanelArt.InkifyRecursive(card.transform);
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
        TutorialFontProvider.ApplyTo(text);
        ScrollPanelArt.PlaceText(text, Rect.MinMaxRect(
            anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y), fontSize, fontSize);
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
        labelText.fontSize = UITextScale.Body;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        label = labelText;

        Button button = buttonObject.GetComponent<Button>();
        ScrollPanelArt.ApplyButtonSkin(button, primary: name == "ConfirmButton", fallbackFill: color);
        ScrollPanelArt.SizeButtonLabel(button);
        return button;
    }
}
