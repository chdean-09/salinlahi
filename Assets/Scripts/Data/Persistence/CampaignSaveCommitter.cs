using System;

public interface ITransactionMetadataProvider
{
    string CreateTransactionId();
    DateTime UtcNow { get; }
}

public sealed class SystemTransactionMetadataProvider : ITransactionMetadataProvider
{
    public string CreateTransactionId() => Guid.NewGuid().ToString("N");
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class CampaignSaveCommitContext
{
    public bool CanBackupValidatedPrimary { get; set; }
}

public sealed class CampaignSaveCommitResult
{
    public bool Success { get; private set; }
    public CampaignSaveDocument Document { get; private set; }
    public CampaignSaveFailureCode FailureCode { get; private set; }
    public string ErrorMessage { get; private set; }

    public static CampaignSaveCommitResult Succeeded(CampaignSaveDocument document) => new CampaignSaveCommitResult
    {
        Success = true,
        Document = document,
        FailureCode = CampaignSaveFailureCode.None,
    };

    public static CampaignSaveCommitResult Failed(CampaignSaveFailureCode code, string message) => new CampaignSaveCommitResult
    {
        FailureCode = code,
        ErrorMessage = message,
    };
}

public sealed class CampaignSaveCommitter
{
    private readonly ICampaignSaveStorage _storage;
    private readonly CampaignConfigSO _campaign;
    private readonly ITransactionMetadataProvider _metadata;
    private readonly Func<string> _archiveChecksumProvider;

    public CampaignSaveCommitter(
        ICampaignSaveStorage storage,
        CampaignConfigSO campaign,
        ITransactionMetadataProvider metadata = null,
        Func<string> archiveChecksumProvider = null)
    {
        _storage = storage;
        _campaign = campaign;
        _metadata = metadata ?? new SystemTransactionMetadataProvider();
        _archiveChecksumProvider = archiveChecksumProvider;
    }

    public CampaignSaveCommitResult TryCommit(
        CampaignSaveDocument current,
        Action<CampaignSaveDocument> mutation,
        CampaignSaveCommitContext context = null)
    {
        try
        {
            CampaignSaveDocument candidate = current == null
                ? CampaignProgressFactory.CreateClean(_campaign, _metadata.UtcNow)
                : CampaignSaveSerializer.DeepClone(current);
            mutation?.Invoke(candidate);
            candidate.revision = current == null ? 1 : current.revision + 1;
            candidate.transactionId = _metadata.CreateTransactionId();
            candidate.transactionState = CampaignSaveTransactionState.Committed;
            candidate.updatedAtUtc = _metadata.UtcNow.ToUniversalTime().ToString("O");
            if (string.IsNullOrEmpty(candidate.createdAtUtc))
                candidate.createdAtUtc = candidate.updatedAtUtc;

            CampaignSaveValidationResult validation = CampaignSaveValidator.Validate(
                candidate, _campaign, _archiveChecksumProvider?.Invoke());
            if (!validation.IsValid)
                return CampaignSaveCommitResult.Failed(validation.FailureCode, validation.ErrorMessage);

            string serialized = CampaignSaveSerializer.Serialize(candidate);
            _storage.WriteAllTextFlushed(CampaignSaveFileRole.Temporary, serialized);
            CampaignSaveParseResult readBack = CampaignSaveSerializer.TryDeserialize(
                _storage.ReadAllText(CampaignSaveFileRole.Temporary));
            if (!readBack.Success || readBack.Document.transactionId != candidate.transactionId ||
                readBack.Document.revision != candidate.revision)
                return CampaignSaveCommitResult.Failed(
                    readBack.Success ? CampaignSaveFailureCode.InvalidStructure : readBack.FailureCode,
                    "The temporary save failed read-back validation.");

            if (context != null && context.CanBackupValidatedPrimary && current != null &&
                _storage.Exists(CampaignSaveFileRole.Primary))
                _storage.Copy(CampaignSaveFileRole.Primary, CampaignSaveFileRole.Backup, true);

            _storage.PromoteTemporaryToPrimary();
            CampaignSaveParseResult published = CampaignSaveSerializer.TryDeserialize(
                _storage.ReadAllText(CampaignSaveFileRole.Primary));
            if (!published.Success)
                return CampaignSaveCommitResult.Failed(published.FailureCode, "The published save failed validation.");
            CampaignSaveValidationResult publishedValidation = CampaignSaveValidator.Validate(
                published.Document, _campaign, _archiveChecksumProvider?.Invoke());
            if (!publishedValidation.IsValid)
                return CampaignSaveCommitResult.Failed(publishedValidation.FailureCode, publishedValidation.ErrorMessage);
            return CampaignSaveCommitResult.Succeeded(published.Document);
        }
        catch (Exception exception)
        {
            return CampaignSaveCommitResult.Failed(CampaignSaveFailureCode.IoFailure, exception.Message);
        }
    }

    public CampaignSaveCommitResult TryPromoteValidatedTemporary(
        CampaignSaveDocument selectedTemporary,
        CampaignSaveDocument validatedPrimary = null)
    {
        try
        {
            CampaignSaveParseResult readBack = CampaignSaveSerializer.TryDeserialize(
                _storage.ReadAllText(CampaignSaveFileRole.Temporary));
            if (!readBack.Success || readBack.Document.transactionId != selectedTemporary.transactionId ||
                readBack.Document.revision != selectedTemporary.revision)
                return CampaignSaveCommitResult.Failed(CampaignSaveFailureCode.InvalidStructure, "Temporary changed during recovery.");
            if (validatedPrimary != null)
                _storage.Copy(CampaignSaveFileRole.Primary, CampaignSaveFileRole.Backup, true);
            _storage.PromoteTemporaryToPrimary();
            CampaignSaveParseResult published = CampaignSaveSerializer.TryDeserialize(
                _storage.ReadAllText(CampaignSaveFileRole.Primary));
            if (!published.Success)
                return CampaignSaveCommitResult.Failed(published.FailureCode, "Recovered save failed validation.");
            CampaignSaveValidationResult validation = CampaignSaveValidator.Validate(
                published.Document, _campaign, _archiveChecksumProvider?.Invoke());
            return validation.IsValid
                ? CampaignSaveCommitResult.Succeeded(published.Document)
                : CampaignSaveCommitResult.Failed(validation.FailureCode, validation.ErrorMessage);
        }
        catch (Exception exception)
        {
            return CampaignSaveCommitResult.Failed(CampaignSaveFailureCode.IoFailure, exception.Message);
        }
    }
}
