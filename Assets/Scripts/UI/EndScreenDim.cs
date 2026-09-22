using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The end screens' translucent backdrop. Gameplay.unity authors each panel with TWO
/// stacked full-screen layers — an opaque navy "Backdrop" over a 0.85 "Background" —
/// and Level_01_Tutorial.unity authors "Background" alone. Either way the world is
/// fully covered, which reads flat once the screens became content-rich. Apply()
/// retints the FIRST matching layer to a translucent dim so the frozen gameplay frame
/// stays visible behind the results, and disables the redundant layers so stacked
/// alphas cannot compound past the chosen opacity.
///
/// Runtime-only by design: both scenes are high-conflict serialized assets, and
/// EndScreenBackdropSceneTests pins the AUTHORED backdrop as opaque — that guard
/// protects against the canvas-level scrim defect it was written for, not this
/// deliberate presentation-time dim.
/// </summary>
public static class EndScreenDim
{
    /// <summary>House navy at ~80% — the world reads clearly but stays pushed back.</summary>
    public static readonly Color VictoryTint = new Color(0.015f, 0.02f, 0.045f, 0.80f);

    /// <summary>Same depth, a faint red lean — sits under the red DEFEAT banner.</summary>
    public static readonly Color DefeatTint = new Color(0.055f, 0.015f, 0.025f, 0.80f);

    public const string RuntimeDimName = "[Runtime] DimBackdrop";

    public static void Apply(GameObject panel, Color tint)
    {
        if (panel == null)
            return;

        Image dim = null;
        Image backgroundNamed = null;
        var extras = new System.Collections.Generic.List<Image>();
        foreach (Transform child in panel.transform)
        {
            if (child.name != "Backdrop" && child.name != "Background" && child.name != RuntimeDimName)
                continue;
            if (!child.TryGetComponent(out Image image))
                continue;
            if (child.name == "Background" && backgroundNamed == null)
                backgroundNamed = image;
            else if (dim == null)
                dim = image;
            else
                extras.Add(image);
        }

        // Prefer "Backdrop" (Gameplay's opaque scrim); tutorial panels author only
        // "Background", which then takes the dim role.
        if (dim == null)
        {
            dim = backgroundNamed;
            backgroundNamed = null;
        }
        if (backgroundNamed != null)
            extras.Add(backgroundNamed);

        if (dim == null)
            dim = CreateRuntimeDim(panel.transform);

        dim.color = tint;
        dim.enabled = true;
        // Eat clicks so the dimmed world cannot steal pointer input.
        dim.raycastTarget = true;
        dim.transform.SetSiblingIndex(0);

        foreach (Image extra in extras)
            extra.enabled = false;
    }

    private static Image CreateRuntimeDim(Transform parent)
    {
        var dimObject = new GameObject(RuntimeDimName, typeof(RectTransform), typeof(Image));
        dimObject.transform.SetParent(parent, false);
        var rect = dimObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        // Match the authored overscan so the dim outcovers the panel at any aspect.
        rect.offsetMin = new Vector2(-2000f, -2000f);
        rect.offsetMax = new Vector2(2000f, 2000f);
        return dimObject.GetComponent<Image>();
    }
}
