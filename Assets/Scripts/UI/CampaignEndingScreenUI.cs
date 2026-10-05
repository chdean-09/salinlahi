using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Offline campaign credits. Opened explicitly from the final era's Results action;
/// never saves progress, reloads gameplay, or requests contributor data at runtime.
/// </summary>
public sealed class CampaignEndingScreenUI : MonoBehaviour
{
    // Snapshot: github.com/chdean-09/salinlahi/contributors, 2026-10-03.
    // Full names verified against GitHub profiles and repository commit authors.
    // One credit per person, alphabetical; Ian's display name is owner-approved.
    private const string Credits =
        "<b>SALINLAHI</b>\n\n" +
        "<size=75%>Ginawa nina</size>\n\n" +
        "Chad Andrada\n\n" +
        "Ian Clyde\n\n" +
        "Jeff Andre Millan\n\n" +
        "Jon Wayne Cabusbusan\n\n\n" +
        "<size=85%><b>Salamat sa paglalaro!</b></size>\n\n" +
        "<size=75%>Balikan ang kahit anong antas at\nmagpatuloy sa pag-aaral ng Baybayin.</size>";

    private const float ScrollUnitsPerSecond = 24f;
    private const float EntranceDuration = 0.25f;
    private const string CreditsMusicPath = "Audio/Credits/credits-ambient";
    private RectTransform _creditsViewport;
    private RectTransform _creditsContent;
    private RectTransform _continuationContent;
    private float _cycleHeight;
    private float _scrollOffset;
    private Button _returnButton;
    private TMP_Text _status;
    private bool _returning;
    private CanvasGroup _screenGroup;
    private float _entranceElapsed;

