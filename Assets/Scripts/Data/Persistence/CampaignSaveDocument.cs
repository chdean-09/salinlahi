using System;
using System.Collections.Generic;

[Serializable]
public sealed class CampaignSaveDocument
{
    // SALIN-227 moved this 3 -> 4 when `endlessModeUnlocked` was dropped from CampaignProgressData.
    // A v4 build cannot read any file written by a v3 build: the integrity hash is recomputed from
    // re-serialized JSON (CampaignSaveSerializer.TryDeserialize), so a stored key the current class
    // no longer emits changes the hash input and the file fails as a superseded save. That is the
    // documented one-time development reset -- see CampaignSaveMigrator for why v4 has no data arm.
    public const int CurrentSaveSchemaVersion = 5;

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
    // SALIN-227 removed `endlessModeUnlocked` here, at save schema v4. SALIN-225 had deliberately
    // kept the field because dropping it changes the re-serialized JSON of every save on disk and
    // fails them all. That is still true, and it is now the accepted outcome: a v3 save is reported
    // as SupersededSchema and safe-reset rather than migrated. See CampaignSaveMigrator.
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

    /// <summary>
    /// SALIN-220. The five per-objective completion flags. The successor level unlocks only when
    /// all five are true -- see <see cref="LevelObjectiveGate"/>, the one place that predicate
    /// lives.
    /// </summary>
    /// <remarks>
    /// SATISFIED-BY-DEFAULT IS DELIBERATE, DO NOT "FIX" IT. An objective the level does not
    /// author counts as satisfied, so a level with no challenge sequence still writes
    /// wordsRestored and contextPassed true. Five of the fifteen levels author none; a literal
    /// all-five gate would hard-lock the campaign there. The rule is applied where the flags are
    /// produced, in <see cref="LevelObjectiveFlagResolver"/>.
    ///
    /// Additive under JsonUtility: a save written before SALIN-220 deserializes with all five
    /// false, and no CampaignSaveValidator invariant inspects them.
    ///
    /// SALIN-227 CORRECTION. This block used to end "Old saves are unaffected in practice". That
    /// was wrong, and it was measured wrong, not argued wrong: the additive-field reasoning holds
    /// for JsonUtility.FromJson but not for an integrity hash computed over ToJson output. ToJson
    /// emits all five keys, so a save written before SALIN-220 does not re-serialize to its own
    /// stored bytes and fails the checksum. Such saves were already being rejected and safe-reset
    /// before SALIN-227 existed. Proven on disk, not inferred.
    /// </remarks>
    public bool storyViewed;
    public bool symbolsPracticed;
    public bool wordsRestored;
    public bool contextPassed;
    public bool finalSyllableRestored;
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

    /// <summary>
    /// SALIN-227. The file was written by an OLDER save schema than this build's, so its stored
    /// integrity hash cannot be re-derived under the current field set. Distinct from
    /// ChecksumMismatch, which now means genuine corruption, and from UnsupportedSchema, which
    /// means a NEWER build wrote it. Deliberately NOT in CampaignSaveRecoveryResolver.IsBlocking:
    /// a superseded save must safe-reset with a notice, not refuse to boot.
    /// </summary>
    SupersededSchema,
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
