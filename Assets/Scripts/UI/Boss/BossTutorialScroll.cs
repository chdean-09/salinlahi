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

    [Header("Animation")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private float _animDuration = 0.15f;
    [SerializeField] private float _startScale = 0.85f;

    private BossTutorialPage[] _pages;
    private BossTutorialPaging _paging;
    private Coroutine _anim;

    /// <summary>Raised when the player closes the scroll with the red X.</summary>
    public event Action OnClosed;

    private void Awake()
    {
        if (_leftArrow != null) _leftArrow.onClick.AddListener(GoLeft);
        if (_rightArrow != null) _rightArrow.onClick.AddListener(GoRight);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        HideImmediate();
    }

    public void Show(IReadOnlyList<BossTutorialPage> pages)
    {
        if (pages == null || pages.Count == 0)
            return;

        _pages = new BossTutorialPage[pages.Count];
        for (int i = 0; i < pages.Count; i++) _pages[i] = pages[i];

        _paging = new BossTutorialPaging(_pages.Length);
        RenderCurrent();

        gameObject.SetActive(true);
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(0f, 1f, _startScale, 1f, deactivateAtEnd: false));
    }

    private void GoLeft() { _paging.Prev(); RenderCurrent(); }
    private void GoRight() { _paging.Next(); RenderCurrent(); }

    private void Close()
    {
        if (!gameObject.activeSelf) return;
        AudioManager.Instance?.PlayMenuExitButtonClick();
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(1f, 0f, 1f, _startScale, deactivateAtEnd: true));
        OnClosed?.Invoke();
    }

    private void RenderCurrent()
    {
        if (_pages == null || _pages.Length == 0) return;
        BossTutorialPage page = _pages[_paging.Index];

        if (_art != null)
        {
            _art.sprite = page.art;
            _art.enabled = page.art != null;
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
