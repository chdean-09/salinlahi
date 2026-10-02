using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Renders the drawing-feedback states that are neither a plain success nor a plain failure: a
/// syllable needed later in the text, a duplicate of one already restored, a syllable no enemy is
/// carrying, a drawing this level forgave, and a drawing this level refused.
///
/// <para>The point of the component is that these read as five visibly different things. A correct
/// draw on a filler enemy — which the spawn schedule produces deliberately and often — is correct
/// recall arriving out of order, and if it renders the same way a miss does, the schedule spends the
/// level telling the player they were wrong for remembering. So a later-needed or already-restored
/// draw is answered with a pulse on the slot still to fill, while a miss is only
/// counted here and answered by DrawingFeedback.</para>
///
/// <para>The later-needed and already-restored states used to fly the badge to their slot as well,
/// settling as an outline or bouncing off. Removed after the 2026-09-29 playtest: a glyph landing
/// on a box it did not fill read as a drawing that failed to register, and the only glyph that
/// travels into a box now is the one that fills it (ActiveCluePresenter's slot flight).</para>
///
/// <para>A miss also used to flash the recognised glyph at the draw site and leave it dimmed until
/// the next stroke, and a forgiven or refused drawing replayed the ideal form over an enemy. Both
/// removed after the 2026-10-02 playtest: the white outline art read as stray handwriting appearing
/// on the field at random, not as feedback on the drawing that caused it.</para>
///
/// <para>Every scene reference below is optional. The HUD this belongs on does not carry any of them
/// yet, and the state this component reports has to stay correct and assertable in the meantime
/// rather than waiting on scene work — the same arrangement <see cref="DrawingFeedback"/> already
/// uses for its flash and its label.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class DrawFeedbackPresenter : MonoBehaviour
{
    [Header("Target Text Slots")]
    [Tooltip("The target text's slots in reading order — word 0's syllables left to right, then "
             + "word 1's. Index-aligned with the flattened slot list the feedback reports, so the "
             + "order here is not cosmetic: the cursor pulse lands on whichever entry it names.")]
    [SerializeField] private RectTransform[] _slotAnchors = new RectTransform[0];

    [Header("Overlays")]
    [Tooltip("The player's own ink. Held, then faded, after a refused drawing.")]
    [SerializeField] private CanvasGroup _playerInk;

    [Tooltip("Legacy message label, kept hidden now that post-draw text strips are removed.")]
    [SerializeField] private TMP_Text _messageLabel;

    [Header("Accuracy Response")]
    [Tooltip("Seconds a refused drawing's ink stays fully visible before fading. Design value: 0.5.")]
    [SerializeField, Min(0f)] private float _refusedInkHoldSeconds = 0.5f;

    [Tooltip("Seconds the refused ink takes to fade once the hold expires.")]
    [SerializeField, Min(0f)] private float _refusedInkFadeSeconds = 0.25f;

    [Header("Cursor Pulse")]
    [Tooltip("How many times the cursor slot pulses after a non-advancing draw. Design value: 2.")]
    [SerializeField, Min(0)] private int _cursorPulseCount = 2;

    [Tooltip("Seconds for one out-and-back pulse of the cursor slot.")]
    [SerializeField, Min(0.01f)] private float _cursorPulseSeconds = 0.22f;

    [Tooltip("Peak scale of a cursor-slot pulse.")]
    [SerializeField, Min(1f)] private float _cursorPulseScale = 1.18f;

    /// <summary>
    /// Diagnostic copy for the last cue. Combat text strips are no longer displayed.
    /// </summary>
    public string LastMessage { get; private set; } = string.Empty;

    /// <summary>The relation of the most recent draw that reached this presenter.</summary>
    public DrawTextRelation LastRelation { get; private set; } = DrawTextRelation.Unknown;

    /// <summary>The accuracy verdict of the most recent submitted drawing.</summary>
    public DrawAccuracyVerdict LastVerdict { get; private set; } = DrawAccuracyVerdict.Accepted;

    /// <summary>Later-needed responses played. Counted separately from misses on purpose: the two
    /// reading as one number would hide the very confusion this component exists to prevent.</summary>
    public int LaterNeededCueCount { get; private set; }

    /// <summary>Already-filled duplicate responses played.</summary>
    public int AlreadyFilledCueCount { get; private set; }

    /// <summary>Misses (draws no enemy was carrying) that reached this presenter.</summary>
    public int MissCueCount { get; private set; }

    private Coroutine _inkRoutine;
    private Coroutine _pulseRoutine;

    private void Awake()
    {
        SetMessage(string.Empty);
    }

    private void OnEnable()
    {
        DrawFeedbackSignals.OnAccuracyResolved += HandleAccuracyResolved;
        DrawFeedbackSignals.OnTextRelationResolved += HandleTextRelationResolved;
        EventBus.OnDrawingStarted += HandleDrawingStarted;
    }

    private void OnDisable()
    {
        DrawFeedbackSignals.OnAccuracyResolved -= HandleAccuracyResolved;
        DrawFeedbackSignals.OnTextRelationResolved -= HandleTextRelationResolved;
        EventBus.OnDrawingStarted -= HandleDrawingStarted;
    }

    /// <summary>
    /// Takes the previous attempt's residue back down as a new stroke begins: the ink faded out by a
    /// refusal is restored to full.
    /// </summary>
    /// <remarks>
    /// The faded ink is a state this component deliberately LEAVES standing so the player can still
    /// see it while reading the prompt. Something has to retract it, and the next stroke is the only
    /// honest moment: leaving it permanently would mean the refusal that faded the ink to zero also
    /// made every subsequent drawing on this level invisible.
    /// </remarks>
    public void HandleDrawingStarted()
    {
        if (_inkRoutine != null)
        {
            StopCoroutine(_inkRoutine);
            _inkRoutine = null;
        }

        if (_playerInk != null)
            _playerInk.alpha = 1f;
    }

    /// <summary>
    /// Plays the response for one accuracy verdict: nothing for a clean or forgiven drawing, and the
    /// retry prompt plus ink-hold for a refused one.
    /// </summary>
    public void HandleAccuracyResolved(DrawAccuracyReport report)
    {
        LastVerdict = report.Verdict;

        if (report.Verdict == DrawAccuracyVerdict.Rejected)
            PlayRefusedResponse();
    }

    /// <summary>
    /// Plays the response for one draw's relationship to the target text.
    /// </summary>
    public void HandleTextRelationResolved(DrawFeedbackReport report)
    {
        LastRelation = report.Relation;

        // One mapping from relation to copy, in the vocabulary, so the prompts stay readable side by
        // side. Keeping them inline here is how two of them drift into saying the same thing.
        // NoCarrier deliberately maps to nothing: its line was removed, and the miss is answered by
        // DrawingFeedback rather than through the message band.
        string prompt = DrawFeedbackVocabulary.ForRelation(report.Relation);
        if (!string.IsNullOrEmpty(prompt))
            SetMessage(prompt);

        switch (report.Relation)
        {
            case DrawTextRelation.LaterNeeded:
                LaterNeededCueCount++;
                StartCursorPulse(report.CursorSlotIndex);
                break;

            case DrawTextRelation.AlreadyFilled:
                AlreadyFilledCueCount++;
                StartCursorPulse(report.CursorSlotIndex);
                break;

            case DrawTextRelation.BlockedCarrier:
                report.BlockedTarget?.GlyphBadge?.PlayBlockedFlash();
                break;

            case DrawTextRelation.NoCarrier:
                MissCueCount++;
                break;
        }
    }

    private void PlayRefusedResponse()
    {
        SetMessage(DrawFeedbackVocabulary.SloppyRetry);

        if (_inkRoutine != null)
            StopCoroutine(_inkRoutine);
        _inkRoutine = StartCoroutine(HoldThenFadeInk());
    }

    private IEnumerator HoldThenFadeInk()
    {
        if (_playerInk == null)
        {
            _inkRoutine = null;
            yield break;
        }

        _playerInk.alpha = 1f;
        yield return WaitUnscaled(_refusedInkHoldSeconds);

        float elapsed = 0f;
        while (elapsed < _refusedInkFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _playerInk.alpha = 1f - Mathf.Clamp01(elapsed / _refusedInkFadeSeconds);
            yield return null;
        }

        _playerInk.alpha = 0f;
        _inkRoutine = null;
    }

    private void StartCursorPulse(int cursorSlotIndex)
    {
        RectTransform cursor = ResolveSlotAnchor(cursorSlotIndex);
        if (cursor == null || _cursorPulseCount <= 0)
            return;

        if (_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseSlot(cursor));
    }

    private IEnumerator PulseSlot(RectTransform slot)
    {
        Vector3 baseScale = slot.localScale;

        for (int i = 0; i < _cursorPulseCount; i++)
        {
            float elapsed = 0f;
            while (elapsed < _cursorPulseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                // One out-and-back per pulse, so the count the designer authors is the count the
                // player sees rather than half of it.
                float phase = Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / _cursorPulseSeconds));
                slot.localScale = baseScale * Mathf.LerpUnclamped(1f, _cursorPulseScale, phase);
                yield return null;
            }
        }

        slot.localScale = baseScale;
        _pulseRoutine = null;
    }

    /// <summary>
    /// The rect a badge should fly to for one flattened target-text slot.
    ///
    /// Prefers the live restoration rail built by <see cref="ActiveCluePresenter"/> over the
    /// serialized <see cref="_slotAnchors"/> array, because the rail is the real target-text HUD:
    /// it is constructed from the level's own focus words, so its ordering mirrors
    /// <c>TargetTextSlotMap.Build</c> — the same flattening that produced
    /// <c>DrawFeedbackReport.SlotIndex</c>. Index alignment is therefore by construction rather
    /// than by a scene author remembering to drag four rects in reading order, which is exactly
    /// the mistake that would send a badge to the wrong slot and read as a bug.
    ///
    /// The serialized array stays as the fallback for a scene that has no rail (a level with
    /// restoration disabled, or an Editor-authored HUD wired before the rail existed).
    /// </summary>
    private RectTransform ResolveSlotAnchor(int slotIndex)
    {
        if (slotIndex < 0)
            return null;

        ActiveCluePresenter presenter = ActiveCluePresenter.Active;
        if (presenter != null)
        {
            RectTransform railAnchor = presenter.GetRestorationSlotAnchor(slotIndex);
            if (railAnchor != null)
                return railAnchor;
        }

        if (_slotAnchors == null || slotIndex >= _slotAnchors.Length)
            return null;

        return _slotAnchors[slotIndex];
    }

    // Unscaled throughout: an enemy introduction runs the level at timeScale 0.15, and feedback the
    // player is reading must not stretch to eight seconds because a card happened to be up.
    private static IEnumerator WaitUnscaled(float seconds)
    {
        if (seconds > 0f)
            yield return new WaitForSecondsRealtime(seconds);
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
}
