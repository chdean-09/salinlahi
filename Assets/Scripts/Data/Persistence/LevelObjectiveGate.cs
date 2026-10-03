using System.Collections.Generic;

/// <summary>
/// SALIN-220. Stable identifiers for the five per-objective completion flags persisted on
/// <see cref="LevelProgressRecord"/> and carried on <see cref="CampaignProgressOutcome"/>.
/// </summary>
/// <remarks>
/// The identifier suffix is the serialized field name verbatim, so a consumer can map an
/// identifier back to the flag it names without a lookup table. SALIN-229/235/242 read these
/// field names as a contract -- do not rename them, and do not add a sixth. UF-27 names six
/// requirements including Combat, but combat is already gated by
/// <see cref="LevelFlowMachine.ReportDefenseComplete"/> and is deliberately not a flag here.
///
/// No player-facing wording exists for any of these objectives anywhere in the repository, so
/// they are machine-readable identifiers only. See the TODO in <see cref="LevelLockNoticeCopy"/>.
/// </remarks>
public static class LevelObjectives
{
    public const string StoryViewed = "objective.storyViewed";
    public const string SymbolsPracticed = "objective.symbolsPracticed";
    public const string WordsRestored = "objective.wordsRestored";
    public const string ContextPassed = "objective.contextPassed";
    public const string FinalSyllableRestored = "objective.finalSyllableRestored";

    /// <summary>Every objective identifier, in the order the gate reports a missing one.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        StoryViewed,
        SymbolsPracticed,
        WordsRestored,
        ContextPassed,
        FinalSyllableRestored,
    };
}

/// <summary>
/// SALIN-220. Plain carrier for one attempt's five objective results, handed from the level flow
/// to <see cref="ProgressManager"/> and copied onto the outcome at commit time.
/// </summary>
/// <remarks>
/// Field names mirror <see cref="LevelProgressRecord"/> and <see cref="CampaignProgressOutcome"/>
/// exactly so the copies read one-to-one and a mismatched assignment is visible on sight.
/// </remarks>
public sealed class LevelObjectiveFlags
{
    public bool storyViewed;
    public bool symbolsPracticed;
    public bool wordsRestored;
    public bool contextPassed;
    public bool finalSyllableRestored;

    /// <summary>
    /// All five satisfied. The meaning of "this completion recorded no objective detail", used
    /// for a journal written before SALIN-220 and for any commit path that never ran the level
    /// flow -- by the rules in force when such a completion was earned it was fully complete.
    /// </summary>
    public static LevelObjectiveFlags Satisfied() => new LevelObjectiveFlags
    {
        storyViewed = true,
        symbolsPracticed = true,
        wordsRestored = true,
        contextPassed = true,
        finalSyllableRestored = true,
    };
}

/// <summary>
/// SALIN-220. The single unlock predicate over the persisted objective flags.
/// </summary>
/// <remarks>
/// Three places must agree exactly on when a successor unlocks: the writer
/// (<see cref="CampaignOutcomeCoordinator.ApplyLevelProgression"/>), the verifier
/// (<see cref="CampaignOutcomeCoordinator.VerifyPublishedOutcome"/>) and the display
/// (<see cref="LevelLockResolver"/>). Two copies of this rule is how a gate ends up telling the
/// player something the save does not say, so all three call in here and nowhere else.
///
/// The gate withholds only the SUCCESSOR's unlock. The finished level itself always stays
/// <c>completed</c> with its earned stars: withholding <c>completed</c> while stars were earned
/// trips the star-state invariant in <see cref="CampaignSaveValidator"/> and would reject the
/// whole save. Withholding a successor unlock is legal there, because that validator's
/// "a later level is unlocked before its predecessor" rule is one-way.
/// </remarks>
public static class LevelObjectiveGate
{
    /// <summary>True when every objective on <paramref name="level"/> is satisfied.</summary>
    public static bool AllSatisfied(LevelProgressRecord level)
    {
        return level != null &&
            level.storyViewed &&
            level.symbolsPracticed &&
            level.wordsRestored &&
            level.contextPassed &&
            level.finalSyllableRestored;
    }

    /// <summary>
    /// The identifier of the first unsatisfied objective on <paramref name="level"/>, or
    /// <c>null</c> when all five are satisfied or the record is missing.
    /// </summary>
    public static string FirstUnsatisfied(LevelProgressRecord level)
    {
        if (level == null)
            return null;
        if (!level.storyViewed)
            return LevelObjectives.StoryViewed;
        if (!level.symbolsPracticed)
            return LevelObjectives.SymbolsPracticed;
        if (!level.wordsRestored)
            return LevelObjectives.WordsRestored;
        if (!level.contextPassed)
            return LevelObjectives.ContextPassed;
        if (!level.finalSyllableRestored)
            return LevelObjectives.FinalSyllableRestored;
        return null;
    }

    /// <summary>
    /// Merges the outcome's flags onto the committed record monotonically.
    /// </summary>
    /// <remarks>
    /// OR, never assignment -- the same shape as <c>bestStars</c>'s max and
    /// <c>TutorialProgressRecord.seen</c>'s <c>|=</c>. A replay that satisfies a
    /// previously-missing objective must be able to complete the set; it must never clear one
    /// the player has already earned.
    /// </remarks>
    public static void ApplyMonotonic(LevelProgressRecord level, CampaignProgressOutcome outcome)
    {
        if (level == null || outcome == null)
            return;
        level.storyViewed |= outcome.storyViewed;
        level.symbolsPracticed |= outcome.symbolsPracticed;
        level.wordsRestored |= outcome.wordsRestored;
        level.contextPassed |= outcome.contextPassed;
        level.finalSyllableRestored |= outcome.finalSyllableRestored;
    }

    /// <summary>
    /// SALIN-220 half-transaction check: every flag the outcome carried true must be true on the
    /// committed record. The invariant, not equality -- a record may legitimately already carry
    /// an objective this weaker attempt did not satisfy.
    /// </summary>
    public static bool AllCarriedFlagsApplied(
        LevelProgressRecord level, CampaignProgressOutcome outcome)
    {
        if (outcome == null)
            return true;
        if (level == null)
            return false;
        return (!outcome.storyViewed || level.storyViewed) &&
            (!outcome.symbolsPracticed || level.symbolsPracticed) &&
            (!outcome.wordsRestored || level.wordsRestored) &&
            (!outcome.contextPassed || level.contextPassed) &&
            (!outcome.finalSyllableRestored || level.finalSyllableRestored);
    }

    /// <summary>True when the outcome carries any objective flag set.</summary>
    public static bool CarriesAnyFlag(CampaignProgressOutcome outcome)
    {
        return outcome != null &&
            (outcome.storyViewed ||
             outcome.symbolsPracticed ||
             outcome.wordsRestored ||
             outcome.contextPassed ||
             outcome.finalSyllableRestored);
    }

    /// <summary>Copies a carrier onto an outcome. Null means "no detail recorded" (all satisfied).</summary>
    public static void CopyTo(LevelObjectiveFlags flags, CampaignProgressOutcome outcome)
    {
        if (outcome == null)
            return;
        LevelObjectiveFlags source = flags ?? LevelObjectiveFlags.Satisfied();
        outcome.storyViewed = source.storyViewed;
        outcome.symbolsPracticed = source.symbolsPracticed;
        outcome.wordsRestored = source.wordsRestored;
        outcome.contextPassed = source.contextPassed;
        outcome.finalSyllableRestored = source.finalSyllableRestored;
    }
}
