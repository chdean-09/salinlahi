using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueController : MonoBehaviour
{
    // The story scroll fills the bottom 30% of the screen. This is not only a look: the rod
    // below is a fixed-pixel slice border, so a short panel is what pushes the copy onto it.
    private const float DialoguePanelHeight = 0.30f;

    // Fixed sizes rather than auto-fit: at half height every authored line fits, and the
    // speaker name is the title, so it outranks the body.
    private const float SpeakerFontSize = 72f;
    private const float BodyFontSize = 52f;

    private const float SlideUpSeconds = 0.35f;
    private const float SlideDownSeconds = 0.30f;
    private const float DialogueSidePadding = 0.17f;
    private const float DialoguePortraitRight = 0.17f;
    private const float DialogueTextWithPortraitMinX = 0.20f;


    // PanelBackground_Top is 9-sliced with a 155px top border -- the rod. A slice border is
    // a fixed pixel height: it does NOT shrink with the panel, so the shorter the panel, the
    // larger the share of it the rod swallows. That is what used to drop the speaker name on
    // top of the rod once the panel was clamped down to sit under the shrine.
    private const float ScrollRodPixels = 155f;
    private const float ReferenceScreenHeight = 1920f;

    /// <summary>The rod's share of the panel at the design height.</summary>
    public static float ScrollRodFraction =>
        ScrollRodPixels / (DialoguePanelHeight * ReferenceScreenHeight);

    /// <summary>
    /// The panel's vertical offset while it slides, from one full panel below the screen at
    /// <paramref name="normalizedTime"/> 0 to flush with the bottom edge at 1.
    /// </summary>
    public static float SlideOffsetY(float normalizedTime, float panelHeight)
    {
        float t = Mathf.Clamp01(normalizedTime);
        float inverse = 1f - t;
        float eased = 1f - (inverse * inverse * inverse);
        return -panelHeight * (1f - eased);
    }

    [Header("UI References")]
    [SerializeField] private GameObject _overlayPanel;
    [SerializeField] private TMP_Text _speakerText;
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _tapCatcher;

    [Header("Typewriter Settings")]
    [SerializeField] private float _charsPerSecond = 30f;

    // Render the dialogue above the tutorial spotlight dim (sortingOrder 100) and the
    // Level 1 guide canvas (110) so instruction text / buttons are never darkened by the
    // overlay. A nested Canvas with overrideSorting affects only this subtree, so the
    // rest of the HUD is still dimmed normally by the spotlight.
    private const int OverlaySortingOrder = 200;

    private DialogueSO _currentDialogue;
    private int _lineIndex;
    private bool _isTypewriting;
    private Coroutine _typewriterRoutine;
    private Coroutine _slideRoutine;
    private bool _onParchment;

    public static DialogueController CreateRuntime()
    {
        Canvas canvas = FindTutorialCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject controllerObject = new("RuntimeDialogueController", typeof(RectTransform));
        controllerObject.transform.SetParent(canvas.transform, false);
        RectTransform controllerRect = controllerObject.GetComponent<RectTransform>();
        Stretch(controllerRect);

        DialogueController controller = controllerObject.AddComponent<DialogueController>();
        controller._overlayPanel = CreateOverlay(controllerObject.transform);
        controller._speakerText = CreateText(controller._overlayPanel.transform, "SpeakerText", new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.90f), SpeakerFontSize, TextAlignmentOptions.Center);
        controller._bodyText = CreateText(controller._overlayPanel.transform, "BodyText", new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.70f), BodyFontSize, TextAlignmentOptions.Center);
        controller._portraitImage = CreatePortrait(controller._overlayPanel.transform);
        controller._tapCatcher = CreateTapCatcher(controllerObject.transform);
        controller._tapCatcher.onClick.AddListener(controller.OnTapCatcherPressed);
        // Not ApplyResponsiveDialogueLayout: that lays the copy out but leaves the panel on
        // its flat placeholder colour. Awake and OnEnable already ran during AddComponent
        // above, while _overlayPanel was still null, so this is the first chance to dress the
        // panel as parchment -- and it has to happen before the panel is ever on screen.
        controller.ConfigureResponsiveLayout(hasPortrait: false);

        controller._overlayPanel.SetActive(false);
        controller.SetTapCatcherActive(false);
        return controller;
    }

    private static Canvas FindTutorialCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == "TutorialCanvas")
                return canvas;
        }

        return null;
    }

    private static GameObject CreateOverlay(Transform parent)
    {
        GameObject panel = new("DialogueOverlay", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, DialoguePanelHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.04f, 0.035f, 0.03f, 0.92f);
        image.raycastTarget = false;
        return panel;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
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
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
        return text;
    }

    private static Image CreatePortrait(Transform parent)
    {
        GameObject portraitObject = new("PortraitImage", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(parent, false);
        RectTransform rect = portraitObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.03f, 0.16f);
        rect.anchorMax = new Vector2(0.13f, 0.86f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = portraitObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        portraitObject.SetActive(false);
        return image;
    }

    private static Button CreateTapCatcher(Transform parent)
    {
        GameObject buttonObject = new("DialogueTapCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Stretch(buttonObject.GetComponent<RectTransform>());

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        return buttonObject.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Awake()
    {
        ConfigureResponsiveLayout(hasPortrait: _portraitImage != null && _portraitImage.gameObject.activeSelf);

        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);

        // The full-screen tap-catcher is a SIBLING of _overlayPanel, so toggling the panel
        // never turns it off. Manage it explicitly here so it can't sit active (and block
        // drawing input via IsScreenPositionOverUI) while no dialogue is showing.
        SetTapCatcherActive(false);
    }

    private void SetTapCatcherActive(bool active)
    {
        if (_tapCatcher != null)
            _tapCatcher.gameObject.SetActive(active);
    }

    private void OnEnable()
    {
        ConfigureResponsiveLayout(hasPortrait: _portraitImage != null && _portraitImage.gameObject.activeSelf);
        NameLossEffectRegistry.Changed += HandleNameLossEffectChanged;

        if (_tapCatcher != null)
            _tapCatcher.onClick.AddListener(OnTapCatcherPressed);
    }

    private void OnDisable()
    {
        NameLossEffectRegistry.Changed -= HandleNameLossEffectChanged;
        if (_tapCatcher != null)
            _tapCatcher.onClick.RemoveListener(OnTapCatcherPressed);
    }

    public void Play(DialogueSO dialogue)
    {
        if (!gameObject.activeInHierarchy)
        {
            DialogueController runtime = CreateRuntime();
            runtime.Play(dialogue);
            return;
        }

        if (_currentDialogue != null) return;

        if (GameManager.Instance == null ||
            (GameManager.Instance.CurrentState != GameState.Playing
                && GameManager.Instance.CurrentState != GameState.LevelComplete)) return;

        if (dialogue == null || dialogue.lines == null || dialogue.lines.Length == 0) return;

        _currentDialogue = dialogue;
        _lineIndex = 0;

        if (_overlayPanel != null)
        {
            _overlayPanel.SetActive(true);
            EnsureOverlayOnTop();

            // Dress the scroll before it is shown. The frame used to be applied by the first
            // ShowLine, which now happens only after the panel has finished rising -- so the
            // panel climbed into view as a dark rectangle and turned to parchment on arrival.
            ConfigureResponsiveLayout(dialogue.lines[0].portrait != null);
        }

        GameManager.Instance.EnterDialoguePause();

        EventBus.RaiseDialogueStarted();

        if (CanAnimate)
        {
            _slideRoutine = StartCoroutine(RiseThenShowFirstLine());
            return;
        }

        SetTapCatcherActive(true);
        ShowLine(_currentDialogue.lines[0]);
    }

    /// <summary>
    /// The scroll rises into view before any copy appears, then the first line types on.
    /// Taps are swallowed until it has settled so a fast tapper cannot skip a line that is
    /// not on screen yet.
    /// </summary>
    private IEnumerator RiseThenShowFirstLine()
    {
        ClearLine();
        SetTapCatcherActive(false);

        yield return Slide(0f, 1f, SlideUpSeconds);

        _slideRoutine = null;
        SetTapCatcherActive(true);

        if (_currentDialogue != null)
            ShowLine(_currentDialogue.lines[_lineIndex]);
    }

    /// <summary>
    /// Drives the panel between parked-below-the-screen and seated, on UNSCALED time:
    /// EnterDialoguePause sets Time.timeScale to 0, so a scaled tween would never advance.
    /// </summary>
    private IEnumerator Slide(float from, float to, float duration)
    {
        RectTransform panel = ResolvePanelRect();
        if (panel == null || duration <= 0f)
            yield break;

        float height = ResolvePanelHeightPixels(panel);
        float elapsed = 0f;

        SetPanelOffset(panel, SlideOffsetY(from, height));

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetPanelOffset(panel, SlideOffsetY(Mathf.Lerp(from, to, t), height));
            yield return null;
        }

        SetPanelOffset(panel, SlideOffsetY(to, height));
    }

    private static void SetPanelOffset(RectTransform panel, float offsetY)
    {
        if (panel == null)
            return;

        Vector2 position = panel.anchoredPosition;
        position.y = offsetY;
        panel.anchoredPosition = position;
    }

    /// <summary>
    /// The panel's height in canvas units. The laid-out rect is preferred, but it reads zero
    /// until the canvas has run, so the canvas and then the reference resolution stand in.
    /// </summary>
    private static float ResolvePanelHeightPixels(RectTransform panel)
    {
        if (panel != null && panel.rect.height > 0f)
            return panel.rect.height;

        Canvas canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
        if (canvas != null && canvas.transform is RectTransform canvasRect
            && canvasRect.rect.height > 0f)
        {
            return canvasRect.rect.height * DialoguePanelHeight;
        }

        return ReferenceScreenHeight * DialoguePanelHeight;
    }

    /// <summary>
    /// Edit-mode tests and any headless driver have no player loop to advance a coroutine,
    /// and dialogue completion is what advances the level flow. Outside play mode the scroll
    /// therefore skips the animation and opens and closes synchronously, exactly as before.
    /// </summary>
    private bool CanAnimate =>
        Application.isPlaying && isActiveAndEnabled && gameObject.activeInHierarchy;

    private void ClearLine()
    {
        if (_speakerText != null)
            _speakerText.text = string.Empty;
        if (_bodyText != null)
        {
            _bodyText.text = string.Empty;
            UITextReveal.Complete(_bodyText);
        }
    }

    // Promote the dialogue overlay's subtree to its own Canvas layered above the spotlight
    // dim so the panel, text, and tap/Next button stay fully visible during enemy highlights.
    private void EnsureOverlayOnTop()
    {
        if (_overlayPanel == null) return;

        Canvas canvas = _overlayPanel.GetComponent<Canvas>();
        if (canvas == null)
            canvas = _overlayPanel.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        // A nested overrideSorting canvas needs its own raycaster for the tap-catcher/buttons.
        if (_overlayPanel.GetComponent<GraphicRaycaster>() == null)
            _overlayPanel.AddComponent<GraphicRaycaster>();
    }

    private void ShowLine(DialogueLine line)
    {
        if (_speakerText != null)
            _speakerText.text = NameLossEffectRegistry.IsActive ? string.Empty : line.speakerName ?? "";

        bool hasPortrait = false;
        if (_portraitImage != null)
        {
            if (line.portrait != null)
            {
                _portraitImage.sprite = line.portrait;
                _portraitImage.gameObject.SetActive(true);
                hasPortrait = true;
            }
            else
            {
                _portraitImage.gameObject.SetActive(false);
            }
        }

        ConfigureResponsiveLayout(hasPortrait);

        if (_typewriterRoutine != null)
            StopCoroutine(_typewriterRoutine);

        _typewriterRoutine = StartCoroutine(TypewriterRoutine(line.text ?? ""));
    }

    private void HandleNameLossEffectChanged()
    {
        if (_speakerText == null || _currentDialogue == null
            || _currentDialogue.lines == null
            || _lineIndex < 0 || _lineIndex >= _currentDialogue.lines.Length)
            return;

        DialogueLine line = _currentDialogue.lines[_lineIndex];
        _speakerText.text = NameLossEffectRegistry.IsActive ? string.Empty : line.speakerName ?? "";
    }

    private void ConfigureResponsiveLayout(bool hasPortrait)
    {
        RectTransform panel = ResolvePanelRect();
        _onParchment = ApplyDialogueFrame(panel);
        ApplyResponsiveDialogueLayout(panel, _speakerText, _bodyText, _portraitImage, hasPortrait);
        if (_onParchment)
        {
            ScrollPanelArt.Inkify(_speakerText);
            ScrollPanelArt.Inkify(_bodyText);
        }
    }

    // The dialogue panel is a parchment scroll-top: rod along the top edge, parchment
    // body below. Works for both the runtime overlay (Image on the root) and the
    // serialized scene variant (Image on a child panel). The portrait is skipped.
    private bool ApplyDialogueFrame(RectTransform panel)
    {
        if (panel == null)
            return false;

        Image background = panel.GetComponent<Image>();
        if (background == null)
        {
            foreach (Image image in panel.GetComponentsInChildren<Image>(true))
            {
                if (_portraitImage != null && image == _portraitImage)
                    continue;
                background = image;
                break;
            }
        }

        return ScrollPanelArt.ApplyTop(background);
    }


    private RectTransform ResolvePanelRect()
    {
        if (_overlayPanel != null && _overlayPanel.TryGetComponent(out RectTransform overlayRect))
            return overlayRect;

        if (_bodyText != null && _bodyText.rectTransform.parent is RectTransform bodyParent)
            return bodyParent;

        if (_speakerText != null && _speakerText.rectTransform.parent is RectTransform speakerParent)
            return speakerParent;

        return null;
    }

    public static void ApplyResponsiveDialogueLayout(
        RectTransform panel,
        TMP_Text speakerText,
        TMP_Text bodyText,
        Image portraitImage,
        bool hasPortrait)
    {
        if (panel != null)
        {
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, DialoguePanelHeight);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.pivot = new Vector2(0.5f, 0f);
        }

        float textMinX = hasPortrait ? DialogueTextWithPortraitMinX : DialogueSidePadding;
        float textMaxX = ScrollPanelArt.TopSafeArea.xMax;
        ConfigureDialogueText(
            speakerText,
            new Vector2(textMinX, 0.60f),
            new Vector2(textMaxX, 0.70f),
            SpeakerFontSize,
            TextAlignmentOptions.Center);

        // Hung from just under the title and growing downward. Centering the body in a
        // half-screen panel leaves it adrift mid-paper with a gap below the title.
        ConfigureDialogueText(
            bodyText,
            new Vector2(textMinX, 0.16f),
            new Vector2(textMaxX, 0.58f),
            BodyFontSize,
            TextAlignmentOptions.Top);

        // At 30% panel height the fixed sizes overflow their bands (speaker ~58px
        // band vs ~86px line, body ~4 lines vs 6 needed). Auto-fit so long lines
        // shrink instead of clipping off the parchment.
        if (speakerText != null)
        {
            speakerText.enableAutoSizing = true;
            speakerText.fontSizeMin = UITextScale.Body;
            speakerText.fontSizeMax = SpeakerFontSize;
        }
        if (bodyText != null)
        {
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = UITextScale.AutoSizeFloor;
            bodyText.fontSizeMax = BodyFontSize;
        }

        if (portraitImage != null)
        {
            RectTransform portraitRect = portraitImage.rectTransform;
            portraitRect.anchorMin = new Vector2(0.035f, 0.16f);
            portraitRect.anchorMax = new Vector2(DialoguePortraitRight, 0.88f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
        }
    }

    private static void ConfigureDialogueText(
        TMP_Text text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        _isTypewriting = true;

        if (_bodyText == null)
        {
            _isTypewriting = false;
            yield break;
        }

        // Full text assigned up front so the reveal never reflows the layout.
        _bodyText.text = fullText;
        yield return UITextReveal.Play(_bodyText, _charsPerSecond);

        _isTypewriting = false;
    }

    private void OnTapCatcherPressed()
    {
        if (_currentDialogue == null) return;

        // The scroll is still moving; there is nothing on it to read or skip yet.
        if (_slideRoutine != null) return;

        if (_isTypewriting)
        {
            SkipTypewriter();
            return;
        }

        _lineIndex++;

        if (_lineIndex < _currentDialogue.lines.Length)
        {
            ShowLine(_currentDialogue.lines[_lineIndex]);
        }
        else
        {
            EndDialogue();
        }
    }

    private void SkipTypewriter()
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        _isTypewriting = false;

        if (_bodyText != null && _currentDialogue != null &&
            _lineIndex < _currentDialogue.lines.Length)
        {
            _bodyText.text = _currentDialogue.lines[_lineIndex].text ?? "";
            UITextReveal.Complete(_bodyText);
        }
    }

    private void EndDialogue()
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        _isTypewriting = false;
        _currentDialogue = null;
        _lineIndex = 0;
        SetTapCatcherActive(false);

        // The scroll withdraws before the flow is told the beat is over: DialogueComplete
        // advances the level, which would tear this panel down mid-animation otherwise.
        if (CanAnimate)
            _slideRoutine = StartCoroutine(SinkThenClose());
        else
            CloseImmediately();
    }

    private IEnumerator SinkThenClose()
    {
        ClearLine();
        yield return Slide(1f, 0f, SlideDownSeconds);

        _slideRoutine = null;
        CloseImmediately();
    }

    private void CloseImmediately()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ExitDialoguePause();

        EventBus.RaiseDialogueComplete();
    }
}
