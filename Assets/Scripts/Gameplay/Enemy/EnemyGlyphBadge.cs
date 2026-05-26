using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class EnemyGlyphBadge : MonoBehaviour
{
    [SerializeField] private GlyphBadgeConfigSO _config;

    private Enemy _enemy;
    private SpriteRenderer _renderer;
    private Coroutine _swapRoutine;
    private Coroutine _finalDrawRoutine;
    private Coroutine _decoyRejectRoutine;
    private Coroutine _failFlashRoutine;

    private Vector3 _baseLocalPosition;
    private Vector3 _baseLocalScale;
    private Quaternion _baseLocalRotation;
    private Color _baseColor = Color.white;
    private bool _layoutApplied;
    // Cached world-space layout values from EnemyDataSO/GlyphBadgeConfigSO.
    // Used by LateUpdate to recompute the inverse-parent-scale compensation each
    // frame so the badge stays world-stable even after the parent's localScale
    // changes (e.g. boss collapse / stand-up squash-stretch).
    private Vector2 _desiredWorldOffset;
    private float _desiredWorldScale = 1f;

    public GlyphBadgeConfigSO Config => _config;
    public bool IsSwapping => _swapRoutine != null;
    public bool IsPlayingFinalDraw => _finalDrawRoutine != null;
    public bool IsPlayingDecoyReject => _decoyRejectRoutine != null;

    private void Awake()
    {
        _enemy = GetComponentInParent<Enemy>();
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer != null)
        {
            _renderer.sortingOrder = RenderOrder.EnemyGlyphBadge;
            _baseColor = _renderer.color;
        }
        _baseLocalPosition = transform.localPosition;
        _baseLocalScale = transform.localScale;
        _baseLocalRotation = transform.localRotation;
    }

    private void OnDisable()
    {
        ResetForPool();
    }

    /// <summary>
    /// Apply layout (offset + scale) from EnemyDataSO override or GlyphBadgeConfigSO default.
    /// Called from Enemy.Initialize after the enemy data is bound.
    /// </summary>
    public void ApplyLayout()
    {
        if (_enemy == null || _enemy.Data == null || _config == null) return;
        EnemyDataSO d = _enemy.Data;
        _desiredWorldOffset = d.overrideBadgeOffset ? d.glyphBadgeOffsetOverride : _config.defaultWorldOffset;
        _desiredWorldScale = d.overrideBadgeScale ? d.glyphBadgeScaleOverride : _config.defaultWorldScale;
        _baseLocalRotation = Quaternion.identity;
        _layoutApplied = true;
        RecomputeBaseFromParentScale(forceApplyTransform: true);
    }

    /// <summary>
    /// Recompute the base local position/scale from the cached desired world
    /// values and the parent's current lossyScale. Called from LateUpdate so the
    /// badge stays world-stable even when the parent's localScale changes after
    /// the initial layout (e.g. boss collapse squashes the boss sprite y-scale).
    /// When an animation routine is in flight, only the base values are
    /// refreshed; the transform is not overwritten so the coroutine retains
    /// ownership of localPosition/localScale.
    /// </summary>
    public void RecomputeBaseFromParentScale(bool forceApplyTransform = false)
    {
        if (!_layoutApplied) return;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float invX = InverseOrOne(parentScale.x);
        float invY = InverseOrOne(parentScale.y);

        _baseLocalPosition = new Vector3(_desiredWorldOffset.x * invX, _desiredWorldOffset.y * invY, 0f);
        _baseLocalScale = new Vector3(_desiredWorldScale * invX, _desiredWorldScale * invY, 1f);

        if (forceApplyTransform || (!IsSwapping && !IsPlayingFinalDraw && !IsPlayingDecoyReject))
        {
            transform.localPosition = _baseLocalPosition;
            transform.localScale = _baseLocalScale;
            transform.localRotation = _baseLocalRotation;
        }
    }

    private void LateUpdate()
    {
        if (!_layoutApplied) return;
        RecomputeBaseFromParentScale();
    }

    public void Refresh()
    {
        if (_swapRoutine != null) return;
        if (_enemy == null) return;
        SetCharacter(_enemy.VisualCharacter);
    }

    public void SetCharacter(BaybayinCharacterSO ch)
    {
        Sprite sprite = ResolveSprite(ch);
        if (_renderer == null) return;
        if (sprite == null)
        {
            _renderer.enabled = false;
            return;
        }
        _renderer.sprite = sprite;
        _renderer.enabled = true;
    }

    public void PlaySwap(BaybayinCharacterSO next)
    {
        if (!isActiveAndEnabled || _config == null) return;
        if (_swapRoutine != null) StopCoroutine(_swapRoutine);
        _swapRoutine = StartCoroutine(SwapRoutine(next));
    }

    public void PlayFinalDraw()
    {
        if (!isActiveAndEnabled || _config == null) return;
        if (_swapRoutine != null) { StopCoroutine(_swapRoutine); _swapRoutine = null; }
        if (_finalDrawRoutine != null) StopCoroutine(_finalDrawRoutine);
        _finalDrawRoutine = StartCoroutine(FinalDrawRoutine());
    }

    public IEnumerator PlayDecoyReject()
    {
        if (!isActiveAndEnabled || _config == null) yield break;
        if (_swapRoutine != null) { StopCoroutine(_swapRoutine); _swapRoutine = null; }
        if (_decoyRejectRoutine != null) StopCoroutine(_decoyRejectRoutine);
        _decoyRejectRoutine = StartCoroutine(DecoyRejectRoutine());
        yield return _decoyRejectRoutine;
    }

    public void PlayFailFlash()
    {
        if (!isActiveAndEnabled || _config == null) return;
        if (_failFlashRoutine != null) StopCoroutine(_failFlashRoutine);
        _failFlashRoutine = StartCoroutine(FailFlashRoutine());
    }

    public void Show()
    {
        if (_renderer == null) return;
        Color c = _renderer.color; c.a = 1f; _renderer.color = c;
        _renderer.enabled = _renderer.sprite != null;
    }

    public void Hide()
    {
        if (_swapRoutine != null) { StopCoroutine(_swapRoutine); _swapRoutine = null; }
        if (_finalDrawRoutine != null) { StopCoroutine(_finalDrawRoutine); _finalDrawRoutine = null; }
        if (_renderer == null) return;
        Color c = _renderer.color; c.a = 0f; _renderer.color = c;
    }

    public void ResetForPool()
    {
        StopAllCoroutines();
        _swapRoutine = null;
        _finalDrawRoutine = null;
        _decoyRejectRoutine = null;
        _failFlashRoutine = null;
        if (_renderer != null)
        {
            Color c = _baseColor; c.a = 1f; _renderer.color = c;
            _renderer.enabled = false;
        }
        transform.localPosition = _baseLocalPosition;
        transform.localScale = _baseLocalScale;
        transform.localRotation = _baseLocalRotation;
    }

    private Sprite ResolveSprite(BaybayinCharacterSO character)
    {
        if (character == null) return null;
        bool useScrambled = _enemy != null
                            && _enemy.HasVisualCharacterOverride
                            && character.scrambledBadgeSprite != null;
        return useScrambled ? character.scrambledBadgeSprite : character.badgeSprite;
    }

    private IEnumerator SwapRoutine(BaybayinCharacterSO next)
    {
        Vector3 startPos = _baseLocalPosition;
        Vector3 outPos = _baseLocalPosition + (Vector3)_config.swapSlideOffset;
        float t = 0f;
        while (t < _config.swapOutDuration)
        {
            t += Time.deltaTime;
            float u = _config.swapOutDuration > 0f ? Mathf.Clamp01(t / _config.swapOutDuration) : 1f;
            transform.localPosition = Vector3.Lerp(startPos, outPos, u);
            SetAlpha(1f - u);
            yield return null;
        }
        SetCharacter(next);
        Vector3 inStart = _baseLocalPosition - (Vector3)_config.swapSlideOffset;
        transform.localPosition = inStart;
        t = 0f;
        while (t < _config.swapInDuration)
        {
            t += Time.deltaTime;
            float u = _config.swapInDuration > 0f ? Mathf.Clamp01(t / _config.swapInDuration) : 1f;
            transform.localPosition = Vector3.Lerp(inStart, _baseLocalPosition, u);
            SetAlpha(u);
            yield return null;
        }
        transform.localPosition = _baseLocalPosition;
        SetAlpha(1f);
        _swapRoutine = null;
    }

    private IEnumerator FinalDrawRoutine()
    {
        Vector3 startScale = _baseLocalScale;
        Vector3 peakScale = _baseLocalScale * _config.finalDrawChargeScale;
        Color originalColor = _renderer != null ? _renderer.color : Color.white;
        float t = 0f;
        while (t < _config.finalDrawChargeDuration)
        {
            t += Time.deltaTime;
            float u = _config.finalDrawChargeDuration > 0f ? Mathf.Clamp01(t / _config.finalDrawChargeDuration) : 1f;
            transform.localScale = Vector3.Lerp(startScale, peakScale, u);
            if (_renderer != null) _renderer.color = Color.Lerp(originalColor, _config.finalDrawFlashColor, u);
            yield return null;
        }
        Vector3 startPos = _baseLocalPosition;
        Vector3 endPos = startPos + new Vector3(0f, _config.finalDrawReleaseRise, 0f);
        Quaternion startRot = _baseLocalRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, 0, _config.finalDrawReleaseRotation);
        t = 0f;
        while (t < _config.finalDrawReleaseDuration)
        {
            t += Time.deltaTime;
            float u = _config.finalDrawReleaseDuration > 0f ? Mathf.Clamp01(t / _config.finalDrawReleaseDuration) : 1f;
            transform.localScale = Vector3.Lerp(peakScale, Vector3.zero, u);
            transform.localPosition = Vector3.Lerp(startPos, endPos, u);
            transform.localRotation = Quaternion.Slerp(startRot, endRot, u);
            SetAlpha(1f - u);
            yield return null;
        }
        if (_renderer != null) _renderer.enabled = false;
        _finalDrawRoutine = null;
    }

    private IEnumerator DecoyRejectRoutine()
    {
        Color originalColor = _renderer != null ? _renderer.color : Color.white;
        float t = 0f;
        while (t < _config.decoyRejectFlashDuration)
        {
            t += Time.deltaTime;
            if (_renderer != null) _renderer.color = _config.decoyRejectFlashColor;
            yield return null;
        }
        if (_renderer != null) _renderer.color = originalColor;
        Vector3 basePos = _baseLocalPosition;
        t = 0f;
        while (t < _config.decoyRejectShakeDuration)
        {
            t += Time.deltaTime;
            float wave = Mathf.Sin(t * _config.decoyRejectShakeFrequency * Mathf.PI * 2f);
            float decay = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, _config.decoyRejectShakeDuration));
            transform.localPosition = basePos + new Vector3(wave * _config.decoyRejectShakeMagnitude * decay, 0f, 0f);
            yield return null;
        }
        transform.localPosition = basePos;
        _decoyRejectRoutine = null;
    }

    private IEnumerator FailFlashRoutine()
    {
        Color originalColor = _renderer != null ? _renderer.color : Color.white;
        if (_renderer != null) _renderer.color = _config.failFlashColor;
        yield return new WaitForSeconds(_config.failFlashDuration);
        if (_renderer != null) _renderer.color = originalColor;
        _failFlashRoutine = null;
    }

    private void SetAlpha(float a)
    {
        if (_renderer == null) return;
        Color c = _renderer.color; c.a = a; _renderer.color = c;
    }

    private static float InverseOrOne(float v) => Mathf.Approximately(v, 0f) ? 1f : 1f / v;
}
