using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Animates a guide dot along a stroke path to demonstrate the correct glyph drawing motion.
/// Lives on the TutorialAssistAnimation prefab. Feed it the templatePoints from the active
/// Level1TutorialStepSO, call Play(), and subscribe to OnComplete for cleanup.
///
/// The path is rendered by TutorialPathTrail (must be on a child GameObject).
/// The guide dot is a child Transform (typically a small sprite or particle root).
/// </summary>
public sealed class TutorialAssistAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialPathTrail _pathTrail;
    [SerializeField] private Transform _guideDot;

    [Header("Timing")]
    [Tooltip("Seconds to travel the full stroke path in one pass.")]
    [SerializeField] private float _strokeDuration = 1.8f;
    [Tooltip("Seconds the dot holds at the end before fading / looping.")]
    [SerializeField] private float _holdAtEndSeconds = 0.45f;
    [Tooltip("How many times to play the stroke. 0 = loop until Stop() is called.")]
    [SerializeField] private int _loopCount = 2;

    /// <summary>Fires once all loops complete (or immediately after Stop() if cut short).</summary>
    public event Action OnComplete;

    private Coroutine _playCoroutine;

    /// <summary>
    /// Begin the assist animation using the given world-space template points.
    /// Safe to call while already playing — restarts from the beginning.
    /// </summary>
    public void Play(Vector2[] templatePoints)
    {
        if (_playCoroutine != null) StopCoroutine(_playCoroutine);
        _playCoroutine = StartCoroutine(AnimateCoroutine(templatePoints));
    }

    /// <summary>Immediately stop playback and fire OnComplete.</summary>
    public void Stop()
    {
        if (_playCoroutine != null)
        {
            StopCoroutine(_playCoroutine);
            _playCoroutine = null;
        }
        Cleanup();
        OnComplete?.Invoke();
    }

    private IEnumerator AnimateCoroutine(Vector2[] points)
    {
        if (_pathTrail != null) _pathTrail.Setup(points);

        if (points == null || points.Length < 2)
        {
            Cleanup();
            OnComplete?.Invoke();
            yield break;
        }

        float[] arcLengths = ComputeArcLengths(points);

        int loops = 0;
        while (_loopCount == 0 || loops < _loopCount)
        {
            yield return StrokeOnce(points, arcLengths);
            loops++;
            if (_loopCount != 0 && loops >= _loopCount) break;
            yield return new WaitForSecondsRealtime(_holdAtEndSeconds);
        }

        Cleanup();
        OnComplete?.Invoke();
    }

    private IEnumerator StrokeOnce(Vector2[] points, float[] arcLengths)
    {
        if (_guideDot != null) _guideDot.gameObject.SetActive(true);

        float totalLength = arcLengths[arcLengths.Length - 1];
        float elapsed = 0f;

        // Snap to start
        if (_guideDot != null)
        {
            Vector2 start = points[0];
            _guideDot.position = new Vector3(start.x, start.y, _guideDot.position.z);
        }

        while (elapsed < _strokeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _strokeDuration);
            Vector2 pos = SampleAlongArc(points, arcLengths, t * totalLength);
            if (_guideDot != null)
                _guideDot.position = new Vector3(pos.x, pos.y, _guideDot.position.z);
            yield return null;
        }

        // Snap to end
        if (_guideDot != null)
        {
            Vector2 end = points[points.Length - 1];
            _guideDot.position = new Vector3(end.x, end.y, _guideDot.position.z);
        }

        yield return new WaitForSecondsRealtime(_holdAtEndSeconds);
    }

    private void Cleanup()
    {
        if (_pathTrail != null) _pathTrail.Hide();
        if (_guideDot != null) _guideDot.gameObject.SetActive(false);
    }

    private static float[] ComputeArcLengths(Vector2[] points)
    {
        float[] lengths = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
            lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        return lengths;
    }

    private static Vector2 SampleAlongArc(Vector2[] points, float[] arcLengths, float targetArc)
    {
        for (int i = 1; i < points.Length; i++)
        {
            if (arcLengths[i] >= targetArc)
            {
                float segLen = arcLengths[i] - arcLengths[i - 1];
                float t = segLen > 0f ? (targetArc - arcLengths[i - 1]) / segLen : 0f;
                return Vector2.Lerp(points[i - 1], points[i], t);
            }
        }
        return points[points.Length - 1];
    }
}
