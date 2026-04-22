#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SALIN55SceneBuilder
{
    private const string MenuPath = "Salinlahi/SALIN-55: Setup Levels 11-15 (Chapter 3)";
    private const string EnemyDataFolder = "Assets/ScriptableObjects";
    private const string BossConfigFolder = "Assets/ScriptableObjects";
    private const string WavesFolder = "Assets/ScriptableObjects/Waves";
    private const string LevelsFolder = "Assets/ScriptableObjects/Levels";
    private const string CharactersFolder = "Assets/ScriptableObjects/Characters";
    private const string EnemyPrefabsFolder = "Assets/Prefabs/Enemies";

    private static readonly string[] AllCharacterIDs =
    {
        "BA", "KA", "DA", "GA", "HA", "LA", "MA", "NA", "NGA",
        "PA", "SA", "TA", "WA", "YA", "A", "EI", "OU"
    };

    [MenuItem(MenuPath)]
    public static void SetupLevels11To15()
    {
        Undo.SetCurrentGroupName("SALIN-55: Setup Levels 11-15");
        int undoGroup = Undo.GetCurrentGroup();

        var characters = LoadAllCharacters();
        if (characters.Count < 17)
            Debug.LogWarning($"[SALIN-55] Only found {characters.Count}/17 BaybayinCharacterSO assets.");

        Sprite placeholderSprite = LoadSoldadoSprite();

        var enemyData = CreateEnemyDataAssets(placeholderSprite);
        EnemyDataSO bossData = UpdateBossEnemyData();
        BossConfigSO bossConfig = CreateBossConfigAsset(bossData);
        CreateEnemyPrefabs();

        var l11Waves = CreateLevel11Waves(enemyData, characters);
        var l12Waves = CreateLevel12Waves(enemyData, characters);
        var l13Waves = CreateLevel13Waves(enemyData, characters);
        var l14Waves = CreateLevel14Waves(enemyData, characters);
        var l15Waves = CreateLevel15Waves(enemyData, characters);

        WireLevelConfig(11, "Bagong Pananakop", l11Waves, characters, false, null);
        WireLevelConfig(12, "Karahawan", l12Waves, characters, false, null);
        WireLevelConfig(13, "Kempei Patrol", l13Waves, characters, false, null);
        WireLevelConfig(14, "Gauntlet", l14Waves, characters, false, null);
        WireLevelConfig(15, "Kadiliman", l15Waves, characters, true, bossConfig);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("SALIN-55 Scene Setup",
            "Setup complete!\n\n" +
            $"Created {enemyData.Count} EnemyDataSO assets (Heitai, Kisha, Kempei, Shokan).\n" +
            "Created 4 enemy prefabs.\n" +
            "Created BossConfig_Kadiliman.\n" +
            "Created wave configs for Levels 11-15.\n" +
            "Wired Level 11-15 configs.\n\n" +
            "MANUAL STEPS REMAINING:\n" +
            "1. Open Gameplay scene and register new prefabs in EnemyPool._registeredEnemyPrefabs.\n" +
            "2. Save all scenes and assets.",
            "OK");
    }

    private static Dictionary<string, BaybayinCharacterSO> LoadAllCharacters()
    {
        var result = new Dictionary<string, BaybayinCharacterSO>();
        string[] guids = AssetDatabase.FindAssets("t:BaybayinCharacterSO", new[] { CharactersFolder });

        foreach (string guid in guids)
        {
            string apath = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(apath);
            if (c != null && !string.IsNullOrEmpty(c.characterID))
                result[c.characterID] = c;
        }

        Debug.Log($"[SALIN-55] Loaded {result.Count} BaybayinCharacterSO assets.");
        return result;
    }

    private static Sprite LoadSoldadoSprite()
    {
        var soldado = LoadAssetByName<EnemyDataSO>("EnemyData_Soldado");
        if (soldado != null && soldado.walkFrames != null && soldado.walkFrames.Length > 0)
            return soldado.walkFrames[0];
        return null;
    }

    private static Dictionary<string, EnemyDataSO> CreateEnemyDataAssets(Sprite placeholderSprite)
    {
        var result = new Dictionary<string, EnemyDataSO>();

        var entries = new (string name, string id, float speed, int health)[]
        {
            ("EnemyData_Heitai", "heitai", 1.8f, 1),
            ("EnemyData_Kisha", "kisha", 3.75f, 1),
            ("EnemyData_Kempei", "kempei", 1.5f, 2),
            ("EnemyData_Shokan", "shokan", 1.2f, 2),
        };

        foreach (var entry in entries)
        {
            string assetPath = $"{EnemyDataFolder}/{entry.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(assetPath);
            if (existing != null)
            {
                result[entry.id] = existing;
                Debug.Log($"[SALIN-55] EnemyDataSO already exists: {entry.name}");
                continue;
            }

            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = entry.id;
            data.moveSpeed = entry.speed;
            data.maxHealth = entry.health;

            if (placeholderSprite != null)
                data.walkFrames = new Sprite[] { placeholderSprite };

            AssetDatabase.CreateAsset(data, assetPath);
            result[entry.id] = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(assetPath);
            Debug.Log($"[SALIN-55] Created EnemyDataSO: {entry.name} (id={entry.id}, speed={entry.speed}, hp={entry.health})");
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static EnemyDataSO UpdateBossEnemyData()
    {
        var bossData = LoadAssetByName<EnemyDataSO>("EnemyData_Boss");
        if (bossData == null)
        {
            Debug.LogError("[SALIN-55] EnemyData_Boss.asset not found. Create it manually.");
            return null;
        }

        Undo.RecordObject(bossData, "Update EnemyData_Boss stats");
        bossData.enemyID = "kadiliman";
        bossData.moveSpeed = 0.5f;
        bossData.maxHealth = 10;
        EditorUtility.SetDirty(bossData);

        Debug.Log("[SALIN-55] Updated EnemyData_Boss: id=kadiliman, speed=0.5, hp=10");
        return bossData;
    }

    private static BossConfigSO CreateBossConfigAsset(EnemyDataSO bossData)
    {
        string assetPath = $"{BossConfigFolder}/BossConfig_Kadiliman.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BossConfigSO>(assetPath);
        if (existing != null)
        {
            Debug.Log("[SALIN-55] BossConfig_Kadiliman already exists.");
            return existing;
        }

        var config = ScriptableObject.CreateInstance<BossConfigSO>();
        config.bossName = "Kadiliman";
        config.bossID = "kadiliman";
        config.maxHealth = 10;
        config.moveSpeed = 0.5f;
        config.phaseCount = 1;
        config.bossEnemyData = bossData;

        AssetDatabase.CreateAsset(config, assetPath);
        Debug.Log("[SALIN-55] Created BossConfig_Kadiliman.");
        return AssetDatabase.LoadAssetAtPath<BossConfigSO>(assetPath);
    }

    private static void CreateEnemyPrefabs()
    {
        string[] prefabNames = { "[Enemy] Heitai", "[Enemy] Kisha", "[Enemy] Kempei", "[Enemy] Shokan" };
        GameObject soldadoPrefab = LoadAssetByName<GameObject>("[Enemy] Soldado");

        foreach (string prefabName in prefabNames)
        {
            string assetPath = $"{EnemyPrefabsFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
            {
                Debug.Log($"[SALIN-55] Prefab already exists: {prefabName}");
                continue;
            }

            if (soldadoPrefab == null)
            {
                Debug.LogWarning($"[SALIN-55] Cannot create {prefabName}: [Enemy] Soldado prefab not found. Create manually.");
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(soldadoPrefab);
            instance.name = prefabName;
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
            Undo.DestroyObjectImmediate(instance);

            if (prefab != null)
                Debug.Log($"[SALIN-55] Created prefab: {prefabName}");
            else
                Debug.LogError($"[SALIN-55] Failed to save prefab: {prefabName}");
        }

        AssetDatabase.SaveAssets();
    }

    private static List<WaveConfigSO> CreateLevel11Waves(
        Dictionary<string, EnemyDataSO> enemyData,
        Dictionary<string, BaybayinCharacterSO> characters)
    {
        var waveDefs = new (string id, int num, string[] enemyIDs, int count, float interval, float delay, string[] charIDs)[]
        {
            ("Level11_Wave1", 1, new[]{ "heitai" }, 4, 3.0f, 2.0f, new[]{ "BA", "KA", "DA", "GA" }),
            ("Level11_Wave2", 2, new[]{ "heitai" }, 5, 2.8f, 1.5f, new[]{ "HA", "LA", "MA", "NA", "NGA" }),
            ("Level11_Wave3", 3, new[]{ "heitai" }, 6, 2.5f, 1.5f, new[]{ "PA", "SA", "TA", "WA", "YA" }),
            ("Level11_Wave4", 4, new[]{ "heitai" }, 7, 2.2f, 1.0f, AllCharacterIDs),
        };

        return CreateWaveAssets(waveDefs, enemyData, characters);
    }

    private static List<WaveConfigSO> CreateLevel12Waves(
        Dictionary<string, EnemyDataSO> enemyData,
        Dictionary<string, BaybayinCharacterSO> characters)
    {
        var waveDefs = new (string id, int num, string[] enemyIDs, int count, float interval, float delay, string[] charIDs)[]
        {
            ("Level12_Wave1", 1, new[]{ "heitai" }, 5, 2.8f, 2.0f, new[]{ "BA", "KA", "DA", "GA", "HA" }),
            ("Level12_Wave2", 2, new[]{ "heitai", "kisha" }, 5, 2.5f, 1.5f, new[]{ "LA", "MA", "NA", "NGA", "PA" }),
            ("Level12_Wave3", 3, new[]{ "heitai", "kisha" }, 6, 2.3f, 1.5f, new[]{ "SA", "TA", "WA", "YA", "A" }),
            ("Level12_Wave4", 4, new[]{ "kisha" }, 6, 2.0f, 1.0f, new[]{ "EI", "OU", "BA", "KA", "DA" }),
            ("Level12_Wave5", 5, new[]{ "heitai", "kisha" }, 7, 2.0f, 1.0f, AllCharacterIDs),
        };

        return CreateWaveAssets(waveDefs, enemyData, characters);
    }

    private static List<WaveConfigSO> CreateLevel13Waves(
        Dictionary<string, EnemyDataSO> enemyData,
        Dictionary<string, BaybayinCharacterSO> characters)
    {
        var waveDefs = new (string id, int num, string[] enemyIDs, int count, float interval, float delay, string[] charIDs)[]
        {
            ("Level13_Wave1", 1, new[]{ "heitai", "kisha" }, 5, 2.5f, 2.0f, new[]{ "BA", "KA", "DA", "GA", "HA", "LA" }),
            ("Level13_Wave2", 2, new[]{ "heitai", "kempei" }, 6, 2.3f, 1.5f, new[]{ "MA", "NA", "NGA", "PA", "SA", "TA" }),
            ("Level13_Wave3", 3, new[]{ "kisha", "kempei" }, 6, 2.0f, 1.5f, new[]{ "WA", "YA", "A", "EI", "OU" }),
            ("Level13_Wave4", 4, new[]{ "heitai", "kisha", "kempei" }, 7, 2.0f, 1.0f, AllCharacterIDs),
            ("Level13_Wave5", 5, new[]{ "heitai", "kisha", "kempei" }, 8, 1.8f, 1.0f, AllCharacterIDs),
        };

        return CreateWaveAssets(waveDefs, enemyData, characters);
    }

    private static List<WaveConfigSO> CreateLevel14Waves(
        Dictionary<string, EnemyDataSO> enemyData,
        Dictionary<string, BaybayinCharacterSO> characters)
    {
        var waveDefs = new (string id, int num, string[] enemyIDs, int count, float interval, float delay, string[] charIDs)[]
        {
            ("Level14_Wave1", 1, new[]{ "heitai", "kisha" }, 7, 1.8f, 1.5f, AllCharacterIDs),
            ("Level14_Wave2", 2, new[]{ "heitai", "kempei", "kisha" }, 8, 1.5f, 1.0f, AllCharacterIDs),
            ("Level14_Wave3", 3, new[]{ "heitai", "kisha", "kempei", "shokan" }, 8, 1.3f, 1.0f, AllCharacterIDs),
            ("Level14_Wave4", 4, new[]{ "kisha", "kempei", "shokan" }, 9, 1.2f, 0.8f, AllCharacterIDs),
            ("Level14_Wave5", 5, new[]{ "heitai", "kisha", "kempei", "shokan" }, 10, 1.0f, 0.5f, AllCharacterIDs),
        };

        return CreateWaveAssets(waveDefs, enemyData, characters);
    }

    private static List<WaveConfigSO> CreateLevel15Waves(
        Dictionary<string, EnemyDataSO> enemyData,
        Dictionary<string, BaybayinCharacterSO> characters)
    {
        var waveDefs = new (string id, int num, string[] enemyIDs, int count, float interval, float delay, string[] charIDs)[]
        {
            ("Level15_Wave1", 1, new[]{ "heitai", "kisha" }, 3, 2.0f, 2.0f, AllCharacterIDs),
        };

        return CreateWaveAssets(waveDefs, enemyData, characters);
    }

    private static List<WaveConfigSO> CreateWaveAssets(
        (string id, int num, string[] enemyIDs, int count, float interval, float delay, string[] charIDs)[] waveDefs,
        Dictionary<string, EnemyDataSO> enemyData,
        Dictionary<string, BaybayinCharacterSO> characters)
    {
        var waves = new List<WaveConfigSO>();

        foreach (var def in waveDefs)
        {
            string assetPath = $"{WavesFolder}/{def.id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(assetPath);
            if (existing != null)
            {
                waves.Add(existing);
                Debug.Log($"[SALIN-55] Wave already exists: {def.id}");
                continue;
            }

            var wave = ScriptableObject.CreateInstance<WaveConfigSO>();
            wave.waveID = def.id;
            wave.waveNumber = def.num;
            wave.enemyCount = def.count;
            wave.spawnInterval = def.interval;
            wave.waveStartDelay = def.delay;

            wave.enemyTypesInWave = new List<EnemyDataSO>();
            foreach (string eid in def.enemyIDs)
            {
                if (enemyData.TryGetValue(eid, out var ed))
                    wave.enemyTypesInWave.Add(ed);
                else
                    Debug.LogWarning($"[SALIN-55] EnemyDataSO not found for id: {eid}");
            }

            wave.charactersInWave = new List<BaybayinCharacterSO>();
            foreach (string cid in def.charIDs)
            {
                if (characters.TryGetValue(cid, out var ch))
                    wave.charactersInWave.Add(ch);
                else
                    Debug.LogWarning($"[SALIN-55] BaybayinCharacterSO not found for id: {cid}");
            }

            AssetDatabase.CreateAsset(wave, assetPath);
            waves.Add(AssetDatabase.LoadAssetAtPath<WaveConfigSO>(assetPath));
            Debug.Log($"[SALIN-55] Created wave: {def.id}");
        }

        AssetDatabase.SaveAssets();
        return waves;
    }

    private static void WireLevelConfig(
        int levelNumber,
        string levelName,
        List<WaveConfigSO> waves,
        Dictionary<string, BaybayinCharacterSO> characters,
        bool isBossLevel,
        BossConfigSO bossConfig)
    {
        string assetPath = $"{LevelsFolder}/Level{levelNumber}_Config.asset";
        var config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(assetPath);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.levelNumber = levelNumber;
            AssetDatabase.CreateAsset(config, assetPath);
            config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(assetPath);
            Debug.Log($"[SALIN-55] Created Level{levelNumber}_Config");
        }

        Undo.RecordObject(config, $"Wire Level{levelNumber}_Config");

        config.levelName = levelName;
        config.levelNumber = levelNumber;
        config.isAvailableInLite = false;
        config.isBossLevel = isBossLevel;
        config.bossConfig = bossConfig;

        config.waves = waves != null ? new List<WaveConfigSO>(waves) : new List<WaveConfigSO>();

        config.allowedCharacters = new List<BaybayinCharacterSO>();
        foreach (string cid in AllCharacterIDs)
        {
            if (characters.TryGetValue(cid, out var ch))
                config.allowedCharacters.Add(ch);
        }

        EditorUtility.SetDirty(config);
        Debug.Log($"[SALIN-55] Wired Level{levelNumber}_Config: {levelName} ({config.waves.Count} waves, boss={isBossLevel}, {config.allowedCharacters.Count} chars)");
    }

    private static T LoadAssetByName<T>(string assetName) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}");

        foreach (string guid in guids)
        {
            string apath = AssetDatabase.GUIDToAssetPath(guid);
            if (apath.Contains(assetName))
                return AssetDatabase.LoadAssetAtPath<T>(apath);
        }

        return null;
    }
}
#endif
