using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Which level a corruption type makes its first appearance on, derived from the campaign's wave
/// tables in campaign order.
///
/// <para>
/// <b>Why this exists.</b> An enemy's introduction card is a campaign-wide one-shot
/// (<see cref="EnemyIntroductionProgress"/>), which is right for a type the player met two levels
/// ago and wrong for the level that is teaching it: a retry of Level 1 replayed Juan's whole
/// pre-combat onboarding and then met Abo and Nawalang Mukha in silence, because a previous
/// attempt had spent their cards. The rule this file answers is "a level always introduces the
/// types that debut on it" — every attempt, regardless of the campaign record — and a type's debut
/// level is not authored anywhere, so it is derived here from the same wave tables the roster gate
/// reads.
/// </para>
///
/// <para>
/// <b>Fails closed.</b> No campaign, a level the campaign does not list, or a type no level spawns
/// all answer false, which leaves the campaign-wide one-shot in charge — the stricter behaviour.
/// </para>
/// </summary>
public static class EnemyDebutLookup
{
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    /// <summary>
    /// Test seam. When set, <see cref="DebutsOnCurrentCampaignLevel"/> reads this campaign instead
    /// of the save manager's. Tests must clear it in teardown.
    /// </summary>
    public static CampaignConfigSO CampaignOverrideForTests;
#endif

    /// <summary>
    /// Whether <paramref name="data"/> debuts on <paramref name="level"/> in the active campaign.
    /// </summary>
    public static bool DebutsOnCurrentCampaignLevel(LevelConfigSO level, EnemyDataSO data)
    {
        return DebutsOn(ResolveCampaign(), level, data);
    }

    /// <summary>
    /// Whether <paramref name="data"/> debuts on <paramref name="level"/>: the first level in
    /// campaign order whose introducible wave roster contains the type is <paramref name="level"/>.
    /// Pure, so it is an EditMode test.
    /// </summary>
    public static bool DebutsOn(CampaignConfigSO campaign, LevelConfigSO level, EnemyDataSO data)
    {
        if (campaign == null || level == null || data == null)
            return false;

        LevelConfigSO debut = FindDebutLevel(campaign, data);
        return debut != null && IsSameLevel(debut, level);
    }

    /// <summary>The first level in campaign order that spawns the type, or null.</summary>
    public static LevelConfigSO FindDebutLevel(CampaignConfigSO campaign, EnemyDataSO data)
    {
        if (campaign == null || data == null)
            return null;

        foreach (LevelConfigSO level in FlattenInCampaignOrder(campaign))
        {
            if (RosterContains(LevelRoster.BuildIntroducibleRoster(level), data))
                return level;
        }

        return null;
    }

    /// <summary>
    /// Every non-null level of the campaign, eras by their authored <c>order</c> and levels by
    /// <c>levelNumber</c>, both stably so ties keep list order. Mirrors the archive's traversal
    /// (<c>MemoryArchiveModel.Build</c>) so "first level" means the same thing in both places.
    /// </summary>
    public static List<LevelConfigSO> FlattenInCampaignOrder(CampaignConfigSO campaign)
    {
        var result = new List<LevelConfigSO>();
        if (campaign?.eras == null)
            return result;

        IEnumerable<EraConfigSO> eras = campaign.eras.Where(e => e != null).OrderBy(e => e.order);
        foreach (EraConfigSO era in eras)
        {
            if (era.levels == null)
                continue;

            result.AddRange(era.levels.Where(l => l != null).OrderBy(l => l.levelNumber));
        }

        return result;
    }

    private static CampaignConfigSO ResolveCampaign()
    {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        if (CampaignOverrideForTests != null)
            return CampaignOverrideForTests;
#endif
        return SaveManager.Instance != null ? SaveManager.Instance.Campaign : null;
    }

    private static bool IsSameLevel(LevelConfigSO a, LevelConfigSO b)
    {
        if (a == b)
            return true;

        return !string.IsNullOrEmpty(a.stableId)
            && string.Equals(a.stableId, b.stableId, System.StringComparison.Ordinal);
    }

    /// <summary>Reference or case-insensitive <c>enemyID</c>, the identity rule the lesson lookup uses.</summary>
    private static bool RosterContains(List<EnemyDataSO> roster, EnemyDataSO enemy)
    {
        for (int i = 0; i < roster.Count; i++)
        {
            EnemyDataSO candidate = roster[i];
            if (candidate == enemy)
                return true;

            if (!string.IsNullOrEmpty(candidate.enemyID)
                && string.Equals(candidate.enemyID, enemy.enemyID,
                    System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
