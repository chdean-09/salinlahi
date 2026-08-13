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

    public ChallengeSession(ChallengeSequenceSO sequence, int startingHearts = 3)
    {
        _sequence = sequence;
        HeartsRemaining = startingHearts;
        State = ChallengeSessionState.Idle;
    }

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
        if (token.targetCharacter != null && string.Equals(token.targetCharacter.characterID, characterId, StringComparison.OrdinalIgnoreCase))
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
        if (string.Equals(slot.slotId, slotId, StringComparison.Ordinal) && string.Equals(slot.expectedOccurrenceId, occurrenceId, StringComparison.Ordinal))
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
        if (occurrenceIds != null && expected.SequenceEqual(occurrenceIds))
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
        if (!CanSubmit() || !CurrentUnit.allowHint)
            return;
        if (CurrentUnit.mode == ChallengeMode.GuidedTracing
            && (CurrentUnit.tokens == null || _currentSlotIndex >= CurrentUnit.tokens.Length))
            return;
        if (CurrentUnit.mode != ChallengeMode.GuidedTracing
            && (CurrentUnit.slots == null || _currentSlotIndex >= CurrentUnit.slots.Length))
            return;
        _hintsUsed++;
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

    private ChallengeUnitDefinition CurrentUnit => _sequence.units[_currentUnitIndex];

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
        if (_errors >= Math.Max(1, CurrentUnit.maxErrors))
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
        HeartPenalties += Math.Max(0, CurrentUnit.heartPenalty);
        HeartsRemaining = Math.Max(0, HeartsRemaining - Math.Max(0, CurrentUnit.heartPenalty));
        if (HeartsRemaining == 0)
        {
            State = ChallengeSessionState.Failed;
            NotifyChanged(ChallengeSessionEvent.Failed);
            return;
        }
        NotifyChanged(ChallengeSessionEvent.PenaltyApplied);
        ResetToCheckpoint();
    }

    private void NotifyChanged(ChallengeSessionEvent sessionEvent)
    {
        LastEvent = sessionEvent;
        Changed?.Invoke(this);
    }
}
