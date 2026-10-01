using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the drawing-feedback states that are neither a plain success nor a plain failure: a
/// syllable needed later in the text, a duplicate of one already restored, a syllable no enemy is
/// carrying, a drawing this level forgave, and a drawing this level refused.
///
/// <para>The point of the component is that these read as five visibly different things. A correct
/// draw on a filler enemy — which the spawn schedule produces deliberately and often — is correct
/// recall arriving out of order, and if it renders the same way a miss does, the schedule spends the
/// level telling the player they were wrong for remembering. So a later-needed or already-restored
/// draw is answered with its own line and a pulse on the slot still to fill, while a miss never
/// leaves the draw site and dims in place.</para>
///
/// <para>The later-needed and already-restored states used to fly the badge to their slot as well,
/// settling as an outline or bouncing off. Removed after the 2026-09-29 playtest: a glyph landing
/// on a box it did not fill read as a drawing that failed to register, and the only glyph that
/// travels into a box now is the one that fills it (ActiveCluePresenter's slot flight).</para>
///
/// <para>Every scene reference below is optional. The HUD this belongs on does not carry any of them
/// yet, and the state this component reports has to stay correct and assertable in the meantime
/// rather than waiting on scene work — the same arrangement <see cref="DrawingFeedback"/> already
/// uses for its flash and its label.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class DrawFeedbackPresenter : MonoBehaviour
{
    [Header("Canvas Wiring")]
    [Tooltip("Canvas these overlays live under. Used to convert world positions into UI space. "
             + "Resolved from this object's parents when left unassigned.")]
    [SerializeField] private Canvas _canvas;

    [Tooltip("Camera that renders the enemies. Falls back to Camera.main when unassigned.")]
    [SerializeField] private Camera _worldCamera;

    [Tooltip("Where the player's ink sits, so the miss glyph can flash at the draw site rather than "
             + "somewhere on the HUD the player was not looking.")]
    [SerializeField] private RectTransform _drawSiteAnchor;

    [Header("Target Text Slots")]
    [Tooltip("The target text's slots in reading order — word 0's syllables left to right, then "
             + "word 1's. Index-aligned with the flattened slot list the feedback reports, so the "
             + "order here is not cosmetic: the cursor pulse lands on whichever entry it names.")]
    [SerializeField] private RectTransform[] _slotAnchors = new RectTransform[0];

    [Header("Overlays")]
    [Tooltip("Glyph flashed at the draw site on a miss, then left dimmed in place.")]
    [SerializeField] private Image _missGlyph;

    [Tooltip("Overlay that replays the ideal form. Set its Image Type to Filled for the replay to "
             + "wipe on as a stroke; any other type fades it in instead.")]
    [SerializeField] private Image _ghostStrokeOverlay;

    [Tooltip("The player's own ink. Held, then faded, after a refused drawing.")]
    [SerializeField] private CanvasGroup _playerInk;

    [Tooltip("Label carrying the player-facing prompt. Safe to leave unwired. Must NOT be the same "
             + "label DrawingFeedback writes to: a later-needed draw is a kill, so DrawingFeedback's "
             + "OnEnemyDefeated cue fires for it too and lands AFTER this one — a shared label would "
             + "end up reading 'Nice, that's the one' over a syllable that filled nothing.")]
    [SerializeField] private TMP_Text _messageLabel;

    [Header("Accuracy Response")]
    [Tooltip("Seconds the ideal form replays for, on a badge, after either accuracy tier. "
             + "Design value: 1.2.")]
    [SerializeField, Min(0f)] private float _ghostStrokeReplaySeconds = 1.2f;

    [Tooltip("Seconds a refused drawing's ink stays fully visible before fading. Design value: 0.5. "
             + "The hold is what makes the replay legible as a correction OF something.")]
    [SerializeField, Min(0f)] private float _refusedInkHoldSeconds = 0.5f;

    [Tooltip("Seconds the refused ink takes to fade once the hold expires.")]
    [SerializeField, Min(0f)] private float _refusedInkFadeSeconds = 0.25f;

    [Tooltip("Seconds to wait after a forgiven drawing before its silent correction plays. Must "
             + "outlast CombatResolver's pronunciation lead and the death burst, or the correction "
             + "lands on an enemy the player is still watching die and reads as part of the kill.")]
    [SerializeField, Min(0f)] private float _silentCorrectionDelaySeconds = 0.6f;

    [Tooltip("Alpha the replayed ideal form reaches over the badge. An outline, not an occupant.")]
    [SerializeField, Range(0f, 1f)] private float _ghostOutlineAlpha = 0.45f;

    [Header("Cursor Pulse")]
    [Tooltip("How many times the cursor slot pulses after a non-advancing draw. Design value: 2.")]
    [SerializeField, Min(0)] private int _cursorPulseCount = 2;

    [Tooltip("Seconds for one out-and-back pulse of the cursor slot.")]
    [SerializeField, Min(0.01f)] private float _cursorPulseSeconds = 0.22f;

    [Tooltip("Peak scale of a cursor-slot pulse.")]
    [SerializeField, Min(1f)] private float _cursorPulseScale = 1.18f;

    [Header("Miss Response")]
    [Tooltip("Seconds the missed glyph is shown at full strength before dimming.")]
    [SerializeField, Min(0f)] private float _missFlashSeconds = 0.35f;

    [Tooltip("Alpha the missed glyph is left at. Design value: 0.30 — present but plainly inert.")]
    [SerializeField, Range(0f, 1f)] private float _missDimAlpha = 0.3f;

    [Tooltip("Seconds a prompt stays up before the label clears. 0 leaves it until the next prompt.")]
    [SerializeField, Min(0f)] private float _messageHoldSeconds = 3f;

    /// <summary>
    /// The wording last handed to the player. Held for the same reason
    /// <see cref="DrawingFeedback.LastMessage"/> is: the label that renders it is an optional scene
    /// reference, so this is the assertable record of what the player was actually told.
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

    /// <summary>Miss responses played.</summary>
    public int MissCueCount { get; private set; }

    /// <summary>Silent corrections played after a forgiven drawing.</summary>
    public int SilentCorrectionCount { get; private set; }

    /// <summary>Correct-form replays played after a refused drawing.</summary>
    public int RefusedReplayCount { get; private set; }

    // Set when a drawing was accepted below the global default, cleared when the correction it owes
    // has been scheduled. Latched rather than acted on immediately because the correction belongs to
    // the kill, and the kill is not known until the text relation arrives.
    private bool _silentCorrectionPending;

    private Coroutine _replayRoutine;
    private Coroutine _inkRoutine;
    private Coroutine _missRoutine;
    private Coroutine _pulseRoutine;
    private Coroutine _clearMessageRoutine;

    private void Awake()
    {
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        HideOverlay(_missGlyph);
        HideOverlay(_ghostStrokeOverlay);
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

        // A pending correction must not survive the component going away, or the next level's first
        // clean drawing inherits a correction it never earned.
        _silentCorrectionPending = false;
    }

    /// <summary>
    /// Takes the previous attempt's residue back down as a new stroke begins: the ink faded out by a
    /// refusal is restored to full, and a dimmed miss glyph is cleared.
    /// </summary>
    /// <remarks>
    /// Both of those are states this component deliberately LEAVES standing so the player can still
    /// see them while reading the prompt. Something has to retract them, and the next stroke is the
    /// only honest moment: a timer would take the correction away while it was still being read, and
    /// leaving them permanently would mean the refusal that faded the ink to zero also made every
    /// subsequent drawing on this level invisible.
    /// </remarks>
    public void HandleDrawingStarted()
    {
        // Also the expiry of any uncollected correction. A forgiven drawing that never reached combat
        // — consumed by the boss route, or submitted while a tutorial override holds combat — raises
        // no text relation, so its latch would otherwise still be set when the NEXT drawing's kill
        // arrives and would hand that kill a correction it did not earn.
        _silentCorrectionPending = false;

        if (_inkRoutine != null)
        {
            StopCoroutine(_inkRoutine);
            _inkRoutine = null;
        }

        if (_playerInk != null)
            _playerInk.alpha = 1f;

        if (_missRoutine != null)
        {
            StopCoroutine(_missRoutine);
            _missRoutine = null;
        }

        HideOverlay(_missGlyph);
    }

    /// <summary>
    /// Plays the response for one accuracy verdict: nothing for a clean drawing, a latched silent
    /// correction for a forgiven one, and the ink-hold plus correct-form replay for a refused one.
    /// </summary>
    public void HandleAccuracyResolved(DrawAccuracyReport report)
    {
        LastVerdict = report.Verdict;

        switch (report.Verdict)
        {
            case DrawAccuracyVerdict.Accepted:
                _silentCorrectionPending = false;
                break;

            case DrawAccuracyVerdict.AcceptedWithSilentCorrection:
                // No prompt and no interruption. This drawing succeeded in full; the correction is
                // the only thing owed, and it waits for the kill to finish.
                _silentCorrectionPending = true;
                break;

            case DrawAccuracyVerdict.Rejected:
                _silentCorrectionPending = false;
                PlayRefusedResponse();
                break;
        }
    }

    /// <summary>
    /// Plays the response for one draw's relationship to the target text, and settles any silent
    /// correction the drawing that produced it had owing.
    /// </summary>
    public void HandleTextRelationResolved(DrawFeedbackReport report)
    {
        LastRelation = report.Relation;

        // One mapping from relation to copy, in the vocabulary, so the prompts stay readable side by
        // side. Keeping them inline here is how two of them drift into saying the same thing.
        // NoCarrier deliberately maps to nothing: its line was removed, and the miss now answers
        // through PlayMissResponse below rather than through the message band.
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
                PlayMissResponse(report.DrawnCharacter);
                break;
        }

        // Runs for every relation including a clean fill: a forgiven drawing that filled its slot is
        // still owed its correction, and a forgiven drawing that missed is owed nothing because
        // nothing died to correct on. ConsumeSilentCorrection settles both.
        ConsumeSilentCorrection(report);
    }

    private void ConsumeSilentCorrection(DrawFeedbackReport report)
    {
        if (!_silentCorrectionPending)
            return;

        _silentCorrectionPending = false;

        // The correction is replayed on the badge of the enemy that died, so a draw that killed
        // nothing has no badge to play it on. Skipping it is right rather than relocating it: the
        // player has already been told the board carried nothing, and a correct-form replay on top
        // of that reads as a second, contradictory verdict on the same drawing.
        if (report.ResolvedTarget == null)
            return;

        BaybayinCharacterSO form = report.DrawnCharacter;
        if (form == null)
            return;

        SilentCorrectionCount++;
        StartReplay(form, report.ResolvedTarget.transform.position, _silentCorrectionDelaySeconds);
    }

    private void PlayRefusedResponse()
    {
        SetMessage(DrawFeedbackVocabulary.SloppyRetry);

        if (_inkRoutine != null)
            StopCoroutine(_inkRoutine);
        _inkRoutine = StartCoroutine(HoldThenFadeInk());

        // The form replayed is the one the player still needs, not the one the recognizer guessed:
        // a refused stroke's best match is by definition a shape the recognizer did not believe, and
        // replaying that would teach the miss back to them. The needed glyph comes from the clue
        // mark, which is the only "this one, now" answer the game has.
        Enemy clue = ActiveClueDirector.Instance != null ? ActiveClueDirector.Instance.CurrentClue : null;
        if (clue == null || clue.Character == null)
            return;

        RefusedReplayCount++;
        StartReplay(clue.Character, clue.transform.position, _refusedInkHoldSeconds);
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

    private void PlayMissResponse(BaybayinCharacterSO drawnCharacter)
    {
        if (_missGlyph == null)
            return;

        Sprite sprite = ResolveFormSprite(drawnCharacter);
        if (sprite == null)
            return;

        if (_missRoutine != null)
            StopCoroutine(_missRoutine);
        _missRoutine = StartCoroutine(FlashThenDimMiss(sprite));
    }

    private IEnumerator FlashThenDimMiss(Sprite sprite)
    {
        _missGlyph.sprite = sprite;
        _missGlyph.enabled = true;
        SetAlpha(_missGlyph, 1f);

        if (_drawSiteAnchor != null)
            PlaceAtLocalPointOf(_missGlyph.rectTransform, _drawSiteAnchor);

        yield return WaitUnscaled(_missFlashSeconds);

        // Left dimmed rather than removed. The glyph is a record of what the game understood, and
        // taking it away would leave the player with a prompt about a syllable they can no longer see.
        SetAlpha(_missGlyph, _missDimAlpha);
        _missRoutine = null;
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

    private void StartReplay(BaybayinCharacterSO form, Vector3 worldPoint, float delaySeconds)
    {
        if (_ghostStrokeOverlay == null)
            return;

        Sprite sprite = ResolveFormSprite(form);
        if (sprite == null)
            return;

        if (_replayRoutine != null)
        {
            StopCoroutine(_replayRoutine);
        }

        _replayRoutine = StartCoroutine(ReplayForm(sprite, worldPoint, delaySeconds));
    }

    /// <summary>
    /// Wipes the ideal form on over the badge, then clears it.
    /// </summary>
    /// <remarks>
    /// A wipe rather than a true stroke-order animation. The recognition templates hold point clouds,
    /// not ordered strokes with timing, so a faithful replay would need stroke data that does not
    /// exist in the project yet. A directional reveal of <c>glyphOutlineSprite</c> — the same bare
    /// outline the Tracing Dojo's guide and the trace hint already use — gives the form the sense of
    /// being drawn rather than appearing, which is what the correction needs to carry, and needs no
    /// new art. Set the overlay Image's Type to Filled to get the wipe; any other type fades instead.
    /// </remarks>
    private IEnumerator ReplayForm(Sprite sprite, Vector3 worldPoint, float delaySeconds)
    {
        yield return WaitUnscaled(delaySeconds);

        _ghostStrokeOverlay.sprite = sprite;
        _ghostStrokeOverlay.enabled = true;
        TryPlaceOverWorldPoint(_ghostStrokeOverlay.rectTransform, worldPoint);

        bool wipes = _ghostStrokeOverlay.type == Image.Type.Filled;
        SetAlpha(_ghostStrokeOverlay, wipes ? _ghostOutlineAlpha : 0f);

        float elapsed = 0f;
        while (elapsed < _ghostStrokeReplaySeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _ghostStrokeReplaySeconds);

            if (wipes)
                _ghostStrokeOverlay.fillAmount = t;
            else
                SetAlpha(_ghostStrokeOverlay, _ghostOutlineAlpha * t);

            yield return null;
        }

        HideOverlay(_ghostStrokeOverlay);
        _replayRoutine = null;
    }

    /// <summary>
    /// Prefers <c>glyphOutlineSprite</c>, the bare glyph generated from the recognition templates,
    /// and falls back to <c>badgeSprite</c>. Never <c>displaySprite</c>: that is the learning card
    /// with the romanised syllable printed on it, so using it here would answer in Latin script a
    /// question the player is being asked in Baybayin.
    /// </summary>
    private static Sprite ResolveFormSprite(BaybayinCharacterSO character)
    {
        if (character == null)
            return null;

        return character.glyphOutlineSprite != null ? character.glyphOutlineSprite : character.badgeSprite;
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

    /// <summary>
    /// Moves <paramref name="target"/> so it sits over <paramref name="worldPoint"/>. False when the
    /// conversion has no camera or no rect parent to resolve against.
    /// </summary>
    private bool TryPlaceOverWorldPoint(RectTransform target, Vector3 worldPoint)
    {
        Camera world = _worldCamera != null ? _worldCamera : Camera.main;
        if (target == null || world == null)
            return false;

        if (!(target.parent is RectTransform parent))
            return false;

        Vector2 screenPoint = world.WorldToScreenPoint(worldPoint);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screenPoint, UiCamera, out Vector2 local))
            return false;

        target.anchoredPosition = local;
        return true;
    }

    private void PlaceAtLocalPointOf(RectTransform target, RectTransform reference)
    {
        if (target != null && reference != null)
            target.anchoredPosition = LocalPointOf(target, reference);
    }

    /// <summary>
    /// <paramref name="reference"/>'s position expressed in <paramref name="target"/>'s parent
    /// space, so a UI overlay can be moved to another UI element regardless of their anchoring.
    /// </summary>
    private Vector2 LocalPointOf(RectTransform target, RectTransform reference)
    {
        if (!(target.parent is RectTransform parent))
            return target.anchoredPosition;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(UiCamera, reference.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, screenPoint, UiCamera, out Vector2 local)
            ? local
            : target.anchoredPosition;
    }

    // Overlay canvases pass a null camera to the rect utilities; every other render mode needs the
    // canvas's own camera or the screen point lands in the wrong space.
    private Camera UiCamera =>
        _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

    private static void SetAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private static void HideOverlay(Image image)
    {
        if (image == null)
            return;

        image.enabled = false;
        SetAlpha(image, 0f);
    }

    // Unscaled throughout: an enemy introduction runs the level at timeScale 0.15, and feedback the
    // player is reading must not stretch to eight seconds because a card happened to be up.
    private static IEnumerator WaitUnscaled(float seconds)
    {
        if (seconds > 0f)
            yield return new WaitForSecondsRealtime(seconds);
    }

    private void SetMessage(string message)
    {
        LastMessage = message;

        if (_messageLabel == null)
            return;

        CombatNotificationBanner.Configure(_messageLabel, 0.25f);
        _messageLabel.text = message;

        if (_clearMessageRoutine != null)
        {
            StopCoroutine(_clearMessageRoutine);
            _clearMessageRoutine = null;
        }

        // LastMessage is deliberately not cleared alongside the label: it is the assertable record of
        // what the player was told, and a record that erases itself on a timer is not a record.
        if (_messageHoldSeconds <= 0f || !isActiveAndEnabled)
            return;

        _clearMessageRoutine = StartCoroutine(ClearMessageAfterHold());
    }

    private IEnumerator ClearMessageAfterHold()
    {
        yield return WaitUnscaled(_messageHoldSeconds);

        if (_messageLabel != null)
            _messageLabel.text = string.Empty;

        _clearMessageRoutine = null;
    }
}
