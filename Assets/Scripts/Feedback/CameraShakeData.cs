using UnityEngine;

[CreateAssetMenu(menuName = "Salinlahi/Feedback/Camera Shake Data", fileName = "CameraShakeData")]
public sealed class CameraShakeData : ScriptableObject
{
    [Tooltip("How long the camera shake lasts in seconds.")]
    [Min(0f)]
    public float Duration = 0.60f;

    [Tooltip("How strong the camera shake is.")]
    [Min(0f)]
    public float Magnitude = 0.08f;

    [Tooltip("Optional curve to control the falloff over time. Defaults to linear ease-out if not set or empty.")]
    public AnimationCurve FalloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
}
