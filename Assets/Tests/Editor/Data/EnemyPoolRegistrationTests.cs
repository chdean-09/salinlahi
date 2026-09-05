using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Guards the enemy-id contract between the level configs and the EnemyPool prefab.
    ///
    /// EnemyPool.ResolvePool falls back to the default prefab on an unknown id and only
    /// logs a warning, so a level can ship spawning enemies nobody registered and still
    /// look "fine" — every one of them just renders as the default creature. That is QA
    /// finding B-02 (docs/review/manual-qa-2026-09-05.md), and it survived to a manual
    /// pass precisely because nothing failed loudly.
    ///
    /// These tests read the assets as they sit on disk. They never call a bootstrap and
    /// never apply a SerializedObject edit, so running them cannot rewrite content.
    /// </summary>
    [TestFixture]
    public sealed class EnemyPoolRegistrationTests
    {
        private const string PoolPrefabPath = "Assets/Prefabs/Managers/[Manager] EnemyPool.prefab";

        /// <summary>
        /// Enemy ids a level spawns that the pool does not register; each silently falls
        /// back to the default prefab. Asserted for exact set equality so the list cannot
        /// drift in either direction: adding another unregistered enemy fails, and wiring
        /// one up without deleting its entry here fails too. Shrink this as art lands —
        /// when it is empty, delete it and the allowance with it.
        /// </summary>
        private static readonly string[] KnownUnregistered =
        {
            "abo-ng-simula", "bakod", "gapos", "hati", "iligaw", "kadena", "mantsa",
            "nawalang-mukha", "ngatngat", "punit", "salungat", "takip", "walang-awa",
        };

        private static string Normalise(string id) => (id ?? string.Empty).Trim().ToLowerInvariant();

        private static List<(string Id, bool HasPrefab)> ReadRegistrations()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoolPrefabPath);
            Assert.IsNotNull(prefab, $"EnemyPool prefab missing at {PoolPrefabPath}; update this test if it moved.");

            var pool = prefab.GetComponent<EnemyPool>();
            Assert.IsNotNull(pool, "EnemyPool component missing from its own manager prefab.");

            var so = new SerializedObject(pool);
            SerializedProperty list = so.FindProperty("_registeredEnemyPrefabs");
            Assert.IsNotNull(list, "_registeredEnemyPrefabs not found; the field was renamed without updating this test.");

            var result = new List<(string, bool)>(list.arraySize);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                result.Add((
                    Normalise(entry.FindPropertyRelative("enemyID").stringValue),
                    entry.FindPropertyRelative("prefab").objectReferenceValue != null));
            }
            return result;
        }

        private static Dictionary<string, List<string>> EnemyIdsByLevel()
        {
            var byLevel = new Dictionary<string, List<string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:LevelConfigSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
                if (level == null || level.waves == null) continue;

                var ids = new List<string>();
                foreach (WaveDefinition wave in level.waves)
                {
                    if (wave?.enemyTypes == null) continue;
                    foreach (EnemyDataSO enemy in wave.enemyTypes)
                    {
                        if (enemy == null) continue;
                        string id = Normalise(enemy.enemyID);
                        if (id.Length > 0 && !ids.Contains(id)) ids.Add(id);
                    }
                }
                if (ids.Count > 0) byLevel[System.IO.Path.GetFileNameWithoutExtension(path)] = ids;
            }
            Assert.IsNotEmpty(byLevel, "No LevelConfigSO assets resolved — the query or the asset layout changed.");
            return byLevel;
        }

        [Test]
        public void EveryEnemyIdSpawnedByALevel_IsRegisteredOrAKnownGap()
        {
            var registered = ReadRegistrations().Select(r => r.Id).ToHashSet();
            var byLevel = EnemyIdsByLevel();

            var missing = new SortedSet<string>();
            var offenders = new List<string>();
            foreach (KeyValuePair<string, List<string>> level in byLevel.OrderBy(l => l.Key))
            {
                string[] gaps = level.Value.Where(id => !registered.Contains(id)).ToArray();
                if (gaps.Length == 0) continue;
                foreach (string id in gaps) missing.Add(id);
                offenders.Add($"  {level.Key}: {gaps.Length}/{level.Value.Count} unregistered — {string.Join(", ", gaps)}");
            }

            CollectionAssert.AreEqual(
                KnownUnregistered.Select(Normalise).OrderBy(id => id).ToArray(),
                missing.ToArray(),
                "The set of enemy ids that fall back to the default prefab has changed.\n"
                + "If you added an enemy to a level, register its prefab in the EnemyPool.\n"
                + "If you registered one, delete it from KnownUnregistered.\n"
                + "Levels currently affected:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void EveryRegisteredId_IsAuthoredByAnEnemyDataAsset()
        {
            var authored = AssetDatabase.FindAssets("t:EnemyDataSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EnemyDataSO>)
                .Where(e => e != null)
                .Select(e => Normalise(e.enemyID))
                .ToHashSet();

            string[] orphans = ReadRegistrations()
                .Select(r => r.Id)
                .Where(id => id.Length > 0 && !authored.Contains(id))
                .ToArray();

            CollectionAssert.IsEmpty(orphans,
                "The pool registers ids no EnemyDataSO authors, so the registration can never "
                + $"be reached: {string.Join(", ", orphans)}. Remove the row or add the data asset.");
        }

        [Test]
        public void PoolRegistrations_HaveNoBlanksDuplicatesOrMissingPrefabs()
        {
            List<(string Id, bool HasPrefab)> registrations = ReadRegistrations();
            Assert.IsNotEmpty(registrations, "The EnemyPool registers nothing at all.");

            CollectionAssert.IsEmpty(
                registrations.Where(r => r.Id.Length == 0).ToArray(),
                "A registration has a blank enemyID; EnemyPool skips it at runtime with only a warning.");

            string[] duplicates = registrations.GroupBy(r => r.Id)
                .Where(g => g.Key.Length > 0 && g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            CollectionAssert.IsEmpty(duplicates,
                $"Duplicate enemyID registrations are silently ignored: {string.Join(", ", duplicates)}");

            string[] prefabless = registrations.Where(r => !r.HasPrefab).Select(r => r.Id).ToArray();
            CollectionAssert.IsEmpty(prefabless,
                $"Registrations with no prefab are skipped at runtime: {string.Join(", ", prefabless)}");
        }
    }
}
