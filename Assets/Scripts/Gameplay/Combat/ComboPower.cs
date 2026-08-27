/// <summary>
/// The power a five-correct-trace streak grants, chosen by the level's challenge tier (SALIN-182).
/// </summary>
public enum ComboPower
{
    /// <summary>Tier 1, and any tier outside the authored range. Focus Mode only.</summary>
    None,

    /// <summary>Tier 2. One bonus combat hit, with no additional language progress.</summary>
    RapidShot,

    /// <summary>Tiers 3-4. The active target plus one aligned target, objective advanced once.</summary>
    PiercingArrow,

    /// <summary>Tier 5. One nonstacking shield that blocks the next Scroll heart loss.</summary>
    Shield,
}

/// <summary>
/// Maps a challenge tier to its combo power. Pure and total: every int maps to something, so an
/// unauthored or out-of-range tier degrades to <see cref="ComboPower.None"/> rather than throwing
/// mid-combat.
/// </summary>
/// <remarks>
/// The tier→power assignment follows SALIN-182's completion criteria literally. The criteria fix the
/// mapping but not the magnitudes, which is why every quantity lives in GameConfigSO instead: those
/// are balance values and still need design sign-off.
/// </remarks>
public static class ComboPowerResolver
{
    public static ComboPower ForTier(int tier)
    {
        switch (tier)
        {
            case 2: return ComboPower.RapidShot;
            case 3:
            case 4: return ComboPower.PiercingArrow;
            case 5: return ComboPower.Shield;
            default: return ComboPower.None;
        }
    }

    /// <summary>
    /// The tier of the level being played, or 0 when no level config is active — which resolves to
    /// <see cref="ComboPower.None"/>, so combat outside a configured level grants nothing.
    /// </summary>
    public static int CurrentTier()
    {
        LevelConfigSO level = GameManager.CurrentLevelConfig;
        if (level == null || level.challengePolicy == null)
            return 0;

        return level.challengePolicy.tier;
    }
}
