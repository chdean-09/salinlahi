using System.Collections;
using UnityEngine;

public class FocusModeIndicator : MonoBehaviour
{
    [Header("Focus Mode")]
    [SerializeField] private GameObject _focusModeIndicator;
    [SerializeField] private CanvasGroup _focusModeCanvasGroup;
    [SerializeField] private float _focusFadeDuration = 0.2f;

    private void OnEnable()
    {
        EventBus.OnFocusModeActivated += ShowFocusIndicator;
        EventBus.OnFocusModeDeactivated += HideFocusIndicator;
    }

    private void OnDisable()
    {
        EventBus.OnFocusModeActivated -= ShowFocusIndicator;
        EventBus.OnFocusModeDeactivated -= HideFocusIndicator;
    }

    private void ShowFocusIndicator()
    {
        if (_focusModeIndicator != null)
            _focusModeIndicator.SetActive(true);

        if (_focusModeCanvasGroup != null)
            StartCoroutine(FadeCanvasGroup(_focusModeCanvasGroup, 0f, 1f, _focusFadeDuration));
    }

    private void HideFocusIndicator()
    {
        if (_focusModeCanvasGroup != null)
            StartCoroutine(FadeCanvasGroupThenDisable(_focusModeCanvasGroup, _focusModeIndicator, _focusFadeDuration));
        else if (_focusModeIndicator != null)
            _focusModeIndicator.SetActive(false);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator FadeCanvasGroupThenDisable(CanvasGroup group, GameObject target, float duration)
    {
        yield return FadeCanvasGroup(group, 1f, 0f, duration);
        if (target != null) target.SetActive(false);
    }
}