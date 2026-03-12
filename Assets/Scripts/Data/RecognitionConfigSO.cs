using UnityEngine;

[CreateAssetMenu(fileName = "RecognitionConfig", menuName = "Salinlahi/Recognition Config")]
public class RecognitionConfigSO : ScriptableObject
{
    [Header("Point Cloud")]
    [Range(16, 64)]
    [Tooltip("Number of points the $P algorithm resamples each stroke to. Default: 32")]
    public int resamplePointCount = 32;

    [Header("Confidence")]
    [Range(0f, 1f)]
    [Tooltip("Minimum score (0-1) required to accept a recognition result. Default: 0.60")]
    public float minimumConfidence = 0.60f;

    [Header("Stroke Timing")]
    [Tooltip("Seconds after lifting the finger before the system submits for recognition")]
    public float multiStrokeWindowSeconds = 1.5f;

    [Tooltip("Minimum screen points in a stroke to be considered valid. Prevents taps.")]
    public int minimumPointCount = 8;
}
