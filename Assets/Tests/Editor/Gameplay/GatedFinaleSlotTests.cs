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
    public void LastSymbolRepeats_StillGatesTheLastSlot()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(MultiWordLevel(true, "ba.ta", "ma.ta"), null);

            IReadOnlyList<SpawnSlot> slots = coordinator.Slots;
            Assert.AreEqual(4, slots.Count, "test setup: BATA + MATA is four flattened slots.");

            // Until 2026-09-17 this asserted the OPPOSITE: the gate had to avoid slot 3 and land on
            // slot 2 (ma@MATA), because restoration was by symbol and any TA carrier filled both TA
            // slots at once -- so gating ta@MATA withheld nothing and Level 2 finished early. One
            // carrier now restores one slot, so the last slot is genuinely withholdable and the
            // level ends on the syllable it is teaching.
            Assert.AreEqual(3, GatedSlotIndex(slots),
                "the gate lands on the last slot, ta@MATA, even though TA also fills slot 1. "
                + "Per-slot restoration means BATA's TA cannot fill MATA's.");
            Assert.AreEqual("symbol.ta", slots[3].SymbolStableId,
                "test setup: slot 3 is the repeated TA -- the case this fixture exists for.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    public void LastSymbolIsUnique_StillGatesTheLastSlot()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(MultiWordLevel(true, "ba.ta", "ta.ma"), null);

            Assert.AreEqual(3, GatedSlotIndex(coordinator.Slots),
                "The last slot, as always. This case used to be the interesting one -- the last "
                + "slot happening to also be the last UNIQUE slot -- and is now simply the rule.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// A level whose every symbol repeats used to be left ungated: whichever slot the gate picked,
    /// another slot's carrier filled it, so attaching one would have claimed a guarantee the engine
    /// could not keep. It was an author-time error too
    /// (<c>CampaignConfigValidator.ValidateGatedFinale</c>).
    ///
    /// <para>
    /// Per-slot restoration removed the problem entirely: no slot can be filled by another slot's
    /// carrier, so this shape gates like any other and the validator case was retired with it.
    /// </para>
    /// </summary>
    [Test]
    public void EverySymbolRepeating_IsNowGatedLikeAnyOtherLevel()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(MultiWordLevel(true, "ba.ta", "ta.ba"), null);

            Assert.AreEqual(4, coordinator.Slots.Count, "test setup: four flattened slots.");
            Assert.AreEqual(3, GatedSlotIndex(coordinator.Slots),
                "Both BA and TA appear twice. That used to make the level ungateable; it is now "
                + "an ordinary level whose last slot is withheld.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// An authored gate wins where the derivation would have landed: slot 3, the last slot of
    /// <c>[ba, ta, ma, ta]</c>.
    /// </summary>
    [Test]
    public void AuthoredGate_OnTheDerivedSlot_IsNeverOverwritten()
    {
        var go = new GameObject("SpawnAssignmentCoordinator");
        try
        {
            LevelConfigSO config = MultiWordLevel(true, "ba.ta", "ma.ta");
            config.spawnAssignmentPolicy.slotGates.Add(
                new SpawnSlotGate { slotIndex = 3, gateToken = SpawnGateRegistry.AboAshShown });

            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(config, null);

            Assert.AreEqual(SpawnGateRegistry.AboAshShown, coordinator.Slots[3].GateToken,
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
