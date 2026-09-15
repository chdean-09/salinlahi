using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-223. Presented when the level flow reaches a planned phase whose authored
/// content does not exist. The phase refuses to complete rather than falling through,
/// so the level cannot be finished and the next level cannot unlock.
///
/// The panel is never wired in a scene: <see cref="LevelFlowController"/> creates one
/// on demand, and it builds its own canvas and card the way ChallengeModeUI does. That
/// is deliberate — a [SerializeField] reference would have to be authored into every
/// scene that hosts a LevelFlowController, which this ticket does not touch.
///
/// <see cref="Present"/> returns false when no surface could be built (an EditMode host,
/// or a stripped scene). The caller MUST treat false as "leave the level" and never hold:
/// a wait that nothing can satisfy hangs the runner instead of failing it.
/// </summary>
public sealed class LevelContentMissingPanel : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _mainMenuButton;

    private Action _mainMenuAction;
    private bool _mainMenuListenerBound;
    private bool _isPresented;

    public bool HasRequiredReferences =>
        _overlayRoot != null && _titleText != null && _bodyText != null && _mainMenuButton != null;

    /// <summary>True while the panel is on screen. Read by tests and by the flow.</summary>
    public bool IsPresented => _isPresented;

    /// <summary>The phase whose content was missing, valid while <see cref="IsPresented"/>.</summary>
    public LevelPhase PresentedPhase { get; private set; }

    private void Awake()
    {
        BindListeners();
        if (!_isPresented)
            Hide();
    }

    /// <summary>
    /// Shows the panel for <paramref name="phase"/>. Returns false when no surface could
    /// be presented; the caller must then exit the level instead of waiting.
    /// </summary>
    public bool Present(LevelPhase phase, Action mainMenuAction)
    {
        if (Application.isPlaying && !HasRequiredReferences)
            BuildFallbackUi();

        if (!HasRequiredReferences)
        {
            Hide();
            return false;
        }

        BindListeners();
        _mainMenuAction = mainMenuAction;
        _isPresented = true;
        PresentedPhase = phase;
        Render(phase);
        _overlayRoot.SetActive(true);
        _mainMenuButton.interactable = true;
        return true;
    }

    public void Hide()
    {
        _isPresented = false;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void BindListeners()
    {
        if (_mainMenuButton != null && !_mainMenuListenerBound)
        {
            _mainMenuButton.onClick.AddListener(HandleMainMenu);
            _mainMenuListenerBound = true;
        }
    }

    private void HandleMainMenu()
    {
        _mainMenuAction?.Invoke();
    }

    // SALIN-223 copy. English, matching the UI-chrome half of the language split (narrative
    // content is Filipino; see the dialogue assets). Register follows
    // CampaignOutcomeSaveFailurePanel.Render.
    //
    // It deliberately does NOT blame the player and does NOT say progress was lost: nothing was
    // lost, the level simply never reached its save step. Saying "not saved" without that
    // reassurance reads as data loss.
    private void Render(LevelPhase phase)
    {
        _titleText.text = "This level is not ready yet";
        _bodyText.text =
            "Salinlahi is still missing some of the content this level needs, so it cannot be "
            + "finished yet. Nothing you have already unlocked has been affected. Return to the "
            + "Main Menu and try another level.";
        DebugLogger.Log($"LevelContentMissingPanel: presented for {phase}.");
    }

    private void BuildFallbackUi()
    {
        if (HasRequiredReferences)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] ContentMissingCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transform.SetParent(canvas.transform, false);
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 300);

        _overlayRoot = gameObject;

        // Unity's GetComponent hands back a "missing component" stub rather than a plain
        // null reference, so `??` does not fire and the next member access throws
        // MissingComponentException. Only the overloaded == null comparison is safe here.
        Image overlayImage = GetComponent<Image>();
        if (overlayImage == null)
            overlayImage = gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 190f / 255f);
        overlayImage.raycastTarget = true;

        RectTransform overlayRect = gameObject.GetComponent<RectTransform>();
        if (overlayRect != null)
        {
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        }

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        GameObject card = new GameObject("MessageCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(820f, 560f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color32(45, 32, 25, 255);
        cardImage.raycastTarget = true;
        bool onParchment = ScrollPanelArt.ApplyFull(cardImage);

        _titleText = CreateText(card.transform, "TitleText", string.Empty, 45f, 120f, 48f);
        _bodyText = CreateText(card.transform, "BodyText", string.Empty, 185f, 260f, 32f);
        _mainMenuButton = CreateButton(card.transform, "MainMenuButton", "Main Menu", 25f);

        if (onParchment)
            ScrollPanelArt.InkifyRecursive(card.transform);
    }

    // Visual constants deliberately mirror CampaignOutcomeSaveFailurePanel so the two
    // failure surfaces read as one family.
    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float top,
        float height,
        float fontSize)
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

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(50f, -top - height);
        rect.offsetMax = new Vector2(-50f, -top);
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, float y)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(520f, 110f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(209, 168, 82, 255);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText(buttonObject.transform, "Label", labelText, 0f, 110f, 32f);
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
