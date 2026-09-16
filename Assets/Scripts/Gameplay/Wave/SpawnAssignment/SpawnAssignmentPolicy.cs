using System;
using System.Collections.Generic;

/// <summary>
/// Binds one flattened target slot to a beat that must resolve before it is fillable.
/// </summary>
[Serializable]
public class SpawnSlotGate
{
    /// <summary>Zero-based index into the flattened slot list (all focus words in order).</summary>
    public int slotIndex;

    /// <summary>Token the beat system opens, e.g. SpawnGateRegistry.AboAshShown.</summary>
    public string gateToken;
}

/// <summary>
/// Overrides the anti-rush floor for one flattened target slot.
///
/// Exists because the floor is a pacing knob everywhere except the tutorial's very first
/// interaction, where it is the difference between a first drawing that restores a slot and one
/// that restores nothing. Level 1 lowers slot 0 to 1 so the needed E/I carrier arrives on spawn 2
/// instead of spawn 5; slots 1-3 keep the level's scalar floor, so the anti-rush purpose is intact
/// for the rest of the level.
/// </summary>
[Serializable]
public class SpawnSlotFloor
{
    /// <summary>Zero-based index into the flattened slot list (all focus words in order).</summary>
    public int slotIndex;

    /// <summary>Spawns that must pass before this slot's needed symbol may be drawn.</summary>
    public int minSpawnsBeforeNeeded;
}

/// <summary>
/// Per-level tuning for <see cref="SpawnAssignmentDirector"/>.
///
/// Deliberately free of UnityEngine types so the whole assignment system stays testable without a
/// scene, matching the precedent set by <see cref="ActiveClueSelector"/>. Unity still serializes it
/// as a nested field on <see cref="LevelConfigSO"/> because it is <see cref="SerializableAttribute"/>
/// with public fields; the cost is that these show in the Inspector without tooltips.
///
/// Design source: docs/design/spawn-assignment-system.md.
/// </summary>
[Serializable]
public class SpawnAssignmentPolicy
{
    /// <summary>
    /// Anti-rush floor. The needed symbol cannot be drawn until this many enemies have spawned
    /// since the current slot window became fillable.
    ///
    /// This is the single mechanism preventing an instant win, and the primary pacing knob. It is
    /// a spawn count rather than a timer because what is being rationed is opportunities, not
    /// seconds: a player who clears quickly should not be made to wait through dead air.
    ///
    /// Level 1 uses 4, derived in the design doc from a 130s target:
    /// minSpawnsBeforeNeeded = (X - overhead) / (slots * meanInterval) - 1/neededWeight.
    ///
    /// This is the level-wide default. <see cref="slotFloors"/> overrides it for named slots;
    /// always read the floor through <see cref="MinSpawnsBeforeNeededForSlot"/> rather than this
    /// field directly.
    /// </summary>
    public int minSpawnsBeforeNeeded = 4;

    /// <summary>Probability that a spawn carries the needed symbol once the floor is satisfied.</summary>
    public float neededWeight = 0.5f;

    /// <summary>
    /// Seconds before the needed symbol is force-spawned. Fixes a supply failure: the symbol never
    /// appeared. Counts only pause-aware time, so dialogue, intermissions and gated beats do not
    /// bank a forced-spawn debt.
    /// </summary>
    public float starvationTimeout = 30f;

    /// <summary>
    /// Seconds before escalating. Fixes a comprehension failure: the symbol did appear and the
    /// player could not read it. Suppresses filler entirely and raises the clue channel. Kept
    /// separate from <see cref="starvationTimeout"/> because the two failures share a symptom
    /// (the slot is not filling) but need opposite remedies.
    /// </summary>
    public float hardStarvationTimeout = 50f;

    /// <summary>
    /// Below this many distinct later-needed symbols, filler also draws from already-restored
    /// slots. Load-bearing in Level 1 rather than an edge case: while NA is the cursor, the only
    /// ungated later-needed symbol is A, so without this every filler for ~20 seconds is A.
    /// </summary>
    public int minFillerVariety = 2;

