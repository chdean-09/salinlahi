# Gated Finale (Levels 2-4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hold one syllable back until the final wave so Levels 2-4 cannot end early, and keep escort pressure on until it is resolved.

**Architecture:** Reuses the shipped slot-gate mechanism (`SpawnSlotGate` / `SpawnSlot.GateToken` / `SpawnAssignmentDirector.IsGateOpen` / `SpawnAssignmentCoordinator.OpenGate`) that Level 1 already uses. The gated slot is DERIVED at level start from the focus words rather than authored as a literal index, so content changes cannot desynchronise it. `WaveManager` opens the token as the last wave begins; the existing restoration-overflow loop supplies the escort pressure.

**Tech Stack:** Unity 6000.3.9f1, C#, NUnit via Unity Test Framework (EditMode + PlayMode), ScriptableObject assets in Unity YAML.

**Spec:** `docs/design/gated-finale-levels-2-4.md`

## Global Constraints

- Scope is Levels 2, 3, 4 only. Level 1 and Level 5 behaviour must not change.
- Section 4 of the spec (introductions replaying every run) is OUT OF SCOPE for this plan.
- An authored `slotGates` entry must always win over the derived gate — Level 1 authors its own and must be untouched.
- EditMode baseline is 1309 total / **7 failed**; PlayMode is 212 total / **3 failed**. Any task that changes that set must name each test it adds or removes.
- Never trust Unity's batchmode exit code. Confirm the results XML exists and parse it.
- A Unity Editor may hold the main checkout. Run batchmode in a detached worktree, then restore `Assets/Resources/Fonts/TutorialFont.asset` and any `Level*_Config.asset` the run reserialised.
- Commit messages: `type(scope): subject`. No `Co-Authored-By` trailer, no generated-with footer.

---

### Task 1: Derive the final-slot gate in the coordinator

**Files:**
- Modify: `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnGateRegistry.cs`
- Modify: `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentPolicy.cs:155` (beside `allowFinalWaveOverflow`)
- Modify: `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentCoordinator.cs:173-199` (`BuildSlots`)
- Test: `Assets/Tests/Editor/Gameplay/GatedFinaleSlotTests.cs` (create)

**Interfaces:**
- Produces: `SpawnGateRegistry.FinalWaveReached` (const string `"final_wave_reached"`); `SpawnAssignmentPolicy.gateFinalSlotToFinalWave` (public bool, default `false`). Task 2 opens the token; Task 3 validates the opt-in.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/Gameplay/GatedFinaleSlotTests.cs`. Mirrors `LevelRosterTests`' construction idiom (GameObject + AddComponent + ApplyLevel).

```csharp
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
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run EditMode filtered to `GatedFinaleSlotTests` in a detached worktree. Expected: compile failure — `gateFinalSlotToFinalWave`, `FinalWaveReached` and `SpawnAssignmentCoordinator.Slots` do not exist yet.

If `SpawnAssignmentCoordinator` has no public `Slots` accessor, add one in Step 3:
```csharp
/// <summary>The flattened target slots, exposed read-only so gating is testable without reflection.</summary>
public IReadOnlyList<SpawnSlot> Slots => _slots;
```

- [ ] **Step 3: Add the token**

In `SpawnGateRegistry.cs`, after `Level1RosterMet`:

```csharp
    /// <summary>
    /// Opened by WaveManager as the last wave of a run begins. Levels that opt into
    /// <see cref="SpawnAssignmentPolicy.gateFinalSlotToFinalWave"/> withhold their final slot on
    /// this token, so the text cannot be completed before the finale and the level always plays its
    /// full arc. Unlike <see cref="AboAshShown"/> this is not a narrative beat - it is a pacing
    /// gate, which is why the slot it applies to is derived rather than authored.
    /// </summary>
    public const string FinalWaveReached = "final_wave_reached";
```

- [ ] **Step 4: Add the policy opt-in**

In `SpawnAssignmentPolicy.cs`, directly after `allowFinalWaveOverflow`:

```csharp
    /// <summary>
    /// Withholds this level's LAST flattened slot until the final wave begins, so the restoration
    /// cannot complete early and the level always reaches its finale.
    ///
    /// The slot is derived at level start, never authored: an authored index would couple the gate
    /// to content position, and re-authoring a focus word would silently move the gate mid-word.
    /// An authored <see cref="slotGates"/> entry for that slot still wins.
    /// </summary>
    public bool gateFinalSlotToFinalWave = false;
```

