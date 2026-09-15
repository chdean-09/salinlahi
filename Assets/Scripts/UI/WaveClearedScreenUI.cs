using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-232. The Wave Cleared screen. Presented when the Defense phase reports
/// completion, and held there — nothing advances to the ContextChallenge phase until
/// the player taps the continue button (AC-5, AC-7).
///
/// The screen is never wired in a scene: <see cref="LevelFlowController"/> creates one
/// on demand and it builds its own canvas and card, exactly as
/// <see cref="LevelContentMissingPanel"/> (SALIN-223) does. That is deliberate — a
/// [SerializeField] reference would have to be authored into every scene that hosts a
/// LevelFlowController, and Assets/_Scenes/*.unity are the project's two
/// highest-collision serialized assets with an unconfigured merge driver
/// (.gitattributes:11). Serialized-asset conflict surface for this ticket: zero.
///
/// CONTRACT — <see cref="Present"/> returning false means PROCEED IMMEDIATELY, never
/// hold. This is the OPPOSITE remedy from LevelContentMissingPanel, whose false means
/// "leave the level" (LevelContentMissingPanel.cs:16-18). The difference matters: a
/// celebration surface that could not be built must never strand a level the player has
/// legitimately cleared. The caller reports defense completion straight through instead.
///
/// NO ACCURACY READOUT. See the WaveClearedCopy banner: D-021 (LOCKED) cut the displayed
/// accuracy statistic and no combat-accuracy metric exists. Do not add one from the
/// stale ticket text.
/// </summary>
public sealed class WaveClearedScreenUI : MonoBehaviour
{
    // Four separate objects, never aliased onto one: the SALIN-272 defect was three
    // serialized fields pointing at a single GameObject, which no functional test could
    // see. WaveClearedScreenTests pins that they stay distinct.
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _bannerText;
    [SerializeField] private TMP_Text _heartsText;
    [SerializeField] private Button _continueButton;

    private Action _continueAction;
    private bool _continueListenerBound;
    private bool _isPresented;

    public bool HasRequiredReferences =>
        _overlayRoot != null && _bannerText != null && _heartsText != null && _continueButton != null;

    /// <summary>True while the screen is on screen. Read by tests and by the flow.</summary>
    public bool IsPresented => _isPresented;

    private void Awake()
    {
        BindListeners();
        if (!_isPresented)
            Hide();
    }

    /// <summary>
    /// Shows the screen with the hearts readout for this clear. Returns false when no
    /// surface could be presented (an EditMode host, or a stripped scene); the caller
    /// must then advance immediately rather than waiting — see the class contract.
    /// </summary>
    public bool Present(int heartsRemaining, int maxHearts, Action continueAction)
    {
        if (Application.isPlaying && !HasRequiredReferences)
            BuildFallbackUi();

        if (!HasRequiredReferences)
        {
            Hide();
            return false;
        }

        BindListeners();
        _continueAction = continueAction;
        _isPresented = true;
        Render(heartsRemaining, maxHearts);
        _overlayRoot.SetActive(true);
        _continueButton.interactable = true;
        return true;
    }

    public void Hide()
    {
        _isPresented = false;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    /// <summary>
    /// The continue tap, exposed for the PlayMode fixtures so they drive the same seam
    /// the button drives rather than poking the flow machine directly. A test that
    /// reported defense completion itself would pass even if the gate had stopped
    /// holding, which is the whole behaviour under test.
    /// </summary>
    public void Continue()
    {
        HandleContinue();
    }

    private void BindListeners()
    {
        if (_continueButton != null && !_continueListenerBound)
        {
            _continueButton.onClick.AddListener(HandleContinue);
            _continueListenerBound = true;
        }
    }

    private void HandleContinue()
    {
        _continueAction?.Invoke();
    }

    // SALIN-232 copy. Every string comes from WaveClearedCopy; nothing is composed here.
    private void Render(int heartsRemaining, int maxHearts)
    {
        _bannerText.text = WaveClearedCopy.BannerLabel;
        _heartsText.text = WaveClearedCopy.Hearts(heartsRemaining, maxHearts);
        DebugLogger.Log(
            $"WaveClearedScreenUI: presented with hearts {heartsRemaining}/{maxHearts}.");
    }

    private void BuildFallbackUi()
    {
        if (HasRequiredReferences)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] WaveClearedCanvas",
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

        GameObject card = new GameObject("BannerCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(820f, 460f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color32(45, 32, 25, 255);
        cardImage.raycastTarget = true;
        bool onParchment = ScrollPanelArt.ApplyFull(cardImage);

        _bannerText = CreateText(card.transform, "BannerText", string.Empty, 45f, 120f, 56f);
        _heartsText = CreateText(card.transform, "HeartsText", string.Empty, 185f, 90f, 36f);
        _continueButton = CreateButton(
            card.transform, "ContinueButton", WaveClearedCopy.ContinueLabel, 25f);

        if (onParchment)
        {
            ScrollPanelArt.InkifyRecursive(card.transform);
            ScrollPanelArt.Inkify(_continueButton.GetComponentInChildren<TMP_Text>(true));
        }
    }

    // Visual constants deliberately mirror LevelContentMissingPanel so the runtime-built
    // overlays read as one family.
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
