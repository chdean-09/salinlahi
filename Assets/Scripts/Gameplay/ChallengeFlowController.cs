using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChallengePlayResult
{
    NotStarted,
    MissingSequence,
    InvalidSequence,
    Completed,
    Exited,
    Failed
}

public class ChallengeFlowController : MonoBehaviour
{
    [SerializeField] private ChallengeModeUI _ui;
    [SerializeField] private ChallengeInputRouter _inputRouter;
    [SerializeField] private HeartSystem _heartSystem;
    [SerializeField] private Level1TutorialGuideUI _guideUI;

    public ChallengeSession Session { get; private set; }
    public ChallengePlayResult LastPlayResult { get; private set; } = ChallengePlayResult.NotStarted;
    public bool IsFinished => Session != null && (Session.State == ChallengeSessionState.Completed || Session.State == ChallengeSessionState.Exited || Session.State == ChallengeSessionState.Failed);

    private int _appliedHeartPenalties;
    private Level1TutorialStepSO _renderedGuideStep;
    private bool _guideVisible;
    private int _levelHintsUsed;
    private float _levelEmergencyHintScorePenalty;

    /// <summary>
    /// SALIN-226. Hints used across EVERY challenge session this level ran, not just the
    /// last one. A segmented level plays one session per segment, so reading
    /// <see cref="Session"/> alone would report only the final segment's hints into the
    /// star/score calculation — wrong stars, with nothing failing. On an unsegmented level
    /// this is exactly the single session's value.
    /// </summary>
    public int LevelHintsUsed => _levelHintsUsed;

    /// <summary>
    /// SALIN-226. Emergency-hint score penalty accumulated across every challenge session
    /// this level ran. See <see cref="LevelHintsUsed"/>.
    /// </summary>
    public float LevelEmergencyHintScorePenalty => _levelEmergencyHintScorePenalty;

    /// <summary>
    /// SALIN-226. Zeroes the level-wide challenge metrics. Called once per level, as
    /// segment 0's Defense leg opens, so the accumulators cover exactly one playthrough.
    /// </summary>
    public void BeginLevelChallengeMetrics()
    {
        _levelHintsUsed = 0;
        _levelEmergencyHintScorePenalty = 0f;
    }

    public IEnumerator Play(
        ChallengeSequenceSO sequence,
        int levelNumber,
        ChallengeTierPolicy policy,
        IChallengeEvidenceSink evidence)
    {
        return PlayCore(sequence, levelNumber, policy, evidence);
    }

    public IEnumerator Play(ChallengeSequenceSO sequence, int levelNumber)
    {
        return PlayCore(sequence, levelNumber, null, null);
    }

    /// <summary>
    /// SALIN-226. Plays only the named units of <paramref name="sequence"/>, in the given
    /// order — one alternating segment's restoration leg.
    /// </summary>
    /// <remarks>
    /// Builds a runtime-only subset ScriptableObject and plays that, which leaves
    /// ChallengeSession completely untouched: it still hard-starts at unit 0 of whatever
    /// sequence it is handed. A strict subset of a valid sequence stays valid, because the
    /// validator's cross-references (slots, candidates, occurrences) are all per-unit and
    /// its only sequence-global sets are uniqueness sets, which a subset can only shrink.
    ///
    /// <paramref name="policy"/> is passed straight through to the session exactly as the
    /// whole-sequence path does. Nothing here reads a raw serialized policy field; those are
    /// stale by design and ChallengeSession.ResolveEffectivePolicy is the only correct
    /// reader (SALIN-222).
    /// </remarks>
    public IEnumerator Play(
        ChallengeSequenceSO sequence,
        int levelNumber,
        ChallengeTierPolicy policy,
        IChallengeEvidenceSink evidence,
        IReadOnlyList<string> unitIds)
    {
        return PlaySubsetCore(sequence, levelNumber, policy, evidence, unitIds);
    }