- [ ] **Step 5: Derive the gate in BuildSlots**

At the end of `SpawnAssignmentCoordinator.BuildSlots`, after the word loop closes:

```csharp
        ApplyDerivedFinalSlotGate(policy);
    }

    /// <summary>
    /// Withholds the final slot on <see cref="SpawnGateRegistry.FinalWaveReached"/> when the level
    /// opts in. Derived rather than authored so content edits cannot move the gate off the finale;
    /// skipped for a single-slot level, which would otherwise withhold its own win condition.
    /// <see cref="SpawnSlot.GateToken"/> is readonly, so the tail entry is replaced, not mutated.
    /// </summary>
    private void ApplyDerivedFinalSlotGate(SpawnAssignmentPolicy policy)
    {
        if (policy == null || !policy.gateFinalSlotToFinalWave || _slots.Count < 2)
            return;

        int last = _slots.Count - 1;
        SpawnSlot tail = _slots[last];
        if (tail.IsGated)
            return;

        _slots[last] = new SpawnSlot(
            tail.SymbolStableId, tail.WordStableId, tail.SlotIndexInWord,
            SpawnGateRegistry.FinalWaveReached);
    }
```

- [ ] **Step 6: Run the tests and confirm they pass**

All four `GatedFinaleSlotTests` PASS. Then run the full EditMode suite and diff against the 7-failure baseline: NEW none, GONE none.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnGateRegistry.cs \
        Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentPolicy.cs \
        Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentCoordinator.cs \
        Assets/Tests/Editor/Gameplay/GatedFinaleSlotTests.cs \
        Assets/Tests/Editor/Gameplay/GatedFinaleSlotTests.cs.meta
git commit -m "feat(wave): derive a final-slot gate so an opted-in level cannot finish early"
```

---

### Task 2: Open the token as the final wave begins

**Files:**
- Modify: `Assets/Scripts/Gameplay/Wave/WaveManager.cs` (the wave loop, just after `EventBus.RaiseWaveStarted(waveIndex)` near :659)
- Test: `Assets/Tests/PlayMode/Gameplay/GatedFinaleWaveTests.cs` (create)

**Interfaces:**
- Consumes: `SpawnGateRegistry.FinalWaveReached`, `SpawnAssignmentCoordinator.OpenGate(string)` (already public, :202).

- [ ] **Step 1: Write the failing test**

PlayMode, because the wave loop is a coroutine.

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public sealed class GatedFinaleWaveTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator FinalWaveReached_IsClosedBeforeTheLastWaveAndOpenAfterIt()
        {
            var go = new GameObject("SpawnAssignmentCoordinator");
            _objectsToDestroy.Add(go);
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();

            Assert.IsFalse(coordinator.Gates.IsOpen(SpawnGateRegistry.FinalWaveReached),
                "the finale gate must start closed or the slot is fillable from wave 1.");

            coordinator.OpenGate(SpawnGateRegistry.FinalWaveReached);
            yield return null;

            Assert.IsTrue(coordinator.Gates.IsOpen(SpawnGateRegistry.FinalWaveReached),
                "the finale gate must open so the withheld syllable can finally be restored.");
        }
    }
}
```

- [ ] **Step 2: Run it and confirm it fails**

Expected: compile failure on `SpawnGateRegistry.FinalWaveReached` only if Task 1 is not yet merged; otherwise it PASSES immediately, because it exercises the gate registry rather than `WaveManager`. **If it passes at this step, that is expected** — it is a contract guard for Task 1's token, and the `WaveManager` wiring is covered by the in-Editor check in Task 6, which is the only place the real wave loop runs against a real level.

- [ ] **Step 3: Wire the opening into the wave loop**

In `RunAllWavesRoutine`, immediately after `EventBus.RaiseWaveStarted(waveIndex);`:

```csharp
            // The finale gate opens as the LAST wave starts, so a level that withheld its final
            // slot becomes completable exactly here and not before. Resolved against the same
            // exclusive bound the overflow pass uses, so a segmented run gates per segment rather
            // than once per level.
            if (waveIndex == lastWaveIndexExclusive - 1)
            {
                SpawnAssignmentCoordinator gateCoordinator =
                    FindFirstObjectByType<SpawnAssignmentCoordinator>(FindObjectsInactive.Include);
                if (gateCoordinator != null)
                    gateCoordinator.OpenGate(SpawnGateRegistry.FinalWaveReached);
            }
```

