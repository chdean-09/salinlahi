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
            Level1TutorialStepSO na = LoadStep("Assets/ScriptableObjects/Tutorial/Level1TutorialStep_NA.asset");
            Level1TutorialStepSO a = LoadStep("Assets/ScriptableObjects/Tutorial/Level1TutorialStep_A.asset");
            Level1TutorialStepSO ma = LoadStep("Assets/ScriptableObjects/Tutorial/Level1TutorialStep_MA.asset");

            // HeartLossDemo was removed from Level 1's beatOrder on 2026-09-14 at the owner's
            // explicit instruction: "the heart should stay at 3 healthy hearts after tutorial".
            // The beat never actually spent a heart — it drove the shake/flash through a
            // simulator, deliberately avoiding HeartSystem.LoseHeart — but it still READ as a
            // heart emptying, which is what the instruction was reacting to. Level 1 now teaches
            // base damage reactively on the first real breach instead (level-01 design plan, §5).
            //
            // So this assertion is updated rather than the data reverted. If a later change puts
            // a heart-loss teach back into Level 1, put HeartLossDemo back here with it.
            Assert.AreEqual(
                new[]
                {
                    OnboardingBeatType.ProtagonistIntro,
                    OnboardingBeatType.BaseIntro,
                    OnboardingBeatType.SoloTeach,
                    OnboardingBeatType.Release,
                },
                sequence.beatOrder);
            Assert.AreSame(ei, sequence.soloTeachStep);
            Assert.AreEqual(new[] { ei, na, a, ma }, sequence.basicTeachSteps);
            Assert.IsNotNull(ei.guideSprite);
            Assert.IsNotNull(na.guideSprite);
            Assert.IsNotNull(a.guideSprite);
            Assert.IsNotNull(ma.guideSprite);
            Assert.IsEmpty(sequence.basicTeachVideos);
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
