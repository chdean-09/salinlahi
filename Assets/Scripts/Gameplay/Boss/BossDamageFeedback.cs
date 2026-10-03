using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BossController))]
[RequireComponent(typeof(SpriteRenderer))]
public class BossDamageFeedback : MonoBehaviour
{
    [Header("Hit Flash")]
    [SerializeField] private Color _flashColor = Color.white;

    [Header("Shake")]
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _shakeFrequency = 20f;
    [SerializeField] private float _shakeMagnitude = 0.1f;

    [Header("Progressive Damage Tint")]
    [SerializeField] private Color _healthyColor = Color.white;
    [SerializeField] private Color _criticalColor = new Color(1f, 0.5f, 0.5f, 1f); // Pale red tint

    private BossController _boss;
    private SpriteRenderer _renderer;
    private Coroutine _hitFeedbackRoutine;
    private Vector3 _appliedShake;

    public bool IsHurtPaused { get; private set; }

    // Exposed for BossStateVisuals to read the critical tint color.
    public Color CriticalColor => _criticalColor;

    private void Awake()
    {
        _boss = GetComponent<BossController>();
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        _boss.OnDrawnThisPhaseChanged += HandleSmallHit;
        EventBus.OnBossDamaged += HandleEmphasizedDamage;
    }

    private void OnDisable()
    {
        _boss.OnDrawnThisPhaseChanged -= HandleSmallHit;
        EventBus.OnBossDamaged -= HandleEmphasizedDamage;
        ResetState();
    }

    private void ResetState()
    {
        if (_hitFeedbackRoutine != null)
        {
            StopCoroutine(_hitFeedbackRoutine);
            _hitFeedbackRoutine = null;
        }
        ClearShakeOffset();
        _renderer.color = _healthyColor;
        IsHurtPaused = false;
    }

    private void ClearShakeOffset()
    {
        if (_appliedShake == Vector3.zero) return;
        transform.position -= _appliedShake;
        _appliedShake = Vector3.zero;
    }

    // Small flash + tiny shake fired on each correct draw (OnDrawnThisPhaseChanged).
    private void HandleSmallHit()
    {
        if (_hitFeedbackRoutine != null) return; // ignore double-hits
        _hitFeedbackRoutine = StartCoroutine(PlayHitFeedback(
            flashDuration: 0.06f,
            shakeMagnitude: _shakeMagnitude * 0.4f,
            shakeDuration: _shakeDuration * 0.5f,
            screenTimeDip: 0f));
    }

    // Large white flash + hard shake + time-scale dip fired when the boss loses an HP (OnBossDamaged).
    private void HandleEmphasizedDamage(int phaseIndex, int hpRemaining)
    {
        if (_hitFeedbackRoutine != null)
        {
            StopCoroutine(_hitFeedbackRoutine);
            _hitFeedbackRoutine = null;
        }
        _hitFeedbackRoutine = StartCoroutine(PlayHitFeedback(
            flashDuration: 0.2f,
            shakeMagnitude: _shakeMagnitude * 1.5f,
            shakeDuration: _shakeDuration * 1.5f,
            screenTimeDip: 0.3f));
    }

    // Used by BossStateVisuals.PlayCollapse to play the white flash only,
    // without shake/pause (those are owned by the collapse one-shot).
    public void PlaySmallFlashOnly(float duration)
    {
        StartCoroutine(FlashOnly(duration));
    }

    private IEnumerator FlashOnly(float duration)
    {
        Color before = _renderer.color;
        _renderer.color = _flashColor;
        yield return new WaitForSeconds(duration);
        _renderer.color = before;
    }

    private IEnumerator PlayHitFeedback(float flashDuration, float shakeMagnitude, float shakeDuration, float screenTimeDip)
    {
        float totalDur = Mathf.Max(flashDuration, shakeDuration);

        _appliedShake = Vector3.zero;
        IsHurtPaused = true;
        _renderer.color = _flashColor;

        if (screenTimeDip > 0f)
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f - screenTimeDip;
            yield return new WaitForSecondsRealtime(0.15f);  // SCALED-TIME EXCEPTION
            Time.timeScale = previousTimeScale;
        }

        float t = 0f;
        while (t < totalDur)
        {
            // Flash color only lasts for the flash duration
            if (t > flashDuration)
            {
                _renderer.color = _healthyColor;
            }

            if (shakeDuration > 0f)
            {
                transform.position -= _appliedShake;

                if (t < shakeDuration)
                {
                    float angle = t * _shakeFrequency * Mathf.PI * 2f;
                    float decay = 1f - Mathf.Clamp01(t / shakeDuration);
                    Vector3 next = new Vector3(
                        Mathf.Sin(angle) * shakeMagnitude * decay,
                        Mathf.Cos(angle * 1.7f) * shakeMagnitude * decay,
                        0f);
                    transform.position += next;
                    _appliedShake = next;
                }
                else
                {
                    _appliedShake = Vector3.zero;
                }
            }

            yield return null;
            t += Time.deltaTime;
        }

        ClearShakeOffset();
        IsHurtPaused = false;
        _renderer.color = _healthyColor;
        _hitFeedbackRoutine = null;
    }
}