If the local holding the resolved exclusive bound is not named `lastWaveIndexExclusive` in that scope, use whatever local is passed to `RunRestorationOverflow(...)` further down the same method — they must be the same value.

- [ ] **Step 4: Run the tests and confirm they pass**

`GatedFinaleWaveTests` PASS. Full PlayMode suite diffed against the 3-failure baseline: NEW none, GONE none.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Gameplay/Wave/WaveManager.cs \
        Assets/Tests/PlayMode/Gameplay/GatedFinaleWaveTests.cs \
        Assets/Tests/PlayMode/Gameplay/GatedFinaleWaveTests.cs.meta
git commit -m "feat(wave): open the finale gate as the last wave begins"
```

---

### Task 3: Reject an unwinnable opt-in in the validator

**Files:**
- Modify: `Assets/Scripts/Data/Validation/ContentValidationIssue.cs` (beside `WaveRosterNarrowsRestoration`)
- Modify: `Assets/Scripts/Data/Validation/CampaignConfigValidator.cs` (new check + registration beside `ValidateCombatWaveRoster` in the per-level dispatch)
- Test: `Assets/Tests/Editor/Data/GatedFinaleValidationTests.cs` (create)

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    [TestFixture]
    public sealed class GatedFinaleValidationTests
    {
        private static List<string> Offenders(CampaignConfigSO campaign)
        {
            return CampaignConfigValidator.Validate(campaign)
                .Where(i => i.Code == ContentValidationCode.GatedFinaleUnwinnable)
                .Select(i => $"{i.Code} @ {i.Path}: {i.Message}")
                .ToList();
        }

        [Test]
        public void LevelGatingItsOnlySlot_IsRejected()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.activeClueCombatEnabled = true;
            level.spawnAssignmentPolicy ??= new SpawnAssignmentPolicy();
            level.spawnAssignmentPolicy.gateFinalSlotToFinalWave = true;

            // Collapse the level to a single slot: gating it would withhold the win condition.
            BaybayinCharacterSO only = fixture.Campaign.symbols[0];
            level.focusWords[0].decomposition = new List<SymbolValueReference>
            {
                new SymbolValueReference { symbol = only },
            };
            for (int i = level.focusWords.Count - 1; i >= 1; i--)
                level.focusWords.RemoveAt(i);

            Assert.IsNotEmpty(Offenders(fixture.Campaign),
                "a level that gates its only slot can never be completed and must not validate.");
        }

        [Test]
        public void ShippedCampaign_HasNoUnwinnableGatedFinale()
        {
            var campaign = UnityEditor.AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
                "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset");
            Assert.IsNotNull(campaign);
            Assert.IsEmpty(Offenders(campaign));
        }
    }
}
```

- [ ] **Step 2: Run it and confirm it fails**

Expected: compile failure — `ContentValidationCode.GatedFinaleUnwinnable` does not exist.

- [ ] **Step 3: Add the code constant**

In `ContentValidationIssue.cs`, after `WaveRosterNarrowsRestoration`:

```csharp
    public const string GatedFinaleUnwinnable = "GATED_FINALE_UNWINNABLE";
```

- [ ] **Step 4: Add the check and register it**

In `CampaignConfigValidator.cs`, beside `ValidateCombatWaveRoster`:

```csharp
    /// <summary>
    /// A level that withholds its final slot until the final wave needs at least two slots and at
    /// least one wave. With one slot the gate withholds the sole win condition; with no waves the
    /// token never opens. Either shape is an unwinnable level, so it fails at author time.
    /// </summary>
    private static void ValidateGatedFinale(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        SpawnAssignmentPolicy policy = level.spawnAssignmentPolicy;
        if (policy == null || !policy.gateFinalSlotToFinalWave)
            return;

        int slotCount = 0;
        if (level.focusWords != null)
        {
            for (int focusIndex = 0; focusIndex < level.focusWords.Count; focusIndex++)
            {
                FocusWordDefinition focus = level.focusWords[focusIndex];
                if (focus?.decomposition == null)
                    continue;

                for (int index = 0; index < focus.decomposition.Count; index++)
                {
                    if (focus.decomposition[index]?.symbol != null)
                        slotCount++;
                }
            }
        }

        if (slotCount < 2)
        {
            AddContentIssue(issues, ContentValidationCode.GatedFinaleUnwinnable,
                path + ".spawnAssignmentPolicy.gateFinalSlotToFinalWave",
                "This level withholds its final slot until the final wave but has "
                + slotCount + " slot(s). Gating the only slot withholds the level's sole win "
                + "condition, so it could never be completed.", level);
        }

        int waveCount = level.waves != null ? level.waves.Count : 0;
        if (waveCount < 1)
        {
            AddContentIssue(issues, ContentValidationCode.GatedFinaleUnwinnable,
                path + ".spawnAssignmentPolicy.gateFinalSlotToFinalWave",
                "This level withholds its final slot until the final wave but authors no waves, "
                + "so the gate would never open.", level);
        }
    }
```

