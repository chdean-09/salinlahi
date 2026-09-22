using NUnit.Framework;

/// <summary>
/// Covers <see cref="SpawnAssignmentPolicy.maxOverflowBatches"/>,
/// <see cref="SpawnAssignmentPolicy.OverflowIsUnbounded"/>, and
/// <see cref="WaveManager.ShouldContinueOverflow"/> directly, as pure EditMode cases. The first two
/// prove the data and the unbounded predicate; the third proves the loop condition itself — the
/// off-by-one and logic-flip risks that would otherwise only be exercised by inspection or a full
/// play session, matching the precedent set by <see cref="FinalWaveIndexTests"/> for the
/// neighbouring finale gate. <see cref="WaveManager.RunRestorationOverflow"/>'s coroutine body
/// (the spawner/coordinator wiring around this condition) is left to a full play session, since it
/// has no pure seam to test without scaffolding whose own correctness would be unverified.
/// </summary>
public class OverflowBudgetTests
{
    [Test]
    public void DefaultPolicy_KeepsTodaysTwelveBatchCap()
    {
        var policy = new SpawnAssignmentPolicy();
        Assert.AreEqual(12, policy.maxOverflowBatches,
            "changing the default silently repaces every level that never opted in.");
    }

    [Test]
    public void NonPositiveBudget_MeansUnbounded()
    {
        var policy = new SpawnAssignmentPolicy { maxOverflowBatches = 0 };
        Assert.IsTrue(policy.OverflowIsUnbounded,
            "0 is how a level says 'keep escorting until the run is won or lost'.");

        policy.maxOverflowBatches = 12;
        Assert.IsFalse(policy.OverflowIsUnbounded);
    }

    [Test]
    public void NegativeBudget_AlsoMeansUnbounded()
    {
        var policy = new SpawnAssignmentPolicy { maxOverflowBatches = -1 };
        Assert.IsTrue(policy.OverflowIsUnbounded,
            "the predicate claims zero-OR-negative; -1 must read as unbounded too, not just 0.");
    }

    [Test]
    public void Bounded_FirstBatchWithinBudget_Continues()
    {
        Assert.IsTrue(WaveManager.ShouldContinueOverflow(batch: 0, unbounded: false, maxBatches: 1),
            "batch 0 of a 1-batch budget has not run yet and must be allowed to run.");
    }

    [Test]
    public void Bounded_BatchAtBudget_Stops()
    {
        Assert.IsFalse(WaveManager.ShouldContinueOverflow(batch: 1, unbounded: false, maxBatches: 1),
            "batch 1 has already consumed the 1-batch budget; continuing here is the off-by-one "
            + "that a '<=' instead of '<' would introduce.");
    }

    [Test]
    public void Bounded_ZeroBudget_StopsImmediately()
    {
        Assert.IsFalse(WaveManager.ShouldContinueOverflow(batch: 0, unbounded: false, maxBatches: 0),
            "a bounded policy with a 0 budget must not run even the first batch. (A 0 budget only "
            + "reaches this bounded path if something upstream failed to also set unbounded=true "
            + "for maxBatches<=0 - this case exists to pin the condition's own behaviour, not to "
            + "endorse that combination.)");
    }

    [Test]
    public void Unbounded_ContinuesFarPastAnyBudget()
    {
        Assert.IsTrue(WaveManager.ShouldContinueOverflow(batch: 10_000, unbounded: true, maxBatches: 1),
            "unbounded must keep going regardless of batch count or budget; an '&&' instead of "
            + "'||' would stop this at batch 1.");
    }

    [Test]
    public void PurePolicyHelper_MatchesTheWaveManagerCompatibilityWrapper()
    {
        Assert.AreEqual(
            WaveManager.ShouldContinueOverflow(batch: 3, unbounded: false, maxBatches: 4),
            WaveTerminalPolicy.ShouldContinueOverflow(batch: 3, unbounded: false, maxBatches: 4));
    }
}
