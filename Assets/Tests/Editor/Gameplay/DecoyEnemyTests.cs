using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class DecoyEnemyTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private GameObject _trackerGO;
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _trackerGO = new GameObject("ActiveEnemyTracker_Test");
            _tracker = _trackerGO.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(_trackerGO);
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
        public void DecoyLabel_UsesAssignedCharacter()
        {
            BaybayinCharacterSO assignedCharacter = CreateCharacter("BA", "ba");
            Enemy enemy = CreateEnemy(assignedCharacter, isDecoy: true, yPosition: -1f);

            string labelText = InvokePrivate<string>(enemy, "BuildBaybayinLabelText");

            Assert.AreEqual("Draw: ba (BA)", labelText);
        }

        [Test]
        public void CombatResolver_FirstDecoyDraw_IsProtectedAndNotReportedAsMiss()
        {
            BaybayinCharacterSO assignedCharacter = CreateCharacter("BA", "ba");
            Enemy enemy = CreateEnemy(assignedCharacter, isDecoy: true, yPosition: -2f);
            CombatResolver resolver = CreateResolver();
            int baseHitCount = 0;
            int enemyDefeatedCount = 0;
            bool drawingMissed = false;
            DrawTextRelation relation = DrawTextRelation.Unknown;

            EventBus.OnBaseHit += HandleBaseHit;
            EventBus.OnEnemyDefeated += HandleEnemyDefeated;
            EventBus.OnDrawingMissed += HandleDrawingMissed;
            DrawFeedbackSignals.OnTextRelationResolved += HandleTextRelation;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", assignedCharacter.characterID);

                Assert.AreEqual(0, baseHitCount, "The first false-copy hit in an attempt is protected.");
                Assert.AreEqual(0, enemyDefeatedCount);
                Assert.IsFalse(drawingMissed, "A glyph carried by a copy is not a miss.");
                Assert.AreEqual(DrawTextRelation.FalseCopyShattered, relation);
                Assert.AreEqual(0, _tracker.ActiveCount);
                Assert.IsFalse(enemy.gameObject.activeInHierarchy);
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
                EventBus.OnEnemyDefeated -= HandleEnemyDefeated;
                EventBus.OnDrawingMissed -= HandleDrawingMissed;
                DrawFeedbackSignals.OnTextRelationResolved -= HandleTextRelation;
            }

            void HandleBaseHit(int damage) => baseHitCount += damage;
            void HandleEnemyDefeated(BaybayinCharacterSO _) => enemyDefeatedCount++;
            void HandleDrawingMissed() => drawingMissed = true;
            void HandleTextRelation(DrawFeedbackReport report) => relation = report.Relation;
        }

        [Test]
        public void CombatResolver_LaterDecoyDrawRaisesBaseHitButNeverDrawingMissed()
        {
            BaybayinCharacterSO firstCharacter = CreateCharacter("BA", "ba");
            BaybayinCharacterSO secondCharacter = CreateCharacter("KA", "ka");
            CreateEnemy(firstCharacter, isDecoy: true, yPosition: -1f);
            CreateEnemy(secondCharacter, isDecoy: true, yPosition: -2f);
            CombatResolver resolver = CreateResolver();
            int baseHitCount = 0;
            int drawingMissCount = 0;
            int falseCopyRelations = 0;

            EventBus.OnBaseHit += HandleBaseHit;
            EventBus.OnDrawingMissed += HandleDrawingMissed;
            DrawFeedbackSignals.OnTextRelationResolved += HandleTextRelation;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", firstCharacter.characterID);
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", secondCharacter.characterID);

                Assert.AreEqual(1, baseHitCount, "Only the later false-copy hit costs health.");
                Assert.AreEqual(0, drawingMissCount, "A false-copy hit is feedback, not a drawing miss.");
                Assert.AreEqual(2, falseCopyRelations);
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
                EventBus.OnDrawingMissed -= HandleDrawingMissed;
                DrawFeedbackSignals.OnTextRelationResolved -= HandleTextRelation;
            }

            void HandleBaseHit(int damage) => baseHitCount += damage;
            void HandleDrawingMissed() => drawingMissCount++;
            void HandleTextRelation(DrawFeedbackReport report)
            {
                if (report.Relation == DrawTextRelation.FalseCopyShattered)
                    falseCopyRelations++;
            }
        }

        [Test]
        public void CombatResolver_DecoyFalseHitGraceResetsForANewLevelAttempt()
        {
            BaybayinCharacterSO firstCharacter = CreateCharacter("BA", "ba");
            BaybayinCharacterSO retryCharacter = CreateCharacter("KA", "ka");
            CreateEnemy(firstCharacter, isDecoy: true, yPosition: -1f);
            CreateEnemy(retryCharacter, isDecoy: true, yPosition: -2f);
            CombatResolver resolver = CreateResolver();
            // Edit Mode does not dispatch OnEnable; exercise the real attempt event subscription.
            InvokePrivate<object>(resolver, "OnEnable");
            int baseHitCount = 0;

            EventBus.OnBaseHit += HandleBaseHit;
            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", firstCharacter.characterID);
                EventBus.RaiseLevelAttemptAborted();
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", retryCharacter.characterID);

                Assert.AreEqual(0, baseHitCount,
                    "Each level attempt protects its first actual false-copy hit.");
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
            }

            void HandleBaseHit(int damage) => baseHitCount += damage;
        }

        [Test]
        public void CombatResolver_WrongCharacter_OnDecoy_RaisesDrawingMissed()
        {
            BaybayinCharacterSO assignedCharacter = CreateCharacter("BA", "ba");
            BaybayinCharacterSO wrongCharacter = CreateCharacter("KA", "ka");
            Enemy enemy = CreateEnemy(assignedCharacter, isDecoy: true, yPosition: -3f, maxHealth: 2);
            CombatResolver resolver = CreateResolver();
            bool missedRaised = false;
            bool targetedRaised = false;
            int baseHitCount = 0;

            EventBus.OnDrawingMissed += HandleMissed;
            EventBus.OnEnemyTargeted += HandleTargeted;
            EventBus.OnBaseHit += HandleBaseHit;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", wrongCharacter.characterID);

                Assert.IsTrue(missedRaised);
                Assert.IsFalse(targetedRaised);
                Assert.AreEqual(0, baseHitCount);
                Assert.AreEqual(2, enemy.CurrentHealth);
                Assert.AreEqual(1, _tracker.ActiveCount);
            }
            finally
            {
                EventBus.OnDrawingMissed -= HandleMissed;
                EventBus.OnEnemyTargeted -= HandleTargeted;
                EventBus.OnBaseHit -= HandleBaseHit;
            }

            void HandleMissed() => missedRaised = true;
            void HandleTargeted(Enemy _) => targetedRaised = true;
            void HandleBaseHit(int damage) => baseHitCount += damage;
        }

        private BaybayinCharacterSO CreateCharacter(string characterID, string syllable)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterID;
            character.syllable = syllable;
            _objectsToDestroy.Add(character);
            return character;
        }

        private Enemy CreateEnemy(
            BaybayinCharacterSO assignedCharacter,
            bool isDecoy,
            float yPosition,
            int maxHealth = 1)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.assignedCharacter = assignedCharacter;
            data.isDecoy = isDecoy;
            data.maxHealth = maxHealth;
            data.moveSpeed = 1f;
            data.dealsContactDamage = !isDecoy;
            _objectsToDestroy.Add(data);

            var go = new GameObject("Enemy_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, yPosition, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            var enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            InvokePrivate<object>(enemy, "Awake");
            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private CombatResolver CreateResolver()
        {
            var go = new GameObject("CombatResolver_Test");
            _objectsToDestroy.Add(go);
            return go.AddComponent<CombatResolver>();
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
