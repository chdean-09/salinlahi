using UnityEngine;

[CreateAssetMenu(
    fileName = "GameConfig_Default",
    menuName = "Salinlahi/Game Config")]
public class GameConfigSO : ScriptableObject
{
    [Header("Combo Settings")]
    [Tooltip("Consecutive correct draws needed to trigger Focus Mode")]
    public int focusModeThreshold = 5;

    [Header("Focus Mode Settings")]
    [Tooltip("How long Focus Mode lasts in seconds")]
    public float focusModeDuration = 5f;

    [Tooltip("Enemy speed multiplier during Focus Mode. "
        + "0.5 = half speed.")]
    [Range(0.1f, 1f)]
    public float focusModeSpeedMultiplier = 0.5f;

    // SALIN-182. Every quantity below is a balance value the ticket explicitly does not fix -- it
    // supplies the variants and reward names "but not the exact threshold or tier mapping". They are
    // serialized so they can be tuned per era without a code change, and the defaults preserve
    // current behaviour rather than asserting an approved balance.
    [Header("Combo Powers (SALIN-182)")]
    [Tooltip("Master switch for tier-granted combo powers. Off leaves Focus Mode exactly as it was.")]
    public bool comboPowersEnabled = true;

    [Tooltip("Tier 2 Rapid Shot: extra combat hits on the active target. Grants no language progress.")]
    [Min(0)]
    public int rapidShotBonusHits = 1;

    [Tooltip("Tiers 3-4 Piercing Arrow: also strike one other enemy. "
        + "Pending the lane model, the nearest enemy to the base stands in for the aligned target.")]
    public bool piercingArrowEnabled = true;

    [Tooltip("Tier 5 Shield charges held at once. 1 keeps the shield nonstacking, as specified.")]
    [Min(0)]
    public int shieldCharges = 1;

    [Header("Correction Window (SALIN-182)")]
    [Tooltip("Seconds after a recognition during which an identical repeat is treated as an echo "
        + "rather than a new attempt. Shorter windows make correction stricter. Default 0.15.")]
    [Min(0f)]
    public float echoedRecognitionSeconds = 0.15f;
}
