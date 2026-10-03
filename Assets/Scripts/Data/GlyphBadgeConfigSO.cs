using UnityEngine;

[CreateAssetMenu(fileName = "GlyphBadgeConfig", menuName = "Salinlahi/Glyph Badge Config")]
public class GlyphBadgeConfigSO : ScriptableObject
{
    [Header("Layout")]
    [Tooltip("Default local offset from the enemy root for the GlyphBadge child. Overridden per-enemy by EnemyDataSO.glyphBadgeOffsetOverride.")]
    public Vector2 defaultWorldOffset = new Vector2(0f, 1.2f);

    [Tooltip("Default world-stable scale of the badge transform. Overridden per-enemy by EnemyDataSO.glyphBadgeScaleOverride.")]
    public float defaultWorldScale = 1f;

    [Header("Swap Animation (Capitan hurt-swap, boss intermediate draws)")]
    [Tooltip("Direction + magnitude of the old-sprite slide-out / new-sprite slide-in.")]
    public Vector2 swapSlideOffset = new Vector2(-0.8f, 0f);

    [Tooltip("Seconds the old sprite spends fading and sliding out.")]
    public float swapOutDuration = 0.18f;

    [Tooltip("Seconds the new sprite spends fading and sliding in.")]
    public float swapInDuration = 0.18f;

    [Header("Final-Draw Animation (Seal Broken)")]
    public Color finalDrawFlashColor = Color.white;
    [Tooltip("Seconds of the charge phase (scale up + flash tint).")]
    public float finalDrawChargeDuration = 0.08f;
    [Tooltip("Peak scale multiplier reached at the end of the charge phase.")]
    public float finalDrawChargeScale = 1.15f;
    [Tooltip("Seconds of the release phase (scale to 0, alpha to 0, drift up + rotate).")]
    public float finalDrawReleaseDuration = 0.18f;
    [Tooltip("Local-space Y offset added during the release phase.")]
    public float finalDrawReleaseRise = 0.25f;
    [Tooltip("Degrees of rotation added during the release phase.")]
    public float finalDrawReleaseRotation = 10f;

    [Header("Decoy Reject")]
    public Color decoyRejectFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public float decoyRejectFlashDuration = 0.1f;
    [Tooltip("Peak horizontal shake offset (world units).")]
    public float decoyRejectShakeMagnitude = 0.1f;
    public float decoyRejectShakeDuration = 0.3f;
    [Tooltip("Shake oscillations per second.")]
    public float decoyRejectShakeFrequency = 18f;

    [Header("False Glyph (stained / overridden badge)")]
    [Tooltip("Opacity multiplier applied while the badge shows a FALSE face (a visual character override) instead of the enemy's true symbol. Multiplies whatever alpha the swap/fade routines currently own, so the tell composes with them. 1 disables the dim.")]
    [Range(0f, 1f)]
    public float falseGlyphAlpha = 0.45f;

    [Tooltip("Colour multiplied into a badge showing a false face. Keeps the tell legible where a dim alpha alone would sit over bright art. White disables the tint.")]
    public Color falseGlyphTint = new Color(0.62f, 0.66f, 0.78f, 1f);

    [Header("Fail Feedback (Boss draw failed)")]
    public Color failFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    public float failFlashDuration = 0.15f;

    private void OnValidate()
    {
        defaultWorldScale = Mathf.Max(0.01f, defaultWorldScale);
        swapOutDuration = Mathf.Max(0f, swapOutDuration);
        swapInDuration = Mathf.Max(0f, swapInDuration);
        finalDrawChargeDuration = Mathf.Max(0f, finalDrawChargeDuration);
        finalDrawChargeScale = Mathf.Max(0.01f, finalDrawChargeScale);
        finalDrawReleaseDuration = Mathf.Max(0f, finalDrawReleaseDuration);
        decoyRejectFlashDuration = Mathf.Max(0f, decoyRejectFlashDuration);
        decoyRejectShakeDuration = Mathf.Max(0f, decoyRejectShakeDuration);
        decoyRejectShakeFrequency = Mathf.Max(0f, decoyRejectShakeFrequency);
        failFlashDuration = Mathf.Max(0f, failFlashDuration);
        falseGlyphAlpha = Mathf.Clamp01(falseGlyphAlpha);
    }
}