    private IEnumerator PlaySubsetCore(
        ChallengeSequenceSO sequence,
        int levelNumber,
        ChallengeTierPolicy policy,
        IChallengeEvidenceSink evidence,
        IReadOnlyList<string> unitIds)
    {
        if (sequence == null || unitIds == null || unitIds.Count == 0)
        {
            yield return PlayCore(sequence, levelNumber, policy, evidence);
            yield break;
        }

        ChallengeSequenceSO subset = BuildUnitSubset(sequence, unitIds);
        if (subset == null)
        {
            DebugLogger.LogError(
                "ChallengeFlowController: segment names challenge units that the sequence "
                + "does not contain; refusing to play a partial subset.");
            LastPlayResult = ChallengePlayResult.InvalidSequence;
            yield break;
        }

        try
        {
            yield return PlayCore(subset, levelNumber, policy, evidence);
        }
        finally
        {
            Destroy(subset);
        }
    }

    /// <summary>
    /// SALIN-226. A runtime-only sequence carrying just <paramref name="unitIds"/>, in the
    /// order named. Null when any id is absent from the source — a partial subset would play
    /// less restoration than the level authored, silently.
    /// </summary>
    public static ChallengeSequenceSO BuildUnitSubset(
        ChallengeSequenceSO sequence, IReadOnlyList<string> unitIds)
    {
        if (sequence == null || unitIds == null || unitIds.Count == 0)
            return null;

        var units = new List<ChallengeUnitDefinition>(unitIds.Count);
        foreach (string unitId in unitIds)
        {
            ChallengeUnitDefinition match = null;
            foreach (ChallengeUnitDefinition unit in sequence.units
                ?? System.Array.Empty<ChallengeUnitDefinition>())
            {
                if (unit != null && string.Equals(unit.unitId, unitId, System.StringComparison.Ordinal))
                {
                    match = unit;
                    break;
                }
            }

            if (match == null)
                return null;

            units.Add(match);
        }

        ChallengeSequenceSO subset = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        subset.sequenceId = sequence.sequenceId;
        subset.displayName = sequence.displayName;
        subset.units = units.ToArray();
        return subset;
    }

    private IEnumerator PlayCore(
        ChallengeSequenceSO sequence,
        int levelNumber,
        ChallengeTierPolicy policy,
        IChallengeEvidenceSink evidence)
    {
        if (Session != null && !IsFinished)
        {
            DebugLogger.LogWarning("ChallengeFlowController: Play ignored while another challenge session is active.");
            yield break;
        }
        LastPlayResult = ChallengePlayResult.NotStarted;
        _appliedHeartPenalties = 0;
        _renderedGuideStep = null;
        _guideVisible = false;
        Session = null;
        if (sequence == null)
        {
            LastPlayResult = ChallengePlayResult.MissingSequence;
            yield break;
        }
        ChallengeValidationResult validation = ChallengeSequenceValidator.Validate(sequence);
        if (!validation.IsValid)
        {
            foreach (string error in validation.Errors)
                DebugLogger.LogError($"ChallengeFlowController: {error}");
            LastPlayResult = ChallengePlayResult.InvalidSequence;
            yield break;
        }
        EnsureRuntimeReferences();
        ChallengeRuntimeState.Begin(levelNumber);
        Session = new ChallengeSession(
            sequence,
            _heartSystem == null ? 3 : _heartSystem.GetCurrentHearts(),
            policy,
            evidence);
        Session.Changed += HandleSessionChanged;
        _inputRouter.Bind(this);
        _ui.Bind(this);
        _ui.gameObject.SetActive(true);
        Session.Enter();

        while (!IsFinished)
            yield return null;

        LastPlayResult = Session.State == ChallengeSessionState.Completed
            ? ChallengePlayResult.Completed
            : Session.State == ChallengeSessionState.Exited
                ? ChallengePlayResult.Exited
                : ChallengePlayResult.Failed;

        // SALIN-226. Fold this session's metrics into the level totals before the next
        // segment replaces Session. A run that bailed above (missing or invalid sequence)
        // never reaches here and contributes nothing, exactly as the old single-session
        // read contributed nothing when Session was null.
        _levelHintsUsed += Session.HintsUsed;
        _levelEmergencyHintScorePenalty += Session.EmergencyHintScorePenalty;

        CleanupRuntime();
    }

