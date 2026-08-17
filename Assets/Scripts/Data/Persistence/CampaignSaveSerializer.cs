using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class CampaignSaveSerializer
{
    public static string Serialize(CampaignSaveDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        CampaignSaveDocument clone = DeepClone(document);
        Normalize(clone);
        clone.integritySha256 = string.Empty;
        string unsignedJson = JsonUtility.ToJson(clone, false);
        clone.integritySha256 = ComputeSha256(unsignedJson);
        return JsonUtility.ToJson(clone, false);
    }

    public static CampaignSaveParseResult TryDeserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CampaignSaveParseResult.Failed(CampaignSaveFailureCode.Missing);

        CampaignSaveDocument parsed;
        try
        {
            parsed = JsonUtility.FromJson<CampaignSaveDocument>(json);
        }
        catch (Exception exception)
        {
            return CampaignSaveParseResult.Failed(
                CampaignSaveFailureCode.MalformedJson, exception.Message);
        }

        if (parsed == null)
            return CampaignSaveParseResult.Failed(CampaignSaveFailureCode.MalformedJson);

        Normalize(parsed);
        if (string.IsNullOrEmpty(parsed.integritySha256))
            return CampaignSaveParseResult.Failed(CampaignSaveFailureCode.InvalidStructure);

        string expected = parsed.integritySha256.Trim().ToLowerInvariant();
        parsed.integritySha256 = string.Empty;
        string actual = ComputeSha256(JsonUtility.ToJson(parsed, false));
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            return CampaignSaveParseResult.Failed(CampaignSaveFailureCode.ChecksumMismatch);

        parsed.integritySha256 = expected;
        return CampaignSaveParseResult.Succeeded(parsed);
    }

    public static CampaignSaveDocument DeepClone(CampaignSaveDocument document)
    {
        if (document == null)
            return null;

        CampaignSaveDocument clone = JsonUtility.FromJson<CampaignSaveDocument>(
            JsonUtility.ToJson(document, false));
        Normalize(clone);
        return clone;
    }

    public static string ComputeSha256(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    private static void Normalize(CampaignSaveDocument document)
    {
        if (document == null)
            return;

        if (document.migration == null)
            document.migration = new CampaignMigrationReceipt();
        if (document.recovery == null)
            document.recovery = new CampaignRecoveryReceipt();
        if (document.progress == null)
            document.progress = new CampaignProgressData();
        if (document.progress.levelProgress == null)
            document.progress.levelProgress = new System.Collections.Generic.List<LevelProgressRecord>();
        if (document.progress.unlockedSymbolIds == null)
            document.progress.unlockedSymbolIds = new System.Collections.Generic.List<string>();
        if (document.progress.discoveredEnemyIds == null)
            document.progress.discoveredEnemyIds = new System.Collections.Generic.List<string>();
        if (document.progress.discoveredBossIds == null)
            document.progress.discoveredBossIds = new System.Collections.Generic.List<string>();
        if (document.progress.unlockedMemoryIds == null)
            document.progress.unlockedMemoryIds = new System.Collections.Generic.List<string>();
        if (document.progress.claimedRewardIds == null)
            document.progress.claimedRewardIds = new System.Collections.Generic.List<string>();
        if (document.progress.appliedOutcomeReceipts == null)
            document.progress.appliedOutcomeReceipts = new System.Collections.Generic.List<AppliedOutcomeReceipt>();
        if (document.progress.tutorialProgress == null)
            document.progress.tutorialProgress = new System.Collections.Generic.List<TutorialProgressRecord>();
    }
}
