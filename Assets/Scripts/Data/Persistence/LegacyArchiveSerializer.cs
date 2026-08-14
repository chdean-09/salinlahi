using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class LegacyArchiveSerializer
{
    public static string Serialize(LegacyProgressArchive archive)
    {
        LegacyProgressArchive clone = DeepClone(archive);
        clone.integritySha256 = string.Empty;
        string unsignedJson = JsonUtility.ToJson(clone, false);
        clone.integritySha256 = ComputeSha256(unsignedJson);
        return JsonUtility.ToJson(clone, false);
    }

    public static LegacyArchiveParseResult TryDeserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return LegacyArchiveParseResult.Failed("missing");
        try
        {
            LegacyProgressArchive archive = JsonUtility.FromJson<LegacyProgressArchive>(json);
            if (archive == null || archive.records == null || string.IsNullOrEmpty(archive.integritySha256))
                return LegacyArchiveParseResult.Failed("malformed");
            string expected = archive.integritySha256.Trim().ToLowerInvariant();
            archive.integritySha256 = string.Empty;
            string actual = ComputeSha256(JsonUtility.ToJson(archive, false));
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                return LegacyArchiveParseResult.Failed("checksum");
            archive.integritySha256 = expected;
            return LegacyArchiveParseResult.Succeeded(archive, expected);
        }
        catch (Exception exception)
        {
            return LegacyArchiveParseResult.Failed(exception.Message);
        }
    }

    public static LegacyProgressArchive DeepClone(LegacyProgressArchive archive)
    {
        return JsonUtility.FromJson<LegacyProgressArchive>(JsonUtility.ToJson(archive, false));
    }

    private static string ComputeSha256(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }
}

public sealed class LegacyArchiveParseResult
{
    public bool Success { get; private set; }
    public LegacyProgressArchive Archive { get; private set; }
    public string IntegritySha256 { get; private set; }
    public string ErrorMessage { get; private set; }

    public static LegacyArchiveParseResult Succeeded(LegacyProgressArchive archive, string checksum)
    {
        return new LegacyArchiveParseResult { Success = true, Archive = archive, IntegritySha256 = checksum };
    }

    public static LegacyArchiveParseResult Failed(string message)
    {
        return new LegacyArchiveParseResult { ErrorMessage = message };
    }
}
