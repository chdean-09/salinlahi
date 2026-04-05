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
}
