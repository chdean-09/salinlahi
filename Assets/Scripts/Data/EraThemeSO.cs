using UnityEngine;

/// <summary>
/// Defines the visual theme for a historical era (background, decorations, shrine).
/// Assign one per chapter/era. Referenced by LevelConfigSO.
/// </summary>
[CreateAssetMenu(fileName = "EraTheme", menuName = "Salinlahi/Era Theme")]
public class EraThemeSO : ScriptableObject
{
    [Header("Identity")]
    public string eraName;

    [Header("Background")]
    [Tooltip("Main background sprite (full screen behind everything)")]
    public Sprite backgroundSprite;

    [Tooltip("Camera background color fallback if no sprite is used")]
    public Color backgroundColor = new Color(0.93f, 0.70f, 0.55f);

    [Header("Ground")]
    [Tooltip("Ground sprite (tiled across the bottom of the scene)")]
    public Sprite groundSprite;

    [Header("Base Zone")]
    [Tooltip("The fence/barrier sprite at the shrine defense line")]
    public Sprite baseZoneSprite;

    [Tooltip("The shrine sprite for this era")]
    public Sprite shrineSprite;

    [Header("Decorations")]
    [Tooltip("Top-of-screen foliage/vine overlay")]
    public Sprite topFoliageSprite;

    [Tooltip("Bush/vegetation sprite")]
    public Sprite bushSprite;

    [Tooltip("Torch/lantern sprite")]
    public Sprite torchSprite;

    [Header("Pillar Fill")]
    [Tooltip("How to render the pillar area outside the play column on wider-than-target devices")]
    public PillarFillMode pillarMode = PillarFillMode.None;

    [Tooltip("Used when pillarMode == Color")]
    public Color pillarColor = Color.black;

    [Tooltip("Used when pillarMode == Sprite. Tileable recommended.")]
    public Sprite pillarSprite;
}
