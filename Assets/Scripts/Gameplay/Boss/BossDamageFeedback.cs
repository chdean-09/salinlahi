using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BossController))]
[RequireComponent(typeof(SpriteRenderer))]
public class BossDamageFeedback : MonoBehaviour
{
    [Header("Hit Flash")]
    [SerializeField] private Color _flashColor = Color.white;

    [Header("Shake and Pause")]
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _shakeFrequency = 20f;
    [SerializeField] private float _shakeMagnitude = 0.1f;
    [SerializeField] private float _pauseDuration = 0.2f;

    [Header("Progressive Damage Tint")]
    [SerializeField] private Color _healthyColor = Color.white;
    [SerializeField] private Color _criticalColor = new Color(1f, 0.5f, 0.5f, 1f); // Pale red tint

    private BossController _boss;
    private SpriteRenderer _renderer;
    private Coroutine _hitFeedbackRoutine;
    private Vector3 _appliedShake;

    public bool IsHurtPaused { get; private set; }

    private void Awake()
    {
        _boss = GetComponent<BossController>();
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        _boss.OnDrawnThisPhaseChanged += HandleDrawnThisPhaseChanged;
        EventBus.OnBossPhaseAdsReturning += HandlePhaseReset;
    }

    private void OnDisable()
    {
        _boss.OnDrawnThisPhaseChanged -= HandleDrawnThisPhaseChanged;
        EventBus.OnBossPhaseAdsReturning -= HandlePhaseReset;
        
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

    private void HandleDrawnThisPhaseChanged()
    {
        UpdateProgressiveTint();
        
        // Match EnemyHurtFeedback: if already hurting, ignore the second hit's feedback
        if (_hitFeedbackRoutine != null) return;
        
        _hitFeedbackRoutine = StartCoroutine(PlayHitFeedback());
    }

    private void HandlePhaseReset(int phaseIndex)
    {
        UpdateProgressiveTint();
    }

    private void UpdateProgressiveTint()
    {
        if (_boss.Config == null || _boss.Config.phases == null) return;

        int totalRequired = 0;
        int currentDrawn = 0;

        for (int i = 0; i < _boss.Config.phases.Count; i++)
        {
            int phaseReqCount = CountNonNull(_boss.Config.phases[i].requiredCharacters);
            totalRequired += phaseReqCount;

            if (i < _boss.CurrentPhaseIndex)
            {
                currentDrawn += phaseReqCount;
            }
            else if (i == _boss.CurrentPhaseIndex)
            {
                currentDrawn += _boss.DrawnThisPhase.Count;
            }
        }

        if (totalRequired <= 0) return;

        float healthRatio = 1f - Mathf.Clamp01((float)currentDrawn / totalRequired);
        
        // Only set color instantly if we're not flashing
        if (_hitFeedbackRoutine == null)
        {
            _renderer.color = Color.Lerp(_criticalColor, _healthyColor, healthRatio);
        }
    }

    private Color GetCurrentTint()
    {
        if (_boss.Config == null || _boss.Config.phases == null) return _healthyColor;

        int totalRequired = 0;
        int currentDrawn = 0;

        for (int i = 0; i < _boss.Config.phases.Count; i++)
        {
            int phaseReqCount = CountNonNull(_boss.Config.phases[i].requiredCharacters);
            totalRequired += phaseReqCount;

            if (i < _boss.CurrentPhaseIndex)
            {
                currentDrawn += phaseReqCount;
            }
            else if (i == _boss.CurrentPhaseIndex)
            {
                currentDrawn += _boss.DrawnThisPhase.Count;
            }
        }

        if (totalRequired <= 0) return _healthyColor;

        float healthRatio = 1f - Mathf.Clamp01((float)currentDrawn / totalRequired);
        return Color.Lerp(_criticalColor, _healthyColor, healthRatio);
    }

    private IEnumerator PlayHitFeedback()
    {
        float totalDur = Mathf.Max(_pauseDuration, _shakeDuration);
        
        _appliedShake = Vector3.zero;
        IsHurtPaused = true;
        _renderer.color = _flashColor;
        
        float t = 0f;
        while (t < totalDur)
        {
            // Flash color only lasts for the shake duration
            if (t > _shakeDuration) 
            {
                _renderer.color = GetCurrentTint();
            }

            if (_shakeDuration > 0f)
            {
                transform.position -= _appliedShake;
                
                if (t < _shakeDuration)
                {
                    float angle = t * _shakeFrequency * Mathf.PI * 2f;
                    float decay = 1f - Mathf.Clamp01(t / _shakeDuration);
                    Vector3 next = new Vector3(
                        Mathf.Sin(angle) * _shakeMagnitude * decay,
                        Mathf.Cos(angle * 1.7f) * _shakeMagnitude * decay,
                        0f);
                    transform.position += next;
                    _appliedShake = next;
                }
                else
                {
                    _appliedShake = Vector3.zero;
                }
            }

            if (t >= _pauseDuration)
            {
                IsHurtPaused = false;
            }

            yield return null;
            t += Time.deltaTime;
        }

        ClearShakeOffset();
        IsHurtPaused = false;
        _renderer.color = GetCurrentTint();
        _hitFeedbackRoutine = null;
    }

    private static int CountNonNull(List<BaybayinCharacterSO> list)
    {
        if (list == null) return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) n++;
        return n;
    }
}
