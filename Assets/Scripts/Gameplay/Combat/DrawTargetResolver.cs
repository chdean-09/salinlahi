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
/// decided by the caller and arrives as <see cref="ClueCandidate.IsEligible"/>; bosses, dying,
/// phased-out and resolution-blocked enemies are already false by the time they get here.</para>
///
/// <para><b>Iligaw's false copy remains targetable.</b> A matching real carrier takes priority over
/// a copy; when no eligible real carrier shares the drawn glyph, the copy remains the single
/// target and retains its existing consequence downstream.</para>
///
/// <para><b>The multi-kill chain is the one place that does ask.</b> A copy remains a single-target
/// fallback when no eligible real carrier shares the glyph, but it is not a legitimate member of a set: counted toward the chain
/// threshold it lets a board of two real carriers plus a copy arm a chain authored to need three, and
/// admitted to the chain tail it hands the player a copy's death as part of a reward burst. Both read
/// as progress the copy cannot deliver. This mirrors the rule CombatResolver's legacy burst path has
/// always applied to its own realMatchCount, which skips decoys for the same reason — a reward path
/// is for sets of legitimate enemies. Levels 1-5 ship with the chain disabled, so nothing in the
/// tutorial exercises this today; it is fixed here rather than left latent because the fault is
/// invisible until a chain-enabled level meets an Iligaw, and by then it looks like a tuning
/// problem.</para>
/// </summary>
public static class DrawTargetResolver
{
    /// <summary>
    /// Number of eligible on-screen enemies carrying the drawn glyph that could legitimately belong
    /// to a chain. This is the count the chain threshold is measured against, mirroring
    /// CombatResolver's realMatchCount rule — false copies excluded, exactly as that rule excludes
    /// them, so a copy cannot pad a board up to a threshold the real enemies do not meet.
    /// </summary>
    public static int CountMatches(IReadOnlyList<ClueCandidate> candidates, string drawnCharacterId)
    {
        if (candidates == null || string.IsNullOrEmpty(drawnCharacterId))
            return 0;

        int count = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (IsChainVictim(candidates[i], drawnCharacterId))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Index of the one carrier a non-chaining draw resolves against, or -1 when the draw is a
    /// miss. A matching real carrier wins over copies; within that class, closest to the base wins,
    /// ties broken by spawn sequence. A copy remains targetable when it is the only matching carrier.
    /// </summary>
    public static int SelectSingleIndex(
        IReadOnlyList<ClueCandidate> candidates, string drawnCharacterId)
    {
        if (candidates == null || string.IsNullOrEmpty(drawnCharacterId))
            return -1;

        bool hasEligibleRealCarrier = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            ClueCandidate candidate = candidates[i];
            if (IsCarrier(candidate, drawnCharacterId) && !candidate.IsDecoy)
            {
                hasEligibleRealCarrier = true;
                break;
            }
        }

        // Narrow eligibility to matching carriers. Real carriers have priority when any are
        // eligible; copies remain selectable when they are the only matching carriers.
        var carriers = new List<ClueCandidate>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            ClueCandidate candidate = candidates[i];
            candidate.IsEligible = IsCarrier(candidate, drawnCharacterId)
                                   && (!hasEligibleRealCarrier || !candidate.IsDecoy);
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
    /// Minimum chainable carriers before the chain arms — <see cref="CountMatches"/>, so false copies
    /// do not count toward it. Below the threshold the draw stays single-target, so a level that
    /// chains at three does not silently start chaining at two.
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
            if (!IsChainVictim(candidates[i], drawnCharacterId))
                continue;

            InsertByThreat(candidates, targets, i);
        }
    }

    private static bool IsCarrier(ClueCandidate candidate, string drawnCharacterId)
        => candidate.IsEligible
           && string.Equals(candidate.CharacterId, drawnCharacterId, StringComparison.Ordinal);

    /// <summary>
    /// A carrier that may also be swept up by a chain. The head is chosen with <see cref="IsCarrier"/>
    /// instead, so a copy can still be the one body a draw kills — it just cannot ride along in
    /// someone else's chain, nor help arm one.
    /// </summary>
    private static bool IsChainVictim(ClueCandidate candidate, string drawnCharacterId)
        => IsCarrier(candidate, drawnCharacterId) && !candidate.IsDecoy;

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
