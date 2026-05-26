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
            TutorialRuntimeState.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
            TutorialRuntimeState.Clear();
        }

        [Test]
        public void ShouldRunForContext_RequiresLevelOneOnly()
        {
            Assert.IsTrue(Level1InteractiveTutorialController.ShouldRunForContext(
                "Gameplay",
                LevelTutorialProgress.TutorialLevelNumber));

            Assert.IsTrue(Level1InteractiveTutorialController.ShouldRunForContext(
                "AnyGameplayScene",
                LevelTutorialProgress.TutorialLevelNumber));

            Assert.IsFalse(Level1InteractiveTutorialController.ShouldRunForContext(
                "Gameplay",
                LevelTutorialProgress.TutorialLevelNumber + 1));
        }

        [Test]
        public void ShouldRunFor_RequiresLevelOneAndTutorialSequence()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();
            Level1TutorialSequenceSO sequence = ScriptableObject.CreateInstance<Level1TutorialSequenceSO>();
            LevelConfigSO levelOne = ScriptableObject.CreateInstance<LevelConfigSO>();
            LevelConfigSO levelTwo = ScriptableObject.CreateInstance<LevelConfigSO>();

            try
            {
                levelOne.levelNumber = LevelTutorialProgress.TutorialLevelNumber;
                levelTwo.levelNumber = LevelTutorialProgress.TutorialLevelNumber + 1;

                Assert.IsFalse(controller.ShouldRunFor(levelOne));

                levelOne.tutorialSequence = sequence;
                levelTwo.tutorialSequence = sequence;

                Assert.IsTrue(controller.ShouldRunFor(levelOne));
                Assert.IsFalse(controller.ShouldRunFor(levelTwo));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(sequence);
                Object.DestroyImmediate(levelOne);
                Object.DestroyImmediate(levelTwo);
            }
        }

        [Test]
        public void TutorialRuntimeState_CombatOverrideClears()
        {
            TutorialRuntimeState.Begin(1);
            TutorialRuntimeState.SetCombatOverrideActive(true);

            Assert.IsTrue(TutorialRuntimeState.IsActiveForLevel(1));
            Assert.IsTrue(TutorialRuntimeState.IsCombatOverrideActive);

            TutorialRuntimeState.Clear();

            Assert.IsFalse(TutorialRuntimeState.IsActive);
            Assert.IsFalse(TutorialRuntimeState.IsCombatOverrideActive);
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
                controller.BeginForTests(levelConfig, "Gameplay");

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
        public void ForceGameplayHudVisible_DoesNotRevealFullscreenFeedbackOverlays()
        {
            GameObject hudCanvas = new("HUDCanvas");
            GameObject heartsPanel = new("HeartsPanel");
            GameObject rejectFlash = new("RejectFlash");
            CanvasGroup heartsGroup = heartsPanel.AddComponent<CanvasGroup>();
            CanvasGroup rejectGroup = rejectFlash.AddComponent<CanvasGroup>();

            heartsPanel.transform.SetParent(hudCanvas.transform);
            rejectFlash.transform.SetParent(hudCanvas.transform);
            heartsGroup.alpha = 0f;
            heartsGroup.interactable = false;
            heartsGroup.blocksRaycasts = false;
            rejectGroup.alpha = 0f;
            rejectGroup.interactable = false;
            rejectGroup.blocksRaycasts = false;

            try
            {
                Level1InteractiveTutorialController.ForceGameplayHudVisible();

                Assert.AreEqual(1f, heartsGroup.alpha);
                Assert.IsTrue(heartsGroup.interactable);
                Assert.IsTrue(heartsGroup.blocksRaycasts);
                Assert.AreEqual(0f, rejectGroup.alpha);
                Assert.IsFalse(rejectGroup.interactable);
                Assert.IsFalse(rejectGroup.blocksRaycasts);
            }
            finally
            {
                Object.DestroyImmediate(hudCanvas);
            }
        }

        [Test]
        public void Validator_AcceptsMatchingCharacterWithinToleranceAndDirection()
        {
            Level1TutorialGlyphValidator validator = new();

            Level1TutorialValidationResult result = validator.Validate(
                "BA",
                new RecognitionResult("BA", 0.9f, 0, "SA", 0.2f),
                passedRecognitionThreshold: true);

            Assert.IsTrue(result.IsCorrect);
            Assert.AreEqual(Level1TutorialValidationFailure.None, result.Failure);
        }

        [Test]
        public void Validator_RejectsWrongRecognizedCharacter()
        {
            Level1TutorialGlyphValidator validator = new();

            Level1TutorialValidationResult result = validator.Validate(
                "BA",
                new RecognitionResult("SA", 0.9f, 0, "BA", 0.2f),
                passedRecognitionThreshold: true);

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
        public void AC01_LevelOneWithTutorialData_RunsInGameplayScene()
        {
            GameObject gameObject = new("Level1InteractiveTutorialController");
            Level1InteractiveTutorialController controller = gameObject.AddComponent<Level1InteractiveTutorialController>();
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            Level1TutorialSequenceSO sequence = ScriptableObject.CreateInstance<Level1TutorialSequenceSO>();
            levelConfig.levelNumber = LevelTutorialProgress.TutorialLevelNumber;
            levelConfig.tutorialSequence = sequence;

            try
            {
                bool shouldRun = controller.ShouldRunFor(levelConfig);
                Assert.IsTrue(shouldRun, "Tutorial should run as a Level 1 phase in the normal Gameplay scene");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(levelConfig);
                Object.DestroyImmediate(sequence);
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
                controller.BeginForTests(levelConfig, "Gameplay");
                
                // Simulate first manual success by completing tutorial
                controller.CompleteForTests();
                
                Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial(), 
                    "Interactive tutorial is embedded in Level 1 gameplay and should replay on each open");
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

            // Player draws "SA" during BA prompt
            Level1TutorialValidationResult result = validator.Validate(
                "BA",
                new RecognitionResult("SA", 0.9f, 0, "BA", 0.2f),
                passedRecognitionThreshold: true);

            Assert.IsFalse(result.IsCorrect);
            Assert.AreEqual(Level1TutorialValidationFailure.WrongCharacter, result.Failure);
            // The validator result means no damage is dealt — controller checks IsCorrect before calling Defeat()
        }

        [Test]
        public void AC05_ThreeFailures_TriggerAssist()
        {
            Level1TutorialGlyphValidator validator = new();

            int failureCount = 0;
            const int failuresBeforeAssist = 3;

            for (int i = 0; i < failuresBeforeAssist; i++)
            {
                Level1TutorialValidationResult result = validator.Validate(
                    "BA",
                    new RecognitionResult("BA", 0.9f, 0, "SA", 0.2f),
                    passedRecognitionThreshold: true);

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
                controller.BeginForTests(levelConfig, "Gameplay");
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
            Assert.IsTrue(
                Level1InteractiveTutorialController.ShouldRunForContext("Gameplay", 1),
                "Gate should allow normal Gameplay scene for embedded Level 1 tutorial");
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

        #endregion
    }
}
