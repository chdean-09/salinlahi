using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Retires the twelve colonial enemies (Soldado, Soldier, Fraile, Guardia, Capitan, General,
/// Maestro, Pensionado, Heitai, Kempei, Kisha, Shokan) by moving every level wave, combat roster
/// and boss summon onto the corruption roster, then leaves the colonial assets unreferenced so
/// they can be deleted.
///
/// Why a tool and not hand-edited YAML: <see cref="LevelConfigSO"/> prunes each wave's enemyTypes
/// against allowedEnemyTypes in OnValidate, so the roster and the waves have to change together
/// or the edit silently reverts. Doing it through SerializedObject also keeps the file diffs
/// minimal and the mapping reviewable in one place.
///
/// Replacement principle: a level's enemies are the corruptions of the symbols that level teaches.
/// Each level keeps the enemy-variety count it was authored with (Level 11 stays at one type,
/// Level 14 at four), and every replacement's assignedCharacter is inside that level's
/// allowedCharacters — so a wave that falls back to an enemy's default glyph can no longer demand
/// an untaught symbol. That was already wrong on Level 6, which fielded HA/KA/LA/NGA/PA/SA
/// corruptions in an era that teaches none of them.
///
/// Idempotent: re-running rewrites the same references rather than duplicating them. It can be
/// retired once merged, as the SALIN-212 registry tool was.
/// </summary>
public static class ColonialRosterRetirementTool
{
    private const string MenuPath = "Salinlahi/Campaign/Retire Colonial Enemy Roster";
    private const string EnemyFolder = "Assets/ScriptableObjects/Enemies";
    private const string CorruptedShellPrefab = "Assets/Prefabs/Enemies/[Enemy] Corrupted.prefab";

    private static readonly string[] Colonial =
    {
        "Soldado", "Soldier", "Fraile", "Guardia", "Capitan", "General",
        "Maestro", "Pensionado", "Heitai", "Kempei", "Kisha", "Shokan",
    };

    /// <summary>Per-level: the corruption enemies that replace its colonial ones, in wave order.</summary>
    private sealed class LevelPlan
    {
        public string StableId;
        public string[] Roster;                          // the level's new allowedEnemyTypes
        public Dictionary<string, string> Replacements;  // colonial name -> corruption name
    }

    private static readonly LevelPlan[] Plans =
    {
        // Ugat teaches EI NA A MA (+ BA TA from Level 2).
        new LevelPlan
        {
            StableId = "level.ugat.01",
            Roster = new[] { "Iligaw", "NawalangMukha", "AbongSimula", "Mantsa" },
            Replacements = new Dictionary<string, string>(),   // waves were already clean
        },
        new LevelPlan
        {
            StableId = "level.ugat.02",
            Roster = new[] { "Bakod", "Takip", "Mantsa" },
            Replacements = new Dictionary<string, string> { { "Soldado", "Takip" }, { "Fraile", "Mantsa" } },
        },
        new LevelPlan
        {
            StableId = "level.ugat.03",
            Roster = new[] { "AbongSimula", "Iligaw", "Bakod", "Mantsa", "NawalangMukha", "Takip" },
            Replacements = new Dictionary<string, string>
                { { "Soldado", "Takip" }, { "Fraile", "Mantsa" }, { "Guardia", "AbongSimula" } },
        },
        new LevelPlan
        {
            StableId = "level.ugat.04",
            Roster = new[] { "Iligaw", "NawalangMukha", "AbongSimula", "Mantsa" },
            Replacements = new Dictionary<string, string>
                { { "Soldado", "Iligaw" }, { "Fraile", "NawalangMukha" }, { "Capitan", "AbongSimula" }, { "Guardia", "Mantsa" } },
        },
        // Ugnayan adds KA GA SA WA YA OU.
        new LevelPlan
        {
            StableId = "level.ugnayan.01",
            Roster = new[]
            {
                "Bakod", "Gapos", "Kadena", "Mantsa", "Hati", "Labo",
                "NawalangMukha", "Ngatngat", "Punit", "Salungat", "Takip", "Walang-Awa",
            },
            Replacements = new Dictionary<string, string>(),   // only the roster carried Soldier
        },
        new LevelPlan
        {
            StableId = "level.ugnayan.02",   // SAMA, KASAMA
            Roster = new[] { "Salungat", "Kadena" },
            Replacements = new Dictionary<string, string> { { "Soldier", "Salungat" }, { "Maestro", "Kadena" } },
        },
        new LevelPlan
        {
            StableId = "level.ugnayan.03",   // GANA, KAYA
            Roster = new[] { "Gapos", "YaposngDilim" },
            Replacements = new Dictionary<string, string> { { "Soldier", "Gapos" }, { "Maestro", "YaposngDilim" } },
        },
        new LevelPlan
        {
            StableId = "level.ugnayan.04",   // OO, UNA
            Roster = new[] { "Uhaw", "NawalangMukha" },
            Replacements = new Dictionary<string, string> { { "Soldier", "Uhaw" }, { "Maestro", "NawalangMukha" } },
        },
        // Pamana adds DA HA LA NGA PA.
        new LevelPlan
        {
            StableId = "level.pamana.01",    // DALA, DAMA
            Roster = new[] { "Daan-Lihis" },
            Replacements = new Dictionary<string, string> { { "Heitai", "Daan-Lihis" } },
        },
        new LevelPlan
        {
            StableId = "level.pamana.02",    // HANGA, HALAGA
            Roster = new[] { "Hati", "Ngatngat" },
            Replacements = new Dictionary<string, string> { { "Heitai", "Hati" }, { "Kisha", "Ngatngat" } },
        },
        new LevelPlan
        {
            StableId = "level.pamana.03",    // sentence level: SANGA, HARAYA (SALIN-155)
            Roster = new[] { "Hati", "Salungat", "Ngatngat" },
            Replacements = new Dictionary<string, string>
                { { "Heitai", "Hati" }, { "Kisha", "Salungat" }, { "Kempei", "Ngatngat" } },
        },
        new LevelPlan
        {
            StableId = "level.pamana.04",    // ALAALA, MAHALAGA
            Roster = new[] { "Hati", "Labo", "Mantsa", "AbongSimula" },
            Replacements = new Dictionary<string, string>
                { { "Heitai", "Hati" }, { "Kisha", "Labo" }, { "Kempei", "Mantsa" }, { "Shokan", "AbongSimula" } },
        },
    };

    /// <summary>Each boss's fallbackEnemyTypes, used when a phase's own summon list is empty.</summary>
    private static readonly Dictionary<string, string[]> BossFallbacks = new()
    {
        ["BossConfig_ElInquisidor"] = new[] { "NawalangMukha" },
        ["BossConfig_Kadiliman"] = new[] { "NawalangMukha", "Salungat", "Hati" },
    };

    /// <summary>Boss summon waves, era by era, so each phase still escalates.</summary>
    private static readonly Dictionary<string, string[][]> BossSummons = new()
    {
        // Level 5 Paglimot: the Ugat corruptions the player has faced.
        ["BossConfig_ElInquisidor"] = new[]
        {
            new[] { "NawalangMukha" },
            new[] { "Iligaw", "NawalangMukha", "AbongSimula" },
            new[] { "NawalangMukha", "Iligaw", "AbongSimula", "Mantsa" },
        },
        // Level 15 Paglimot: one phase per era, then all three together.
        ["BossConfig_Kadiliman"] = new[]
        {
            new[] { "NawalangMukha", "Iligaw" },
            new[] { "Salungat", "Kadena", "Gapos" },
            new[] { "Hati", "Labo", "Ngatngat" },
            new[] { "AbongSimula", "Mantsa", "Punit", "NawalangMukha", "Hati" },
        },
    };

    [MenuItem(MenuPath)]
    public static void Retire()
    {
        Dictionary<string, EnemyDataSO> enemies = LoadEnemies();
        Dictionary<string, LevelConfigSO> levels = LoadLevels();
        var log = new List<string>();
        bool failed = false;

        foreach (LevelPlan plan in Plans)
        {
            if (!levels.TryGetValue(plan.StableId, out LevelConfigSO level) || level == null)
            {
                Debug.LogError($"COLONIAL-RETIRE: no LevelConfigSO with stableId '{plan.StableId}'.");
                failed = true;
                continue;
            }

            var so = new SerializedObject(level);
            int wavesChanged = 0;

            SerializedProperty waves = so.FindProperty("waves");
            for (int w = 0; w < waves.arraySize; w++)
            {
                SerializedProperty types = waves.GetArrayElementAtIndex(w).FindPropertyRelative("enemyTypes");
                var replaced = new List<EnemyDataSO>();
                bool changed = false;

                for (int i = 0; i < types.arraySize; i++)
                {
                    var current = types.GetArrayElementAtIndex(i).objectReferenceValue as EnemyDataSO;
                    string name = current != null ? current.name.Replace("EnemyData_", string.Empty) : null;

                    if (name != null && plan.Replacements.TryGetValue(name, out string replacement))
                    {
                        EnemyDataSO next = Resolve(enemies, replacement, ref failed);
                        // A wave that already fields the replacement keeps one copy of it.
                        if (next != null && !replaced.Contains(next))
                            replaced.Add(next);
                        changed = true;
                    }
                    else if (current != null && !replaced.Contains(current))
                    {
                        replaced.Add(current);
                    }
                }

                if (!changed)
                    continue;

                types.ClearArray();
                for (int i = 0; i < replaced.Count; i++)
                {
                    types.InsertArrayElementAtIndex(i);
                    types.GetArrayElementAtIndex(i).objectReferenceValue = replaced[i];
                }

                wavesChanged++;
            }

            // The roster has to be written in the same apply as the waves: OnValidate prunes each
            // wave against it, so a stale roster would delete the replacements we just made.
            SerializedProperty roster = so.FindProperty("allowedEnemyTypes");
            roster.ClearArray();
            for (int i = 0; i < plan.Roster.Length; i++)
            {
                EnemyDataSO entry = Resolve(enemies, plan.Roster[i], ref failed);
                roster.InsertArrayElementAtIndex(i);
                roster.GetArrayElementAtIndex(i).objectReferenceValue = entry;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            log.Add($"{plan.StableId}: {wavesChanged} wave(s) rewritten, roster={string.Join("/", plan.Roster)}");
        }

        foreach (KeyValuePair<string, string[][]> boss in BossSummons)
            failed |= !RetireBoss(boss.Key, boss.Value, enemies, log);

        failed |= !RepointCorruptedShell(enemies, log);
        failed |= !CleanPoolPrefab(log);
        failed |= !RepointScenes(enemies, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        foreach (string line in log)
            Debug.Log("COLONIAL-RETIRE: " + line);

        Debug.Log(failed
            ? "COLONIALRETIRE: FINISHED WITH ERRORS - see the errors above; do not delete anything yet."
            : $"COLONIALRETIRE: OK levels={Plans.Length} bosses={BossSummons.Count} remainingColonialRefs={CountRemainingReferences()}");
    }

    private static bool RetireBoss(
        string assetName, string[][] phases, Dictionary<string, EnemyDataSO> enemies, List<string> log)
    {
        string[] guids = AssetDatabase.FindAssets($"t:BossConfigSO {assetName}");
        if (guids.Length == 0)
        {
            Debug.LogError($"COLONIAL-RETIRE: no BossConfigSO named '{assetName}'.");
            return false;
        }

        var boss = AssetDatabase.LoadAssetAtPath<BossConfigSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        var so = new SerializedObject(boss);
        SerializedProperty phaseList = so.FindProperty("phases");
        bool ok = true;

        if (phaseList.arraySize != phases.Length)
        {
            Debug.LogError(
                $"COLONIAL-RETIRE: {assetName} has {phaseList.arraySize} phases, the plan covers {phases.Length}. Not touching it.");
            return false;
        }

        for (int p = 0; p < phaseList.arraySize; p++)
        {
            SerializedProperty summons = phaseList.GetArrayElementAtIndex(p).FindPropertyRelative("summonEnemyTypes");
            summons.ClearArray();
            for (int i = 0; i < phases[p].Length; i++)
            {
                EnemyDataSO entry = Resolve(enemies, phases[p][i], ref ok);
                summons.InsertArrayElementAtIndex(i);
                summons.GetArrayElementAtIndex(i).objectReferenceValue = entry;
            }
        }

        if (BossFallbacks.TryGetValue(assetName, out string[] fallbacks))
        {
            SerializedProperty list = so.FindProperty("fallbackEnemyTypes");
            list.ClearArray();
            for (int i = 0; i < fallbacks.Length; i++)
            {
                EnemyDataSO entry = Resolve(enemies, fallbacks[i], ref ok);
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = entry;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(boss);
        log.Add($"{assetName}: {phases.Length} phase(s) resummoned, fallback={string.Join("/", fallbacks ?? new string[0])}");
        return ok;
    }

    /// <summary>
    /// The shared corruption shell shipped with Soldado as its default EnemyDataSO, so every
    /// prefab-less corruption enemy depended on a colonial asset.
    /// </summary>
    private static bool RepointCorruptedShell(Dictionary<string, EnemyDataSO> enemies, List<string> log)
    {
        var shell = AssetDatabase.LoadAssetAtPath<GameObject>(CorruptedShellPrefab);
        if (shell == null)
        {
            Debug.LogError($"COLONIAL-RETIRE: missing {CorruptedShellPrefab}.");
            return false;
        }

        var enemy = shell.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogError("COLONIAL-RETIRE: corruption shell has no Enemy component.");
            return false;
        }

        bool ok = true;
        EnemyDataSO fallback = Resolve(enemies, "AbongSimula", ref ok);
        var so = new SerializedObject(enemy);
        so.FindProperty("_data").objectReferenceValue = fallback;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(shell);
        log.Add("[Enemy] Corrupted.prefab: _data -> EnemyData_AbongSimula");
        return ok;
    }

    /// <summary>
    /// The EnemyPool manager prefab registers a pooled prefab per enemy id. Ten of its thirteen
    /// entries were colonial and would become null references once those prefabs are deleted;
    /// the corruption roster is prefab-less and resolves through the default pool instead.
    /// </summary>
    private static bool CleanPoolPrefab(List<string> log)
    {
        const string path = "Assets/Prefabs/Managers/[Manager] EnemyPool.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"COLONIAL-RETIRE: missing {path}.");
            return false;
        }

        var pool = prefab.GetComponent<EnemyPool>();
        if (pool == null)
        {
            Debug.LogError("COLONIAL-RETIRE: EnemyPool component missing from its own manager prefab.");
            return false;
        }

        var so = new SerializedObject(pool);
        SerializedProperty list = so.FindProperty("_registeredEnemyPrefabs");
        var removed = new List<string>();

        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("enemyID").stringValue ?? string.Empty;
            var prefabRef = entry.FindPropertyRelative("prefab").objectReferenceValue;
            bool colonial = Colonial.Any(c => string.Equals(c, id, System.StringComparison.OrdinalIgnoreCase))
                || (prefabRef != null && Colonial.Any(c => prefabRef.name.IndexOf(c, System.StringComparison.OrdinalIgnoreCase) >= 0));

            if (!colonial)
                continue;

            removed.Add(id);
            list.DeleteArrayElementAtIndex(i);
        }

        if (removed.Count == 0)
        {
            log.Add("[Manager] EnemyPool.prefab: already clean");
            return true;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
        removed.Reverse();
        log.Add($"[Manager] EnemyPool.prefab: removed {removed.Count} registration(s) — {string.Join(", ", removed)}");
        return true;
    }

    /// <summary>
    /// Gameplay.unity holds the spawner's fallback data and the pool's prefab registrations; both
    /// still pointed at colonial assets.
    /// </summary>
    private static bool RepointScenes(Dictionary<string, EnemyDataSO> enemies, List<string> log)
    {
        bool ok = true;
        EnemyDataSO fallback = Resolve(enemies, "AbongSimula", ref ok);
        string[] scenes = { "Assets/_Scenes/Gameplay.unity", "Assets/_Scenes/Level_01_Tutorial.unity" };

        foreach (string path in scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool dirty = false;

            foreach (WaveManager manager in Object.FindObjectsByType<WaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(manager);
                SerializedProperty prop = so.FindProperty("_fallbackEnemyData");
                var current = prop.objectReferenceValue as EnemyDataSO;
                if (current == null || !IsColonial(current))
                    continue;

                prop.objectReferenceValue = fallback;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
                log.Add($"{System.IO.Path.GetFileName(path)}: WaveManager._fallbackEnemyData -> EnemyData_AbongSimula");
            }

            foreach (EnemyPool pool in Object.FindObjectsByType<EnemyPool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(pool);
                SerializedProperty registrations = so.FindProperty("_registeredEnemyPrefabs");
                int removed = 0;

                for (int i = registrations.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty entry = registrations.GetArrayElementAtIndex(i);
                    var prefab = entry.FindPropertyRelative("prefab").objectReferenceValue as Enemy;
                    string id = entry.FindPropertyRelative("enemyID").stringValue ?? string.Empty;
                    bool colonialId = Colonial.Any(c => string.Equals(c, id, System.StringComparison.OrdinalIgnoreCase));
                    bool colonialPrefab = prefab != null
                        && Colonial.Any(c => prefab.name.IndexOf(c, System.StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!colonialId && !colonialPrefab)
                        continue;

                    registrations.DeleteArrayElementAtIndex(i);
                    removed++;
                }

                if (removed == 0)
                    continue;

                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
                log.Add($"{System.IO.Path.GetFileName(path)}: removed {removed} colonial pool registration(s)");
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        return ok;
    }

    private static bool IsColonial(Object asset)
    {
        string name = asset.name.Replace("EnemyData_", string.Empty);
        return Colonial.Any(c => string.Equals(c, name, System.StringComparison.OrdinalIgnoreCase));
    }

    private static EnemyDataSO Resolve(Dictionary<string, EnemyDataSO> enemies, string name, ref bool ok)
    {
        if (enemies.TryGetValue(name, out EnemyDataSO data) && data != null)
            return data;

        Debug.LogError($"COLONIAL-RETIRE: no EnemyData_{name}.asset.");
        ok = false;
        return null;
    }

    private static Dictionary<string, EnemyDataSO> LoadEnemies()
    {
        var map = new Dictionary<string, EnemyDataSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyDataSO", new[] { EnemyFolder }))
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null)
                map[data.name.Replace("EnemyData_", string.Empty)] = data;
        }

        return map;
    }

    private static Dictionary<string, LevelConfigSO> LoadLevels()
    {
        var map = new Dictionary<string, LevelConfigSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:LevelConfigSO"))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (level != null && !string.IsNullOrEmpty(level.stableId))
                map[level.stableId] = level;
        }

        return map;
    }

    /// <summary>Counts level/boss references still pointing at a colonial enemy, so the run can prove it finished.</summary>
    private static int CountRemainingReferences()
    {
        int count = 0;

        foreach (LevelConfigSO level in LoadLevels().Values)
        {
            if (level.allowedEnemyTypes != null)
                count += level.allowedEnemyTypes.Count(e => e != null && IsColonial(e));

            if (level.waves == null)
                continue;

            foreach (WaveDefinition wave in level.waves)
            {
                if (wave?.enemyTypes != null)
                    count += wave.enemyTypes.Count(e => e != null && IsColonial(e));
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:BossConfigSO"))
        {
            var boss = AssetDatabase.LoadAssetAtPath<BossConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (boss?.phases == null)
                continue;

            foreach (BossPhase phase in boss.phases)
            {
                if (phase?.summonEnemyTypes != null)
                    count += phase.summonEnemyTypes.Count(e => e != null && IsColonial(e));
            }

            if (boss.fallbackEnemyTypes != null)
                count += boss.fallbackEnemyTypes.Count(e => e != null && IsColonial(e));
        }

        return count;
    }
}
