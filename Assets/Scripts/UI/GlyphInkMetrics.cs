using System;
using System.Collections.Generic;

/// <summary>
/// Per-glyph ink fractions for the Baybayin sprite sets: the share of each sprite's
/// frame that is actual ink on its dominant axis (the larger of width and height),
/// measured from the alpha bounding box of the PNGs.
///
/// WHY PER-GLYPH, NOT ONE CONSTANT. The shared 0.44 calibration was measured on A,
/// NA and MA, which happen to sit at the bottom of the real range (0.40-0.72). A
/// flat value makes the same nominal ink size render visibly different sizes —
/// WA, KA, SA, DA and HA overshot while GA, OU and EI undershot — so a row of
/// "equal" glyphs looked unequal. Sizing each glyph's rect by its own dominant
/// fraction gives every glyph the same logical ink box, which is what "the same
/// size" means for letterforms of different aspect.
/// </summary>
public static class GlyphInkMetrics
{
    /// <summary>Fallback for a symbol with no measured value: the old calibration.</summary>
    public const float DefaultAlmanacFraction = 0.44f;

    /// <summary>
    /// The GlyphOutlines set (Art/UI/GlyphOutlines/[ID].png) is generated from the
    /// recognition templates and every glyph shares a uniform 0.828 ink width, so a
    /// single constant is exact there — no table needed.
    /// </summary>
    public const float OutlineFraction = 0.828f;

    /// <summary>
    /// The dominant-axis ink fraction of <paramref name="symbol"/>'s almanac sprite
    /// (Art/UI/Almanac/[ID]-Almanac.png). Returns <paramref name="fallback"/> for a
    /// symbol outside the measured set.
    /// </summary>
    public static float ForAlmanac(BaybayinCharacterSO symbol, float fallback = DefaultAlmanacFraction)
    {
        if (symbol == null || string.IsNullOrEmpty(symbol.characterID))
            return fallback;
        return Almanac.TryGetValue(symbol.characterID, out float fraction) ? fraction : fallback;
    }

    private static readonly Dictionary<string, float> Almanac =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 0.438f,
            ["BA"] = 0.584f,
            ["DA"] = 0.706f,
            ["EI"] = 0.466f,
            ["GA"] = 0.419f,
            ["HA"] = 0.709f,
            ["KA"] = 0.716f,
            ["LA"] = 0.484f,
            ["MA"] = 0.441f,
            ["NA"] = 0.431f,
            ["NGA"] = 0.450f,
            ["OU"] = 0.522f,
            ["PA"] = 0.475f,
            ["RA"] = 0.484f,
            ["SA"] = 0.672f,
            ["TA"] = 0.450f,
            ["WA"] = 0.547f,
            ["YA"] = 0.506f,
        };
}
