using System.Collections;
using UnityEngine;

public sealed class CameraShakeController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CameraShakeData _defaultShakeData;
    [SerializeField] private float _fallbackDuration = 0.15f;
    [SerializeField] private float _fallbackMagnitude = 0.08f;

    private Coroutine _shakeRoutine;
    private Vector3 _restLocalPosition;

    private void Awake()
    {
        _restLocalPosition = transform.localPosition;
    }

    private void OnDisable()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        ResetTransform();
    }

    public void Shake()
    {
        if (_defaultShakeData != null)
            Shake(_defaultShakeData);
        else
            Shake(_fallbackDuration, _fallbackMagnitude);
    }

    public void Shake(CameraShakeData data)
    {
        if (data == null) return;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        ResetTransform();
        _shakeRoutine = StartCoroutine(ShakeRoutine(data.Duration, data.Magnitude, data.FalloffCurve));
    }

    public void Shake(float duration, float magnitude)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float safeMagnitude = Mathf.Max(0f, magnitude);

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        ResetTransform();
        _shakeRoutine = StartCoroutine(ShakeRoutine(safeDuration, safeMagnitude, null));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude, AnimationCurve curve)
    {
        Vector3 origin = _restLocalPosition;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            float falloff = 1f - t;
            if (curve != null && curve.keys.Length > 0)
            {
                falloff = curve.Evaluate(t);
            }

            float x = Random.Range(-1f, 1f) * magnitude * falloff;
            float y = Random.Range(-1f, 1f) * magnitude * falloff;
            transform.localPosition = origin + new Vector3(x, y, 0f);

            yield return null;
        }

        ResetTransform();
        _shakeRoutine = null;
    }

    private void ResetTransform()
    {
        transform.localPosition = _restLocalPosition;
    }
}
