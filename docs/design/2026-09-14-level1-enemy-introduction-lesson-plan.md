# Level 1 Enemy Introduction Lesson — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `EnemyIntroductionBeat` the single enemy-introduction system, and extend it on Level 1 — via a level-scoped `EnemyLessonSO` profile — into the eight-beat Abo lesson (appear, ability, react, generalize, name, explain, glyph, draw).

**Architecture:** Every new behaviour is first expressed as a **pure, UnityEngine-free decision function** with EditMode tests, then wired into the MonoBehaviour that consumes it. This mirrors the precedent already set by `SpawnAssignmentDirector`, `ActiveClueSelector` and `SpawnGateRegistry`, all of which are deliberately scene-free so their logic is EditMode-testable. The MonoBehaviour layer stays thin: it resolves references, calls the decision function, and plays coroutines.

**Tech Stack:** Unity 6000.3.9f1, C#, Unity Test Framework (NUnit), ScriptableObject-driven content.

**Spec:** `docs/design/2026-09-14-level1-enemy-introduction-lesson-design.md` — read it before Task 1. Sections 4.2 (the ash arithmetic) and 7.1 (deferral must suppress) are the two non-obvious rules; the rest of the plan assumes you have read them.

## Global Constraints

- **Unity 6000.3.9f1.** Editor at `/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity`.
- **EditMode tests never run lifecycle callbacks.** `Awake`, `OnEnable`, `Start` and coroutines do not fire. Anything that needs them is a PlayMode test. This is why every task below tests a pure function in EditMode and defers the MonoBehaviour behaviour to Task 10.
- **Batchmode exits 0 when the Editor is already open**, producing no results XML and a truncated log. Never trust the exit code — always parse the results XML. Close the Editor before a batchmode run.
- **`-runTests` dirties the worktree**: it reserializes `Assets/Resources/Fonts/TutorialFont.asset` and drops `PerformanceTestRun*.json`. Do not commit those. `-executeMethod` runs stay clean.
- **Never create an asset with `AssetDatabase.CreateAsset` over an existing one.** It reissues the GUID and silently unwires every reference to it. Mutate the existing asset instead. This applies to Task 8.
- **A green suite is not a visual pass.** This project has shipped five UI wiring defects behind a green suite. Task 10 requires looking at the running game.
- **Copy for beats 3 and 4 is Filipino** per ruling Q16 (story lines stay Filipino; English is UI copy only). Task 8 leaves those fields empty pending the content owner — do not invent Filipino copy.
- **An enemy is permanently bound to one character** per ruling C2. Abo is `Char_A`. Never reassign an enemy's character to make a lesson work.

---

### Task 1: `EnemyLessonSO` and its lookup

The data shape for a lesson, and the pure function that finds the lesson for a given enemy on a given level. No behaviour yet.

**Files:**
- Create: `Assets/Scripts/Data/EnemyLessonSO.cs`
- Create: `Assets/Scripts/Data/EnemyLessonLookup.cs`
- Modify: `Assets/Scripts/Data/LevelConfigSO.cs` (add `enemyLessons` in the `[Header("Flow")]` block, after `onboardingSequence` at line 116)
- Test: `Assets/Tests/Editor/Data/EnemyLessonLookupTests.cs`

