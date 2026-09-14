/// <summary>
/// Player-facing wording for the drawing-feedback states that are not plain success or failure:
/// a syllable needed later, a syllable already restored, a syllable no enemy is carrying, a syllable
/// whose carrier turned out to be a false copy, and a drawing refused for accuracy.
///
/// <para>Kept apart from <see cref="DrawingFeedbackVocabulary"/>, which words the accept/reject
/// verdict. These five say something about the BOARD rather than about the stroke, and they must be
/// readable next to each other to stay distinct — a later-needed syllable and a miss are the two
/// states most easily collapsed into the same sentence, and collapsing them is exactly the failure
/// the filler policy cannot survive.</para>
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
    /// for it, so <see cref="ForMiss"/>'s claim — nothing out there is carrying that one — is simply
    /// false here. A player who watches an enemy break apart while being told the board was empty
    /// learns to distrust the prompt rather than the copy.
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

    /// <summary>
    /// Fallback miss wording for when the drawn glyph could not be resolved to content and so cannot
    /// be named. Same claim as <see cref="ForMiss"/>, minus the glyph.
    /// </summary>
    public const string MissUnnamed = "Nothing out there is carrying that one right now.";

    /// <summary>
    /// Wording for a miss: a recognised syllable that no on-screen enemy carries.
    /// </summary>
    /// <remarks>
    /// This string is a statement about the board and must stay one. It also serves the player who
    /// draws a syllable the spawn schedule has gated out of the level so far — and that player must
    /// not be able to infer the gate from what they are told. A rule-shaped phrasing leaks it
    /// instantly: "not yet", "not available yet", "you haven't unlocked", "that comes later" all
    /// describe a schedule the player is not supposed to know exists, and the last of those would
    /// additionally be indistinguishable from <see cref="LaterNeeded"/>, which is a completely
    /// different situation. Describing only what is on screen is true in both cases and leaks
    /// nothing in either, so one string covers both.
    ///
    /// If this copy is ever revised, the constraint survives the revision: no "yet", no reference to
    /// order, unlocking, or availability. Only what is or is not out there at this moment.
    /// </remarks>
    /// <param name="glyphLabel">
    /// How to name the drawn syllable to the player — its romanised syllable where one is authored.
    /// </param>
    public static string ForMiss(string glyphLabel)
    {
        if (string.IsNullOrEmpty(glyphLabel))
            return MissUnnamed;

        return $"No enemy out there carries {glyphLabel} right now.";
    }

    /// <summary>The prompt for one text relation, or empty when that relation says nothing.</summary>
    public static string ForRelation(DrawTextRelation relation, string glyphLabel)
    {
        switch (relation)
        {
            case DrawTextRelation.LaterNeeded:
                return LaterNeeded;
            case DrawTextRelation.AlreadyFilled:
                return AlreadyFilled;
            case DrawTextRelation.FalseCopyShattered:
                return FalseCopyShattered;
            case DrawTextRelation.NoCarrier:
                return ForMiss(glyphLabel);
            default:
                // FillsCursorSlot is its own reward and NotInTargetText has nothing to promise the
                // player, so neither gets a line. Silence is a deliberate response here, not a gap:
                // a prompt on every successful fill would turn the one state that needs no
                // explanation into the noisiest one on screen.
                return string.Empty;
        }
    }
}
