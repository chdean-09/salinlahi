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
            _lastPassedThreshold = true;
            _lastScore = 1f;

            EventBus.OnDrawingFailed += HandleDrawingFailed;
            EventBus.OnCharacterRecognized += HandleCharacterRecognized;
            EventBus.OnRecognitionResolved += HandleRecognitionResolved;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnDrawingFailed -= HandleDrawingFailed;
            EventBus.OnCharacterRecognized -= HandleCharacterRecognized;
            EventBus.OnRecognitionResolved -= HandleRecognitionResolved;

            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<RecognitionManager>();

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

        private void HandleCharacterRecognized(string _) => _recognizedCount++;

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
