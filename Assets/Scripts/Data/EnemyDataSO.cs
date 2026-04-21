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

}
