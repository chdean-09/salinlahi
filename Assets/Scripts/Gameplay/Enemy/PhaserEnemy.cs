using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class PhaserEnemy : MonoBehaviour
{
    private const float FallbackInterval = 0.5f;
    private const float MinDuration = 0.01f;

    [SerializeField] private Renderer[] _renderers;

    private Enemy _enemy;
    private Coroutine _toggleRoutine;
    private bool _isVisible = true;
    private SpriteRenderer[] _spriteRenderers;
    private TextMeshPro[] _textLabels;
    private Color[] _spriteBaseColors;
    private Color[] _labelBaseColors;

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
        SetVisibleImmediate();

        if (ShouldRunPhaser())
            _toggleRoutine = StartCoroutine(ToggleVisibilityRoutine());
    }

    private IEnumerator ToggleVisibilityRoutine()
    {
        while (ShouldRunPhaser())
        {
            float holdDuration = Mathf.Max(MinDuration, GetCurrentHoldDuration());
            yield return new WaitForSeconds(holdDuration);

            if (_isVisible)
            {
                yield return FadeOutWithPulse();
                // Damage becomes invalid only once fully invisible.
                _isVisible = false;
                ApplyAlpha(0f);
            }
            else
            {
                // Damage is valid while transitioning back from invisible.
                _isVisible = true;
                yield return FadeIn();
                ApplyAlpha(1f);
            }
        }

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
            return _enemy.Data.phaserVisibleDuration > 0f
                ? _enemy.Data.phaserVisibleDuration
                : fallback;
        }

        return _enemy.Data.phaserInvisibleDuration > 0f
            ? _enemy.Data.phaserInvisibleDuration
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
            float baseAlpha = 1f - t;
            float pulse = pulseCount > 0
                ? Mathf.Sin(t * pulseCount * Mathf.PI * 2f) * pulseAmplitude * baseAlpha
                : 0f;
            float alpha = Mathf.Clamp01(baseAlpha + pulse);
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
