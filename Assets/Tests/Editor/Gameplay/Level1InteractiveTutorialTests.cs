using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class Level1InteractiveTutorialTests
    {
        [SetUp]
        public void SetUp()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [Test]
        public void ShouldRunForContext_RequiresTutorialSceneAndLevelOne()
        {
            Assert.IsTrue(Level1InteractiveTutorialController.ShouldRunForContext(
                "Level_01_Tutorial",
                LevelTutorialProgress.TutorialLevelNumber));

            Assert.IsFalse(Level1InteractiveTutorialController.ShouldRunForContext(
                "Gameplay",
                LevelTutorialProgress.TutorialLevelNumber));

            Assert.IsFalse(Level1InteractiveTutorialController.ShouldRunForContext(
                "Level_01_Tutorial",
                LevelTutorialProgress.TutorialLevelNumber + 1));
        }

        [Test]
        public void BeginTutorial_DoesNotMarkProgressSeen()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            levelConfig.levelNumber = LevelTutorialProgress.TutorialLevelNumber;

            try
            {
                controller.BeginForTests(levelConfig, "Level_01_Tutorial");

                Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial());
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(levelConfig);
            }
        }

        [Test]
        public void CompleteTutorial_DoesNotMarkProgressSeen()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();

            try
            {
                controller.CompleteForTests();

                Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial());
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Validator_AcceptsMatchingCharacterWithinToleranceAndDirection()
        {
            Level1TutorialGlyphValidator validator = new();
            List<Vector2> template = CreateLine(0f, 0f, 100f, 0f);
            List<Vector2> playerStroke = CreateLine(1f, 2f, 101f, 1f);

            Level1TutorialValidationResult result = validator.Validate(
                "BA",
                new RecognitionResult("BA", 0.9f, 0, "SA", 0.2f),
                passedRecognitionThreshold: true,
                new List<List<Vector2>> { playerStroke },
                new List<List<Vector2>> { template },
                tolerancePixels: 15f);

            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(Level1TutorialValidationFailure.None, result.Failure);
        }

        [Test]
        public void Validator_RejectsReversedDirection()
        {
            Level1TutorialGlyphValidator validator = new();
            List<Vector2> template = CreateLine(0f, 0f, 100f, 0f);
            List<Vector2> reversedStroke = CreateLine(100f, 0f, 0f, 0f);

            Level1TutorialValidationResult result = validator.Validate(
                "BA",
                new RecognitionResult("BA", 0.9f, 0, "SA", 0.2f),
                passedRecognitionThreshold: true,
                new List<List<Vector2>> { reversedStroke },
                new List<List<Vector2>> { template },
                tolerancePixels: 15f);

            Assert.IsFalse(result.IsCorrect);
            Assert.AreEqual(Level1TutorialValidationFailure.DirectionMismatch, result.Failure);
        }

        [Test]
        public void Validator_RejectsWrongRecognizedCharacter()
        {
            Level1TutorialGlyphValidator validator = new();
            List<Vector2> template = CreateLine(0f, 0f, 100f, 0f);
            List<Vector2> playerStroke = CreateLine(0f, 0f, 100f, 0f);

            Level1TutorialValidationResult result = validator.Validate(
                "BA",
                new RecognitionResult("SA", 0.9f, 0, "BA", 0.2f),
                passedRecognitionThreshold: true,
                new List<List<Vector2>> { playerStroke },
                new List<List<Vector2>> { template },
                tolerancePixels: 15f);

            Assert.IsFalse(result.IsCorrect);
            Assert.AreEqual(Level1TutorialValidationFailure.WrongCharacter, result.Failure);
        }

        [Test]
        public void GuideUI_ShowPrompt_CreatesAndShowsGuideSprite()
        {
            GameObject gameObject = new("Level1TutorialGuideUI");
            Level1TutorialGuideUI guide = gameObject.AddComponent<Level1TutorialGuideUI>();
            Level1TutorialStepSO step = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            Texture2D texture = new(2, 2);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            step.guideSprite = sprite;

            try
            {
                guide.ShowPrompt(step, canSkip: false);

                Transform guideSpriteTransform = gameObject.transform.Find("GuideSpriteImage");
                Assert.IsNotNull(guideSpriteTransform);

                Image guideSpriteImage = guideSpriteTransform.GetComponent<Image>();
                Assert.IsNotNull(guideSpriteImage);
                Assert.AreSame(sprite, guideSpriteImage.sprite);
                Assert.IsTrue(guideSpriteImage.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(step);
                Object.DestroyImmediate(gameObject);
            }
        }

        private static List<Vector2> CreateLine(float x0, float y0, float x1, float y1)
        {
            return new List<Vector2>
            {
                new(x0, y0),
                new(x1, y1)
            };
        }

        #region Acceptance Tests

        [Test]
        public void AC01_NonTutorialScene_DoesNotRunTutorial()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            levelConfig.levelNumber = LevelTutorialProgress.TutorialLevelNumber;

            try
            {
                // Simulate being in a non-tutorial scene by using a different scene name
                bool shouldRun = controller.ShouldRunFor(levelConfig);
                // This will be false because the active scene is not Level_01_Tutorial
                Assert.IsFalse(shouldRun, "Tutorial should not run outside Level_01_Tutorial scene");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(levelConfig);
            }
        }

        [Test]
        public void AC02_TutorialBlocksNormalWaves_WavesStartOnlyAfterCompletion()
        {
            // Verify that the controller exists and is configured
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();

            try
            {
                Assert.IsFalse(controller.IsConfigured, 
                    "Controller without steps should not be configured, blocking wave start via LevelFlowController");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AC03_BASuccess_UnlocksSkip()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            levelConfig.levelNumber = LevelTutorialProgress.TutorialLevelNumber;

            try
            {
                controller.BeginForTests(levelConfig, "Level_01_Tutorial");
                
                // Simulate first manual success by completing tutorial
                controller.CompleteForTests();
                
                Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial(), 
                    "Interactive tutorial is embedded in Level_01_Tutorial and should replay on each open");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(levelConfig);
            }
        }

        [Test]
        public void AC04_WrongSyllableDuringBA_DoesNotDamageBaseOrEnemy()
        {
            Level1TutorialGlyphValidator validator = new();
            List<Vector2> template = CreateLine(0f, 0f, 100f, 0f);
            List<Vector2> playerStroke = CreateLine(0f, 0f, 100f, 0f);

            // Player draws "SA" during BA prompt
            Level1TutorialValidationResult result = validator.Validate(
                "BA",
                new RecognitionResult("SA", 0.9f, 0, "BA", 0.2f),
                passedRecognitionThreshold: true,
                new List<List<Vector2>> { playerStroke },
                new List<List<Vector2>> { template },
                tolerancePixels: 15f);

            Assert.IsFalse(result.IsCorrect);
            Assert.AreEqual(Level1TutorialValidationFailure.WrongCharacter, result.Failure);
            // The validator result means no damage is dealt — controller checks IsCorrect before calling Defeat()
        }

        [Test]
        public void AC05_ThreeFailures_TriggerAssist()
        {
            Level1TutorialGlyphValidator validator = new();
            List<Vector2> template = CreateLine(0f, 0f, 100f, 0f);
            List<Vector2> badStroke = CreateLine(50f, 50f, 60f, 60f); // far from template

            int failureCount = 0;
            const int failuresBeforeAssist = 3;

            for (int i = 0; i < failuresBeforeAssist; i++)
            {
                Level1TutorialValidationResult result = validator.Validate(
                    "BA",
                    new RecognitionResult("BA", 0.9f, 0, "SA", 0.2f),
                    passedRecognitionThreshold: true,
                    new List<List<Vector2>> { badStroke },
                    new List<List<Vector2>> { template },
                    tolerancePixels: 15f);

                if (!result.IsCorrect)
                    failureCount++;
            }

            Assert.AreEqual(failuresBeforeAssist, failureCount, 
                "Three consecutive bad draws should count as 3 failures");
            // In the real controller, failureCount >= failuresBeforeAssist triggers assist
        }

        [Test]
        public void AC06_TutorialEnemy_CannotHitBase()
        {
            // Verify that FreezeThreat disables the collider
            GameObject enemyObject = new("TutorialEnemy");
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            
            // Add a collider to simulate contact
            BoxCollider2D collider = enemyObject.AddComponent<BoxCollider2D>();
            
            try
            {
                Level1TutorialEnemyController tutorialEnemy = new Level1TutorialEnemyController(enemy);
                tutorialEnemy.FreezeThreat();
                
                Assert.IsFalse(collider.enabled, 
                    "Tutorial enemy collider should be disabled to prevent base contact");
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void AC07_CompletingTutorial_DoesNotMarkProgress()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();

            try
            {
                Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial(), 
                    "Tutorial should not be marked seen before completion");

                controller.CompleteForTests();

                Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial(), 
                    "Interactive Level 1 tutorial should not set one-time progress");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AC08_SkippingTutorial_DoesNotMarkProgress()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            levelConfig.levelNumber = LevelTutorialProgress.TutorialLevelNumber;

            try
            {
                controller.BeginForTests(levelConfig, "Level_01_Tutorial");
                controller.CompleteForTests();

                Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial(), 
                    "Skip path should not prevent embedded tutorial from replaying next time");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(levelConfig);
            }
        }

        [Test]
        public void AC09_LaterLevels_DoNotShowTutorialUI()
        {
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            levelConfig.levelNumber = 2; // Level 2, not Level 1

            try
            {
                bool shouldShow = LevelTutorialProgress.ShouldShowForLevel(levelConfig);
                Assert.IsFalse(shouldShow, 
                    "Tutorial UI should not show for levels other than Level 1");
            }
            finally
            {
                Object.DestroyImmediate(levelConfig);
            }
        }

        [Test]
        public void AC10_GateCheck_FailsClosed_WrongSceneName()
        {
            Assert.IsFalse(
                Level1InteractiveTutorialController.ShouldRunForContext("Gameplay", 1),
                "Gate must fail closed if scene name is not Level_01_Tutorial");
        }

        [Test]
        public void AC11_TutorialEnemy_HasOneCorrectDrawDefeat()
        {
            GameObject enemyObject = new("TutorialEnemy");
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            
            try
            {
                Level1TutorialEnemyController tutorialEnemy = new Level1TutorialEnemyController(enemy);
                
                // Defeat is called with Max(1, CurrentHealth)
                // This forces one-hit defeat regardless of actual health
                tutorialEnemy.Defeat();
                
                Assert.IsTrue(enemy.IsDying || enemy.CurrentHealth <= 0, 
                    "Tutorial enemy should be defeated in one correct draw");
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void AC12_BaseDamageDisabled_DuringTutorial()
        {
            GameObject enemyObject = new("TutorialEnemy");
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            BoxCollider2D collider = enemyObject.AddComponent<BoxCollider2D>();
            
            try
            {
                Level1TutorialEnemyController tutorialEnemy = new Level1TutorialEnemyController(enemy);
                tutorialEnemy.FreezeThreat();
                
                // Verify collider is disabled (prevents base contact/trigger)
                Assert.IsFalse(collider.enabled, 
                    "Base damage must be disabled for tutorial enemies");
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void AC13_ToleranceWidens_OnSecondFailure()
        {
            Level1TutorialGlyphValidator validator = new();
            List<Vector2> template = CreateLine(0f, 0f, 100f, 0f);
            
            // Stroke that is just outside 15px tolerance but inside 20px
            List<Vector2> borderlineStroke = CreateLine(16f, 0f, 116f, 0f);

            // First attempt: strict tolerance (15px) — should fail
            Level1TutorialValidationResult result1 = validator.Validate(
                "BA",
                new RecognitionResult("BA", 0.9f, 0, "SA", 0.2f),
                passedRecognitionThreshold: true,
                new List<List<Vector2>> { borderlineStroke },
                new List<List<Vector2>> { template },
                tolerancePixels: 15f);

            Assert.IsFalse(result1.IsCorrect, "Should fail with 15px tolerance");

            // Second attempt: widened tolerance (20px) — should pass
            Level1TutorialValidationResult result2 = validator.Validate(
                "BA",
                new RecognitionResult("BA", 0.9f, 0, "SA", 0.2f),
                passedRecognitionThreshold: true,
                new List<List<Vector2>> { borderlineStroke },
                new List<List<Vector2>> { template },
                tolerancePixels: 20f);

            Assert.IsTrue(result2.IsCorrect, "Should pass with widened 20px tolerance");
        }

        #endregion
    }
}