    /// <summary>
    /// Share of filler drawn from the level's cumulative symbol pool minus the target's own
    /// symbols. 0 for Level 1, whose pool is exactly its target. Raising it is what stops a
    /// later 18-symbol level from still being a 4-symbol shooting gallery.
    /// </summary>
    public float offTargetFillerWeight = 0f;

    /// <summary>
    /// How many unrestored slots are simultaneously fillable. 1 restores strict left-to-right
    /// order (Level 1). Larger values keep level length sub-linear in slot count and match how a
    /// sentence is actually read, which is what makes this system survive an 18-symbol target.
    /// </summary>
    public int activeSlotWindow = 1;

    /// <summary>
    /// Zero-based flattened slot index whose needed carrier spawns as a pair, guaranteeing one
    /// on-screen choice between two real target symbols. -1 disables. Level 1 uses 1 (the NA slot
    /// of INA); the design doc calls this "slot 2" in 1-based prose.
    /// </summary>
    public int choiceMomentSlotIndex = 1;

    /// <summary>Seconds within which the pair's second member must spawn.</summary>
    public float choicePairWindow = 1.5f;

    /// <summary>
    /// Concurrency ceiling the choice pair checks before it is issued. When the budget is
    /// unavailable the directive carries forward rather than being dropped, which is what makes
    /// the choice moment a guarantee instead of a probability.
    /// </summary>
    public int maxConcurrentEnemies = 8;

    /// <summary>
    /// Forces the level's very first assignment to carry the symbol that emits this spoken value.
    /// Empty means no directive.
    ///
    /// It exists because "the first enemy type the player ever meets" is a narrative decision the
    /// schedule cannot express: while slot 0 is the cursor, every ungated later-needed symbol is
    /// legal filler, so Level 1's opening enemy could be any of them. Naming the symbol names the
    /// enemy, because each Level 1 enemy is pinned to exactly one symbol by
    /// EnemyDataSO.assignedCharacter - Level 1 authors "value.a" and gets Abo ng Simula, whose
    /// introduction card and inert first ash the opening beat is written around.
    ///
    /// A spoken value id rather than a symbol id, because this class stays free of UnityEngine
    /// types and cannot hold a BaybayinCharacterSO reference; the id is resolved against
    /// ContentIdentity.IsApprovedSpokenValue, the same authority the focus-word decompositions and
    /// the validator use, so "value.e" correctly names symbol.ei rather than a symbol.e that does
    /// not exist.
    ///
    /// One-shot: spent on the first spawn that can honour it. It consumes one spawn against the
    /// floor exactly as any other spawn does, but neither resets nor re-arms it, does not touch
    /// <see cref="neededWeight"/>, and does not enter the filler rotation - so every schedule
    /// decision after the opening enemy is the one a run without a directive would have made.
    /// </summary>
    public string openingSpawnSpokenValueId = "";

    /// <summary>
    /// Lets the final wave keep emitting past its authored enemyCount until the last slot is
    /// restored. Required, not optional: an ordinary run that misses one needed carrier exhausts
    /// the 24-enemy Level 1 budget and would otherwise deadlock.
    ///
    /// NOT YET CONSUMED. Honouring it means changing wave-completion in WaveManager, a different
    /// subsystem from spawn assignment, so it is deliberately left for a follow-up. Until then a
    /// missed final-slot carrier relies on the starvation timer landing inside the authored budget.
    /// </summary>
    public bool allowFinalWaveOverflow = true;

    /// <summary>
    /// Withholds this level's LAST flattened slot until the final wave begins, so the restoration
    /// cannot complete early and the level always reaches its finale.
    ///
    /// The slot is derived at level start, never authored: an authored index would couple the gate
    /// to content position, and re-authoring a focus word would silently move the gate mid-word.
    /// An authored <see cref="slotGates"/> entry for that slot still wins.
    /// </summary>
    public bool gateFinalSlotToFinalWave = false;

