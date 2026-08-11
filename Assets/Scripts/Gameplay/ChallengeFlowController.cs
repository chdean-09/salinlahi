using System.Collections;
using UnityEngine;

public class ChallengeFlowController : MonoBehaviour
{
    [SerializeField] private ChallengeModeUI _ui;
    [SerializeField] private ChallengeInputRouter _inputRouter;
    [SerializeField] private HeartSystem _heartSystem;
    [SerializeField] private Level1TutorialGuideUI _guideUI;

    public ChallengeSession Session { get; private set; }
    public bool IsFinished => Session != null && (Session.State == ChallengeSessionState.Completed || Session.State == ChallengeSessionState.Exited || Session.State == ChallengeSessionState.Failed);

    public IEnumerator Play(ChallengeSequenceSO sequence, int levelNumber)
    {
        Session = null;
        if (sequence == null)
            yield break;
        ChallengeValidationResult validation = ChallengeSequenceValidator.Validate(sequence);
        if (!validation.IsValid)
        {
            foreach (string error in validation.Errors)
                DebugLogger.LogError($"ChallengeFlowController: {error}");
            yield break;
        }
        EnsureRuntimeReferences();
        ChallengeRuntimeState.Begin(levelNumber);
        Session = new ChallengeSession(sequence, _heartSystem == null ? 3 : _heartSystem.GetCurrentHearts());
        Session.Changed += HandleSessionChanged;
        _inputRouter.Bind(this);
        _ui.Bind(this);
        Session.Enter();

        while (!IsFinished)
            yield return null;

        Session.Changed -= HandleSessionChanged;
        ChallengeRuntimeState.Clear();
        if (_ui != null)
            _ui.gameObject.SetActive(false);
    }

    public void Update()
    {
        if (Session == null || !ChallengeRuntimeState.IsActive)
            return;
        int penaltiesBefore = Session.HeartPenalties;
        Session.Tick(Time.unscaledDeltaTime);
        int newPenalties = Session.HeartPenalties - penaltiesBefore;
        if (newPenalties > 0 && _heartSystem != null)
            _heartSystem.LoseHeart(newPenalties);
    }

    public void SubmitTrace(string characterId) => Session?.SubmitTrace(characterId);
    public void SubmitPlacement(string occurrenceId)
    {
        if (Session == null || Session.CurrentUnitDefinition == null || Session.CurrentSlotIndex >= Session.CurrentUnitDefinition.slots.Length)
            return;
        string slotId = Session.CurrentUnitDefinition.slots[Session.CurrentSlotIndex].slotId;
        Session.SubmitPlacement(slotId, occurrenceId);
    }
    public void SubmitRestoration(string[] occurrenceIds) => Session?.SubmitRestoration(occurrenceIds);
    public void RequestHint() => Session?.RequestHint();
    public void Retry() => Session?.Retry();
    public void Exit() => Session?.Exit();

    private void EnsureRuntimeReferences()
    {
        _heartSystem ??= FindFirstObjectByType<HeartSystem>();
        _guideUI ??= FindFirstObjectByType<Level1TutorialGuideUI>(FindObjectsInactive.Include);
        _guideUI ??= Level1TutorialGuideUI.CreateRuntime();
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
        ChallengeRuntimeState.SetDrawingInputLocked(!guided || session.State == ChallengeSessionState.Paused);
        if (_guideUI != null)
        {
            if (guided && unit.guidedStep != null && session.State == ChallengeSessionState.Active)
            {
                _guideUI.ShowPrompt(unit.guidedStep, false);
                _guideUI.AnimateGuidePath();
            }
            else if (!guided)
            {
                _guideUI.Hide();
            }
        }
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
        if (ChallengeRuntimeState.IsActive)
            ChallengeRuntimeState.Clear();
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
