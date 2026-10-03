using System.Collections.Generic;

/// <summary>
/// SALIN-253. The one place that answers "has the player just finished an era?" and "which
/// era comes next?".
///
/// PURE AND STATIC BY DESIGN. No MonoBehaviour, no SaveManager, no scene, no
/// <see cref="ContentIdentity"/> string parsing. The whole of the era-boundary rule is
/// therefore exercised from Edit Mode with in-memory ScriptableObject fixtures, which is
/// what makes the one genuinely dangerous case below testable at all.
///
/// THE AUTHORED COUNT IS READ, NEVER RECOMPUTED AS ((n - 1) % 5 + 1).
/// <see cref="CampaignLevelLabel"/> (:27-30) and LevelSelectUI.cs:306-309 both already forbid
/// the modulo form, for the same two reasons that apply here:
///   (1) it bypasses the levelNumber <-> eraLocalOrder invariant that
///       CampaignConfigValidator.cs:389 enforces, so a mis-authored config would be papered
///       over rather than caught; and
///   (2) it silently produces the wrong answer the moment an era is not exactly five levels.
/// <see cref="IsEraFinalLevel"/> compares the authored <c>eraLocalOrder</c> against the
/// authored <c>era.levels.Count</c>, which is the shape already shipping at
/// LevelFlowController.cs:1445. EraBoundaryTests pins this with a synthetic FOUR-level era:
/// that fixture is the only thing in the suite that discriminates this implementation from a
/// "% 5" one, because on the real 5/5/5 campaign the two agree on every single level.
///
/// ORDERING IS POSITIONAL, NOT BY EraConfigSO.order. <see cref="NextEra"/> and
/// <see cref="IndexOfEra"/> both walk <c>campaign.eras</c> in list order with nulls
/// compacted out, because that is exactly what LevelSelectUI.ResolveEras() (:211-238) hands
/// to LevelSelectUI.ShowEra(int). An index derived from any other ordering would open Level
/// Select on the wrong era while every unit test that sorted by <c>order</c> stayed green.
/// <see cref="MemoryArchiveModel.Build"/> deliberately sorts by <c>order</c> instead — it is
/// answering a different question (what the player reads, top to bottom) and the two must not
/// be conflated.
///
/// NOTHING HERE UNLOCKS ANYTHING. The next era's first level is already unlocked by
/// CampaignOutcomeCoordinator.cs:244-252, which advances a flat 15-entry list by index + 1
/// and so crosses era boundaries implicitly (docs/audit/AUDIT.md:116 records UF-34 as PARTIAL
/// for precisely that reason). This class presents that fact and routes to it; it must never
/// re-implement it.
/// </summary>
public static class EraBoundary
{
    /// <summary>
    /// True when <paramref name="level"/> is the last level of <paramref name="era"/> as
    /// authored — i.e. the completion that should open the era completion screen.
    ///
    /// Every null or incoherent input is false rather than an exception: this is read on the
    /// results path, where throwing would take the Results screen down with it, and "we could
    /// not tell" must degrade to today's behaviour (no era screen), never to a wrong one.
    /// </summary>
    public static bool IsEraFinalLevel(EraConfigSO era, LevelConfigSO level)
    {
        if (era == null || level == null || era.levels == null)
            return false;

        int authoredCount = CountNonNull(era.levels);
        if (authoredCount < 1)
            return false;

        // eraLocalOrder is 1-based. A 0 means "not authored on this config" and must not be
        // allowed to match a zero-length era.
        return level.eraLocalOrder >= 1 && level.eraLocalOrder == authoredCount;
    }

    /// <summary>
    /// The era that follows <paramref name="era"/> in the campaign's authored order, or null
    /// when <paramref name="era"/> is the final era (or is not in this campaign at all).
    ///
    /// A null return is the "Pamana is over" case and is a normal result, not a failure: the
    /// era completion screen shows its close control instead of "Enter Next Era".
    /// </summary>
    public static EraConfigSO NextEra(CampaignConfigSO campaign, EraConfigSO era)
    {
        List<EraConfigSO> eras = CompactEras(campaign);
        int index = IndexIn(eras, era);
        if (index < 0 || index + 1 >= eras.Count)
            return null;

        return eras[index + 1];
    }

    /// <summary>
    /// The position of <paramref name="era"/> in the campaign's compacted era list — the
    /// index LevelSelectUI.ShowEra(int) expects — or -1 when it is unknown.
    ///
    /// -1 is a meaningful result. The handoff treats it as "do not steer Level Select", which
    /// leaves the screen opening on era 0 exactly as it does today.
    /// </summary>
    public static int IndexOfEra(CampaignConfigSO campaign, EraConfigSO era) =>
        IndexIn(CompactEras(campaign), era);

    private static int IndexIn(List<EraConfigSO> eras, EraConfigSO era)
    {
        if (era == null)
            return -1;

        for (int i = 0; i < eras.Count; i++)
            if (eras[i] == era)
                return i;

        return -1;
    }

    private static List<EraConfigSO> CompactEras(CampaignConfigSO campaign)
    {
        var result = new List<EraConfigSO>();
        if (campaign == null || campaign.eras == null)
            return result;

        for (int i = 0; i < campaign.eras.Count; i++)
            if (campaign.eras[i] != null)
                result.Add(campaign.eras[i]);

        return result;
    }

    private static int CountNonNull(List<LevelConfigSO> levels)
    {
        int count = 0;
        for (int i = 0; i < levels.Count; i++)
            if (levels[i] != null)
                count++;
        return count;
    }
}
