using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TutorialFontProvider
{
    private const string FontAssetPath = "Fonts/TutorialFont";
    private static TMP_FontAsset _cachedFontAsset;

    // Readability treatment shared by every player-facing text. For TMP texts this is
    // the SDF shader's underlay: a hard-edged offset silhouette rendered inside the
    // glyph shader, which on the pixel-style VT323 font reads as a deliberate retro
    // drop shadow. uGUI Shadow/Outline stamp offset copies of the glyph and visibly
    // ghost/thicken thin pixel glyphs, so they are not used here.
    private static readonly Color LegibilityUnderlayColor = new Color(0f, 0f, 0f, 0.85f);
    private const float LegibilityUnderlayOffsetX = 0.55f;
    private const float LegibilityUnderlayOffsetY = -0.55f;
    private const float LegibilityUnderlayDilate = 0.55f;
    private const float LegibilityShadowAlpha = 0.6f;
    private static readonly Vector2 LegibilityShadowDistance = new Vector2(1.5f, -1.5f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReloadInit()
    {
        _cachedFontAsset = null;
    }

    public static TMP_FontAsset FontAsset
    {
        get
        {
            if (_cachedFontAsset == null)
                _cachedFontAsset = Resources.Load<TMP_FontAsset>(FontAssetPath);

            return _cachedFontAsset;
        }
    }

    public static void ApplyTo(TMP_Text textComponent)
    {
        if (textComponent == null)
            return;

        TMP_FontAsset font = FontAsset;
        if (font != null)
            textComponent.font = font;

        ApplyLegibilityEffects(textComponent);
    }

    /// <summary>
    /// Applies the shared readability treatment. TMP texts get the SDF underlay
    /// (runtime only — touching fontMaterial creates a material instance, which must
    /// never land on a serialized component in Edit mode). Legacy uGUI Text gets a
    /// small uGUI Shadow; its non-pixel fonts render that cleanly.
    /// </summary>
    public static void ApplyLegibilityEffects(Graphic graphic)
    {
        if (graphic == null)
            return;

        if (graphic is TMP_Text tmp)
        {
            if (!Application.isPlaying)
                return;

            Material mat = tmp.fontMaterial;
            if (mat == null)
                return;

            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(Shader.PropertyToID("_UnderlayColor"), LegibilityUnderlayColor);
            mat.SetFloat(Shader.PropertyToID("_UnderlayOffsetX"), LegibilityUnderlayOffsetX);
            mat.SetFloat(Shader.PropertyToID("_UnderlayOffsetY"), LegibilityUnderlayOffsetY);
            mat.SetFloat(Shader.PropertyToID("_UnderlayDilate"), LegibilityUnderlayDilate);
            mat.SetFloat(Shader.PropertyToID("_UnderlaySoftness"), 0f);
            return;
        }

        // Match on exact type — Outline subclasses Shadow, so a Shadow found this way
        // could actually be an Outline another system added.
        Shadow shadow = null;
        foreach (Shadow effect in graphic.GetComponents<Shadow>())
        {
            if (effect.GetType() == typeof(Shadow))
            {
                shadow = effect;
                break;
            }
        }
        if (shadow == null)
            shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, LegibilityShadowAlpha);
        shadow.effectDistance = LegibilityShadowDistance;
    }

    /// <summary>
    /// Removes the legibility treatment — called by parchment inking paths, where
    /// dark ink on light paper needs no dark separation and the underlay would
    /// only thicken the glyphs.
    /// </summary>
    public static void ClearLegibilityEffects(Graphic graphic)
    {
        if (graphic == null)
            return;

        if (graphic is TMP_Text tmp)
        {
            if (Application.isPlaying && tmp.fontMaterial != null)
                tmp.fontMaterial.DisableKeyword("UNDERLAY_ON");
            return;
        }

        foreach (Shadow effect in graphic.GetComponents<Shadow>())
        {
            if (effect.GetType() == typeof(Shadow))
                effect.enabled = false;
        }
    }
}
