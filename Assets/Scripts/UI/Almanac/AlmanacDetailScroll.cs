using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single reusable "blank scroll" overlay, filled per selection. <see cref="Show"/> sets the
/// art/title/description and plays a short expand animation; the red X button calls <see cref="Hide"/>.
/// Degrades gracefully: an empty description is hidden, a missing portrait leaves the frame blank.
/// Uses Time.unscaledDeltaTime so it animates regardless of timeScale (same idiom as SceneLoader).
/// </summary>
public class AlmanacDetailScroll : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private Image _art;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _description;

    [Header("Animation")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _closeButton;
    [SerializeField] private float _animDuration = 0.15f;
    [SerializeField] private float _startScale = 0.85f;

    private Coroutine _anim;

    /// <summary>Raised when the detail scroll opens. The Almanac uses this to hide its nav buttons.</summary>
    public event Action OnShown;
    /// <summary>Raised when the detail scroll begins closing, so nav buttons can reappear.</summary>
    public event Action OnHidden;

    private void Awake()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        HideImmediate();
    }

    public void Show(Sprite art, string title, string description)
    {
        if (_art != null)
        {
            _art.sprite = art;
            _art.enabled = art != null;
        }
        if (_title != null) _title.text = title ?? string.Empty;
        if (_description != null)
        {
            bool hasText = !string.IsNullOrWhiteSpace(description);
            _description.text = hasText ? description : string.Empty;
            _description.gameObject.SetActive(hasText);
        }

        gameObject.SetActive(true);
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(0f, 1f, _startScale, 1f, deactivateAtEnd: false));
        OnShown?.Invoke();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        OnHidden?.Invoke();
        AudioManager.Instance?.PlayMenuExitButtonClick();
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(1f, 0f, 1f, _startScale, deactivateAtEnd: true));
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
