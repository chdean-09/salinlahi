using System;
using System.Collections.Generic;

/// <summary>
/// One flattened slot of the level's target text, in reading order across all focus words.
/// Level 1's INA + AMA flattens to [symbol.ei, symbol.na, symbol.a, symbol.ma].
/// </summary>
public sealed class SpawnSlot
{
    /// <summary>Canonical symbol id, e.g. "symbol.ma". Matches BaybayinCharacterSO.stableId.</summary>
    public readonly string SymbolStableId;

    /// <summary>Owning focus word's stableId, so restoration state can be read back per word.</summary>
    public readonly string WordStableId;

    /// <summary>Index of this slot inside its own word's decomposition.</summary>
    public readonly int SlotIndexInWord;

    /// <summary>Stable occurrence identity for sentence/objective targets.</summary>
    public readonly string OccurrenceId;

    /// <summary>
    /// Beat that must resolve before this slot is fillable, or null/empty when ungated.
    /// Level 1's final slot carries "abo_ash_shown".
    /// </summary>
    public readonly string GateToken;

    public SpawnSlot(
        string symbolStableId,
        string wordStableId,
        int slotIndexInWord,
        string gateToken = null,
        string occurrenceId = null)
    {
        SymbolStableId = symbolStableId;
        WordStableId = wordStableId;
        SlotIndexInWord = slotIndexInWord;
        GateToken = gateToken;
        OccurrenceId = occurrenceId;
    }

    public bool IsGated => !string.IsNullOrEmpty(GateToken);
}

/// <summary>What a given spawn is for.</summary>
public enum SpawnAssignmentRole
{
    /// <summary>Carries a symbol that fills a currently fillable slot.</summary>
    Needed,

    /// <summary>Carries a deliberate non-advancing symbol.</summary>
    Filler,

    /// <summary>
    /// Every remaining slot is gated. Nothing can advance the level right now, so this spawn is
    /// filler and the level is structurally unable to complete until a gate opens.
    /// </summary>
    HoldForGate,
}

/// <summary>Everything the director needs about the world for one assignment decision.</summary>
public struct SpawnAssignmentRequest
{
    /// <summary>
    /// Restoration state per flattened slot, in the same order as the director's slot list.
    /// Re-read every spawn rather than tracked internally, because
    /// accepted clue resolution advances the scene objective and may unlock a new active unit;
    /// a privately-tracked cursor would drift after that transition (the legacy fallback uses the
    /// same read path for compatibility).
    /// </summary>
    public IReadOnlyList<bool> RestoredSlots;

    /// <summary>Gate tokens that have resolved. Null is treated as "none resolved".</summary>
    public IReadOnlyCollection<string> OpenGateTokens;

    /// <summary>
    /// Pause-aware elapsed seconds. The caller must exclude dialogue, intermission waves and the
    /// gated beat itself; a timer that runs while nothing can spawn fires the instant play resumes.
    /// </summary>
    public float Now;

    /// <summary>Live enemies on screen, checked before issuing a choice pair.</summary>
    public int ActiveEnemyCount;

    /// <summary>
    /// Symbols legal for this wave, from WaveDefinition.characters. Filler never leaves this set.
    /// Null or empty means the director may use any symbol it knows about.
    /// </summary>
    public IReadOnlyList<string> WaveSymbolWhitelist;

    /// <summary>Level pool minus target symbols, used only when offTargetFillerWeight > 0.</summary>
    public IReadOnlyList<string> OffTargetSymbols;
}

/// <summary>The director's decision for one spawn.</summary>
public struct SpawnAssignment
{
    /// <summary>Symbol this enemy carries.</summary>
    public string SymbolStableId;

    public SpawnAssignmentRole Role;

    /// <summary>Flattened slot this fills, or -1 for filler.</summary>
    public int SlotIndex;

    /// <summary>
    /// True when this spawn opens the guaranteed choice moment. The caller must also spawn
    /// <see cref="PairedDecoySymbolStableId"/> within the policy's pair window, and must suspend
    /// active-clue marking for that window.
    /// </summary>
    public bool StartsChoicePair;

    /// <summary>Second member of the choice pair. Null unless <see cref="StartsChoicePair"/>.</summary>
    public string PairedDecoySymbolStableId;

    /// <summary>
    /// True once hard starvation has fired: the caller should raise the clue channel from Glyph to
    /// Glyph | LatinText using the level's existing audioVisualFallback path.
    /// </summary>
    public bool EscalateClueChannel;

    /// <summary>True when this spawn was forced by either starvation timer rather than drawn.</summary>
    public bool WasForced;

    /// <summary>
    /// True on the one spawn that satisfied the policy's opening directive. Exposed because the
    /// beat that plays around it - Level 1's Abo introduction card - has to know this is the
    /// authored opening enemy rather than an ordinary filler that happens to carry the same symbol.
    /// </summary>
    public bool IsOpeningDirective;

    public static SpawnAssignment None => new SpawnAssignment
    {
        SymbolStableId = null,
        Role = SpawnAssignmentRole.Filler,
        SlotIndex = -1,
    };
}

/// <summary>
/// Injectable randomness. Exists so a test can prove the floor holds regardless of the draw, which
/// a seeded System.Random cannot express directly.
/// </summary>
public interface ISpawnRandom
{
    /// <summary>Uniform in [0,1).</summary>
    double NextDouble();

    /// <summary>Uniform integer in [0, exclusiveMax).</summary>
    int NextInt(int exclusiveMax);
}

/// <summary>Production randomness.</summary>
public sealed class SystemSpawnRandom : ISpawnRandom
{
    private readonly Random _random;

    public SystemSpawnRandom(int seed)
    {
        _random = seed == 0 ? new Random() : new Random(seed);
    }

    public double NextDouble() => _random.NextDouble();

    public int NextInt(int exclusiveMax) =>
        exclusiveMax <= 0 ? 0 : _random.Next(exclusiveMax);
}