    public void Update()
    {
        if (Session == null || !ChallengeRuntimeState.IsActive)
            return;
        int penaltiesBefore = Session.HeartPenalties;
        Session.Tick(Time.unscaledDeltaTime);
        ApplyPendingHeartPenalties(penaltiesBefore);
    }

    public void SubmitTrace(string characterId)
    {
        Session?.SubmitTrace(characterId);
        ApplyPendingHeartPenalties();
    }

    public void SubmitPlacement(string occurrenceId)
    {
        if (Session == null
            || Session.CurrentUnitDefinition == null
            || Session.CurrentUnitDefinition.slots == null
            || Session.CurrentSlotIndex >= Session.CurrentUnitDefinition.slots.Length)
            return;
        string slotId = Session.CurrentUnitDefinition.slots[Session.CurrentSlotIndex].slotId;
        Session.SubmitPlacement(slotId, occurrenceId);
        ApplyPendingHeartPenalties();
    }

    public void SubmitRestoration(string[] occurrenceIds)
    {
        Session?.SubmitRestoration(occurrenceIds);
        ApplyPendingHeartPenalties();
    }

    public void RequestHint() => Session?.RequestHint();
    public void Retry() => Session?.Retry();
    public void Exit() => Session?.Exit();

    // -------------------------------------------------------------------------
    // SALIN-231. Focus-word handoff, so the hint modal can explain the word.
    //
    // The ONE hint type the authored data can actually serve is the text meaning:
    // FocusWordDefinition.meaning is authored on every level (Level 5 -> IBA
    // "different", MANA "inheritance"). The other three types in the ticket title
    // are DATA-BLOCKED and are deliberately not built — targetCharacter is
    // {fileID: 0} on all 45 tokens across all 10 authored challenge sequences, so
    // "replayed audio" and "first symbol" have no BaybayinCharacterSO to read, and
    // media.contextImage is null on every focus word, so "image meaning" has no image.
    //
    // The meaning lives on LevelConfigSO, which this controller never sees: Play
    // receives (sequence, levelNumber, policy, evidence). Rather than widen three
    // Play overloads into six-parameter signatures, LevelFlowController hands the
    // list over immediately before each Play call. Both of its call sites do so, so
    // no path plays a sequence against another level's words.
    // -------------------------------------------------------------------------

    private IReadOnlyList<FocusWordDefinition> _focusWords;

    /// <summary>Supplies the level's focus words for hint resolution. Call before Play.</summary>
    public void SetLevelFocusWords(IReadOnlyList<FocusWordDefinition> focusWords)
    {
        _focusWords = focusWords;
    }

    /// <summary>
    /// The focus word a unit evidences, joined on
    /// ChallengeUnitDefinition.evidenceContentId -> LevelConfigSO.focusWords[*].stableId
    /// (e.g. "level.ugat.05.focus.01"). Null when the unit evidences nothing, when no
    /// words were supplied, or when the id does not join — all of which leave the modal
    /// with nothing to sell, so it disables confirm rather than charging for nothing.
    /// </summary>
    public FocusWordDefinition ResolveFocusWord(ChallengeUnitDefinition unit)
    {
        if (unit == null || string.IsNullOrEmpty(unit.evidenceContentId) || _focusWords == null)
            return null;

        foreach (FocusWordDefinition focus in _focusWords)
        {
            if (focus != null
                && string.Equals(focus.stableId, unit.evidenceContentId, System.StringComparison.Ordinal))
            {
                return focus;
            }
        }
        return null;
    }

    private void EnsureRuntimeReferences()
    {
        _heartSystem ??= FindFirstObjectByType<HeartSystem>();
        _guideUI ??= FindFirstObjectByType<Level1TutorialGuideUI>(FindObjectsInactive.Include);
        _guideUI ??= Level1TutorialGuideUI.CreateRuntime();
        _guideUI.PrepareForChallenge();
        if (_ui == null)
        {
            GameObject uiObject = new GameObject("[Runtime] ChallengeModeUI", typeof(RectTransform));
            uiObject.transform.SetParent(transform, false);
            _ui = uiObject.AddComponent<ChallengeModeUI>();
        }
        if (_inputRouter == null)
            _inputRouter = gameObject.GetComponent<ChallengeInputRouter>() ?? gameObject.AddComponent<ChallengeInputRouter>();
    }

