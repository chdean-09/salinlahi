using System;
using System.Collections.Generic;

/// <summary>
/// Targeting policy for an accepted draw: which on-screen enemies a recognised glyph resolves
/// against.
///
/// <para>Before this type, a draw on an active-clue level could only resolve against the single
/// marked clue — in practice the enemy closest to the base — so a correct glyph carried by any
/// other enemy read as a miss. The policy here widens <b>targetability</b> to every eligible
/// carrier of the drawn glyph while leaving <b>lethality</b> where the level author already put
/// it: <c>LevelConfigSO.multiKillChainEnabled</c> plus the resolver's chain threshold decide
/// whether one carrier or every carrier resolves.</para>
///
/// <para>Deliberately free of UnityEngine types, following <see cref="ActiveClueSelector"/>, so
/// every targeting rule is an EditMode assertion rather than a scene rehearsal. Eligibility is
/// decided by the caller and arrives as <see cref="ClueCandidate.IsEligible"/>; decoys, bosses,
/// dying, phased-out and resolution-blocked enemies are already false by the time they get here.
/// That is load-bearing for Iligaw: a decoy carries a deliberately false glyph, and pulling one
/// into a multi-target set would turn a correct draw into a heart loss.</para>
/// </summary>
public static class DrawTargetResolver
{
    /// <summary>
    /// Number of eligible on-screen enemies carrying the drawn glyph. This is the count the
    /// chain threshold is measured against, mirroring CombatResolver's realMatchCount rule.
    /// </summary>
    public static int CountMatches(IReadOnlyList<ClueCandidate> candidates, string drawnCharacterId)
    {
        if (candidates == null || string.IsNullOrEmpty(drawnCharacterId))
            return 0;

        int count = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (IsCarrier(candidates[i], drawnCharacterId))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Index of the one carrier a non-chaining draw resolves against, or -1 when the draw is a
    /// miss. Closest to the base wins, ties broken by spawn sequence — the same policy the clue
    /// mark uses, reused rather than restated so the two can never drift apart.
    /// </summary>
    public static int SelectSingleIndex(
        IReadOnlyList<ClueCandidate> candidates, string drawnCharacterId)
    {
        if (candidates == null || string.IsNullOrEmpty(drawnCharacterId))
            return -1;

        // Narrow eligibility to "eligible AND carries the drawn glyph", then hand the set to the
        // existing selector. Indices line up one-for-one with the caller's list.
        var carriers = new List<ClueCandidate>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            ClueCandidate candidate = candidates[i];
            candidate.IsEligible = IsCarrier(candidate, drawnCharacterId);
            carriers.Add(candidate);
        }

        return ActiveClueSelector.SelectIndex(carriers);
    }

    /// <summary>
    /// Fills <paramref name="targets"/> with the candidate indices this draw resolves against,
    /// closest carrier first. Empty means a miss.
    /// </summary>
    /// <param name="chainAllMatches">
    /// The level's <c>multiKillChainEnabled</c>. False keeps one draw to one kill.
    /// </param>
    /// <param name="chainThreshold">
    /// Minimum eligible carriers before the chain arms. Below it the draw stays single-target,
    /// so a level that chains at three does not silently start chaining at two.
    /// </param>
    public static void SelectTargets(
        IReadOnlyList<ClueCandidate> candidates,
        string drawnCharacterId,
        bool chainAllMatches,
        int chainThreshold,
        List<int> targets)
    {
        if (targets == null)
            return;

        targets.Clear();

        int head = SelectSingleIndex(candidates, drawnCharacterId);
        if (head < 0)
            return;

        // The head is always the single-target winner, so the caller can take targets[0] for
        // pronunciation and get the same character whether or not the chain armed.
        targets.Add(head);

        if (!chainAllMatches)
            return;

        if (CountMatches(candidates, drawnCharacterId) < Math.Max(1, chainThreshold))
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (i == head)
                continue;
            if (!IsCarrier(candidates[i], drawnCharacterId))
                continue;

            InsertByThreat(candidates, targets, i);
        }
    }

    private static bool IsCarrier(ClueCandidate candidate, string drawnCharacterId)
        => candidate.IsEligible
           && string.Equals(candidate.CharacterId, drawnCharacterId, StringComparison.Ordinal);

    /// <summary>
    /// Insertion sort over the tail (index 0 is the reserved head). Exact distance compare with a
    /// spawn-sequence tiebreak: transitive, so the chain order depends on the candidate set and
    /// not on the order the tracker happened to hand it over. Target sets are small — a wave, not
    /// a list — so an insertion sort costs less than the allocation a comparer would.
    /// </summary>
    private static void InsertByThreat(
        IReadOnlyList<ClueCandidate> candidates, List<int> targets, int index)
    {
        for (int slot = 1; slot < targets.Count; slot++)
        {
            if (IsMoreThreatening(candidates[index], candidates[targets[slot]]))
            {
                targets.Insert(slot, index);
                return;
            }
        }

        targets.Add(index);
    }

    private static bool IsMoreThreatening(ClueCandidate a, ClueCandidate b)
    {
        if (a.DistanceToBase != b.DistanceToBase)
            return a.DistanceToBase < b.DistanceToBase;

        return a.SpawnSequence < b.SpawnSequence;
    }
}
