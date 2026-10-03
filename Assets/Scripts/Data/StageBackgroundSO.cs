using System;
using UnityEngine;

/// <summary>
/// A stage's ground set: base tiles, a weighted scatter layer and side-margin strips,
/// all authored on the 32 PPU grid by scripts/art/generate_stage_backgrounds.py and
/// composited at level load by <see cref="StageBackgroundBaker"/> into one texture.
/// Referenced from <see cref="EraThemeSO.stageBackground"/>.
/// </summary>
[CreateAssetMenu(fileName = "StageBackground", menuName = "Salinlahi/Stage Background")]
public class StageBackgroundSO : ScriptableObject
{
    [Serializable]
    public struct ScatterEntry
    {
        public Sprite sprite;
        [Tooltip("Relative placement frequency. Variants of one element type share its weight.")]
        public float weight;
        [Tooltip("Safe to mirror horizontally. Anything with a lit face or cast shadow is not: the key light would flip.")]
        public bool flippable;
    }

    [Header("Ground")]
    [Tooltip("32x32 base tiles, picked at random per cell.")]
    public Sprite[] groundTiles;
    [Tooltip("Base tiles with a trodden band, used in the three centre columns where enemies walk.")]
    public Sprite[] wornTiles;
    [Range(0f, 1f)] public float wornChance = 0.65f;

    [Header("Scatter")]
    public ScatterEntry[] scatter;
    [Tooltip("Placement attempts per 32x32 tile of lane.")]
    public float scatterPerTile = 5f;
    [Tooltip("Minimum centre-to-centre distance between placed elements, in texels.")]
    public int scatterMinDistancePx = 6;

    [Header("Margins")]
    [Tooltip("Strips tiling in Y along the left edge of the play column; one is chosen per repeat.")]
    public Sprite[] marginLeft;
    public Sprite[] marginRight;

    public bool IsComplete =>
        AllPresent(groundTiles) && AllPresent(marginLeft) && AllPresent(marginRight)
        && scatter != null && scatter.Length > 0 && Array.TrueForAll(scatter, e => e.sprite != null && e.weight > 0f);

    /// <summary>Width of the margin strips in texels; the lane is what remains between them.</summary>
    public int MarginWidthPx => AllPresent(marginLeft) ? Mathf.RoundToInt(marginLeft[0].rect.width) : 0;

    private static bool AllPresent(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0) return false;
        foreach (Sprite s in sprites) if (s == null) return false;
        return true;
    }
}
