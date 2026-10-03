using System;

public sealed class ReviewDueItem
{
    public string ContentId { get; }
    public LearningContentKind ContentKind { get; }
    public MasteryState State { get; }
    public ReviewCheckpoint? Checkpoint { get; }
    public float Priority { get; }

    public ReviewDueItem(
        string contentId,
        LearningContentKind contentKind,
        MasteryState state,
        ReviewCheckpoint? checkpoint,
        float priority)
    {
        ContentId = contentId;
        ContentKind = contentKind;
        State = state;
        Checkpoint = checkpoint;
        Priority = priority;
    }
}

/// <summary>
/// Pure ordering weight for suggested practice. Required items are never filtered by this score.
/// </summary>
public static class PracticePriority
{
    public static float Compute(
        float accuracy, MasteryState state, int overdueCount, LearningTuningSO tuning)
    {
        if (tuning == null)
            return 0f;

        float accuracyTerm = (1f - Clamp01(accuracy)) * tuning.accuracyWeight;
        float stateTerm = ((int)MasteryState.Mastered - (int)state) * tuning.stateGapWeight;
        float overdueTerm = Math.Max(0, overdueCount) * tuning.overdueWeight;

        return accuracyTerm + stateTerm + overdueTerm;
    }

    public static float AccuracyOrDefault(int attempts, int successes)
    {
        if (attempts <= 0)
            return 1f;
        return Clamp01(successes / (float)attempts);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        return value > 1f ? 1f : value;
    }
}
