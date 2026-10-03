using System.Collections;
using UnityEngine;

// Non-damage visual treatments for the new state machine:
//   - Panting bob + red tint during WindingDown and the Vulnerable active window.
//   - Collapse one-shot (~0.3s) when entering Vulnerable.
//   - Stand-up tween (~0.3s) when exiting Vulnerable (Damaged or timeout).
// All visuals reuse existing sprite sheets — no new art (spec §9).
[RequireComponent(typeof(BossController))]
[RequireComponent(typeof(SpriteRenderer))]
public class BossStateVisuals : MonoBehaviour
{
    [Header("Panting Bob")]
    [SerializeField] private float _bobAmplitude = 0.05f;
    [SerializeField] private float _bobHalfAmplitudeVulnerable = 0.025f;
    [SerializeField] private float _bobFrequency = 1.5f;
    [SerializeField, Range(0f, 1f)] private float _pantingTintLerp = 0.4f;

    [Header("Collapse")]
    [SerializeField] private float _collapseDuration = 0.25f;
    [SerializeField] private float _collapseYScale = 0.85f;
    [SerializeField] private float _collapseYOffset = -0.1f;
    [SerializeField] private float _collapseFlashDuration = 0.1f;

    [Header("Stand-Up")]
    [SerializeField] private float _standUpDuration = 0.3f;

    private SpriteRenderer _renderer;
    private BossDamageFeedback _dmgFeedback;
    private Coroutine _pantingRoutine;
    private Vector3 _spriteBaseLocalScale;
    private Color _baseColor;
    private bool _isPanting;
    private bool _halfAmplitude; // true after collapse, until stand-up

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _dmgFeedback = GetComponent<BossDamageFeedback>();
        _spriteBaseLocalScale = transform.localScale;
        _baseColor = _renderer.color;
    }

    public void BeginPanting()
    {
        if (_isPanting) return;
        _isPanting = true;
        _halfAmplitude = false;
        _pantingRoutine = StartCoroutine(PantLoop());
    }

    public void EndPanting()
    {
        _isPanting = false;
        _halfAmplitude = false;
        if (_pantingRoutine != null)
        {
            StopCoroutine(_pantingRoutine);
            _pantingRoutine = null;
        }
        _renderer.color = _baseColor;
    }

    public IEnumerator PlayCollapse()
    {
        // Switch panting to half-amplitude (continues throughout active window).
        _halfAmplitude = true;

        // White flash (kept brief; reuse BossDamageFeedback's flash if available).
        if (_dmgFeedback != null)
            _dmgFeedback.PlaySmallFlashOnly(_collapseFlashDuration);

        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(_spriteBaseLocalScale.x,
            _spriteBaseLocalScale.y * _collapseYScale, _spriteBaseLocalScale.z);
        float startY = transform.localPosition.y;
        float endY = startY + _collapseYOffset;

        float t = 0f;
        while (t < _collapseDuration)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / _collapseDuration);
            transform.localScale = Vector3.Lerp(startScale, endScale, u);
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                Mathf.Lerp(startY, endY, u),
                transform.localPosition.z);
            yield return null;
            t += Time.deltaTime;
        }
        transform.localScale = endScale;
    }

    public IEnumerator PlayStandUp()
    {
        // Squash-stretch back to base: (1, 0.85) -> (1, 1.05) -> (1, 1.0)
        // and Y offset -0.1 -> 0 over the same window.
        Vector3 startScale = transform.localScale;
        Vector3 peakScale = new Vector3(_spriteBaseLocalScale.x,
            _spriteBaseLocalScale.y * 1.05f, _spriteBaseLocalScale.z);
        Vector3 endScale = _spriteBaseLocalScale;

        float startY = transform.localPosition.y;
        float endY = startY - _collapseYOffset; // back to base
        float half = _standUpDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            float u = t / half;
            transform.localScale = Vector3.Lerp(startScale, peakScale, u);
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                Mathf.Lerp(startY, endY, u),
                transform.localPosition.z);
            yield return null;
            t += Time.deltaTime;
        }
        t = 0f;
        while (t < half)
        {
            float u = t / half;
            transform.localScale = Vector3.Lerp(peakScale, endScale, u);
            yield return null;
            t += Time.deltaTime;
        }
        transform.localScale = endScale;

        EndPanting();
    }

    private IEnumerator PantLoop()
    {
        float t = 0f;
        float baseY = transform.localPosition.y;
        while (_isPanting)
        {
            float amp = _halfAmplitude ? _bobHalfAmplitudeVulnerable : _bobAmplitude;
            // Asymmetric: down-stroke ~30% slower than up-stroke.
            float phase = Mathf.Sin(t * Mathf.PI * 2f * _bobFrequency);
            float weighted = phase >= 0 ? phase : phase * 0.7f;
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                baseY + weighted * amp,
                transform.localPosition.z);

            // Tint toward critical color (uses BossDamageFeedback's tint if exposed).
            if (_dmgFeedback != null)
                _renderer.color = Color.Lerp(_baseColor, _dmgFeedback.CriticalColor, _pantingTintLerp);

            yield return null;
            t += Time.deltaTime;
        }
    }
}
