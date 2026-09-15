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

    /// <summary>
    /// The wiring half of the C1 regression: the level-start evaluation must actually be reached
    /// from SpawnAssignmentCoordinator.ApplyLevel, on the same call that resets the gate registry.
    /// The helper being correct is not enough if nobody calls it on a retry.
    /// </summary>
    [Test]
    public void ApplyLevel_OpensTheRosterGateWhenTheCampaignHasAlreadyMetEveryType()
    {
        EnemyIntroductionProgress.ResetForTests();
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            EnemyDataSO abo = Enemy("abo-ng-simula");
            LevelConfigSO config = Config(new[] { abo });
            config.activeClueCombatEnabled = true;

            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = "A";
            symbol.stableId = "symbol.a";
            config.focusWords = new List<FocusWordDefinition>
            {
                new FocusWordDefinition
                {
                    stableId = "word.test",
                    decomposition = new List<SymbolValueReference>
                    {
                        new SymbolValueReference { symbol = symbol, spokenValueId = "value.a" },
                    },
                },
            };

            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();

            // First attempt: the campaign meets the type.
            coordinator.ApplyLevel(config, null);
            Assert.IsFalse(coordinator.Gates.IsOpen(SpawnGateRegistry.Level1RosterMet),
                "an unmet roster must not open the gate at level start");
            Assert.IsTrue(EnemyIntroductionProgress.TryClaimIntroduction(abo),
                "test setup: the type is introduced during the first attempt");

            // Second attempt — the retry. ApplyLevel resets every gate, and no introduction will
            // ever play again because the claim is campaign-wide, so this call is the only chance
            // the gate has to open.
            coordinator.ApplyLevel(config, null);

            Assert.IsTrue(coordinator.Gates.IsOpen(SpawnGateRegistry.Level1RosterMet),
                "on a retry every type is already introduced and no introduction plays, so a gate "
                + "raised only after an introduction never opens and the level cannot be completed");
        }
        finally
        {
            Object.DestroyImmediate(go);
            EnemyIntroductionProgress.ResetForTests();
        }
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

    /// <summary>
    /// C1 regression. Introductions are campaign-wide PlayerPrefs; the gate registry is reset per
    /// level attempt. So on a replay or a retry of Level 1 every type is already introduced, no
    /// introduction plays, and a gate raised only from the end of an introduction never opens —
    /// slot 3 (value.ma) stays withheld and the level cannot be completed. The level-start
    /// evaluation is what closes that, so it is asserted here against a registry that starts closed.
    /// </summary>
    [Test]
    public void TryOpenRosterGate_OpensAtLevelStartWhenEveryTypeIsAlreadyIntroduced()
    {
        EnemyDataSO abo = Enemy("abo");
        EnemyDataSO iligaw = Enemy("iligaw");
        LevelConfigSO config = Config(new[] { abo, iligaw });

        // The state ApplyLevel leaves behind on a retry: every gate closed again.
        var registry = new SpawnGateRegistry();
        registry.Reset();
        Assert.IsFalse(registry.IsOpen(SpawnGateRegistry.Level1RosterMet),
            "test setup: the retry starts with the roster gate closed");

        bool opened = LevelRoster.TryOpenRosterGate(config, _ => true, token => registry.Open(token));

        Assert.IsTrue(opened, "an already-met roster must satisfy the level-start evaluation");
        Assert.IsTrue(registry.IsOpen(SpawnGateRegistry.Level1RosterMet),
            "a replay introduces nothing, so only the level-start evaluation can open this gate; "
            + "without it the level's final slot is withheld forever and the level is unwinnable");
    }

    [Test]
    public void TryOpenRosterGate_LeavesTheGateClosedOnAFirstRun()
    {
        // Negative control for the test above: a genuine first run must still have to earn it.
        EnemyDataSO abo = Enemy("abo");
        EnemyDataSO iligaw = Enemy("iligaw");
        LevelConfigSO config = Config(new[] { abo, iligaw });
        var registry = new SpawnGateRegistry();
        var seen = new HashSet<EnemyDataSO> { abo };

        Assert.IsFalse(
            LevelRoster.TryOpenRosterGate(config, seen.Contains, token => registry.Open(token)));
        Assert.IsFalse(registry.IsOpen(SpawnGateRegistry.Level1RosterMet));

        // And the post-introduction evaluation still works through the same helper.
        seen.Add(iligaw);
        Assert.IsTrue(
            LevelRoster.TryOpenRosterGate(config, seen.Contains, token => registry.Open(token)));
        Assert.IsTrue(registry.IsOpen(SpawnGateRegistry.Level1RosterMet));
    }

    [Test]
    public void TryOpenRosterGate_IsIdempotentSoBothCallSitesMayFire()
    {
        EnemyDataSO abo = Enemy("abo");
        LevelConfigSO config = Config(new[] { abo });
        var registry = new SpawnGateRegistry();

        Assert.IsTrue(LevelRoster.TryOpenRosterGate(config, _ => true, token => registry.Open(token)));
        Assert.IsTrue(LevelRoster.TryOpenRosterGate(config, _ => true, token => registry.Open(token)),
            "level start and post-introduction both evaluate this; the second must not report failure");
        Assert.IsTrue(registry.IsOpen(SpawnGateRegistry.Level1RosterMet));
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
