using UnityEngine;

[CreateAssetMenu(fileName = "BossConfig", menuName = "Salinlahi/Boss Config")]
public class BossConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string bossName;
    public string bossID;

    [Header("Stats")]
    [Tooltip("Boss health pool")]
    public int maxHealth = 10;

    [Tooltip("Boss movement speed (0 = stationary)")]
    public float moveSpeed = 0.5f;

    [Header("Phases")]
    [Tooltip("Number of phases (1 for stub)")]
    public int phaseCount = 1;

    [Header("Visuals")]
    public Sprite bossSprite;
    public RuntimeAnimatorController animatorController;

    [Header("Spawning")]
    [Tooltip("Enemy prefab to use for the boss")]
    public EnemyDataSO bossEnemyData;
}
