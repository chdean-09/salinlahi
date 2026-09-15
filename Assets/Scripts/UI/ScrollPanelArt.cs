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
        if (text != null)
            text.color = InkColor;
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
