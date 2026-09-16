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

    /// <summary>
    /// The debut rule's half of the gate. A type that debuts on this level replays its card every
    /// attempt, so on a retry the gate must NOT open at level start on the strength of the
    /// campaign-wide record — the player is about to meet the type again, and MA must wait for it.
    /// </summary>
    [Test]
    public void ApplyLevel_KeepsTheRosterGateClosedOnARetry_WhenTheTypeDebutsOnThisLevel()
    {
        EnemyIntroductionProgress.ResetForTests();
        var go = new GameObject("SpawnAssignmentCoordinator");
        var campaign = ScriptableObject.CreateInstance<CampaignConfigSO>();
        var era = ScriptableObject.CreateInstance<EraConfigSO>();
        var earlier = ScriptableObject.CreateInstance<LevelConfigSO>();
        try
        {
            EnemyDataSO abo = Enemy("abo-ng-simula");
            LevelConfigSO config = Config(new[] { abo });
            config.levelNumber = 1;
            config.stableId = "level.test.debut";
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

            era.order = 1;
            era.levels = new List<LevelConfigSO> { config };
            campaign.eras = new List<EraConfigSO> { era };
            EnemyDebutLookup.CampaignOverrideForTests = campaign;

            Assert.IsTrue(EnemyIntroductionProgress.TryClaimIntroduction(abo),
                "test setup: the type was introduced on a previous attempt");

            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(config, null);

            Assert.IsFalse(coordinator.Gates.IsOpen(SpawnGateRegistry.Level1RosterMet),
                "Abo debuts on this level, so his card replays this attempt and the gate must "
                + "wait for it rather than open at level start off the campaign-wide record");

            // Same save, but the type debuted on an EARLIER level: the record counts, as before.
            earlier.levelNumber = 0;
            earlier.stableId = "level.test.earlier";
            earlier.waves = new List<WaveDefinition>
            {
                new WaveDefinition { enemyTypes = new List<EnemyDataSO> { abo } },
            };
            era.levels.Insert(0, earlier);

            coordinator.ApplyLevel(config, null);
            Assert.IsTrue(coordinator.Gates.IsOpen(SpawnGateRegistry.Level1RosterMet),
                "negative control: a type met on an earlier level does not replay here, so "
                + "the campaign-wide record still opens the gate at level start");
        }
        finally
        {
            EnemyDebutLookup.CampaignOverrideForTests = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(earlier);
            Object.DestroyImmediate(era);
            Object.DestroyImmediate(campaign);
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

    private static BaybayinCharacterSO Symbol(string stableId)
    {
        var c = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        c.characterID = stableId;
        c.stableId = stableId;
        return c;
    }

    /// <summary>
    /// Level 1 as shipped: INA AMA flattens to slots I, NA, A, MA; slot 3 (MA) is gated on
    /// level1_roster_met; and each symbol is embodied by exactly one enemy type, so Mantsa only
    /// ever spawns carrying MA. A gate that waits on Mantsa's introduction can therefore never
    /// open — Mantsa cannot be met until the gate opens — and the level is unwinnable.
    /// </summary>
    private static LevelConfigSO Level1ShapedConfig(
        out EnemyDataSO iligaw, out EnemyDataSO nawalang, out EnemyDataSO abo, out EnemyDataSO mantsa)
    {
        BaybayinCharacterSO i = Symbol("symbol.ei");
        BaybayinCharacterSO na = Symbol("symbol.na");
        BaybayinCharacterSO a = Symbol("symbol.a");
        BaybayinCharacterSO ma = Symbol("symbol.ma");

        iligaw = Enemy("iligaw");
        iligaw.assignedCharacter = i;
        nawalang = Enemy("nawalang-mukha");
        nawalang.assignedCharacter = na;
        abo = Enemy("abo-ng-simula");
        abo.assignedCharacter = a;
        mantsa = Enemy("mantsa");
        mantsa.assignedCharacter = ma;

        LevelConfigSO config = Config(new[] { iligaw, nawalang, abo, mantsa });
        config.focusWords = new List<FocusWordDefinition>
        {
            new FocusWordDefinition
            {
                stableId = "word.ina",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = i },
                    new SymbolValueReference { symbol = na },
                },
            },
            new FocusWordDefinition
            {
                stableId = "word.ama",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = a },
                    new SymbolValueReference { symbol = ma },
                },
            },
        };
        config.spawnAssignmentPolicy = new SpawnAssignmentPolicy();
        config.spawnAssignmentPolicy.slotGates = new List<SpawnSlotGate>
        {
            new SpawnSlotGate { slotIndex = 3, gateToken = SpawnGateRegistry.Level1RosterMet },
        };
        return config;
    }

    [Test]
    public void TryOpenRosterGate_DoesNotWaitOnTheTypeThatCanOnlySpawnBehindTheGate()
    {
        LevelConfigSO config = Level1ShapedConfig(
            out EnemyDataSO iligaw, out EnemyDataSO nawalang, out EnemyDataSO abo, out EnemyDataSO mantsa);
        var registry = new SpawnGateRegistry();
        var seen = new HashSet<EnemyDataSO> { iligaw, nawalang, abo };

        bool opened = LevelRoster.TryOpenRosterGate(config, seen.Contains, token => registry.Open(token));

        Assert.IsTrue(opened,
            "Mantsa embodies MA and MA is the gated slot, so Mantsa cannot be introduced until the "
            + "gate opens; waiting on it deadlocks the level. The gate must open once every OTHER "
            + "roster type has been met.");
        Assert.IsTrue(registry.IsOpen(SpawnGateRegistry.Level1RosterMet));
        Assert.IsFalse(seen.Contains(mantsa), "test setup: Mantsa was never introduced");
    }

    [Test]
    public void TryOpenRosterGate_StillWaitsOnEveryTypeThatCanSpawnUngated()
    {
        LevelConfigSO config = Level1ShapedConfig(
            out EnemyDataSO iligaw, out EnemyDataSO nawalang, out _, out _);
        var registry = new SpawnGateRegistry();
        var seen = new HashSet<EnemyDataSO> { iligaw, nawalang };

        Assert.IsFalse(
            LevelRoster.TryOpenRosterGate(config, seen.Contains, token => registry.Open(token)),
            "Abo carries A, an ungated slot, so he can be met before the gate and must be.");
        Assert.IsFalse(registry.IsOpen(SpawnGateRegistry.Level1RosterMet));
    }

    [Test]
    public void BuildRosterGateRoster_ExcludesOnlyTypesWhoseSymbolIsBehindTheGate()
    {
        LevelConfigSO config = Level1ShapedConfig(
            out EnemyDataSO iligaw, out EnemyDataSO nawalang, out EnemyDataSO abo, out EnemyDataSO mantsa);

        List<EnemyDataSO> roster = LevelRoster.BuildRosterGateRoster(config);

        CollectionAssert.AreEquivalent(new[] { iligaw, nawalang, abo }, roster);
        CollectionAssert.DoesNotContain(roster, mantsa);
    }

    [Test]
    public void BuildRosterGateRoster_IsTheWholeRosterWhenNothingIsGated()
    {
        LevelConfigSO config = Level1ShapedConfig(
            out EnemyDataSO iligaw, out EnemyDataSO nawalang, out EnemyDataSO abo, out EnemyDataSO mantsa);
        config.spawnAssignmentPolicy.slotGates.Clear();

        CollectionAssert.AreEquivalent(
            new[] { iligaw, nawalang, abo, mantsa }, LevelRoster.BuildRosterGateRoster(config));
    }
}
