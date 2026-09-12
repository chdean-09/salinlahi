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
    /// <summary>
    /// SALIN-227. Quarantine and failure reason for a save written by an older schema. Kept apart
    /// from the "corrupt-*" reasons so a superseded file is not misfiled as corruption.
    /// </summary>
    public const string SupersededReasonCode = "superseded-schema";

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
            return PublishSelectedCandidate(primary, primary.IsMigratable
                ? CampaignSaveInitializationStatus.Migrated
                : CampaignSaveInitializationStatus.Ready);
        }

        if (decision.Kind == RecoveryDecisionKind.PromoteTemporary)
        {
            if (temporary.IsMigratable)
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
            if (backup.IsMigratable)
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
            QuarantineIfPresent(CampaignSaveFileRole.Primary, QuarantineReason(primary, "corrupt-primary"));
            QuarantineIfPresent(CampaignSaveFileRole.Temporary, QuarantineReason(temporary, "corrupt-temporary"));
            QuarantineIfPresent(CampaignSaveFileRole.Backup, QuarantineReason(backup, "corrupt-backup"));
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
            string rawJson = _storage.ReadAllText(role);
            CampaignSaveParseResult parsed = CampaignSaveSerializer.TryDeserialize(rawJson);
            if (!parsed.Success)
                return ClassifyParseFailure(role, rawJson, parsed.FailureCode);
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
            // SALIN-227 (Barrier B). This was `== 1`, which was the ONLY route to the migrator on
            // the load path -- so the v2 -> v3 arm that has existed since SALIN-171 could never run
            // against a file, no matter how the checksum behaved: a stored 2 fell straight through
            // to Validate below and was rejected as InvalidStructure. A range test routes every
            // in-range stored version instead of just the lowest one.
            if (parsed.Document.saveSchemaVersion >= 1 &&
                parsed.Document.saveSchemaVersion < CampaignSaveDocument.CurrentSaveSchemaVersion)
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
                    ReasonCode = migration.Success ? "schema-migratable" : migration.ErrorMessage,
                    IsMigratable = migration.Success,
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

    /// <summary>
    /// SALIN-227. Separates "this file is corrupt" from "this file was written by an older build".
    /// </summary>
    /// <remarks>
    /// A save whose schema has been superseded fails the integrity check for a completely ordinary
    /// reason: TryDeserialize re-derives the hash from JSON re-serialized with the CURRENT field
    /// set, so a key the old build emitted and this one no longer does changes the hash input. The
    /// bytes are intact; only the shape moved. Reporting that as ChecksumMismatch and filing it as
    /// "corrupt-primary" told whoever read the quarantine later a plain untruth, which is the one
    /// defect here worth fixing -- the safe-reset outcome itself is what the ticket asks for.
    ///
    /// The stored version is read back WITHOUT an integrity check, which is safe because the value
    /// is advisory: it only chooses a reason code. It can never admit a save, and the returned
    /// candidate still carries no Document, so the file stays rejected either way.
    /// </remarks>
    private static CandidateInspection ClassifyParseFailure(
        CampaignSaveFileRole role, string rawJson, CampaignSaveFailureCode failureCode)
    {
        if (failureCode == CampaignSaveFailureCode.ChecksumMismatch &&
            TryReadStoredSchemaVersion(rawJson, out int storedVersion) &&
            storedVersion >= 1 && storedVersion < CampaignSaveDocument.CurrentSaveSchemaVersion)
        {
            return new CandidateInspection
            {
                Role = role,
                Exists = true,
                FailureCode = CampaignSaveFailureCode.SupersededSchema,
                ReasonCode = SupersededReasonCode,
            };
        }

        return new CandidateInspection { Role = role, Exists = true, FailureCode = failureCode };
    }

    private static bool TryReadStoredSchemaVersion(string rawJson, out int storedVersion)
    {
        storedVersion = 0;
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;
        try
        {
            CampaignSaveDocument stored = UnityEngine.JsonUtility.FromJson<CampaignSaveDocument>(rawJson);
            if (stored == null)
                return false;
            storedVersion = stored.saveSchemaVersion;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string QuarantineReason(CandidateInspection candidate, string corruptReason)
    {
        return candidate != null && candidate.FailureCode == CampaignSaveFailureCode.SupersededSchema
            ? SupersededReasonCode
            : corruptReason;
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
        if (!selected.IsMigratable)
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
