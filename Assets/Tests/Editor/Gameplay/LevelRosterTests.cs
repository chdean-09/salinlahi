using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class LevelRosterTests
{
    private static EnemyDataSO Enemy(string id, bool decoy = false, bool suppress = false,
        string display = null)
    {
        var d = ScriptableObject.CreateInstance<EnemyDataSO>();
        d.enemyID = id;
        d.displayName = display ?? id;
        d.isDecoy = decoy;
        d.suppressDiscovery = suppress;
        return d;
    }

    private static LevelConfigSO Config(params EnemyDataSO[][] wavesOfTypes)
    {
        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.waves = new List<WaveDefinition>();
        foreach (EnemyDataSO[] types in wavesOfTypes)
        {
            var wave = new WaveDefinition();
            wave.enemyTypes = new List<EnemyDataSO>(types);
            config.waves.Add(wave);
        }
        return config;
    }

    [Test]
    public void BuildIntroducibleRoster_DeduplicatesAcrossWaves()
    {
        EnemyDataSO abo = Enemy("abo");
        EnemyDataSO iligaw = Enemy("iligaw");

        List<EnemyDataSO> roster = LevelRoster.BuildIntroducibleRoster(
            Config(new[] { abo, iligaw }, new[] { abo, iligaw }));

        Assert.AreEqual(2, roster.Count);
    }

    [Test]
    public void BuildIntroducibleRoster_ExcludesDecoysSuppressedAndUnnamed()
    {
        EnemyDataSO real = Enemy("real");
        List<EnemyDataSO> roster = LevelRoster.BuildIntroducibleRoster(Config(new[]
        {
            real,
            Enemy("decoy", decoy: true),
            Enemy("hidden", suppress: true),
            Enemy("nameless", display: "   "),
            null,
        }));

        Assert.AreEqual(1, roster.Count);
        Assert.AreSame(real, roster[0]);
    }

    [Test]
    public void AllIntroduced_IsFalseUntilEveryRosterTypeIsRecorded()
    {
        EnemyDataSO abo = Enemy("abo");
        EnemyDataSO iligaw = Enemy("iligaw");
        var roster = new List<EnemyDataSO> { abo, iligaw };
        var seen = new HashSet<EnemyDataSO> { abo };

        Assert.IsFalse(LevelRoster.AllIntroduced(roster, seen.Contains));
        seen.Add(iligaw);
        Assert.IsTrue(LevelRoster.AllIntroduced(roster, seen.Contains));
    }

    [Test]
    public void AllIntroduced_IsFalseForAnEmptyRoster()
    {
        // A level with no introducible types must not open a gate that is standing in for
        // "the player has met everything" — that would silently ungate slot 3 on a config error.
        Assert.IsFalse(LevelRoster.AllIntroduced(new List<EnemyDataSO>(), _ => true));
        Assert.IsFalse(LevelRoster.AllIntroduced(null, _ => true));
    }

    [Test]
    public void RosterGate_StaysClosedWhileAnyTypeIsUnintroduced()
    {
        EnemyDataSO abo = Enemy("abo");
        EnemyDataSO iligaw = Enemy("iligaw");
        var registry = new SpawnGateRegistry();
        var seen = new HashSet<EnemyDataSO> { abo };
        var roster = new List<EnemyDataSO> { abo, iligaw };

        if (LevelRoster.AllIntroduced(roster, seen.Contains))
            registry.Open(SpawnGateRegistry.Level1RosterMet);

        Assert.IsFalse(registry.IsOpen(SpawnGateRegistry.Level1RosterMet));

        seen.Add(iligaw);
        if (LevelRoster.AllIntroduced(roster, seen.Contains))
            registry.Open(SpawnGateRegistry.Level1RosterMet);

        Assert.IsTrue(registry.IsOpen(SpawnGateRegistry.Level1RosterMet));
    }
}
