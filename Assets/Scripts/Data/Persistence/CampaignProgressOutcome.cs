using System;
using System.Collections.Generic;

/// <summary>
/// One recorded level metric. A plain record rather than a dictionary entry because persistence
/// runs through JsonUtility, which does not serialize Dictionary at all.
/// </summary>
[Serializable]
public sealed class LevelMetricRecord
{
    public string metricId;
    public float value;

    public LevelMetricRecord() { }

    public LevelMetricRecord(string metricId, float value)
    {
        this.metricId = metricId;
        this.value = value;
    }
}

[Serializable]
public sealed class CampaignProgressOutcome
{
    // v3 (SALIN-140) adds `metrics`. Metrics were computed by LevelResultsCalculator, shown on the
    // Results screen and then discarded, so a completed level's score was unrecoverable the moment
    // that screen closed -- and SALIN-160/161/190 all read progress that was never written.
    public const int CurrentOutcomeSchemaVersion = 3;
    public const int MinimumOutcomeSchemaVersion = 1;
    public int outcomeSchemaVersion = CurrentOutcomeSchemaVersion;
    public LearningSessionKind sessionKind = LearningSessionKind.LevelAttempt;
    public LearningEvidenceBatch evidence = new LearningEvidenceBatch();
    public string outcomeId;
    public string journeyGenerationId;
    public string campaignId;
    public int contentSchemaVersion;
    public string levelId;
    public int stars;

    /// <summary>
    /// Metrics for this attempt, sorted by <see cref="LevelMetricRecord.metricId"/>. Order is part of
    /// the contract: the journal's integrity hash is computed over the serialized document, so an
    /// unstable order would produce a different checksum for identical data.
    /// </summary>
    public List<LevelMetricRecord> metrics = new List<LevelMetricRecord>();
    public List<string> unlockedSymbolIds = new List<string>();
    public List<string> unlockedMemoryIds = new List<string>();
    public List<string> claimedRewardIds = new List<string>();
    public string completedAtUtc;
}

[Serializable]
public sealed class CampaignOutcomeJournalDocument
{
    public const int CurrentJournalSchemaVersion = 1;
    public string fileFormat = "salinlahi-campaign-outcome-journal";
    public int journalSchemaVersion = CurrentJournalSchemaVersion;
    public CampaignProgressOutcome outcome = new CampaignProgressOutcome();
    public string integritySha256;
}

public enum CampaignOutcomeCommitStatus
{
    Committed,
    AlreadyCommitted,
    PendingRetry,
    Rejected,
    Blocked,
}

public sealed class CampaignOutcomeCommitResult
{
    public CampaignOutcomeCommitStatus Status { get; private set; }
    public CampaignProgressOutcome Outcome { get; private set; }
    public CampaignSaveFailureCode FailureCode { get; private set; }
    public string ReasonCode { get; private set; }
    public bool IsAccepted => Status == CampaignOutcomeCommitStatus.Committed ||
        Status == CampaignOutcomeCommitStatus.AlreadyCommitted;

    public static CampaignOutcomeCommitResult Committed(CampaignProgressOutcome outcome) =>
        Create(CampaignOutcomeCommitStatus.Committed, outcome,
            CampaignSaveFailureCode.None, "committed");

    public static CampaignOutcomeCommitResult AlreadyCommitted(CampaignProgressOutcome outcome) =>
        Create(CampaignOutcomeCommitStatus.AlreadyCommitted, outcome,
            CampaignSaveFailureCode.None, "already-committed");

    public static CampaignOutcomeCommitResult PendingRetry(
        CampaignProgressOutcome outcome, CampaignSaveFailureCode code, string reason) =>
        Create(CampaignOutcomeCommitStatus.PendingRetry, outcome, code, reason);

    public static CampaignOutcomeCommitResult Rejected(
        CampaignProgressOutcome outcome, CampaignSaveFailureCode code, string reason) =>
        Create(CampaignOutcomeCommitStatus.Rejected, outcome, code, reason);

    public static CampaignOutcomeCommitResult Blocked(
        CampaignProgressOutcome outcome, CampaignSaveFailureCode code, string reason) =>
        Create(CampaignOutcomeCommitStatus.Blocked, outcome, code, reason);

    private static CampaignOutcomeCommitResult Create(
        CampaignOutcomeCommitStatus status,
        CampaignProgressOutcome outcome,
        CampaignSaveFailureCode code,
        string reason) => new CampaignOutcomeCommitResult
        {
            Status = status,
            Outcome = outcome,
            FailureCode = code,
            ReasonCode = reason,
        };
}

public sealed class CampaignOutcomeJournalWriteResult
{
    public bool Success { get; private set; }
    public CampaignProgressOutcome Outcome { get; private set; }
    public CampaignSaveFailureCode FailureCode { get; private set; }
    public string ReasonCode { get; private set; }

    public static CampaignOutcomeJournalWriteResult Published(
        CampaignProgressOutcome outcome) => new CampaignOutcomeJournalWriteResult
        {
            Success = true,
            Outcome = outcome,
            FailureCode = CampaignSaveFailureCode.None,
            ReasonCode = "journal-published",
        };

    public static CampaignOutcomeJournalWriteResult Failed(
        CampaignProgressOutcome outcome,
        CampaignSaveFailureCode code,
        string reason) => new CampaignOutcomeJournalWriteResult
        {
            Success = false,
            Outcome = outcome,
            FailureCode = code,
            ReasonCode = reason,
        };
}

public sealed class CampaignOutcomeJournalParseResult
{
    public bool Success { get; private set; }
    public CampaignOutcomeJournalDocument Document { get; private set; }
    public CampaignSaveFailureCode FailureCode { get; private set; }
    public string ErrorMessage { get; private set; }

    public static CampaignOutcomeJournalParseResult Succeeded(CampaignOutcomeJournalDocument document) =>
        new CampaignOutcomeJournalParseResult
        {
            Success = true,
            Document = document,
            FailureCode = CampaignSaveFailureCode.None,
        };

    public static CampaignOutcomeJournalParseResult Failed(
        CampaignSaveFailureCode code, string message = null) =>
        new CampaignOutcomeJournalParseResult
        {
            Success = false,
            FailureCode = code,
            ErrorMessage = message,
        };
}
