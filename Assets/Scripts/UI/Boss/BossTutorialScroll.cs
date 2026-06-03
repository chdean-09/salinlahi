using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Paged "blank scroll" overlay for the upfront boss tutorial. Reuses AlmanacDetailScroll's
/// visual idiom (expand/fade on Time.unscaledDeltaTime, red X to close, graceful art/body
/// hiding) and adds left/right pagination across BossTutorialPage[]. Page 1 is the boss name
/// + lore; later pages are mechanics. The red X closes from any page and raises OnClosed.
///
/// Per-page art supports frame animation (walkFrames-style) and boss-state visual effects
/// (panting bob + tint, collapsed squash) applied to the UI Image, mirroring the runtime
/// BossStateVisuals so the player previews what each state looks like.
/// </summary>
public class BossTutorialScroll : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private Image _art;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _body;
    [SerializeField] private TextMeshProUGUI _pageIndicator;

    [Header("Navigation")]
    [SerializeField] private Button _leftArrow;
    [SerializeField] private Button _rightArrow;
    [SerializeField] private Button _closeButton;
    [Range(0f, 1f)]
    [SerializeField] private float _disabledArrowAlpha = 0.35f;

    [Header("Open/Close Animation")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private float _animDuration = 0.15f;
    [SerializeField] private float _startScale = 0.85f;

    [Header("Art Effects — Panting")]
    [Tooltip("Y-offset amplitude (anchored position units) for the panting bob.")]
    [SerializeField] private float _bobAmplitude = 4f;
    [Tooltip("Half-amplitude used during the Collapsed effect.")]
    [SerializeField] private float _bobHalfAmplitude = 2f;
    [Tooltip("Bob oscillation frequency in Hz.")]
    [SerializeField] private float _bobFrequency = 1.5f;
    [Tooltip("Color to tint the art toward during Panting / Collapsed effects.")]
    [SerializeField] private Color _pantingTintColor = new Color(1f, 0.5f, 0.5f, 1f);
    [Tooltip("Lerp factor toward the panting tint color. 0 = no tint, 1 = full tint.")]
    [Range(0f, 1f)]
    [SerializeField] private float _pantingTintLerp = 0.4f;

    [Header("Art Effects — Collapsed")]
    [Tooltip("Y-scale multiplier when Collapsed (e.g. 0.85 = squashed to 85% height).")]
    [SerializeField] private float _collapseYScale = 0.85f;
    [Tooltip("Anchored Y offset applied when Collapsed (pushes the art down).")]
    [SerializeField] private float _collapseYOffset = -8f;

    private BossTutorialPage[] _pages;
    private BossTutorialPaging _paging;
    private Coroutine _anim;
    private Coroutine _artEffectRoutine;

    // Stored base state of the art Image so effects can be applied/restored cleanly.
    private Vector2 _artBaseAnchoredPos;
    private Vector3 _artBaseScale;
    private bool _artBaseStateCaptured;

    /// <summary>Raised when the player closes the scroll with the red X.</summary>
    public event Action OnClosed;

    private void Awake()
    {
        if (_leftArrow != null) _leftArrow.onClick.AddListener(GoLeft);
        if (_rightArrow != null) _rightArrow.onClick.AddListener(GoRight);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        CaptureArtBaseState();
        HideImmediate();
    }

    public void Show(IReadOnlyList<BossTutorialPage> pages)
    {
        if (pages == null || pages.Count == 0)
            return;

        _pages = new BossTutorialPage[pages.Count];
        for (int i = 0; i < pages.Count; i++) _pages[i] = pages[i];

        _paging = new BossTutorialPaging(_pages.Length);
        gameObject.SetActive(true);
        RenderCurrent();
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(0f, 1f, _startScale, 1f, deactivateAtEnd: false));
    }

    private void GoLeft() { _paging.Prev(); RenderCurrent(); }
    private void GoRight() { _paging.Next(); RenderCurrent(); }

    private void Close()
    {
        if (!gameObject.activeSelf) return;
        StopArtEffect();
        AudioManager.Instance?.PlayMenuExitButtonClick();
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(1f, 0f, 1f, _startScale, deactivateAtEnd: true));
        OnClosed?.Invoke();
    }

    private void CaptureArtBaseState()
    {
        if (_art == null || _artBaseStateCaptured) return;
        RectTransform rt = _art.rectTransform;
        _artBaseAnchoredPos = rt.anchoredPosition;
        _artBaseScale = rt.localScale;
        _artBaseStateCaptured = true;
    }

    private void RestoreArtBaseState()
    {
        if (_art == null || !_artBaseStateCaptured) return;
        RectTransform rt = _art.rectTransform;
        rt.anchoredPosition = _artBaseAnchoredPos;
        rt.localScale = _artBaseScale;
        _art.color = Color.white;
    }

    private void RenderCurrent()
    {
        if (_pages == null || _pages.Length == 0) return;
        BossTutorialPage page = _pages[_paging.Index];

        // Stop any running art effect/animation from the previous page.
        StopArtEffect();
        RestoreArtBaseState();

        if (_art != null)
        {
            bool hasArt = page.HasArt;
            _art.enabled = hasArt;
            if (hasArt)
            {
                _art.sprite = page.frames[0];
                // Start the combined frame-animation + effect coroutine.
                _artEffectRoutine = StartCoroutine(RunArtEffects(page));
            }
        }

        if (_title != null) _title.text = page.title ?? string.Empty;
        if (_body != null)
        {
            bool hasText = !string.IsNullOrWhiteSpace(page.body);
            _body.text = hasText ? page.body : string.Empty;
            _body.gameObject.SetActive(hasText);
        }
        if (_pageIndicator != null)
            _pageIndicator.text = $"{_paging.Index + 1} / {_pages.Length}";

        ApplyArrowState(_leftArrow, _paging.CanGoLeft);
        ApplyArrowState(_rightArrow, _paging.CanGoRight);
    }

    /// <summary>
    /// Combined art coroutine: drives frame animation and per-page visual effects
    /// (panting bob + tint, collapsed squash) on the UI Image, using unscaled time
    /// so it runs even if the game is paused.
    /// </summary>
    private IEnumerator RunArtEffects(BossTutorialPage page)
    {
        if (_art == null) yield break;

        Sprite[] frames = page.frames;
        float fps = page.animationFps;
        BossTutorialArtEffect effect = page.effect;
        RectTransform rt = _art.rectTransform;

        bool animate = frames != null && frames.Length > 1 && fps > 0f;
        bool hasPanting = effect == BossTutorialArtEffect.Panting
                       || effect == BossTutorialArtEffect.Collapsed;
        bool isCollapsed = effect == BossTutorialArtEffect.Collapsed;

        // Apply the static collapse transform before starting the loop.
        if (isCollapsed)
        {
            Vector3 s = _artBaseScale;
            s.y *= _collapseYScale;
            rt.localScale = s;
            rt.anchoredPosition = _artBaseAnchoredPos + new Vector2(0f, _collapseYOffset);
        }

        // If there's nothing to animate and no effect, bail — the static sprite
        // was already set in RenderCurrent.
        if (!animate && !hasPanting)
            yield break;

        float frameDuration = animate ? 1f / fps : 0f;
        float frameTimer = 0f;
        int frameIndex = 0;
        float effectTime = 0f;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;

            // --- Frame animation ---
            if (animate)
            {
                frameTimer += dt;
                while (frameTimer >= frameDuration)
                {
                    frameTimer -= frameDuration;
                    frameIndex = (frameIndex + 1) % frames.Length;
                }
                if (frames[frameIndex] != null)
                    _art.sprite = frames[frameIndex];
            }

            // --- Panting bob + tint ---
            if (hasPanting)
            {
                float amp = isCollapsed ? _bobHalfAmplitude : _bobAmplitude;
                float phase = Mathf.Sin(effectTime * Mathf.PI * 2f * _bobFrequency);
                // Asymmetric: down-stroke ~30% slower than up-stroke (matches BossStateVisuals).
                float weighted = phase >= 0 ? phase : phase * 0.7f;

                Vector2 basePos = isCollapsed
                    ? _artBaseAnchoredPos + new Vector2(0f, _collapseYOffset)
                    : _artBaseAnchoredPos;
                rt.anchoredPosition = basePos + new Vector2(0f, weighted * amp);

                _art.color = Color.Lerp(Color.white, _pantingTintColor, _pantingTintLerp);
            }

            effectTime += dt;
            yield return null;
        }
    }

    private void StopArtEffect()
    {
        if (_artEffectRoutine != null)
        {
            StopCoroutine(_artEffectRoutine);
            _artEffectRoutine = null;
        }
    }

    private void ApplyArrowState(Button arrow, bool enabled)
    {
        if (arrow == null) return;
        arrow.interactable = enabled;
        Image img = arrow.targetGraphic as Image ?? arrow.GetComponent<Image>();
        if (img != null)
        {
            Color c = img.color;
            c.a = enabled ? 1f : _disabledArrowAlpha;
            img.color = c;
        }
    }

    private void HideImmediate()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private IEnumerator Animate(float fromA, float toA, float fromS, float toS, bool deactivateAtEnd)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.interactable = !deactivateAtEnd;
            _canvasGroup.blocksRaycasts = !deactivateAtEnd;
        }

        float t = 0f;
        while (t < _animDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / _animDuration);
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(fromA, toA, k);
            if (_panel != null) _panel.localScale = Vector3.one * Mathf.Lerp(fromS, toS, k);
            yield return null;
        }

        if (_canvasGroup != null) _canvasGroup.alpha = toA;
        if (_panel != null) _panel.localScale = Vector3.one * toS;
        if (deactivateAtEnd) gameObject.SetActive(false);
        _anim = null;
    }
}
