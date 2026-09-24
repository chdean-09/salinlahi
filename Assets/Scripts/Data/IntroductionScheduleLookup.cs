using UnityEngine;

/// <summary>
/// Reads the campaign's <see cref="IntroductionScheduleSO"/> and answers whether a level may
/// introduce a type.
/// </summary>
/// <remarks>
/// <b>Two different absences, two different answers.</b> A campaign with no schedule assigned is
/// not configured yet, and every level behaves as it did before this existed — otherwise adding the
/// field would have silenced the introductions in every synthetic fixture and in any scene played
/// outside the campaign. A campaign WITH a schedule that simply does not name a type for a level is
/// a decision, and it is obeyed: no card.
/// </remarks>
public static class IntroductionScheduleLookup
{
    /// <summary>
    /// Test seam. When set, the schedule is read from here instead of the campaign. Tests must
    /// clear it in teardown. Mirrors <see cref="EnemyDebutLookup.CampaignOverrideForTests"/>.
    /// </summary>
    internal static IntroductionScheduleSO ScheduleOverrideForTests { get; set; }

    /// <summary>
    /// Whether <paramref name="level"/> may introduce <paramref name="data"/>: true when no
    /// schedule is configured, otherwise exactly what the schedule says.
    /// </summary>
    public static bool AllowsIntroduction(LevelConfigSO level, EnemyDataSO data)
    {
        IntroductionScheduleSO schedule = Resolve();
        if (schedule == null)
            return true;

        return schedule.Introduces(level, data);
    }

    /// <summary>The schedule in force, or null when none is configured.</summary>
    public static IntroductionScheduleSO Resolve()
    {
        if (ScheduleOverrideForTests != null)
            return ScheduleOverrideForTests;

        CampaignConfigSO campaign = EnemyDebutLookup.ResolveCampaignForLookup();
        return campaign != null ? campaign.introductionSchedule : null;
    }
}
