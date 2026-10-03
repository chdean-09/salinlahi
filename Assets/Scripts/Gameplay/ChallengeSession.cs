using System;
using System.Collections.Generic;
using System.Linq;

public enum ChallengeSessionState
{
    Idle,
    Entry,
    Active,
    SupportiveRetry,
    HintShown,
    Penalty,
    CheckpointReset,
    Paused,
    Success,
    TimedOut,
    Exited,
    Completed,
    Failed
}

public enum ChallengeSessionEvent
{
    None,
    Entry,
    Entered,
    UnitStarted,
    TraceAccepted,
    PlacementAccepted,
    RestorationAccepted,
    SupportiveRetry,
    RetryOpened,
    HintShown,
    HintApplied,
    TimedOut,
    PenaltyApplied,
    CheckpointReset,
    CheckpointReopened,
    MemoryRevealStarted,
    MemoryRevealTicked,
    MemoryRecallStarted,
    TimerTicked,
    Paused,
    Resumed,
    UnitSucceeded,
    Completed,
    Exited,
    Failed
}

public sealed class ChallengeSession
{
    private readonly ChallengeSequenceSO _sequence;
    private readonly List<string> _committedOccurrenceIds = new List<string>();
    private readonly HashSet<string> _currentProgress = new HashSet<string>();
    private int _checkpointUnitIndex;
    private List<string> _checkpointCommitted = new List<string>();
    private int _checkpointSlotIndex;
    private float _checkpointTime;
    private float _checkpointMemoryReveal;
    private int _currentUnitIndex;
    private int _currentSlotIndex;
    private int _errors;
    private int _hintsUsed;
    private float _remainingTime;
    private float _memoryRevealRemaining;
    private ChallengeCluePolicy _cluePolicy;
    private string _hintOccurrenceId;

    private readonly ChallengeTierPolicy _policy;
    private readonly IChallengeEvidenceSink _evidence;

    public ChallengeSession(
        ChallengeSequenceSO sequence,
        int startingHearts = 3,
        ChallengeTierPolicy policy = null,
        IChallengeEvidenceSink evidence = null)
    {
        _sequence = sequence;
        _policy = ResolveEffectivePolicy(policy);
        _evidence = evidence;
        HeartsRemaining = startingHearts;
        State = ChallengeSessionState.Idle;
    }

    private int _emergencyHintsUsed;

    /// <summary>Emergency hints consumed this level attempt (tier 5). Survives checkpoint resets.</summary>
    public int EmergencyHintsUsed => _emergencyHintsUsed;

    /// <summary>Recorded score deduction from emergency hints; consumed by Results (SALIN-202).</summary>
    public float EmergencyHintScorePenalty =>
        _policy != null && _policy.emergencyHintEnabled
            ? _emergencyHintsUsed * _policy.emergencyHintScorePenalty
            : 0f;

    // -------------------------------------------------------------------------
    // SALIN-231. Read-only hint-economy queries.
    //
    // The hint modal has to answer "is a hint available, how many are left, what
    // does one cost" BEFORE the player confirms, and answering must not spend the
    // hint. Every member below is a pure query: none mutates, none notifies.
    //
    // ⚠️ ALL OF THESE RESOLVE THROUGH _policy, which ResolveEffectivePolicy has
    // already snapped to ChallengeTierPolicy.ForTier(tier) for any authored tier
    // 1-5. Reading emergencyHintEnabled off a LevelConfigSO instead would report
    // the STALE serialized flag — Level5_Config.asset carries `tier: 5` beside
    // `emergencyHintEnabled: 0`, and ForTier(5) makes it true. See SALIN-222's
    // note on this ticket; it has already misled one reader.
    // -------------------------------------------------------------------------

    /// <summary>Sentinel for <see cref="EmergencyHintsRemaining"/> when no budget is enforced.</summary>
    public const int UnlimitedHints = int.MaxValue;

    /// <summary>
    /// True when the effective policy meters hints at all — tier 5 only today
    /// (<see cref="ChallengeTierPolicy.ForTier"/>). Tiers 1-4 give unlimited free hints,
    /// so both the budget and the penalty are inert there.
    /// </summary>
    public bool HintBudgetIsLimited => _policy != null && _policy.emergencyHintEnabled;

