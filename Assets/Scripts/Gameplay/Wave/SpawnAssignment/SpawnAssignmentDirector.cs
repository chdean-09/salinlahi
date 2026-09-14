using System.Collections.Generic;

/// <summary>
/// Decides which symbol each spawning enemy carries.
///
/// Completing the target text wins the level immediately, so this sequence is the level's length
/// control and its content-delivery schedule at the same time. Uniform random assignment supplies
/// neither: a lucky Level 1 run fills all four slots in four draws and ends before Iligaw has
/// appeared, while an unlucky one withholds the needed symbol long enough to read as broken.
///
/// The replacement makes the needed symbol a scheduled resource with a floor (an anti-rush spawn
/// count) and a ceiling (two starvation timers), with deliberate filler in between.
///
/// Deliberately free of UnityEngine types so every criterion is an EditMode test without a scene,
/// matching <see cref="ActiveClueSelector"/>. Unity wiring lives in the calling spawner.
///
/// Design source: docs/design/spawn-assignment-system.md.
/// </summary>
public sealed class SpawnAssignmentDirector
{
    private readonly List<SpawnSlot> _slots = new List<SpawnSlot>();
    private readonly SpawnAssignmentPolicy _policy;
    private readonly ISpawnRandom _random;

    // Monotonic counter used as the "recently seen" clock for both filler and needed selection.
    private int _sequence;

    // Symbol -> _sequence when it was last emitted as filler / last offered as needed.
    private readonly Dictionary<string, int> _lastFillerSequence = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _lastNeededSequence = new Dictionary<string, int>();

    // Identity of the currently armed eligible set. When this changes - a slot filled, or a gate
    // opened - the slot re-arms: the floor applies again and both starvation flags clear.
    private string _armedWindowKey;
    private float _armedAt;
    private int _spawnsSinceArmed;
    private bool _softForced;
    private bool _hardForced;

    private bool _choiceConsumed;
    private bool _choiceArmed;

    // Scratch buffers reused every call so a per-spawn assignment allocates nothing.
    private readonly List<int> _window = new List<int>();
    private readonly List<int> _eligible = new List<int>();
    private readonly List<string> _pool = new List<string>();
    private readonly HashSet<string> _excluded = new HashSet<string>();

