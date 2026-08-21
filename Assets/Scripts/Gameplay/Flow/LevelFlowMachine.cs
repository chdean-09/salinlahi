using System;

/// <summary>
/// Pure-C# LF-CONTRACT-v2 phase machine. The single choke point for level-flow
/// state: completion reports for the wrong phase, duplicate reports, and reports
/// after a terminal state are rejected without a state change. Defense systems can
/// only ever report defense completion, and Results is reachable exclusively
/// through an accepted atomic save (<see cref="ReportSaveResult"/>).
/// </summary>
public sealed class LevelFlowMachine
{
    private readonly LevelPhasePlan _plan;
    private LevelPhase _phase = LevelPhase.NotStarted;
    private bool _paused;

    public LevelFlowMachine(LevelPhasePlan plan)
    {
        _plan = plan ?? LevelPhasePlan.FromConfig(null);
    }

    public LevelPhase Phase => _phase;

    public bool IsTerminal =>
        _phase == LevelPhase.Completed
        || _phase == LevelPhase.Defeated
        || _phase == LevelPhase.Exited;

    public bool IsPaused => _paused;

    /// <summary>Raised after every state change with (previousPhase, newPhase).</summary>
    public event Action<LevelPhase, LevelPhase> PhaseChanged;

    public void Begin()
    {
        if (_phase != LevelPhase.NotStarted)
            return;

        Transition(_plan.NextPlannedAfter(LevelPhase.NotStarted));
    }

    public bool ReportPhaseComplete(LevelPhase phase)
    {
        if (IsTerminal || _phase == LevelPhase.NotStarted || phase != _phase)
            return false;

        // AtomicSave advances only through ReportSaveResult; a bare completion
        // would let Results open without a committed save.
        if (phase == LevelPhase.AtomicSave)
            return false;

        Transition(_plan.NextPlannedAfter(phase));
        return true;
    }

    /// <summary>
    /// The only way defense systems influence the flow. They can never mark the
    /// level complete or write campaign rewards.
    /// </summary>
    public bool ReportDefenseComplete()
    {
        if (_phase != LevelPhase.Defense)
            return false;

        Transition(_plan.NextPlannedAfter(LevelPhase.Defense));
        return true;
    }

    /// <summary>
    /// Legal only during AtomicSave. An accepted save advances to Results; a
    /// rejected save holds the machine in AtomicSave for the retry loop.
    /// </summary>
    public bool ReportSaveResult(bool accepted)
    {
        if (_phase != LevelPhase.AtomicSave)
            return false;

        if (accepted)
            Transition(_plan.NextPlannedAfter(LevelPhase.AtomicSave));

        return true;
    }

    public bool ReportDefeat()
    {
        if (IsTerminal)
            return false;

        Transition(LevelPhase.Defeated);
        return true;
    }

    public bool RequestExit()
    {
        if (IsTerminal)
            return false;

        Transition(LevelPhase.Exited);
        return true;
    }

    public void NotifyPaused()
    {
        if (IsTerminal)
            return;

        _paused = true;
    }

    public void NotifyResumed()
    {
        _paused = false;
    }

    private void Transition(LevelPhase next)
    {
        LevelPhase previous = _phase;
        _phase = next;
        if (IsTerminal)
            _paused = false;

        PhaseChanged?.Invoke(previous, next);
    }
}
