using System.Collections.Generic;

public static class BaybayinIdCanonicalizer
{
    // Canonical equivalence groups:
    // I-E, O-U, PA-FA, BA-VA, SA-ZA.
    //
    // DA-RA was a sixth group until SALIN-217. Ruling Q2 (2026-09-11), reaffirmed by OQ-6
    // (2026-09-12), makes DA and RA two taught identities rather than two readings of one glyph, so
    // RA canonicalizes to itself and RA_template_01..05 load under their own key again. "DARA" is
    // kept as an alias of DA so ids saved while the fold was in force still resolve instead of
    // falling through Canonicalize's pass-through.
    //
    // What the fold was for, and what removing it costs: SALIN-212 folded RA into DA because every
    // consumer compares raw ids -- ActiveEnemyTracker.FindAllWithCharacter, the active-clue check in
    // CombatResolver, BossController.TryRouteDraw -- and nothing in the game carried RA, so an
    // unfolded "RA" matched nothing and scored a correct draw as a miss. That reason expires with
    // this ticket: symbol.ra is now a catalogue symbol in the Level 13+ pools.
    //
    // The measurement that justified the fold, preserved because it is one-directional: with the RA
    // key removed, all five RA templates matched DA and nothing else, scoring 0.756-0.839 against a
    // 0.60 confidence floor (commit 935f2392). Nobody has measured the reverse -- whether DA draws
    // now leak into RA with 12 DA templates competing against 5 RA templates in live $P. If DA
    // starts resolving as RA, that is the regression to look for, and template curation belongs to
    // a separate ticket rather than to a tweak here.
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
        { "RA", "RA" },
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
        // The shipped art file is still named DA-RA, so both identities offer it as a candidate.
        if (canonical == "DA") AddUnique(candidates, "DA-RA");
        if (canonical == "RA") AddUnique(candidates, "DA-RA");

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