using System.Collections.Generic;

public static class BaybayinIdCanonicalizer
{
    // Canonical equivalence groups:
    // I-E, O-U, PA-FA, BA-VA, SA-ZA, DA-RA.
    //
    // DA-RA (SALIN-212) is the only group whose members BOTH have template files on disk, so it is
    // the only one that actually merges template sets: RA_template_01..05 now load under "DA"
    // alongside DA_template_01..12, giving one key with 17 variants. That is correct -- they are 17
    // recorded samples of one glyph. Measured with the project's own recognizer: with the RA key
    // removed, all five RA templates match DA and nothing else, scoring 0.756-0.839 against a 0.60
    // confidence floor. No other symbol competes.
    //
    // Without this group the recognizer could return "RA" for a correctly drawn glyph, and every
    // consumer compares raw ids -- ActiveEnemyTracker.FindAllWithCharacter, the active-clue check in
    // CombatResolver, and BossController.TryRouteDraw. No enemy, clue or boss requirement carries
    // RA, so that draw matched nothing and scored as a miss. The reading (da versus ra) comes from
    // level content via spokenValueId, never from recognition.
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

        { "DA", "DA" },
        { "RA", "DA" },
        { "DARA", "DA" },
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
        if (canonical == "DA") AddUnique(candidates, "DA-RA");

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