    public SpawnAssignmentDirector(
        IReadOnlyList<SpawnSlot> slots,
        SpawnAssignmentPolicy policy,
        ISpawnRandom random = null)
    {
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                    _slots.Add(slots[i]);
            }
        }

        _policy = policy ?? new SpawnAssignmentPolicy();
        _policy.Sanitize();
        _random = random ?? new SystemSpawnRandom(_policy.assignmentSeed);
    }

    public IReadOnlyList<SpawnSlot> Slots => _slots;

    /// <summary>True once hard starvation has fired and until the starved slot is restored.</summary>
    public bool IsClueEscalated => _hardForced;

    /// <summary>True while the guaranteed choice moment is still owed.</summary>
    public bool IsChoiceMomentPending =>
        _policy.choiceMomentSlotIndex >= 0 && !_choiceConsumed;

    /// <summary>
    /// Picks the symbol for one spawn. Pure with respect to everything except this director's own
    /// pacing state, so the same request twice can legitimately differ.
    /// </summary>
    public SpawnAssignment AssignNext(SpawnAssignmentRequest request)
    {
        _sequence++;

        if (_slots.Count == 0)
            return SpawnAssignment.None;

        BuildWindow(request);
        if (_window.Count == 0)
            return SpawnAssignment.None; // every slot restored - the level is over

        BuildEligible(request);
        ArmIfWindowChanged(request.Now);

        // Gating lives here, in the construction of the eligible set, rather than inside the draw.
        // That is what keeps a gated symbol out of FILLER as well as out of the needed slot, and
        // what makes the level structurally unable to complete while a gate is shut.
        if (_eligible.Count == 0)
        {
            return new SpawnAssignment
            {
                SymbolStableId = PickFiller(request),
                Role = SpawnAssignmentRole.HoldForGate,
                SlotIndex = -1,
            };
        }

        float elapsed = request.Now - _armedAt;

        // Ceiling. The hard timer is sticky: once the player has demonstrated they cannot read the
        // glyph, every spawn carries it until the slot fills.
        if (_hardForced || elapsed >= _policy.hardStarvationTimeout)
        {
            _hardForced = true;
            return Needed(request, forced: true);
        }

        if (!_softForced && elapsed >= _policy.starvationTimeout)
        {
            _softForced = true;
            return Needed(request, forced: true);
        }

        // Floor. A spawn count, not a timer: what is rationed is opportunities, not seconds.
        if (_spawnsSinceArmed < _policy.minSpawnsBeforeNeeded)
        {
            _spawnsSinceArmed++;
            return Filler(request);
        }

        _spawnsSinceArmed++;
        if (_random.NextDouble() < _policy.neededWeight)
            return Needed(request, forced: false);

        return Filler(request);
    }

    // ------------------------------------------------------------------ window and arming

    private void BuildWindow(SpawnAssignmentRequest request)
    {
        _window.Clear();
        for (int i = 0; i < _slots.Count && _window.Count < _policy.activeSlotWindow; i++)
        {
            if (!IsRestored(request, i))
                _window.Add(i);
        }
    }

    private void BuildEligible(SpawnAssignmentRequest request)
    {
        _eligible.Clear();
        for (int i = 0; i < _window.Count; i++)
        {
            if (IsGateOpen(request, _slots[_window[i]].GateToken))
                _eligible.Add(_window[i]);
        }
    }

    /// <summary>
    /// Re-arms when the eligible set changes. One mechanism covers both "a slot was filled" and
    /// "a gate opened", which is why the starvation clock for a gated slot cannot start early and
    /// bank a forced-spawn debt against the beat.
    /// </summary>
    private void ArmIfWindowChanged(float now)
    {
        string key = BuildWindowKey();
        if (key == _armedWindowKey)
            return;

        _armedWindowKey = key;
        _armedAt = now;
        _spawnsSinceArmed = 0;
        _softForced = false;
        _hardForced = false;
    }

    private string BuildWindowKey()
    {
        if (_eligible.Count == 0)
            return "gated";

        string key = "";
        for (int i = 0; i < _eligible.Count; i++)
            key += _eligible[i] + ",";

        return key;
    }

    // ------------------------------------------------------------------ needed

    private SpawnAssignment Needed(SpawnAssignmentRequest request, bool forced)
    {
        int slotIndex = PickNeededSlot();
        SpawnSlot slot = _slots[slotIndex];
        _lastNeededSequence[slot.SymbolStableId] = _sequence;

        var assignment = new SpawnAssignment
        {
            SymbolStableId = slot.SymbolStableId,
            Role = SpawnAssignmentRole.Needed,
            SlotIndex = slotIndex,
            EscalateClueChannel = _hardForced,
            WasForced = forced,
        };

        if (!ShouldIssueChoicePair(request, slotIndex))
            return assignment;

        string decoy = PickChoiceDecoy(request, slot.SymbolStableId);
        if (decoy == null)
        {
            // No legal second member. The directive carries forward rather than being spent, so
            // the choice moment stays a guarantee.
            return Filler(request);
        }

        _choiceConsumed = true;
        assignment.StartsChoicePair = true;
        assignment.PairedDecoySymbolStableId = decoy;
        return assignment;
    }

    /// <summary>
    /// With a window of one this is simply the cursor. With a wider window it is the least recently
    /// offered slot, so a sentence-length target cycles its working set instead of hammering the
    /// leftmost unfilled slot.
    /// </summary>
    private int PickNeededSlot()
    {
        int best = _eligible[0];
        int bestSequence = LastNeededSequence(_slots[best].SymbolStableId);

        for (int i = 1; i < _eligible.Count; i++)
        {
            int candidate = _eligible[i];
            int sequence = LastNeededSequence(_slots[candidate].SymbolStableId);
            if (sequence < bestSequence)
            {
                best = candidate;
                bestSequence = sequence;
            }
        }

        return best;
    }

    private int LastNeededSequence(string symbolId) =>
        _lastNeededSequence.TryGetValue(symbolId, out int sequence) ? sequence : -1;

    // ------------------------------------------------------------------ choice moment

    private bool ShouldIssueChoicePair(SpawnAssignmentRequest request, int slotIndex)
    {
        if (_choiceConsumed || _policy.choiceMomentSlotIndex < 0)
            return false;

        // The directive arms when the level REACHES the designated slot and stays armed until it is
        // spent. Binding it to that one slot instead lost the guarantee outright: in simulation the
        // budget was full for both of the designated slot's needed spawns, the player filled the
        // slot anyway, and the choice moment silently never happened.
        if (slotIndex >= _policy.choiceMomentSlotIndex)
            _choiceArmed = true;

        if (!_choiceArmed)
            return false;

        // Budget precondition. Failing it must not consume the directive.
        return request.ActiveEnemyCount + 2 <= _policy.maxConcurrentEnemies;
    }

    /// <summary>
    /// The pair's non-advancing member: a real target symbol the player must actively reject.
    /// Never the needed symbol, and never a gated one.
    /// </summary>
    private string PickChoiceDecoy(SpawnAssignmentRequest request, string neededSymbolId)
    {
        BuildFillerPool(request);
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != neededSymbolId)
                return _pool[i];
        }

        return null;
    }

    // ------------------------------------------------------------------ filler

    private SpawnAssignment Filler(SpawnAssignmentRequest request)
    {
        return new SpawnAssignment
        {
            SymbolStableId = PickFiller(request),
            Role = SpawnAssignmentRole.Filler,
            SlotIndex = -1,
            EscalateClueChannel = _hardForced,
        };
    }

    private string PickFiller(SpawnAssignmentRequest request)
    {
        BuildFillerPool(request);
        if (_pool.Count == 0)
            return null;

        // Least recently seen, so the fallback duplicates still rotate instead of repeating one
        // glyph while a slot is stuck.
        string best = _pool[0];
        int bestSequence = LastFillerSequence(best);
        for (int i = 1; i < _pool.Count; i++)
        {
            int sequence = LastFillerSequence(_pool[i]);
            if (sequence < bestSequence)
            {
                best = _pool[i];
                bestSequence = sequence;
            }
        }

        _lastFillerSequence[best] = _sequence;
        return best;
    }

    private int LastFillerSequence(string symbolId) =>
        _lastFillerSequence.TryGetValue(symbolId, out int sequence) ? sequence : -1;

    /// <summary>
    /// Filler pool, in the priority order the design fixes: later-needed first, restored duplicates
    /// only as a variety fallback, off-target only when explicitly weighted.
    /// </summary>
    private void BuildFillerPool(SpawnAssignmentRequest request)
    {
        _pool.Clear();
        BuildExclusions(request);

        // 1. Later-needed: unrestored, ungated, outside the current window.
        for (int i = 0; i < _slots.Count; i++)
        {
            if (IsRestored(request, i) || _window.Contains(i))
                continue;

            if (!IsGateOpen(request, _slots[i].GateToken))
                continue;

            AddCandidate(request, _slots[i].SymbolStableId);
        }

        // 2. Variety fallback. Load-bearing in Level 1: while NA is the cursor the only ungated
        // later-needed symbol is A, so without this every filler for ~20 seconds is A.
        if (_pool.Count < _policy.minFillerVariety)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (IsRestored(request, i))
                    AddCandidate(request, _slots[i].SymbolStableId);
            }
        }

        // 3. Off-target. Zero for Level 1, whose pool is exactly its target.
        if (_policy.offTargetFillerWeight > 0f
            && request.OffTargetSymbols != null
            && request.OffTargetSymbols.Count > 0
            && _random.NextDouble() < _policy.offTargetFillerWeight)
        {
            _pool.Clear();
            for (int i = 0; i < request.OffTargetSymbols.Count; i++)
                AddCandidate(request, request.OffTargetSymbols[i]);
        }

        if (_pool.Count > 0)
            return;

        // Last resort: any restored symbol the exclusions still permit, so a spawn is never
        // silently dropped.
        for (int i = 0; i < _slots.Count; i++)
        {
            if (IsRestored(request, i))
                AddCandidate(request, _slots[i].SymbolStableId);
        }
    }

    /// <summary>
    /// Symbols filler may never carry: anything that would advance a fillable slot, and anything
    /// still behind a closed gate.
    /// </summary>
    private void BuildExclusions(SpawnAssignmentRequest request)
    {
        _excluded.Clear();

        for (int i = 0; i < _eligible.Count; i++)
            _excluded.Add(_slots[_eligible[i]].SymbolStableId);

        for (int i = 0; i < _slots.Count; i++)
        {
            if (IsRestored(request, i))
                continue;

            if (!IsGateOpen(request, _slots[i].GateToken))
                _excluded.Add(_slots[i].SymbolStableId);
        }
    }

    private void AddCandidate(SpawnAssignmentRequest request, string symbolId)
    {
        if (string.IsNullOrEmpty(symbolId) || _excluded.Contains(symbolId) || _pool.Contains(symbolId))
            return;

        // The wave's authored character list still decides what may appear at all.
        if (request.WaveSymbolWhitelist != null
            && request.WaveSymbolWhitelist.Count > 0
            && !Contains(request.WaveSymbolWhitelist, symbolId))
        {
            return;
        }

        _pool.Add(symbolId);
    }

    // ------------------------------------------------------------------ helpers

    private static bool IsRestored(SpawnAssignmentRequest request, int slotIndex) =>
        request.RestoredSlots != null
        && slotIndex < request.RestoredSlots.Count
        && request.RestoredSlots[slotIndex];

    private static bool IsGateOpen(SpawnAssignmentRequest request, string gateToken)
    {
        if (string.IsNullOrEmpty(gateToken))
            return true;

        if (request.OpenGateTokens == null)
            return false;

        foreach (string token in request.OpenGateTokens)
        {
            if (token == gateToken)
                return true;
        }

        return false;
    }

    private static bool Contains(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return true;
        }

        return false;
    }
}
