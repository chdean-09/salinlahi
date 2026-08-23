using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class CampaignOutcomeSerializer
{
    public static string Serialize(CampaignOutcomeJournalDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        CampaignOutcomeJournalDocument clone = DeepClone(document);
        Normalize(clone);
        clone.integritySha256 = string.Empty;
        string unsignedJson = JsonUtility.ToJson(clone, false);
        clone.integritySha256 = ComputeSha256(unsignedJson);
        return JsonUtility.ToJson(clone, false);
    }

    public static CampaignOutcomeJournalParseResult TryDeserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CampaignOutcomeJournalParseResult.Failed(CampaignSaveFailureCode.Missing);

        CampaignOutcomeJournalDocument parsed;
        try
        {
            parsed = JsonUtility.FromJson<CampaignOutcomeJournalDocument>(json);
        }
        catch (Exception exception)
        {
            return CampaignOutcomeJournalParseResult.Failed(
                CampaignSaveFailureCode.MalformedJson, exception.Message);
        }

        if (parsed == null)
            return CampaignOutcomeJournalParseResult.Failed(CampaignSaveFailureCode.MalformedJson);

        Normalize(parsed);
        if (!string.Equals(parsed.fileFormat, "salinlahi-campaign-outcome-journal", StringComparison.Ordinal))
            return CampaignOutcomeJournalParseResult.Failed(
                CampaignSaveFailureCode.InvalidStructure, "Unexpected journal file format.");
        if (string.IsNullOrEmpty(parsed.integritySha256))
            return CampaignOutcomeJournalParseResult.Failed(CampaignSaveFailureCode.InvalidStructure);

        string expected = parsed.integritySha256.Trim().ToLowerInvariant();
        parsed.integritySha256 = string.Empty;
        string actual = ComputeSha256(JsonUtility.ToJson(parsed, false));
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            return CampaignOutcomeJournalParseResult.Failed(CampaignSaveFailureCode.ChecksumMismatch);

        parsed.integritySha256 = expected;
        if (parsed.journalSchemaVersion > CampaignOutcomeJournalDocument.CurrentJournalSchemaVersion)
            return CampaignOutcomeJournalParseResult.Failed(
                CampaignSaveFailureCode.UnsupportedSchema, "The outcome journal was created by a newer version.");
        if (parsed.journalSchemaVersion != CampaignOutcomeJournalDocument.CurrentJournalSchemaVersion)
            return CampaignOutcomeJournalParseResult.Failed(
                CampaignSaveFailureCode.InvalidStructure, "The outcome journal schema is not supported.");
        return CampaignOutcomeJournalParseResult.Succeeded(parsed);
    }

    public static CampaignOutcomeJournalDocument DeepClone(CampaignOutcomeJournalDocument document)
    {
        if (document == null)
            return null;
        CampaignOutcomeJournalDocument clone = JsonUtility.FromJson<CampaignOutcomeJournalDocument>(
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

    private static void Normalize(CampaignOutcomeJournalDocument document)
    {
        if (document == null)
            return;
        if (document.outcome == null)
            document.outcome = new CampaignProgressOutcome();
        if (document.outcome.unlockedSymbolIds == null)
            document.outcome.unlockedSymbolIds = new System.Collections.Generic.List<string>();
        if (document.outcome.unlockedMemoryIds == null)
            document.outcome.unlockedMemoryIds = new System.Collections.Generic.List<string>();
        if (document.outcome.claimedRewardIds == null)
            document.outcome.claimedRewardIds = new System.Collections.Generic.List<string>();
    }
}
