using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    public sealed class ReleaseQaRegressionTests
    {
        [Test]
        public void Shrine_ClusteredContacts_ChargeOnlyOneHeart()
        {
            var shrine = new GameObject("QA shrine", typeof(HeartSystem), typeof(PlayerBase));
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(HeartSystem).GetMethod("Awake", flags).Invoke(shrine.GetComponent<HeartSystem>(), null);
                typeof(PlayerBase).GetMethod("Awake", flags).Invoke(shrine.GetComponent<PlayerBase>(), null);
                typeof(PlayerBase).GetMethod("OnEnable", flags).Invoke(shrine.GetComponent<PlayerBase>(), null);
                EventBus.RaiseBaseHit(1);
                EventBus.RaiseBaseHit(1);
                EventBus.RaiseBaseHit(1);
                Assert.AreEqual(2, shrine.GetComponent<HeartSystem>().GetCurrentHearts());
            }
            finally
            {
                typeof(PlayerBase).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(shrine.GetComponent<PlayerBase>(), null);
                Object.DestroyImmediate(shrine);
            }
        }

        [Test]
        public void Introduction_SeenMechanic_CountsOnRetryWithoutForcedTutorial()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            var era = ScriptableObject.CreateInstance<EraConfigSO>();
            var campaign = ScriptableObject.CreateInstance<CampaignConfigSO>();
            try
            {
                enemy.enemyID = "qa-ragasa";
                level.levelNumber = 13;
                level.stableId = "level.qa.ragasa";
                level.waves = new List<WaveDefinition>
                {
                    new WaveDefinition { enemyTypes = new List<EnemyDataSO> { enemy } }
                };
                era.levels = new List<LevelConfigSO> { level };
                campaign.eras = new List<EraConfigSO> { era };
                EnemyDebutLookup.CampaignOverrideForTests = campaign;
                EnemyIntroductionProgress.ResetForTests();
                Assert.IsTrue(EnemyIntroductionProgress.TryClaimIntroduction(enemy));
                Assert.IsTrue(EnemyIntroductionBeat.CountsAsIntroducedThisAttempt(level, enemy));
            }
            finally
            {
                EnemyDebutLookup.CampaignOverrideForTests = null;
                EnemyIntroductionProgress.ResetForTests();
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(level);
                Object.DestroyImmediate(era);
                Object.DestroyImmediate(campaign);
            }
        }

        [TestCase(12, 2.8f)]
        [TestCase(13, 2.5f)]
        [TestCase(14, 1.8f)]
        public void PamanaEarlyWaves_GiveDoubleSpawnSpacing(int number, float previousInterval)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"Assets/ScriptableObjects/Levels/Level{number}_Config.asset");
            Assert.IsNotNull(level);
            Assert.LessOrEqual(level.enemySpeedMultiplier, 0.65f);
            Assert.GreaterOrEqual(level.waves[0].spawnInterval, previousInterval * 2f);
        }

        // Active-clue combat once matched a drawing only against the glyphs on screen. Every drawing
        // then resembled SOME on-screen glyph best, so scribbles killed enemies and a DA drawn at a
        // lone RA counted as RA. The drawing's identity has to come from every character; combat
        // decides afterwards whether that identity has an eligible target.
        [TestCase("DA", new[] { "RA" }, new string[0])]
        [TestCase("LA", new[] { "GA" }, new[] { "LA" })]
        public void ActiveClueCombat_ReportsTheDrawnGlyph_NotTheClosestOnScreenGlyph(
            string drawn, string[] openCarriers, string[] blockedCarriers)
        {
            var root = new GameObject("QA recognition context");
            var manager = root.AddComponent<GameManager>();
            var tracker = root.AddComponent<ActiveEnemyTracker>();
            var recognition = root.AddComponent<RecognitionManager>();
            var managerInstance = typeof(Singleton<GameManager>).GetProperty("Instance");
            var trackerInstance = typeof(Singleton<ActiveEnemyTracker>).GetProperty("Instance");
            var recognitionInstance = typeof(Singleton<RecognitionManager>).GetProperty("Instance");
            object previousManager = managerInstance.GetValue(null);
            object previousTracker = trackerInstance.GetValue(null);
            object previousRecognition = recognitionInstance.GetValue(null);
            bool previousLogging = RecognitionLogger.LoggingEnabled;
            var recognized = new List<string>();
            System.Action<string> onRecognized = recognized.Add;
            var assets = new List<Object>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                managerInstance.SetValue(null, manager);
                trackerInstance.SetValue(null, tracker);
                recognitionInstance.SetValue(null, null);
                RecognitionLogger.LoggingEnabled = false;
                typeof(RecognitionManager).GetField("_config", flags).SetValue(recognition,
                    AssetDatabase.LoadAssetAtPath<RecognitionConfigSO>(
                        "Assets/ScriptableObjects/RecognitionConfig_Default.asset"));
                typeof(RecognitionManager).GetMethod("Awake", flags).Invoke(recognition, null);

                var level = ScriptableObject.CreateInstance<LevelConfigSO>();
                assets.Add(level);
                level.activeClueCombatEnabled = true;
                manager.SetLevel(level);
                typeof(GameManager).GetProperty("CurrentState").SetValue(manager, GameState.Playing);
                Enemy Carrier(string id)
                {
                    var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
                    character.characterID = id;
                    var data = ScriptableObject.CreateInstance<EnemyDataSO>();
                    data.assignedCharacter = character;
                    assets.Add(character);
                    assets.Add(data);
                    var shell = new GameObject(id, typeof(BoxCollider2D), typeof(EnemyMover), typeof(Enemy));
                    shell.transform.SetParent(root.transform);
                    var enemy = shell.GetComponent<Enemy>();
                    typeof(Enemy).GetField("_data", flags).SetValue(enemy, data);
                    tracker.Register(enemy);
                    return enemy;
                }
                foreach (string id in openCarriers) Carrier(id);
                foreach (string id in blockedCarriers) Carrier(id).AddResolutionBlock(this);

                EventBus.OnCharacterRecognized += onRecognized;
                recognition.Recognize(StrokeTextParser.ParseStrokes(System.IO.File.ReadAllText(
                    $"Assets/Resources/Templates/{drawn}_template_01.txt")));

                CollectionAssert.AreEqual(new[] { drawn }, recognized,
                    $"A drawn {drawn} must be reported as {drawn}, whichever glyphs are on screen.");
            }
            finally
            {
                EventBus.OnCharacterRecognized -= onRecognized;
                RecognitionLogger.LoggingEnabled = previousLogging;
                Object.DestroyImmediate(root);
                managerInstance.SetValue(null, previousManager);
                trackerInstance.SetValue(null, previousTracker);
                recognitionInstance.SetValue(null, previousRecognition);
                foreach (Object asset in assets) Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ShortIntentionalStroke_IsAcceptedByProductionThresholds()
        {
            var config = AssetDatabase.LoadAssetAtPath<RecognitionConfigSO>(
                "Assets/ScriptableObjects/RecognitionConfig_Default.asset");
            Assert.IsFalse(StrokeValidation.IsTapLikeStroke(new List<Vector2>
            {
                new Vector2(100f, 100f), new Vector2(114f, 103f), new Vector2(124f, 108f)
            }, config.minimumStrokePathLengthPixels, config.minimumStrokeBoundsPixels));
        }
    }
}
