using System;
using System.Collections.Generic;

public sealed class CampaignOutcomeCoordinator
{
    private readonly CampaignSaveService _service;
    private readonly CampaignOutcomeJournal _journal;
    private readonly CampaignConfigSO _campaign;
    private readonly ITransactionMetadataProvider _metadata;

    public CampaignOutcomeCoordinator(
        CampaignSaveService service,
        CampaignOutcomeJournal journal,
        CampaignConfigSO campaign,
        ITransactionMetadataProvider metadata = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _metadata = metadata ?? new SystemTransactionMetadataProvider();
    }

    public CampaignOutcomeCommitResult TryCommit(CampaignProgressOutcome outcome)
    {
        if (_service.Current == null)
            return CampaignOutcomeCommitResult.Blocked(
                outcome, CampaignSaveFailureCode.InvalidStructure, "save-service-not-ready");

        CampaignSaveValidationResult validation = CampaignOutcomeValidator.Validate(
            outcome, _campaign, _service.Current);
        if (!validation.IsValid)
            return CampaignOutcomeCommitResult.Rejected(
                outcome, validation.FailureCode, "outcome-invalid");

        if (HasReceipt(_service.Current, outcome.outcomeId))
        {
            if (!_journal.Clear())
                return CampaignOutcomeCommitResult.PendingRetry(
                    outcome, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
            return CampaignOutcomeCommitResult.AlreadyCommitted(outcome);
        }

        CampaignOutcomeJournalLoadResult unresolved = _journal.TryLoadRecoverable(_service.Current);
        if (unresolved.Status == CampaignOutcomeCommitStatus.Blocked)
            return CampaignOutcomeCommitResult.Blocked(
                outcome, unresolved.FailureCode, unresolved.ReasonCode);
        if (unresolved.Status == CampaignOutcomeCommitStatus.PendingRetry &&
            !SameOutcome(unresolved.Outcome, outcome))
            return CampaignOutcomeCommitResult.Blocked(
                outcome, CampaignSaveFailureCode.InvalidStructure, "different-pending-outcome");

        CampaignOutcomeJournalWriteResult journalResult = _journal.TryPersist(
            outcome, _service.Current);
        if (!journalResult.Success)
            return ResolveJournalFailure(outcome, journalResult.FailureCode, journalResult.ReasonCode);

        return CommitJournaledOutcome(outcome);
    }

    public CampaignOutcomeCommitResult RetryPending()
    {
        if (_service.Current == null)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "save-service-not-ready");
        CampaignOutcomeJournalLoadResult loaded = _journal.TryLoadRecoverable(_service.Current);
        if (loaded.Status == CampaignOutcomeCommitStatus.Blocked)
            return CampaignOutcomeCommitResult.Blocked(null, loaded.FailureCode, loaded.ReasonCode);
        if (loaded.Status != CampaignOutcomeCommitStatus.PendingRetry || loaded.Outcome == null)
            return CampaignOutcomeCommitResult.Rejected(null, loaded.FailureCode, loaded.ReasonCode);
        if (HasReceipt(_service.Current, loaded.Outcome.outcomeId))
        {
            if (!_journal.Clear())
                return CampaignOutcomeCommitResult.PendingRetry(
                    loaded.Outcome, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
            return CampaignOutcomeCommitResult.AlreadyCommitted(loaded.Outcome);
        }
        return CommitJournaledOutcome(loaded.Outcome);
    }

    public CampaignOutcomeCommitResult ReplayPendingOnStartup()
    {
        return RetryPending();
    }

    public CampaignOutcomeCommitResult TryResetJourney()
    {
        if (_service.Current == null)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "save-service-not-ready");

        CampaignSaveDocument clean = CampaignProgressFactory.CreateClean(_campaign, _metadata.UtcNow);
        CampaignMigrationReceipt migration = _service.Current.migration;
        CampaignRecoveryReceipt recovery = _service.Current.recovery;
        CampaignSaveCommitResult committed = _service.TryCommit(document =>
        {
            document.progress = clean.progress;
            document.migration = migration;
            document.recovery = recovery;
        });
        if (!committed.Success)
            return CampaignOutcomeCommitResult.Blocked(
                null, committed.FailureCode, "reset-save-failed");
        if (!string.Equals(
                committed.Document.progress.journeyGenerationId,
                clean.progress.journeyGenerationId,
                StringComparison.Ordinal))
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "reset-generation-verification-failed");
        if (!_journal.Clear())
            return CampaignOutcomeCommitResult.PendingRetry(
                null, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
        return CampaignOutcomeCommitResult.Committed(null);
    }

