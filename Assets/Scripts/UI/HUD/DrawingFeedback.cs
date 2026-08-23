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

    /// <summary>
    /// How many correction cues this HUD has been asked for. The flash itself lives on
    /// serialized scene references that may or may not be wired, so this is the assertable
    /// record that a rejected draw actually reached the player-facing feedback path.
    /// </summary>
    public int RejectCueCount { get; private set; }

    private void Awake()
    {
        if (_rejectFlash != null) _rejectFlash.alpha = 0f;
        if (_rejectXMark != null) _rejectXMark.SetActive(false);
        if (_successFlash != null) _successFlash.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.OnDrawingFailed += ShowRejectFeedback;
        // A recognized-but-wrong draw is just as rejected as an unreadable one: the clue
        // mismatch path and the legacy no-carrier path both raise OnDrawingMissed, and before
        // SALIN-135 neither produced a HUD cue. Both are non-destructive — no heart, no
        // evidence success, no clue advance — so this adds the correction cue and nothing else.
        EventBus.OnDrawingMissed += ShowRejectFeedback;
        EventBus.OnEnemyDefeated += ShowSuccessFeedback;
    }

    private void OnDisable()
    {
        EventBus.OnDrawingFailed -= ShowRejectFeedback;
        EventBus.OnDrawingMissed -= ShowRejectFeedback;
        EventBus.OnEnemyDefeated -= ShowSuccessFeedback;
    }

    private void ShowRejectFeedback()
    {
        RejectCueCount++;

        // FlashFeedback yields immediately without a CanvasGroup, so an unwired HUD gains
        // nothing from starting the coroutine at all.
        if (_rejectFlash == null)
            return;

        StartCoroutine(FlashFeedback(_rejectFlash, _rejectXMark, _rejectDuration));
    }

    private void ShowSuccessFeedback(BaybayinCharacterSO _)
    {
        if (_successFlash == null)
            return;

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