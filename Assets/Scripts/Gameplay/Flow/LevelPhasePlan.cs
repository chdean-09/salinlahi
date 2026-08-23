/// <summary>
/// Immutable per-level phase plan computed once from a <see cref="LevelConfigSO"/>.
/// A phase that is not planned is skipped by <see cref="LevelFlowMachine"/> without
/// executor involvement, which is how legacy configs (no revised content authored)
/// traverse the flow unchanged: Story → Defense → AtomicSave → Results.
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
        bool hasMemoryReward)
    {
        _hasFocusWords = hasFocusWords;
        _hasSymbolLearning = hasSymbolLearning;
        _hasRequiredPractice = hasRequiredPractice;
        _hasContextChallenge = hasContextChallenge;
        _hasMemoryReward = hasMemoryReward;
    }

    public static LevelPhasePlan FromConfig(LevelConfigSO config)
    {
        if (config == null)
            return new LevelPhasePlan(false, false, false, false, false);

        // The challenge-prototype path plays the sequence as a pre-wave tutorial
        // replacement inside the Defense executor; planning phase 6 as well would
        // run the same sequence twice.
        bool contextChallenge = config.challengeSequence != null && !config.challengePrototypeEnabled;

        return new LevelPhasePlan(
            hasFocusWords: config.focusWords != null && config.focusWords.Count > 0,
            hasSymbolLearning: config.learningRequirements != null && config.learningRequirements.Count > 0,
            hasRequiredPractice: config.practiceRequirements != null && config.practiceRequirements.Count > 0,
            hasContextChallenge: contextChallenge,
            hasMemoryReward: config.rewardIds != null && config.rewardIds.Count > 0);
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
