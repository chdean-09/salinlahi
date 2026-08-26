using System;
using System.Collections.Generic;

[Serializable]
public sealed class CampaignSaveDocument
{
    public const int CurrentSaveSchemaVersion = 3;

    public string fileFormat = "salinlahi-campaign-save";
    public string campaignId;
    public int contentSchemaVersion;
    public int saveSchemaVersion = CurrentSaveSchemaVersion;
    public string transactionId;
    public long revision;
    public string transactionState;
    public string createdAtUtc;
    public string updatedAtUtc;
    public string integritySha256;
    public CampaignMigrationReceipt migration = new CampaignMigrationReceipt();
    public CampaignRecoveryReceipt recovery = new CampaignRecoveryReceipt();
    public CampaignProgressData progress = new CampaignProgressData();
}

[Serializable]
public sealed class CampaignProgressData
{
    public string journeyGenerationId;
    public string activeLevelId;
    public List<LevelProgressRecord> levelProgress = new List<LevelProgressRecord>();
    public List<string> unlockedSymbolIds = new List<string>();
    public List<string> discoveredEnemyIds = new List<string>();
    public List<string> discoveredBossIds = new List<string>();
    public List<string> unlockedMemoryIds = new List<string>();
    public List<string> claimedRewardIds = new List<string>();
    public List<AppliedOutcomeReceipt> appliedOutcomeReceipts = new List<AppliedOutcomeReceipt>();
    public List<TutorialProgressRecord> tutorialProgress = new List<TutorialProgressRecord>();
    public List<SymbolMasteryRecord> symbolMastery = new List<SymbolMasteryRecord>();
    public List<WordMasteryRecord> wordMastery = new List<WordMasteryRecord>();
    public bool endlessModeUnlocked;
}

[Serializable]
public sealed class AppliedOutcomeReceipt
{
    public string outcomeId;
    public string levelId;
    public string appliedAtUtc;
    public LearningSessionKind sessionKind;

    public AppliedOutcomeReceipt() { }

    public AppliedOutcomeReceipt(string outcomeId, string levelId, string appliedAtUtc)
        : this(outcomeId, levelId, appliedAtUtc, LearningSessionKind.LevelAttempt) { }

    public AppliedOutcomeReceipt(
        string outcomeId, string levelId, string appliedAtUtc, LearningSessionKind sessionKind)
    {
        this.outcomeId = outcomeId;
        this.levelId = levelId;
        this.appliedAtUtc = appliedAtUtc;
        this.sessionKind = sessionKind;
    }
}

[Serializable]
public sealed class LevelProgressRecord
{
    public string levelId;
    public bool unlocked;
    public bool completed;
    public int bestStars;

    /// <summary>
    /// Best recorded score (SALIN-140) and the metric set from that same attempt.
    /// </summary>
    /// <remarks>
    /// These two move together, and only when the score improves, so the stored metrics always
    /// describe one real run rather than a mix of several. <see cref="bestStars"/> deliberately keeps
    /// its own independent max -- pre-existing committed-save behaviour -- so after several attempts
    /// bestStars and bestMetrics can describe different runs. Documented rather than silently
    /// redefined, because changing it would alter the meaning of saves already on players' devices.
    ///
    /// Both live inside CampaignProgressData, so TryResetJourney clears them by construction: it
    /// replaces progress wholesale from CampaignProgressFactory.CreateClean.
    /// </remarks>
    public float bestScore;
    public List<LevelMetricRecord> bestMetrics = new List<LevelMetricRecord>();
}

[Serializable]
public sealed class TutorialProgressRecord
{
    public string levelId;
    public bool seen;
    public int lastCompletedBeatIndex = -1;
}

[Serializable]
public sealed class CampaignMigrationReceipt
{
    public string migrationId;
    public int sourceSaveSchemaVersion;
    public CampaignMigrationState state = CampaignMigrationState.NotRequired;
    public string legacyArchiveSha256;
    public string completedAtUtc;
    public bool noticeAcknowledged;
}

[Serializable]
public sealed class CampaignRecoveryReceipt
{
    public string reasonCode;
    public string occurredAtUtc;
    public bool noticeAcknowledged;
}

public enum CampaignSaveFailureCode
{
    None,
    Missing,
    MalformedJson,
    ChecksumMismatch,
    UnsupportedSchema,
    WrongIdentity,
    IncompleteTransaction,
    InvalidStructure,
    InvalidCampaign,
    IoFailure,
}

public enum CampaignMigrationState
{
    NotRequired,
    Completed,
}

public enum CampaignSaveNoticeKind
{
    None,
    Migration,
    Recovery,
    Blocking,
}

public static class CampaignSaveTransactionState
{
    public const string Committed = "committed";
}

[Serializable]
public sealed class CampaignSaveNotice
{
    public CampaignSaveNoticeKind kind;
    public string reasonCode;

    public CampaignSaveNotice()
    {
        kind = CampaignSaveNoticeKind.None;
    }

    public CampaignSaveNotice(CampaignSaveNoticeKind kind, string reasonCode)
    {
        this.kind = kind;
        this.reasonCode = reasonCode;
    }
}

public sealed class CampaignSaveParseResult
{
    public bool Success { get; private set; }
    public CampaignSaveDocument Document { get; private set; }
    public CampaignSaveFailureCode FailureCode { get; private set; }
    public string ErrorMessage { get; private set; }

    public static CampaignSaveParseResult Succeeded(CampaignSaveDocument document)
    {
        return new CampaignSaveParseResult
        {
            Success = true,
            Document = document,
            FailureCode = CampaignSaveFailureCode.None,
        };
    }

    public static CampaignSaveParseResult Failed(CampaignSaveFailureCode code, string message = null)
    {
        return new CampaignSaveParseResult
        {
            Success = false,
            FailureCode = code,
            ErrorMessage = message,
        };
    }
}
