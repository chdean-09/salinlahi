using System;

public sealed class LegacyArchiveService
{
    private readonly ICampaignSaveStorage _storage;
    private readonly ILegacyProgressSource _source;
    private readonly Func<DateTime> _utcNow;
    private readonly string _sourceApplicationVersion;

    public LegacyArchiveService(
        ICampaignSaveStorage storage,
        ILegacyProgressSource source,
        Func<DateTime> utcNow = null,
        string sourceApplicationVersion = "unknown")
    {
        _storage = storage;
        _source = source;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _sourceApplicationVersion = sourceApplicationVersion;
    }

    public LegacyArchiveLoadResult LoadOrCreate(CampaignConfigSO campaign)
    {
        try
        {
            if (_storage.Exists(CampaignSaveFileRole.LegacyArchive))
            {
                LegacyArchiveParseResult parsed = LegacyArchiveSerializer.TryDeserialize(
                    _storage.ReadAllText(CampaignSaveFileRole.LegacyArchive));
                if (parsed.Success && IsCompatible(parsed.Archive, campaign))
                    return new LegacyArchiveLoadResult
                    {
                        Status = LegacyArchiveStatus.LoadedExisting,
                        Archive = parsed.Archive,
                        IntegritySha256 = parsed.IntegritySha256,
                    };

                _storage.Quarantine(CampaignSaveFileRole.LegacyArchive, "invalid-archive", _utcNow());
                if (!HasLegacyData())
                    return new LegacyArchiveLoadResult { Status = LegacyArchiveStatus.Unrecoverable };
                return CreateArchive(campaign, LegacyArchiveStatus.Rebuilt);
            }

            if (!HasLegacyData())
                return new LegacyArchiveLoadResult { Status = LegacyArchiveStatus.NoLegacyData };
            return CreateArchive(campaign, LegacyArchiveStatus.Created);
        }
        catch (Exception exception)
        {
            return new LegacyArchiveLoadResult
            {
                Status = LegacyArchiveStatus.IoFailure,
                ErrorMessage = exception.Message,
            };
        }
    }

    private LegacyArchiveLoadResult CreateArchive(CampaignConfigSO campaign, LegacyArchiveStatus status)
    {
        var archive = new LegacyProgressArchive
        {
            sourceSaveSchemaVersion = 0,
            targetCampaignId = campaign?.manifest?.campaignId,
            sourceApplicationVersion = _sourceApplicationVersion,
            createdAtUtc = _utcNow().ToUniversalTime().ToString("O"),
        };
        for (int i = 0; i < LegacyProgressKeyCatalog.All.Count; i++)
        {
            LegacyProgressKeyDefinition definition = LegacyProgressKeyCatalog.All[i];
            bool present = _source.HasKey(definition.Key);
            var record = new LegacyProgressRecord
            {
                key = definition.Key,
                valueType = definition.ValueType,
                wasPresent = present,
            };
            if (present)
            {
                switch (definition.ValueType)
                {
                    case LegacyProgressValueType.Int: record.intValue = _source.GetInt(definition.Key, 0); break;
                    case LegacyProgressValueType.Float: record.floatValue = _source.GetFloat(definition.Key, 0f); break;
                    case LegacyProgressValueType.String: record.stringValue = _source.GetString(definition.Key, string.Empty); break;
                }
            }
            archive.records.Add(record);
        }

        string serialized = LegacyArchiveSerializer.Serialize(archive);
        _storage.WriteAllTextFlushed(CampaignSaveFileRole.LegacyArchive, serialized);
        LegacyArchiveParseResult parsed = LegacyArchiveSerializer.TryDeserialize(serialized);
        return new LegacyArchiveLoadResult
        {
            Status = status,
            Archive = parsed.Archive,
            IntegritySha256 = parsed.IntegritySha256,
        };
    }

    private bool HasLegacyData()
    {
        for (int i = 0; i < LegacyProgressKeyCatalog.All.Count; i++)
            if (_source.HasKey(LegacyProgressKeyCatalog.All[i].Key)) return true;
        return false;
    }

    private static bool IsCompatible(LegacyProgressArchive archive, CampaignConfigSO campaign)
    {
        return archive != null && archive.archiveSchemaVersion == 1 && archive.sourceSaveSchemaVersion == 0 &&
            string.Equals(archive.migrationId, "legacy-v0-to-revised-v1", StringComparison.Ordinal) &&
            string.Equals(archive.targetCampaignId, campaign?.manifest?.campaignId, StringComparison.Ordinal) &&
            archive.records != null && archive.records.Count == LegacyProgressKeyCatalog.All.Count;
    }
}
