using System.Collections;
using TMPro;
using UnityEngine;

// flashes a centered "MASS CLEAR xN" badge for a short display window
public class MassClearBadge : MonoBehaviour
{
    [Header("Mass Clear Badge")]
    [SerializeField] private CanvasGroup _badgeRoot;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private float _displaySeconds = 1.0f;
    [SerializeField] private float _fadeSeconds = 0.2f;

    private Coroutine _currentRoutine;

    private void Awake()
    {
        if (_badgeRoot != null)
            _badgeRoot.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.OnAOETriggered += OnAOE;
    }

    private void OnDisable()
    {
        EventBus.OnAOETriggered -= OnAOE;
    }

    private void OnAOE(int defeatedCount)
    {
        if (_badgeRoot == null || _label == null) return;
        if (defeatedCount <= 0) return;

        _label.text = $"MASS CLEAR x{defeatedCount}";

        if (_currentRoutine != null)
            StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        _badgeRoot.alpha = 1f;

        float hold = _displaySeconds;
        while (hold > 0f)
        {
            hold -= Time.unscaledDeltaTime;
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < _fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _badgeRoot.alpha = 1f - (elapsed / _fadeSeconds);
            yield return null;
        }

        _badgeRoot.alpha = 0f;
        _currentRoutine = null;
    }
}