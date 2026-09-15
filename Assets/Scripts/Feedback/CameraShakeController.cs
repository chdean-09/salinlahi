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
    private AspectLockedCamera _subscribedColumn;

    private void Awake()
    {
        _restLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        SubscribeToPlayColumn();
        RebaseRestPosition();
    }

    // Again in Start, because this component usually lives on the camera itself and Awake order
    // within one GameObject is not ours to choose: OnEnable can run before AspectLockedCamera has
    // published its instance. Start cannot.
    private void Start()
    {
        SubscribeToPlayColumn();
        RebaseRestPosition();
    }

    private void SubscribeToPlayColumn()
    {
        AspectLockedCamera playColumn = AspectLockedCamera.Instance;
        if (playColumn == null || playColumn == _subscribedColumn)
            return;

        if (_subscribedColumn != null)
            _subscribedColumn.OnPlayAreaChanged -= RebaseRestPosition;

        playColumn.OnPlayAreaChanged += RebaseRestPosition;
        _subscribedColumn = playColumn;
    }

    private void OnDisable()
    {
        if (_subscribedColumn != null)
        {
            _subscribedColumn.OnPlayAreaChanged -= RebaseRestPosition;
            _subscribedColumn = null;
        }

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        ResetTransform();
    }

    /// <summary>
    /// Re-reads where the camera rests.
    ///
    /// <para>
    /// Every shake begins by snapping the camera back to this position, so a rest position captured
    /// in Awake outranks anything that moves the camera afterwards: the HUD band at the foot of the
    /// screen lowers the camera so the restoration rail clears the fence, and the first shake of the
    /// level — the first hit on the base — silently put the play field back on top of the rail and
    /// left it there. Found in a play session, not in a test: nothing shakes the camera until
    /// something is hit.
    /// </para>
    /// </summary>
    private void RebaseRestPosition()
    {
        if (_shakeRoutine != null)
            return;

        _restLocalPosition = transform.localPosition;
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
        _restLocalPosition = transform.localPosition;
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
        _restLocalPosition = transform.localPosition;
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
            
            float falloff = curve != null ? curve.Evaluate(t) : (1f - t);

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
