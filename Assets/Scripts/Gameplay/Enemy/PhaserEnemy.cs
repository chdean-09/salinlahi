using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class PhaserEnemy : MonoBehaviour
{
    private const float FallbackInterval = 0.5f;
    private const float MinDuration = 0.01f;
    private const float DefaultPulseMinAlpha = 0.6f;

    [SerializeField] private Renderer[] _renderers;

    private Enemy _enemy;
    private Coroutine _toggleRoutine;
    private bool _isVisible = true;
    private SpriteRenderer[] _spriteRenderers;
    private TextMeshPro[] _textLabels;
    private Color[] _spriteBaseColors;
    private Color[] _labelBaseColors;
    private bool _hasCompletedInvisibleState;
    private bool _hasCompletedSinglePhaseCycle;

    public bool IsVisible => _isVisible;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        CacheRenderersIfMissing();
    }

    private void OnEnable()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        CacheRenderersIfMissing();
        CacheFadeTargets();
        SetVisibleImmediate();

        RefreshPhaserState();
    }

    private void OnDisable()
    {
        StopToggleRoutine();

        // Pool safety: always restore deterministic visible state when disabled.
        SetVisibleImmediate();
    }

    public void RefreshPhaserState()
    {
        StopToggleRoutine();
        _hasCompletedInvisibleState = false;
        _hasCompletedSinglePhaseCycle = false;
        SetVisibleImmediate();

        if (ShouldRunPhaser())
            _toggleRoutine = StartCoroutine(ToggleVisibilityRoutine());
    }

    private IEnumerator ToggleVisibilityRoutine()
    {
        while (ShouldRunPhaser() && !_hasCompletedSinglePhaseCycle)
        {
            float holdDuration = Mathf.Max(MinDuration, GetCurrentHoldDuration());
            yield return new WaitForSeconds(holdDuration);

            if (_isVisible)
            {
                yield return FadeOutWithPulse();
                // Damage becomes invalid only once fully invisible.
                _isVisible = false;
                _hasCompletedInvisibleState = true;
                ApplyAlpha(0f);
            }
            else
            {
                // Damage is valid while transitioning back from invisible.
                _isVisible = true;
                yield return FadeIn();
                ApplyAlpha(1f);
                _hasCompletedSinglePhaseCycle = true;
            }
        }

        // One-shot phase behavior: remain visible after first full cycle.
        _isVisible = true;
        ApplyAlpha(1f);
        _toggleRoutine = null;
    }

    private bool ShouldRunPhaser()
    {
        return _enemy != null
            && _enemy.Data != null
            && _enemy.Data.isPhaser;
    }

    private float GetInterval()
    {
        if (_enemy == null || _enemy.Data == null)
            return FallbackInterval;

        return _enemy.Data.phaserInterval > 0f
            ? _enemy.Data.phaserInterval
            : FallbackInterval;
    }

    private float GetCurrentHoldDuration()
    {
        if (_enemy == null || _enemy.Data == null)
            return FallbackInterval;

        float fallback = GetInterval();
        if (_isVisible)
        {
            float randomized = GetRandomDuration(_enemy.Data.phaserVisibleHoldMin, _enemy.Data.phaserVisibleHoldMax, fallback);
            if (!_hasCompletedInvisibleState)
                randomized = Mathf.Max(randomized, GetInitialVisibleDelay(fallback));
            return randomized;
        }

        return GetRandomDuration(_enemy.Data.phaserInvisibleHoldMin, _enemy.Data.phaserInvisibleHoldMax, fallback);
    }

    private float GetRandomDuration(float min, float max, float fallback)
    {
        float minDuration = min > 0f ? min : fallback;
        float maxDuration = max > 0f ? max : fallback;
        if (maxDuration < minDuration)
        {
            float tmp = minDuration;
            minDuration = maxDuration;
            maxDuration = tmp;
        }

        if (Mathf.Approximately(minDuration, maxDuration))
            return minDuration;

        return Random.Range(minDuration, maxDuration);
    }

    private float GetInitialVisibleDelay(float fallback)
    {
        if (_enemy == null || _enemy.Data == null)
            return fallback;

        return _enemy.Data.phaserInitialVisibleDelayMin > 0f
            ? _enemy.Data.phaserInitialVisibleDelayMin
            : fallback;
    }

    private void CacheRenderersIfMissing()
    {
        if (_renderers != null && _renderers.Length > 0)
            return;

        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private void CacheFadeTargets()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        _textLabels = GetComponentsInChildren<TextMeshPro>(includeInactive: true);

        _spriteBaseColors = new Color[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            _spriteBaseColors[i] = _spriteRenderers[i] != null
                ? _spriteRenderers[i].color
                : Color.white;
        }

        _labelBaseColors = new Color[_textLabels.Length];
        for (int i = 0; i < _textLabels.Length; i++)
        {
            _labelBaseColors[i] = _textLabels[i] != null
                ? _textLabels[i].color
                : Color.white;
        }
    }

    private void SetVisibleImmediate()
    {
        _isVisible = true;
        ApplyAlpha(1f);
    }

    private IEnumerator FadeOutWithPulse()
    {
        float duration = Mathf.Max(0f, _enemy.Data != null ? _enemy.Data.phaserFadeOutDuration : 0f);
        if (duration <= 0f)
        {
            ApplyAlpha(0f);
            yield break;
        }

        int pulseCount = _enemy.Data != null ? Mathf.Max(0, _enemy.Data.phaserFadeOutPulseCount) : 0;
        float pulseAmplitude = _enemy.Data != null
            ? Mathf.Clamp01(_enemy.Data.phaserFadeOutPulseAmplitude)
            : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Telegraph invisibility by pulsing out and back to full visibility
            // before the final invisible state is applied.
            float pulseTime = Mathf.SmoothStep(0f, 1f, t);
            float pulseWave = pulseCount > 0
                ? Mathf.Abs(Mathf.Sin(pulseTime * pulseCount * Mathf.PI))
                : 0f;
            float targetPulseMinAlpha = Mathf.Min(1f - pulseAmplitude, DefaultPulseMinAlpha);
            float alpha = Mathf.Lerp(1f, targetPulseMinAlpha, pulseWave);
            ApplyAlpha(alpha);
            yield return null;
        }
    }

    private IEnumerator FadeIn()
    {
        float duration = Mathf.Max(0f, _enemy.Data != null ? _enemy.Data.phaserFadeInDuration : 0f);
        if (duration <= 0f)
        {
            ApplyAlpha(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyAlpha(t);
            yield return null;
        }
    }

    private void ApplyAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (_renderers == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = true;
        }

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _spriteRenderers[i];
            if (sr == null) continue;
            Color baseColor = _spriteBaseColors[i];
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        }

        for (int i = 0; i < _textLabels.Length; i++)
        {
            TextMeshPro label = _textLabels[i];
            if (label == null) continue;
            Color baseColor = _labelBaseColors[i];
            label.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        }
    }

    private void StopToggleRoutine()
    {
        if (_toggleRoutine == null)
            return;

        StopCoroutine(_toggleRoutine);
        _toggleRoutine = null;
    }
}
