using System;

/// <summary>
/// How a drawing scored against the two thresholds that now apply to it: the level's own accuracy
/// floor and the campaign-wide default it may sit below.
/// </summary>
public enum DrawAccuracyVerdict
{
    /// <summary>Below the level's floor. The draw is refused and nothing in combat resolves.</summary>
    Rejected,

    /// <summary>
    /// At or above the level's floor but below the global default — a drawing this level forgives and
    /// the rest of the campaign would not. It succeeds in full, and earns a silent correction after
    /// the kill rather than a prompt, because interrupting a success to grade it is how a forgiving
    /// threshold stops reading as forgiving.
    /// </summary>
    AcceptedWithSilentCorrection,

    /// <summary>At or above the global default. Ordinary success, no correction owed.</summary>
    Accepted,
}

/// <summary>
/// Where a correctly drawn syllable sits relative to the target text's cursor. This is the
/// distinction the spawn-assignment filler policy depends on: filler enemies deliberately carry
/// syllables needed LATER, so drawing one is correct recall that does not advance the text. Without
/// a relation this specific, every one of those draws would have to be rendered as the same
/// undifferentiated "nothing happened", and a policy built on later-needed filler would spend the
/// whole level telling the player they were wrong for being right.
/// </summary>
public enum DrawTextRelation
{
    /// <summary>No target text to classify against — a legacy level, or a level with no focus words.</summary>
    Unknown,

    /// <summary>The cursor slot's syllable. The text advances; this is the ordinary success.</summary>
    FillsCursorSlot,

    /// <summary>
    /// A carrier of the drawn syllable fell, but it was one of Iligaw's copies; the text did not
    /// advance.
    ///
    /// <para>Kept apart from <see cref="FillsCursorSlot"/> because the glyph alone cannot tell the
    /// two apart. A copy wears the cursor's own symbol, so the drawing is right, the kill is real,
    /// and the only thing that did not happen is the restoration — which means a classifier reading
    /// the glyph and nothing else reports the copy's death as a slot fill. On screen that is
    /// indistinguishable from genuinely restoring a slot, so the player is congratulated for progress
    /// they do not have, and the deception beat's entire lesson — the copy fell, the real one is
    /// still walking — is replaced by a false one.</para>
    ///
    /// <para>Equally not <see cref="NoCarrier"/>. A body the player could plainly read on screen
    /// really did die, and wording this as "nothing out there carries that" would be the lie that
    /// refusing copies as targets used to tell.</para>
    /// </summary>
    FalseCopyShattered,

    /// <summary>An unfilled slot that comes after the cursor. Correct recall, out of order.</summary>
    LaterNeeded,

    /// <summary>A slot already restored earlier in this level.</summary>
    AlreadyFilled,

    /// <summary>
    /// Carried by an enemy but absent from the target text — an off-target pool symbol. Level 1 has
    /// none (its pool is its target), but the relation exists so later levels do not fall into
    /// LaterNeeded and promise the player a future slot that will never arrive.
    /// </summary>
    NotInTargetText,

    /// <summary>No eligible on-screen enemy carried the syllable at all: a miss.</summary>
    NoCarrier,
}

/// <summary>One drawing's accuracy outcome, as handed to the feedback presenter.</summary>
public struct DrawAccuracyReport
{
    public DrawAccuracyVerdict Verdict;

    /// <summary>Recognizer's best match. Meaningful even when rejected — it is what the ink looked like.</summary>
    public string CharacterId;

    public float Score;

    /// <summary>The floor actually applied, after any level override.</summary>
    public float LevelThreshold;

    /// <summary>The campaign default, so a presenter can tell a forgiven draw from a clean one.</summary>
    public float GlobalThreshold;
}

/// <summary>One drawing's relationship to the target text, and which enemy (if any) it resolved against.</summary>
public struct DrawFeedbackReport
{
    public DrawTextRelation Relation;

    public string DrawnCharacterId;

    /// <summary>The drawn glyph as content, for rendering. Null when the level could not resolve it.</summary>
    public BaybayinCharacterSO DrawnCharacter;

    /// <summary>Flattened target-text slot this syllable belongs to, or -1 when it belongs to none.</summary>
    public int SlotIndex;

    /// <summary>Flattened index of the slot the text is currently waiting on, or -1 when the text is done.</summary>
    public int CursorSlotIndex;

    /// <summary>The enemy the draw killed. Null on a miss.</summary>
    public Enemy ResolvedTarget;
}

/// <summary>
/// Signals for the drawing-feedback states, kept deliberately apart from <see cref="EventBus"/>.
///
/// These are presentation signals with no subscriber outside the feedback HUD, and both carry a
/// struct payload that only the presenter understands. Routing them through the global bus would put
/// two more entries in front of every system that reads it, for an audience of one. The tradeoff is
/// the same one <see cref="ActiveClueDirector.OnActiveClueResolved"/> already accepted in the other
/// direction, and for the same reason: keep the global bus for things systems genuinely share.
/// </summary>
public static class DrawFeedbackSignals
{
    /// <summary>
    /// Raised once per submitted drawing, before combat resolves, so a presenter can latch a pending
    /// silent correction and settle it when the kill it belongs to arrives.
    /// </summary>
    public static event Action<DrawAccuracyReport> OnAccuracyResolved;

    /// <summary>
    /// Raised once per accepted drawing that reached combat, after the target set is known and
    /// BEFORE the clue is consumed — the cursor in the payload is the one the player was actually
    /// answering, not the one they advanced it to.
    /// </summary>
    public static event Action<DrawFeedbackReport> OnTextRelationResolved;

    public static void RaiseAccuracyResolved(DrawAccuracyReport report) => OnAccuracyResolved?.Invoke(report);

    public static void RaiseTextRelationResolved(DrawFeedbackReport report) => OnTextRelationResolved?.Invoke(report);
}
