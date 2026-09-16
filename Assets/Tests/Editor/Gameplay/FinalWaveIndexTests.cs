using NUnit.Framework;

/// <summary>
/// Covers <see cref="WaveManager.IsFinalWaveIndex"/> directly, as pure EditMode cases. The gate's
/// timing must be provably correct on its own: <see cref="GatedFinaleSlotTests"/> only proves the
/// derived slot exists, not that WaveManager opens it at the right wave, and only a full play
/// session would otherwise exercise the real wave loop.
/// </summary>
public class FinalWaveIndexTests
{
    [Test]
    public void LastIndexOfTheRun_IsFinal()
    {
        Assert.IsTrue(WaveManager.IsFinalWaveIndex(2, 3),
            "index 2 is the last wave of a 3-wave run (0, 1, 2) and must report as final.");
    }

    [Test]
    public void EarlierIndex_IsNotFinal()
    {
        Assert.IsFalse(WaveManager.IsFinalWaveIndex(1, 3),
            "index 1 of a 3-wave run still has a wave after it and must not report as final.");
    }

    [Test]
    public void FirstIndexOfAMultiWaveRun_IsNotFinal()
    {
        Assert.IsFalse(WaveManager.IsFinalWaveIndex(0, 3),
            "index 0 of a 3-wave run is the opener, not the finale; an off-by-one here would open "
            + "the gate on wave 1 and defeat the whole feature.");
    }

    [Test]
    public void SingleWaveRun_ReportsItsOnlyWaveAsFinal()
    {
        Assert.IsTrue(WaveManager.IsFinalWaveIndex(0, 1),
            "a 1-wave run's only wave is also its last, so the gate must still open.");
    }

    [Test]
    public void ZeroExclusiveBound_IsNeverFinal()
    {
        Assert.IsFalse(WaveManager.IsFinalWaveIndex(-1, 0),
            "a run with no waves must not report wave -1 as the finale.");
    }

    [Test]
    public void NegativeExclusiveBound_IsNeverFinal()
    {
        Assert.IsFalse(WaveManager.IsFinalWaveIndex(-2, -1),
            "a negative bound is never a valid run length and must never read as final.");
    }
}
