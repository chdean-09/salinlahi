using System.Collections.Generic;
using UnityEngine;

public enum BossMovementPattern { Hover, Pace, Teleport }

// Single phase definition embedded in BossConfigSO.phases. Phase clears
// when the player completes the Vulnerable window (N correct random
// glyphs within vulnerabilityTimer). Each phase is 1 HP.
[System.Serializable]
public class BossPhase
{
    [Header("Summoning Phase")]
    [Tooltip("Seconds the boss summons minions during this phase.")]
    public float summonDuration = 30f;
    [Tooltip("Seconds between summon ticks. In Teleport movement this is also the teleport cadence.")]
    public float summonInterval = 5f;
    [Tooltip("Min minions spawned per tick. Inclusive.")]
    public int summonBurstMin = 2;
    [Tooltip("Max minions spawned per tick. Inclusive (Random.Range(min, max+1)).")]
    public int summonBurstMax = 3;
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
}
