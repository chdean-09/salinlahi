using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the enemy prefabs the two component-backed corruption abilities need,
/// and registers them on the EnemyPool manager prefab.
///
/// EnemyPool.Get resolves enemyID to a registered prefab and falls back to a
/// single shared one, which carries only EnemyGlyphBadge + EnemyMover + Enemy.
/// So data alone cannot give a corrupted form a phasing or zigzag behaviour the
/// way it can give it a decoy flag: PhaserEnemy and PensionadoMover are
/// components, and they have to exist on the spawned object. This mirrors how
/// Fraile (PhaserEnemy) and Pensionado (PensionadoMover) are built.
///
/// Only Labo and Daan-Lihis are covered, matching the ability assignments in
/// CorruptionStatsAuthoringTool. Both prefabs are variants of the shared Soldado
/// prefab so they inherit its collider, badge wiring and sorting untouched.
/// </summary>
public static class CorruptionPrefabRegistrationTool
{
    private const string BasePrefabPath = "Assets/Prefabs/Enemies/[Enemy] Soldado.prefab";
    private const string PoolPrefabPath = "Assets/Prefabs/Managers/[Manager] EnemyPool.prefab";
    private const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";

    private sealed class Target
    {
        public string EnemyID;
        public string PrefabName;
        public System.Type Component;
    }

    private static readonly Target[] Targets =
    {
        new Target { EnemyID = "labo",       PrefabName = "[Enemy] Labo",       Component = typeof(PhaserEnemy) },
        new Target { EnemyID = "daan-lihis", PrefabName = "[Enemy] Daan-Lihis", Component = typeof(PensionadoMover) },
    };

    [MenuItem("Salinlahi/Art/Register Corruption Prefabs")]
    public static void Run()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError($"[CorruptionPrefabs] Base prefab missing: {BasePrefabPath}");
            return;
        }

        var poolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoolPrefabPath);
        EnemyPool pool = poolPrefab != null ? poolPrefab.GetComponent<EnemyPool>() : null;
        if (pool == null)
        {
            Debug.LogError($"[CorruptionPrefabs] EnemyPool missing: {PoolPrefabPath}");
            return;
        }

        var created = new List<(string enemyID, GameObject prefab)>();

        foreach (Target target in Targets)
        {
            string path = $"{EnemyPrefabFolder}/{target.PrefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                Debug.Log($"[CorruptionPrefabs] {target.PrefabName} already exists — reusing.");
                created.Add((target.EnemyID, existing));
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                if (instance.GetComponent(target.Component) == null)
                    instance.AddComponent(target.Component);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                if (saved == null)
                {
                    Debug.LogError($"[CorruptionPrefabs] Failed to save {path}");
                    continue;
                }
                created.Add((target.EnemyID, saved));
                Debug.Log($"[CorruptionPrefabs] Created {path} with {target.Component.Name}.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        int registered = RegisterAll(pool, created);
        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();

        int sceneRegistered = RegisterInScenes(created);

        Debug.Log($"[CorruptionPrefabs] Done. prefabs={created.Count} prefabRegistrations={registered} sceneRegistrations={sceneRegistered}");
    }

    /// <summary>
    /// The scene instances of the EnemyPool override
    /// <c>_registeredEnemyPrefabs.Array.size</c>, which pins the live list to its
    /// own length — growing the source prefab's array is therefore invisible at
    /// runtime ("EnemyPool: Unknown enemyID ... Falling back to default pool").
    /// That is how Pensionado and the Japanese roster were registered too, so the
    /// registration has to be repeated on each scene instance.
    /// </summary>
    private static int RegisterInScenes(List<(string enemyID, GameObject prefab)> created)
    {
        string[] scenePaths =
        {
            "Assets/_Scenes/Bootstrap.unity",
            "Assets/_Scenes/Gameplay.unity",
            "Assets/_Scenes/Level_01_Tutorial.unity",
        };

        int total = 0;
        foreach (string scenePath in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool dirty = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (EnemyPool pool in root.GetComponentsInChildren<EnemyPool>(true))
                {
                    int added = RegisterAll(pool, created);
                    if (added > 0)
                    {
                        EditorUtility.SetDirty(pool);
                        dirty = true;
                        total += added;
                        Debug.Log($"[CorruptionPrefabs] {scenePath}: added {added} registration(s).");
                    }
                }
            }

            if (dirty)
                EditorSceneManager.SaveScene(scene);
        }
        return total;
    }

    private static int RegisterAll(EnemyPool pool, List<(string enemyID, GameObject prefab)> created)
    {
        var so = new SerializedObject(pool);
        SerializedProperty list = so.FindProperty("_registeredEnemyPrefabs");
        if (list == null)
        {
            Debug.LogError("[CorruptionPrefabs] _registeredEnemyPrefabs not found on EnemyPool.");
            return 0;
        }

        int added = 0;
        foreach ((string enemyID, GameObject prefab) in created)
        {
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                string id = item.FindPropertyRelative("enemyID").stringValue;
                if (!string.IsNullOrWhiteSpace(id)
                    && id.Trim().ToLowerInvariant() == enemyID)
                {
                    present = true;
                    break;
                }
            }
            if (present)
            {
                Debug.Log($"[CorruptionPrefabs] '{enemyID}' already registered — left alone.");
                continue;
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("enemyID").stringValue = enemyID;
            // The registration holds an Enemy, not the GameObject.
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab.GetComponent<Enemy>();
            entry.FindPropertyRelative("defaultCapacity").intValue = 10;
            entry.FindPropertyRelative("maxSize").intValue = 20;
            added++;
            Debug.Log($"[CorruptionPrefabs] Registered '{enemyID}'.");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return added;
    }
}
