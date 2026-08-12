using System;
using System.Collections.Generic;

[Serializable]
public sealed class CampaignIdentityManifest
{
    public int identityManifestVersion;
    public string campaignId;
    public int contentSchemaVersion;
    public int saveSchemaVersion;
    public string sourceWorkbookSha256;
    public List<int> supportedSourceContentSchemas = new();
    public List<int> supportedSourceSaveSchemas = new();
    public string migrationId;
    public int minimumReadableSaveSchema;
    public int maximumReadableSaveSchema;
    public string startingLevelId;

    public bool IsRevisedV1 =>
        identityManifestVersion == 1 &&
        string.Equals(campaignId, ContentIdentity.RevisedCampaignId, StringComparison.Ordinal) &&
        contentSchemaVersion == 1 &&
        saveSchemaVersion == 1 &&
        string.Equals(sourceWorkbookSha256, ContentIdentity.ApprovedWorkbookSha256, StringComparison.Ordinal) &&
        ListsEqual(supportedSourceContentSchemas, new[] { 0, 1 }) &&
        ListsEqual(supportedSourceSaveSchemas, new[] { 0, 1 }) &&
        string.Equals(migrationId, "legacy-v0-to-revised-v1", StringComparison.Ordinal) &&
        minimumReadableSaveSchema == 1 &&
        maximumReadableSaveSchema == 1 &&
        string.Equals(startingLevelId, "level.ugat.01", StringComparison.Ordinal);

    public static CampaignIdentityManifest CreateRevisedV1()
    {
        return new CampaignIdentityManifest
        {
            identityManifestVersion = 1,
            campaignId = ContentIdentity.RevisedCampaignId,
            contentSchemaVersion = 1,
            saveSchemaVersion = 1,
            sourceWorkbookSha256 = ContentIdentity.ApprovedWorkbookSha256,
            supportedSourceContentSchemas = new List<int> { 0, 1 },
            supportedSourceSaveSchemas = new List<int> { 0, 1 },
            migrationId = "legacy-v0-to-revised-v1",
            minimumReadableSaveSchema = 1,
            maximumReadableSaveSchema = 1,
            startingLevelId = "level.ugat.01",
        };
    }

    private static bool ListsEqual(IReadOnlyList<int> actual, IReadOnlyList<int> expected)
    {
        if (actual == null || expected == null || actual.Count != expected.Count)
            return false;

        for (int i = 0; i < actual.Count; i++)
        {
            if (actual[i] != expected[i])
                return false;
        }

        return true;
    }
}
