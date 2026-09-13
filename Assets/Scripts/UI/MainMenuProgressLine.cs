/// <summary>
/// SALIN-256 (spec UF-03/UF-06). Turns the player's campaign position into the one line the
/// main menu shows: "Ugat Level 3  ·  13%".
///
/// PURE AND STATIC ON PURPOSE. No MonoBehaviour, no scene, no singleton reads inside — the
/// caller passes the campaign in. That is what makes the whole rendering rule reachable from
/// EditMode, where MainMenuProgressLineTests pins it. The MonoBehaviour half (finding the
/// menu transform and building a label) is the part that cannot be unit-tested, so it is kept
/// as small as possible and lives in MainMenuUI.
///
/// ERA RESOLUTION IS DELIBERATELY NOT SHARED. Three private FindEraForLevel-style helpers
/// already exist — LevelSelectUI.cs:286, LevelFlowController.cs:1449 and the era walk in
/// MemoryArchiveModel.cs:127-175 — and all three are owned by other in-flight tickets.
/// TryResolveEra below reads the campaign graph directly rather than reaching into any of
/// them, so this ticket converges on none of their files.
///
/// THE BINDING RULES COME FROM CampaignLevelLabel.cs:18-30 AND ARE NOT NEGOTIABLE HERE:
/// the era name is read from EraConfigSO.eraName, never from LevelConfigSO.chapterName (which
/// has zero production readers), and eraLocalOrder is READ from the authored
/// LevelConfigSO.eraLocalOrder, never recomputed as ((n - 1) % 5 + 1) — recomputing would
/// bypass the invariant CampaignConfigValidator.cs:389 enforces.
///
/// WHY THE PERCENTAGE STAYS LOW ALL DEMO — THIS IS CORRECT, NOT A DEFECT. Decision D-015
/// scopes the demo to Ugat levels 1-5 and levels 6-15 refuse completion by design, while the
/// denominator is the full 15-level campaign (ProgressManager.TotalLevels). So the line caps
/// at 5/15 = 33% for the entire demo. The acceptance criterion's own arithmetic fixes the
/// denominator at 15: at "Ugat Level 3" the player has finished levels 1 and 2, and
/// 2/15 = 13.3% -> 13% is the "13%" the criterion asks for. A demo-only denominator would
/// read 2/5 = 40% and contradict the criterion.
/// </summary>
public static class MainMenuProgressLine
{
    /// <summary>
    /// Finds the era that owns <paramref name="globalLevelNumber"/> and reports its display
    /// name together with the level's AUTHORED order inside that era.
    ///
    /// Null-safe at every hop — campaign, eras, the era list's individual elements, levels,
    /// and the level list's individual elements — because a partially authored campaign is a
    /// real state in this project; MemoryArchiveModel.cs:127-135 filters the same way.
    /// </summary>
    /// <returns>True when the level was found in the campaign graph.</returns>
    public static bool TryResolveEra(
        CampaignConfigSO campaign, int globalLevelNumber, out string eraName, out int eraLocalOrder)
    {
        eraName = null;
        eraLocalOrder = 0;

        if (campaign == null || campaign.eras == null)
            return false;

        for (int eraIndex = 0; eraIndex < campaign.eras.Count; eraIndex++)
        {
            EraConfigSO era = campaign.eras[eraIndex];
            if (era == null || era.levels == null)
                continue;

            for (int levelIndex = 0; levelIndex < era.levels.Count; levelIndex++)
            {
                LevelConfigSO level = era.levels[levelIndex];
                if (level == null || level.levelNumber != globalLevelNumber)
                    continue;

                eraName = era.eraName;
                // Read, never recomputed. See the class header.
                eraLocalOrder = level.eraLocalOrder;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whole-number completion percentage, floored.
    ///
    /// INFERENCE — the acceptance criterion does not settle the rounding rule. Its single data
    /// point (2/15 = 13.3% -> 13%) is satisfied by both integer floor and round-half-up. Floor
    /// is chosen because it can never OVERSTATE progress: round-half-up would print "100%"
    /// from 14.5/15 upward, telling a player the journey is finished while a level remains.
    /// Floor prints 100% only at a genuine 15/15. Flagged for the SALIN-291 copy review; if
    /// product rules otherwise, this method is the only thing that changes.
    ///
    /// A non-positive total returns 0 rather than dividing — a campaign with no levels is a
    /// load failure, not a finished journey.
    /// </summary>
    public static int CompletionPercent(int completed, int total)
    {
        if (total <= 0)
            return 0;

        if (completed <= 0)
            return 0;

        if (completed >= total)
            return 100;

        return (completed * 100) / total;
    }

    /// <summary>
    /// The finished main-menu progress string, or
    /// <see cref="MainMenuProgressCopy.ProgressUnavailable"/> when the current level cannot be
    /// named at all.
    ///
    /// Staying silent is deliberate: a bare "· 13%" with no level beside it reads as a bug to
    /// a player, and CampaignLevelLabel.cs:55-58 already establishes empty-means-silent as the
    /// house contract for an unnameable level.
    /// </summary>
    public static string Format(
        CampaignConfigSO campaign, int currentLevelNumber, int completedCount, int totalLevels)
    {
        TryResolveEra(campaign, currentLevelNumber, out string eraName, out int eraLocalOrder);

        // The three-argument overload owns the fallback policy: era-relative when the era is
        // known, the global number only when it is not. Duplicating that choice here is
        // exactly how a global 1-15 leaks back onto a screen (CampaignLevelLabel.cs:81-83).
        string levelLabel = CampaignLevelLabel.Format(eraName, eraLocalOrder, currentLevelNumber);
        if (string.IsNullOrEmpty(levelLabel))
            return MainMenuProgressCopy.ProgressUnavailable;

        int percent = CompletionPercent(completedCount, totalLevels);
        return string.Format(MainMenuProgressCopy.ProgressFormat, levelLabel, percent);
    }
}
