using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-256 — the rendering rule behind the main menu's "Ugat Level 3  ·  13%" line.
    ///
    /// WHOLE STRINGS, NEVER Contains(). SALIN-258 found two existing assertions in this project
    /// — Contains("4") and Contains("5") — that were true under BOTH the old and the new
    /// behaviour and therefore proved nothing. Every assertion below pins the complete string,
    /// so a change to the separator, the order of the parts, or the rounding rule fails here.
    ///
    /// These fixtures are built with CreateInstance rather than loaded from disk on purpose:
    /// the rule under test is arithmetic and string formatting, and an authored campaign would
    /// make the test depend on content that other tickets legitimately edit.
    /// </summary>
    [TestFixture]
    public sealed class MainMenuProgressLineTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
            _created.Clear();
        }

        [Test]
        public void UgatLevelThreeWithTwoComplete_RendersTheAcceptanceCriterionStringVerbatim()
        {
            CampaignConfigSO campaign = BuildCampaign();

            // The acceptance criterion's own example: at Ugat Level 3 the player has finished
            // levels 1 and 2, and 2/15 = 13.3% -> 13%.
            Assert.AreEqual(
                "Ugat Level 3  ·  13%",
                MainMenuProgressLine.Format(campaign, 3, 2, 15),
                "This is the exact string SALIN-256's acceptance criterion names. A change to "
                + "the separator, the ordering or the rounding breaks the criterion.");
        }

        [Test]
        public void OneOfFifteen_FloorsToSixPercent_RatherThanRoundingToSeven()
        {
            CampaignConfigSO campaign = BuildCampaign();

            // 1/15 = 6.66%. Floor -> 6, round-half-up -> 7. THIS CASE IS THE ONLY THING THAT
            // PINS THE ROUNDING RULE: the criterion's own 13% example is satisfied by both.
            Assert.AreEqual(
                "Ugat Level 2  ·  6%",
                MainMenuProgressLine.Format(campaign, 2, 1, 15),
                "Completion percentage must floor. Rounding up would let the line reach 100% "
                + "before the final level is actually complete.");
        }

        [Test]
        public void FreshSave_RendersZeroPercentAsACleanString()
        {
            CampaignConfigSO campaign = BuildCampaign();

            Assert.AreEqual(
                "Ugat Level 1  ·  0%",
                MainMenuProgressLine.Format(campaign, 1, 0, 15),
                "A brand-new save must render a clean 0%, not a blank line and not NaN.");
        }

        [Test]
        public void EveryLevelComplete_RendersOneHundredPercent()
        {
            CampaignConfigSO campaign = BuildCampaign();

            Assert.AreEqual(
                "Ugat Level 5  ·  100%",
                MainMenuProgressLine.Format(campaign, 5, 15, 15));
        }

        [Test]
        public void ZeroTotalLevels_DoesNotDivideByZero()
        {
            CampaignConfigSO campaign = BuildCampaign();

            Assert.AreEqual(
                "Ugat Level 1  ·  0%",
                MainMenuProgressLine.Format(campaign, 1, 0, 0),
                "A campaign that reports no levels is a load failure, not a finished journey.");
        }

        [Test]
        public void LevelOutsideTheCampaign_DegradesToTheGlobalNumberInsteadOfThrowing()
        {
            CampaignConfigSO campaign = BuildCampaign();

            // Level 9 is not in this fixture's single era, so the era cannot be named. The
            // CampaignLevelLabel fallback keeps the copy true rather than blank.
            Assert.AreEqual(
                "Level 9  ·  13%",
                MainMenuProgressLine.Format(campaign, 9, 2, 15));
        }

        [Test]
        public void EraLocalOrder_IsReadFromTheAuthoredField_NotRecomputedFromTheGlobalNumber()
        {
            // Level 3 is authored with eraLocalOrder = 5. Recomputing the order as
            // ((3 - 1) % 5 + 1) would yield 3 and render "Ugat Level 3" — which is why this
            // fixture deliberately authors an order that disagrees with the formula.
            CampaignConfigSO campaign = BuildCampaign(levelThreeAuthoredOrder: 5);

            Assert.AreEqual(
                "Ugat Level 5  ·  13%",
                MainMenuProgressLine.Format(campaign, 3, 2, 15),
                "eraLocalOrder must be READ from LevelConfigSO, never recomputed. Recomputing "
                + "bypasses the invariant CampaignConfigValidator enforces and silently "
                + "produces wrong labels the moment an era is not exactly five levels.");
        }

        [Test]
        public void NullCampaignAndUnnameableLevel_RenderNothingRatherThanABareSeparator()
        {
            // No campaign and no level number to fall back on: there is nothing truthful to
            // say, so the line stays silent instead of showing a stray "·  13%".
            Assert.AreEqual(
                MainMenuProgressCopy.ProgressUnavailable,
                MainMenuProgressLine.Format(null, 0, 2, 15));
        }

        /// <summary>
        /// One era, "Ugat", carrying levels 1-5 with authored eraLocalOrder matching their
        /// global number unless the caller asks for a deliberate disagreement.
        /// </summary>
        private CampaignConfigSO BuildCampaign(int levelThreeAuthoredOrder = 3)
        {
            var campaign = ScriptableObject.CreateInstance<CampaignConfigSO>();
            _created.Add(campaign);

            var era = ScriptableObject.CreateInstance<EraConfigSO>();
            _created.Add(era);
            era.eraName = "Ugat";
            era.order = 1;
            era.levels = new List<LevelConfigSO>();

            for (int levelNumber = 1; levelNumber <= 5; levelNumber++)
            {
                var level = ScriptableObject.CreateInstance<LevelConfigSO>();
                _created.Add(level);
                level.levelNumber = levelNumber;
                level.eraLocalOrder = levelNumber == 3 ? levelThreeAuthoredOrder : levelNumber;
                era.levels.Add(level);
            }

            campaign.eras = new List<EraConfigSO> { era };
            return campaign;
        }
    }
}