    public void Present()
    {
        if (_creditsContent == null)
            BuildScreen();

        gameObject.SetActive(true);
        _returning = false;
        _returnButton.interactable = true;
        _entranceElapsed = Application.isPlaying ? 0f : EntranceDuration;
        _screenGroup.alpha = Application.isPlaying ? 0f : 1f;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_creditsContent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_continuationContent);
        _cycleHeight = Mathf.Max(_creditsContent.rect.height + 80f, _creditsViewport.rect.height);
        _scrollOffset = 0f;
        PositionCredits();
        _status.text = string.Empty;
        EventSystem.current?.SetSelectedGameObject(_returnButton.gameObject);
        if (Application.isPlaying)
            AudioManager.Instance?.PlayCreditsBgm(Resources.Load<AudioClip>(CreditsMusicPath));
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            AudioManager.Instance?.StopCreditsBgm();
    }

    private void Update()
    {
        AdvanceEntrance(Time.unscaledDeltaTime);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ReturnToMainMenu();
            return;
        }

        AdvanceCredits(Time.unscaledDeltaTime);
    }

    private void AdvanceEntrance(float seconds)
    {
        if (_screenGroup == null || _entranceElapsed >= EntranceDuration)
            return;
        _entranceElapsed = Mathf.Min(EntranceDuration, _entranceElapsed + Mathf.Max(0f, seconds));
        float remaining = 1f - _entranceElapsed / EntranceDuration;
        _screenGroup.alpha = 1f - remaining * remaining * remaining * remaining;
    }

    /// <summary>Unscaled time keeps the credits readable when gameplay has stopped.</summary>
    public void AdvanceCredits(float seconds)
    {
        if (_creditsContent == null || _cycleHeight <= 0f || !gameObject.activeInHierarchy)
            return;

        float elapsed = Mathf.Clamp(seconds, 0f, 0.1f);
        _scrollOffset = Mathf.Repeat(_scrollOffset + ScrollUnitsPerSecond * elapsed, _cycleHeight);
        PositionCredits();
    }

    private void PositionCredits()
    {
        _creditsContent.anchoredPosition = new Vector2(0f, _scrollOffset);
        _continuationContent.anchoredPosition = new Vector2(0f, _scrollOffset - _cycleHeight);
    }

    public void ReturnToMainMenu()
    {
        if (_returning)
            return;
        if (SceneLoader.Instance == null)
        {
            _status.text = "Hindi mabuksan ang Pangunahing Menu. Pakisubukan muli.";
            DebugLogger.LogError("CampaignEndingScreenUI: SceneLoader not available.");
            return;
        }

        _returning = true;
        _status.text = "Binubuksan ang Pangunahing Menu...";
        _returnButton.interactable = false;
        AudioManager.Instance?.PlayMenuButtonClick();
        AudioManager.Instance?.StopCreditsBgm();
        EraCompletionScreenUI.PendingEraIndex = EraCompletionScreenUI.NoPendingEra;
        SceneLoader.Instance.LoadMainMenu();
    }

    private void BuildScreen()
    {
        _screenGroup = gameObject.AddComponent<CanvasGroup>();
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 330; // Above Results (era summary 315, memory card 320).
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject overlay = ScrollPanelArt.CreateDimOverlay(transform, "EndingBackdrop");
        overlay.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.045f, 1f);
        RectTransform safeArea = NewRect(overlay.transform, "SafeArea");
        ScrollPanelArt.SetAnchors(safeArea, Rect.MinMaxRect(0f, 0f, 1f, 1f));
        safeArea.gameObject.AddComponent<SafeAreaHandler>();
        RectTransform paper = ScrollPanelArt.CreateScrollPanel(safeArea, "EndingScroll");
        // Same compact parchment bounds as the Settings screen.
        ScrollPanelArt.SetAnchors(paper, Rect.MinMaxRect(0.04f, 0.17f, 0.96f, 0.83f));
        // Readable parchment fallback even if artwork cannot load.
        paper.GetComponent<Image>().color = new Color32(239, 219, 182, 255);
        ScrollPanelArt.ApplyFull(paper.GetComponent<Image>());

        TMP_Text heading = NewText(paper, "Heading", "Tapos na ang Paglalakbay", UITextScale.Display);
        heading.fontStyle = FontStyles.Bold;
        ScrollPanelArt.PlaceText(heading, Rect.MinMaxRect(0.16f, 0.74f, 0.84f, 0.82f), UITextScale.Title, UITextScale.Display);
        TMP_Text summary = NewText(paper, "Summary", "Ugat · Ugnayan · Pamana\nNatapos ang lahat ng 15 antas", UITextScale.Body);
        ScrollPanelArt.SetAnchors(summary.rectTransform, Rect.MinMaxRect(0.16f, 0.66f, 0.84f, 0.74f));

        RectTransform viewport = NewRect(paper, "CreditsViewport");
        ScrollPanelArt.SetAnchors(viewport, Rect.MinMaxRect(0.17f, 0.30f, 0.83f, 0.64f));
        Image hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();
        _creditsViewport = viewport;
        _creditsContent = NewCreditsText(viewport, "Credits");
        // The second copy enters as the first leaves, making the slow roll continuous.
        _continuationContent = NewCreditsText(viewport, "CreditsContinuation");

        _status = NewText(paper, "Status", string.Empty, UITextScale.Secondary);
        ScrollPanelArt.SetAnchors(_status.rectTransform, Rect.MinMaxRect(0.17f, 0.25f, 0.83f, 0.30f));
        _returnButton = NewButton(paper, "ReturnButton", "Menu",
            Rect.MinMaxRect(0.17f, 0.16f, 0.83f, 0.25f), true);
        _returnButton.onClick.AddListener(ReturnToMainMenu);
    }

    private static RectTransform NewCreditsText(Transform parent, string name)
    {
        TMP_Text credits = NewText(parent, name, Credits, UITextScale.Title);
        RectTransform content = credits.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        credits.alignment = TextAlignmentOptions.Top;
        credits.margin = new Vector4(8f, 24f, 8f, 40f);
        credits.lineSpacing = 12f;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static TMP_Text NewText(Transform parent, string name, string value, float size)
    {
        RectTransform rect = NewRect(parent, name);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TutorialFontProvider.ApplyTo(text);
        ScrollPanelArt.Inkify(text);
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button NewButton(Transform parent, string name, string label, Rect area, bool primary)
    {
        RectTransform rect = NewRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        // This screen has one action; keyboard navigation must not reach the covered Victory buttons.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.targetGraphic = image;
        NewText(rect, "Label", label, UITextScale.Body);
        ScrollPanelArt.PlaceButton(button, area, primary);
        return button;
    }
}