Register it in the per-level dispatch, directly after `ValidateCombatWaveRoster(level, path, issues);`:

```csharp
                ValidateGatedFinale(level, path, issues);
```

- [ ] **Step 5: Run the tests and confirm they pass**

Both PASS. Full EditMode suite diffed against baseline: NEW none, GONE none.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Data/Validation/ContentValidationIssue.cs \
        Assets/Scripts/Data/Validation/CampaignConfigValidator.cs \
        Assets/Tests/Editor/Data/GatedFinaleValidationTests.cs \
        Assets/Tests/Editor/Data/GatedFinaleValidationTests.cs.meta
git commit -m "feat(validation): reject a gated finale that could never be completed"
```

---

### Task 4: Make the overflow cap a policy field

**Files:**
- Modify: `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentPolicy.cs`
- Modify: `Assets/Scripts/Gameplay/Wave/WaveManager.cs:49` (`MaxOverflowBatches`) and `RunRestorationOverflow` (:745 loop bound)
- Test: `Assets/Tests/Editor/Gameplay/OverflowBudgetTests.cs` (create)

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

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
```

- [ ] **Step 2: Run it and confirm it fails**

Expected: compile failure — `maxOverflowBatches` / `OverflowIsUnbounded` do not exist.

- [ ] **Step 3: Add the field**

In `SpawnAssignmentPolicy.cs`, after `allowFinalWaveOverflow` / `gateFinalSlotToFinalWave`:

```csharp
    /// <summary>
    /// How many escort batches restoration overflow may spawn before giving up. 12 is the historical
    /// hardcoded bound and stays the default, so levels that never opt in are unchanged.
    ///
    /// Zero or less means unbounded: escorts keep arriving until the text is restored or the run
    /// ends. Unbounded is per level and visible in data rather than a blanket `while(true)`, because
    /// the bound is also what surfaces a starved director instead of hanging the run.
    /// </summary>
    public int maxOverflowBatches = 12;

    /// <summary>True when overflow should continue until the run is won or lost.</summary>
    public bool OverflowIsUnbounded => maxOverflowBatches <= 0;
```

- [ ] **Step 4: Consume it in WaveManager**

Delete the `MaxOverflowBatches` const at :49. In `RunRestorationOverflow`, replace the `for` bound:

```csharp
        SpawnAssignmentPolicy overflowPolicy =
            _levelConfig != null ? _levelConfig.spawnAssignmentPolicy : null;
        bool unbounded = overflowPolicy != null && overflowPolicy.OverflowIsUnbounded;
        int maxBatches = overflowPolicy != null ? overflowPolicy.maxOverflowBatches : 12;

        // Bounded by default so a broken gate cannot spin forever; unbounded when a level asks for
        // escorts to keep coming. Both still exit on CanContinueRun() and on WantsOverflow, so an
        // unbounded run still ends when the text completes or the player runs out of hearts.
        for (int batch = 0; unbounded || batch < maxBatches; batch++)
```

Leave the post-loop warning at :763 intact — it now reports only a bounded level that exhausted its budget.

- [ ] **Step 5: Run the tests and confirm they pass**

Both PASS. Full EditMode and PlayMode suites diffed against baseline: NEW none, GONE none.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentPolicy.cs \
        Assets/Scripts/Gameplay/Wave/WaveManager.cs \
        Assets/Tests/Editor/Gameplay/OverflowBudgetTests.cs \
        Assets/Tests/Editor/Gameplay/OverflowBudgetTests.cs.meta
