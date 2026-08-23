using System.Collections.Generic;

/// <summary>
/// Pure evidence-to-state rules. No I/O, no EventBus, no persistence access.
/// </summary>
public static class MasteryEvaluator
{
    public static MasteryState Evaluate(DimensionEvidence evidence, LearningTuningSO tuning)
    {
        if (evidence == null || tuning == null)
            return MasteryState.None;

        MasteryState earned = MasteryState.Introduced;

        if (evidence.immediateSuccesses >= tuning.immediateSuccessesForPracticed)
            earned = MasteryState.Practiced;

        if (earned == MasteryState.Practiced &&
            evidence.delayedSuccesses >= tuning.delayedSuccessesForRecalled)
            earned = MasteryState.Recalled;

        if (earned == MasteryState.Recalled &&
            evidence.delayedSuccesses >= tuning.delayedSuccessesForMastered &&
            evidence.delayedSessionCount >= tuning.delayedSessionsForMastered)
            earned = MasteryState.Mastered;

        return earned > evidence.highWaterState ? earned : evidence.highWaterState;
    }

    public static MasteryState Aggregate(
        IReadOnlyList<DimensionEvidence> dimensions, LearningContentKind contentKind)
    {
        IReadOnlyList<MasteryDimension> applicable = MasteryDimensions.For(contentKind);
        MasteryState lowest = MasteryState.Mastered;

        for (int i = 0; i < applicable.Count; i++)
        {
            MasteryState state = FindState(dimensions, applicable[i]);
            if (state < lowest)
                lowest = state;
        }

        return lowest;
    }

    private static MasteryState FindState(
        IReadOnlyList<DimensionEvidence> dimensions, MasteryDimension dimension)
    {
        if (dimensions == null)
            return MasteryState.None;

        for (int i = 0; i < dimensions.Count; i++)
            if (dimensions[i] != null && dimensions[i].dimension == dimension)
                return dimensions[i].highWaterState;

        return MasteryState.None;
    }
}
