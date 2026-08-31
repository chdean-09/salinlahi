using UnityEngine;

public class TracingDojoController : MonoBehaviour
{
    [SerializeField] private GhostStrokeRenderer _ghost;
    [SerializeField] private FeedbackToast _toast;
    [SerializeField] private CharacterDropdown _dropdown;
    [SerializeField] private CharacterRegistrySO _registry;

    private BaybayinCharacterSO _selected;
    private bool _previousLoggingEnabled;
    private LearningEvidenceRecorder _recorder;

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += OnResolved;
        _previousLoggingEnabled = RecognitionLogger.LoggingEnabled;
        RecognitionLogger.LoggingEnabled = false;
        if (GameManager.Instance != null) GameManager.Instance.EnterPractice();

        // instructedContentIds stays empty: the dojo teaches nothing new, so all of its evidence
        // is delayed retrieval, which is what lets dojo work advance a symbol toward Recalled.
        _recorder = new LearningEvidenceRecorder(
            ProgressManager.Instance != null ? ProgressManager.Instance.GetSelectedLevelId() : null,
            LearningSessionKind.FreePractice);
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= OnResolved;
        RecognitionLogger.LoggingEnabled = _previousLoggingEnabled;
        if (GameManager.Instance != null) GameManager.Instance.ExitPractice();

        if (_recorder != null && ProgressManager.Instance != null)
        {
            CampaignOutcomeCommitResult result =
                ProgressManager.Instance.CommitPracticeSession(_recorder.Build());
            if (!result.IsAccepted)
                DebugLogger.LogWarning(
                    $"TracingDojo: practice evidence pending retry ({result.ReasonCode}).");
        }

        _recorder = null;
    }

    public void SelectCharacter(BaybayinCharacterSO character)
    {
        _selected = character;
        _ghost.Render(character);
        _dropdown.SetCurrentCharacter(character);
        _dropdown.Close();
    }

    private void OnResolved(RecognitionResult result, bool passedThreshold, float threshold)
    {
        CampaignConfigSO campaign = SaveManager.Instance != null
            ? SaveManager.Instance.Campaign
            : null;
        string selectedStableId = _selected != null ? _selected.stableId : null;
        string recognizedStableId = campaign != null
            ? TracingDojoEvidence.ResolveStableId(campaign, result.characterID)
            : null;

        // Match on stableId once a campaign exists; until SALIN-172 authors one, keep the exact
        // legacy comparison so the dojo does not regress in Legacy mode.
        bool matchesSelected;
        if (_selected == null)
            matchesSelected = true;
        else if (campaign != null && ContentIdentity.IsCanonical(selectedStableId))
            matchesSelected = string.Equals(
                selectedStableId, recognizedStableId, System.StringComparison.Ordinal);
        else
            matchesSelected = result.characterID == _selected.characterID;

        bool pass = passedThreshold && matchesSelected;

        _toast.Show(result.characterID, pass);

        if (_recorder != null && ContentIdentity.IsCanonical(selectedStableId))
        {
            LearningEvidenceEntry entry = TracingDojoEvidence.Resolve(
                selectedStableId,
                matchesSelected ? selectedStableId : recognizedStableId,
                passedThreshold);
            _recorder.RecordAttempt(
                entry.contentId,
                entry.contentKind,
                entry.dimension,
                entry.successCount > 0,
                answerWasVisible: false);
        }

        if (pass && _selected != null && _selected.pronunciationClip != null)
        {
            AudioManager.Instance?.PlayPronunciation(_selected.pronunciationClip);
        }
    }
}
