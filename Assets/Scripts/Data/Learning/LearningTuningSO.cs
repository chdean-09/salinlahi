using UnityEngine;

[CreateAssetMenu(fileName = "LearningTuning", menuName = "Salinlahi/Learning Tuning")]
public sealed class LearningTuningSO : ScriptableObject
{
    [Header("Mastery thresholds")]
    [Tooltip("Immediate successes in one dimension required to reach Practiced.")]
    [Min(1)] public int immediateSuccessesForPracticed = 2;

    [Tooltip("Delayed retrieval successes required to reach Recalled.")]
    [Min(1)] public int delayedSuccessesForRecalled = 1;

    [Tooltip("Delayed retrieval successes required to reach Mastered.")]
    [Min(1)] public int delayedSuccessesForMastered = 2;

    [Tooltip("Distinct committed sessions carrying delayed retrieval successes required to reach " +
             "Mastered. Sessions rather than levels, so finale content stays reachable.")]
    [Min(1)] public int delayedSessionsForMastered = 2;

    [Header("Review offsets")]
    [Min(1)] public int nextLevelOffset = 1;
    [Min(1)] public int laterLevelOffset = 3;

    [Header("Suggested practice priority weights")]
    [Tooltip("Weight on (1 - accuracy). Higher favours weaker content.")]
    public float accuracyWeight = 1f;

    [Tooltip("Weight on how far the aggregate state sits below Mastered.")]
    public float stateGapWeight = 1f;

    [Tooltip("Weight on the count of overdue review checkpoints.")]
    public float overdueWeight = 2f;
}
