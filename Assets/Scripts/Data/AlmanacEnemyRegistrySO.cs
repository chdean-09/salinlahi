using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AlmanacEnemyRegistry", menuName = "Salinlahi/Almanac Enemy Registry")]
public class AlmanacEnemyRegistrySO : ScriptableObject
{
    [Tooltip("Ordered. Place regular enemies first, then bosses (the grid sets bosses apart).")]
    public List<AlmanacEnemyEntry> entries = new List<AlmanacEnemyEntry>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entries == null) return;
        foreach (AlmanacEnemyEntry entry in entries)
        {
            if (entry != null && entry.bossConfig != null && entry.bossConfig.bossEnemyData != null)
                entry.enemyData = entry.bossConfig.bossEnemyData;
        }
    }
#endif
}

[System.Serializable]
public class AlmanacEnemyEntry
{
    [Tooltip("In-world enemy data AND the discovery key. For a boss this is bossConfig.bossEnemyData (auto-synced).")]
    public EnemyDataSO enemyData;

    [Tooltip("Set only for boss entries. Null for regular enemies.")]
    public BossConfigSO bossConfig;

    public bool IsBoss => bossConfig != null;

    public string ResolveDisplayName()
    {
        if (IsBoss) return bossConfig.bossName;
        return enemyData != null ? enemyData.displayName : string.Empty;
    }

    public string ResolveDescription()
    {
        if (IsBoss) return bossConfig.description;
        return enemyData != null ? enemyData.description : string.Empty;
    }

    public Sprite ResolvePortrait()
    {
        if (IsBoss) return bossConfig.bossSprite;
        if (enemyData == null) return null;
        if (enemyData.portraitSprite != null) return enemyData.portraitSprite;
        return enemyData.walkFrames != null && enemyData.walkFrames.Length > 0
            ? enemyData.walkFrames[0]
            : null;
    }
}
