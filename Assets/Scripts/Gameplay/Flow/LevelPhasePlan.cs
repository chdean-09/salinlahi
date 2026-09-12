/// <summary>
/// Immutable per-level phase plan computed once from a <see cref="LevelConfigSO"/>.
/// A phase that is not planned is skipped by <see cref="LevelFlowMachine"/> without
/// executor involvement.
///
/// SALIN-223: <see cref="LevelPhase.ContextChallenge"/> and
/// <see cref="LevelPhase.MemoryReward"/> are now planned on EVERY level, whether or not
/// their content exists. Planning them only when content was authored meant a level with
/// neither ran Story → Defense → AtomicSave → Results and completed on wave clear alone —
/// the phases vanished silently instead of failing loudly. Missing content is now
/// reported through <see cref="ContextChallengeContentMissing"/> and
/// <see cref="MemoryRewardContentMissing"/>, and the executors refuse to complete the
/// phase rather than falling through.
///
/// FocusWords, SymbolLearning and RequiredPractice keep the content-conditional rule;
/// this ticket does not change them.
/// </summary>
public sealed class LevelPhasePlan
{
    /// <summary>The nine playable phases in LF-CONTRACT-v2 order.</summary>
    public static readonly LevelPhase[] PhaseOrder =
    {
        LevelPhase.Story,
        LevelPhase.FocusWords,
        LevelPhase.SymbolLearning,
        LevelPhase.RequiredPractice,
        LevelPhase.Defense,
        LevelPhase.ContextChallenge,
        LevelPhase.MemoryReward,
        LevelPhase.AtomicSave,
        LevelPhase.Results,
    };

    private readonly bool _hasFocusWords;
    private readonly bool _hasSymbolLearning;
    private readonly bool _hasRequiredPractice;
    private readonly bool _hasContextChallenge;
    private readonly bool _hasMemoryReward;

    private LevelPhasePlan(
        bool hasFocusWords,
        bool hasSymbolLearning,
        bool hasRequiredPractice,
        bool hasContextChallenge,
        bool hasMemoryReward,
        bool contextChallengeContentMissing,
        bool memoryRewardContentMissing)
    {
        _hasFocusWords = hasFocusWords;
        _hasSymbolLearning = hasSymbolLearning;
        _hasRequiredPractice = hasRequiredPractice;
        _hasContextChallenge = hasContextChallenge;
        _hasMemoryReward = hasMemoryReward;
        ContextChallengeContentMissing = contextChallengeContentMissing;
        MemoryRewardContentMissing = memoryRewardContentMissing;
    }

    /// <summary>
    /// SALIN-223. True when <see cref="LevelPhase.ContextChallenge"/> is planned but the
    /// level has no <c>challengeSequence</c> to run. The executor presents the
    /// content-missing panel and refuses to complete the phase.
    /// </summary>
    public bool ContextChallengeContentMissing { get; }

    /// <summary>
    /// SALIN-223. True when <see cref="LevelPhase.MemoryReward"/> is planned but the level
    /// has no memory content to give.
    ///
    /// Both keys are required, deliberately. The plan has always keyed this phase on
    /// <c>rewardIds</c> while the executor keys it on <c>contextMedia.cutscene</c>; on
    /// today's data the two agree on all fifteen levels, so no test can distinguish them
    /// and picking one would ship a silent wrong answer the moment a level is authored
    /// half-way. Requiring both is the only rule that cannot pass on a half-authored level.
    /// </summary>
    public bool MemoryRewardContentMissing { get; }

    public static LevelPhasePlan FromConfig(LevelConfigSO config)
    {
        if (config == null)
        {
            // A null config is the legacy case. It still plans both phases: leaving them
            // unplanned here would reintroduce the silent completion through the back door.
            return new LevelPhasePlan(
                hasFocusWords: false,
                hasSymbolLearning: false,
                hasRequiredPractice: false,
                hasContextChallenge: true,
                hasMemoryReward: true,
                contextChallengeContentMissing: true,
                memoryRewardContentMissing: true);
        }

        // The challenge-prototype path plays the sequence as a pre-wave tutorial
        // replacement inside the Defense executor; planning phase 6 as well would
        // run the same sequence twice. This carve-out is the one case where
        // ContextChallenge is legitimately unplanned, and it survives SALIN-223.
        bool contextChallenge = !config.challengePrototypeEnabled;

        return new LevelPhasePlan(
            hasFocusWords: config.focusWords != null && config.focusWords.Count > 0,
            hasSymbolLearning: config.learningRequirements != null && config.learningRequirements.Count > 0,
            hasRequiredPractice: config.practiceRequirements != null && config.practiceRequirements.Count > 0,
            hasContextChallenge: contextChallenge,
            hasMemoryReward: true,
            contextChallengeContentMissing: contextChallenge && config.challengeSequence == null,
            memoryRewardContentMissing:
                config.rewardIds == null
                || config.rewardIds.Count == 0
                || config.contextMedia == null
                || config.contextMedia.cutscene == null);
    }

    public bool Has(LevelPhase phase)
    {
        switch (phase)
        {
            case LevelPhase.Story:
            case LevelPhase.Defense:
            case LevelPhase.AtomicSave:
            case LevelPhase.Results:
                return true;
            case LevelPhase.FocusWords:
                return _hasFocusWords;
            case LevelPhase.SymbolLearning:
                return _hasSymbolLearning;
            case LevelPhase.RequiredPractice:
                return _hasRequiredPractice;
            case LevelPhase.ContextChallenge:
                return _hasContextChallenge;
            case LevelPhase.MemoryReward:
                return _hasMemoryReward;
            default:
                return false;
        }
    }

    /// <summary>
    /// The first planned phase strictly after <paramref name="phase"/> in
    /// LF-CONTRACT-v2 order, or <see cref="LevelPhase.Completed"/> when
    /// <paramref name="phase"/> is the last planned phase.
    /// </summary>
    public LevelPhase NextPlannedAfter(LevelPhase phase)
    {
        bool passed = phase == LevelPhase.NotStarted;
        for (int i = 0; i < PhaseOrder.Length; i++)
        {
            LevelPhase candidate = PhaseOrder[i];
            if (passed && Has(candidate))
                return candidate;
            if (candidate == phase)
                passed = true;
        }

        return LevelPhase.Completed;
    }
}
