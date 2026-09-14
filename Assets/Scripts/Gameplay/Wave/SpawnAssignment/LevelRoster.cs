using System;
using System.Collections.Generic;

/// <summary>
/// The set of enemy types a level can actually introduce, derived from its wave table.
///
/// Derived rather than authored so a wave-table edit cannot leave the roster gate waiting on a
/// type the level no longer spawns — which would hold slot 3 closed forever and make the level
/// unwinnable.
///
/// The filters mirror the data-level half of EnemyIntroductionBeat.IsIntroducibleSpawn. Its
/// remaining checks (IsBoss, an introduction already playing, drawing input not yet accepted) are
/// runtime state with no data equivalent; bosses are not listed in wave enemyTypes.
/// </summary>
public static class LevelRoster
{
    public static List<EnemyDataSO> BuildIntroducibleRoster(LevelConfigSO config)
    {
        var roster = new List<EnemyDataSO>();
        if (config?.waves == null)
            return roster;

        for (int w = 0; w < config.waves.Count; w++)
        {
            List<EnemyDataSO> types = config.waves[w]?.enemyTypes;
            if (types == null)
                continue;

            for (int i = 0; i < types.Count; i++)
            {
                EnemyDataSO data = types[i];
                if (data == null || data.isDecoy || data.suppressDiscovery)
                    continue;
                if (string.IsNullOrWhiteSpace(data.displayName))
                    continue;
                if (roster.Contains(data))
                    continue;

                roster.Add(data);
            }
        }

        return roster;
    }

    public static bool AllIntroduced(
        IReadOnlyList<EnemyDataSO> roster, Func<EnemyDataSO, bool> hasBeenIntroduced)
    {
        // An empty roster means a config problem, not "everything met". Reporting true would
        // ungate the final slot on a broken level rather than surfacing the break.
        if (roster == null || roster.Count == 0 || hasBeenIntroduced == null)
            return false;

        for (int i = 0; i < roster.Count; i++)
        {
            if (!hasBeenIntroduced(roster[i]))
                return false;
        }

        return true;
    }
}
