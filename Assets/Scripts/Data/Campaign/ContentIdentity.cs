using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class ContentIdentity
{
    public const string RevisedCampaignId = "campaign.revised-v1";
    public const int RevisedLevelsPerEra = 5;
    public const int RevisedFocusWordsPerLevel = 2;
    public const int RevisedSpokenValueCount = 22;
    public const string RevisedDaSymbolId = "symbol.da";
    public const string RevisedRaSymbolId = "symbol.ra";
    public const string RevisedDaSpokenValueId = "value.da";
    public const string RevisedRaSpokenValueId = "value.ra";

    // SALIN-217: PA is no longer the finale symbol (ruling Q1 moved that to YA), but
    // ValidatePaInstructionOrder still has to mean PA. It reaches PA through IsPa, which used to
    // read RevisedFinaleSymbolId back when the two happened to coincide. Naming PA explicitly keeps
    // that rule pinned to PA instead of silently following the finale wherever it goes next.
    public const string RevisedPaSymbolId = "symbol.pa";
    public const string RevisedPaSpokenValueId = "value.pa";

    public const string ApprovedWorkbookSha256 =
        "33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83";

    public static readonly IReadOnlyList<string> RevisedEraIds =
        new[] { "era.ugat", "era.ugnayan", "era.pamana" };

    public static readonly IReadOnlyList<string> RevisedSymbolIds = new[]
    {
        "symbol.a", "symbol.ei", "symbol.ba", "symbol.ma", "symbol.na",
        "symbol.ta", "symbol.ou", "symbol.ka", "symbol.ga", "symbol.sa",
        "symbol.wa", "symbol.ya", RevisedDaSymbolId, "symbol.ha", "symbol.la",
        "symbol.nga", RevisedRaSymbolId, RevisedPaSymbolId,
    };

    public static readonly IReadOnlyList<string> RevisedLevelIds = CreateLevelIds();
    public static readonly string RevisedFinaleLevelId =
        RevisedLevelIds[RevisedLevelIds.Count - 1];
    // SALIN-217, ruling Q1 (routed here by plan-review R9): YA closes the campaign, not PA.
    //
    // This was RevisedSymbolIds[Count - 1] — the finale was whatever happened to be last in the
    // array. Do not restore that. Plan-review R8 exists only because of it: adding any symbol at
    // the end silently moved the finale, so R8 had to forbid appending RA last. Naming the finale
    // makes that whole class of accident impossible, which is why symbol.ra can now sit before
    // symbol.pa (AC-13) while the finale stays YA.
    public const string RevisedFinaleSymbolId = "symbol.ya";
    public static readonly string RevisedFinaleSpokenValueId =
        "value." + RevisedFinaleSymbolId.Substring("symbol.".Length);

    /// <summary>
    /// SALIN-221 (ruling Q2): the spoken values approved for each visual symbol that carries more
    /// than its own primary value. E/I and O/U keep their combined citation value as the primary
    /// entry — it is what the learning card, the cumulative pools and every requirement resolve —
    /// and add the per-word-context values a focus-word decomposition selects. Every other symbol
    /// is covered by the default rule in <see cref="IsApprovedSpokenValue"/>: "value." + its symbol
    /// suffix.
    ///
    /// D-025: DA had an entry here for exactly one reason — its id read "symbol.da", so the
    /// default rule computed "value.dara", which no symbol emits. Renaming the id to symbol.da
    /// makes the default rule derive value.da unaided, so the entry is DELETED rather than
    /// updated. Neither DA nor RA needs one now, and the hazard the old note described — a wrong
    /// edit here letting IsApprovedSpokenValue contradict ValidateSymbolCatalog with no test
    /// catching it — no longer has anywhere to live.
    /// The map lives here rather than in the validator so that changing which values a symbol may
    /// carry stays a data edit in one place.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>>
        ApprovedSpokenValueIds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            { "symbol.ei", new[] { "value.ei", "value.e", "value.i" } },
            { "symbol.ou", new[] { "value.ou", "value.o", "value.u" } },
        };

    /// <summary>
    /// Whether <paramref name="spokenValueId"/> is an approved value for
    /// <paramref name="symbolId"/>. Symbols absent from <see cref="ApprovedSpokenValueIds"/> carry
    /// exactly their primary value.
    /// </summary>
    public static bool IsApprovedSpokenValue(string symbolId, string spokenValueId)
    {
        if (string.IsNullOrEmpty(symbolId) || string.IsNullOrEmpty(spokenValueId) ||
            !symbolId.StartsWith("symbol.", StringComparison.Ordinal))
            return false;

        if (ApprovedSpokenValueIds.TryGetValue(symbolId, out IReadOnlyList<string> approved))
        {
            for (int index = 0; index < approved.Count; index++)
            {
                if (string.Equals(approved[index], spokenValueId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        return string.Equals(
            "value." + symbolId.Substring("symbol.".Length),
            spokenValueId,
            StringComparison.Ordinal);
    }

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
