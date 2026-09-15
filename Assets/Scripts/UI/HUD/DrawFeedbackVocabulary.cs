/// <summary>
/// Player-facing wording for the drawing-feedback states that are not plain success or failure:
/// a syllable needed later, a syllable already restored, a syllable whose carrier turned out to be
/// a false copy, and a drawing refused for accuracy.
///
/// <para>Kept apart from <see cref="DrawingFeedbackVocabulary"/>, which words the accept/reject
/// verdict. These four say something about the BOARD rather than about the stroke, and they must be
/// readable next to each other to stay distinct.</para>
///
/// <para><b>A miss gets no line.</b> <see cref="DrawTextRelation.NoCarrier"/> used to print "No
/// enemy out there carries {syllable} right now."; that message was removed on request. The miss
/// still answers — the presenter's miss response and its cue counter are untouched — it just
/// answers without words.</para>
/// </summary>
public static class DrawFeedbackVocabulary
{
    /// <summary>
    /// A target syllable whose slot comes after the cursor. States the ORDER, not a mistake: the
    /// player recalled a real syllable of the real word and the only thing wrong was the turn.
    /// </summary>
    public const string LaterNeeded = "That one comes later — the text fills in order.";

    /// <summary>A duplicate of a slot already restored.</summary>
    public const string AlreadyFilled = "That one is already restored.";

    /// <summary>
    /// A carrier of the drawn syllable fell, but it was one of Iligaw's copies, so nothing was
    /// restored. States the two facts the player cannot read off the board on their own: the thing
    /// that broke was a copy, and the text is no further along.
    /// </summary>
    /// <remarks>
    /// Three constraints, and this line is wrong the moment any one of them slips.
    ///
    /// <b>It must not read as a miss.</b> The player recognised the glyph and a body on screen died
    /// for it, so the miss claim — nothing out there is carrying that one — is simply false here. A
    /// player who watches an enemy break apart while being told the board was empty learns to
    /// distrust the prompt rather than the copy. That the miss no longer prints a line of its own
    /// does not relax this: borrowing the miss's wording here would resurrect the same lie.
    ///
    /// <b>It must not read as <see cref="LaterNeeded"/>.</b> The drawn syllable IS the one the text
    /// is waiting on. Order was never the problem, so nothing here may mention order, turns, or
    /// later — that wording would send the player away to draw it again at a moment when this same
    /// outcome is waiting for them.
    ///
    /// <b>It must not state the counter.</b> The introduction template states what an ability does
    /// and leaves the player to work out what to do about it, so no "look for the real one" and no
    /// "check for the dot". Naming what fell and what is still walking describes the board; naming
    /// what to draw next would prescribe the answer, and the derivation is the lesson.
    /// </remarks>
    public const string FalseCopyShattered = "That one was a copy — it fell, and the real one still walks.";

    /// <summary>Wording for a drawing refused on accuracy. Names the shape, never the score.</summary>
    public const string SloppyRetry = "Draw it again — follow the shape.";

    /// <summary>The prompt for one text relation, or empty when that relation says nothing.</summary>
    /// <remarks>
    /// Takes the relation alone. It used to take the drawn syllable as well, purely so the miss line
    /// could name it; with that line gone no surviving prompt mentions the glyph, and keeping the
    /// parameter would leave a hook inviting one to start.
    /// </remarks>
    public static string ForRelation(DrawTextRelation relation)
    {
        switch (relation)
        {
            case DrawTextRelation.LaterNeeded:
                return LaterNeeded;
            case DrawTextRelation.AlreadyFilled:
                return AlreadyFilled;
            case DrawTextRelation.FalseCopyShattered:
                return FalseCopyShattered;
            default:
                // FillsCursorSlot is its own reward, NotInTargetText has nothing to promise the
                // player, and NoCarrier's line was cut on request. Silence is a deliberate response
                // here, not a gap: a prompt on every successful fill would turn the one state that
                // needs no explanation into the noisiest one on screen.
                return string.Empty;
        }
    }
}
