using UnityEngine;

[CreateAssetMenu(fileName = "RecognitionConfig", menuName = "Salinlahi/Recognition Config")]
public class RecognitionConfigSO : ScriptableObject
{
    [Header("Point Cloud")]
    [Range(16, 64)]
    [Tooltip("Total number of points the $P recognizer resamples the full drawing/template to. Default: 32")]
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

    [Header("Mobile Stroke Sampling")]
    [Tooltip("Minimum screen-pixel distance before a real touch sample is appended to recognition input.")]
    public float rawSampleMinDistancePixels = 2f;

    [Tooltip("Target spacing in screen pixels for visual-only stroke subdivision.")]
    public float visualSampleSpacingPixels = 8f;

    [Tooltip("Maximum visual-only points inserted for one sparse real input segment.")]
    public int maxVisualSamplesPerSegment = 24;

    [Header("Tap Rejection")]
    [Tooltip("Minimum path length in screen pixels before a completed stroke is considered intentional drawing.")]
    public float minimumStrokePathLengthPixels = 40f;

    [Tooltip("Minimum width or height in screen pixels before a completed stroke is considered intentional drawing.")]
    public float minimumStrokeBoundsPixels = 12f;
}
