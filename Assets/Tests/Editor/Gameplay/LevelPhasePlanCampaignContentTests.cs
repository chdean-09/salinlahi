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
    /// consequence of the refusal guard explicit and reviewable: every shipped level must carry
    /// the content needed by its planned phases.
    /// </summary>
    [TestFixture]
    public sealed class LevelPhasePlanCampaignContentTests
    {
        private const string LevelsFolder = "Assets/ScriptableObjects/Levels";

        // All fifteen shipped demo levels now carry their authored challenge and reward data.
        private static readonly int[] LevelsWithoutAChallengeSequence = { };
        private static readonly int[] LevelsWithoutMemoryReward = { };

        // SALIN-283. Levels that legitimately author a flowSegments list, mapped to the number
        // of segments they author. Every level ABSENT from this map is still held at a single
        // unsegmented pass, so an accidental segment list anywhere else still fails the test.
        private static readonly Dictionary<int, int> ExpectedSegmentCounts = new Dictionary<int, int>
        {
            { 5, 3 },
            { 10, 3 },
        };

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
        public void NoShippedLevel_ReportsAMissingChallenge()
        {
            List<int> missing = LoadLevels()
                .Where(level => LevelPhasePlan.FromConfig(level).ContextChallengeContentMissing)
                .Select(level => level.levelNumber)
                .OrderBy(number => number)
                .ToList();

            CollectionAssert.AreEqual(LevelsWithoutAChallengeSequence, missing,
                "Every shipped level must carry a challenge sequence.");
        }

        [Test]
        public void NoShippedLevel_ReportsAMissingMemoryReward()
        {
            List<int> missing = LoadLevels()
                .Where(level => LevelPhasePlan.FromConfig(level).MemoryRewardContentMissing)
                .Select(level => level.levelNumber)
                .OrderBy(number => number)
                .ToList();

            CollectionAssert.AreEqual(LevelsWithoutMemoryReward, missing,
                "Every shipped level must carry its memory reward content.");
        }

        /// <summary>
        /// SALIN-226, narrowed by SALIN-283. Every shipped level runs one Defense/ContextChallenge
        /// pass unless it deliberately authors a segment list. A failure here that is NOT
        /// accompanied by authored segments means a level lost or gained a segment list by accident.
        ///
        /// D-010 authorises Levels 5, 10 and 15 to author segments. Levels 5 and 10 currently
        /// author three segments; Level 15 remains boss-driven without a flowSegments list.
        ///
        /// SegmentPlanInvalid staying false is the stronger half and stays UNCONDITIONAL for
        /// all fifteen levels: it goes true when a level authors segments that its own waves
        /// or challenge sequence cannot honour.
        /// </summary>
        [Test]
        public void EveryShippedLevel_RunsItsAuthoredFlowPasses_SALIN226()
        {
            foreach (LevelConfigSO level in LoadLevels())
            {
                LevelPhasePlan plan = LevelPhasePlan.FromConfig(level);

                Assert.IsFalse(plan.SegmentPlanInvalid,
                    $"Level {level.levelNumber} reports an unusable segment plan.");

                if (ExpectedSegmentCounts.TryGetValue(level.levelNumber, out int expectedSegments))
                {
                    Assert.AreEqual(expectedSegments, plan.SegmentCount,
                        $"Level {level.levelNumber} authors an alternating flow and must plan "
                        + "exactly its authored number of Defense/ContextChallenge passes.");
                    Assert.AreEqual(expectedSegments, plan.Segments.Count,
                        $"Level {level.levelNumber}'s authored segments were rejected, so the plan "
                        + "collapsed back to a single unsegmented pass.");
                    continue;
                }

                Assert.AreEqual(1, plan.SegmentCount,
                    $"Level {level.levelNumber} must still run one Defense/ContextChallenge "
                    + "pass: SALIN-226 ships the engine, not the content.");
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
