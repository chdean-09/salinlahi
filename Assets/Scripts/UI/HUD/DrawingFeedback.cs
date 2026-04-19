using System.Collections;
using UnityEngine;

public class DrawingFeedback : MonoBehaviour
{
    [Header("Drawing Feedback")]
    [SerializeField] private CanvasGroup _rejectFlash;
    [SerializeField] private GameObject _rejectXMark;
    [SerializeField] private CanvasGroup _successFlash;
    [SerializeField] private float _rejectDuration = 0.5f;
    [SerializeField] private float _successDuration = 0.3f;

    private void Awake()
    {
        if (_rejectFlash != null) _rejectFlash.alpha = 0f;
        if (_rejectXMark != null) _rejectXMark.SetActive(false);
        if (_successFlash != null) _successFlash.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.OnDrawingFailed += ShowRejectFeedback;
        EventBus.OnEnemyDefeated += ShowSuccessFeedback;
    }

    private void OnDisable()
    {
        EventBus.OnDrawingFailed -= ShowRejectFeedback;
        EventBus.OnEnemyDefeated -= ShowSuccessFeedback;
    }

    private void ShowRejectFeedback()
    {
        StartCoroutine(FlashFeedback(_rejectFlash, _rejectXMark, _rejectDuration));
    }

    private void ShowSuccessFeedback(BaybayinCharacterSO _)
    {
        StartCoroutine(FlashFeedback(_successFlash, null, _successDuration));
    }

    private IEnumerator FlashFeedback(CanvasGroup flash, GameObject mark, float duration)
    {
        if (flash == null) yield break;

        flash.alpha = 1f;
        if (mark != null) mark.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            flash.alpha = 1f - (elapsed / duration);
            yield return null;
        }

        flash.alpha = 0f;
        if (mark != null) mark.SetActive(false);
    }
}