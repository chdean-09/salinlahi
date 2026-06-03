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
        public void LevelOneOnboardingSequence_UsesBAOUHAFrameHints()
        {
            OnboardingSequenceSO sequence = LoadSequence(Level1SequencePath);
            Level1TutorialStepSO ba = LoadStep("Assets/ScriptableObjects/Tutorial/Level1TutorialStep_BA.asset");
            Level1TutorialStepSO ou = LoadStep("Assets/ScriptableObjects/Tutorial/Level1TutorialStep_OU.asset");
            Level1TutorialStepSO ha = LoadStep("Assets/ScriptableObjects/Tutorial/Level1TutorialStep_HA.asset");

            Assert.AreEqual(
                new[]
                {
                    OnboardingBeatType.ProtagonistIntro,
                    OnboardingBeatType.BaseIntro,
                    OnboardingBeatType.SoloTeach,
                    OnboardingBeatType.HeartLossDemo,
                    OnboardingBeatType.Release,
                },
                sequence.beatOrder);
            Assert.AreSame(ba, sequence.soloTeachStep);
            Assert.AreEqual(new[] { ba, ou, ha }, sequence.basicTeachSteps);
            AssertValidFrameTemplate(sequence.basicTeachVideos[0], "Level 1 BA");
            AssertValidFrameTemplate(sequence.basicTeachVideos[1], "Level 1 O");
            AssertValidFrameTemplate(sequence.basicTeachVideos[2], "Level 1 HA");
        }

        [Test]
        public void LevelTwoAdvancedOnboardingSequence_UsesBAComboFrameHint()
        {
            OnboardingSequenceSO sequence = LoadSequence(Level2SequencePath);

            AssertValidFrameTemplate(sequence.comboTeachVideo, "Level 2 BA combo");
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
