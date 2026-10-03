using System;
using System.Collections.Generic;

/// <summary>
/// The set of enemy types a level can actually introduce, derived from its wave table.
///
/// Derived rather than authored so a wave-table edit cannot leave the roster gate waiting on a
/// type the level no longer spawns — which would hold slot 3 closed forever and make the level
/// unwinnable. For the same reason the gate itself waits on <see cref="BuildRosterGateRoster"/>,
/// which drops the types that can only spawn once the gate is open.
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

    /// <summary>
    /// Opens the roster gate when this level's whole introducible roster has already been
    /// introduced. Returns true when the condition held, whether or not the token was already open
    /// (the registry's Open is idempotent).
    ///
    /// <para>
    /// <b>This must be evaluated at LEVEL START as well as after each introduction.</b> The two
    /// facts it joins live on different clocks: <c>EnemyIntroductionProgress</c> is cross-session,
    /// campaign-wide PlayerPrefs, while <c>SpawnGateRegistry</c> is reset per level attempt by
    /// <c>SpawnAssignmentCoordinator.ApplyLevel</c>. On a second run of Level 1 — a retry after a
    /// loss, or a replay — every type is already introduced, so no introduction ever plays, so a
    /// gate raised only from the end of an introduction never opens and the level's final slot is
    /// withheld forever: an unwinnable level. The same hole swallows a claim that is accepted but
    /// never begun (see <c>EnemyIntroductionBeat.TryClaim</c>'s stale-claim reset), which records
    /// the type as introduced with no playback to re-evaluate the gate.
    /// </para>
    ///
    /// <para>
    /// Shared by both call sites rather than duplicated so the level-start and post-introduction
    /// evaluations can never drift into disagreeing about what "roster met" means.
    /// </para>
    /// </summary>
    public static bool TryOpenRosterGate(
        LevelConfigSO config, Func<EnemyDataSO, bool> hasBeenIntroduced, Action<string> openGate)
    {
        if (config == null || openGate == null)
            return false;

        if (!AllIntroduced(BuildRosterGateRoster(config), hasBeenIntroduced))
            return false;

        openGate(SpawnGateRegistry.Level1RosterMet);
        return true;
    }

    /// <summary>
    /// The roster the <see cref="SpawnGateRegistry.Level1RosterMet"/> gate actually waits on: the
    /// introducible roster minus every type whose assigned symbol sits in a slot that gate
    /// withholds.
    ///
    /// <para>
    /// <b>The deadlock this closes.</b> On Level 1 the enemy is chosen BY the symbol
    /// (<c>SpawnAssignmentCoordinator.ResolveEnemyData</c>): Mantsa embodies MA and nothing else.
    /// Slot 3 is MA, gated on this token, and the director keeps a gated symbol out of filler as
    /// well as out of the needed slot — so Mantsa never spawns while the gate is shut. A gate that
    /// waits on Mantsa's introduction therefore waits on a spawn it is itself preventing: MA never
    /// appears, AMA can never be restored, and the level cannot be completed. The types that can
    /// only be met behind the gate are exactly the ones the gate must not require.
    /// </para>
    /// </summary>
    public static List<EnemyDataSO> BuildRosterGateRoster(LevelConfigSO config)
    {
        List<EnemyDataSO> roster = BuildIntroducibleRoster(config);
        HashSet<string> gatedSymbols = CollectSymbolsGatedBy(config, SpawnGateRegistry.Level1RosterMet);
        if (gatedSymbols.Count == 0)
            return roster;

        roster.RemoveAll(data =>
            data.assignedCharacter != null
            && !string.IsNullOrEmpty(data.assignedCharacter.stableId)
            && gatedSymbols.Contains(data.assignedCharacter.stableId));
        return roster;
    }

    /// <summary>
    /// The stable IDs of every symbol in a slot gated on <paramref name="gateToken"/>. Flattens the
    /// focus words exactly as <c>SpawnAssignmentCoordinator.BuildSlots</c> does — every word's
    /// decomposition in order, skipping null or ID-less symbols — so the slot indices the policy's
    /// gates name resolve to the same symbols the director withholds.
    /// </summary>
    public static HashSet<string> CollectSymbolsGatedBy(LevelConfigSO config, string gateToken)
    {
        var gated = new HashSet<string>();
        if (config?.focusWords == null || string.IsNullOrEmpty(gateToken))
            return gated;

        SpawnAssignmentPolicy policy = config.spawnAssignmentPolicy;
        if (policy == null)
            return gated;

        int flatIndex = 0;
        for (int wordIndex = 0; wordIndex < config.focusWords.Count; wordIndex++)
        {
            FocusWordDefinition word = config.focusWords[wordIndex];
            if (word?.decomposition == null)
                continue;

            for (int slotIndex = 0; slotIndex < word.decomposition.Count; slotIndex++)
            {
                SymbolValueReference reference = word.decomposition[slotIndex];
                if (reference?.symbol == null || string.IsNullOrEmpty(reference.symbol.stableId))
                    continue;

                if (policy.GateTokenForSlot(flatIndex) == gateToken)
                    gated.Add(reference.symbol.stableId);

                flatIndex++;
            }
        }

        return gated;
    }
}
