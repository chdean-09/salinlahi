using System;

public enum CampaignSaveInitializationStatus
{
    Ready,
    Migrated,
    Recovered,
    SafeReset,
    BlockedInvalidCampaign,
    BlockedUnsupportedSchema,
    BlockedIo,
}

public sealed class CampaignSaveInitializationResult
{
    public CampaignSaveInitializationStatus Status { get; set; }
    public CampaignSaveDocument Document { get; set; }
    public CampaignSaveFailureCode FailureCode { get; set; }
    public string ReasonCode { get; set; }
}

public sealed class CampaignSaveService
{
    private readonly ICampaignSaveStorage _storage;
    private readonly ILegacyProgressSource _legacySource;
    private readonly ITransactionMetadataProvider _metadata;
    private CampaignConfigSO _campaign;
    private LegacyArchiveLoadResult _archiveResult;
    private CampaignSaveCommitter _committer;

    public CampaignSaveDocument Current { get; private set; }
    public CampaignConfigSO Campaign => _campaign;
    public ICampaignSaveStorage Storage => _storage;
    public ITransactionMetadataProvider Metadata => _metadata;

    public CampaignSaveService(
        ICampaignSaveStorage storage,
        ILegacyProgressSource legacySource,
        ITransactionMetadataProvider metadata = null)
    {
        _storage = storage;
        _legacySource = legacySource;
        _metadata = metadata ?? new SystemTransactionMetadataProvider();
    }

    public CampaignSaveInitializationResult Initialize(CampaignConfigSO campaign)
    {
        try
        {
            return InitializeInternal(campaign);
        }
        catch (Exception exception)
        {
            return Blocked(CampaignSaveInitializationStatus.BlockedIo,
                CampaignSaveFailureCode.IoFailure, exception.Message);
        }
    }

