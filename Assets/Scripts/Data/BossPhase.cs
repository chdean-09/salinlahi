using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum BossMovementPattern { Hover, Pace, Teleport }

// Single phase definition embedded in BossConfigSO.phases. Phase clears
// when the player completes the Vulnerable window (N correct random
// glyphs within vulnerabilityTimer). Each phase is 1 HP.
[System.Serializable]
public class BossPhase
{
    [Header("Summoning Phase")]
    [FormerlySerializedAs("summonDuration")]
    [Tooltip("Total phase length in seconds. No NEW summon acts may start after this elapses; an act already in progress always runs to completion.")]
    public float summonPhaseDuration = 30f;

    [FormerlySerializedAs("summonInterval")]
    [Tooltip("Seconds BETWEEN summon acts. The boss's movement pattern (teleport / pace) fires during this gap; in Teleport movement this is also the teleport cadence.")]
    public float delayBetweenSummons = 5f;

    [FormerlySerializedAs("summonBurstMin")]
    [Tooltip("Min minions spawned per summon act (inclusive).")]
    public int minionsPerSummonMin = 2;

    [FormerlySerializedAs("summonBurstMax")]
    [Tooltip("Max minions spawned per summon act (inclusive, Random.Range(min, max+1)).")]
    public int minionsPerSummonMax = 3;

    [Tooltip("Seconds WITHIN a summon act between consecutive minion spawns. Total in-act duration ≈ count × delayBetweenMinions. Set to 0 to disable stagger — not recommended.")]
    public float delayBetweenMinions = 0.6f;
    [Tooltip("Pool of enemy types this phase may summon. Empty falls back to BossConfigSO.fallbackEnemyTypes.")]
    public List<EnemyDataSO> summonEnemyTypes;
    [Tooltip("Half-range around the boss's CURRENT position for each minion's spawn origin.")]
    public Vector2 summonSpawnRange = new Vector2(2f, 0f);

    [Header("Vulnerability Window")]
    [Tooltip("Number of correct random glyphs the player must draw to damage the boss this phase.")]
    public int requiredCharacterCount = 3;
    [Tooltip("Seconds the vulnerability window lasts. Counted from when the collapse animation completes.")]
    public float vulnerabilityTimer = 12f;

    [Header("Movement")]
    public BossMovementPattern movementPattern = BossMovementPattern.Pace;
    [Tooltip("World units per second. Used by Pace; ignored by Hover/Teleport.")]
    public float movementSpeed = 1f;
    [Tooltip("(Pace only) Horizontal half-range around the boss's base position.")]
    public float paceHalfRange = 1.5f;
    [Tooltip("(Teleport only) Half-range around base position. Y > 0 enables vertical teleport.")]
    public Vector2 teleportHalfRange = new Vector2(2f, 0f);

    private void OnValidate()
    {
        if (delayBetweenMinions < 0f) delayBetweenMinions = 0f;
    }
}
