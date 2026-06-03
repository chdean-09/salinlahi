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

    [TextArea]
    [Tooltip("Almanac detail copy for this boss. Optional.")]
    public string description;

    [Header("Spawning")]
    [Tooltip("EnemyDataSO defining the boss's prefab, base sprite, animator, and collision behavior. Its assignedCharacter MUST be null so the boss is invisible to FindClosestToBase.")]
    public EnemyDataSO bossEnemyData;

    [Header("Phases")]
    [Tooltip("Ordered. Phase count = boss's effective HP. Last phase clear ends the encounter.")]
    public List<BossPhase> phases;

    [Header("Summon Fallback")]
    [Tooltip("Enemy types summoned when a BossPhase.summonEnemyTypes list is empty.")]
    public List<EnemyDataSO> fallbackEnemyTypes;

    [Header("Summon Bounds")]
    [Tooltip("Hard horizontal world-space cap applied to every minion spawn position. x = minX, y = maxX. Prevents summons drifting off-screen when the boss is near the edge of the playfield. Set x >= y to disable clamping.")]
    public Vector2 summonHorizontalBounds = Vector2.zero;

    [Header("Intro / Outro")]
    [Tooltip("Seconds the boss is invulnerable on entry while the intro animation plays.")]
    public float introDuration = 2.0f;
    [Tooltip("Seconds before OnLevelComplete fires after the last phase is cleared.")]
    public float outroDuration = 2.5f;

    [Header("Audio")]
    [Tooltip("Per-boss audio bank. May be left null — BossAudio will no-op cleanly if absent.")]
    public BossAudioBankSO audioBank;

    [Header("Tutorial")]
    [Tooltip("Optional upfront tutorial shown at level start before the encounter begins. Null = no tutorial.")]
    public BossTutorialSO tutorial;
}
