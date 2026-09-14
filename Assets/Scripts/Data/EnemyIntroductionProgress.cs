using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Campaign-wide, cross-session record of which enemy types have already had their introduction
/// card shown (<see cref="EnemyIntroductionBeat"/>). "First ever spawn" is the whole contract of
/// that beat, so it cannot be a per-level or per-session flag: Level 1 introduces four types, and
/// Levels 2+ must introduce only types the player has genuinely never met, including across a quit.
///
/// <para>
/// <b>Why this is not <see cref="EnemyDiscoveryProgress"/>.</b> That store answers a question of
/// exactly the same shape — "has this type ever been seen?" — persisted per <c>enemyID</c> through
/// the same keys. Reusing it was the first design and it is wrong, because it is <i>consumed</i>:
/// <c>EnemyDiscoveryOnboardingController</c> marks a type discovered when it shows the almanac
/// discovery overlay. Sharing one flag means whichever of the two surfaces fires first silently
/// suppresses the other, and which one that is depends on scene wiring. Two independent one-shots
/// need two independent flags.
/// </para>
///
/// <para>
/// <b>Storage.</b> A newline-joined set of normalized enemy IDs under a single PlayerPrefs key in
/// <c>ProgressManager</c>'s <c>salinlahi.tutorial.*</c> namespace, which is the shape every other
/// one-time tutorial flag there uses (<c>Level1FtueSeenKey</c>, the beat-index keys). The revised
/// save repository has no slot for this flag, so unlike
/// <see cref="EnemyDiscoveryProgress"/> there is no repository branch to take; the key stays the
/// single source of truth in every save mode.
/// </para>
///
/// <para>
/// <b>The session set is not a cache.</b> When the save system is in
/// <see cref="SaveManagerMode.RevisedBlocked"/> nothing may be written to disk, and a purely
/// persisted flag would then read false on every spawn — the card would re-fire for the same type
/// for the whole session, which is worse than losing the record across a quit. The in-memory set
/// holds the one-shot within the session regardless of whether the write landed.
/// </para>
/// </summary>
public static class EnemyIntroductionProgress
{
    /// <summary>
    /// PlayerPrefs key holding the newline-joined set of introduced enemy IDs. Namespaced to match
    /// <c>ProgressManager.Level1FtueSeenKey</c> and its siblings so a save-reset sweep that walks
    /// the tutorial namespace finds it.
    /// </summary>
    public const string IntroducedEnemyIDsKey = "salinlahi.tutorial.enemy_introductions_shown";

    /// <summary>
    /// Types introduced during this run, whether or not the write to disk was permitted. Static
    /// rather than instance state because the beat runner is per-scene and the record is not: a
    /// level reload must not re-introduce a type the player met two minutes ago.
    /// </summary>
    private static readonly HashSet<string> IntroducedThisSession = new();

    /// <summary>
    /// True when this type's introduction card has already been shown — earlier in this session, or
    /// in any previous one.
    /// </summary>
    public static bool HasBeenIntroduced(EnemyDataSO data)
    {
        string enemyID = EnemyDiscoveryProgress.NormalizeEnemyID(data);
        if (enemyID == null)
            return false;

        return IntroducedThisSession.Contains(enemyID) || LoadIntroducedIDs().Contains(enemyID);
    }

    /// <summary>
    /// Claims this type's introduction. Returns true exactly once per type per save, and false on
    /// every later call — so the caller can treat a true return as "this spawn owns the card" with
    /// no second check and no race between two enemies initialized in the same frame.
    /// <para>
    /// The record is written <b>before</b> the card plays, not after it finishes. A player who quits
    /// mid-card has met the type; replaying the halt and the card on the next launch would read as a
    /// bug, and the ability suppression that rides on the same flag would suppress a second spawn.
    /// </para>
    /// </summary>
    public static bool TryClaimIntroduction(EnemyDataSO data)
    {
        string enemyID = EnemyDiscoveryProgress.NormalizeEnemyID(data);
        if (enemyID == null)
            return false;

        if (!IntroducedThisSession.Add(enemyID))
            return false;

        HashSet<string> persisted = LoadIntroducedIDs();
        if (!persisted.Add(enemyID))
            return false;

        if (SaveManager.Instance == null || SaveManager.Instance.Mode != SaveManagerMode.RevisedBlocked)
            SaveIntroducedIDs(persisted);

        return true;
    }

    /// <summary>
    /// Forgets every introduction, so the cards play again. Used by the same save-reset paths that
    /// clear the other one-time tutorial flags.
    /// </summary>
    public static void ClearAllIntroduced()
    {
        IntroducedThisSession.Clear();
        PlayerPrefs.DeleteKey(IntroducedEnemyIDsKey);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    /// <summary>
    /// Test seam. Clears the session set as well as the key — a test that only deleted the key
    /// would still be blocked by a claim an earlier test in the same run made.
    /// </summary>
    public static void ResetForTests()
    {
        ClearAllIntroduced();
    }
#endif

    private static HashSet<string> LoadIntroducedIDs()
    {
        HashSet<string> introduced = new HashSet<string>();
        string raw = PlayerPrefs.GetString(IntroducedEnemyIDsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return introduced;

        string[] ids = raw.Split('\n');
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i]?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(id))
                introduced.Add(id);
        }

        return introduced;
    }

    private static void SaveIntroducedIDs(HashSet<string> introduced)
    {
        List<string> sorted = new List<string>(introduced);
        sorted.Sort(System.StringComparer.Ordinal);
        PlayerPrefs.SetString(IntroducedEnemyIDsKey, string.Join("\n", sorted));
        PlayerPrefs.Save();
    }
}
