using TMPro;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loads the parchment scroll sprites from Resources and applies them to the
/// runtime-built panels, plus the shared dark "ink on parchment" text color.
/// Null-safe: when the sprites fail to load (edit-mode tests, batch runs) the
/// panel keeps its existing flat color instead of going blank.
/// </summary>
public static class ScrollPanelArt
{
    private const string ResourcePath = "Art/UI/Almanac/PanelBackground";
    private const string FullSpriteName = "PanelBackground_0";
    private const string TopSpriteName = "PanelBackground_Top";

    public static readonly Color InkColor = new Color32(58, 38, 22, 255);
    public static readonly Rect FullSafeArea = Rect.MinMaxRect(0.16f, 0.16f, 0.84f, 0.80f);
    public static readonly Rect TopSafeArea = Rect.MinMaxRect(0.16f, 0.12f, 0.84f, 0.71f);

    /// <summary>The scroll's normalized rect inside its overlay — shared by every
    /// modal parchment surface so the ready screen, focus-word preview and symbol
    /// cards all present the same scroll in the same place.</summary>
    public static readonly Rect ScrollArea = Rect.MinMaxRect(0.04f, 0.14f, 0.96f, 0.86f);

    /// <summary>Full-screen dim behind the scroll (matches the ready screen). 0.93 keeps
    /// a hint of scene presence while making whatever text sits underneath illegible —
    /// at 0.88 a live cutscene's caption and "Tap anywhere" prompt stayed readable
    /// around the scroll's edges and competed with the modal's own call to action.</summary>
    public static readonly Color DimOverlayColor = new Color(0.015f, 0.02f, 0.045f, 0.93f);

    /// <summary>Flat panel fallback used until/unless the parchment sprite applies.</summary>
    public static readonly Color FlatPanelColor = new Color(0.025f, 0.035f, 0.08f, 0.98f);

    /// <summary>Fallback fills when the parchment artwork cannot load.</summary>
    public static readonly Color GoldButtonFill = new Color(0.85f, 0.72f, 0.35f, 1f);
    public static readonly Color SlateButtonFill = new Color(0.18f, 0.24f, 0.34f, 1f);

    private static Sprite[] _sprites;
    private static bool _loaded;
    private static Sprite _buttonSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReloadInit()
    {
        _sprites = null;
        _loaded = false;
        if (_buttonSprite != null)
            Object.Destroy(_buttonSprite);
        _buttonSprite = null;
    }

    /// <summary>The full scroll — rod at top and bottom — for tall panels.</summary>
    public static Sprite Full => Find(FullSpriteName);

    /// <summary>Top of the scroll — rod at the top edge only — for wide banners.</summary>
    public static Sprite Top => Find(TopSpriteName);

    /// <summary>
    /// Applies the full-scroll sprite as a sliced panel background.
    /// Returns true when the sprite was applied, so callers can switch text to ink.
    /// </summary>
    public static bool ApplyFull(Image image)
    {
        return Apply(image, Full);
    }

    /// <summary>
    /// Applies the scroll-top sprite as a sliced panel background.
    /// Returns true when the sprite was applied, so callers can switch text to ink.
    /// </summary>
    public static bool ApplyTop(Image image)
    {
        return Apply(image, Top);
    }

    /// <summary>Switches a text component (TMP or legacy uGUI Text) to ink.</summary>
    public static void Inkify(Graphic text)
    {
        if (text == null)
            return;

        text.color = InkColor;
        TutorialFontProvider.ClearLegibilityEffects(text);
    }

