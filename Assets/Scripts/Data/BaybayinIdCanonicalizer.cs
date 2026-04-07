using System.Collections.Generic;

public static class BaybayinIdCanonicalizer
{
    // Canonical equivalence groups:
    // I-E, O-U, PA-FA, BA-VA, SA-ZA.
    private static readonly Dictionary<string, string> s_aliasToCanonical = new Dictionary<string, string>
    {
        { "E", "EI" },
        { "I", "EI" },
        { "EI", "EI" },

        { "O", "OU" },
        { "U", "OU" },
        { "OU", "OU" },

        { "PA", "PA" },
        { "FA", "PA" },
        { "PAFA", "PA" },

        { "BA", "BA" },
        { "VA", "BA" },
        { "BAVA", "BA" },

        { "SA", "SA" },
        { "ZA", "SA" },
        { "SAZA", "SA" },
    };

    public static string Canonicalize(string rawID)
    {
        string normalized = Normalize(rawID);
        if (string.IsNullOrEmpty(normalized)) return string.Empty;

        if (s_aliasToCanonical.TryGetValue(normalized, out string canonical))
            return canonical;

        return normalized;
    }

    public static List<string> GetSpriteResourceCandidates(string rawID)
    {
        var candidates = new List<string>();
        if (string.IsNullOrWhiteSpace(rawID))
            return candidates;

        string uppercaseRaw = rawID.Trim().ToUpperInvariant();
        AddUnique(candidates, uppercaseRaw);
        AddUnique(candidates, uppercaseRaw.Replace('_', '-'));

        string canonical = Canonicalize(rawID);
        AddUnique(candidates, canonical);

        if (canonical == "EI") AddUnique(candidates, "E-I");
        if (canonical == "OU") AddUnique(candidates, "O-U");
        if (canonical == "PA") AddUnique(candidates, "PA-FA");
        if (canonical == "BA") AddUnique(candidates, "BA-VA");
        if (canonical == "SA") AddUnique(candidates, "SA-ZA");

        return candidates;
    }

    private static string Normalize(string rawID)
    {
        if (string.IsNullOrWhiteSpace(rawID))
            return string.Empty;

        string normalized = rawID.Trim().ToUpperInvariant();
        normalized = normalized.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        return normalized;
    }

    private static void AddUnique(List<string> candidates, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!candidates.Contains(value))
            candidates.Add(value);
    }
}