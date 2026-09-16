using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GatedFinaleSlotTests
{
    private static BaybayinCharacterSO Symbol(string id)
    {
        var s = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        s.characterID = id.ToUpperInvariant();
        s.stableId = "symbol." + id;
        return s;
    }

    private static LevelConfigSO Level(bool optIn, params string[] syllables)
    {
        return MultiWordLevel(optIn, syllables);
    }

    /// <summary>
    /// Builds a level from one word per argument, each word given as dot-separated syllables -
    /// <c>MultiWordLevel(true, "ba.ta", "ma.ta")</c> is Level 2's shape. Symbols with the same
    /// syllable share a stableId across words, which is exactly the condition that makes the
    /// last-slot gate a no-op: restoration is by symbol, so one TA carrier fills both TA slots.
    /// </summary>
    private static LevelConfigSO MultiWordLevel(bool optIn, params string[] words)
    {
        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.activeClueCombatEnabled = true;
        config.spawnAssignmentPolicy = new SpawnAssignmentPolicy
        {
            gateFinalSlotToFinalWave = optIn,
        };
        config.focusWords = new List<FocusWordDefinition>();
        for (int wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            var decomposition = new List<SymbolValueReference>();
            foreach (string syllable in words[wordIndex].Split('.'))
                decomposition.Add(new SymbolValueReference { symbol = Symbol(syllable) });

            config.focusWords.Add(new FocusWordDefinition
            {
                stableId = "word.test." + wordIndex,
                decomposition = decomposition,
            });
        }

        return config;
    }

    private static int GatedSlotIndex(IReadOnlyList<SpawnSlot> slots)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index].GateToken == SpawnGateRegistry.FinalWaveReached)
                return index;
        }

        return -1;
    }

    [Test]
    public void OptedInLevel_GatesOnlyItsFinalSlot_OnTheFinalWaveToken()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(Level(true, "ba", "ta", "ma"), null);

            IReadOnlyList<SpawnSlot> slots = coordinator.Slots;
            Assert.AreEqual(3, slots.Count, "test setup: three slots expected.");
            Assert.IsFalse(slots[0].IsGated, "only the FINAL slot may be gated.");
            Assert.IsFalse(slots[1].IsGated, "only the FINAL slot may be gated.");
            Assert.AreEqual(SpawnGateRegistry.FinalWaveReached, slots[2].GateToken,
                "the final slot must be withheld until the final wave, or the level can end early.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LevelThatDidNotOptIn_LeavesEverySlotUngated()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(Level(false, "ba", "ta", "ma"), null);

            foreach (SpawnSlot slot in coordinator.Slots)
                Assert.IsFalse(slot.IsGated, "opting out must leave Levels 1 and 5 exactly as they were.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AuthoredGate_OnTheFinalSlot_IsNeverOverwritten()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            LevelConfigSO config = Level(true, "ba", "ta");
            config.spawnAssignmentPolicy.slotGates.Add(
                new SpawnSlotGate { slotIndex = 1, gateToken = SpawnGateRegistry.AboAshShown });

            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(config, null);

            Assert.AreEqual(SpawnGateRegistry.AboAshShown, coordinator.Slots[1].GateToken,
                "an authored gate is a deliberate per-level choice and must win over the derived one.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SingleSlotLevel_IsNeverGated_SoItCannotBecomeUnwinnable()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(Level(true, "ba"), null);

            Assert.IsFalse(coordinator.Slots[0].IsGated,
                "gating the only slot would withhold the sole win condition.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// The Level 2 defect, as a synthetic shape. BATA + MATA flatten to <c>[ba, ta, ma, ta]</c>.
    /// Gating the literal last slot (<c>ta@MATA</c>) withholds nothing, because
    /// <c>ActiveCluePresenter.ActiveClueRestorationState.Apply</c> restores EVERY slot matching a
    /// defeated carrier's symbol: one TA carrier taken for BATA's slot 1 fills the "gated" slot 3
    /// for free, and the level completes in wave 2 exactly as if the feature were off.
    /// </summary>
    [Test]
    public void LastSymbolRepeats_GatesTheLastUniqueSlot_NotTheLastSlot()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(MultiWordLevel(true, "ba.ta", "ma.ta"), null);

            IReadOnlyList<SpawnSlot> slots = coordinator.Slots;
            Assert.AreEqual(4, slots.Count, "test setup: BATA + MATA is four flattened slots.");

            Assert.IsFalse(slots[3].IsGated,
                "slot 3 is ta@MATA and TA also fills slot 1 of BATA. Restoration is by symbol, so "
                + "gating this slot withholds nothing: any TA carrier fills it, the target text "
                + "completes in wave 2, and the gated finale is inert on the very level it was "
                + "built for.");

            Assert.AreEqual(2, GatedSlotIndex(slots),
                "the gate must land on slot 2 (ma@MATA), the LAST slot whose symbol occurs exactly "
                + "once, because that is the only slot no other carrier can restore.");
            Assert.AreEqual("symbol.ma", slots[2].SymbolStableId,
                "test setup: slot 2 is MA, the level's last uniquely-occurring symbol.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// Levels 3 (<c>[ba, ta, ta, ma]</c>) and 4 (<c>[i, na, a, ma]</c>) both end on a symbol that
    /// occurs once, so the rule change must leave them exactly where they were: on the last slot.
    /// This is the regression guard for the levels that were NOT broken.
    /// </summary>
    [Test]
    public void LastSymbolIsUnique_StillGatesTheLastSlot()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(MultiWordLevel(true, "ba.ta", "ta.ma"), null);

            Assert.AreEqual(3, GatedSlotIndex(coordinator.Slots),
                "TA repeats but MA does not, so the last slot is still the last unique one and "
                + "Level 3's shipped behaviour must be unchanged by the rule change.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// When every symbol repeats, no slot can be withheld: whichever one the gate picked, another
    /// slot's carrier would fill it. Gating anyway would be a silent no-op dressed as a feature, so
    /// the level is left ungated at runtime and rejected at author time by
    /// <c>CampaignConfigValidator.ValidateGatedFinale</c> (see
    /// <c>GatedFinaleValidationTests.LevelWithNoUniquelyOccurringSymbol_IsRejected</c>).
    /// </summary>
    [Test]
    public void NoUniquelyOccurringSymbol_LeavesEverySlotUngated()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(MultiWordLevel(true, "ba.ta", "ta.ba"), null);

            Assert.AreEqual(4, coordinator.Slots.Count, "test setup: four flattened slots.");
            Assert.AreEqual(-1, GatedSlotIndex(coordinator.Slots),
                "every symbol here occurs twice, so no gate can withhold anything. Attaching one "
                + "anyway would claim a guarantee the engine cannot keep; this shape is an "
                + "author-time error instead.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// An authored gate wins even when it sits on the derived slot rather than the last slot: the
    /// derivation now targets slot 2 of <c>[ba, ta, ma, ta]</c>, so that is where the override has
    /// to be respected.
    /// </summary>
    [Test]
    public void AuthoredGate_OnTheDerivedSlot_IsNeverOverwritten()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            LevelConfigSO config = MultiWordLevel(true, "ba.ta", "ma.ta");
            config.spawnAssignmentPolicy.slotGates.Add(
                new SpawnSlotGate { slotIndex = 2, gateToken = SpawnGateRegistry.AboAshShown });

            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(config, null);

            Assert.AreEqual(SpawnGateRegistry.AboAshShown, coordinator.Slots[2].GateToken,
                "an authored gate is a deliberate per-level choice and must win over the derived "
                + "one wherever the derivation lands.");
            Assert.AreEqual(-1, GatedSlotIndex(coordinator.Slots),
                "the derivation must not fall back to some other slot when its target is already "
                + "authored; it yields to the author entirely.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