    /// <summary>
    /// Inks every TMP or legacy uGUI text under the panel that does not live
    /// inside a Button, so colored action buttons keep their contrasting labels.
    /// </summary>
    public static void InkifyRecursive(Transform root)
    {
        if (root == null)
            return;

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if ((graphic is TMP_Text || graphic is Text)
                && graphic.GetComponentInParent<Button>() == null)
            {
                Inkify(graphic);
            }
        }
    }

    /// <summary>
    /// Seats a text band inside the parchment and lets it auto-size within that band.
    /// Applying the scroll sprite alone is not enough: the runtime panels were laid out
    /// against a flat rectangle, so their content has to be re-anchored into the paper or
    /// it lands on the rods and past the edges.
    /// </summary>
    public static void PlaceText(TMP_Text text, Rect area, float fontSizeMin, float fontSizeMax)
    {
        if (text == null)
            return;

        SetAnchors(text.rectTransform, area);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSizeMin;
        text.fontSizeMax = fontSizeMax;
        text.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>
    /// Keeps reference prose at a reading size inside its authored band.
    /// Call after seating the text so the viewport follows layout changes.
    /// </summary>
    public static ScrollRect MakeReadingScroll(TMP_Text text)
    {
        if (text == null || text.rectTransform.parent == null)
            return null;

        RectTransform content = text.rectTransform;
        ScrollRect scroll = content.parent.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            ScrollRect authored = content.GetComponentInParent<ScrollRect>(true);
            if (authored != null && authored.content != null
                && (authored.content == content || content.IsChildOf(authored.content)))
            {
                // Authored almanac/boss panels already have a masked, fitted scroll.
                // Nesting a second viewport inside its layout group collapses the body.
                ConfigureReadingCopy(text);
                // Authored bars can overlay a full-width viewport. Keep the prose
                // clear of that track without replacing its visibility policy.
                if (authored.verticalScrollbar != null)
                {
                    float lane = authored.verticalScrollbar.GetComponent<RectTransform>().rect.width + 12f;
                    Vector4 margin = text.margin;
                    margin.z = Mathf.Max(margin.z, lane);
                    text.margin = margin;
                }
                if (authored.content != content && authored.content.GetComponent<LayoutGroup>() == null)
                {
                    VerticalLayoutGroup layout = authored.content.gameObject.AddComponent<VerticalLayoutGroup>();
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                }
                ContentSizeFitter fitter = authored.content.GetComponent<ContentSizeFitter>();
                if (fitter == null) fitter = authored.content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                SetAnchors(authored.viewport, Rect.MinMaxRect(0f, 0f, 1f, 1f));
                EnsureVerticalScrollbar(authored);
                LayoutRebuilder.ForceRebuildLayoutImmediate(authored.content);
                authored.StopMovement();
                authored.verticalNormalizedPosition = 1f;
                return authored;
            }
        }
        bool creatingViewport = scroll == null;
        if (scroll == null)
        {
            GameObject host = new GameObject("ReadingViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            host.transform.SetParent(content.parent, false);
            Image background = host.GetComponent<Image>();
            background.color = Color.clear;
            scroll = host.GetComponent<ScrollRect>();
            scroll.viewport = host.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            content.SetParent(host.transform, false);
            ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        RectTransform viewport = scroll.viewport;
        // A reused scroll still has top-stretched content. Only copy the band
        // when first creating it or when its caller explicitly reseats the text.
        if (creatingViewport || content.anchorMin != new Vector2(0f, 1f) || content.anchorMax != Vector2.one)
        {
            viewport.anchorMin = content.anchorMin;
            viewport.anchorMax = content.anchorMax;
            viewport.offsetMin = content.offsetMin;
            viewport.offsetMax = content.offsetMax;
        }
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        ConfigureReadingCopy(text);
        EnsureVerticalScrollbar(scroll);
        // A new line reseats the content, so restore the scrollbar lane as well.
        if (scroll.verticalScrollbar != null)
        {
            RectTransform track = scroll.verticalScrollbar.GetComponent<RectTransform>();
            content.offsetMax = new Vector2(-track.rect.width - 12f, 0f);
        }
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
        return scroll;
    }

    private static void ConfigureReadingCopy(TMP_Text text)
    {
        text.fontSize = Mathf.Max(text.fontSize, UITextScale.Body);
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>Seats a button inside the parchment. Companion to <see cref="PlaceText"/>.</summary>
    public static void PlaceButton(Button button, Rect area, bool primary = true)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        SetAnchors(rect, area);
        rect.pivot = new Vector2(0.5f, 0.5f);
        if (primary)
            StylePrimaryButton(button);
        else
            StyleSecondaryButton(button);
    }

    /// <summary>
    /// Sizes a parchment button's label: stretched to the button rect and
    /// auto-sized between the reading floor and the title tier, so every
    /// scroll-family action renders at the same readable size no matter how the
    /// button was built. <see cref="UITextScale.Body"/> stays the floor, so a
    /// label never shrinks below the size it had before.
    /// </summary>
    public static void SizeButtonLabel(Button button)
    {
        if (button == null)
            return;

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);
            // TMP measures advances for auto-sizing; bold glyph ink can extend
            // slightly beyond those advances. Keep that ink inside the label.
            label.margin = new Vector4(4f, 0f, 4f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = UITextScale.Body;
            label.fontSizeMax = UITextScale.Title;
            label.textWrappingMode = TextWrappingModes.Normal;
            if (label.richText && !(label.textPreprocessor is ActionLabelPreprocessor))
                label.textPreprocessor = new ActionLabelPreprocessor(label.textPreprocessor);
        }
    }

    // Keep words intact without changing the label's source text. Wrapping remains
    // available between words, and existing rich-text tags pass through unchanged.
    private sealed class ActionLabelPreprocessor : ITextPreprocessor
    {
        private static readonly Regex Word = new Regex(@"(?:<[^>]*>|[^\s<])+", RegexOptions.Compiled);
        private readonly ITextPreprocessor _previous;

        public ActionLabelPreprocessor(ITextPreprocessor previous)
        {
            _previous = previous;
        }

        public string PreprocessText(string text)
        {
            string display = _previous != null ? _previous.PreprocessText(text) : text;
            return string.IsNullOrEmpty(display) ? display
                : Word.Replace(display, match => "<nobr>" + match.Value + "</nobr>");
        }
    }

    /// <summary>Primary action: a parchment tile with dark ink.</summary>
    public static void StylePrimaryButton(Button button)
    {
        StyleActionButton(button, GoldButtonFill, true);
    }

    /// <summary>Secondary action: a quieter tint of the same parchment tile.</summary>
    public static void StyleSecondaryButton(Button button)
    {
        StyleActionButton(button, SlateButtonFill, false);
    }

    /// <summary>
    /// Restyles an authored or runtime action while preserving its events and layout.
    /// </summary>
    public static void StyleActionButton(Button button, Color fill, bool inkLabel)
    {
        ApplyButtonSkin(button, inkLabel, fill);
        SizeButtonLabel(button);
    }

    /// <summary>Shared artwork and interaction states, without changing label sizing.</summary>
    public static void ApplyButtonSkin(Button button, bool primary = true, Color? fallbackFill = null)
    {
        if (button == null)
            return;
        Sprite sprite = LoadButtonSprite();
        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
            image.overrideSprite = null;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = sprite != null
                ? (primary ? Color.white : new Color32(224, 208, 178, 255))
                : (fallbackFill ?? (primary ? GoldButtonFill : SlateButtonFill));
            image.preserveAspect = false;
            button.targetGraphic = image;
        }
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color32(255, 239, 196, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color32(222, 193, 147, 255);
        colors.disabledColor = new Color32(160, 150, 130, 255);
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            TutorialFontProvider.ApplyTo(label);
            if (sprite != null || primary)
                Inkify(label);
            else
                label.color = Color.white;
        }
        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            if (sprite != null || primary)
                Inkify(label);
            else
                label.color = Color.white;
        }
    }

    private static Sprite LoadButtonSprite()
    {
        if (_buttonSprite != null)
            return _buttonSprite;
        Sprite source = Full;
        if (source == null)
            return null;
        // Use the paper and stepped inner frame of the existing full-scroll sprite,
        // excluding its rods. These insets match PanelBackground_0's imported rect.
        Rect paper = new Rect(source.rect.x + 68f, source.rect.y + 64f,
            source.rect.width - 136f, source.rect.height - 119f);
        _buttonSprite = Sprite.Create(source.texture, paper,
            new Vector2(0.5f, 0.5f), source.pixelsPerUnit * 8f, 0,
            SpriteMeshType.FullRect, new Vector4(100f, 100f, 100f, 100f), false);
        _buttonSprite.name = "ScrollButtonParchment";
        _buttonSprite.hideFlags = HideFlags.HideAndDontSave;
        return _buttonSprite;
    }

    public static void SetAnchors(RectTransform rect, Rect area)
    {
        if (rect == null)
            return;

        rect.anchorMin = area.min;
        rect.anchorMax = area.max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Resolves the canvas a runtime modal should render under: the owner's own
    /// parent canvas when usable, otherwise the highest-order active canvas that
    /// is not hidden or input-blocked by a CanvasGroup. A faded host such as a
    /// post-transition loading canvas (alpha 0, raycasts blocked) would leave the
    /// modal invisible and unclickable, so it is never chosen.
    /// </summary>
    public static Canvas ResolveModalCanvas(Component owner)
    {
        Canvas parented = owner != null ? owner.GetComponentInParent<Canvas>() : null;
        if (IsUsableModalHost(parented))
            return parented;

        Canvas best = null;
        foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsUsableModalHost(candidate))
                continue;
            if (best == null || candidate.sortingOrder > best.sortingOrder)
                best = candidate;
        }
        return best;
    }

    private static bool IsUsableModalHost(Canvas canvas)
    {
        if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy)
            return false;
        CanvasGroup group = canvas.GetComponentInParent<CanvasGroup>();
        if (group != null && (group.alpha <= 0.01f || !group.blocksRaycasts))
            return false;
        return true;
    }

    /// <summary>
    /// Builds the shared modal chrome: a full-screen dim overlay as the canvas's
    /// last sibling. Pair with <see cref="CreateScrollPanel"/> for runtime-built
    /// panels, or reparent an authored panel into it and seat that panel at
    /// <see cref="ScrollArea"/>.
    /// </summary>
    public static GameObject CreateDimOverlay(Transform parent, string name)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        overlay.transform.SetAsLastSibling();
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = DimOverlayColor;
        overlayImage.raycastTarget = true;
        return overlay;
    }

    /// <summary>Creates the scroll-sized panel child of a dim overlay.</summary>
    public static RectTransform CreateScrollPanel(Transform overlayParent, string name)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlayParent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        SetAnchors(rect, ScrollArea);
        Image image = panel.GetComponent<Image>();
        image.color = FlatPanelColor;
        image.raycastTarget = true;
        return rect;
    }

    /// <summary>Adds a vertical scrollbar that remains visible whenever content
    /// overflows. AutoHide responds to overflow, not inactivity. Existing authored
    /// scrollbars retain their wiring and viewport expansion settings.</summary>
    public static void EnsureVerticalScrollbar(ScrollRect scroll)
    {
        if (scroll == null || !scroll.vertical || scroll.viewport == null
            || scroll.content == null || scroll.verticalScrollbar != null)
            return;

        const float width = 28f;
        const float gap = 12f;

        // Reserve a lane inside the mask, beside rather than over the content.
        Vector2 contentInset = scroll.content.offsetMax;
        contentInset.x -= width + gap;
        scroll.content.offsetMax = contentInset;

        GameObject track = new GameObject(
            "[Runtime] VerticalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        track.transform.SetParent(scroll.viewport, false);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = Vector2.one;
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(width, 0f);
        trackRect.anchoredPosition = Vector2.zero;
        track.GetComponent<Image>().color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.25f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(track.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = new Color(0.85f, 0.72f, 0.35f, 1f);

        Scrollbar scrollbar = track.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private static bool Apply(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return false;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        return true;
    }

    private static Sprite Find(string name)
    {
        EnsureLoaded();
        if (_sprites == null)
            return null;

        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] != null && _sprites[i].name == name)
                return _sprites[i];
        }

        return null;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        _sprites = Resources.LoadAll<Sprite>(ResourcePath);
    }
}
