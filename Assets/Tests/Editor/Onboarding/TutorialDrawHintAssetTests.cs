using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class TutorialDrawHintAssetTests
    {
        private const string Level1SequencePath = "Assets/ScriptableObjects/Tutorial/Level1OnboardingSequence.asset";
        private const string Level2SequencePath = "Assets/ScriptableObjects/Tutorial/Level2AdvancedOnboardingSequence.asset";

        [Test]
        public void LevelOneOnboardingSequence_UsesUgatSymbolsAndGuideSprites()
        {
            OnboardingSequenceSO sequence = LoadSequence(Level1SequencePath);
            Level1TutorialStepSO ei = LoadStep("Assets/ScriptableObjects/Tutorial/Level1TutorialStep_EI.asset");

            // HeartLossDemo was removed from Level 1's beatOrder on 2026-09-14 at the owner's
            // explicit instruction: "the heart should stay at 3 healthy hearts after tutorial".
            // The beat never actually spent a heart — it drove the shake/flash through a
            // simulator, deliberately avoiding HeartSystem.LoseHeart — but it still READ as a
            // heart emptying, which is what the instruction was reacting to.
            //
            // It was put BACK on 2026-09-15, at the same owner's request, as part of the Iligaw
            // retarget. The reasoning above is why that is safe: DemoHeartSimulator still never
            // calls HeartSystem.LoseHeart, so the hearts really do stay at three afterwards, and
            // the earlier removal was about what the beat looked like rather than what it did.
            // This is the "if a later change puts a heart-loss teach back into Level 1, put
            // HeartLossDemo back here with it" case the previous note left open.
            //
            // SoloTeach was dropped from Level 1's beatOrder, and basicTeachSteps was cleared, on
            // 2026-09-15 (task 7 of the level1-enemy-introduction-lesson plan): the four-step
            // "teach loop" is superseded by the eight-beat per-enemy lesson authored on
            // LevelConfigSO.enemyLessons (see EnemyLessonSO / AboLesson.asset). Task 7 went further
            // than dropping SoloTeach from beatOrder: soloTeachStep, basicTeachSteps and
            // basicTeachVideos were removed from OnboardingSequenceSO entirely, so there is no
            // longer a field on the sequence asset for this test to assert against. The EI step
            // asset itself (and its guideSprite) is unaffected and still checked here.
            Assert.AreEqual(
                new[]
                {
                    OnboardingBeatType.ProtagonistIntro,
                    OnboardingBeatType.BaseIntro,
                    OnboardingBeatType.HeartLossDemo,
                    OnboardingBeatType.Release,
                },
                sequence.beatOrder);
            Assert.IsNotNull(ei.guideSprite);
        }

        private static OnboardingSequenceSO LoadSequence(string path)
        {
            OnboardingSequenceSO sequence = AssetDatabase.LoadAssetAtPath<OnboardingSequenceSO>(path);
            Assert.IsNotNull(sequence, $"Expected onboarding sequence at {path}.");
            return sequence;
        }

        private static Level1TutorialStepSO LoadStep(string path)
        {
            Level1TutorialStepSO step = AssetDatabase.LoadAssetAtPath<Level1TutorialStepSO>(path);
            Assert.IsNotNull(step, $"Expected tutorial step at {path}.");
            return step;
        }

        private static void AssertValidFrameTemplate(OnboardingVideoTemplate template, string label)
        {
            Assert.IsNull(template.videoClip, $"{label} should use frame playback, not VideoClip.");
            Assert.IsNull(template.gifTexture, $"{label} should use explicit frame sprites, not a GIF texture.");
            Assert.IsNull(template.animationClip, $"{label} should use frame playback, not AnimationClip.");
            Assert.IsNotNull(template.gifFrames, $"{label} gifFrames must be assigned.");
            Assert.IsNotEmpty(template.gifFrames, $"{label} gifFrames must not be empty.");
            Assert.GreaterOrEqual(template.gifFramesPerSecond, 1f, $"{label} FPS must be at least 1.");
            Assert.AreEqual("Tap anywhere to continue", template.tapToProceedText);

            for (int i = 0; i < template.gifFrames.Length; i++)
                Assert.IsNotNull(template.gifFrames[i], $"{label} frame {i} must not be null.");
        }
    }
}
