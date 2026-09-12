using System.Collections.Generic;

/// <summary>
/// SALIN-220. Derives one attempt's five objective flags from the level's phase plan and the set
/// of phases the machine actually completed.
/// </summary>
/// <remarks>
/// THE RULE (D2): an objective the level does not author counts as SATISFIED. A flag is true when
/// the phase that produces it was not planned for this level, or was planned and completed.
///
/// This is not leniency, it is the only safe rule today. Some phases are conditional on authored
/// content (<see cref="LevelPhasePlan.FromConfig"/>) -- FocusWords, SymbolLearning and
/// RequiredPractice -- so a literal "all five must be true" gate would make a level permanently
/// unable to unlock its successor on an objective it never authored.
///
/// SALIN-223 UPDATE, and it narrows this rule's reach considerably: ContextChallenge and
/// MemoryReward are now planned on EVERY level. A level that authors no challenge sequence no
/// longer skips the phase -- the executor refuses to complete it and the flow never reaches
/// AtomicSave, which is this resolver's only caller. So the "unauthored challenge" case that
/// originally motivated this rule can no longer reach the resolver at all: on Levels 6, 7, 8, 10
/// and 13 no unlock is withheld here because no unlock is attempted. On Levels 1-5 ContextChallenge
/// is planned and completed, so its two flags are true on the merits. The rule still matters for
/// the genuinely conditional phases above, and for the challenge-prototype carve-out, where the
/// sequence plays inside Defense rather than as a planned ContextChallenge phase.
///
/// CONSEQUENCE, STATED PLAINLY: every level that can be completed today writes all five flags
/// true, so this gate withholds no unlock in normal play. SALIN-220 delivers the persisted
/// contract and the mechanism; SALIN-228 (real RequiredPractice), SALIN-229 (the final-syllable
/// step) and SALIN-242 give the flags something to be false about.
/// </remarks>
public static class LevelObjectiveFlagResolver
{
#if UNITY_EDITOR
    /// <summary>
    /// SALIN-220 AC6 demo hook. When set to a <see cref="LevelObjectives"/> identifier, the next
    /// resolve clears that one flag so the gate can be observed withholding an unlock. Set from
    /// <c>Salinlahi/Debug/SALIN-220</c>; mirrors the <c>CampaignSaveFileStorage.EditorFailNextAt</c>
    /// fault-injection precedent, and is compiled out of player builds.
    /// </summary>
    public static string EditorForceUnsatisfied { get; set; }
#endif

    /// <summary>
    /// Resolves the five flags. A null plan or a null completed-phase set is treated as "nothing
    /// authored, nothing completed" and yields all five satisfied, matching the rule above.
    /// </summary>
    public static LevelObjectiveFlags Resolve(
        LevelPhasePlan plan, IReadOnlyCollection<LevelPhase> completedPhases)
    {
        var flags = new LevelObjectiveFlags
        {
            storyViewed = IsSatisfied(plan, completedPhases, LevelPhase.Story),
            symbolsPracticed = IsSatisfied(plan, completedPhases, LevelPhase.RequiredPractice),

            // Words restored and the context challenge are both produced by the one
            // ChallengeSession: its units are the word restorations and its slots are the context
            // matches. There is no separate signal on dev, so they share the ContextChallenge
            // phase. SALIN-228/242 split them when each gets its own pass/fail.
            wordsRestored = IsSatisfied(plan, completedPhases, LevelPhase.ContextChallenge),
            contextPassed = IsSatisfied(plan, completedPhases, LevelPhase.ContextChallenge),

            // TODO(SALIN-229): LevelConfigSO.finalRestorationValue is authored but has no runtime
            // reader, so there is nothing to check. Hard-true until SALIN-229 wires the
            // final-syllable step. Do not invent a check here.
            finalSyllableRestored = true,
        };

#if UNITY_EDITOR
        ApplyEditorFault(flags);
#endif
        return flags;
    }

    private static bool IsSatisfied(
        LevelPhasePlan plan, IReadOnlyCollection<LevelPhase> completedPhases, LevelPhase phase)
    {
        if (plan == null || !plan.Has(phase))
            return true;
        return completedPhases != null && Contains(completedPhases, phase);
    }

    private static bool Contains(IReadOnlyCollection<LevelPhase> phases, LevelPhase phase)
    {
        foreach (LevelPhase candidate in phases)
            if (candidate == phase)
                return true;
        return false;
    }

#if UNITY_EDITOR
    private static void ApplyEditorFault(LevelObjectiveFlags flags)
    {
        string forced = EditorForceUnsatisfied;
        if (string.IsNullOrEmpty(forced))
            return;

        if (forced == LevelObjectives.StoryViewed)
            flags.storyViewed = false;
        else if (forced == LevelObjectives.SymbolsPracticed)
            flags.symbolsPracticed = false;
        else if (forced == LevelObjectives.WordsRestored)
            flags.wordsRestored = false;
        else if (forced == LevelObjectives.ContextPassed)
            flags.contextPassed = false;
        else if (forced == LevelObjectives.FinalSyllableRestored)
            flags.finalSyllableRestored = false;
    }
#endif
}
