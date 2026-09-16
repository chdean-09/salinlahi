using NUnit.Framework;

/// <summary>
/// Covers <see cref="SpawnAssignmentPolicy.maxOverflowBatches"/> and
/// <see cref="SpawnAssignmentPolicy.OverflowIsUnbounded"/> directly, as pure EditMode cases. These
/// prove the data and the unbounded predicate; they do not exercise
/// <see cref="WaveManager.RunRestorationOverflow"/>'s loop itself, which needs a spawner, a level
/// and a coordinator to run at all and is left to a full play session, matching the precedent set
/// by <see cref="FinalWaveIndexTests"/> for the neighbouring finale gate.
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
}
