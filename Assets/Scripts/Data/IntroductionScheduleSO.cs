using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The authored plan for which corruption types each level introduces — one asset for the whole
/// campaign, so the teaching order can be read and changed in one place.
///
/// <para>
/// <b>Why this is authored rather than derived.</b> Before this, a type was introduced the first
/// time it was ever met, with no notion of which level owns it. A player entering the campaign at
/// Level 2 therefore met Abo, Iligaw and Mantsa there — cards owed to Level 1 — on top of the two
/// Level 2 exists to teach. Deriving the plan from the wave tables fixes that case but leaves it
/// computed: invisible in the inspector and not something a designer can change.
/// </para>
///
/// <para>
/// <b>The list is the whole truth.</b> When this asset is assigned to the campaign, a level
/// introduces exactly the types named for it and nothing else. An unlisted type that a wave spawns
/// simply arrives with no card. Add it here to give it one.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "IntroductionSchedule", menuName = "Salinlahi/Introduction Schedule")]
public sealed class IntroductionScheduleSO : ScriptableObject
{
    [Serializable]
    public sealed class LevelIntroductions
    {
        [Tooltip("The level this plan is for.")]
        public LevelConfigSO level;

        [Tooltip("The corruption types this level introduces, in no particular order. A type left "
                 + "out of every entry is never introduced anywhere.")]
        public EnemyDataSO[] introduces = Array.Empty<EnemyDataSO>();

        [Tooltip("Previously-taught syllables that spawn as padding alongside this level's own. "
                 + "Level 2's are EI and NA. Left empty, the level falls back to deriving them "
                 + "from cumulativeSymbolPool minus its target symbols, which is what every level "
                 + "did before this asset existed.")]
        public BaybayinCharacterSO[] fillerSymbols = Array.Empty<BaybayinCharacterSO>();

        [Tooltip("The syllable held back for last: the level is won when its slot is restored. "
                 + "Must be one of the level's own focus-word symbols. Left null, the finale is "
                 + "derived as the last slot whose symbol occurs exactly once (DerivedFinaleGate).")]
        public BaybayinCharacterSO finaleSymbol;
    }

    [Tooltip("One entry per level that introduces anything. A level with no entry introduces "
             + "nothing.")]
    public LevelIntroductions[] levels = Array.Empty<LevelIntroductions>();

    /// <summary>
    /// Whether <paramref name="level"/> is authored to introduce <paramref name="data"/>.
    /// Pure, so the whole rule is an EditMode test.
    /// </summary>
    public bool Introduces(LevelConfigSO level, EnemyDataSO data)
    {
        if (level == null || data == null || levels == null)
            return false;

        for (int i = 0; i < levels.Length; i++)
        {
            LevelIntroductions entry = levels[i];
            if (entry?.level == null || entry.introduces == null)
                continue;

            // Matched by stableId rather than by reference: the prefab-less corruption roster
            // shares one shell across types, and a level config can be reloaded into a different
            // instance between edits.
            if (!string.Equals(entry.level.stableId, level.stableId, StringComparison.Ordinal))
                continue;

            for (int j = 0; j < entry.introduces.Length; j++)
            {
                EnemyDataSO listed = entry.introduces[j];
                if (listed != null && SameEnemy(listed, data))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The authored entry for a level, or null when it has none.
    /// </summary>
    public LevelIntroductions FindEntry(LevelConfigSO level)
    {
        if (level == null || levels == null)
            return null;

        for (int i = 0; i < levels.Length; i++)
        {
            LevelIntroductions entry = levels[i];
            if (entry?.level != null
                && string.Equals(entry.level.stableId, level.stableId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// The authored filler syllables for a level as stableIds, or null when the level authors
    /// none and the derived pool should be used instead. An empty authored array is a DECISION —
    /// this level spawns no padding — and returns an empty list rather than null.
    /// </summary>
    public List<string> ResolveFillerSymbolIds(LevelConfigSO level)
    {
        LevelIntroductions entry = FindEntry(level);
        if (entry?.fillerSymbols == null || entry.fillerSymbols.Length == 0)
            return null;

        var ids = new List<string>(entry.fillerSymbols.Length);
        for (int i = 0; i < entry.fillerSymbols.Length; i++)
        {
            BaybayinCharacterSO symbol = entry.fillerSymbols[i];
            if (symbol != null && !string.IsNullOrEmpty(symbol.stableId) && !ids.Contains(symbol.stableId))
                ids.Add(symbol.stableId);
        }

        return ids;
    }

    /// <summary>The authored finale symbol's stableId for a level, or null when it derives one.</summary>
    public string ResolveFinaleSymbolId(LevelConfigSO level)
    {
        BaybayinCharacterSO symbol = FindEntry(level)?.finaleSymbol;
        return symbol != null && !string.IsNullOrEmpty(symbol.stableId) ? symbol.stableId : null;
    }

    /// <summary>
    /// Matches the identity rule the introduction record uses, so "listed here" and "already
    /// introduced" can never disagree about what counts as the same type.
    /// </summary>
    private static bool SameEnemy(EnemyDataSO a, EnemyDataSO b)
    {
        if (a == b)
            return true;

        string left = EnemyDiscoveryProgress.NormalizeEnemyID(a);
        string right = EnemyDiscoveryProgress.NormalizeEnemyID(b);
        return left != null && right != null && string.Equals(left, right, StringComparison.Ordinal);
    }
}
