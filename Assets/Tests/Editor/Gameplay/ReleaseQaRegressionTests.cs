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

        [Test]
        public void RecognitionContext_ExcludesDyingAndBlockedCarriersAndInactiveFocusGlyphs()
        {
            var root = new GameObject("QA recognition context");
            var manager = root.AddComponent<GameManager>();
            var tracker = root.AddComponent<ActiveEnemyTracker>();
            var recognition = root.AddComponent<RecognitionManager>();
            var managerInstance = typeof(Singleton<GameManager>).GetProperty("Instance");
            var trackerInstance = typeof(Singleton<ActiveEnemyTracker>).GetProperty("Instance");
            object previousManager = managerInstance.GetValue(null);
            object previousTracker = trackerInstance.GetValue(null);
            var assets = new List<Object>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                managerInstance.SetValue(null, manager);
                trackerInstance.SetValue(null, tracker);
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
                Carrier("NGA");
                var dying = Carrier("NA");
                typeof(Enemy).GetField("_isDying", flags).SetValue(dying, true);
                Carrier("BA").AddResolutionBlock(this);
                level.focusWords = new List<FocusWordDefinition>
                {
                    new FocusWordDefinition { decomposition = new List<SymbolValueReference>
                    { new SymbolValueReference { symbol = dying.Character } } }
                };
                var candidates = (ISet<string>)typeof(RecognitionManager)
                    .GetMethod("ResolveCombatCandidates", flags).Invoke(recognition, null);
                CollectionAssert.AreEquivalent(new[] { "NGA" }, candidates);
            }
            finally
            {
                Object.DestroyImmediate(root);
                managerInstance.SetValue(null, previousManager);
                trackerInstance.SetValue(null, previousTracker);
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
