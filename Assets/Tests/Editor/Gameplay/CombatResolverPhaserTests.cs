using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class CombatResolverPhaserTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            var trackerGo = new GameObject("ActiveEnemyTracker_Phaser_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<GameManager>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void SingleTarget_SkipsInvisiblePhaser_AndTargetsNextMatch()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy invisiblePhaser = CreateEnemy(assigned, y: -5f, isPhaser: true);
            Enemy fallback = CreateEnemy(assigned, y: -1f, isPhaser: false);
            SetPhaserVisible(invisiblePhaser, false);

            CombatResolver resolver = CreateResolver();
            Enemy targeted = null;
            EventBus.OnEnemyTargeted += HandleTargeted;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

                Assert.AreSame(fallback, targeted);
                Assert.AreEqual(1, invisiblePhaser.CurrentHealth);
                Assert.AreEqual(0, fallback.CurrentHealth);
            }
            finally
            {
                EventBus.OnEnemyTargeted -= HandleTargeted;
            }

            void HandleTargeted(Enemy enemy) => targeted = enemy;
        }

        [Test]
        public void SingleTarget_AllMatchesInvisiblePhaser_RaisesDrawingMissedOnly()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy invisiblePhaser = CreateEnemy(assigned, y: -2f, isPhaser: true);
            SetPhaserVisible(invisiblePhaser, false);
            CombatResolver resolver = CreateResolver();

            bool missed = false;
            bool failed = false;
            EventBus.OnDrawingMissed += HandleMissed;
            EventBus.OnDrawingFailed += HandleFailed;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);
                Assert.IsTrue(missed);
                Assert.IsFalse(failed);
                Assert.AreEqual(1, invisiblePhaser.CurrentHealth);
            }
            finally
            {
                EventBus.OnDrawingMissed -= HandleMissed;
                EventBus.OnDrawingFailed -= HandleFailed;
            }

            void HandleMissed() => missed = true;
            void HandleFailed() => failed = true;
        }

        [Test]
        public void AOE_ExcludesInvisiblePhasers()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy visible1 = CreateEnemy(assigned, y: -1f, isPhaser: false);
            Enemy visible2 = CreateEnemy(assigned, y: -2f, isPhaser: false);
            Enemy visible3 = CreateEnemy(assigned, y: -3f, isPhaser: false);
            Enemy invisiblePhaser = CreateEnemy(assigned, y: -4f, isPhaser: true);
            SetPhaserVisible(invisiblePhaser, false);

            CombatResolver resolver = CreateResolver();
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

            Assert.AreEqual(0, visible1.CurrentHealth);
            Assert.AreEqual(0, visible2.CurrentHealth);
            Assert.AreEqual(0, visible3.CurrentHealth);
            Assert.AreEqual(1, invisiblePhaser.CurrentHealth);
        }

        [Test]
        public void AOE_WhenCurrentLevelDisablesMultiKillChain_ResolvesOnlyClosestMatch()
        {
            GameManager gameManager = CreateGameManager();
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            levelConfig.levelNumber = 1;
            levelConfig.multiKillChainEnabled = false;
            gameManager.SetLevel(levelConfig);
            _objectsToDestroy.Add(levelConfig);

            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy farthest = CreateEnemy(assigned, y: -1f, isPhaser: false);
            Enemy middle = CreateEnemy(assigned, y: -2f, isPhaser: false);
            Enemy closest = CreateEnemy(assigned, y: -3f, isPhaser: false);

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);

            int aoeTriggeredCount = 0;
            int chainStepCount = 0;
            EventBus.OnAOETriggered += HandleAoeTriggered;
            EventBus.OnChainAttackStep += HandleChainStep;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

                Assert.AreEqual(1, farthest.CurrentHealth);
                Assert.AreEqual(1, middle.CurrentHealth);
                Assert.AreEqual(0, closest.CurrentHealth);
                Assert.AreEqual(0, aoeTriggeredCount);
                Assert.AreEqual(0, chainStepCount);
            }
            finally
            {
                EventBus.OnAOETriggered -= HandleAoeTriggered;
                EventBus.OnChainAttackStep -= HandleChainStep;
            }

            void HandleAoeTriggered(int _) => aoeTriggeredCount++;
            void HandleChainStep(Enemy _) => chainStepCount++;
        }

        [Test]
        public void Awake_DestroysDuplicateResolverInstance()
        {
            CombatResolver first = CreateResolver();
            Assert.IsNotNull(first);

            var duplicateGo = new GameObject("CombatResolver_Duplicate_Test");
            _objectsToDestroy.Add(duplicateGo);
            CombatResolver duplicate = duplicateGo.AddComponent<CombatResolver>();

            Assert.IsTrue(duplicate == null);
            Assert.AreEqual(1, Object.FindObjectsByType<CombatResolver>(FindObjectsSortMode.None).Length);
        }

        private BaybayinCharacterSO CreateCharacter(string id, string syllable)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.syllable = syllable;
            _objectsToDestroy.Add(character);
            return character;
        }

        private Enemy CreateEnemy(BaybayinCharacterSO assigned, float y, bool isPhaser)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.assignedCharacter = assigned;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.isPhaser = isPhaser;
            data.phaserInterval = 5f;
            _objectsToDestroy.Add(data);

            var go = new GameObject("Enemy_Phaser_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            if (isPhaser)
                go.AddComponent<PhaserEnemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            Assert.AreEqual(1, go.GetComponents<Enemy>().Length);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private CombatResolver CreateResolver()
        {
            var go = new GameObject("CombatResolver_Phaser_Test");
            _objectsToDestroy.Add(go);
            return go.AddComponent<CombatResolver>();
        }

        private GameManager CreateGameManager()
        {
            var go = new GameObject("GameManager_CombatResolver_Test");
            _objectsToDestroy.Add(go);
            GameManager gameManager = go.AddComponent<GameManager>();
            SetSingletonInstance(gameManager);
            return gameManager;
        }

        private static void SetPhaserVisible(Enemy enemy, bool visible)
        {
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            Assert.IsNotNull(phaser);
            SetPrivateField(phaser, "_isVisible", visible);
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

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }
    }
}
