using System.Collections.Generic;

/// <summary>
/// SALIN-202: level outcome metrics with stable identifiers, computed from the
/// session's learning-evidence batch plus hearts and hint stats. Formulas are
/// documented in docs/design/scoring-and-stars.md — that document and these
/// constants are the single source of truth.
/// </summary>
public sealed class LevelResults
{
    public LevelResults(IReadOnlyDictionary<string, float> metrics, int stars)
    {
        Metrics = metrics;
        Stars = stars;
    }

    public IReadOnlyDictionary<string, float> Metrics { get; }
    public int Stars { get; }
}

public static class LevelResultsCalculator
{
    public const string TracingAccuracyMetricId = "metric.tracing-accuracy";
    public const string ContextAccuracyMetricId = "metric.context-accuracy";
    public const string HeartsRatioMetricId = "metric.hearts-ratio";
    public const string HintsUsedMetricId = "metric.hints-used";
    public const string EmergencyHintPenaltyMetricId = "metric.emergency-hint-penalty";
    public const string ScoreMetricId = "metric.score";

    public static LevelResults Compute(
        LearningEvidenceBatch evidence,
        int heartsRemaining,
        int maxHearts,
        int hintsUsed,
        float emergencyHintPenalty)
    {
        float tracingAccuracy = Accuracy(evidence, MasteryDimension.Form);
        float contextAccuracy = Accuracy(evidence, MasteryDimension.Assembly, MasteryDimension.Meaning);
        float heartsRatio = maxHearts > 0
            ? Clamp01((float)heartsRemaining / maxHearts)
            : 0f;
        float penalty = Clamp01(emergencyHintPenalty);
        float score = Clamp01(
            0.5f * tracingAccuracy + 0.3f * contextAccuracy + 0.2f * heartsRatio - penalty) * 100f;

        int stars = 1;
        if (heartsRatio >= 0.5f && contextAccuracy >= 0.6f)
            stars = 2;
        if (heartsRatio >= 0.99f && tracingAccuracy >= 0.8f && contextAccuracy >= 0.8f)
            stars = 3;

        var metrics = new Dictionary<string, float>
        {
            [TracingAccuracyMetricId] = tracingAccuracy,
            [ContextAccuracyMetricId] = contextAccuracy,
            [HeartsRatioMetricId] = heartsRatio,
            [HintsUsedMetricId] = hintsUsed,
            [EmergencyHintPenaltyMetricId] = penalty,
            [ScoreMetricId] = score,
        };
        return new LevelResults(metrics, stars);
    }

    /// <summary>Successes over attempts across the given dimensions; 1 with no attempts.</summary>
    private static float Accuracy(LearningEvidenceBatch evidence, params MasteryDimension[] dimensions)
    {
        int attempts = 0;
        int successes = 0;
        if (evidence?.entries != null)
        {
            foreach (LearningEvidenceEntry entry in evidence.entries)
            {
                if (entry == null)
                    continue;
                foreach (MasteryDimension dimension in dimensions)
                {
                    if (entry.dimension == dimension)
                    {
                        attempts += entry.attemptCount;
                        successes += entry.successCount;
                        break;
                    }
                }
            }
        }

        return attempts > 0 ? Clamp01((float)successes / attempts) : 1f;
    }

    private static float Clamp01(float value)
    {
        return value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
