using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    public class WaveSpawnerSubsetTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            var trackerObject = new GameObject("ActiveEnemyTracker_WaveSpawner_Test");
            _tracker = trackerObject.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerObject);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void SelectCharacterForSpawn_ReturnsOnlyFromWaveSubset()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            BaybayinCharacterSO inSubset = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            EnemyDataSO enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            WaveDefinition wave = new()
            {
                characters = new List<BaybayinCharacterSO> { inSubset },
                enemyTypes = new List<EnemyDataSO> { enemy },
            };

            try
            {
                BaybayinCharacterSO result =
                    InvokePrivate(spawner, "SelectCharacterForSpawn", wave, enemy) as BaybayinCharacterSO;
                Assert.AreSame(inSubset, result);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(inSubset);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void SelectEnemyDataForSpawn_ReturnsOnlyFromWaveSubset()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            EnemyDataSO inSubset = ScriptableObject.CreateInstance<EnemyDataSO>();
            WaveDefinition wave = new() { enemyTypes = new List<EnemyDataSO> { inSubset } };

            try
            {
                EnemyDataSO result =
                    InvokePrivate(spawner, "SelectEnemyDataForSpawn", wave) as EnemyDataSO;
                Assert.AreSame(inSubset, result);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(inSubset);
            }
        }

        [Test]
        public void SelectCharacterForSpawn_FallsBackToAssignedCharacter_WhenSubsetEmpty()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            BaybayinCharacterSO assigned = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            EnemyDataSO enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.assignedCharacter = assigned;
            WaveDefinition wave = new() { characters = new List<BaybayinCharacterSO>() };

            try
            {
                BaybayinCharacterSO result =
                    InvokePrivate(spawner, "SelectCharacterForSpawn", wave, enemy) as BaybayinCharacterSO;
                Assert.AreSame(assigned, result);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(assigned);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void CanSpawnEnemyNow_ActiveCapIncludesDyingEnemiesUntilReturn()
        {
            EnemyDataSO activeData = CreateEnemyData("active");
            Enemy active = RegisterEnemy(activeData);
            SetPrivateField(active, "_isDying", true);
            EnemyDataSO incoming = CreateEnemyData("incoming");
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.maxActiveEnemies = 1;
            _objectsToDestroy.Add(level);
            var spawnerObject = new GameObject("WaveSpawner_Cap_Test");
            _objectsToDestroy.Add(spawnerObject);
            WaveSpawner spawner = spawnerObject.AddComponent<WaveSpawner>();

            Assert.AreEqual(1, _tracker.ActiveCount);
            Assert.IsFalse((bool)InvokePrivate(spawner, "CanSpawnEnemyNow", incoming, level),
                "A dying enemy still occupies a field slot until it is returned to the pool.");
        }

        [Test]
        public void CanSpawnEnemyNow_RejectsConcurrentKadenaAndBlockerSources()
        {
            var spawnerObject = new GameObject("WaveSpawner_Blocker_Test");
            _objectsToDestroy.Add(spawnerObject);
            WaveSpawner spawner = spawnerObject.AddComponent<WaveSpawner>();
            LevelConfigSO earlyLevel = ScriptableObject.CreateInstance<LevelConfigSO>();
            earlyLevel.levelNumber = 1;
            _objectsToDestroy.Add(earlyLevel);

            EnemyDataSO kadena = CreateEnemyData("kadena");
            kadena.chainsNearestEnemy = true;
            Enemy activeKadena = RegisterEnemy(kadena);
            EnemyDataSO nextKadena = CreateEnemyData("kadena-copy");
            nextKadena.chainsNearestEnemy = true;
            Assert.IsFalse((bool)InvokePrivate(spawner, "CanSpawnEnemyNow", nextKadena, earlyLevel),
                "Only one Kadena may be active across the whole level set.");

            _tracker.Unregister(activeKadena);
            EnemyDataSO gapos = CreateEnemyData("gapos");
            gapos.learningAbility = EnemyLearningAbility.BoundPair;
            RegisterEnemy(gapos);
            EnemyDataSO bakod = CreateEnemyData("bakod");
            bakod.blocksEnemiesBehind = true;
            LevelConfigSO blockerLevel = ScriptableObject.CreateInstance<LevelConfigSO>();
            blockerLevel.levelNumber = 7;
            _objectsToDestroy.Add(blockerLevel);

            Assert.IsFalse((bool)InvokePrivate(spawner, "CanSpawnEnemyNow", bakod, blockerLevel),
                "L7–15 may have only one active Gapos/Kadena/Bakod blocker source.");
        }

        private EnemyDataSO CreateEnemyData(string id)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = id;
            data.maxHealth = 1;
            data.isDecoy = false;
            _objectsToDestroy.Add(data);
            return data;
        }

        private Enemy RegisterEnemy(EnemyDataSO data)
        {
            var enemyObject = new GameObject("ActiveEnemy_WaveSpawner_Test");
            _objectsToDestroy.Add(enemyObject);
            enemyObject.AddComponent<BoxCollider2D>();
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            SetPrivateField(enemy, "_data", data);
            _tracker.Register(enemy);
            return enemy;
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { null });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static object InvokePrivate(object target, string method, params object[] args)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"Missing method '{method}' on {target.GetType().Name}.");
            return m.Invoke(target, args);
        }
    }
}
