using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class Level1OnboardingControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyRuntimeObject("TutorialCanvas");
            DestroyRuntimeObject("TutorialSpotlightOverlay");
        }

        [Test]
        public void Awake_WhenSceneObjectHasNoBeatComponents_AddsBasicLevelOneBeatComponents()
        {
            GameObject host = new("Level1OnboardingControllerHost");
            try
            {
                host.AddComponent<Level1OnboardingController>();

                Assert.NotNull(host.GetComponent<ProtagonistIntroBeat>());
                Assert.NotNull(host.GetComponent<BaseIntroBeat>());
                Assert.NotNull(host.GetComponent<SoloTeachBeat>());
                Assert.IsNull(host.GetComponent<ComboTeachBeat>(),
                    "Level 1 onboarding must not attach the multi-kill chain tutorial beat.");
                Assert.NotNull(host.GetComponent<HeartLossDemoBeat>());
                Assert.NotNull(host.GetComponent<ReleaseBeat>());
                Assert.AreEqual(5, host.GetComponents<OnboardingBeat>().Length);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LegacyLevelOneSequence_ConvertsAllStepsToBasicTeachSteps_WithoutComboTeach()
        {
            GameObject host = new("Level1OnboardingControllerHost");
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            Level1TutorialSequenceSO legacySequence = ScriptableObject.CreateInstance<Level1TutorialSequenceSO>();
            Level1TutorialStepSO ba = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            Level1TutorialStepSO ou = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            Level1TutorialStepSO ha = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            BaybayinCharacterSO haCharacter = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            EnemyDataSO haEnemy = ScriptableObject.CreateInstance<EnemyDataSO>();

            try
            {
                ha.targetCharacter = haCharacter;
                ha.enemyData = haEnemy;
                legacySequence.steps = new[] { ba, ou, ha };
                levelConfig.levelNumber = LevelTutorialProgress.Level1TutorialLevelNumber;
                levelConfig.tutorialSequence = legacySequence;

                Level1OnboardingController controller = host.AddComponent<Level1OnboardingController>();
                OnboardingSequenceSO sequence = InvokePrivate<OnboardingSequenceSO>(
                    controller,
                    "ResolveSequence",
                    levelConfig);

                Assert.IsNotNull(sequence);
                Assert.AreEqual(new[] { ba, ou, ha }, sequence.basicTeachSteps);
                Assert.AreSame(ba, sequence.soloTeachStep);
                Assert.IsNull(sequence.comboTeachStep);
                Assert.AreSame(haCharacter, sequence.heartLossDemoCharacter);
                Assert.AreSame(haEnemy, sequence.heartLossDemoEnemyData);
                Assert.Contains(OnboardingBeatType.SoloTeach, sequence.beatOrder);
                Assert.IsFalse(System.Array.Exists(sequence.beatOrder, beat => beat == OnboardingBeatType.ComboTeach));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(levelConfig);
                Object.DestroyImmediate(legacySequence);
                Object.DestroyImmediate(ba);
                Object.DestroyImmediate(ou);
                Object.DestroyImmediate(ha);
                Object.DestroyImmediate(haCharacter);
                Object.DestroyImmediate(haEnemy);
            }
        }

        [Test]
        public void SceneGifFallbacks_WithBasicTeachSteps_ApplyOnlyToMatchingGlyph()
        {
            GameObject host = new("Level1OnboardingControllerHost");
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            Level1TutorialSequenceSO legacySequence = ScriptableObject.CreateInstance<Level1TutorialSequenceSO>();
            Texture2D baTexture = new(1, 1);

            Level1TutorialStepSO ba = CreateStep("BA");
            Level1TutorialStepSO ou = CreateStep("OU");
            Level1TutorialStepSO ha = CreateStep("HA");

            try
            {
                legacySequence.steps = new[] { ba, ou, ha };
                levelConfig.levelNumber = LevelTutorialProgress.Level1TutorialLevelNumber;
                levelConfig.tutorialSequence = legacySequence;

                Level1OnboardingController controller = host.AddComponent<Level1OnboardingController>();
                SetPrivateField(controller, "_baGifTexture", baTexture);

                OnboardingSequenceSO sequence = InvokePrivate<OnboardingSequenceSO>(
                    controller,
                    "ResolveSequence",
                    levelConfig);

                Assert.IsNotNull(sequence.basicTeachVideos);
                Assert.AreEqual(3, sequence.basicTeachVideos.Length);
                Assert.AreSame(baTexture, sequence.basicTeachVideos[0].gifTexture);
                Assert.IsNull(sequence.basicTeachVideos[1].gifTexture);
                Assert.IsNull(sequence.basicTeachVideos[2].gifTexture);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(levelConfig);
                Object.DestroyImmediate(legacySequence);
                Object.DestroyImmediate(ba.targetCharacter);
                Object.DestroyImmediate(ou.targetCharacter);
                Object.DestroyImmediate(ha.targetCharacter);
                Object.DestroyImmediate(ba);
                Object.DestroyImmediate(ou);
                Object.DestroyImmediate(ha);
                Object.DestroyImmediate(baTexture);
            }
        }

        [Test]
        public void NormalizeSequenceForLevel_LevelTwoForcesAdvancedBeatOrder()
        {
            OnboardingSequenceSO sequence = ScriptableObject.CreateInstance<OnboardingSequenceSO>();
            Level1TutorialStepSO comboStep = ScriptableObject.CreateInstance<Level1TutorialStepSO>();

            try
            {
                sequence.comboTeachStep = comboStep;
                sequence.beatOrder = new[]
                {
                    OnboardingBeatType.ComboTeach,
                    OnboardingBeatType.Release,
                };

                Level1OnboardingController.NormalizeSequenceForLevel(
                    sequence,
                    LevelTutorialProgress.Level2TutorialLevelNumber);

                Assert.AreEqual(
                    new[]
                    {
                        OnboardingBeatType.ComboTeach,
                        OnboardingBeatType.FocusModeTeach,
                        OnboardingBeatType.Release,
                    },
                    sequence.beatOrder);
                Assert.AreSame(comboStep, sequence.focusPracticeStep);
                Assert.AreEqual(2, sequence.focusPracticeKillCount);
                Assert.AreSame(comboStep, sequence.focusChainStep);
                Assert.AreEqual(3, sequence.focusChainEnemyCount);
                Assert.IsFalse(string.IsNullOrWhiteSpace(sequence.focusModeIntro.fallbackText));
                Assert.IsFalse(string.IsNullOrWhiteSpace(sequence.focusChainIntro.fallbackText));
            }
            finally
            {
                Object.DestroyImmediate(sequence);
                Object.DestroyImmediate(comboStep);
            }
        }

        private static void DestroyRuntimeObject(string objectName)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < objects.Length; i++)
            {
                GameObject obj = objects[i];
                if (obj != null && obj.name == objectName)
                    Object.DestroyImmediate(obj);
            }
        }

        private static Level1TutorialStepSO CreateStep(string characterId)
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterId;

            Level1TutorialStepSO step = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            step.targetCharacter = character;
            return step;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            return (T)method.Invoke(target, args);
        }
    }
}
