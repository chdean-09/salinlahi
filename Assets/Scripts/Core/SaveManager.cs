using UnityEngine;

public enum SaveManagerMode
{
    Uninitialized,
    Legacy,
    RevisedReady,
    RevisedBlocked,
}

public sealed class SaveManager : Singleton<SaveManager>
{
    [SerializeField] private CampaignConfigSO _campaign;

    public SaveManagerMode Mode { get; private set; } = SaveManagerMode.Uninitialized;
    public CampaignProgressRepository Repository { get; private set; }
    public CampaignSaveNotice PendingNotice { get; private set; } = new CampaignSaveNotice();
    public CampaignSaveInitializationResult InitializationResult { get; private set; }
    public CampaignOutcomeCoordinator OutcomeCoordinator { get; private set; }
    public CampaignOutcomeCommitResult LastOutcomeResult { get; private set; }
    public CampaignConfigSO Campaign => _campaign;

    public void Initialize()
    {
        if (_campaign == null)
        {
            Mode = SaveManagerMode.Legacy;
            Repository = null;
            OutcomeCoordinator = null;
            LastOutcomeResult = null;
            PendingNotice = new CampaignSaveNotice();
            return;
        }

        Initialize(new CampaignSaveService(
            new CampaignSaveFileStorage(),
            new PlayerPrefsLegacyProgressSource()));
    }

    public void Initialize(CampaignSaveService service)
    {
        if (_campaign == null)
        {
            Mode = SaveManagerMode.Legacy;
            Repository = null;
            OutcomeCoordinator = null;
            LastOutcomeResult = null;
            PendingNotice = new CampaignSaveNotice();
            return;
        }

        InitializationResult = service.Initialize(_campaign);
        if (InitializationResult.Document == null)
        {
            Mode = SaveManagerMode.RevisedBlocked;
            Repository = null;
            OutcomeCoordinator = null;
            PendingNotice = new CampaignSaveNotice(
                CampaignSaveNoticeKind.Blocking,
                InitializationResult.ReasonCode ?? InitializationResult.FailureCode.ToString());
            return;
        }

        OutcomeCoordinator = new CampaignOutcomeCoordinator(
            service,
            new CampaignOutcomeJournal(service.Storage, _campaign, service.Metadata),
            _campaign,
            service.Metadata);
        LastOutcomeResult = OutcomeCoordinator.ReplayPendingOnStartup();
        if (LastOutcomeResult.Status == CampaignOutcomeCommitStatus.Blocked)
        {
            Mode = SaveManagerMode.RevisedBlocked;
            Repository = null;
            PendingNotice = new CampaignSaveNotice(
                CampaignSaveNoticeKind.Blocking,
                LastOutcomeResult.ReasonCode ?? LastOutcomeResult.FailureCode.ToString());
            return;
        }

        Mode = SaveManagerMode.RevisedReady;
        Repository = new CampaignProgressRepository(service, _campaign);
        PendingNotice = Repository.GetPendingNotice();
        if (LastOutcomeResult.Status == CampaignOutcomeCommitStatus.PendingRetry)
            PendingNotice = new CampaignSaveNotice(
                CampaignSaveNoticeKind.Recovery, "outcome-replay-pending");
    }

    public void SetCampaignForTests(CampaignConfigSO campaign)
    {
        _campaign = campaign;
    }

    public void SetServiceForTests(CampaignSaveService service)
    {
        Initialize(service);
    }

    public void RetryInitialization()
    {
        if (_campaign == null || InitializationResult == null)
            return;
        Initialize();
    }

    public void RefreshPendingNotice()
    {
        if (Repository != null)
            PendingNotice = Repository.GetPendingNotice();
    }

    public CampaignOutcomeCommitResult RetryPendingOutcome()
    {
        if (OutcomeCoordinator == null)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "outcome-coordinator-missing");
        LastOutcomeResult = OutcomeCoordinator.RetryPending();
        return LastOutcomeResult;
    }

    public CampaignOutcomeCommitResult ResetJourneyAtomically()
    {
        if (OutcomeCoordinator == null)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "outcome-coordinator-missing");
        LastOutcomeResult = OutcomeCoordinator.TryResetJourney();
        RefreshPendingNotice();
        return LastOutcomeResult;
    }
}
