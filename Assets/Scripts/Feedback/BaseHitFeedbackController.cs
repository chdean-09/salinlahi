using System.Collections;
using UnityEngine;

public sealed class BaseHitFeedbackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraShakeController _cameraShake;
    [SerializeField] private Transform _baseVisualRoot;
    [SerializeField] private DamageEdgeFlashController _edgeFlash;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _baseHitClip;

    [Header("Base Wobble")]
    [SerializeField] private bool _enableBaseWobble = true;
    [SerializeField] private Vector3 _wobbleOffset = new Vector3(0f, -0.05f, 0f);
    [SerializeField] private float _wobbleDuration = 0.16f;

    private Coroutine _wobbleRoutine;
    private Vector3 _baseVisualOrigin;

    private void Awake()
    {
        if (_baseVisualRoot == null && _enableBaseWobble)
        {
            DebugLogger.LogWarning("BaseHitFeedbackController: Base wobble is enabled, but _baseVisualRoot is not assigned. Wobble feedback will be skipped.");
            _enableBaseWobble = false;
        }
    }

    private void Start()
    {
        CacheBaseVisualOrigin();
    }

    private void OnEnable()
    {
        EventBus.OnBaseHit += HandleBaseHit;
    }

    private void OnDisable()
    {
        EventBus.OnBaseHit -= HandleBaseHit;

        if (_wobbleRoutine != null)
        {
            StopCoroutine(_wobbleRoutine);
            _wobbleRoutine = null;
        }

        ResetBaseVisual();
    }

    private void HandleBaseHit(int _)
    {
        _cameraShake?.Shake();
        _edgeFlash?.Flash();

        if (_audioSource != null && _baseHitClip != null)
            _audioSource.PlayOneShot(_baseHitClip);

        if (_enableBaseWobble && _baseVisualRoot != null)
        {
            CacheBaseVisualOrigin();

            if (_wobbleRoutine != null)
                StopCoroutine(_wobbleRoutine);

            _wobbleRoutine = StartCoroutine(WobbleRoutine());
        }
    }

    private IEnumerator WobbleRoutine()
    {
        float duration = Mathf.Max(0.05f, _wobbleDuration);
        float elapsed = 0f;
        Vector3 origin = _baseVisualOrigin;
        Vector3 hitPosition = origin + _wobbleOffset;
        Vector3 overshoot = origin - (_wobbleOffset * 0.35f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (t < 0.45f)
            {
                float phase = t / 0.45f;
                _baseVisualRoot.localPosition = Vector3.LerpUnclamped(origin, hitPosition, phase);
            }
            else
            {
                float phase = (t - 0.45f) / 0.55f;
                if (phase < 0.45f)
                {
                    float overshootPhase = phase / 0.45f;
                    _baseVisualRoot.localPosition = Vector3.LerpUnclamped(hitPosition, overshoot, overshootPhase);
                }
                else
                {
                    float settlePhase = (phase - 0.45f) / 0.55f;
                    _baseVisualRoot.localPosition = Vector3.LerpUnclamped(overshoot, origin, settlePhase);
                }
            }

            yield return null;
        }

        ResetBaseVisual();
        _wobbleRoutine = null;
    }

    private void ResetBaseVisual()
    {
        if (_baseVisualRoot != null)
            _baseVisualRoot.localPosition = _baseVisualOrigin;
    }

    private void CacheBaseVisualOrigin()
    {
        if (_baseVisualRoot != null)
            _baseVisualOrigin = _baseVisualRoot.localPosition;
    }
}
