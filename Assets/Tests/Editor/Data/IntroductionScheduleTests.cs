using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// The authored introduction plan. Which level introduces which corruption type is now declared
    /// in one asset rather than inferred from the wave tables, so it can be read and changed.
    ///
    /// <para>
    /// The shipped-asset half is the one that matters. Every synthetic assertion below would still
    /// pass if the campaign shipped with no schedule wired at all, or with Level 2 naming Abo — the
    /// rule would be correct and the data wrong, which is exactly the shape of the defect this
    /// replaces.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class IntroductionScheduleTests
    {
        private const string CampaignPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        private static CampaignConfigSO Campaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignPath}.");
            return campaign;
        }

        private static EnemyDataSO Enemy(string assetName)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                $"Assets/ScriptableObjects/Enemies/EnemyData_{assetName}.asset");
            Assert.IsNotNull(data, assetName);
            return data;
        }

        private static LevelConfigSO Level(int number)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"Assets/ScriptableObjects/Levels/Level{number}_Config.asset");
            Assert.IsNotNull(level, $"Level {number}");
            return level;
        }

        // -------------------------------------------------------------------
        // The shipped plan
        // -------------------------------------------------------------------

        [Test]
        public void ShippedCampaign_CarriesTheSchedule()
        {
            Assert.IsNotNull(Campaign().introductionSchedule,
                "Without the schedule assigned, introductions fall back to the first-encounter rule "
                + "and the whole authored plan is inert in the shipping build.");
        }

        [Test]
        public void LevelTwo_IntroducesOnlyItsOwnTwoTypes()
        {
            IntroductionScheduleSO schedule = Campaign().introductionSchedule;
            LevelConfigSO levelTwo = Level(2);

            Assert.IsTrue(schedule.Introduces(levelTwo, Enemy("Bakod")), "Bakod debuts on Level 2.");
            Assert.IsTrue(schedule.Introduces(levelTwo, Enemy("Takip")), "Takip debuts on Level 2.");

            foreach (string borrowed in new[] { "AbongSimula", "Iligaw", "Mantsa", "NawalangMukha" })
            {
                Assert.IsFalse(schedule.Introduces(levelTwo, Enemy(borrowed)),
                    $"{borrowed} belongs to Level 1. Level 2's wave roster spawns it, so before the "
                    + "schedule existed a player who skipped Level 1 met it here — three borrowed "
                    + "cards on top of the two this level exists to teach.");
            }
        }

        [Test]
        public void LevelOne_KeepsItsFourTypes()
        {
            IntroductionScheduleSO schedule = Campaign().introductionSchedule;
            LevelConfigSO levelOne = Level(1);

            foreach (string own in new[] { "AbongSimula", "Iligaw", "Mantsa", "NawalangMukha" })
            {
                Assert.IsTrue(schedule.Introduces(levelOne, Enemy(own)),
                    $"{own} must still be introduced somewhere, and Level 1 is where it is met.");
            }
        }

        [Test]
        public void EveryWaveReachableType_IsIntroducedBySomeLevel()
        {
            IntroductionScheduleSO schedule = Campaign().introductionSchedule;
            var missing = new System.Collections.Generic.List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
                if (data == null || data.suppressDiscovery || string.IsNullOrWhiteSpace(data.displayName))
                    continue;
                if (path.Contains("Boss_"))
                    continue;

                bool named = false;
                foreach (IntroductionScheduleSO.LevelIntroductions entry in schedule.levels)
                {
                    if (entry?.level != null && schedule.Introduces(entry.level, data))
                    {
                        named = true;
                        break;
                    }
                }

                if (!named)
                    missing.Add(data.displayName);
            }

            CollectionAssert.IsEmpty(missing,
                "The schedule is the whole truth, so a type no level names is a type the player "
                + "never has explained to them. Add it to the level that should teach it: "
                + string.Join(", ", missing));
        }

        // -------------------------------------------------------------------
        // The rule
        // -------------------------------------------------------------------

        [Test]
        public void UnknownLevel_IntroducesNothing()
        {
            IntroductionScheduleSO schedule = Campaign().introductionSchedule;
            var stranger = ScriptableObject.CreateInstance<LevelConfigSO>();
            stranger.stableId = "level.not.in.the.plan";

            try
            {
                Assert.IsFalse(schedule.Introduces(stranger, Enemy("Bakod")),
                    "A level with no entry introduces nothing.");
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void NullArguments_AreRefusedRatherThanThrowing()
        {
            IntroductionScheduleSO schedule = Campaign().introductionSchedule;
            Assert.IsFalse(schedule.Introduces(null, Enemy("Bakod")));
            Assert.IsFalse(schedule.Introduces(Level(2), null));
        }
    }
}