    private CampaignSaveInitializationResult InitializeInternal(CampaignConfigSO campaign)
    {
        _campaign = campaign;
        var contentIssues = CampaignConfigValidator.Validate(campaign);
        for (int i = 0; i < contentIssues.Count; i++)
        {
            if (contentIssues[i].Severity == ContentValidationSeverity.Error)
                return Blocked(CampaignSaveInitializationStatus.BlockedInvalidCampaign,
                    CampaignSaveFailureCode.InvalidCampaign, "invalid-campaign");
        }

        _archiveResult = null;
        _committer = new CampaignSaveCommitter(
            _storage, _campaign, _metadata, () => _archiveResult?.IntegritySha256);

        CandidateInspection primary = Inspect(CampaignSaveFileRole.Primary);
        CandidateInspection temporary = Inspect(CampaignSaveFileRole.Temporary);
        CandidateInspection backup = Inspect(CampaignSaveFileRole.Backup);
        RecoveryDecision decision = CampaignSaveRecoveryResolver.Resolve(primary, temporary, backup);
        if (decision.Kind == RecoveryDecisionKind.Blocked)
        {
            CampaignSaveInitializationStatus status = primary.FailureCode == CampaignSaveFailureCode.UnsupportedSchema ||
                temporary.FailureCode == CampaignSaveFailureCode.UnsupportedSchema ||
                backup.FailureCode == CampaignSaveFailureCode.UnsupportedSchema
                ? CampaignSaveInitializationStatus.BlockedUnsupportedSchema
                : CampaignSaveInitializationStatus.BlockedIo;
            return Blocked(status, status == CampaignSaveInitializationStatus.BlockedUnsupportedSchema
                ? CampaignSaveFailureCode.UnsupportedSchema : CampaignSaveFailureCode.IoFailure, decision.ReasonCode);
        }

        if (decision.Kind == RecoveryDecisionKind.UsePrimary)
        {
            return PublishSelectedCandidate(primary, primary.IsMigratableV1
                ? CampaignSaveInitializationStatus.Migrated
                : CampaignSaveInitializationStatus.Ready);
        }

        if (decision.Kind == RecoveryDecisionKind.PromoteTemporary)
        {
            if (temporary.IsMigratableV1)
                return PublishSelectedCandidate(temporary, CampaignSaveInitializationStatus.Migrated);
            CampaignSaveCommitResult promoted = _committer.TryPromoteValidatedTemporary(
                temporary.Document, primary.Document);
            if (!promoted.Success)
                return Blocked(CampaignSaveInitializationStatus.BlockedIo, promoted.FailureCode, "temporary-recovery-failed");
            Current = promoted.Document;
            return new CampaignSaveInitializationResult { Status = CampaignSaveInitializationStatus.Recovered, Document = Current };
        }

        if (decision.Kind == RecoveryDecisionKind.RestoreBackup)
        {
            if (backup.IsMigratableV1)
                return PublishSelectedCandidate(backup, CampaignSaveInitializationStatus.Migrated);
            CampaignSaveCommitResult restored = _committer.TryCommit(
                backup.Document, null, new CampaignSaveCommitContext());
            if (!restored.Success)
                return Blocked(CampaignSaveInitializationStatus.BlockedIo, restored.FailureCode, "backup-recovery-failed");
            Current = restored.Document;
            return new CampaignSaveInitializationResult { Status = CampaignSaveInitializationStatus.Recovered, Document = Current };
        }

        bool hadCorruptEvidence = decision.Kind == RecoveryDecisionKind.CorruptRevisedData;
        if (hadCorruptEvidence)
        {
            QuarantineIfPresent(CampaignSaveFileRole.Primary, "corrupt-primary");
            QuarantineIfPresent(CampaignSaveFileRole.Temporary, "corrupt-temporary");
            QuarantineIfPresent(CampaignSaveFileRole.Backup, "corrupt-backup");
        }

        _archiveResult = new LegacyArchiveService(_storage, _legacySource, () => _metadata.UtcNow)
            .LoadOrCreate(_campaign);
        if (_archiveResult.Status == LegacyArchiveStatus.IoFailure)
            return Blocked(CampaignSaveInitializationStatus.BlockedIo, CampaignSaveFailureCode.IoFailure, "archive-io-failure");

        CampaignSaveDocument fresh;
        CampaignSaveInitializationStatus initStatus;
        if (_archiveResult.Archive != null &&
            (_archiveResult.Status == LegacyArchiveStatus.Created ||
             _archiveResult.Status == LegacyArchiveStatus.Rebuilt ||
             _archiveResult.Status == LegacyArchiveStatus.LoadedExisting))
        {
            fresh = LegacyMigrationBuilder.CreateFreshJourney(
                _archiveResult.Archive, _archiveResult.IntegritySha256, _campaign, _metadata.UtcNow);
            initStatus = CampaignSaveInitializationStatus.Migrated;
        }
        else
        {
            fresh = CampaignProgressFactory.CreateClean(_campaign, _metadata.UtcNow);
            if (hadCorruptEvidence || _archiveResult.Status == LegacyArchiveStatus.Unrecoverable)
            {
                fresh.recovery = new CampaignRecoveryReceipt
                {
                    reasonCode = "safe-reset",
                    occurredAtUtc = _metadata.UtcNow.ToUniversalTime().ToString("O"),
                    noticeAcknowledged = false,
                };
                initStatus = CampaignSaveInitializationStatus.SafeReset;
            }
            else
                initStatus = CampaignSaveInitializationStatus.Ready;
        }

        CampaignSaveCommitResult committed = _committer.TryCommit(fresh, null);
        if (!committed.Success)
            return Blocked(CampaignSaveInitializationStatus.BlockedIo, committed.FailureCode, "initial-save-failed");
        Current = committed.Document;
        return new CampaignSaveInitializationResult { Status = initStatus, Document = Current };
    }

    public CampaignSaveCommitResult TryCommit(Action<CampaignSaveDocument> mutation)
    {
        if (_committer == null || Current == null)
            return CampaignSaveCommitResult.Failed(
                CampaignSaveFailureCode.InvalidStructure,
                "Campaign save service is not initialized.");
        CampaignSaveCommitResult result = _committer.TryCommit(Current, mutation,
            new CampaignSaveCommitContext { CanBackupValidatedPrimary = true });
        if (result.Success)
            Current = result.Document;
        return result;
    }

    public bool TryUpdate(Action<CampaignSaveDocument> mutation)
    {
        return TryCommit(mutation).Success;
    }

    public void RetryReadOnlyInitialization()
    {
        if (_campaign != null)
            Initialize(_campaign);
    }