**Interfaces:**
- Consumes: `EnemyDataSO`, `LevelConfigSO`, `OnboardingBeatCopy`, `Level1TutorialStepSO` (all existing)
- Produces: `EnemyLessonSO` (fields below); `EnemyLessonLookup.Find(LevelConfigSO, EnemyDataSO) -> EnemyLessonSO` (null when none)

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/Data/EnemyLessonLookupTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class EnemyLessonLookupTests
{
    private static EnemyDataSO Enemy(string id)
    {
        var d = ScriptableObject.CreateInstance<EnemyDataSO>();
        d.enemyID = id;
        d.displayName = id;
        return d;
    }

    [Test]
    public void Find_ReturnsLessonMatchingTheEnemy()
    {
        EnemyDataSO abo = Enemy("abo-ng-simula");
        var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
        lesson.enemy = abo;

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new[] { lesson };

        Assert.AreSame(lesson, EnemyLessonLookup.Find(config, abo));
    }

    [Test]
    public void Find_ReturnsNullForAnEnemyWithNoLesson()
    {
        EnemyDataSO abo = Enemy("abo-ng-simula");
        EnemyDataSO iligaw = Enemy("iligaw");
        var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
        lesson.enemy = abo;

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new[] { lesson };

        Assert.IsNull(EnemyLessonLookup.Find(config, iligaw));
    }

    [Test]
    public void Find_MatchesByEnemyIDWhenTheReferenceDiffers()
    {
        // A pooled shell can carry a different EnemyDataSO instance with the same id after a
        // domain reload; identity must not be reference-only.
        var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
        lesson.enemy = Enemy("abo-ng-simula");

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new[] { lesson };

        Assert.AreSame(lesson, EnemyLessonLookup.Find(config, Enemy("abo-ng-simula")));
    }

    [Test]
    public void Find_ToleratesNullsEverywhere()
    {
        Assert.IsNull(EnemyLessonLookup.Find(null, Enemy("abo-ng-simula")));

        var config = ScriptableObject.CreateInstance<LevelConfigSO>();
        config.enemyLessons = new EnemyLessonSO[] { null };
        Assert.IsNull(EnemyLessonLookup.Find(config, Enemy("abo-ng-simula")));
        Assert.IsNull(EnemyLessonLookup.Find(config, null));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run (Editor closed):

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "EnemyLessonLookupTests" -testResults /tmp/salin-t1.xml -logFile /tmp/salin-t1.log; grep -c 'result="Failed"' /tmp/salin-t1.xml
```

Expected: compile error — `EnemyLessonSO` and `EnemyLessonLookup` do not exist. Read `/tmp/salin-t1.log`, not the exit code.

- [ ] **Step 3: Write `EnemyLessonSO`**

`Assets/Scripts/Data/EnemyLessonSO.cs`:

```csharp
using UnityEngine;

/// <summary>
/// A level-scoped extension of <c>EnemyIntroductionBeat</c>'s four-step card into the eight-beat
/// lesson. Level 1 authors exactly one (Abo ng Simula); every other level authors none and keeps
/// the four-step card unchanged.
///
/// <para>
/// <b>Level-scoped, not enemy-scoped.</b> This lives on <see cref="LevelConfigSO"/> rather than on
/// <see cref="EnemyDataSO"/> because ruling C2 fixes an enemy's asset and abilities across every
/// level it appears in. A lesson hung on the enemy would re-teach on Level 7.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "EnemyLesson", menuName = "Salinlahi/Enemy Lesson")]
public sealed class EnemyLessonSO : ScriptableObject
{
    [Header("Trigger")]
    [Tooltip("The enemy whose first spawn on this level runs the lesson.")]
    public EnemyDataSO enemy;

    [Tooltip("The lesson will not start until this many focus-word slots are restored. Abo needs 1: "
        + "his ash masks a word's FIRST slot, which is already masked while the needed slot is that "
        + "same first slot, so the ash is a guaranteed no-op at level start. See spec section 4.2.")]
    [Min(0)]
    public int requiredRestoredSlots = 1;

    [Header("Beat 2 — Ability")]
    [Tooltip("Arms the ability on the introduction spawn instead of suppressing it. This INVERTS "
        + "the default rule and is correct only for an enemy whose lesson reveals the ability "
        + "before naming it.")]
    public bool armAbilityOnIntroduction;

    [Tooltip("Wall-clock seconds held after the ability fires, before the react line.")]
    [Min(0f)]
    public float abilityBeatSeconds = 2.5f;

    [Header("Beats 3 and 4 — once per campaign")]
    [Tooltip("Beat 3. The reaction to the ability the player just watched.")]
    public OnboardingBeatCopy reactLine;

    [Tooltip("Beat 4. The rule: every enemy has its own ability. Shown once per campaign.")]
    public OnboardingBeatCopy ruleLine;

    [Header("Beats 7 and 8 — glyph")]
    [Tooltip("Hides the enemy's glyph badge through beats 1-6 so beat 7 is a reveal.")]
    public bool revealGlyphLate;

    [Tooltip("Beat 8's guided draw. Null skips the draw gate and ends the lesson after beat 7.")]
    public Level1TutorialStepSO drawStep;
}
```

- [ ] **Step 4: Write `EnemyLessonLookup`**

`Assets/Scripts/Data/EnemyLessonLookup.cs`:

```csharp
/// <summary>
/// Finds the <see cref="EnemyLessonSO"/> a level authored for an enemy, if any. Pure and
/// allocation-free so the introduction beat can call it on every spawn without a scene.
/// </summary>
public static class EnemyLessonLookup
{
    public static EnemyLessonSO Find(LevelConfigSO config, EnemyDataSO data)
    {
        if (config?.enemyLessons == null || data == null)
            return null;

        for (int i = 0; i < config.enemyLessons.Length; i++)
        {
            EnemyLessonSO lesson = config.enemyLessons[i];
            if (lesson == null || lesson.enemy == null)
                continue;

            if (lesson.enemy == data)
                return lesson;

            // Identity by id as well as reference: a pooled shell can carry a different
            // EnemyDataSO instance with the same enemyID across a domain reload.
            if (!string.IsNullOrEmpty(lesson.enemy.enemyID)
                && string.Equals(lesson.enemy.enemyID, data.enemyID,
                    System.StringComparison.OrdinalIgnoreCase))
                return lesson;
        }

        return null;
    }
}
```

- [ ] **Step 5: Add the field to `LevelConfigSO`**

In `Assets/Scripts/Data/LevelConfigSO.cs`, immediately after the `onboardingSequence` field (line 116):

```csharp
    [Tooltip("Enemy introduction lessons authored for this level. Level 1 carries one (Abo ng "
        + "Simula); every other level leaves this empty and uses the four-step introduction card.")]
    public EnemyLessonSO[] enemyLessons = System.Array.Empty<EnemyLessonSO>();
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "EnemyLessonLookupTests" -testResults /tmp/salin-t1.xml -logFile /tmp/salin-t1.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t1.xml | head -1
```

Expected: `failed="0"`, `passed="4"`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Data/EnemyLessonSO.cs Assets/Scripts/Data/EnemyLessonLookup.cs Assets/Scripts/Data/LevelConfigSO.cs Assets/Tests/Editor/Data/EnemyLessonLookupTests.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "feat(tutorial): add EnemyLessonSO and its level-scoped lookup"
```

---

### Task 2: the roster-met gate

Replaces `abo_ash_shown` as Level 1's slot-3 gate with a token that opens only once every enemy in the level's wave roster has been introduced. Pure logic only; the opener is wired in Task 6.

**Files:**
- Modify: `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnGateRegistry.cs` (add the constant beside `AboAshShown` at line 30)
- Create: `Assets/Scripts/Gameplay/Wave/SpawnAssignment/LevelRoster.cs`
- Test: `Assets/Tests/Editor/Gameplay/LevelRosterTests.cs`

**Interfaces:**
- Consumes: `LevelConfigSO.waves[*].enemyTypes`, `EnemyDataSO.isDecoy` / `suppressDiscovery` / `displayName`
- Produces: `SpawnGateRegistry.Level1RosterMet` (const string `"level1_roster_met"`); `LevelRoster.BuildIntroducibleRoster(LevelConfigSO) -> List<EnemyDataSO>`; `LevelRoster.AllIntroduced(IReadOnlyList<EnemyDataSO>, Func<EnemyDataSO,bool>) -> bool`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/Gameplay/LevelRosterTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "LevelRosterTests" -testResults /tmp/salin-t2.xml -logFile /tmp/salin-t2.log; grep -i "error CS" /tmp/salin-t2.log | head -3
```

Expected: `LevelRoster` does not exist.

- [ ] **Step 3: Add the gate constant**

In `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnGateRegistry.cs`, after `AboAshShown` (line 30):

```csharp
    /// <summary>
    /// Level 1's slot-3 gate, replacing <see cref="AboAshShown"/>. It opens when every introducible
    /// type in the level's wave roster has been introduced.
    ///
    /// <para>
    /// The swap is forced by the eight-beat lesson: Abo's ash now arms during his introduction
    /// rather than on a later spawn, so <see cref="AboAshShown"/> opens near the start of the level
    /// and no longer withholds anything. The constraint the gate actually encodes — the level must
    /// not be completable before the player has met what is in it — is unchanged, so the token is
    /// retargeted rather than removed. <see cref="AboAshShown"/> stays: it is still the ash's own
    /// once-latch, read by AshFirstSlotController.HasAshBeenShown.
    /// </para>
    /// </summary>
    public const string Level1RosterMet = "level1_roster_met";
```

- [ ] **Step 4: Write `LevelRoster`**

`Assets/Scripts/Gameplay/Wave/SpawnAssignment/LevelRoster.cs`:

```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// The set of enemy types a level can actually introduce, derived from its wave table.
///
/// Derived rather than authored so a wave-table edit cannot leave the roster gate waiting on a
/// type the level no longer spawns — which would hold slot 3 closed forever and make the level
/// unwinnable.
///
/// The filters mirror the data-level half of EnemyIntroductionBeat.IsIntroducibleSpawn. Its
/// remaining checks (IsBoss, an introduction already playing, drawing input not yet accepted) are
/// runtime state with no data equivalent; bosses are not listed in wave enemyTypes.
/// </summary>
public static class LevelRoster
{
    public static List<EnemyDataSO> BuildIntroducibleRoster(LevelConfigSO config)
    {
        var roster = new List<EnemyDataSO>();
        if (config?.waves == null)
            return roster;

        for (int w = 0; w < config.waves.Count; w++)
        {
            List<EnemyDataSO> types = config.waves[w]?.enemyTypes;
            if (types == null)
                continue;

            for (int i = 0; i < types.Count; i++)
            {
                EnemyDataSO data = types[i];
                if (data == null || data.isDecoy || data.suppressDiscovery)
                    continue;
                if (string.IsNullOrWhiteSpace(data.displayName))
                    continue;
                if (roster.Contains(data))
                    continue;

                roster.Add(data);
            }
        }

        return roster;
    }

    public static bool AllIntroduced(
        IReadOnlyList<EnemyDataSO> roster, Func<EnemyDataSO, bool> hasBeenIntroduced)
    {
        // An empty roster means a config problem, not "everything met". Reporting true would
        // ungate the final slot on a broken level rather than surfacing the break.
        if (roster == null || roster.Count == 0 || hasBeenIntroduced == null)
            return false;

        for (int i = 0; i < roster.Count; i++)
        {
            if (!hasBeenIntroduced(roster[i]))
                return false;
        }

        return true;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "LevelRosterTests" -testResults /tmp/salin-t2.xml -logFile /tmp/salin-t2.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t2.xml | head -1
```

Expected: `failed="0"`, `passed="4"`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnGateRegistry.cs Assets/Scripts/Gameplay/Wave/SpawnAssignment/LevelRoster.cs Assets/Tests/Editor/Gameplay/LevelRosterTests.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "feat(waves): add level1_roster_met gate and wave-derived roster"
```

---

### Task 3: the introduction decision (the tri-state)

**This is the task most likely to be got wrong.** Read spec section 7.1 first.

Today a spawn is binary: it is either an introduction spawn (card plays, ability suppressed) or it is not (no card, ability **armed**). The existing comment in `Enemy.Initialize` defends arming-on-decline, and is right for the ordinary case: "the safe failure is an ability with no card."

The lesson adds a third case. While a level's lesson is pending, other types are declined **deliberately** — so under the old rule Iligaw would spawn a mirror decoy before the player has been told abilities exist. Deferral must therefore suppress.

**Files:**
- Create: `Assets/Scripts/Gameplay/Tutorial/Onboarding/IntroductionDecision.cs`
- Test: `Assets/Tests/Editor/Onboarding/IntroductionDecisionTests.cs`

**Interfaces:**
- Consumes: nothing (pure; no UnityEngine types)
- Produces: `enum IntroductionOutcome { None, IntroduceAndSuppress, IntroduceAndArm, DeferAndSuppress }`; `IntroductionDecision.Resolve(bool claimAccepted, bool lessonArmsAbility, bool aLessonIsPending) -> IntroductionOutcome`; `IntroductionDecision.SuppressesAbility(IntroductionOutcome) -> bool`

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/Onboarding/IntroductionDecisionTests.cs`:

```csharp
using NUnit.Framework;

public class IntroductionDecisionTests
{
    [Test]
    public void OrdinaryIntroduction_IntroducesAndSuppresses()
    {
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: true, lessonArmsAbility: false, aLessonIsPending: false);

        Assert.AreEqual(IntroductionOutcome.IntroduceAndSuppress, outcome);
        Assert.IsTrue(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void LessonIntroduction_IntroducesAndArms()
    {
        // Beat 2 requires the ability to fire during the introduction. This is the inversion.
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: true, lessonArmsAbility: true, aLessonIsPending: false);

        Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, outcome);
        Assert.IsFalse(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void DeclinedWhileALessonIsPending_DefersAndSuppresses()
    {
        // The 7.1 rule. Without it Iligaw spawns a mirror decoy before beat 4 has told the
        // player abilities exist.
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: false, lessonArmsAbility: false, aLessonIsPending: true);

        Assert.AreEqual(IntroductionOutcome.DeferAndSuppress, outcome);
        Assert.IsTrue(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void DeclinedWithNoLessonPending_ArmsTheAbility()
    {
        // NEGATIVE CONTROL for the test above. This is the existing contract and it must not
        // regress: an ordinary declined claim leaves the ability armed, because an ability with
        // no card is a better failure than a card's worth of silence with the ability off.
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: false, lessonArmsAbility: false, aLessonIsPending: false);

        Assert.AreEqual(IntroductionOutcome.None, outcome);
        Assert.IsFalse(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void AnAcceptedClaimIgnoresThePendingFlag()
    {
        // The enemy whose lesson is pending is itself claim-accepted; it must not defer itself.
        Assert.AreEqual(
            IntroductionOutcome.IntroduceAndArm,
            IntroductionDecision.Resolve(true, lessonArmsAbility: true, aLessonIsPending: true));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "IntroductionDecisionTests" -testResults /tmp/salin-t3.xml -logFile /tmp/salin-t3.log; grep -i "error CS" /tmp/salin-t3.log | head -3
```

Expected: `IntroductionDecision` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Gameplay/Tutorial/Onboarding/IntroductionDecision.cs`:

```csharp
/// <summary>What a spawn does about its type's introduction, and what that implies for the ability.</summary>
public enum IntroductionOutcome
{
    /// <summary>Not an introduction. Ability armed — the ordinary case.</summary>
    None = 0,

    /// <summary>Four-step card plays; the ability is inert this spawn and arms on a later one.</summary>
    IntroduceAndSuppress = 1,

    /// <summary>Eight-beat lesson plays; the ability fires during it. Beat 2 depends on this.</summary>
    IntroduceAndArm = 2,

    /// <summary>Held back so a pending lesson lands first. Ability suppressed until it does.</summary>
    DeferAndSuppress = 3,
}

/// <summary>
/// Resolves a spawn's introduction outcome. Pure and UnityEngine-free so the rule — including the
/// one that inverts the existing arming contract — is asserted in EditMode.
///
/// <para>
/// <b>Two opposite rules live here and both are correct.</b> An ordinary declined claim leaves the
/// ability ARMED: an ability with no card is a better failure than a card's worth of silence with
/// the ability switched off. A claim declined because a lesson is pending SUPPRESSES: that decline
/// is deliberate, and the whole point of the lesson is that the player meets an ability only after
/// being told abilities exist. Do not collapse these two into one branch.
/// </para>
/// </summary>
public static class IntroductionDecision
{
    public static IntroductionOutcome Resolve(
        bool claimAccepted, bool lessonArmsAbility, bool aLessonIsPending)
    {
        if (claimAccepted)
        {
            return lessonArmsAbility
                ? IntroductionOutcome.IntroduceAndArm
                : IntroductionOutcome.IntroduceAndSuppress;
        }

        return aLessonIsPending
            ? IntroductionOutcome.DeferAndSuppress
            : IntroductionOutcome.None;
    }

    public static bool SuppressesAbility(IntroductionOutcome outcome)
    {
        return outcome == IntroductionOutcome.IntroduceAndSuppress
            || outcome == IntroductionOutcome.DeferAndSuppress;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "IntroductionDecisionTests" -testResults /tmp/salin-t3.xml -logFile /tmp/salin-t3.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t3.xml | head -1
```

Expected: `failed="0"`, `passed="5"`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Gameplay/Tutorial/Onboarding/IntroductionDecision.cs Assets/Tests/Editor/Onboarding/IntroductionDecisionTests.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "feat(tutorial): resolve introduction outcome as a tri-state"
```

---

### Task 4: wire the tri-state into `Enemy` and `EnemyIntroductionBeat`

Replaces the boolean claim with the Task 3 outcome. No lesson playback yet — a lesson-profile claim still plays the existing four-step card. This keeps the task reviewable: the only observable change is which abilities are suppressed.

**Files:**
- Modify: `Assets/Scripts/Gameplay/Enemy/Enemy.cs:267` (the claim), `:288` (the suppression call), `:336` (the begin call)
- Modify: `Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/EnemyIntroductionBeat.cs` (`TryClaimIntroductionSpawn`, `TryClaim`, `IsIntroducibleSpawn`)

**Interfaces:**
- Consumes: `IntroductionDecision.Resolve`, `IntroductionDecision.SuppressesAbility`, `IntroductionOutcome`, `EnemyLessonLookup.Find`
- Produces: `EnemyIntroductionBeat.ResolveIntroduction(Enemy, EnemyDataSO) -> IntroductionOutcome` (replaces `TryClaimIntroductionSpawn`); `EnemyIntroductionBeat.HasPendingLesson` (static bool); `Enemy.IntroductionOutcome` (public getter, used by Task 10's assertions)

- [ ] **Step 1: Replace the static claim entry point**

In `EnemyIntroductionBeat.cs`, replace `TryClaimIntroductionSpawn` with:

```csharp
    /// <summary>
    /// Resolves what this spawn does about its type's introduction.
    ///
    /// <para>
    /// Called from <c>Enemy.Initialize</c> BEFORE the ability components are configured, because
    /// the outcome also decides suppression. Returning <see cref="IntroductionOutcome.None"/> or
    /// <see cref="IntroductionOutcome.DeferAndSuppress"/> does not spend the type's one-shot.
    /// </para>
    /// </summary>
    public static IntroductionOutcome ResolveIntroduction(Enemy enemy, EnemyDataSO data)
    {
        if (s_instance == null || enemy == null || data == null)
            return IntroductionOutcome.None;

        return s_instance.ResolveFor(enemy, data);
    }

    /// <summary>True while this level authored a lesson that has not yet played.</summary>
    public static bool HasPendingLesson =>
        s_instance != null && s_instance.ResolvePendingLesson() != null;

    private IntroductionOutcome ResolveFor(Enemy enemy, EnemyDataSO data)
    {
        EnemyLessonSO lesson = ResolveLesson(data);
        bool claimed = TryClaim(enemy, data, lesson);
        return IntroductionDecision.Resolve(
            claimAccepted: claimed,
            lessonArmsAbility: claimed && lesson != null && lesson.armAbilityOnIntroduction,
            aLessonIsPending: !claimed && ResolvePendingLesson() != null);
    }

    /// <summary>The level's authored lesson for this type, or null.</summary>
    private EnemyLessonSO ResolveLesson(EnemyDataSO data) =>
        EnemyLessonLookup.Find(GameManager.CurrentLevelConfig, data);

    /// <summary>
    /// The level's authored lesson if it has not played yet, else null. A lesson whose enemy has
    /// already been introduced is not pending, which is what lets deferral end.
    /// </summary>
    private EnemyLessonSO ResolvePendingLesson()
    {
        LevelConfigSO config = GameManager.CurrentLevelConfig;
        if (config?.enemyLessons == null)
            return null;

        for (int i = 0; i < config.enemyLessons.Length; i++)
        {
            EnemyLessonSO lesson = config.enemyLessons[i];
            if (lesson?.enemy == null)
                continue;
            if (!EnemyIntroductionProgress.HasBeenIntroduced(lesson.enemy))
                return lesson;
        }

        return null;
    }
```

- [ ] **Step 2: Add the lesson precondition to `TryClaim`**

Change `TryClaim(Enemy, EnemyDataSO)` to `TryClaim(Enemy, EnemyDataSO, EnemyLessonSO)` and insert, immediately before the `EnemyIntroductionProgress.TryClaimIntroduction(data)` call:

```csharp
        // The lesson's precondition. Declining here deliberately does NOT spend the type's
        // one-shot, so an Abo who arrives before the clue can lose anything simply introduces
        // himself on a later spawn instead of burning his introduction on a no-op ash.
        if (lesson != null && RestoredSlotCount() < lesson.requiredRestoredSlots)
            return false;
```

And add:

```csharp
    /// <summary>
    /// Focus-word slots restored so far. Read from the presenter rather than tracked here, so the
    /// number the precondition reads is the same number the clue renders. See spec section 4.2.
    /// </summary>
    private static int RestoredSlotCount()
    {
        ActiveCluePresenter presenter = FindFirstObjectByType<ActiveCluePresenter>(
            FindObjectsInactive.Include);
        return presenter != null ? presenter.RestoredSlotCount : 0;
    }
```

`ActiveCluePresenter.RestoredSlotCount` already exists (`Assets/Scripts/UI/HUD/ActiveCluePresenter.cs:304`) as a getter over `_restorationState`. Use it; do not add a second source of this number.

- [ ] **Step 3: Let deferral decline other types**

In `IsIntroducibleSpawn`, after the existing `TutorialRuntimeState` check, add:

```csharp
        // Deferral. While this level's lesson is still pending, every other type waits: the rule
        // that enemies have abilities is taught once, by the lesson, and a card that lands first
        // would spend that first-meeting moment on an enemy the lesson did not choose.
        if (lesson == null && s_instance != null && s_instance.ResolvePendingLesson() != null)
            return false;
```

Pass `lesson` through from `TryClaim` to `IsIntroducibleSpawn`.

- [ ] **Step 4: Update `Enemy.Initialize`**

At `Assets/Scripts/Gameplay/Enemy/Enemy.cs:267`, replace the boolean claim:

```csharp
        // Asked before the ability components are configured, because the outcome is also the
        // signal for suppression. Three outcomes, not two: see IntroductionDecision — a claim
        // declined because a lesson is pending suppresses, while an ordinarily declined claim
        // still arms.
        _introductionOutcome = EnemyIntroductionBeat.ResolveIntroduction(this, _data);
        _isIntroductionSpawn = _introductionOutcome == IntroductionOutcome.IntroduceAndSuppress
            || _introductionOutcome == IntroductionOutcome.IntroduceAndArm;
```

At line 288, replace the suppression call:

```csharp
        ApplyIntroductionSpawnSuppression(
            IntroductionDecision.SuppressesAbility(_introductionOutcome));
```

Add the backing field and getter beside `_isIntroductionSpawn`:

```csharp
    private IntroductionOutcome _introductionOutcome;

    /// <summary>This spawn's introduction outcome. Read by tests and by the introduction beat.</summary>
    public IntroductionOutcome IntroductionOutcome => _introductionOutcome;
```

Line 336's `BeginIntroduction` call is unchanged — it still guards on `_isIntroductionSpawn`.

- [ ] **Step 5: Run the full EditMode suite to verify nothing regressed**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /tmp/salin-t4.xml -logFile /tmp/salin-t4.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t4.xml | head -1
```

Expected: `failed="0"`. `AbongSimulaAshTests` in particular must still pass — it asserts a correct draw resolves identically with and without ash.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/Enemy/Enemy.cs Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/EnemyIntroductionBeat.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "feat(tutorial): resolve introductions as a tri-state at spawn time"
```

---

### Task 5: play the eight beats

**Files:**
- Modify: `Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/EnemyIntroductionBeat.cs` (`PlayIntroduction`)

**Interfaces:**
- Consumes: `EnemyLessonSO`, `EnemyIntroductionCardView.PrepareCard/SetCardProgress/ShowAbilityLine/HideCardImmediate/ShowBanner/HideBanner`, `Enemy.GlyphBadge` (`EnemyGlyphBadge.Show()` / `Hide()`), `Level1TutorialGuideUI.ShowMessage/ShowFeedback/Hide`, `SoloTeachBeat.WaitForCorrectDraw`
- Produces: nothing new

- [ ] **Step 1: Split the existing coroutine**

Rename the current `PlayIntroduction(Enemy)` body to `PlayCard(Enemy, EnemyDataSO)` unchanged, and introduce:

```csharp
    private IEnumerator PlayIntroduction(Enemy enemy)
    {
        _isPlaying = true;
        EnemyDataSO data = enemy.Data;
        EnemyLessonSO lesson = ResolveLesson(data);

        try
        {
            if (_spawnSettleSeconds > 0f)
                yield return new WaitForSecondsRealtime(_spawnSettleSeconds);

            if (!IsStillPresentable(enemy, data))
                yield break;

            yield return lesson != null
                ? PlayLesson(enemy, data, lesson)
                : PlayCard(enemy, data);
        }
        finally
        {
            ReleaseTimeScale();
            LiftVignette();
            ReleaseEnemy(enemy);
            if (enemy != null) enemy.GlyphBadge?.Show();
            _isPlaying = false;
            _claimedEnemy = null;
            _routine = null;
        }
    }
```

The `finally` gains one line — the badge is restored on **every** exit path, including an abort mid-lesson, or beat 7 would leave an enemy permanently unmarked.

- [ ] **Step 2: Write `PlayLesson`**

```csharp
    /// <summary>
    /// The eight-beat lesson. Beats 1, 5 and 6 are the card's own steps; 2, 3, 4, 7 and 8 are the
    /// lesson's. Every wait is realtime, like the card's, because the beat holds Time.timeScale
    /// down and a scaled wait would stretch a two-second beat past thirteen.
    /// </summary>
    private IEnumerator PlayLesson(Enemy enemy, EnemyDataSO data, EnemyLessonSO lesson)
    {
        _card.PrepareCard(ResolveWalkSprite(data), data.displayName, data.discoverySubtitle);

        // Beat 7 is a reveal, so the badge goes dark before the player ever sees it.
        if (lesson.revealGlyphLate)
            enemy.GlyphBadge?.Hide();

        // Beat 1 — Appear. Halt and vignette, with NO card yet: the player must watch the
        // ability land on an enemy they cannot yet read anything about.
        HaltEnemy(enemy);
        RaiseVignette(enemy);
        yield return RampTimeScale(Time.timeScale, _introductionTimeScale, _haltRampSeconds);

        // Beat 2 — Ability. Already armed by IntroduceAndArm; this is the hold that lets the
        // player watch the clue crumble.
        yield return WaitRealtime(lesson.abilityBeatSeconds);

        // Beats 3 and 4 — React, then the rule. Once per campaign.
        if (!EnemyIntroductionProgress.HasSeenAbilityRule())
        {
            DialogueController dialogue = ResolveDialogueController();
            yield return OnboardingDialogueRunner.Play(dialogue, lesson.reactLine);
            yield return OnboardingDialogueRunner.Play(dialogue, lesson.ruleLine);
            EnemyIntroductionProgress.MarkAbilityRuleSeen();
        }

        // Beats 5 and 6 — Name, then ability line. The card's own steps, in its own order.
        yield return RampCard(0f, 1f, _haltRampSeconds);
        yield return WaitRealtime(_nameStepSeconds);
        _card.ShowAbilityLine(data.abilityLine);
        yield return WaitRealtime(_abilityStepSeconds);
        yield return RampCard(1f, 0f, _releaseRampSeconds);
        _card.HideCardImmediate();

        // Beat 7 — Glyph. The badge comes up while the enemy is still spotlit and time is still
        // slow, so the reveal is the only thing moving on screen.
        enemy.GlyphBadge?.Show();
        yield return WaitRealtime(_nameStepSeconds);

        // Beat 8 — Draw. Time and movement come back first: the draw is real combat against a
        // real enemy, not a frozen exercise.
        LiftVignette();
        yield return RampTimeScale(Time.timeScale, _restoreTimeScale, _releaseRampSeconds);
        ReleaseTimeScale();
        ReleaseEnemy(enemy);

        if (lesson.drawStep != null)
            yield return PlayDrawStep(enemy, lesson.drawStep);

        _card.ShowBanner(data.abilityLine);
        yield return WaitWhileEnemyLives(enemy, data);
        _card.HideBanner();
    }
```

- [ ] **Step 3: Write `PlayDrawStep`**

```csharp
    /// <summary>
    /// Beat 8. Reuses the surviving Level1TutorialStepSO guide machinery against a live enemy.
    /// Input is never locked — the beat's standing promise — so a player who has already started a
    /// stroke can finish it.
    /// </summary>
    /// <summary>
    /// The scene's dialogue controller. Resolved on demand rather than serialized because this
    /// beat is created by the wiring tool before the HUD it will need exists.
    /// OnboardingDialogueRunner.Play already no-ops on a null controller and on blank copy, which
    /// is what lets beats 3 and 4 ship before their Filipino copy is authored.
    /// </summary>
    private DialogueController ResolveDialogueController()
    {
        if (_dialogue == null)
            _dialogue = FindFirstObjectByType<DialogueController>(FindObjectsInactive.Include);

        return _dialogue;
    }

    private DialogueController _dialogue;

    private IEnumerator PlayDrawStep(Enemy enemy, Level1TutorialStepSO step)
    {
        Level1TutorialGuideUI guide = FindFirstObjectByType<Level1TutorialGuideUI>(
            FindObjectsInactive.Include);

        if (guide != null)
            guide.ShowMessage(step.promptText, canSkip: false);

        string expectedID = step.targetCharacter != null ? step.targetCharacter.characterID : null;
        if (string.IsNullOrEmpty(expectedID))
        {
            if (guide != null) guide.Hide();
            yield break;
        }

        System.Action<RecognitionResult, bool, float> feedback = null;
        if (guide != null)
        {
            feedback = (result, passed, _) =>
            {
                if (passed && string.Equals(result.characterID, expectedID,
                        System.StringComparison.OrdinalIgnoreCase))
                    return;

                guide.ShowFeedback(passed
                    ? step.wrongCharacterFeedback
                    : step.recognitionFailedFeedback);
            };
            EventBus.OnRecognitionResolved += feedback;
        }

        try
        {
            yield return TutorialDrawWait.WaitForCorrectDraw(expectedID);
        }
        finally
        {
            if (feedback != null)
                EventBus.OnRecognitionResolved -= feedback;
            if (guide != null)
                guide.Hide();
        }
    }
```

`WaitForCorrectDraw` currently lives on `SoloTeachBeat`, which Task 7 deletes. Extract it **now**, before writing the code above, into `Assets/Scripts/Gameplay/Tutorial/TutorialDrawWait.cs` — copied verbatim, only the containing type changes:

```csharp
using System.Collections;

/// <summary>
/// Blocks until the recognizer resolves the expected character. Extracted from SoloTeachBeat when
/// that beat was deleted; the eight-beat lesson's beat 8 is its only remaining caller.
/// </summary>
public static class TutorialDrawWait
{
    public static IEnumerator WaitForCorrectDraw(string expectedCharacterID)
    {
        bool resolved = false;
        System.Action<RecognitionResult, bool, float> handler = (result, passed, _) =>
        {
            if (passed && string.Equals(result.characterID, expectedCharacterID,
                    System.StringComparison.OrdinalIgnoreCase))
                resolved = true;
        };
        EventBus.OnRecognitionResolved += handler;
        try { yield return new WaitUntil(() => resolved); }
        finally { EventBus.OnRecognitionResolved -= handler; }
    }
}
```

- [ ] **Step 4: Add the once-per-campaign latch**

In `Assets/Scripts/Data/EnemyIntroductionProgress.cs`, beside `IntroducedEnemyIDsKey`:

```csharp
    public const string AbilityRuleSeenKey = "salinlahi.tutorial.ability_rule_seen";

    /// <summary>
    /// Whether beats 3 and 4 — the reaction and the rule that every enemy has an ability — have
    /// played. Once per campaign: the rule is general, so a second level teaching it again would
    /// read as the game forgetting the player.
    /// </summary>
    public static bool HasSeenAbilityRule() => PlayerPrefs.GetInt(AbilityRuleSeenKey, 0) == 1;

    public static void MarkAbilityRuleSeen()
    {
        PlayerPrefs.SetInt(AbilityRuleSeenKey, 1);
        PlayerPrefs.Save();
    }
```

Add `PlayerPrefs.DeleteKey(AbilityRuleSeenKey);` to both `ClearAllIntroduced()` and `ResetForTests()`, so New Journey re-teaches the rule.

- [ ] **Step 5: Verify the latch in EditMode**

Add to `Assets/Tests/Editor/Onboarding/IntroductionDecisionTests.cs`:

```csharp
    [Test]
    public void AbilityRuleLatch_IsOnceUntilCleared()
    {
        EnemyIntroductionProgress.ResetForTests();
        Assert.IsFalse(EnemyIntroductionProgress.HasSeenAbilityRule());

        EnemyIntroductionProgress.MarkAbilityRuleSeen();
        Assert.IsTrue(EnemyIntroductionProgress.HasSeenAbilityRule());

        EnemyIntroductionProgress.ClearAllIntroduced();
        Assert.IsFalse(EnemyIntroductionProgress.HasSeenAbilityRule(),
            "New Journey must re-teach the rule.");
    }
```

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "IntroductionDecisionTests" -testResults /tmp/salin-t5.xml -logFile /tmp/salin-t5.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t5.xml | head -1
```

Expected: `failed="0"`, `passed="6"`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/EnemyIntroductionBeat.cs Assets/Scripts/Gameplay/Tutorial/TutorialDrawWait.cs Assets/Scripts/Data/EnemyIntroductionProgress.cs Assets/Tests/Editor/Onboarding/IntroductionDecisionTests.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "feat(tutorial): play the eight-beat enemy lesson"
```

---

### Task 6: open the roster-met gate

**Files:**
- Modify: `Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/EnemyIntroductionBeat.cs` (end of `PlayIntroduction`'s `finally`)
- Test: `Assets/Tests/Editor/Gameplay/LevelRosterTests.cs` (extend)

**Interfaces:**
- Consumes: `LevelRoster.BuildIntroducibleRoster`, `LevelRoster.AllIntroduced`, `EnemyIntroductionProgress.HasBeenIntroduced`, `SpawnAssignmentCoordinator.OpenGate`
- Produces: nothing new

- [ ] **Step 1: Add the check**

In `EnemyIntroductionBeat`, after `_isPlaying = false;` in the `finally`:

```csharp
            RaiseRosterGateIfComplete();
```

And:

```csharp
    /// <summary>
    /// Opens Level 1's slot-3 gate once every introducible type in the wave roster has been
    /// introduced. Called after each introduction rather than counted, so a level whose roster
    /// changes mid-development cannot leave a stale count holding the final slot shut.
    /// </summary>
    private static void RaiseRosterGateIfComplete()
    {
        LevelConfigSO config = GameManager.CurrentLevelConfig;
        if (config == null)
            return;

        if (!LevelRoster.AllIntroduced(
                LevelRoster.BuildIntroducibleRoster(config),
                EnemyIntroductionProgress.HasBeenIntroduced))
            return;

        SpawnAssignmentCoordinator coordinator = FindFirstObjectByType<SpawnAssignmentCoordinator>(
            FindObjectsInactive.Include);
        coordinator?.OpenGate(SpawnGateRegistry.Level1RosterMet);
    }
```

- [ ] **Step 2: Add the gate test**

Append to `LevelRosterTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run and verify**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "LevelRosterTests" -testResults /tmp/salin-t6.xml -logFile /tmp/salin-t6.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t6.xml | head -1
```

Expected: `failed="0"`, `passed="5"`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/EnemyIntroductionBeat.cs Assets/Tests/Editor/Gameplay/LevelRosterTests.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "feat(waves): open level1_roster_met when every roster type is introduced"
```

---

### Task 7: delete the superseded systems

**Files:**
- Delete: `Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/SoloTeachBeat.cs` (+ `.meta`)
- Delete: `Assets/Scripts/UI/EnemyDiscoveryOnboardingController.cs` (+ `.meta`)
- Delete: `Assets/Prefabs/UI/EnemyDiscoveryOverlay.prefab` (+ `.meta`)
- Modify: `Assets/Scripts/Data/OnboardingSequenceSO.cs`, `Assets/Scripts/Gameplay/Tutorial/Onboarding/Level1OnboardingController.cs`
- Modify: `docs/handoff-enemy-discovery-overlay.md`, `Assets/Editor/UI/Level1TeachingBeatSceneWiringTool.cs` (stale docstring)

- [ ] **Step 1: Retire the enum value, do not reuse it**

In `OnboardingSequenceSO.cs`, remove `SoloTeach = 2` from `OnboardingBeatType` and extend the existing comment:

```csharp
    // SALIN-225 removed ComboTeach = 3 and FocusModeTeach = 6 with the mechanics they taught.
    // The eight-beat lesson removes SoloTeach = 2: teaching moved into EnemyIntroductionBeat, one
    // enemy at a time, triggered by a spawn. The survivors keep their explicit values so
    // serialized beatOrder blobs stay valid, and 2, 3 and 6 stay burned — a stale blob carrying
    // one of them must bind to nothing rather than silently to a new beat.
```

- [ ] **Step 2: Remove the SoloTeach fields**

Delete from `OnboardingSequenceSO`: `soloTeachStep`, `basicTeachSteps`, `basicTeachVideos`, `soloTeachPreVideo`, `soloTeachVideo`, `soloTeachPostSuccess`, and the `Beat 3 — Symbol Teach` header. Remove `SoloTeach` from the default `beatOrder` initialiser.

- [ ] **Step 3: Remove the controller's SoloTeach handling**

In `Level1OnboardingController.cs`, delete the GIF scene-override resolution that logs *"GIF scene overrides were assigned, but no matching teach step was found"* (line ~365) and any `EnsureDefaultBeatComponents` registration of `SoloTeachBeat`. Delete `CopyLegacySteps` / `GetLegacyStep` if they become unreferenced.

- [ ] **Step 4: Delete the files**

```bash
git rm Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/SoloTeachBeat.cs Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/SoloTeachBeat.cs.meta
git rm Assets/Scripts/UI/EnemyDiscoveryOnboardingController.cs Assets/Scripts/UI/EnemyDiscoveryOnboardingController.cs.meta
git rm Assets/Prefabs/UI/EnemyDiscoveryOverlay.prefab Assets/Prefabs/UI/EnemyDiscoveryOverlay.prefab.meta
```

- [ ] **Step 5: Correct the two stale docs**

Prepend to `docs/handoff-enemy-discovery-overlay.md`:

```markdown
> **Closed 2026-09-14 — do not action this handoff.** The overlay it describes has been deleted,
> not restored. `EnemyIntroductionBeat` replaced it; see
> `docs/design/2026-09-14-level1-enemy-introduction-lesson-design.md`.
```

In `Level1TeachingBeatSceneWiringTool.cs`, the class docstring claims none of the teaching components are placed. All six are present in `Assets/_Scenes/Gameplay.unity`. Replace that clause with a statement that the tool is idempotent re-wiring.

- [ ] **Step 6: Run the full EditMode suite**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /tmp/salin-t7.xml -logFile /tmp/salin-t7.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t7.xml | head -1
```

Expected: `failed="0"`. `Level1OnboardingControllerTests` and `LevelFlowControllerTests` reference the legacy sequence; update them to drop SoloTeach expectations rather than deleting the tests.

- [ ] **Step 7: Commit**

```bash
git add -A Assets/Scripts Assets/Tests Assets/Editor Assets/Prefabs docs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "refactor(tutorial): delete the SoloTeach loop and the dead discovery overlay"
```

---

### Task 8: authoring

**Do not use `AssetDatabase.CreateAsset` on any existing asset** — it reissues the GUID and unwires every reference. Edit in the Inspector, or mutate in place.

**Files:**
- Create: `Assets/ScriptableObjects/Tutorial/AboLesson.asset`
- Modify: `Assets/ScriptableObjects/Levels/Level1_Config.asset`
- Modify: `Assets/ScriptableObjects/Tutorial/Level1OnboardingSequence.asset`
- Modify: `Assets/ScriptableObjects/Tutorial/Level1TutorialStep_A.asset`

- [ ] **Step 1: Create the lesson asset**

`Assets → Create → Salinlahi → Enemy Lesson`, named `AboLesson`, in `Assets/ScriptableObjects/Tutorial/`. Set:

| Field | Value |
|---|---|
| `enemy` | `EnemyData_AbongSimula` |
| `requiredRestoredSlots` | `1` |
| `armAbilityOnIntroduction` | ✔ |
| `abilityBeatSeconds` | `2.5` |
| `revealGlyphLate` | ✔ |
| `drawStep` | `Level1TutorialStep_A` |
| `reactLine` / `ruleLine` | **leave empty** — Filipino copy, pending the content owner |

- [ ] **Step 2: Retarget Level 1**

In `Level1_Config`:

| Field | From | To |
|---|---|---|
| `spawnAssignmentPolicy.openingSpawnSpokenValueId` | `value.a` | `value.ei` |
| `spawnAssignmentPolicy.slotFloors[0].minSpawnsBeforeNeeded` | `1` | `0` |
| `spawnAssignmentPolicy.slotGates[0].gateToken` | `abo_ash_shown` | `level1_roster_met` |
| `enemyLessons` | — | one element: `AboLesson` |

- [ ] **Step 3: Drop the teach loop from the sequence**

In `Level1OnboardingSequence`: clear `basicTeachSteps`, and set `beatOrder` to `ProtagonistIntro, BaseIntro, Release`.

- [ ] **Step 4: Verify the YAML**

```bash
grep -E "openingSpawnSpokenValueId|gateToken|minSpawnsBeforeNeeded|beatOrder" Assets/ScriptableObjects/Levels/Level1_Config.asset Assets/ScriptableObjects/Tutorial/Level1OnboardingSequence.asset
```

Expected: `value.ei`, `level1_roster_met`, `beatOrder: 000000000100000005000000`.

- [ ] **Step 5: Commit**

```bash
git add Assets/ScriptableObjects
git commit -m "chore(levels): author the Abo lesson and retarget Level 1's opening and gate"
```

---

### Task 9: the first-draw trace guide

For Iligaw, Nawalang Mukha and Mantsa. Non-blocking: it never halts, gates or pauses.

**Files:**
- Create: `Assets/Scripts/UI/FirstDrawGuidePresenter.cs`
- Create: `Assets/Tests/Editor/UI/FirstDrawGuideTests.cs`
- Modify: `Assets/Editor/UI/Level1TeachingBeatSceneWiringTool.cs` (place it)

**Interfaces:**
- Consumes: `EventBus.OnActiveClueChanged` (or the presenter's existing change notification), `Level1TutorialStepSO.guideSprite` / `templatePoints`
- Produces: `FirstDrawGuideMemory.ShouldShowFor(string characterID, ISet<string> alreadyShown) -> bool`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

public class FirstDrawGuideTests
{
    [Test]
    public void ShowsOncePerCharacter()
    {
        var shown = new HashSet<string>();
        Assert.IsTrue(FirstDrawGuideMemory.ShouldShowFor("na", shown));
        shown.Add("na");
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor("na", shown));
    }

    [Test]
    public void IsCaseInsensitiveAndIgnoresBlanks()
    {
        var shown = new HashSet<string> { "na" };
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor("NA", shown));
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor("  ", shown));
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor(null, shown));
    }
}
```

- [ ] **Step 2: Run it and confirm it fails** — same batchmode command, `-testFilter "FirstDrawGuideTests"`.

- [ ] **Step 3: Implement**

```csharp
using System.Collections.Generic;

/// <summary>
/// Which glyphs have already had their one non-blocking trace guide. Pure, so the once-only rule
/// is EditMode-asserted separately from the presenter that draws it.
/// </summary>
public static class FirstDrawGuideMemory
{
    public static bool ShouldShowFor(string characterID, ISet<string> alreadyShown)
    {
        if (string.IsNullOrWhiteSpace(characterID) || alreadyShown == null)
            return false;

        return !alreadyShown.Contains(characterID.Trim().ToLowerInvariant());
    }
}
```

`Assets/Scripts/UI/FirstDrawGuidePresenter.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One low-opacity trace guide, the first time each glyph is the needed symbol.
///
/// <para>
/// <b>It never blocks.</b> No EnterDialoguePause, no TutorialRuntimeState.SetDrawingInputLocked,
/// no Time.timeScale, and the image disables its own raycasts — the player keeps drawing straight
/// through it. Abo's lesson is the only thing on Level 1 permitted to gate.
/// </para>
/// </summary>
public sealed class FirstDrawGuidePresenter : MonoBehaviour
{
    [Tooltip("The image the guide sprite is drawn into. Raycast target is forced off at Awake.")]
    [SerializeField] private Image _guideImage;

    [Tooltip("Steps carrying each glyph's guide sprite, matched by targetCharacter.characterID.")]
    [SerializeField] private Level1TutorialStepSO[] _steps;

    [Tooltip("Seconds the guide stays up if the player has not drawn the glyph correctly.")]
    [SerializeField] private float _timeoutSeconds = 8f;

    [Range(0f, 1f)]
    [SerializeField] private float _guideAlpha = 0.35f;

    private readonly HashSet<string> _shown = new();
    private Coroutine _routine;

    private void Awake()
    {
        if (_guideImage != null)
        {
            _guideImage.raycastTarget = false;
            _guideImage.enabled = false;
        }
    }

    private void OnEnable() => EventBus.OnActiveClueChanged += HandleClueChanged;

    private void OnDisable()
    {
        EventBus.OnActiveClueChanged -= HandleClueChanged;
        Hide();
    }

    private void HandleClueChanged(BaybayinCharacterSO needed)
    {
        string id = needed != null ? needed.characterID : null;
        if (!FirstDrawGuideMemory.ShouldShowFor(id, _shown))
            return;

        Level1TutorialStepSO step = FindStep(id);
        if (step == null || step.guideSprite == null)
            return;

        _shown.Add(id.Trim().ToLowerInvariant());

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowUntilDrawnOrTimeout(step, id));
    }

    private IEnumerator ShowUntilDrawnOrTimeout(Level1TutorialStepSO step, string expectedID)
    {
        _guideImage.sprite = step.guideSprite;
        _guideImage.color = new Color(1f, 1f, 1f, _guideAlpha);
        _guideImage.enabled = true;

        bool drawn = false;
        System.Action<RecognitionResult, bool, float> handler = (result, passed, _) =>
        {
            if (passed && string.Equals(result.characterID, expectedID,
                    System.StringComparison.OrdinalIgnoreCase))
                drawn = true;
        };
        EventBus.OnRecognitionResolved += handler;

        try
        {
            float elapsed = 0f;
            while (!drawn && elapsed < _timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally
        {
            EventBus.OnRecognitionResolved -= handler;
            Hide();
        }
    }

    private Level1TutorialStepSO FindStep(string characterID)
    {
        if (_steps == null) return null;
        for (int i = 0; i < _steps.Length; i++)
        {
            Level1TutorialStepSO step = _steps[i];
            if (step?.targetCharacter == null) continue;
            if (string.Equals(step.targetCharacter.characterID, characterID,
                    System.StringComparison.OrdinalIgnoreCase))
                return step;
        }
        return null;
    }

    private void Hide()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        if (_guideImage != null) _guideImage.enabled = false;
    }
}
```

**Before writing this, confirm `EventBus.OnActiveClueChanged` exists with a `BaybayinCharacterSO` payload** (`grep -n "OnActiveClueChanged" Assets/Scripts/Core/EventBus.cs`). If the event carries a different shape, adapt the handler signature — do not add a new event.

- [ ] **Step 4: Run and verify** — expected `failed="0"`, `passed="2"`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/FirstDrawGuidePresenter.cs Assets/Scripts/UI/FirstDrawGuideMemory.cs Assets/Tests/Editor/UI/FirstDrawGuideTests.cs Assets/Editor/UI/Level1TeachingBeatSceneWiringTool.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
git commit -m "feat(ui): add the non-blocking first-draw trace guide"
```

---

### Task 10: PlayMode integration and the visual check

EditMode cannot run any of this: every path depends on `Awake`, `OnEnable` and coroutines.

**Files:**
- Create: `Assets/Tests/PlayMode/Gameplay/Level1LessonTests.cs`

- [ ] **Step 1: Write the PlayMode tests**

Cover, each as its own `[UnityTest]`:

1. **Ordering.** With `Level1_Config` active, the first spawn is an `I` carrier and `EnemyIntroductionCardView` never presents during it. Assert `enemy.IntroductionOutcome == IntroductionOutcome.DeferAndSuppress`.
2. **Deferral suppresses.** That same first Iligaw reports its `MirrorDecoyController` suppressed. **Negative control:** an Iligaw spawned with no lesson pending reports it armed. Without the control this test passes against a matcher that matches nothing.
3. **The inversion.** Abo's introduction spawn reports `IntroduceAndArm` and his `AshFirstSlotController` is *not* suppressed.
4. **Precondition.** An Abo spawned with zero restored slots is declined **and** `EnemyIntroductionProgress.HasBeenIntroduced(abo)` is still false — the one-shot was not spent.
5. **Beat 2 is visible.** With slot 0 restored, `BuildMaskedSpellingWithRestoration(INA, needed=NA, ash:true)` differs from the same call with `ash:false`. **Negative control:** with nothing restored they are equal.
6. **Teardown.** Abort mid-lesson (disable the beat) and assert `Time.timeScale == 1`, the vignette is down, the glyph badge is visible, and the enemy moves.

Isolate aggressively: PlayMode tests on this project have leaked scenes and singletons between cases. Call `EnemyIntroductionProgress.ResetForTests()` in `[SetUp]`, and tear down any scene a test loads.

- [ ] **Step 2: Run the PlayMode suite**

```bash
/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "Level1LessonTests" -testResults /tmp/salin-t10.xml -logFile /tmp/salin-t10.log; grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/salin-t10.xml | head -1
```

Expected: `failed="0"`, `passed="6"`. Parse the XML; the exit code is meaningless if an Editor is open.

- [ ] **Step 3: Look at it**

A green suite is not a pass on this project. Open the Editor, set `PlayerSettings.runInBackground` on the macOS target, play Level 1 and watch for:

- Iligaw arrives first, nameless, with a trace guide; drawing `I` moves the clue from `_NA` to `i_`
- Abo halts the field with **no card** — the ash gusts and `i_` visibly **crumbles** to `__`
- The react and rule lines land after the ash, before the name
- The card names Abo, then states the ability
- The badge **appears** on Abo (it must not have been visible before this)
- Drawing `A` kills him, the ash lifts, and `i_` returns
- `Time.timeScale` is back to 1 and nothing is left dimmed

Capture a screenshot of the crumble and of the badge reveal.

- [ ] **Step 4: Commit**

```bash
git add Assets/Tests/PlayMode/Gameplay/Level1LessonTests.cs
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null || true
rm -f PerformanceTestRun*.json
git commit -m "test(tutorial): cover the Level 1 lesson ordering, inversion and teardown"
```

---

## Open items carried from the spec

- **Beats 3 and 4 Filipino copy** — Task 8 leaves `reactLine` / `ruleLine` empty. The lesson plays without them (the dialogue runner no-ops on blank copy); it is not finished until they are authored against `docs/content/`.
- **First-draw guide dismissal** — Task 9 uses an 8-second timeout. Confirm against playtest.
- **`Level1TutorialSequenceSO` legacy adapter** — still referenced by `LevelConfigSO.tutorialSequence`. Out of scope; separate cleanup.
- **Spec section 4.4 fallback** — "Abo first, ashed slots re-rendered as a burnt mark" stays parked. If the ~20-second delay before beat 1 playtests badly, that is the alternative to reach for.