    /// <summary>
    /// Hints left this level attempt, or <see cref="UnlimitedHints"/> when unmetered.
    /// The sentinel rather than a nullable keeps the caller's arithmetic total.
    /// </summary>
    public int EmergencyHintsRemaining => HintBudgetIsLimited
        ? Math.Max(0, Math.Max(0, _policy.emergencyHintsPerAttempt) - _emergencyHintsUsed)
        : UnlimitedHints;

    /// <summary>
    /// Fraction of metric.score one hint costs (0.10 at tier 5), or 0 when unmetered.
    /// ⚠️ This is SCORE, not stars: LevelResultsCalculator derives stars from hearts and
    /// the accuracies alone and applies the penalty to score only, so copy built on this
    /// must not say "stars".
    /// </summary>
    public float EmergencyHintCostFraction => HintBudgetIsLimited ? _policy.emergencyHintScorePenalty : 0f;

    /// <summary>The budget existed and is spent — the state that drives "No Hints Left".</summary>
    public bool IsHintExhausted => HintBudgetIsLimited && EmergencyHintsRemaining <= 0;

    /// <summary>
    /// Exactly the conjunction <see cref="RequestHint"/> enforces, factored out so the
    /// modal's disclosure and the guard cannot drift apart. RequestHint calls this, so
    /// an edit to one is an edit to both — a query that disagreed with the guard would
    /// show the player a wrong cost or a wrong budget with nothing failing.
    /// </summary>
    public bool CanRequestHint
    {
        get
        {
            if (!CanSubmit() || !CurrentUnit.allowHint)
                return false;
            if (IsHintExhausted)
                return false;
            return CurrentUnit.mode == ChallengeMode.GuidedTracing
                ? CurrentUnit.tokens != null && _currentSlotIndex < CurrentUnit.tokens.Length
                : CurrentUnit.slots != null && _currentSlotIndex < CurrentUnit.slots.Length;
        }
    }

    private bool HeartPenaltiesEnabled => _policy == null || _policy.heartPenaltiesEnabled;

    // A set tier overrides per-unit error limits; tier 0 / null preserves legacy unit data.
    private int ErrorThreshold => _policy != null && _policy.tier > 0
        ? Math.Max(1, _policy.errorsPerPenalty)
        : Math.Max(1, CurrentUnit.maxErrors);

    public ChallengeSessionState State { get; private set; }
    public ChallengeSessionEvent LastEvent { get; private set; }
    public int CurrentUnitIndex => _currentUnitIndex;
    public int CurrentSlotIndex => _currentSlotIndex;
    public int Errors => _errors;
    public int HintsUsed => _hintsUsed;
    public int HeartPenalties { get; private set; }
    public int HeartsRemaining { get; private set; }
    public float RemainingTime => _remainingTime;
    public ChallengeCluePolicy CluePolicy => _cluePolicy;
    public ChallengeUnitDefinition CurrentUnitDefinition => _sequence != null && _sequence.units != null && _currentUnitIndex >= 0 && _currentUnitIndex < _sequence.units.Length ? CurrentUnit : null;
    public int RequiredSlotCount => CurrentUnit == null
        ? 0
        : CurrentUnit.mode == ChallengeMode.GuidedTracing
            ? (CurrentUnit.tokens == null ? 0 : CurrentUnit.tokens.Length)
            : (CurrentUnit.slots == null ? 0 : CurrentUnit.slots.Length);
    public bool IsMemoryRevealActive => CurrentUnit != null && CurrentUnit.mode == ChallengeMode.TimedMemory && _memoryRevealRemaining > 0f;
    public float MemoryRevealRemaining => _memoryRevealRemaining;
    public string HintOccurrenceId => _hintOccurrenceId;
    public IReadOnlyCollection<string> CommittedOccurrenceIds => _committedOccurrenceIds.AsReadOnly();
    public IReadOnlyCollection<string> CurrentProgress => _currentProgress;
    public event Action<ChallengeSession> Changed;

    public void Enter()
    {
        if (_sequence == null || _sequence.units == null || _sequence.units.Length == 0)
            throw new InvalidOperationException("A challenge session requires at least one unit.");
        if (State != ChallengeSessionState.Idle)
            return;

        State = ChallengeSessionState.Entry;
        NotifyChanged(ChallengeSessionEvent.Entry);
        _currentUnitIndex = 0;
        _committedOccurrenceIds.Clear();
        OpenCurrentUnit();
        State = ChallengeSessionState.Active;
        NotifyChanged(ChallengeSessionEvent.Entered);
        if (IsMemoryRevealActive)
            NotifyChanged(ChallengeSessionEvent.MemoryRevealStarted);
    }

