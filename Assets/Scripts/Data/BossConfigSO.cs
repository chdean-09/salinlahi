using System.Collections.Generic;
using UnityEngine;

// Configuration for a single boss encounter. Phase count is the source of
// truth for the boss's effective HP — there is no separate maxHealth field.
[CreateAssetMenu(fileName = "BossConfig", menuName = "Salinlahi/Boss Config")]
public class BossConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string bossName;
    public string bossID;

    [Header("Visuals")]
    [Tooltip("Optional HUD/portrait sprite, distinct from the in-world Enemy sprite.")]
    public Sprite bossSprite;

    [Header("Spawning")]
    [Tooltip("EnemyDataSO defining the boss's prefab, base sprite, animator, and collision behavior. Its assignedCharacter MUST be null so the boss is invisible to FindClosestToBase.")]
    public EnemyDataSO bossEnemyData;

    [Header("Phases")]
    [Tooltip("Ordered. Phase count = boss's effective HP. Last phase clear ends the encounter.")]
    public List<BossPhase> phases;

    [Header("Intro / Outro")]
    [Tooltip("Seconds the boss is invulnerable on entry while the intro animation plays.")]
    public float introDuration = 2.0f;
    [Tooltip("Seconds before OnLevelComplete fires after the last phase is cleared.")]
    public float outroDuration = 2.5f;
}
