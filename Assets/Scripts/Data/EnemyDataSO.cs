using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Salinlahi/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Identity")]
    public string enemyID;             // "standard", "fast", "chain"

    [Header("Stats")]
    [Tooltip("World units per second the enemy moves toward the base")]
    public float moveSpeed = 1.5f;

    [Header("Visuals")]
    public Sprite[] walkFrames;
    public RuntimeAnimatorController animatorController;

    [Header("Character")]
    [Tooltip("The Baybayin character this enemy displays and requires to be defeated")]
    public BaybayinCharacterSO assignedCharacter;
}
