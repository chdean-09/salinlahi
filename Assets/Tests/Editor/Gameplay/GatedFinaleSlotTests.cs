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
        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.activeClueCombatEnabled = true;
        config.spawnAssignmentPolicy = new SpawnAssignmentPolicy
        {
            gateFinalSlotToFinalWave = optIn,
        };
        var decomposition = new List<SymbolValueReference>();
        foreach (string syllable in syllables)
            decomposition.Add(new SymbolValueReference { symbol = Symbol(syllable) });
        config.focusWords = new List<FocusWordDefinition>
        {
            new FocusWordDefinition { stableId = "word.test", decomposition = decomposition },
        };
        return config;
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
}