    /// <summary>Non-zero makes a playtest replayable. 0 seeds from the clock.</summary>
    public int assignmentSeed = 0;

    /// <summary>
    /// Slots withheld until a beat resolves. Level 1 gates slot 3 (the MA of AMA) on
    /// <see cref="SpawnGateRegistry.AboAshShown"/>.
    /// </summary>
    public List<SpawnSlotGate> slotGates = new List<SpawnSlotGate>();

    /// <summary>
    /// Slots whose anti-rush floor differs from <see cref="minSpawnsBeforeNeeded"/>. Level 1 lowers
    /// slot 0 (the E/I of INA) to 1 so the tutorial's first drawing actually restores something.
    /// </summary>
    public List<SpawnSlotFloor> slotFloors = new List<SpawnSlotFloor>();

    /// <summary>Gate token for a flattened slot index, or null when the slot is ungated.</summary>
    public string GateTokenForSlot(int slotIndex)
    {
        if (slotGates == null)
            return null;

        for (int i = 0; i < slotGates.Count; i++)
        {
            SpawnSlotGate gate = slotGates[i];
            if (gate != null && gate.slotIndex == slotIndex && !string.IsNullOrEmpty(gate.gateToken))
                return gate.gateToken;
        }

        return null;
    }

    /// <summary>
    /// Anti-rush floor for a flattened slot index: the slot's own override when one is authored,
    /// otherwise the level-wide <see cref="minSpawnsBeforeNeeded"/>. Falling back rather than
    /// requiring an entry per slot is what keeps a per-slot exception cheap to author and keeps the
    /// four other levels, which want a uniform floor, on an empty list.
    /// </summary>
    public int MinSpawnsBeforeNeededForSlot(int slotIndex)
    {
        if (slotFloors == null)
            return minSpawnsBeforeNeeded;

        for (int i = 0; i < slotFloors.Count; i++)
        {
            SpawnSlotFloor floor = slotFloors[i];
            if (floor != null && floor.slotIndex == slotIndex)
                return floor.minSpawnsBeforeNeeded;
        }

        return minSpawnsBeforeNeeded;
    }

    /// <summary>Clamps hand-edited Inspector values into ranges the director can honour.</summary>
    public void Sanitize()
    {
        if (minSpawnsBeforeNeeded < 0) minSpawnsBeforeNeeded = 0;
        if (neededWeight < 0f) neededWeight = 0f;
        if (neededWeight > 1f) neededWeight = 1f;
        if (starvationTimeout < 0f) starvationTimeout = 0f;
        if (hardStarvationTimeout < starvationTimeout) hardStarvationTimeout = starvationTimeout;
        if (minFillerVariety < 0) minFillerVariety = 0;
        if (offTargetFillerWeight < 0f) offTargetFillerWeight = 0f;
        if (offTargetFillerWeight > 1f) offTargetFillerWeight = 1f;
        if (activeSlotWindow < 1) activeSlotWindow = 1;
        if (choicePairWindow < 0f) choicePairWindow = 0f;
        if (maxConcurrentEnemies < 1) maxConcurrentEnemies = 1;

        // A negative per-slot floor would read as "no floor at all" and hand the player an
        // instant-win opportunity on the slot someone was only trying to loosen.
        if (slotFloors != null)
        {
            for (int i = 0; i < slotFloors.Count; i++)
            {
                SpawnSlotFloor floor = slotFloors[i];
                if (floor == null)
                    continue;

                if (floor.slotIndex < 0) floor.slotIndex = 0;
                if (floor.minSpawnsBeforeNeeded < 0) floor.minSpawnsBeforeNeeded = 0;
            }
        }

        if (openingSpawnSpokenValueId == null) openingSpawnSpokenValueId = "";
        openingSpawnSpokenValueId = openingSpawnSpokenValueId.Trim();
    }
}
