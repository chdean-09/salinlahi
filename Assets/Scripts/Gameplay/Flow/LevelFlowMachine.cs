using System;
using System.Collections.Generic;

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
    private readonly HashSet<LevelPhase> _completedPhases = new HashSet<LevelPhase>();
    private LevelPhase _phase = LevelPhase.NotStarted;
    private bool _paused;

    public LevelFlowMachine(LevelPhasePlan plan)
    {
        _plan = plan ?? LevelPhasePlan.FromConfig(null);
    }

    public LevelPhase Phase => _phase;

    /// <summary>The plan this machine was built from. Read-only; the machine owns no plan rules.</summary>
    public LevelPhasePlan Plan => _plan;

    /// <summary>
    /// SALIN-220. The phases this run actually completed, in no particular order. The machine
    /// tracks only the CURRENT phase, so without this there is no record of what was finished by
    /// the time the atomic save computes its objective flags.
    /// </summary>
    /// <remarks>
    /// Recorded only on an ACCEPTED report, so a rejected or duplicate report adds nothing. A
    /// phase the plan skipped is never recorded, which is exactly what
    /// <see cref="LevelObjectiveFlagResolver"/> needs: skipped means unauthored, and unauthored
    /// counts as satisfied there rather than here. Defeat and exit record nothing -- they are not
    /// completions. No transition rule is changed by any of this.
    /// </remarks>
    public IReadOnlyCollection<LevelPhase> CompletedPhases => _completedPhases;

    /// <summary>True when <paramref name="phase"/> was completed during this run.</summary>
    public bool HasCompleted(LevelPhase phase) => _completedPhases.Contains(phase);

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

        _completedPhases.Add(phase);
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

        _completedPhases.Add(LevelPhase.Defense);
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
        {
            _completedPhases.Add(LevelPhase.AtomicSave);
            Transition(_plan.NextPlannedAfter(LevelPhase.AtomicSave));
        }

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
