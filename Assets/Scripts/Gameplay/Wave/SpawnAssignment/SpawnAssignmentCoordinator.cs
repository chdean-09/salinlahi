using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-side host for <see cref="SpawnAssignmentDirector"/>: builds the flattened slot list from
/// the level's focus words, keeps the pause-aware clock, reads restoration state back from
/// <see cref="ActiveCluePresenter"/>, and resolves a chosen symbol to the enemy that embodies it.
///
/// All decision logic lives in the director, which is free of UnityEngine types. This class is the
/// wiring only, so what it can get wrong is limited to lookups and lifecycle.
///
/// Design source: docs/design/spawn-assignment-system.md.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpawnAssignmentCoordinator : MonoBehaviour
{
    private readonly SpawnGateRegistry _gates = new SpawnGateRegistry();
    private readonly List<SpawnSlot> _slots = new List<SpawnSlot>();
    private readonly List<bool> _restoredBuffer = new List<bool>();
    private readonly List<string> _waveSymbolBuffer = new List<string>();
    private readonly List<string> _offTargetBuffer = new List<string>();

    private SpawnAssignmentDirector _director;
    private LevelConfigSO _level;
    private bool _loggedEnemyRosterFallback;
    private ActiveCluePresenter _presenter;

    /// <summary>
    /// Pause-aware seconds since the level's spawning began. Advanced in Update and frozen while
    /// paused or in dialogue: a starvation timer that runs while nothing can spawn fires the
    /// instant gameplay resumes, which is the opposite of what gating a beat is for.
    /// </summary>
    private float _clock;

    private bool _clockFrozen;

    // Consecutive spawns spent with every remaining slot gated.
    private int _heldForGateSpawns;

    private const int GateStuckSpawnThreshold = 12;

    public SpawnGateRegistry Gates => _gates;

    /// <summary>The flattened target slots, exposed read-only so gating is testable without reflection.</summary>
    public IReadOnlyList<SpawnSlot> Slots => _slots;

    /// <summary>Seconds between the two members of a choice pair.</summary>
    public float ChoicePairWindow =>
        _level?.spawnAssignmentPolicy != null ? _level.spawnAssignmentPolicy.choicePairWindow : 0f;

    /// <summary>
    /// True when every slot of every focus word has been restored, i.e. the target text is finished
    /// and the level has nothing left to teach.
    /// </summary>
    public bool IsTargetComplete
    {
        get
        {
            if (_slots.Count == 0)
                return false;

            ActiveClueRestorationState state = _presenter != null ? _presenter.RestorationState : null;
            if (state == null)
                return false;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (!state.IsTargetComplete(_slots[i].WordStableId, _slots[i].SymbolStableId))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// True when the authored wave budget has been spent but the words are not finished, so the
    /// defense must keep going.
    ///
    /// Without this the level hits RefuseCompletionForMissingContent ("combat ended before
    /// restoring focus word slots") and dead-ends: the player cannot finish and cannot retry into a
    /// different outcome, because the wave budget, not their skill, ran out.
    /// </summary>
    public bool WantsOverflow =>
        IsActive
        && _level.spawnAssignmentPolicy != null
        && _level.spawnAssignmentPolicy.allowFinalWaveOverflow
        && !IsTargetComplete;

    /// <summary>True when this level routes spawns through the director rather than legacy random.</summary>
    public bool IsActive =>
        _director != null
        && _level != null
        && _level.activeClueCombatEnabled
        && _slots.Count > 0;

    private void OnEnable()
    {
        EventBus.OnGamePaused += FreezeClock;
        EventBus.OnGameResumed += ResumeClock;
        EventBus.OnDialogueStarted += FreezeClock;
        EventBus.OnDialogueComplete += ResumeClock;
    }

    private void OnDisable()
    {
        EventBus.OnGamePaused -= FreezeClock;
        EventBus.OnGameResumed -= ResumeClock;
        EventBus.OnDialogueStarted -= FreezeClock;
        EventBus.OnDialogueComplete -= ResumeClock;
    }

    private void Update()
    {
        if (!_clockFrozen)
            _clock += Time.deltaTime;
    }

    private void FreezeClock() => _clockFrozen = true;

    private void ResumeClock() => _clockFrozen = false;

    /// <summary>
    /// Rebuilds the schedule for a level. Safe to call on a retry: the gate registry resets, so a
    /// beat the player already saw last attempt does not start open.
    /// </summary>
    public void ApplyLevel(LevelConfigSO level, ActiveCluePresenter presenter)
    {
        _level = level;
        _loggedEnemyRosterFallback = false;
        _presenter = presenter;
        _clock = 0f;
        _clockFrozen = false;
        _gates.Reset();
        _slots.Clear();
        _director = null;

        if (level == null || !level.activeClueCombatEnabled)
            return;

        BuildSlots(level);
        if (_slots.Count == 0)
        {
            DebugLogger.LogWarning(
                "SpawnAssignmentCoordinator: activeClueCombatEnabled but the level's focus words "
                + "decompose to no symbols. Falling back to legacy random assignment.");
            return;
        }

        SpawnAssignmentPolicy policy = level.spawnAssignmentPolicy ?? new SpawnAssignmentPolicy();
        _director = new SpawnAssignmentDirector(_slots, policy);

        OpenRosterGateIfAlreadyMet(level);
    }

    /// <summary>
    /// Re-evaluates the roster gate for this attempt, because <see cref="SpawnGateRegistry.Reset"/>
    /// above just closed every token while enemy introductions are campaign-wide PlayerPrefs that
    /// survive the retry.
    ///
    /// <para>
    /// Without this a second run of a level whose types the player has already met never plays an
    /// introduction, so the post-introduction evaluation in <c>EnemyIntroductionBeat</c> never
    /// fires, so the gated final slot is withheld forever and the level cannot be completed. Both
    /// evaluations are needed: this one covers the already-met start, that one covers the roster
    /// being completed mid-level. <see cref="OpenGate"/> is idempotent, so they may both fire.
    /// </para>
    /// </summary>
    private void OpenRosterGateIfAlreadyMet(LevelConfigSO level)
    {
        LevelRoster.TryOpenRosterGate(
            level, EnemyIntroductionProgress.HasBeenIntroduced, OpenGate);
    }

    /// <summary>Flattens every focus word's decomposition into one ordered slot list.</summary>
    private void BuildSlots(LevelConfigSO level)
    {
        if (level.focusWords == null)
            return;

        SpawnAssignmentPolicy policy = level.spawnAssignmentPolicy ?? new SpawnAssignmentPolicy();

        for (int wordIndex = 0; wordIndex < level.focusWords.Count; wordIndex++)
        {
            FocusWordDefinition word = level.focusWords[wordIndex];
            if (word?.decomposition == null)
                continue;

            for (int slotIndex = 0; slotIndex < word.decomposition.Count; slotIndex++)
            {
                SymbolValueReference reference = word.decomposition[slotIndex];
                if (reference?.symbol == null || string.IsNullOrEmpty(reference.symbol.stableId))
                    continue;

                _slots.Add(new SpawnSlot(
                    reference.symbol.stableId,
                    word.stableId,
                    slotIndex,
                    policy.GateTokenForSlot(_slots.Count)));
            }
        }

        ApplyDerivedFinalSlotGate(policy);
    }

    /// <summary>
    /// Withholds the level's finale slot on <see cref="SpawnGateRegistry.FinalWaveReached"/> when
    /// the level opts in. Derived rather than authored so content edits cannot move the gate off
    /// the finale; skipped for a single-slot level, which would otherwise withhold its own win
    /// condition. <see cref="SpawnSlot.GateToken"/> is readonly, so the entry is replaced, not
    /// mutated, and an authored gate on the chosen slot always wins.
    ///
    /// <para>
    /// The chosen slot is the last one whose symbol occurs EXACTLY ONCE in the flattened list, not
    /// simply the last slot - see <see cref="DerivedFinaleGate"/> for why gating a repeated symbol
    /// withholds nothing. When no symbol is unique the level is left ungated here and reported at
    /// author time by <c>CampaignConfigValidator.ValidateGatedFinale</c>.
    /// </para>
    /// </summary>
    private void ApplyDerivedFinalSlotGate(SpawnAssignmentPolicy policy)
    {
        if (policy == null || !policy.gateFinalSlotToFinalWave || _slots.Count < 2)
            return;

        var symbols = new List<string>(_slots.Count);
        for (int index = 0; index < _slots.Count; index++)
            symbols.Add(_slots[index].SymbolStableId);

        int gateIndex = DerivedFinaleGate.LastUniquelyOccurringIndex(symbols);
        if (gateIndex == DerivedFinaleGate.NoSlot)
            return;

        SpawnSlot target = _slots[gateIndex];
        if (target.IsGated)
            return;

        _slots[gateIndex] = new SpawnSlot(
            target.SymbolStableId, target.WordStableId, target.SlotIndexInWord,
            SpawnGateRegistry.FinalWaveReached);
    }

    /// <summary>Marks a beat resolved, ungating any slot that was waiting on it.</summary>
    public void OpenGate(string token)
    {
        if (_gates.Open(token))
            DebugLogger.Log($"SpawnAssignmentCoordinator: gate '{token}' resolved.");
    }

    /// <summary>
    /// The director's decision for the next spawn, or <see cref="SpawnAssignment.None"/> when this
    /// level does not use the schedule.
    /// </summary>
    public SpawnAssignment AssignNext(WaveDefinition wave)
    {
        if (!IsActive)
            return SpawnAssignment.None;

        SpawnAssignment assignment = _director.AssignNext(BuildRequest(wave));
        WarnIfGateStuck(assignment);
        return assignment;
    }

    /// <summary>
    /// A gate with no one to open it makes the level unwinnable, and the symptom - enemies keep
    /// spawning but the last slot never becomes available - looks like a content bug rather than a
    /// wiring bug. Say so once, loudly, naming the token nobody opened.
    /// </summary>
    private void WarnIfGateStuck(SpawnAssignment assignment)
    {
        if (assignment.Role != SpawnAssignmentRole.HoldForGate)
        {
            _heldForGateSpawns = 0;
            return;
        }

        _heldForGateSpawns++;
        if (_heldForGateSpawns != GateStuckSpawnThreshold)
            return;

        DebugLogger.LogError(
            $"SpawnAssignmentCoordinator: every remaining slot has been gated for "
            + $"{GateStuckSpawnThreshold} spawns, so this level cannot be completed. Waiting on: "
            + $"{DescribeClosedGates()}. Something must call OpenGate for that token.");
    }

    private string DescribeClosedGates()
    {
        var tokens = new List<string>();
        for (int i = 0; i < _slots.Count; i++)
        {
            string token = _slots[i].GateToken;
            if (string.IsNullOrEmpty(token) || _gates.IsOpen(token) || tokens.Contains(token))
                continue;

            tokens.Add(token);
        }

        return tokens.Count == 0 ? "(none)" : string.Join(", ", tokens);
    }

    private SpawnAssignmentRequest BuildRequest(WaveDefinition wave)
    {
        return new SpawnAssignmentRequest
        {
            RestoredSlots = BuildRestoredFlags(),
            OpenGateTokens = _gates.OpenTokens,
            Now = _clock,
            ActiveEnemyCount = ActiveEnemyTracker.Instance != null
                ? ActiveEnemyTracker.Instance.ActiveCount
                : 0,
            WaveSymbolWhitelist = BuildWaveSymbols(wave),
            OffTargetSymbols = BuildOffTargetSymbols(),
        };
    }

    /// <summary>
    /// Re-reads restoration state every spawn rather than tracking it here, because
    /// ActiveClueRestorationState.Apply restores every matching slot across all words at once: one
    /// correct draw can fill several slots, and a private cursor would drift out of step.
    /// </summary>
    private IReadOnlyList<bool> BuildRestoredFlags()
    {
        _restoredBuffer.Clear();
        ActiveClueRestorationState state = _presenter != null ? _presenter.RestorationState : null;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (state == null)
            {
                _restoredBuffer.Add(false);
                continue;
            }

            _restoredBuffer.Add(state.IsTargetComplete(_slots[i].WordStableId, _slots[i].SymbolStableId));
        }

        return _restoredBuffer;
    }

    private IReadOnlyList<string> BuildWaveSymbols(WaveDefinition wave)
    {
        _waveSymbolBuffer.Clear();
        if (wave?.characters == null)
            return _waveSymbolBuffer;

        for (int i = 0; i < wave.characters.Count; i++)
        {
            BaybayinCharacterSO character = wave.characters[i];
            if (character != null && !string.IsNullOrEmpty(character.stableId))
                _waveSymbolBuffer.Add(character.stableId);
        }

        return _waveSymbolBuffer;
    }

    /// <summary>
    /// Level pool minus the target's own symbols. Read from cumulativeSymbolPool, which is derived
    /// from each symbol's first-introduction level, rather than a hand-authored parallel list.
    /// </summary>
    private IReadOnlyList<string> BuildOffTargetSymbols()
    {
        _offTargetBuffer.Clear();
        if (_level?.cumulativeSymbolPool == null)
            return _offTargetBuffer;

        for (int i = 0; i < _level.cumulativeSymbolPool.Count; i++)
        {
            SymbolValueReference reference = _level.cumulativeSymbolPool[i];
            if (reference?.symbol == null || string.IsNullOrEmpty(reference.symbol.stableId))
                continue;

            if (IsTargetSymbol(reference.symbol.stableId))
                continue;

            if (!_offTargetBuffer.Contains(reference.symbol.stableId))
                _offTargetBuffer.Add(reference.symbol.stableId);
        }

        return _offTargetBuffer;
    }

    private bool IsTargetSymbol(string symbolStableId)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].SymbolStableId == symbolStableId)
                return true;
        }

        return false;
    }

    /// <summary>Resolves a scheduled symbol to the character asset carrying it.</summary>
    public BaybayinCharacterSO ResolveCharacter(string symbolStableId, WaveDefinition wave)
    {
        if (string.IsNullOrEmpty(symbolStableId))
            return null;

        BaybayinCharacterSO fromWave = FindCharacter(wave?.characters, symbolStableId);
        if (fromWave != null)
            return fromWave;

        return FindCharacter(_level != null ? _level.allowedCharacters : null, symbolStableId);
    }

    private static BaybayinCharacterSO FindCharacter(
        List<BaybayinCharacterSO> candidates, string symbolStableId)
    {
        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null && candidates[i].stableId == symbolStableId)
                return candidates[i];
        }

        return null;
    }

    /// <summary>
    /// The enemy that embodies a symbol. Level 1 is a clean bijection - Iligaw is E/I, Nawalang
    /// Mukha is NA, Abo ng Simula is A, Mantsa is MA - so choosing the symbol chooses the enemy,
    /// and the identity the glyph badge asserts stays true. When the wave's own list does not
    /// carry the symbol we fall back to the level roster, exactly as ResolveCharacter does: a wave
    /// that narrows its enemyTypes must not be able to put a needed symbol on a body that
    /// contradicts its badge. Returns null only when no enemy on the level owns the symbol, and
    /// the caller then keeps its own type roll.
    /// </summary>
    public EnemyDataSO ResolveEnemyData(string symbolStableId, WaveDefinition wave)
    {
        if (string.IsNullOrEmpty(symbolStableId))
            return null;

        EnemyDataSO fromWave = FindEnemyData(wave?.enemyTypes, symbolStableId);
        if (fromWave != null)
            return fromWave;

        EnemyDataSO fromLevel = FindEnemyData(
            _level != null ? _level.allowedEnemyTypes : null, symbolStableId);
        if (fromLevel != null && !_loggedEnemyRosterFallback)
        {
            // Once per level: a narrowed wave is authoring drift, and the validator's
            // WAVE_ROSTER_NARROWS_RESTORATION check should have caught it before it shipped.
            _loggedEnemyRosterFallback = true;
            DebugLogger.LogWarning(
                "SpawnAssignmentCoordinator: wave enemyTypes did not carry '" + symbolStableId
                + "'; fell back to the level roster (" + fromLevel.name + "). The wave narrows the "
                + "level's enemy roster - see WAVE_ROSTER_NARROWS_RESTORATION.");
        }

        return fromLevel;
    }

    private static EnemyDataSO FindEnemyData(List<EnemyDataSO> candidates, string symbolStableId)
    {
        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyDataSO data = candidates[i];
            if (data?.assignedCharacter != null && data.assignedCharacter.stableId == symbolStableId)
                return data;
        }

        return null;
    }
}