    private CandidateInspection Inspect(CampaignSaveFileRole role)
    {
        if (!_storage.Exists(role))
            return CandidateInspection.Missing(role);
        try
        {
            CampaignSaveParseResult parsed = CampaignSaveSerializer.TryDeserialize(_storage.ReadAllText(role));
            if (!parsed.Success)
                return new CandidateInspection { Role = role, Exists = true, FailureCode = parsed.FailureCode };
            string archiveChecksum = null;
            if (parsed.Document.migration != null && parsed.Document.migration.state == CampaignMigrationState.Completed)
            {
                if (!_storage.Exists(CampaignSaveFileRole.LegacyArchive))
                    return new CandidateInspection
                    {
                        Role = role,
                        Exists = true,
                        FailureCode = CampaignSaveFailureCode.InvalidStructure,
                        ReasonCode = "migration-archive-missing",
                    };
                LegacyArchiveParseResult archive = LegacyArchiveSerializer.TryDeserialize(
                    _storage.ReadAllText(CampaignSaveFileRole.LegacyArchive));
                if (!archive.Success || !string.Equals(
                        archive.Archive.targetCampaignId, _campaign.manifest.campaignId, StringComparison.Ordinal))
                    return new CandidateInspection
                    {
                        Role = role,
                        Exists = true,
                        FailureCode = CampaignSaveFailureCode.InvalidStructure,
                        ReasonCode = "migration-archive-invalid",
                    };
                archiveChecksum = archive.IntegritySha256;
                _archiveResult = new LegacyArchiveLoadResult
                {
                    Status = LegacyArchiveStatus.LoadedExisting,
                    Archive = archive.Archive,
                    IntegritySha256 = archiveChecksum,
                };
            }
            if (parsed.Document.saveSchemaVersion == 1)
            {
                CampaignSaveMigrationResult migration = CampaignSaveMigrator.TryUpgradeToCurrent(
                    parsed.Document, _campaign, "journey.00000000000000000000000000000001");
                if (migration.Success)
                {
                    CampaignSaveValidationResult migratedValidation = CampaignSaveValidator.Validate(
                        migration.Document, _campaign, archiveChecksum);
                    if (!migratedValidation.IsValid)
                        migration = CampaignSaveMigrationResult.Failed(
                            migratedValidation.FailureCode, migratedValidation.ErrorMessage);
                }
                return new CandidateInspection
                {
                    Role = role,
                    Exists = true,
                    Document = migration.Success ? parsed.Document : null,
                    FailureCode = migration.Success ? CampaignSaveFailureCode.None : migration.FailureCode,
                    ReasonCode = migration.Success ? "v1-migratable" : migration.ErrorMessage,
                    IsMigratableV1 = migration.Success,
                };
            }
            CampaignSaveValidationResult validation = CampaignSaveValidator.Validate(parsed.Document, _campaign, archiveChecksum);
            return new CandidateInspection
            {
                Role = role,
                Exists = true,
                Document = validation.IsValid ? parsed.Document : null,
                FailureCode = validation.IsValid ? CampaignSaveFailureCode.None : validation.FailureCode,
                ReasonCode = validation.ErrorMessage,
            };
        }
        catch (Exception exception)
        {
            return new CandidateInspection
            {
                Role = role,
                Exists = true,
                FailureCode = CampaignSaveFailureCode.IoFailure,
                ReasonCode = exception.Message,
            };
        }
    }

    private void QuarantineIfPresent(CampaignSaveFileRole role, string reason)
    {
        if (_storage.Exists(role))
            _storage.Quarantine(role, reason, _metadata.UtcNow);
    }

    private static CampaignSaveInitializationResult Blocked(
        CampaignSaveInitializationStatus status,
        CampaignSaveFailureCode code,
        string reason)
    {
        return new CampaignSaveInitializationResult
        {
            Status = status,
            FailureCode = code,
            ReasonCode = reason,
        };
    }

    private CampaignSaveInitializationResult PublishSelectedCandidate(
        CandidateInspection selected,
        CampaignSaveInitializationStatus status)
    {
        if (!selected.IsMigratableV1)
        {
            Current = selected.Document;
            return new CampaignSaveInitializationResult { Status = status, Document = Current };
        }

        CampaignSaveMigrationResult migrated = CampaignSaveMigrator.TryUpgradeToCurrent(
            selected.Document,
            _campaign,
            "journey." + Guid.NewGuid().ToString("N"));
        if (!migrated.Success)
            return Blocked(CampaignSaveInitializationStatus.BlockedIo,
                migrated.FailureCode, "save-migration-failed");
        CampaignSaveCommitResult published = _committer.TryCommit(
            migrated.Document,
            null,
            new CampaignSaveCommitContext { CanBackupValidatedPrimary = true });
        if (!published.Success)
            return Blocked(CampaignSaveInitializationStatus.BlockedIo,
                published.FailureCode, "save-migration-publication-failed");
        Current = published.Document;
        return new CampaignSaveInitializationResult { Status = status, Document = Current };
    }
}
