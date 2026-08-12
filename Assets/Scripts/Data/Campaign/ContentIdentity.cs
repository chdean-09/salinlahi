using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class ContentIdentity
{
    public const string RevisedCampaignId = "campaign.revised-v1";
    public const int RevisedLevelsPerEra = 5;
    public const int RevisedFocusWordsPerLevel = 2;
    public const int RevisedSpokenValueCount = 18;
    public const string RevisedDaraSymbolId = "symbol.dara";
    public const string RevisedDaSpokenValueId = "value.da";
    public const string RevisedRaSpokenValueId = "value.ra";

    public const string ApprovedWorkbookSha256 =
        "33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83";

    public static readonly IReadOnlyList<string> RevisedEraIds =
        new[] { "era.ugat", "era.ugnayan", "era.pamana" };

    public static readonly IReadOnlyList<string> RevisedSymbolIds = new[]
    {
        "symbol.a", "symbol.ei", "symbol.ba", "symbol.ma", "symbol.na",
        "symbol.ta", "symbol.ou", "symbol.ka", "symbol.ga", "symbol.sa",
        "symbol.wa", "symbol.ya", RevisedDaraSymbolId, "symbol.ha", "symbol.la",
        "symbol.nga", "symbol.pa",
    };

    public static readonly IReadOnlyList<string> RevisedLevelIds = CreateLevelIds();
    public static readonly string RevisedFinaleLevelId =
        RevisedLevelIds[RevisedLevelIds.Count - 1];
    public static readonly string RevisedFinaleSymbolId =
        RevisedSymbolIds[RevisedSymbolIds.Count - 1];
    public static readonly string RevisedFinaleSpokenValueId =
        "value." + RevisedFinaleSymbolId.Substring("symbol.".Length);

    private static readonly Regex CanonicalIdPattern = new Regex(
        "^[a-z0-9]+(?:[.-][a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsCanonical(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.Trim() == value &&
               CanonicalIdPattern.IsMatch(value);
    }

    public static string GetEraIdForLevel(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        for (int i = 0; i < RevisedEraIds.Count; i++)
        {
            string eraId = RevisedEraIds[i];
            if (levelId.StartsWith("level." + eraId.Substring("era.".Length) + ".", System.StringComparison.Ordinal))
                return eraId;
        }

        return null;
    }

    public static string GetLevelId(string eraId, int localOrder)
    {
        if (string.IsNullOrEmpty(eraId) || localOrder < 1 || localOrder > RevisedLevelsPerEra)
            return null;

        return "level." + eraId.Substring("era.".Length) + "." + localOrder.ToString("00");
    }

    private static IReadOnlyList<string> CreateLevelIds()
    {
        var ids = new List<string>(RevisedEraIds.Count * RevisedLevelsPerEra);
        for (int eraIndex = 0; eraIndex < RevisedEraIds.Count; eraIndex++)
        {
            string eraSuffix = RevisedEraIds[eraIndex].Substring("era.".Length);
            for (int localOrder = 1; localOrder <= RevisedLevelsPerEra; localOrder++)
                ids.Add("level." + eraSuffix + "." + localOrder.ToString("00"));
        }

        return ids;
    }
}
