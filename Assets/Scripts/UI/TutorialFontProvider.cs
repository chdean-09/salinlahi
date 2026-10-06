using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TutorialFontProvider
{
    private const string FontAssetPath = "Fonts/TutorialFont";
    private static TMP_FontAsset _cachedFontAsset;

    // A small SDF shadow separates light HUD text from scenery. Parchment inking
    // removes it so dark reading text retains open counters and clean strokes.
    private static readonly Color LegibilityUnderlayColor = new Color(0f, 0f, 0f, 0.85f);
    private const float LegibilityUnderlayOffsetX = 0.25f;
    private const float LegibilityUnderlayOffsetY = -0.25f;
    // Keep VT323's pixel shapes while reducing the synthetic bold expansion that
    // closes small counters on phone screens. The authored font asset stays intact.
    private const float LegibilityBoldWeight = 0.2f;
    // Expanding the shadow also expands its atlas sampling footprint. With this
    // font's nine-pixel atlas padding, dilation bleeds adjacent glyphs into thin
    // horizontal lines above/below the text. An offset silhouette needs no dilation.
    private const float LegibilityUnderlayDilate = 0f;
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

            mat.SetFloat(Shader.PropertyToID("_WeightBold"), LegibilityBoldWeight);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(Shader.PropertyToID("_UnderlayColor"), LegibilityUnderlayColor);
            mat.SetFloat(Shader.PropertyToID("_UnderlayOffsetX"), LegibilityUnderlayOffsetX);
            mat.SetFloat(Shader.PropertyToID("_UnderlayOffsetY"), LegibilityUnderlayOffsetY);
            mat.SetFloat(Shader.PropertyToID("_UnderlayDilate"), LegibilityUnderlayDilate);
            mat.SetFloat(Shader.PropertyToID("_UnderlaySoftness"), 0f);
            tmp.UpdateMeshPadding();
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
    /// dark ink on light paper needs no outline or shadow. An inherited outline
    /// closes the counters in VT323, even when the text is not explicitly bold.
    /// </summary>
    public static void ClearLegibilityEffects(Graphic graphic)
    {
        if (graphic == null)
            return;

        if (graphic is TMP_Text tmp)
        {
            if (Application.isPlaying && tmp.fontMaterial != null)
            {
                tmp.fontMaterial.DisableKeyword("UNDERLAY_ON");
                tmp.fontMaterial.DisableKeyword("OUTLINE_ON");
                tmp.fontMaterial.SetFloat(Shader.PropertyToID("_OutlineWidth"), 0f);
                tmp.fontMaterial.SetFloat(Shader.PropertyToID("_FaceDilate"), 0f);
                tmp.fontMaterial.SetFloat(Shader.PropertyToID("_WeightBold"), LegibilityBoldWeight);
                tmp.UpdateMeshPadding();
            }
            return;
        }

        foreach (Shadow effect in graphic.GetComponents<Shadow>())
        {
            if (effect.GetType() == typeof(Shadow))
                effect.enabled = false;
        }
    }
}
