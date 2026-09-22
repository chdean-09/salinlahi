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
    // Takip: while covered the badge keeps its sprite but stays hidden (GlyphCoverController).
    private bool _covered;
    // SALIN-286: while blocked the badge stays visible but dimmed. See SetResolutionBlocked.
    private bool _resolutionBlocked;
    // Cached world-space layout values from EnemyDataSO/GlyphBadgeConfigSO.
    // Used by LateUpdate to recompute the inverse-parent-scale compensation each
    // frame so the badge stays world-stable even after the parent's localScale
    // changes (e.g. boss collapse / stand-up squash-stretch).
    private Vector2 _desiredWorldOffset;
    private float _desiredWorldScale = 1f;
    // True while the badge is showing a glyph outline because no scroll badge art exists for this
    // symbol. The outlines are 256 px square against roughly 125 px badge art at the same PPU, so
    // an outline shown at the authored badge scale renders about twice the intended size.
    private bool _usingOutlineFallback;
    private const float OutlineFallbackScale = 125f / 256f;

    // True while the badge is showing a FALSE face: a visual character override used by a
    // deliberate decoy, rather than the enemy's own symbol. Mantsa no longer takes this path: its
    // stain is presentation-only and keeps the stable glyph visible under ink.
    private bool _showingFalseGlyph;
    // True while Mantsa's ink obscures a stable glyph. This deliberately changes presentation
    // only; the badge continues to resolve against the enemy's real character.
    private bool _stained;
    // Alpha the swap/fade/final-draw coroutines own. The false-glyph dim multiplies it instead of
    // overwriting it, so the two never fight. See GlyphStainCycle.ResolveBadgeAlpha.
    private float _routineAlpha = 1f;
    // Hue a flash routine (final draw / decoy reject / fail) has temporarily seized. Kept separate
    // from the composed colour so a flash still survives the release fade's SetAlpha calls, the way
    // it did when every routine wrote _renderer.color directly.
    private Color? _flashTint;

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

        float scale = _usingOutlineFallback
            ? _desiredWorldScale * OutlineFallbackScale
            : _desiredWorldScale;
        _baseLocalScale = new Vector3(scale * invX, scale * invY, 1f);

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
        _renderer.enabled = !_covered;

        _showingFalseGlyph = _enemy != null
                             && _enemy.HasVisualCharacterOverride
                             && GlyphStainCycle.IsFalseGlyph(ch != null ? ch.characterID : null,
                                                             _enemy.Character != null ? _enemy.Character.characterID : null);
        ApplyBadgeColor();

        // Whether the fallback is in use depends on which sprite just resolved, so redo the layout.
        RecomputeBaseFromParentScale();
    }

    public bool IsCovered => _covered;
    public bool IsStained => _stained;

    /// <summary>
    /// Applies Mantsa's stain without replacing the real Baybayin glyph. The player must read a
    /// partially obscured form or remove the source of the ink, never learn a polished false form.
    /// </summary>
    public void SetStained(bool stained)
    {
        if (_stained == stained)
            return;

        _stained = stained;
        ApplyBadgeColor();
    }

    /// <summary>
    /// Whether the badge is actually readable on screen: a renderer that exists, is enabled, and is
    /// not fully transparent. <see cref="Hide"/> works by alpha rather than by disabling, so
    /// checking <c>enabled</c> alone reports a hidden badge as visible.
    /// </summary>
    public bool IsVisible =>
        _renderer != null && _renderer.enabled && _renderer.color.a > 0.01f;

    /// <summary>
    /// Hides or reveals the badge without touching its sprite or colour, so a covered enemy still
    /// swaps, flashes and resolves its glyph normally underneath the cover.
    /// </summary>
    public void SetCovered(bool covered)
    {
        if (_covered == covered) return;
        _covered = covered;
        if (_renderer == null) return;
        _renderer.enabled = !_covered && _renderer.sprite != null;
    }

    /// <summary>
    /// ========================================================================
    /// SALIN-286 — PLACEHOLDER BLOCKED-STATE VISUAL. NOT ART-APPROVED.
    /// ========================================================================
    /// A blocked enemy must carry a visible tell, or it reads as a bug: the player draws its
    /// symbol and nothing happens, with no explanation. No blocked-state art is authored —
    /// <see cref="GlyphBadgeConfigSO"/> has no such field and Assets/Art/UI/GlyphBadges/ holds no
    /// blocked badge — so this derives the minimum honest tell from means that already exist:
    /// the badge stays fully visible and is dimmed toward transparent-grey.
    /// <para>
    /// Deliberately NOT <see cref="SetCovered"/>: hiding the badge would make a blocked enemy read
    /// as a Takip cover, which is a different ability with a different remedy.
    /// </para>
    /// <para>
    /// Only the colour channel is touched, never <c>enabled</c> or alpha, so this composes with
    /// cover, <see cref="Show"/>/<see cref="Hide"/> and the swap/flash routines rather than
    /// fighting them. Replace the tint with authored art when it lands; the call sites do not
    /// change.
    /// </para>
    /// </summary>
    public void SetResolutionBlocked(bool blocked)
    {
        if (_resolutionBlocked == blocked) return;
        _resolutionBlocked = blocked;
        ApplyResolutionBlockTint();
    }

    public bool IsResolutionBlocked => _resolutionBlocked;

    /// <summary>Placeholder dim applied to a blocked badge. Replace with authored art.</summary>
    private static readonly Color BlockedTint = new Color(0.45f, 0.45f, 0.5f, 1f);
    private static readonly Color StainedTint = new Color(0.62f, 0.42f, 0.22f, 1f);

    private void ApplyResolutionBlockTint() => ApplyBadgeColor();

    /// <summary>
    /// Single owner of the badge's colour. Composes, in order: the authored base colour, the
    /// blocked-state hue multiply, the false-glyph hue multiply, and finally the alpha the
    /// animation routines own scaled by the false-glyph dim. Every tell multiplies rather than
    /// assigns, so blocked + stained + mid-swap all read at once instead of clobbering each other.
    /// </summary>
    private void ApplyBadgeColor()
    {
        if (_renderer == null) return;

        Color tint = _flashTint ?? _baseColor;
        if (_resolutionBlocked && !_flashTint.HasValue)
            tint = new Color(tint.r * BlockedTint.r, tint.g * BlockedTint.g, tint.b * BlockedTint.b, tint.a);

        if (_stained && !_flashTint.HasValue)
            tint = new Color(tint.r * StainedTint.r, tint.g * StainedTint.g, tint.b * StainedTint.b, tint.a);

        float falseAlpha = _config != null ? _config.falseGlyphAlpha : GlyphStainCycle.DefaultFalseGlyphAlpha;
        // The dim always applies while a false face is up; the hue yields to an in-flight flash so
        // a final-draw / reject flash still reads as itself.
        if (_showingFalseGlyph && !_flashTint.HasValue)
        {
            Color falseTint = _config != null ? _config.falseGlyphTint : Color.white;
            tint = new Color(tint.r * falseTint.r, tint.g * falseTint.g, tint.b * falseTint.b, tint.a);
        }

        tint.a = GlyphStainCycle.ResolveBadgeAlpha(_routineAlpha, _showingFalseGlyph, falseAlpha);
        _renderer.color = tint;
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
        _routineAlpha = 1f;
        ApplyBadgeColor();
        _renderer.enabled = _renderer.sprite != null && !_covered;
    }

    public void Hide()
    {
        if (_swapRoutine != null) { StopCoroutine(_swapRoutine); _swapRoutine = null; }
        if (_finalDrawRoutine != null) { StopCoroutine(_finalDrawRoutine); _finalDrawRoutine = null; }
        if (_renderer == null) return;
        _routineAlpha = 0f;
        ApplyBadgeColor();
    }

    public void ResetForPool()
    {
        StopAllCoroutines();
        _swapRoutine = null;
        _finalDrawRoutine = null;
        _decoyRejectRoutine = null;
        _failFlashRoutine = null;
        _covered = false;
        // Pool safety: a badge that left play dimmed must not come back dimmed.
        _resolutionBlocked = false;
        _stained = false;
        // Pool safety: a badge that left play wearing a false face must not come back wearing one.
        _showingFalseGlyph = false;
        _routineAlpha = 1f;
        _flashTint = null;
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
        if (useScrambled) return character.scrambledBadgeSprite;

        // Scroll badge art exists for seven of the eighteen symbols (Art/UI/GlyphBadges holds BA,
        // DA, HA, KA, O, SA and WA, from SALIN-97/98). The rest have a null badgeSprite, and a null
        // sprite makes SetCharacter disable the renderer - so those enemies walked down carrying no
        // glyph at all and the player had nothing to read. Abo ng Simula is the visible case, since
        // it carries symbol.a. Fall back to the authored glyph outline until the missing scroll art
        // lands; every symbol has one. Delete this fallback once all eighteen badges exist.
        if (character.badgeSprite != null)
        {
            _usingOutlineFallback = false;
            return character.badgeSprite;
        }

        _usingOutlineFallback = character.glyphOutlineSprite != null;
        return character.glyphOutlineSprite;
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
            SetFlashTint(Color.Lerp(originalColor, _config.finalDrawFlashColor, u));
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
        SetFlashTint(null);
        _finalDrawRoutine = null;
    }

    private IEnumerator DecoyRejectRoutine()
    {
        float t = 0f;
        SetFlashTint(_config.decoyRejectFlashColor);
        while (t < _config.decoyRejectFlashDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }
        SetFlashTint(null);
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
        SetFlashTint(_config.failFlashColor);
        yield return new WaitForSeconds(_config.failFlashDuration);
        SetFlashTint(null);
        _failFlashRoutine = null;
    }

    /// <summary>Seizes (or releases, with null) the badge hue for a flash routine.</summary>
    private void SetFlashTint(Color? tint)
    {
        _flashTint = tint;
        ApplyBadgeColor();
    }

    private void SetAlpha(float a)
    {
        _routineAlpha = a;
        ApplyBadgeColor();
    }

    private static float InverseOrOne(float v) => Mathf.Approximately(v, 0f) ? 1f : 1f / v;
}