    private CampaignOutcomeCommitResult CommitJournaledOutcome(CampaignProgressOutcome outcome)
    {
        CampaignSaveCommitResult committed = _service.TryCommit(document => ApplyOutcome(document, outcome));
        if (!committed.Success)
            return ResolveJournalFailure(outcome, committed.FailureCode, "campaign-save-failed");
        if (!VerifyPublishedOutcome(committed.Document, outcome))
            return CampaignOutcomeCommitResult.PendingRetry(
                outcome, CampaignSaveFailureCode.InvalidStructure, "published-outcome-verification-failed");
        if (!_journal.Clear())
            return CampaignOutcomeCommitResult.PendingRetry(
                outcome, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
        return CampaignOutcomeCommitResult.Committed(outcome);
    }

    private CampaignOutcomeCommitResult ResolveJournalFailure(
        CampaignProgressOutcome outcome,
        CampaignSaveFailureCode fallbackCode,
        string fallbackReason)
    {
        CampaignOutcomeJournalLoadResult recovered = _journal.TryLoadRecoverable(_service.Current);
        if (recovered.Status == CampaignOutcomeCommitStatus.PendingRetry)
            return CampaignOutcomeCommitResult.PendingRetry(
                recovered.Outcome, fallbackCode == CampaignSaveFailureCode.None
                    ? CampaignSaveFailureCode.IoFailure : fallbackCode,
                fallbackReason);
        if (recovered.Status == CampaignOutcomeCommitStatus.Blocked)
            return CampaignOutcomeCommitResult.Blocked(
                outcome, recovered.FailureCode, recovered.ReasonCode);
        return CampaignOutcomeCommitResult.Rejected(
            outcome, fallbackCode == CampaignSaveFailureCode.None
                ? CampaignSaveFailureCode.IoFailure : fallbackCode, fallbackReason);
    }

    private static bool HasReceipt(CampaignSaveDocument document, string outcomeId)
    {
        if (document?.progress?.appliedOutcomeReceipts == null)
            return false;
        for (int i = 0; i < document.progress.appliedOutcomeReceipts.Count; i++)
        {
            AppliedOutcomeReceipt receipt = document.progress.appliedOutcomeReceipts[i];
            if (receipt != null && string.Equals(receipt.outcomeId, outcomeId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static LevelProgressRecord FindLevel(CampaignSaveDocument document, string levelId)
    {
        if (document?.progress?.levelProgress == null)
            return null;
        for (int i = 0; i < document.progress.levelProgress.Count; i++)
        {
            LevelProgressRecord record = document.progress.levelProgress[i];
            if (record != null && string.Equals(record.levelId, levelId, StringComparison.Ordinal))
                return record;
        }
        return null;
    }

    private static void UnionSorted(List<string> target, List<string> additions)
    {
        if (additions == null)
            return;
        for (int i = 0; i < additions.Count; i++)
            if (!target.Contains(additions[i]))
                target.Add(additions[i]);
        target.Sort(StringComparer.Ordinal);
    }

    private void ApplyOutcome(CampaignSaveDocument document, CampaignProgressOutcome outcome)
    {
        LevelProgressRecord level = FindLevel(document, outcome.levelId);
        level.completed = true;
        level.unlocked = true;
        level.bestStars = Math.Max(level.bestStars, outcome.stars);

        List<string> levels = CampaignSaveValidator.GetConfiguredLevelIds(_campaign);
        int index = levels.IndexOf(outcome.levelId);
        if (index >= 0 && index + 1 < levels.Count)
            FindLevel(document, levels[index + 1]).unlocked = true;
        else if (index == levels.Count - 1)
            document.progress.endlessModeUnlocked = true;

        UnionSorted(document.progress.unlockedSymbolIds, outcome.unlockedSymbolIds);
        UnionSorted(document.progress.unlockedMemoryIds, outcome.unlockedMemoryIds);
        UnionSorted(document.progress.claimedRewardIds, outcome.claimedRewardIds);
        document.progress.appliedOutcomeReceipts.Add(new AppliedOutcomeReceipt(
            outcome.outcomeId,
            outcome.levelId,
            _metadata.UtcNow.ToUniversalTime().ToString("O")));
        document.progress.appliedOutcomeReceipts.Sort((left, right) =>
            string.CompareOrdinal(left?.outcomeId, right?.outcomeId));
    }

    private bool VerifyPublishedOutcome(CampaignSaveDocument document, CampaignProgressOutcome outcome)
    {
        if (document == null || !HasReceipt(document, outcome.outcomeId))
            return false;
        LevelProgressRecord level = FindLevel(document, outcome.levelId);
        if (level == null || !level.completed || level.bestStars < outcome.stars)
            return false;
        List<string> levels = CampaignSaveValidator.GetConfiguredLevelIds(_campaign);
        int index = levels.IndexOf(outcome.levelId);
        if (index >= 0 && index + 1 < levels.Count && !FindLevel(document, levels[index + 1]).unlocked)
            return false;
        if (index == levels.Count - 1 && !document.progress.endlessModeUnlocked)
            return false;
        return ContainsAll(document.progress.unlockedSymbolIds, outcome.unlockedSymbolIds) &&
            ContainsAll(document.progress.unlockedMemoryIds, outcome.unlockedMemoryIds) &&
            ContainsAll(document.progress.claimedRewardIds, outcome.claimedRewardIds);
    }

    private static bool ContainsAll(List<string> values, List<string> expected)
    {
        if (expected == null)
            return true;
        for (int i = 0; i < expected.Count; i++)
            if (!values.Contains(expected[i]))
                return false;
        return true;
    }

    private static bool SameOutcome(CampaignProgressOutcome left, CampaignProgressOutcome right)
    {
        return string.Equals(
            CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = left }),
            CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = right }),
            StringComparison.Ordinal);
    }
}