    public void SubmitTrace(string characterId)
    {
        if (!CanSubmit() || CurrentUnit.mode != ChallengeMode.GuidedTracing)
            return;
        if (CurrentUnit.tokens == null || _currentSlotIndex >= CurrentUnit.tokens.Length)
            return;

        ChallengeTokenDefinition token = CurrentUnit.tokens[_currentSlotIndex];
        bool correct = token.targetCharacter != null
            && string.Equals(token.targetCharacter.characterID, characterId, StringComparison.OrdinalIgnoreCase);
        RecordTokenEvidence(token, correct);
        if (correct)
        {
            _currentProgress.Add(token.occurrenceId);
            _currentSlotIndex++;
            CompleteUnitIfReady(ChallengeSessionEvent.TraceAccepted);
        }
        else
        {
            RegisterError();
        }
    }

    public void SubmitPlacement(string slotId, string occurrenceId)
    {
        if (!CanSubmit() || (CurrentUnit.mode != ChallengeMode.WordPlacement
            && CurrentUnit.mode != ChallengeMode.SentenceRestoration
            && CurrentUnit.mode != ChallengeMode.ParagraphRestoration
            && CurrentUnit.mode != ChallengeMode.TimedMemory))
            return;
        if (CurrentUnit.mode == ChallengeMode.TimedMemory && IsMemoryRevealActive)
            return;
        if (CurrentUnit.slots == null || _currentSlotIndex >= CurrentUnit.slots.Length)
            return;

        ChallengeSlotDefinition slot = CurrentUnit.slots[_currentSlotIndex];
        bool correct = string.Equals(slot.slotId, slotId, StringComparison.Ordinal)
            && string.Equals(slot.expectedOccurrenceId, occurrenceId, StringComparison.Ordinal);
        RecordUnitEvidence(correct);
        if (correct)
        {
            _currentProgress.Add(occurrenceId);
            _currentSlotIndex++;
            CompleteUnitIfReady(ChallengeSessionEvent.PlacementAccepted);
        }
        else
        {
            RegisterError();
        }
    }

    public void SubmitRestoration(IReadOnlyList<string> occurrenceIds)
    {
        if (!CanSubmit() || (CurrentUnit.mode != ChallengeMode.SentenceRestoration && CurrentUnit.mode != ChallengeMode.ParagraphRestoration))
            return;
        if (CurrentUnit.slots == null)
            return;

        string[] expected = CurrentUnit.slots.Select(slot => slot.expectedOccurrenceId).ToArray();
        bool restorationCorrect = occurrenceIds != null && expected.SequenceEqual(occurrenceIds);
        RecordUnitEvidence(restorationCorrect);
        if (restorationCorrect)
        {
            foreach (string occurrenceId in expected)
                _currentProgress.Add(occurrenceId);
            _currentSlotIndex = expected.Length;
            CompleteUnitIfReady(ChallengeSessionEvent.RestorationAccepted);
        }
        else
        {
            RegisterError();
        }
    }

    public void RequestHint()
    {
        // SALIN-231: the guard now lives in CanRequestHint so the modal's pre-confirm
        // disclosure and this gate are one expression. Tier 5's emergency budget is per
        // level attempt and survives checkpoint resets; requests beyond it are rejected
        // outright, exactly as before.
        if (!CanRequestHint)
            return;
        _hintsUsed++;
        if (_policy != null && _policy.emergencyHintEnabled)
            _emergencyHintsUsed++;
        _hintOccurrenceId = CurrentUnit.mode == ChallengeMode.GuidedTracing
            ? CurrentUnit.tokens[_currentSlotIndex].occurrenceId
            : CurrentUnit.slots[_currentSlotIndex].expectedOccurrenceId;
        _cluePolicy = _cluePolicy == ChallengeCluePolicy.Full ? ChallengeCluePolicy.Reduced : ChallengeCluePolicy.Minimal;
        State = ChallengeSessionState.HintShown;
        NotifyChanged(ChallengeSessionEvent.HintShown);
        State = ChallengeSessionState.Active;
        NotifyChanged(ChallengeSessionEvent.HintApplied);
    }

