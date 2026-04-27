using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Salinlahi/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Identity")]
    public string enemyID;             // "standard", "fast", "chain"

    [Header("Stats")]
    [Tooltip("World units per second the enemy moves toward the base")]
    public float moveSpeed = 1.5f;

    [Header("Health")]
    [Tooltip("1 for regular enemies. 2 for shielded (Capitan, Shokan).")]
    public int maxHealth = 1;

    [Header("Visuals")]
    public Sprite[] walkFrames;
    public RuntimeAnimatorController animatorController;

    [Header("Character")]
    [Tooltip("The Baybayin character this enemy actually requires to be defeated.")]
    public BaybayinCharacterSO assignedCharacter;

    [Header("Decoy")]
    [Tooltip("If true, this enemy is a decoy variant and drawing its character applies a penalty.")]
    public bool isDecoy;

    [Header("Contact Behavior")]
    [Tooltip("If false, reaching the Shrine collision zone will despawn this enemy without damaging the Shrine.")]
    public bool dealsContactDamage = true;

    [Header("Variant Era")]
    [Tooltip("Chapter / faction grouping. Used by GeneralAura to limit its buff to American-era allies.")]
    public Era era = Era.Spanish;

    [Header("Zigzag Mover (Pensionado)")]
    [Tooltip("Horizontal sine amplitude in world units. 0 disables zigzag.")]
    public float zigzagAmplitude = 0f;
    [Tooltip("Sine frequency in Hz. 0 disables zigzag.")]
    public float zigzagFrequency = 0f;

    [Header("Base Speed Modifier (General)")]
    [Tooltip("Multiplier applied on top of moveSpeed. 1.0 = default.")]
    public float baseSpeedMultiplier = 1f;

    [Header("Aura (General)")]
    [Tooltip("Radius in world units. 0 disables aura.")]
    public float auraRadius = 0f;
    [Tooltip("Speed multiplier applied to affected same-era non-boss enemies.")]
    public float auraSpeedMultiplier = 1.3f;

    [Header("Death Animation (optional)")]
    [Tooltip("Frames played in sequence on Defeat() before the enemy returns to the pool. Empty = no death animation; the enemy disappears immediately (existing fast-path behaviour).")]
    public Sprite[] deathFrames;
    [Tooltip("Playback FPS for deathFrames. 0 falls back to the walk animation FPS on Enemy.cs (default 8).")]
    public float deathAnimationFps = 8f;
}

public enum Era
{
    Spanish,
    American,
    Japanese
}