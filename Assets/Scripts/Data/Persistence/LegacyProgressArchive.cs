using System;
using System.Collections.Generic;

[Serializable]
public sealed class LegacyProgressArchive
{
    public string fileFormat = "salinlahi-legacy-progress-archive";
    public int archiveSchemaVersion = 1;
    public int sourceSaveSchemaVersion;
    public string targetCampaignId;
    public string migrationId = "legacy-v0-to-revised-v1";
    public string sourceApplicationVersion;
    public string createdAtUtc;
    public string integritySha256;
    public List<LegacyProgressRecord> records = new List<LegacyProgressRecord>();
}

[Serializable]
public sealed class LegacyProgressRecord
{
    public string key;
    public LegacyProgressValueType valueType;
    public bool wasPresent;
    public int intValue;
    public float floatValue;
    public string stringValue;
}

public enum LegacyArchiveStatus
{
    LoadedExisting,
    Created,
    Rebuilt,
    NoLegacyData,
    Unrecoverable,
    IoFailure,
}

public sealed class LegacyArchiveLoadResult
{
    public LegacyArchiveStatus Status { get; set; }
    public LegacyProgressArchive Archive { get; set; }
    public string IntegritySha256 { get; set; }
    public string ErrorMessage { get; set; }
}
