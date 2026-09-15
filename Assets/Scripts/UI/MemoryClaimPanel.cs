using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-240. The Claim Memory control (spec BTN-CLAIM), shown over the Results screen when
/// the finished level granted a memory. Pressing it opens that memory's card.
///
/// WHY THIS IS ITS OWN OVERLAY AND NOT A BUTTON ON THE RESULTS PANEL. The spec places
/// "Claim Memory" among the Results buttons (UF-28), which would mean editing
/// VictoryScreenUI.cs. SALIN-258 owns that file for its "never show a global 1-15" change,
/// and SALIN-234 rewrote 236 lines of it days ago. Shipping the control as a separate
/// overlay keeps VictoryScreenUI.cs closed, so the two tickets do not collide in a file that
/// is already being rewritten. The deviation is visual, not behavioural: the control appears
/// at the same moment, over the same screen, and does the same thing.
///
/// THE MEMORY IS ALREADY SAVED BY THE TIME THIS SHOWS. unlockedMemoryIds is committed in
/// ExecuteAtomicSave, before the Results screen appears. So this control is a reveal, not a
/// transaction: it cannot fail, it cannot be missed (the memory stays in the archive either
/// way), and dismissing it loses nothing. It is deliberately not gated on a save result.
///
/// Built at runtime on its own canvas, like LevelContentMissingPanel. Zero scene edits.
/// </summary>
public sealed class MemoryClaimPanel : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _claimButton;
    [SerializeField] private Button _dismissButton;

    private MemoryArchiveEntry _entry;
    private int _eraTotal;
    private Action _dismissAction;
    private bool _listenersBound;
    private MemoryCardUI _cardUI;

    public bool HasRequiredReferences =>
        _overlayRoot != null && _bodyText != null && _claimButton != null && _dismissButton != null;

    public bool IsPresented { get; private set; }

    private void Awake()
    {
        BindListeners();
        if (!IsPresented)
            Hide();
    }

    /// <summary>
    /// Shows the claim control for <paramref name="entry"/>.
    ///
    /// Returns false when there is nothing to claim -- a null entry, or one whose content is
    /// not authored. The caller must treat false as "show nothing" and carry on; it must
    /// never wait on it.
    /// </summary>
    public bool Present(MemoryArchiveEntry entry, int eraTotal, Action dismissAction)
    {
        if (entry == null || !entry.HasAuthoredContent)
        {
            Hide();
            return false;
        }

        BuildClaimUi();
        if (!HasRequiredReferences)
        {
            Hide();
            return false;
        }

        BindListeners();
        _entry = entry;
        _eraTotal = eraTotal;
        _dismissAction = dismissAction;
        IsPresented = true;

        // No score, no stars, no accuracy figure: D-021 cut the displayed accuracy statistic
        // and owner ruling R1 confirmed the display went while the scoring stayed. The
        // Results screen behind this overlay owns the numbers; this owns the memory.
        _bodyText.text = MemoryCardCopy.ClaimPromptBody;

        _overlayRoot.SetActive(true);
        _claimButton.interactable = true;
        _dismissButton.interactable = true;
        return true;
    }

    public void Hide()
    {
        IsPresented = false;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    /// <summary>AC-1: claiming shows the card.</summary>
    public void Claim()
    {
        if (_entry == null)
            return;

        if (_cardUI == null)
        {
            GameObject cardObject = new GameObject("[Runtime] MemoryCardUI");
            _cardUI = cardObject.AddComponent<MemoryCardUI>();
        }

        Hide();
        _cardUI.Present(_entry, _eraTotal, _dismissAction);
    }

    private void BindListeners()
    {
        if (_listenersBound || _claimButton == null || _dismissButton == null)
            return;

        _claimButton.onClick.AddListener(Claim);
        _dismissButton.onClick.AddListener(HandleDismiss);
        _listenersBound = true;
    }

    private void HandleDismiss()
    {
        Hide();
        _dismissAction?.Invoke();
    }

    private void BuildClaimUi()
    {
        if (HasRequiredReferences)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] MemoryClaimCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transform.SetParent(canvas.transform, false);
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 305);

        _overlayRoot = gameObject;

        // GetComponent returns a stub, not a plain null; only the overloaded == null works.
        Image overlayImage = GetComponent<Image>();
        if (overlayImage == null)
            overlayImage = gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 150f / 255f);
        overlayImage.raycastTarget = true;

        RectTransform overlayRect = gameObject.GetComponent<RectTransform>();
        if (overlayRect == null)
            overlayRect = gameObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        GameObject card = new GameObject("ClaimCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(760f, 340f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color32(45, 32, 25, 255);
        cardImage.raycastTarget = true;
        bool onParchment = ScrollPanelArt.ApplyFull(cardImage);

        _bodyText = CreateText(card.transform, "BodyText", string.Empty, 60f, 120f, UITextScale.Body);
        _claimButton = CreateButton(card.transform, "ClaimButton", MemoryCardCopy.ClaimLabel, -190f, 30f);
        _dismissButton = CreateButton(card.transform, "DismissButton", MemoryCardCopy.CloseLabel, 190f, 30f);

        ApplyParchmentLayout(cardRect, _bodyText, _claimButton, _dismissButton);

        if (onParchment)
            ScrollPanelArt.InkifyRecursive(card.transform);
    }

    /// <summary>
    /// Seats the message and both buttons inside the scroll's paper. The buttons stack
    /// instead of sitting side by side: the safe area is about two thirds of the card's
    /// width, which is not enough for two labelled buttons in a row without clipping.
    /// </summary>
    public static void ApplyParchmentLayout(
        RectTransform card,
        TMP_Text body,
        Button claim,
        Button dismiss)
    {
        if (card == null)
            return;

        card.sizeDelta = new Vector2(700f, 620f);

        ScrollPanelArt.PlaceText(body, Rect.MinMaxRect(0.17f, 0.54f, 0.83f, 0.77f), UITextScale.Caption, 44f);
        ScrollPanelArt.PlaceButton(claim, Rect.MinMaxRect(0.21f, 0.36f, 0.79f, 0.49f));
        ScrollPanelArt.PlaceButton(dismiss, Rect.MinMaxRect(0.21f, 0.19f, 0.79f, 0.32f));
    }

    private static TMP_Text CreateText(
        Transform parent, string name, string text, float top, float height, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        TutorialFontProvider.ApplyLegibilityEffects(label);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(40f, -top - height);
        rect.offsetMax = new Vector2(-40f, -top);
        return label;
    }

    private static Button CreateButton(
        Transform parent, string name, string labelText, float x, float y)
    {
        GameObject buttonObject = new GameObject(
            name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(340f, 92f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(209, 168, 82, 255);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText(buttonObject.transform, "Label", labelText, 0f, 92f, UITextScale.Body);
        RectTransform labelRect = ((Component)label).GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.color = Color.black;
        return button;
    }
}
