using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Tasks 1-5 built the gated-finale engine, validator, escort budget and data opt-in for
    /// Levels 2-4, but every existing test constructs its own synthetic level config. Nothing
    /// asserted that the SHIPPED campaign actually opts in: the feature could ship switched off
    /// in <c>CampaignConfig_RevisedV1.asset</c> and all of those tests would still pass, because
    /// none of them ever loads that asset.
    ///
    /// This fixture closes that hole directly against the shipped campaign. Levels 2-4 must carry
    /// <see cref="SpawnAssignmentPolicy.gateFinalSlotToFinalWave"/> and an unbounded escort budget
    /// (<c>maxOverflowBatches == 0</c>, read through <see cref="SpawnAssignmentPolicy.OverflowIsUnbounded"/>).
    /// Levels 1 and 5 are a scope guarantee, not an incidental detail: the whole change was scoped
    /// to Levels 2-4 (Level 1 is tutorial pacing, Level 5's segment design is unsettled), so both
    /// must still read exactly as they did before this feature existed — gate off, and the
    /// historical default budget of 12 escort batches untouched.
    /// </summary>
    [TestFixture]
    public sealed class GatedFinaleCampaignOptInTests
    {
        private const string CampaignAssetPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        private const int DefaultMaxOverflowBatches = 12;

        private static CampaignConfigSO LoadCampaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignAssetPath}.");
            return campaign;
        }

        private static LevelConfigSO GetLevel(CampaignConfigSO campaign, string stableId)
        {
            Assert.IsTrue(campaign.TryGetLevel(stableId, out LevelConfigSO level),
                $"Expected the shipped campaign to contain {stableId}.");
            return level;
        }

        [TestCase("level.ugat.02", "Level 2")]
        [TestCase("level.ugat.03", "Level 3")]
        [TestCase("level.ugat.04", "Level 4")]
        public void GatedLevel_OptsIntoFinaleGatingWithUnboundedEscorts(string stableId, string label)
        {
            CampaignConfigSO campaign = LoadCampaign();
            LevelConfigSO level = GetLevel(campaign, stableId);

            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave,
                $"{label} ({stableId}) ships with gateFinalSlotToFinalWave = false. The gated-" +
                "finale feature is scoped to Levels 2-4; with this level opted out, its finale " +
                "can complete early and the feature is inert in the shipping build even though " +
                "every synthetic-fixture test for the engine, validator and escort budget still " +
                "passes.");

            Assert.AreEqual(0, level.spawnAssignmentPolicy.maxOverflowBatches,
                $"{label} ({stableId}) must ship maxOverflowBatches = 0 (unbounded escorts, via " +
                "SpawnAssignmentPolicy.OverflowIsUnbounded) so a gated finale can never run out " +
                "of overflow batches before its final slot is restored. A bounded budget here " +
                "means the level can deadlock instead of finishing.");

            Assert.IsTrue(level.spawnAssignmentPolicy.OverflowIsUnbounded,
                $"{label} ({stableId}): maxOverflowBatches = " +
                $"{level.spawnAssignmentPolicy.maxOverflowBatches} does not read as unbounded " +
                "through OverflowIsUnbounded, which is the property the escort director actually " +
                "consults.");
        }

        [TestCase("level.ugat.01", "Level 1")]
        [TestCase("level.ugat.05", "Level 5")]
        public void OutOfScopeLevel_StaysUngatedWithDefaultEscortBudget(string stableId, string label)
        {
            CampaignConfigSO campaign = LoadCampaign();
            LevelConfigSO level = GetLevel(campaign, stableId);

            Assert.IsFalse(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave,
                $"{label} ({stableId}) now has gateFinalSlotToFinalWave = true. The gated-finale " +
                "change was scoped to Levels 2-4 only: Level 1 is tutorial pacing and Level 5's " +
                "segment design is unsettled, so neither may pick up this behavior as a side " +
                "effect of authoring Levels 2-4.");

            Assert.AreEqual(DefaultMaxOverflowBatches, level.spawnAssignmentPolicy.maxOverflowBatches,
                $"{label} ({stableId})'s maxOverflowBatches must stay the historical default of " +
                $"{DefaultMaxOverflowBatches}. Changing it here would alter escort behavior for a " +
                "level this feature was never meant to touch.");
        }
    }
}
