using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time migration: copies each level's legacy WaveConfigSO references into embedded
/// WaveDefinitions and bootstraps allowedEnemyTypes. Loss-less: any wave character/enemy not
/// already in the roster is ADDED to the roster (never dropped from the wave).
/// </summary>
public static class LevelWaveMigrator
{
    [MenuItem("Salinlahi/Migration/Migrate Levels To Embedded Waves")]
    public static void MigrateAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelConfigSO");
        int migrated = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            if (level == null)
                continue;

            MigrateLevel(level);
            EditorUtility.SetDirty(level);
            migrated++;
            Debug.Log($"Migrated '{level.name}': {level.embeddedWaves.Count} waves, "
                      + $"{level.allowedEnemyTypes.Count} enemy types in roster.", level);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"LevelWaveMigrator: migrated {migrated} level(s).");
    }

    /// <summary>Pure-ish migration of one level; exposed for tests.</summary>
    public static void MigrateLevel(LevelConfigSO level)
    {
        if (level == null || level.waves == null)
            return;

        level.embeddedWaves ??= new List<WaveDefinition>();
        level.allowedCharacters ??= new List<BaybayinCharacterSO>();
        level.allowedEnemyTypes ??= new List<EnemyDataSO>();
        level.embeddedWaves.Clear();

        foreach (WaveConfigSO source in level.waves)
        {
            if (source == null)
                continue;

            WaveDefinition def = new()
            {
                isIntermissionWave = source.isIntermissionWave,
                enemyCount = source.enemyCount,
                spawnInterval = source.spawnInterval,
                waveStartDelay = source.waveStartDelay,
                characters = CloneNonNull(source.charactersInWave),
                enemyTypes = CloneNonNull(source.enemyTypesInWave),
            };

            // Loss-less reconcile: bootstrap rosters from wave usage.
            foreach (BaybayinCharacterSO c in def.characters)
                if (!level.allowedCharacters.Contains(c))
                    level.allowedCharacters.Add(c);
            foreach (EnemyDataSO e in def.enemyTypes)
                if (!level.allowedEnemyTypes.Contains(e))
                    level.allowedEnemyTypes.Add(e);

            level.embeddedWaves.Add(def);
        }
    }

    private static List<T> CloneNonNull<T>(List<T> source) where T : Object
    {
        List<T> result = new();
        if (source == null)
            return result;
        foreach (T item in source)
            if (item != null)
                result.Add(item);
        return result;
    }
}
