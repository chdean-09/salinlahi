using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-135 AC1. The claim the shipped code did not previously make on every path: an
    /// accepted draw resolves exactly once even when recognition echoes, on the clue-disabled
    /// legacy path as well as on the Level 1 slice.
    ///
    /// Assertions deliberately read events raised synchronously by the resolver rather than
    /// enemy health, because the damage itself lands inside a pronunciation-lead coroutine
    /// whose Edit Mode pumping is not something this fixture should depend on.
    ///
    /// AC2 -- the HUD correction cue -- lives in DrawingFeedbackRejectCueTests in the Play
    /// Mode suite instead. DrawingFeedback subscribes in OnEnable and has no [ExecuteAlways],
    /// and Edit Mode does not run the MonoBehaviour lifecycle on a runtime-created
    /// GameObject, so asserting the cue here would only ever have measured a component that
    /// never subscribed.
    /// </summary>
    [TestFixture]
    public class CombatFeedbackCueTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            var trackerGo = new GameObject("ActiveEnemyTracker_Feedback_Test");
            ActiveEnemyTracker tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(tracker);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<GameManager>();
            TutorialRuntimeState.Clear();
            ChallengeRuntimeState.Clear();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        // Before SALIN-135 the echo gate lived inside ResolveActiveClueDraw, so a clue-disabled
        // level -- which is every shipped level except the Level 1 slice -- had no protection at
        // all: one finger-lift that echoed produced two pronunciations and two hits.
        [Test]
        public void LegacyPath_EchoedRecognition_ResolvesExactlyOnce()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA");
            CreateEnemy(assigned, y: -2f);
            CombatResolver resolver = CreateResolver();

            int pronunciations = 0;
            int missed = 0;
            void OnPronunciation(BaybayinCharacterSO _) => pronunciations++;
            void OnMissed() => missed++;

            EventBus.OnPronunciationRequested += OnPronunciation;
            EventBus.OnDrawingMissed += OnMissed;
            try
            {
                InvokePrivate(resolver, "HandleCharacterRecognized", "BA");
                InvokePrivate(resolver, "HandleCharacterRecognized", "BA");
            }
            finally
            {
                EventBus.OnPronunciationRequested -= OnPronunciation;
                EventBus.OnDrawingMissed -= OnMissed;
            }

            Assert.AreEqual(1, pronunciations,
                "One finger-lift is one attempt: an echoed recognition must not resolve twice.");
            Assert.AreEqual(0, missed,
                "An echo is dropped, not converted into a miss.");
        }

        // The gate is a window, not a latch. A player who genuinely draws the same character
        // twice must still get a second response, or a repeated glyph becomes undrawable.
        [Test]
        public void LegacyPath_RepeatOutsideTheEchoWindow_ResolvesAgain()
        {
            BaybayinCharacterSO assigned = CreateCharacter("BA");
            CreateEnemy(assigned, y: -2f);
            CombatResolver resolver = CreateResolver();

            int pronunciations = 0;
            void OnPronunciation(BaybayinCharacterSO _) => pronunciations++;

            EventBus.OnPronunciationRequested += OnPronunciation;
            try
            {
                InvokePrivate(resolver, "HandleCharacterRecognized", "BA");

                // Stand in for the echo window elapsing; Edit Mode cannot advance unscaled time.
                SetPrivateField(resolver, "_lastRecognizedTime", float.NegativeInfinity);

                InvokePrivate(resolver, "HandleCharacterRecognized", "BA");
            }
            finally
            {
                EventBus.OnPronunciationRequested -= OnPronunciation;
            }

            Assert.AreEqual(2, pronunciations,
                "A deliberate repeat outside the echo window is a second attempt.");
        }

        // The miss branch is the other half of the echo problem: it records a failed Form
        // attempt, so a doubled echo would depress the mastery ratio for one user action.
        [Test]
        public void LegacyPath_EchoedMiss_RaisesTheMissCueOnce()
        {
            BaybayinCharacterSO carried = CreateCharacter("BA");
            CreateEnemy(carried, y: -2f);
            CombatResolver resolver = CreateResolver();

            int missed = 0;
            void OnMissed() => missed++;

            EventBus.OnDrawingMissed += OnMissed;
            try
            {
                InvokePrivate(resolver, "HandleCharacterRecognized", "MA");
                InvokePrivate(resolver, "HandleCharacterRecognized", "MA");
            }
            finally
            {
                EventBus.OnDrawingMissed -= OnMissed;
            }

            Assert.AreEqual(1, missed,
                "An echoed miss is still one miss.");
        }

        private BaybayinCharacterSO CreateCharacter(string id)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.stableId = "symbol." + id.ToLowerInvariant();
            character.syllable = id.ToLowerInvariant();
            _objectsToDestroy.Add(character);
            return character;
        }

        private Enemy CreateEnemy(BaybayinCharacterSO assigned, float y)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "enemy.test.feedback";
            data.assignedCharacter = assigned;
            data.maxHealth = 3;
            data.moveSpeed = 1f;
            _objectsToDestroy.Add(data);

            var go = new GameObject("Enemy_Feedback_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private CombatResolver CreateResolver()
        {
            var go = new GameObject("CombatResolver_Feedback_Test");
            _objectsToDestroy.Add(go);
            CombatResolver resolver = go.AddComponent<CombatResolver>();

            // CombatResolver self-destructs as a duplicate, so a resolver leaked by another
            // fixture would silently gut every assertion below.
            Assert.IsTrue(resolver != null,
                "Setup: another CombatResolver is still alive in the test scene.");
            return resolver;
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
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