    private void HandleSessionChanged(ChallengeSession session)
    {
        if (_ui != null)
            _ui.Render(session);
        ChallengeUnitDefinition unit = session.CurrentUnitDefinition;
        bool guided = unit != null && unit.mode == ChallengeMode.GuidedTracing;
        ChallengeRuntimeState.SetDrawingInputLocked(!guided || session.State != ChallengeSessionState.Active);
        if (_guideUI != null)
        {
            if (guided && unit.guidedStep != null && session.State == ChallengeSessionState.Active)
            {
                if (!_guideVisible
                    || _renderedGuideStep != unit.guidedStep
                    || ShouldReplayGuide(session.LastEvent))
                {
                    _guideUI.ShowPrompt(unit.guidedStep, false);
                    _guideUI.AnimateGuidePath();
                }
                _guideVisible = true;
                _renderedGuideStep = unit.guidedStep;
            }
            else if (!guided || unit == null || unit.guidedStep == null)
            {
                if (_guideVisible)
                {
                    _guideUI.Hide();
                    _guideVisible = false;
                    _renderedGuideStep = null;
                }
            }
        }
    }

    private static bool ShouldReplayGuide(ChallengeSessionEvent sessionEvent)
    {
        return sessionEvent == ChallengeSessionEvent.Entered
            || sessionEvent == ChallengeSessionEvent.UnitStarted
            || sessionEvent == ChallengeSessionEvent.RetryOpened
            || sessionEvent == ChallengeSessionEvent.CheckpointReopened
            || sessionEvent == ChallengeSessionEvent.HintApplied;
    }

    private void OnEnable()
    {
        EventBus.OnGamePaused += HandleGamePaused;
        EventBus.OnGameResumed += HandleGameResumed;
    }

    private void OnDisable()
    {
        EventBus.OnGamePaused -= HandleGamePaused;
        EventBus.OnGameResumed -= HandleGameResumed;
        AbortRuntime();
    }

    private void ApplyPendingHeartPenalties(int penaltiesBefore = -1)
    {
        if (Session == null)
            return;

        int pendingPenalties = Session.HeartPenalties - _appliedHeartPenalties;
        if (penaltiesBefore >= 0)
            pendingPenalties = Session.HeartPenalties - penaltiesBefore;
        if (pendingPenalties <= 0)
            return;

        _appliedHeartPenalties = Session.HeartPenalties;
        if (_heartSystem != null)
            _heartSystem.LoseHeart(pendingPenalties);

        // Sandbox mode intentionally bypasses HeartSystem.LoseHeart. The pure session still
        // owns challenge hearts, so route a terminal session failure explicitly when the
        // HeartSystem did not reach zero and therefore did not raise GameOver itself.
        if (Session.State == ChallengeSessionState.Failed
            && (_heartSystem == null || _heartSystem.GetCurrentHearts() > 0))
        {
            EventBus.RaiseGameOver();
        }
    }

    private void CleanupRuntime()
    {
        if (Session != null)
            Session.Changed -= HandleSessionChanged;
        if (_inputRouter != null)
            _inputRouter.Unbind(this);
        ChallengeRuntimeState.Clear();
        if (_guideUI != null)
            _guideUI.Hide();
        _guideVisible = false;
        _renderedGuideStep = null;
        if (_ui != null)
            _ui.gameObject.SetActive(false);
    }

    private void AbortRuntime()
    {
        if (Session != null
            && Session.State != ChallengeSessionState.Completed
            && Session.State != ChallengeSessionState.Exited
            && Session.State != ChallengeSessionState.Failed)
        {
            Session.Exit();
        }
        CleanupRuntime();
    }

    private void HandleGamePaused()
    {
        if (Session != null && Session.State == ChallengeSessionState.Active)
            Session.Pause();
    }

    private void HandleGameResumed()
    {
        if (Session != null && Session.State == ChallengeSessionState.Paused)
            Session.Resume();
    }
}
