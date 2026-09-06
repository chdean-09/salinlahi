using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// A wave rolls each enemy type independently and its roster mixes moveSpeed (Level 6 spans
    /// 0.85-1.9), so a fast enemy rolled late catches the slow one ahead and the two stack into one
    /// unreadable silhouette. Spawning fastest-first removes that, because a later spawn is then
    /// never faster than the one ahead of it. These guard that ordering, and that turning it off
    /// restores the rolled order.
    /// </summary>
    public class WaveSpawnerSpawnOrderTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        private EnemyDataSO Enemy(string name, float speed)
        {
            EnemyDataSO data = Track(ScriptableObject.CreateInstance<EnemyDataSO>());
            data.name = name;
            data.enemyID = name;
            data.moveSpeed = speed;
            return data;
        }

        private WaveSpawner Spawner(bool fastestFirst)
        {
            GameObject obj = Track(new GameObject("WaveSpawner"));
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            typeof(WaveSpawner)
                .GetField("_spawnFastestFirst", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(spawner, fastestFirst);
            return spawner;
        }

        private static List<EnemyDataSO> BuildSpawnOrder(WaveSpawner spawner, WaveDefinition wave, int count)
        {
            MethodInfo method = typeof(WaveSpawner).GetMethod(
                "BuildSpawnOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "BuildSpawnOrder is missing; spawn ordering has been renamed or removed.");
            return (List<EnemyDataSO>)method.Invoke(spawner, new object[] { wave, count });
        }

        // Level 6's real speed spread: slow stone golems through fast fliers.
        private WaveDefinition MixedSpeedWave() => new()
        {
            enemyTypes = new List<EnemyDataSO>
            {
                Enemy("bakod", 0.85f), Enemy("kadena", 1.05f), Enemy("punit", 1.3f),
                Enemy("hati", 1.5f), Enemy("ngatngat", 1.9f),
            },
        };

        [Test]
        public void BuildSpawnOrder_SpawnsFastestFirst()
        {
            WaveSpawner spawner = Spawner(fastestFirst: true);
            WaveDefinition wave = MixedSpeedWave();

            for (int seed = 0; seed < 50; seed++)
            {
                Random.InitState(seed);
                List<EnemyDataSO> order = BuildSpawnOrder(spawner, wave, 8);

                for (int i = 1; i < order.Count; i++)
                {
                    Assert.GreaterOrEqual(order[i - 1].moveSpeed, order[i].moveSpeed,
                        $"seed {seed}: '{order[i].name}' ({order[i].moveSpeed}) spawns after " +
                        $"'{order[i - 1].name}' ({order[i - 1].moveSpeed}) and would catch it");
                }
            }
        }

        [Test]
        public void BuildSpawnOrder_ReordersWithoutChangingWhatSpawns()
        {
            WaveDefinition wave = MixedSpeedWave();

            Random.InitState(1234);
            List<EnemyDataSO> rolled = BuildSpawnOrder(Spawner(fastestFirst: false), wave, 8);

            Random.InitState(1234);
            List<EnemyDataSO> ordered = BuildSpawnOrder(Spawner(fastestFirst: true), wave, 8);

            Assert.AreEqual(8, ordered.Count, "Ordering must not change how many enemies spawn.");
            CollectionAssert.AreEquivalent(rolled, ordered,
                "Ordering must only permute the wave, never change which enemies it contains.");
        }

        [Test]
        public void BuildSpawnOrder_Disabled_KeepsRolledOrder()
        {
            WaveDefinition wave = MixedSpeedWave();

            Random.InitState(99);
            List<EnemyDataSO> first = BuildSpawnOrder(Spawner(fastestFirst: false), wave, 8);

            Random.InitState(99);
            List<EnemyDataSO> second = BuildSpawnOrder(Spawner(fastestFirst: false), wave, 8);

            CollectionAssert.AreEqual(first, second, "Disabled ordering must be the plain rolled sequence.");

            // Negative control for the ordering test above: the rolled sequence is not already sorted,
            // so BuildSpawnOrder_SpawnsFastestFirst is testing real behaviour.
            bool alreadySorted = true;
            for (int i = 1; i < first.Count; i++)
                if (first[i - 1].moveSpeed < first[i].moveSpeed) { alreadySorted = false; break; }
            Assert.IsFalse(alreadySorted,
                "This seed happens to roll an already-sorted wave; pick another so the ordering test has teeth.");
        }

        [Test]
        public void BuildSpawnOrder_EqualSpeeds_KeepsRolledOrderSoWavesStayVaried()
        {
            // Every type shares a speed, so ordering has nothing to sort on. A stable sort must
            // leave the rolled sequence alone rather than collapsing every wave onto one arrangement.
            WaveDefinition wave = new()
            {
                enemyTypes = new List<EnemyDataSO>
                {
                    Enemy("a", 1.5f), Enemy("b", 1.5f), Enemy("c", 1.5f),
                },
            };

            Random.InitState(77);
            List<EnemyDataSO> rolled = BuildSpawnOrder(Spawner(fastestFirst: false), wave, 10);

            Random.InitState(77);
            List<EnemyDataSO> ordered = BuildSpawnOrder(Spawner(fastestFirst: true), wave, 10);

            CollectionAssert.AreEqual(rolled, ordered);
        }

        [Test]
        public void BuildSpawnOrder_SingleSpeedRoster_IsUnaffected()
        {
            // Eras 02-03 run one or two speeds; ordering must be a no-op there, not a reshuffle.
            WaveDefinition wave = new() { enemyTypes = new List<EnemyDataSO> { Enemy("heitai", 1.8f) } };

            Random.InitState(5);
            List<EnemyDataSO> order = BuildSpawnOrder(Spawner(fastestFirst: true), wave, 6);

            Assert.AreEqual(6, order.Count);
            Assert.IsTrue(order.All(d => d != null && Mathf.Approximately(d.moveSpeed, 1.8f)));
        }
    }
}
