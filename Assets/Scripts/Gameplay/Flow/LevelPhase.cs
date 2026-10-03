/// <summary>
/// The nine LF-CONTRACT-v2 level phases plus lifecycle states. Order matters:
/// <see cref="LevelPhasePlan"/> and <see cref="LevelFlowMachine"/> advance through
/// the phase members in declaration order, skipping phases the level config does
/// not plan. The three terminal members are never planned; they are entered only
/// through machine reports.
/// </summary>
public enum LevelPhase
{
    NotStarted,
    Story,
    FocusWords,
    SymbolLearning,
    RequiredPractice,
    Defense,
    ContextChallenge,
    MemoryReward,
    AtomicSave,
    Results,

    // Terminal states.
    Completed,
    Defeated,
    Exited,
}
