using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public sealed class StrokeReplayRecognitionPlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private int _failedCount;
        private int _recognizedCount;
        private int _resolvedCount;
        private int _drawingMissCount;
        private string _lastRecognizedCharacter;
        private bool _lastPassedThreshold;
        private float _lastScore;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<RecognitionManager>();
            _failedCount = 0;
            _recognizedCount = 0;
            _resolvedCount = 0;
            _drawingMissCount = 0;
            _lastRecognizedCharacter = null;
            _lastPassedThreshold = true;
            _lastScore = 1f;

            EventBus.OnDrawingFailed += HandleDrawingFailed;
            EventBus.OnDrawingMissed += HandleDrawingMissed;
            EventBus.OnCharacterRecognized += HandleCharacterRecognized;
            EventBus.OnRecognitionResolved += HandleRecognitionResolved;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnDrawingFailed -= HandleDrawingFailed;
            EventBus.OnDrawingMissed -= HandleDrawingMissed;
            EventBus.OnCharacterRecognized -= HandleCharacterRecognized;
            EventBus.OnRecognitionResolved -= HandleRecognitionResolved;

            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<RecognitionManager>();
            ClearSingletonInstance<ActiveEnemyTracker>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator MissReplay_UsesRecognitionManager_AndCannotAdvanceObjective()
        {
            LevelConfigSO level = CreateLevelWithTarget();
            GameObject gameManagerObject = new GameObject("GameManager_StrokeReplay_Test");
            _objectsToDestroy.Add(gameManagerObject);
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            gameManager.SetLevel(level);

            GameObject objectiveObject = new GameObject("RestorationObjective_StrokeReplay_Test");
            _objectsToDestroy.Add(objectiveObject);
            RestorationObjectiveController objective =
                objectiveObject.AddComponent<RestorationObjectiveController>();
            objective.Configure(level);
            int restoredBefore = objective.State.RestoredTargetCount;

            RecognitionConfigSO recognitionConfig =
                ScriptableObject.CreateInstance<RecognitionConfigSO>();
            recognitionConfig.minimumConfidence = 0.60f;
            _objectsToDestroy.Add(recognitionConfig);

            GameObject recognitionObject = new GameObject("RecognitionManager_StrokeReplay_Test");
            recognitionObject.SetActive(false);
            _objectsToDestroy.Add(recognitionObject);
            RecognitionManager recognition = recognitionObject.AddComponent<RecognitionManager>();
            SetPrivateField(recognition, "_config", recognitionConfig);
            recognitionObject.SetActive(true);

            recognition.Recognize(BuildMissSample());
            yield return null;

            Assert.AreEqual(1, _resolvedCount,
                "A non-degenerate replay miss must produce one scored resolution.");
            Assert.IsFalse(_lastPassedThreshold,
                "The replay miss must be rejected by the active level threshold.");
            Assert.Less(_lastScore, 0.45f);
            Assert.AreEqual(1, _failedCount,
                "A rejected replay must raise exactly one failed-drawing outcome.");
            Assert.AreEqual(0, _recognizedCount,
                "A rejected replay must not raise an accepted-character event.");
            Assert.AreEqual(restoredBefore, objective.State.RestoredTargetCount,
                "A rejected replay must not advance restoration progress.");
        }

        [UnityTest]
        public IEnumerator CorrectRecordedTemplate_UsesRecognizerAndDamagesTheActiveClue()
        {
            GameObject gameManagerObject = CreateGameManagerWithLevel();
            GameObject trackerObject = new GameObject("ActiveEnemyTracker_StrokeReplay_Test");
            trackerObject.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerObject);

            Enemy enemy = CreateActiveEnemy("BA", 3, 2f);
            CreateActiveClueDirector();
            GameObject resolverObject = new GameObject("CombatResolver_StrokeReplay_Test");
            resolverObject.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverObject);
            RecognitionManager recognition = CreateRecognitionManager(0.45f);

            TextAsset fixture = Resources.Load<TextAsset>("Templates/BA_template_01");
            Assert.IsNotNull(fixture, "The shipped BA recognizer template is required for replay QA.");

            recognition.Recognize(StrokeTextParser.ParseStrokes(fixture.text));
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(1, _resolvedCount);
            Assert.IsTrue(_lastPassedThreshold);
            Assert.AreEqual(1, _recognizedCount);
            Assert.AreEqual("BA", BaybayinIdCanonicalizer.Canonicalize(_lastRecognizedCharacter));
            Assert.AreEqual(0, _drawingMissCount);
            Assert.Less(enemy.CurrentHealth, enemy.MaxHealth,
                "A recorded, confidently recognized BA must resolve through combat against the " +
                "active BA clue rather than merely raising an isolated event.");
        }

        [UnityTest]
        public IEnumerator WrongValidRecordedTemplate_IsRecognizedButCannotResolveTheActiveClue()
        {
            GameObject gameManagerObject = CreateGameManagerWithLevel();
            GameObject trackerObject = new GameObject("ActiveEnemyTracker_WrongReplay_Test");
            trackerObject.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerObject);

            Enemy enemy = CreateActiveEnemy("MA", 3, 2f);
            CreateActiveClueDirector();
            GameObject resolverObject = new GameObject("CombatResolver_WrongReplay_Test");
            resolverObject.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverObject);
            RecognitionManager recognition = CreateRecognitionManager(0.45f);

            TextAsset fixture = Resources.Load<TextAsset>("Templates/BA_template_01");
            Assert.IsNotNull(fixture, "The shipped BA recognizer template is required for wrong-glyph QA.");

            recognition.Recognize(StrokeTextParser.ParseStrokes(fixture.text));
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(1, _resolvedCount,
                "A valid wrong glyph still needs to traverse the scored recognition path.");
            Assert.IsTrue(_lastPassedThreshold);
            Assert.AreEqual(1, _recognizedCount);
            Assert.AreEqual(1, _drawingMissCount,
                "A confidently recognized but wrong glyph must be a combat miss, not a kill.");
            Assert.AreEqual(enemy.MaxHealth, enemy.CurrentHealth);
        }

        private LevelConfigSO CreateLevelWithTarget()
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = "A";
            symbol.stableId = "symbol.test.a";
            symbol.syllable = "a";
            _objectsToDestroy.Add(symbol);

            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.levelNumber = 1;
            level.overrideDrawingAccuracyThreshold = true;
            level.drawingAccuracyThresholdOverride = 0.45f;
            level.restorationObjective = new RestorationObjectiveDefinition
            {
                units = new List<RestorationObjectiveUnit>
                {
                    new RestorationObjectiveUnit
                    {
                        stableId = "unit.test",
                        tokens = new List<RestorationObjectiveToken>
                        {
                            new RestorationObjectiveToken
                            {
                                kind = RestorationTokenKind.Target,
                                occurrenceId = "unit.test.occurrence.00",
                                target = new SymbolValueReference { symbol = symbol },
                            },
                        },
                    },
                },
            };
            _objectsToDestroy.Add(level);
            return level;
        }

        private void HandleDrawingFailed() => _failedCount++;

        private void HandleCharacterRecognized(string characterId)
        {
            _recognizedCount++;
            _lastRecognizedCharacter = characterId;
        }

        private void HandleDrawingMissed() => _drawingMissCount++;

        private void HandleRecognitionResolved(
            RecognitionResult result,
            bool passedThreshold,
            float _)
        {
            _resolvedCount++;
            _lastPassedThreshold = passedThreshold;
            _lastScore = result.score;
        }

        private static List<List<Vector2>> BuildMissSample()
        {
            return new List<List<Vector2>>
            {
                new List<Vector2>
                {
                    new Vector2(-1000f, -1f),
                    new Vector2(1000f, 1f),
                },
                new List<Vector2> { new Vector2(-900f, 700f), new Vector2(900f, -700f) },
                new List<Vector2> { new Vector2(-900f, -700f), new Vector2(900f, 700f) },
                new List<Vector2> { new Vector2(-850f, 0f), new Vector2(850f, 0f) },
                new List<Vector2> { new Vector2(0f, -650f), new Vector2(0f, 650f) },
                new List<Vector2> { new Vector2(-500f, -500f), new Vector2(500f, 500f) },
            };
        }

        private GameObject CreateGameManagerWithLevel()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.levelNumber = 1;
            level.activeClueCombatEnabled = true;
            level.activeClueRestorationEnabled = true;
            _objectsToDestroy.Add(level);

            GameObject gameManagerObject = new GameObject("GameManager_StrokeReplay_Combat_Test");
            _objectsToDestroy.Add(gameManagerObject);
            GameManager manager = gameManagerObject.AddComponent<GameManager>();
            manager.SetLevel(level);
            return gameManagerObject;
        }

        private RecognitionManager CreateRecognitionManager(float threshold)
        {
            RecognitionConfigSO recognitionConfig =
                ScriptableObject.CreateInstance<RecognitionConfigSO>();
            recognitionConfig.minimumConfidence = threshold;
            _objectsToDestroy.Add(recognitionConfig);

            GameObject recognitionObject = new GameObject("RecognitionManager_StrokeReplay_Combat_Test");
            recognitionObject.SetActive(false);
            _objectsToDestroy.Add(recognitionObject);
            RecognitionManager recognition = recognitionObject.AddComponent<RecognitionManager>();
            SetPrivateField(recognition, "_config", recognitionConfig);
            recognitionObject.SetActive(true);
            return recognition;
        }

        private ActiveClueDirector CreateActiveClueDirector()
        {
            GameObject directorObject = new GameObject("ActiveClueDirector_StrokeReplay_Test");
            _objectsToDestroy.Add(directorObject);
            ActiveClueDirector director = directorObject.AddComponent<ActiveClueDirector>();
            director.SetObjectiveSource(new StubObjectiveSource());
            director.Reevaluate();
            return director;
        }

        private Enemy CreateActiveEnemy(string characterId, int maxHealth, float y)
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterId;
            character.stableId = "symbol." + characterId.ToLowerInvariant();
            character.syllable = characterId.ToLowerInvariant();
            _objectsToDestroy.Add(character);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "enemy.stroke-replay." + characterId;
            data.maxHealth = maxHealth;
            data.moveSpeed = 0f;
            data.assignedCharacter = character;
            data.deathFrames = System.Array.Empty<Sprite>();
            _objectsToDestroy.Add(data);

            GameObject enemyObject = new GameObject("Enemy_StrokeReplay_" + characterId);
            enemyObject.SetActive(false);
            enemyObject.AddComponent<SpriteRenderer>();
            enemyObject.AddComponent<BoxCollider2D>();
            enemyObject.AddComponent<EnemyMover>();
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            enemyObject.transform.position = new Vector3(0f, y, 0f);
            _objectsToDestroy.Add(enemyObject);
            enemyObject.SetActive(true);
            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private sealed class StubObjectiveSource : IClueObjectiveSource
        {
            public bool IsClueCombatActive => true;
            public IReadOnlyCollection<string> CurrentObjectiveContentIds =>
                System.Array.Empty<string>();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            FieldInfo instanceField = typeof(Singleton<T>).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);
        }
    }
}
