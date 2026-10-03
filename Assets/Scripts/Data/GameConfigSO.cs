using UnityEngine;

[CreateAssetMenu(
    fileName = "GameConfig_Default",
    menuName = "Salinlahi/Game Config")]
public class GameConfigSO : ScriptableObject
{
    [Header("Correction Window (SALIN-182)")]
    [Tooltip("Seconds after a recognition during which an identical repeat is treated as an echo "
        + "rather than a new attempt. Shorter windows make correction stricter. Default 0.15.")]
    [Min(0f)]
    public float echoedRecognitionSeconds = 0.15f;
}
