using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Onboarding
{
    /// <summary>
    /// Level 2 is intentionally a combat-discovery level. Its old advanced onboarding asset is
    /// retained as an unreferenced content placeholder, but the live level must not treat a null
    /// sequence as a missing configuration error or present a reference-form symbol card.
    /// </summary>
    [TestFixture]
    public sealed class Level2OnboardingSequenceTests
    {
        private const string Level2SequencePath =
            "Assets/ScriptableObjects/Tutorial/Level2AdvancedOnboardingSequence.asset";
        private const string Level2ConfigPath =
            "Assets/ScriptableObjects/Levels/Level2_Config.asset";

        [Test]
        public void Level2Config_UsesCombatDiscovery_AndDoesNotAuthorOnboarding()
        {
            LevelConfigSO level = LoadLevel2();

            Assert.IsNull(level.onboardingSequence,
                "Level 2 must not request a separate onboarding sequence.");
            Assert.IsNull(level.tutorialSequence,
                "Level 2 must not fall back to the legacy tutorial sequence.");
            Assert.IsFalse(level.multiKillChainEnabled,
                "The authored Level 2 configuration keeps mass-clear disabled in this slice.");
            Assert.IsTrue(level.suppressSymbolLearningCards,
                "Level 2 should discover symbols through combat instead of reference cards.");
            Assert.AreEqual("value.ta", level.spawnAssignmentPolicy.openingSpawnSpokenValueId,
                "The first Level 2 carrier must be TA so combat discovery follows the objective order.");
        }

        [Test]
        public void Level2AdvancedOnboardingAsset_RemainsUnwiredAndEmpty()
        {
            OnboardingSequenceSO sequence = AssetDatabase.LoadAssetAtPath<OnboardingSequenceSO>(
                Level2SequencePath);

            Assert.IsNotNull(sequence,
                $"The retained placeholder asset should remain available at {Level2SequencePath}.");
            Assert.IsNotNull(sequence.beatOrder);
            Assert.IsEmpty(sequence.beatOrder,
                "An unwired placeholder must not accidentally become a live onboarding sequence.");
        }

        [Test]
        public void Level2Config_HasNoAuthoredSequenceForFlowGating()
        {
            LevelConfigSO level = LoadLevel2();

            Assert.IsFalse(LevelTutorialProgress.HasAuthoredOnboardingSequence(level),
                "Flow gating must be able to distinguish an intentional null sequence from a "
                + "level that actually authored onboarding content.");
        }

        private static LevelConfigSO LoadLevel2()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(Level2ConfigPath);
            Assert.IsNotNull(level, $"Expected a LevelConfigSO at {Level2ConfigPath}.");
            return level;
        }
    }
}
