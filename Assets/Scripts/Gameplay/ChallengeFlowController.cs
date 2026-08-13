using System.Collections;
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

    public IEnumerator Play(ChallengeSequenceSO sequence, int levelNumber)
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
        Session = new ChallengeSession(sequence, _heartSystem == null ? 3 : _heartSystem.GetCurrentHearts());
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
