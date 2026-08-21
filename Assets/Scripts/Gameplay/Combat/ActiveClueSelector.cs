using System.Collections.Generic;

/// <summary>
/// One enemy considered for the active-clue mark, flattened to plain data so the selection
/// policy can be tested without a scene.
/// </summary>
public struct ClueCandidate
{
    /// <summary>Canonical combat character id carried by this enemy.</summary>
    public string CharacterId;

    /// <summary>Distance to the shrine. Lower is closer, so lower wins.</summary>
    public float DistanceToBase;

    /// <summary>Monotonic spawn counter used as the deterministic tiebreaker.</summary>
    public long SpawnSequence;

    public bool IsEligible;

    public ClueCandidate(
        string characterId,
        float distanceToBase,
        long spawnSequence,
        bool isEligible)
    {
        CharacterId = characterId;
        DistanceToBase = distanceToBase;
        SpawnSequence = spawnSequence;
        IsEligible = isEligible;
    }
}

/// <summary>
/// Selection policy for the active clue: threat first, deterministic tiebreak second.
/// Deliberately free of UnityEngine types so each determinism criterion is an EditMode test.
/// </summary>
public static class ActiveClueSelector
{
    /// <summary>
    /// Distance band treated as a tie. Two enemies spawned on the same row do not have
    /// bit-identical Y values, so exact float equality would make ties unreachable.
    /// </summary>
    public const float TieEpsilon = 0.0001f;

    /// <summary>
    /// Returns the index of the winning candidate, or -1 when none is eligible.
    /// Pure and order-independent: the same candidate set always yields the same winner.
    /// </summary>
    public static int SelectIndex(IReadOnlyList<ClueCandidate> candidates)
    {
        if (candidates == null)
            return -1;

        // First pass: the globally closest eligible distance.
        float minimumDistance = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            ClueCandidate candidate = candidates[i];
            if (candidate.IsEligible && candidate.DistanceToBase < minimumDistance)
                minimumDistance = candidate.DistanceToBase;
        }

        if (minimumDistance == float.MaxValue)
            return -1;

        // Second pass: among everything inside the tie band of that global minimum, the
        // lowest spawn sequence wins.
        //
        // Two passes rather than one because epsilon comparison is not transitive: with a
        // chain of candidates each within TieEpsilon of its neighbour but not of the far
        // end, comparing only against the running best made the winner depend on the order
        // the list happened to arrive in. The spec requires this function to be pure and
        // order-independent, so the band is anchored to the global minimum instead.
        float tieBandLimit = minimumDistance + TieEpsilon;
        int best = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            ClueCandidate candidate = candidates[i];
            if (!candidate.IsEligible)
                continue;

            if (candidate.DistanceToBase > tieBandLimit)
                continue;

            if (best < 0 || candidate.SpawnSequence < candidates[best].SpawnSequence)
                best = i;
        }

        return best;
    }
}