    public void Tick(float deltaSeconds)
    {
        if (State != ChallengeSessionState.Active || CurrentUnit.timerSeconds <= 0f || deltaSeconds <= 0f)
            return;

        if (IsMemoryRevealActive)
        {
            _memoryRevealRemaining = Math.Max(0f, _memoryRevealRemaining - deltaSeconds);
            NotifyChanged(_memoryRevealRemaining <= 0f
                ? ChallengeSessionEvent.MemoryRecallStarted
                : ChallengeSessionEvent.MemoryRevealTicked);
            return;
        }

        _remainingTime = Math.Max(0f, _remainingTime - deltaSeconds);
        if (_remainingTime <= 0f)
        {
            State = ChallengeSessionState.TimedOut;
            NotifyChanged(ChallengeSessionEvent.TimedOut);
            ApplyPenalty();
        }
        else
        {
            NotifyChanged(ChallengeSessionEvent.TimerTicked);
        }
    }

    public void Pause()
    {
        if (State == ChallengeSessionState.Active)
        {
            State = ChallengeSessionState.Paused;
            NotifyChanged(ChallengeSessionEvent.Paused);
        }
    }

    public void Resume()
    {
        if (State == ChallengeSessionState.Paused)
        {
            State = ChallengeSessionState.Active;
            NotifyChanged(ChallengeSessionEvent.Resumed);
        }
    }

    public void Retry()
    {
        if (State == ChallengeSessionState.Active)
            ResetToCheckpoint();
    }

    public void ResetToCheckpoint()
    {
        if (State == ChallengeSessionState.Exited || State == ChallengeSessionState.Completed || State == ChallengeSessionState.Failed)
            return;
        State = ChallengeSessionState.CheckpointReset;
        _currentUnitIndex = _checkpointUnitIndex;
        _committedOccurrenceIds.Clear();
        _committedOccurrenceIds.AddRange(_checkpointCommitted);
        _currentProgress.Clear();
        _currentSlotIndex = _checkpointSlotIndex;
        _remainingTime = _checkpointTime;
        _memoryRevealRemaining = _checkpointMemoryReveal;
        _cluePolicy = ChallengeCluePolicy.Full;
        _hintOccurrenceId = null;
        _errors = 0;
        NotifyChanged(ChallengeSessionEvent.CheckpointReset);
        State = ChallengeSessionState.Active;
        NotifyChanged(ChallengeSessionEvent.CheckpointReopened);
    }

    public void Exit()
    {
        if (State == ChallengeSessionState.Completed || State == ChallengeSessionState.Failed || State == ChallengeSessionState.Exited)
            return;
        _currentProgress.Clear();
        _errors = 0;
        State = ChallengeSessionState.Exited;
        NotifyChanged(ChallengeSessionEvent.Exited);
    }

    /// <summary>
    /// A tier inside the authored preset range (<see cref="ChallengeTierPolicy.MinTier"/>-
    /// <see cref="ChallengeTierPolicy.MaxTier"/>) snaps every flag to its canonical preset,
    /// so authoring only has to set the tier. Any other tier value — including 0/unset on
    /// legacy serialized levels — keeps the raw serialized flags exactly as authored.
    /// </summary>
    private static ChallengeTierPolicy ResolveEffectivePolicy(ChallengeTierPolicy policy)
    {
        return policy != null && ChallengeTierPolicy.IsDefinedTier(policy.tier)
            ? ChallengeTierPolicy.ForTier(policy.tier)
            : policy;
    }

    // Bounds-safe: after the final unit completes, _currentUnitIndex points past
    // the array while the Completed notification is still being rendered.
    private ChallengeUnitDefinition CurrentUnit =>
        _sequence != null && _sequence.units != null
        && _currentUnitIndex >= 0 && _currentUnitIndex < _sequence.units.Length
            ? _sequence.units[_currentUnitIndex]
            : null;

    private bool CanSubmit()
    {
        return State == ChallengeSessionState.Active && CurrentUnit != null;
    }

    private void OpenCurrentUnit(bool saveCheckpoint = true)
    {
        _currentSlotIndex = 0;
        _currentProgress.Clear();
        _errors = 0;
        _cluePolicy = CurrentUnit.cluePolicy;
        _remainingTime = CurrentUnit.timerSeconds;
        _memoryRevealRemaining = CurrentUnit.mode == ChallengeMode.TimedMemory
            ? Math.Max(0f, CurrentUnit.memoryRevealSeconds)
            : 0f;
        _hintOccurrenceId = null;
        if (saveCheckpoint)
            SaveCheckpoint();
    }

