using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-223. Runs <see cref="LevelPhasePlan.FromConfig"/> against the REAL shipped
    /// level configs rather than hand-built ones, and pins exactly which levels report
    /// missing content.
    ///
    /// Every other fixture for the phase plan builds its own LevelConfigSO, so nothing in
    /// the repo exercised the plan against authored data. That gap is why the SALIN-220
    /// interaction was only caught at review. These tests also make the campaign-wide
    /// consequence of the refusal guard explicit and reviewable: when SALIN-248 and
    /// SALIN-249 author their content, these expectations must shrink, and a test failure
    /// here is the intended signal that the playable campaign has grown.
    /// </summary>
    [TestFixture]
    public sealed class LevelPhasePlanCampaignContentTests
    {
        private const string LevelsFolder = "Assets/ScriptableObjects/Levels";

        // Observed on dev @ 72b26b2 and unchanged by this ticket, which authors no content.
        private static readonly int[] LevelsWithoutAChallengeSequence = { 6, 7, 8, 10, 13 };
        private static readonly int[] LevelsWithoutMemoryReward = { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        [Test]
        public void EveryShippedLevel_PlansContextChallengeAndMemoryReward_SALIN223()
        {
            foreach (LevelConfigSO level in LoadLevels())
            {
                LevelPhasePlan plan = LevelPhasePlan.FromConfig(level);
                Assert.IsTrue(plan.Has(LevelPhase.ContextChallenge),
                    $"Level {level.levelNumber} must plan ContextChallenge.");
                Assert.IsTrue(plan.Has(LevelPhase.MemoryReward),
                    $"Level {level.levelNumber} must plan MemoryReward.");
            }
        }

        [Test]
        public void ExactlyLevelsSixSevenEightTenAndThirteen_ReportAMissingChallenge_SALIN223()
        {
            List<int> missing = LoadLevels()
                .Where(level => LevelPhasePlan.FromConfig(level).ContextChallengeContentMissing)
                .Select(level => level.levelNumber)
                .OrderBy(number => number)
                .ToList();

            CollectionAssert.AreEqual(LevelsWithoutAChallengeSequence, missing,
                "These levels cannot be completed until SALIN-249 authors their challenge "
                + "sequences. If this list shrank, content landed and the expectation should "
                + "shrink with it; if it grew, a level config lost its challenge.");
        }

        [Test]
        public void ExactlyLevelsSixThroughFifteen_ReportAMissingMemoryReward_SALIN223()
        {
            List<int> missing = LoadLevels()
                .Where(level => LevelPhasePlan.FromConfig(level).MemoryRewardContentMissing)
                .Select(level => level.levelNumber)
                .OrderBy(number => number)
                .ToList();

            CollectionAssert.AreEqual(LevelsWithoutMemoryReward, missing,
                "Ten levels, not the five the SALIN-223 ticket summary names: rewardIds is "
                + "empty on 6 through 15. These cannot be completed until SALIN-248 authors "
                + "both rewardIds and contextMedia.cutscene — the plan requires both keys.");
        }

        /// <summary>
        /// SALIN-226. The pin that this ticket authors no content and changes no shipped
        /// level's behaviour: <c>flowSegments</c> is empty on all fifteen levels, so every
        /// one of them plans exactly one Defense/ContextChallenge pass and runs the flow it
        /// ran before the alternating loop existed.
        ///
        /// When SALIN-247 / SALIN-249 / SALIN-252 author their segment lists, this
        /// expectation must change with them — and a failure here that is NOT accompanied by
        /// authored segments means a level lost or gained a segment list by accident.
        /// SegmentPlanInvalid staying false is the stronger half: it would go true if a
        /// level authored segments that its own waves or challenge sequence cannot honour.
        /// </summary>
        [Test]
        public void EveryShippedLevel_RunsASingleUnsegmentedFlowPass_SALIN226()
        {
            foreach (LevelConfigSO level in LoadLevels())
            {
                LevelPhasePlan plan = LevelPhasePlan.FromConfig(level);
                Assert.AreEqual(1, plan.SegmentCount,
                    $"Level {level.levelNumber} must still run one Defense/ContextChallenge "
                    + "pass: SALIN-226 ships the engine, not the content.");
                Assert.IsFalse(plan.SegmentPlanInvalid,
                    $"Level {level.levelNumber} reports an unusable segment plan.");
                Assert.AreEqual(0, plan.Segments.Count,
                    $"Level {level.levelNumber} must author no flow segments yet.");
            }
        }

        private static List<LevelConfigSO> LoadLevels()
        {
            List<LevelConfigSO> levels = AssetDatabase
                .FindAssets("t:LevelConfigSO", new[] { LevelsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelConfigSO>)
                .Where(level => level != null)
                .ToList();

            // A broken Library makes LoadAssetAtPath return null for every ScriptableObject,
            // which would silently empty these collections and turn the assertions green.
            Assert.AreEqual(15, levels.Count,
                $"Expected the fifteen authored level configs under {LevelsFolder}. A count of "
                + "zero means the AssetDatabase could not load them, not that they are absent.");
            return levels;
        }
    }
}
