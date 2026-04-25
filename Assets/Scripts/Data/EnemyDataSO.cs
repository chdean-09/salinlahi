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

    [Header("Kisha Charge")]
    [Tooltip("Speed multiplier applied after Kisha enters Charge state.")]
    public float chargeMultiplier = 2.5f;

    [Range(0f, 1f)]
    [Tooltip("Viewport Y threshold that starts Kisha's pause/charge sequence. 0 is bottom, 1 is top.")]
    public float chargeTriggerYNormalized = 0.5f;

    [Tooltip("Seconds Kisha waits between walking and charging.")]
    public float pauseDuration = 0.35f;

    [Header("Kempei Censor")]
    [Tooltip("World-space radius around Kempei that receives visual-only label scrambling.")]
    public float scrambleRadius = 3f;
}