    private void SaveCheckpoint()
    {
        _checkpointUnitIndex = _currentUnitIndex;
        _checkpointCommitted = new List<string>(_committedOccurrenceIds);
        _checkpointSlotIndex = 0;
        _checkpointTime = CurrentUnit.timerSeconds;
        _checkpointMemoryReveal = _memoryRevealRemaining;
    }

    private void CompleteUnitIfReady(ChallengeSessionEvent acceptedEvent)
    {
        int required = RequiredSlotCount;
        if (_currentSlotIndex < required)
        {
            NotifyChanged(acceptedEvent);
            return;
        }

        bool createCheckpointForNextUnit = CurrentUnit.checkpointOnSuccess;
        State = ChallengeSessionState.Success;
        foreach (string occurrenceId in _currentProgress)
        {
            if (!_committedOccurrenceIds.Contains(occurrenceId))
                _committedOccurrenceIds.Add(occurrenceId);
        }
        _currentProgress.Clear();
        NotifyChanged(ChallengeSessionEvent.UnitSucceeded);
        _currentUnitIndex++;
        if (_currentUnitIndex >= _sequence.units.Length)
        {
            State = ChallengeSessionState.Completed;
            NotifyChanged(ChallengeSessionEvent.Completed);
            return;
        }

        OpenCurrentUnit(createCheckpointForNextUnit);
        State = ChallengeSessionState.Active;
        NotifyChanged(ChallengeSessionEvent.UnitStarted);
    }

    private void RegisterError()
    {
        _errors++;
        // Tiers 1-2: every error is a supportive retry; hearts are never at stake.
        if (HeartPenaltiesEnabled && _errors >= ErrorThreshold)
        {
            State = ChallengeSessionState.Penalty;
            ApplyPenalty();
            return;
        }
        State = ChallengeSessionState.SupportiveRetry;
        NotifyChanged(ChallengeSessionEvent.SupportiveRetry);
        State = ChallengeSessionState.Active;
        NotifyChanged(ChallengeSessionEvent.RetryOpened);
    }

    private void ApplyPenalty()
    {
        if (HeartPenaltiesEnabled)
        {
            HeartPenalties += Math.Max(0, CurrentUnit.heartPenalty);
            HeartsRemaining = Math.Max(0, HeartsRemaining - Math.Max(0, CurrentUnit.heartPenalty));
            if (HeartsRemaining == 0)
            {
                State = ChallengeSessionState.Failed;
                NotifyChanged(ChallengeSessionEvent.Failed);
                return;
            }
            NotifyChanged(ChallengeSessionEvent.PenaltyApplied);
        }

        if (_policy == null || _policy.checkpointResetOnPenalty)
            ResetToCheckpoint();
        else
        {
            // Reopening in place still has to restock the unit's own budget: a timed
            // unit left on an expired clock re-times-out on every subsequent tick.
            _errors = 0;
            _remainingTime = CurrentUnit == null ? 0f : CurrentUnit.timerSeconds;
            State = ChallengeSessionState.Active;
            NotifyChanged(ChallengeSessionEvent.CheckpointReopened);
        }
    }

    private void RecordTokenEvidence(ChallengeTokenDefinition token, bool success)
    {
        if (_evidence == null || token == null || string.IsNullOrEmpty(token.evidenceContentId))
            return;

        _evidence.RecordAttempt(
            token.evidenceContentId,
            LearningContentKind.Symbol,
            MasteryDimension.Form,
            success,
            answerWasVisible: _hintOccurrenceId != null);
    }

    private void RecordUnitEvidence(bool success)
    {
        if (_evidence == null || CurrentUnit == null || string.IsNullOrEmpty(CurrentUnit.evidenceContentId))
            return;

        // Word placement evidences Assembly; sentence, paragraph, and timed-memory
        // units evidence Meaning. A hinted or revealed answer is immediate
        // retrieval, not recall.
        MasteryDimension dimension = CurrentUnit.mode == ChallengeMode.WordPlacement
            ? MasteryDimension.Assembly
            : MasteryDimension.Meaning;
        _evidence.RecordAttempt(
            CurrentUnit.evidenceContentId,
            LearningContentKind.Word,
            dimension,
            success,
            answerWasVisible: _hintOccurrenceId != null || IsMemoryRevealActive);
    }

    private void NotifyChanged(ChallengeSessionEvent sessionEvent)
    {
        LastEvent = sessionEvent;
        Changed?.Invoke(this);
    }
}
