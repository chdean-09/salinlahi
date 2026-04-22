using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class DecoyEnemyEdgeCaseTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<ComboManager>();
            ClearSingletonInstance<EnemyPool>();
            var trackerGo = new GameObject("ActiveEnemyTracker_Edge_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
            RecognitionLogger.ClearLog();
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<EnemyPool>();
            ClearSingletonInstance<ComboManager>();
            RecognitionLogger.ClearLog();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void EnemyMover_DecoyContact_DoesNotRaiseBaseHit_AndLogsIgnored()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyData(assigned, isDecoy: true, dealsContactDamage: false, enemyID: "maestro");
            Enemy enemy = pool.Get(data);
            Assert.IsNotNull(enemy);

            BoxCollider2D baseCollider = CreatePlayerBaseCollider();
            int baseHitDamage = 0;
            EventBus.OnBaseHit += HandleBaseHit;

            try
            {
                InvokePrivate<object>(
                    enemy.GetComponent<EnemyMover>(),
                    "OnTriggerEnter2D",
                    baseCollider);

                Assert.AreEqual(0, baseHitDamage);
                Assert.AreEqual(0, _tracker.ActiveCount);
                Assert.IsFalse(pool.IsCheckedOut(enemy));
                Assert.IsFalse(enemy.gameObject.activeInHierarchy);

                string csv = File.ReadAllText(GetRecognitionLogPath());
                StringAssert.Contains("decoy_ignored", csv);
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
            }

            void HandleBaseHit(int damage) => baseHitDamage += damage;
        }

        [Test]
        public void EnemyMover_NonDecoyContact_RaisesBaseHitOnce()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyData(assigned, isDecoy: false, dealsContactDamage: true, enemyID: "soldier");
            Enemy enemy = pool.Get(data);
            Assert.IsNotNull(enemy);

            BoxCollider2D baseCollider = CreatePlayerBaseCollider();
            int baseHitDamage = 0;
            EventBus.OnBaseHit += HandleBaseHit;

            try
            {
                InvokePrivate<object>(
                    enemy.GetComponent<EnemyMover>(),
                    "OnTriggerEnter2D",
                    baseCollider);

                Assert.AreEqual(1, baseHitDamage);
                Assert.AreEqual(0, _tracker.ActiveCount);
                Assert.IsFalse(pool.IsCheckedOut(enemy));
                Assert.IsFalse(enemy.gameObject.activeInHierarchy);
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
            }

            void HandleBaseHit(int damage) => baseHitDamage += damage;
        }

        [Test]
        public void CombatResolver_DecoyPenalty_LogsDecoyPenaltyOutcome()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            CreateEnemy(assigned, isDecoy: true, yPosition: -1f);
            CombatResolver resolver = CreateResolver();
            CreatePlayerBaseWithHeartSystem();

            InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

            string csv = File.ReadAllText(GetRecognitionLogPath());
            StringAssert.Contains("decoy_penalty", csv);
        }

        [Test]
        public void Combo_DecoyOnlyStroke_BreaksExistingStreak()
        {
            ComboManager combo = CreateComboManager();
            CreatePlayerBaseWithHeartSystem();

            EventBus.RaiseEnemyTargeted(null);
            Assert.AreEqual(1, combo.CurrentStreak);

            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            CreateEnemy(assigned, isDecoy: true, yPosition: -1f);
            CombatResolver resolver = CreateResolver();

            InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

            Assert.AreEqual(0, combo.CurrentStreak);
        }

        [Test]
        public void CombatResolver_MixedBurst_CombosOnlyNonDecoys_AndPenalizesDecoys()
        {
            ComboManager combo = CreateComboManager();
            HeartSystem heartSystem = CreatePlayerBaseWithHeartSystem();
            CombatResolver resolver = CreateResolver();
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");

            CreateEnemy(assigned, isDecoy: false, yPosition: -1f);
            CreateEnemy(assigned, isDecoy: false, yPosition: -2f);
            CreateEnemy(assigned, isDecoy: true, yPosition: -3f);

            int baseHitDamage = 0;
            int defeatedCount = 0;
            EventBus.OnBaseHit += HandleBaseHit;
            EventBus.OnEnemyDefeated += HandleDefeated;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

                Assert.AreEqual(1, baseHitDamage);
                Assert.AreEqual(2, defeatedCount);
                Assert.AreEqual(2, combo.CurrentStreak);
                Assert.AreEqual(2, heartSystem.GetCurrentHearts());
                Assert.AreEqual(0, _tracker.ActiveCount);
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
                EventBus.OnEnemyDefeated -= HandleDefeated;
            }

            void HandleBaseHit(int damage) => baseHitDamage += damage;
            void HandleDefeated(BaybayinCharacterSO _) => defeatedCount++;
        }

        [Test]
        public void CombatResolver_DecoyOnlyBurst_AppliesPenaltyPerDecoy_WithoutDefeats()
        {
            ComboManager combo = CreateComboManager();
            HeartSystem heartSystem = CreatePlayerBaseWithHeartSystem();
            CombatResolver resolver = CreateResolver();
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");

            EventBus.RaiseEnemyTargeted(null);
            EventBus.RaiseEnemyTargeted(null);
            Assert.AreEqual(2, combo.CurrentStreak);

            CreateEnemy(assigned, isDecoy: true, yPosition: -1f);
            CreateEnemy(assigned, isDecoy: true, yPosition: -2f);
            CreateEnemy(assigned, isDecoy: true, yPosition: -3f);

            int baseHitDamage = 0;
            int defeatedCount = 0;
            EventBus.OnBaseHit += HandleBaseHit;
            EventBus.OnEnemyDefeated += HandleDefeated;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

                Assert.AreEqual(3, baseHitDamage);
                Assert.AreEqual(0, defeatedCount);
                Assert.AreEqual(0, combo.CurrentStreak);
                Assert.AreEqual(0, heartSystem.GetCurrentHearts());
                Assert.AreEqual(0, _tracker.ActiveCount);
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
                EventBus.OnEnemyDefeated -= HandleDefeated;
            }

            void HandleBaseHit(int damage) => baseHitDamage += damage;
            void HandleDefeated(BaybayinCharacterSO _) => defeatedCount++;
        }

        private BaybayinCharacterSO CreateCharacter(string characterID, string syllable)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterID;
            character.syllable = syllable;
            _objectsToDestroy.Add(character);
            return character;
        }

        private EnemyDataSO CreateEnemyData(
            BaybayinCharacterSO assignedCharacter,
            bool isDecoy,
            bool dealsContactDamage,
            string enemyID)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            data.assignedCharacter = assignedCharacter;
            data.isDecoy = isDecoy;
            data.dealsContactDamage = dealsContactDamage;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            _objectsToDestroy.Add(data);
            return data;
        }

        private Enemy CreateEnemy(
            BaybayinCharacterSO assignedCharacter,
            bool isDecoy,
            float yPosition)
        {
            EnemyDataSO data = CreateEnemyData(
                assignedCharacter,
                isDecoy,
                dealsContactDamage: !isDecoy,
                enemyID: isDecoy ? "maestro" : "soldier");

            var go = new GameObject("Enemy_Edge_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, yPosition, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            var enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private CombatResolver CreateResolver()
        {
            var go = new GameObject("CombatResolver_Edge_Test");
            _objectsToDestroy.Add(go);
            return go.AddComponent<CombatResolver>();
        }

        private ComboManager CreateComboManager()
        {
            var go = new GameObject("ComboManager_Edge_Test");
            go.SetActive(false);
            _objectsToDestroy.Add(go);

            var config = ScriptableObject.CreateInstance<GameConfigSO>();
            config.focusModeThreshold = 99;
            config.focusModeDuration = 2f;
            config.focusModeSpeedMultiplier = 0.5f;
            _objectsToDestroy.Add(config);

            var combo = go.AddComponent<ComboManager>();
            SetPrivateField(combo, "_config", config);
            go.SetActive(true);
            return combo;
        }

        private HeartSystem CreatePlayerBaseWithHeartSystem()
        {
            var go = new GameObject("PlayerBase_Edge_Test");
            _objectsToDestroy.Add(go);
            go.AddComponent<HeartSystem>();
            return go.AddComponent<PlayerBase>().GetComponent<HeartSystem>();
        }

        private Enemy CreateEnemyPrefab()
        {
            var prefabGo = new GameObject("EnemyPrefab_Edge_Test");
            prefabGo.SetActive(false);
            _objectsToDestroy.Add(prefabGo);

            prefabGo.AddComponent<SpriteRenderer>();
            prefabGo.AddComponent<BoxCollider2D>();
            prefabGo.AddComponent<EnemyMover>();
            Enemy enemy = prefabGo.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            return enemy;
        }

        private EnemyPool CreateEnemyPool(Enemy prefab)
        {
            var poolGo = new GameObject("EnemyPool_Edge_Test");
            poolGo.SetActive(false);
            _objectsToDestroy.Add(poolGo);

            var pool = poolGo.AddComponent<EnemyPool>();
            SetPrivateField(pool, "_enemyPrefab", prefab);
            SetPrivateField(pool, "_defaultCapacity", 0);
            SetPrivateField(pool, "_maxSize", 8);
            poolGo.SetActive(true);
            return pool;
        }

        private BoxCollider2D CreatePlayerBaseCollider()
        {
            var go = new GameObject("PlayerBaseCollider_Edge_Test");
            go.tag = "PlayerBase";
            _objectsToDestroy.Add(go);
            return go.AddComponent<BoxCollider2D>();
        }

        private static string GetRecognitionLogPath()
        {
            PropertyInfo filePath = typeof(RecognitionLogger).GetProperty(
                "FilePath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(filePath);
            return filePath.GetValue(null) as string;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            FieldInfo instanceField = typeof(Singleton<T>).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }
    }
}