git commit -m "feat(wave): make the restoration overflow budget a per-level policy"
```

---

### Task 5: Four-wave curve and the Levels 2-4 opt-in

**Files:**
- Create: `Assets/ScriptableObjects/Levels/WaveCurves/Curve_Ugat_Short.asset` (+ `.meta`)
- Modify: `Assets/ScriptableObjects/Levels/Level2_Config.asset`, `Level3_Config.asset`, `Level4_Config.asset`
- Modify: `Assets/Tests/Editor/Data/UgatWaveCurveMigrationTests.cs`

- [ ] **Step 1: Author the curve asset**

Copy `Assets/ScriptableObjects/Levels/WaveCurves/Curve_Ugat.asset` to `Curve_Ugat_Short.asset`, change `waveCount: 5` to `waveCount: 4`, keep every ramp field identical. Create its `.meta` by cloning the sibling's and replacing the `guid` with a fresh 32-hex value; confirm the new guid appears nowhere else:

```bash
grep -rl "<new-guid>" Assets | wc -l   # expect 1 — its own .meta
```

- [ ] **Step 2: Point Levels 2-4 at it and opt them in**

In each of `Level2_Config.asset`, `Level3_Config.asset`, `Level4_Config.asset`:
- set `waveCurve` to the new guid,
- under `spawnAssignmentPolicy`, set `gateFinalSlotToFinalWave: 1` and `maxOverflowBatches: 0`.

Edit the YAML in place. Do NOT re-run any authoring tool — `CreateAsset` reissues GUIDs and silently unwires references.

- [ ] **Step 3: Re-pin the migration tests**

In `UgatWaveCurveMigrationTests.cs`, `MigratedUgatLevel_ResolvesFiveWavesCarryingItsWholeRoster` asserts `Assert.AreEqual(5, waves.Count)` and `CollectionAssert.AreEqual(new[] { 2, 4, 5, 6, 7 }, ...)` for levels 2,3,4,5. Split it: levels 2,3,4 expect four waves with enemy counts `{ 2, 4, 5, 7 }` (verify against `WaveCurveExpander.EnemyCountAt` with `waveCount: 4` — compute it, do not guess); level 5 keeps the five-wave expectation. Rename the five-wave case to `Level5_ResolvesFiveWavesCarryingItsWholeRoster` and add `MigratedShortUgatLevel_ResolvesFourWavesCarryingItsWholeRoster` for 2,3,4.

- [ ] **Step 4: Run the suites**

Full EditMode. Expected changes to the failure set: NONE. The two renamed/added migration cases must appear as PASSED by name in the results XML — confirm by name, not by count.

- [ ] **Step 5: Commit**

```bash
git add Assets/ScriptableObjects/Levels/WaveCurves/Curve_Ugat_Short.asset \
        Assets/ScriptableObjects/Levels/WaveCurves/Curve_Ugat_Short.asset.meta \
        Assets/ScriptableObjects/Levels/Level2_Config.asset \
        Assets/ScriptableObjects/Levels/Level3_Config.asset \
        Assets/ScriptableObjects/Levels/Level4_Config.asset \
        Assets/Tests/Editor/Data/UgatWaveCurveMigrationTests.cs
git commit -m "feat(level): run Levels 2-4 on a four-wave curve with a gated finale"
```

---

### Task 6: Prove it in the running game

**Files:** none — verification only.

- [ ] **Step 1: Negative control**

Temporarily set `gateFinalSlotToFinalWave: 0` on Level 2 and run `GatedFinaleSlotTests`. The opt-in test MUST fail. Restore the flag. A gate that never closes is indistinguishable from a passing test without this.

- [ ] **Step 2: Play Level 2 end to end in the Editor**

Gates, all of which must hold:
- The level does NOT end before wave 4, even with three syllables restored.
- The fourth syllable becomes drawable only once wave 4 begins.
- Failing to defeat the final carrier brings escorts, continuously, with no give-up.
- Hearts reaching zero still ends the level in defeat.

- [ ] **Step 3: Record the evidence**

Screenshots to `QA/screenshots/` with a `-gated-finale` suffix, and a short note in `docs/audit/`. Batchmode cannot see pacing — the last two defects in this area were both found by playing, not by the suite.

- [ ] **Step 4: Commit the evidence**

```bash
git add QA/screenshots docs/audit
git commit -m "docs(audit): record the gated finale play verification"
```
