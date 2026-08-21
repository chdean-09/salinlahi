using System;
using UnityEngine;

/// <summary>
/// Level-tuning overlay for <see cref="ChallengeSession"/> difficulty (SALIN-181).
/// A null policy (or an unset tier) preserves the legacy per-unit behavior
/// (unit.maxErrors / unit.heartPenalty / unbounded hints) exactly.
/// </summary>
[Serializable]
public sealed class ChallengeTierPolicy
{
    [Tooltip("Workbook difficulty tier. 0 = unset (legacy per-unit behavior).")]
    [Range(0, 5)] public int tier;

    [Tooltip("Tiers 1-2: false — errors are supportive retries and never cost hearts.")]
    public bool heartPenaltiesEnabled = true;

    [Tooltip("Tiers 3-5: incorrect submissions per heart penalty.")]
    [Min(1)] public int errorsPerPenalty = 3;

    [Tooltip("A penalty resets only the current sentence/paragraph checkpoint.")]
    public bool checkpointResetOnPenalty = true;

    [Tooltip("Tier 5: allow the budgeted emergency hint.")]
    public bool emergencyHintEnabled;

    [Tooltip("Tier 5: emergency hints available per level attempt.")]
    [Min(0)] public int emergencyHintsPerAttempt = 1;

    [Tooltip("Score fraction deducted per emergency hint used (consumed by Results/SALIN-202).")]
    [Range(0f, 1f)] public float emergencyHintScorePenalty = 0.10f;

    /// <summary>Canonical preset for a workbook tier (1-5).</summary>
    public static ChallengeTierPolicy ForTier(int tier)
    {
        int clamped = Mathf.Clamp(tier, 1, 5);
        return new ChallengeTierPolicy
        {
            tier = clamped,
            heartPenaltiesEnabled = clamped >= 3,
            errorsPerPenalty = 3,
            checkpointResetOnPenalty = true,
            emergencyHintEnabled = clamped == 5,
            emergencyHintsPerAttempt = 1,
            emergencyHintScorePenalty = 0.10f,
        };
    }
}
