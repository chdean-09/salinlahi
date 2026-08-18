using System;

public sealed class CampaignOutcomeJournalLoadResult
{
    public CampaignOutcomeCommitStatus Status { get; set; }
    public CampaignProgressOutcome Outcome { get; set; }
    public CampaignSaveFailureCode FailureCode { get; set; }
    public string ReasonCode { get; set; }
}

public sealed class CampaignOutcomeJournal
{
    private readonly ICampaignSaveStorage _storage;
    private readonly CampaignConfigSO _campaign;
    private readonly ITransactionMetadataProvider _metadata;

    public CampaignOutcomeJournal(
        ICampaignSaveStorage storage,
        CampaignConfigSO campaign,
        ITransactionMetadataProvider metadata = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _metadata = metadata ?? new SystemTransactionMetadataProvider();
    }

    public CampaignOutcomeJournalWriteResult TryPersist(
        CampaignProgressOutcome outcome,
        CampaignSaveDocument current)
    {
        CampaignSaveValidationResult validation = CampaignOutcomeValidator.Validate(
            outcome, _campaign, current);
        if (!validation.IsValid)
            return CampaignOutcomeJournalWriteResult.Failed(
                outcome, validation.FailureCode, "outcome-invalid");

        try
        {
            if (_storage.Exists(CampaignSaveFileRole.PendingOutcome))
            {
                JournalCandidate existing = ReadCandidate(CampaignSaveFileRole.PendingOutcome, current);
                if (existing.IsUnsupported)
                    return CampaignOutcomeJournalWriteResult.Failed(
                        outcome, existing.FailureCode, "pending-journal-unsupported");
                if (existing.IsValid)
                {
                    if (SameOutcome(existing.Outcome, outcome))
                    {
                        if (_storage.Exists(CampaignSaveFileRole.PendingOutcomeTemporary))
                            _storage.Delete(CampaignSaveFileRole.PendingOutcomeTemporary);
                        return CampaignOutcomeJournalWriteResult.Published(outcome);
                    }
                    return CampaignOutcomeJournalWriteResult.Failed(
                        outcome, CampaignSaveFailureCode.InvalidStructure, "different-pending-outcome");
                }
            }

            CampaignOutcomeJournalDocument document = new CampaignOutcomeJournalDocument
            {
                outcome = CampaignOutcomeSerializer.DeepClone(new CampaignOutcomeJournalDocument { outcome = outcome }).outcome,
            };
            string serialized = CampaignOutcomeSerializer.Serialize(document);
            _storage.WriteAllTextFlushed(CampaignSaveFileRole.PendingOutcomeTemporary, serialized);
            JournalCandidate temporary = ReadCandidate(CampaignSaveFileRole.PendingOutcomeTemporary, current);
            if (!temporary.IsValid || !SameOutcome(temporary.Outcome, outcome))
                return CampaignOutcomeJournalWriteResult.Failed(
                    outcome, temporary.FailureCode == CampaignSaveFailureCode.None
                        ? CampaignSaveFailureCode.InvalidStructure : temporary.FailureCode,
                    "journal-temporary-invalid");

            _storage.PromotePendingOutcomeTemporary();
            JournalCandidate published = ReadCandidate(CampaignSaveFileRole.PendingOutcome, current);
            if (!published.IsValid || !SameOutcome(published.Outcome, outcome))
                return CampaignOutcomeJournalWriteResult.Failed(
                    outcome, published.FailureCode == CampaignSaveFailureCode.None
                        ? CampaignSaveFailureCode.InvalidStructure : published.FailureCode,
                    "journal-published-invalid");
            return CampaignOutcomeJournalWriteResult.Published(published.Outcome);
        }
        catch (Exception exception)
        {
            return CampaignOutcomeJournalWriteResult.Failed(
                outcome, CampaignSaveFailureCode.IoFailure, exception.Message);
        }
    }

