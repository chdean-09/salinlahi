using System.Collections;
using TMPro;
using UnityEngine;

public class DrawingFeedback : MonoBehaviour
{
    [Header("Drawing Feedback")]
    [SerializeField] private CanvasGroup _rejectFlash;
    [SerializeField] private GameObject _rejectXMark;
    [SerializeField] private CanvasGroup _successFlash;
    [SerializeField] private float _rejectDuration = 0.5f;
    [SerializeField] private float _successDuration = 0.3f;

    [Header("Supportive Feedback")]
    [Tooltip("Consecutive rejected drawings before the optional trace hint is offered. 0 disables the offer.")]
    [Min(0)]
    [SerializeField] private int _attemptsBeforeHelpOffer = 3;

    [Tooltip("Optional prompt inviting the player to see the stroke traced. Safe to leave unwired.")]
    [SerializeField] private GameObject _traceHintPrompt;

    [Tooltip("Legacy message label, kept hidden now that post-draw text strips are removed.")]
    [SerializeField] private TMP_Text _messageLabel;

    /// <summary>
    /// How many correction cues this HUD has been asked for. The flash itself lives on
    /// serialized scene references that may or may not be wired, so this is the assertable
    /// record that a rejected draw actually reached the player-facing feedback path.
    /// </summary>
    public int RejectCueCount { get; private set; }

    /// <summary>
    /// Rejections since the last accepted character. Unlike <see cref="RejectCueCount"/> this
    /// resets on success, because it drives the help offer rather than recording history.
    /// </summary>
    public int ConsecutiveRejectCount { get; private set; }

    /// <summary>True once the configured help threshold has been reached (SALIN-163 AC2).</summary>
    public bool HelpAvailable { get; private set; }

    /// <summary>
    /// Diagnostic copy for the last cue. Retained alongside the visual feedback counters even
    /// though combat text strips are no longer displayed.
    /// </summary>
    public string LastMessage { get; private set; } = string.Empty;

    private void Awake()
    {
        if (_rejectFlash != null) _rejectFlash.alpha = 0f;
        if (_rejectXMark != null) _rejectXMark.SetActive(false);
        if (_successFlash != null) _successFlash.alpha = 0f;
        if (_traceHintPrompt != null) _traceHintPrompt.SetActive(false);
        SetMessage(string.Empty);
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
        ConsecutiveRejectCount++;

        // AC2. Once offered, the hint stays offered for the rest of this run of failures.
        // Withdrawing it on the next attempt would be worse than never having offered it.
        if (_attemptsBeforeHelpOffer > 0 && ConsecutiveRejectCount >= _attemptsBeforeHelpOffer)
            SetHelpAvailable(true);

        SetMessage(DrawingFeedbackVocabulary.ForRejection(ConsecutiveRejectCount, HelpAvailable));

        // FlashFeedback yields immediately without a CanvasGroup, so an unwired HUD gains
        // nothing from starting the coroutine at all.
        if (_rejectFlash == null)
            return;

        StartCoroutine(FlashFeedback(_rejectFlash, _rejectXMark, _rejectDuration));
    }

    private void ShowSuccessFeedback(BaybayinCharacterSO _)
    {
        // AC3. Accepting the character clears the entire help run -- counter, offer and prompt --
        // before the flash is even considered, so a player who succeeded *because* of the hint
        // carries nothing forward from having needed it. Kept above the null guard for the same
        // reason the reject counter is: an unwired HUD must still settle its state correctly.
        ConsecutiveRejectCount = 0;
        SetHelpAvailable(false);
        SetMessage(DrawingFeedbackVocabulary.Accepted);

        if (_successFlash == null)
            return;

        StartCoroutine(FlashFeedback(_successFlash, null, _successDuration));
    }

    // Keep cue state and visual feedback, but do not display post-draw text strips.
    private void SetMessage(string message)
    {
        LastMessage = message;
        if (_messageLabel != null)
        {
            _messageLabel.text = string.Empty;
            _messageLabel.gameObject.SetActive(false);
        }
    }

    private void SetHelpAvailable(bool available)
    {
        HelpAvailable = available;

        if (_traceHintPrompt != null)
            _traceHintPrompt.SetActive(available);
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
