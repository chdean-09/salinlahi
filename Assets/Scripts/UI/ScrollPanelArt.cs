using TMPro;
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
    public static readonly Rect ScrollArea = Rect.MinMaxRect(0.08f, 0.24f, 0.92f, 0.76f);

    /// <summary>Full-screen dim behind the scroll (matches the ready screen). 0.93 keeps
    /// a hint of scene presence while making whatever text sits underneath illegible —
    /// at 0.88 a live cutscene's caption and "Tap anywhere" prompt stayed readable
    /// around the scroll's edges and competed with the modal's own call to action.</summary>
    public static readonly Color DimOverlayColor = new Color(0.015f, 0.02f, 0.045f, 0.93f);

    /// <summary>Flat panel fallback used until/unless the parchment sprite applies.</summary>
    public static readonly Color FlatPanelColor = new Color(0.025f, 0.035f, 0.08f, 0.98f);

    /// <summary>The scroll-family button convention: gold carries the primary action,
    /// dark slate the secondary — the same pair the ready screen ships.</summary>
    public static readonly Color GoldButtonFill = new Color(0.85f, 0.72f, 0.35f, 1f);
    public static readonly Color SlateButtonFill = new Color(0.18f, 0.24f, 0.34f, 1f);

    private static Sprite[] _sprites;
    private static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReloadInit()
    {
        _sprites = null;
        _loaded = false;
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

    /// <summary>Seats a button inside the parchment. Companion to <see cref="PlaceText"/>.</summary>
    public static void PlaceButton(Button button, Rect area)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        SetAnchors(rect, area);
        rect.pivot = new Vector2(0.5f, 0.5f);
        SizeButtonLabel(button);
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
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = UITextScale.Body;
            label.fontSizeMax = UITextScale.Title;
        }
    }

    /// <summary>Primary-action skin: gold fill, ink label.</summary>
    public static void StylePrimaryButton(Button button)
    {
        StyleActionButton(button, GoldButtonFill, true);
    }

    /// <summary>Secondary-action skin: slate fill, white label.</summary>
    public static void StyleSecondaryButton(Button button)
    {
        StyleActionButton(button, SlateButtonFill, false);
    }

    /// <summary>
    /// Restyles a button — authored or runtime — to the shared flat convention: the
    /// plaque sprite comes off (its unsealed edges read mismatched at different
    /// aspect ratios), the flat fill goes on, press feedback falls back to color
    /// tint so an authored SpriteSwap cannot flash the old skin, and the label
    /// takes the shared font, role color, and autosized rect.
    /// </summary>
    public static void StyleActionButton(Button button, Color fill, bool inkLabel)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = fill;
            image.preserveAspect = false;
            button.targetGraphic = image;
        }
        button.transition = Selectable.Transition.ColorTint;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            TutorialFontProvider.ApplyTo(label);
            if (inkLabel)
                Inkify(label);
            else
                label.color = Color.white;
        }
        SizeButtonLabel(button);
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