    public CampaignOutcomeJournalLoadResult TryLoadRecoverable(
        CampaignSaveDocument current)
    {
        try
        {
            JournalCandidate published = ReadCandidate(CampaignSaveFileRole.PendingOutcome, current);
            if (published.IsUnsupported)
                return Blocked(published.FailureCode, "pending-journal-unsupported");
            if (published.Exists && !published.IsValid)
            {
                if (!QuarantineCandidate(CampaignSaveFileRole.PendingOutcome, published, "corrupt-published-journal"))
                    return Blocked(CampaignSaveFailureCode.IoFailure, "journal-quarantine-failed");
                published = JournalCandidate.Missing();
            }

            JournalCandidate temporary = ReadCandidate(CampaignSaveFileRole.PendingOutcomeTemporary, current);
            if (temporary.IsUnsupported)
                return Blocked(temporary.FailureCode, "pending-journal-temporary-unsupported");
            if (temporary.Exists && !temporary.IsValid)
            {
                string reason = temporary.FailureCode == CampaignSaveFailureCode.WrongIdentity
                    ? "superseded-by-reset" : "corrupt-temporary-journal";
                if (!QuarantineCandidate(CampaignSaveFileRole.PendingOutcomeTemporary, temporary, reason))
                    return Blocked(CampaignSaveFailureCode.IoFailure, "journal-quarantine-failed");
                temporary = JournalCandidate.Missing();
            }

            if (published.IsValid && temporary.IsValid)
            {
                if (!SameOutcome(published.Outcome, temporary.Outcome))
                    return Blocked(CampaignSaveFailureCode.InvalidStructure, "different-pending-outcome");
                if (!_storage.DeleteAndReport(CampaignSaveFileRole.PendingOutcomeTemporary))
                    return Blocked(CampaignSaveFailureCode.IoFailure, "journal-temporary-delete-failed");
                return Pending(published.Outcome);
            }

            if (published.IsValid)
                return Pending(published.Outcome);

            if (temporary.IsValid)
            {
                _storage.PromotePendingOutcomeTemporary();
                JournalCandidate promoted = ReadCandidate(CampaignSaveFileRole.PendingOutcome, current);
                if (!promoted.IsValid)
                    return Blocked(promoted.FailureCode == CampaignSaveFailureCode.None
                        ? CampaignSaveFailureCode.InvalidStructure : promoted.FailureCode,
                        "journal-promotion-invalid");
                return Pending(promoted.Outcome);
            }

            return new CampaignOutcomeJournalLoadResult
            {
                Status = CampaignOutcomeCommitStatus.Rejected,
                FailureCode = CampaignSaveFailureCode.Missing,
                ReasonCode = "no-pending-journal",
            };
        }
        catch (Exception exception)
        {
            return Blocked(CampaignSaveFailureCode.IoFailure, exception.Message);
        }
    }

    public bool Clear()
    {
        try
        {
            _storage.Delete(CampaignSaveFileRole.PendingOutcomeTemporary);
            _storage.Delete(CampaignSaveFileRole.PendingOutcome);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private JournalCandidate ReadCandidate(CampaignSaveFileRole role, CampaignSaveDocument current)
    {
        if (!_storage.Exists(role))
            return JournalCandidate.Missing();

        CampaignOutcomeJournalParseResult parsed = CampaignOutcomeSerializer.TryDeserialize(
            _storage.ReadAllText(role));
        if (!parsed.Success)
            return new JournalCandidate
            {
                Exists = true,
                FailureCode = parsed.FailureCode,
                IsUnsupported = parsed.FailureCode == CampaignSaveFailureCode.UnsupportedSchema,
            };

        // Upgrade at the parse boundary: SameOutcome compares serialized JSON from five call
        // sites, and every one of them must see a v2 outcome.
        CampaignOutcomeValidator.UpgradeToCurrent(parsed.Document.outcome);

        CampaignSaveValidationResult validation = CampaignOutcomeValidator.Validate(
            parsed.Document.outcome, _campaign, current);
        return new JournalCandidate
        {
            Exists = true,
            Outcome = parsed.Document.outcome,
            FailureCode = validation.IsValid ? CampaignSaveFailureCode.None : validation.FailureCode,
            IsUnsupported = validation.FailureCode == CampaignSaveFailureCode.UnsupportedSchema,
            IsValid = validation.IsValid,
        };
    }

    private bool QuarantineCandidate(
        CampaignSaveFileRole role,
        JournalCandidate candidate,
        string reason)
    {
        if (!candidate.Exists)
            return true;
        _storage.Quarantine(role, reason, _metadata.UtcNow);
        return true;
    }

    private static bool SameOutcome(CampaignProgressOutcome left, CampaignProgressOutcome right)
    {
        if (left == null || right == null)
            return left == right;
        return string.Equals(
            CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = left }),
            CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = right }),
            StringComparison.Ordinal);
    }

    private static CampaignOutcomeJournalLoadResult Pending(CampaignProgressOutcome outcome)
    {
        return new CampaignOutcomeJournalLoadResult
        {
            Status = CampaignOutcomeCommitStatus.PendingRetry,
            Outcome = outcome,
            FailureCode = CampaignSaveFailureCode.None,
            ReasonCode = "journal-recovered",
        };
    }

    private static CampaignOutcomeJournalLoadResult Blocked(
        CampaignSaveFailureCode code, string reason)
    {
        return new CampaignOutcomeJournalLoadResult
        {
            Status = CampaignOutcomeCommitStatus.Blocked,
            FailureCode = code,
            ReasonCode = reason,
        };
    }

    private sealed class JournalCandidate
    {
        public bool Exists;
        public bool IsValid;
        public bool IsUnsupported;
        public CampaignProgressOutcome Outcome;
        public CampaignSaveFailureCode FailureCode;

        public static JournalCandidate Missing() => new JournalCandidate();
    }
}

internal static class CampaignSaveStorageExtensions
{
    public static bool DeleteAndReport(this ICampaignSaveStorage storage, CampaignSaveFileRole role)
    {
        storage.Delete(role);
        return true;
    }
}
