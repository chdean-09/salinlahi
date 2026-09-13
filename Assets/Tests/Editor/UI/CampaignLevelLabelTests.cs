using NUnit.Framework;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-258 — levels are presented as "Era N, Level 1 to 5", never as a global 1 to 15
    /// (docs/design/spec-rulings-2026-09.md, 2026-09-11, SALIN-258/T60).
    ///
    /// EVERY MEANINGFUL CASE HERE USES LEVELS 6-15. In Ugat — the only era a demo player ever
    /// reaches under D-015 — the era-local number EQUALS the global number for all five levels,
    /// so an Ugat-only fixture passes identically against the old global-numbering code and the
    /// new era-relative code. It would prove nothing. Global 7 = "Ugnayan Level 2" is the
    /// smallest case where the two numbers actually diverge, and it is used deliberately
    /// throughout.
    /// </summary>
    [TestFixture]
    public sealed class CampaignLevelLabelTests
    {
        [TestCase("Ugat", 1, "Ugat Level 1")]
        [TestCase("Ugnayan", 2, "Ugnayan Level 2")]
        [TestCase("Pamana", 5, "Pamana Level 5")]
        public void Format_WithAKnownEra_RendersTheEraRelativeLabel(
            string eraName, int eraLocalOrder, string expected)
        {
            Assert.AreEqual(expected, CampaignLevelLabel.Format(eraName, eraLocalOrder));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Format_WithoutAnEra_DegradesToThePlainLevelForm(string eraName)
        {
            // The legacy progress path genuinely cannot name an era. Degrading is the
            // documented contract (LevelLockNoticePanel's Prerequisite remarks); a blank
            // label there would take the whole notice off screen.
            Assert.AreEqual("Level 3", CampaignLevelLabel.Format(eraName, 3));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Format_BelowTheFirstLevel_IsEmptySoCallersStaySilent(int eraLocalOrder)
        {
            Assert.AreEqual(string.Empty, CampaignLevelLabel.Format("Ugat", eraLocalOrder));
        }

        /// <summary>
        /// The ticket's actual requirement, stated as a prohibition rather than as an expected
        /// string: for a level where the global id and the era-local order differ, the global
        /// id must not survive into the label.
        /// </summary>
        [Test]
        public void Format_ForALevelWhereGlobalAndEraLocalDiffer_NeverRendersTheGlobalNumber_SALIN258()
        {
            // Global level 7 is Ugnayan's second level.
            string label = CampaignLevelLabel.Format("Ugnayan", 2, globalLevelNumber: 7);

            Assert.AreEqual("Ugnayan Level 2", label);
            StringAssert.DoesNotContain("7", label,
                "A global 1-15 id must never reach a player-facing label.");
        }

        [Test]
        public void Format_Resilient_PrefersTheEraRelativeFormWheneverTheEraIsKnown()
        {
            Assert.AreEqual("Pamana Level 3", CampaignLevelLabel.Format("Pamana", 3, 13));
        }

        [TestCase(null, 2)]
        [TestCase("Ugnayan", 0)]
        public void Format_Resilient_FallsBackToTheGlobalNumberWhenTheEraIsNotResolved(
            string eraName, int eraLocalOrder)
        {
            // Both halves must be present to use the era form: an era name with no order, or
            // an order with no era name, is an incomplete lookup and must not be half-rendered.
            Assert.AreEqual("Level 7", CampaignLevelLabel.Format(eraName, eraLocalOrder, 7));
        }

        [Test]
        public void Format_Resilient_WithNoUsableNumberAtAll_IsEmpty()
        {
            Assert.AreEqual(string.Empty, CampaignLevelLabel.Format(null, 0, 0));
        }

        /// <summary>
        /// THE LEAK GUARD, and the durable part of this fixture.
        ///
        /// The per-string tests above pin wording, so they go stale the moment product rewrites
        /// a sentence (SALIN-291 is expected to). This one pins the RULE instead: whatever the
        /// sentences say, no player-facing level copy may contain the global id for a level
        /// whose era-local order differs from it. It fails if a future edit reintroduces a bare
        /// level number on any of these surfaces, regardless of how the sentence is worded.
        /// </summary>
        [Test]
        public void LeakGuard_NoPlayerFacingLevelCopyRendersTheGlobalNumber_SALIN258()
        {
            // Ugnayan Level 2 == global Level 7. "7" appearing anywhere below is the defect.
            const string eraName = "Ugnayan";
            const int eraLocalOrder = 2;
            const int globalLevelNumber = 7;
            string label = CampaignLevelLabel.Format(eraName, eraLocalOrder, globalLevelNumber);

            string[] playerFacingCopy =
            {
                LevelLockNoticeCopy.Prerequisite(label, crossesEra: false, requiredEraName: eraName),
                LevelLockNoticeCopy.Prerequisite(label, crossesEra: true, requiredEraName: eraName),
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.StoryViewed, label),
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.SymbolsPracticed, label),
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.WordsRestored, label),
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.ContextPassed, label),
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.FinalSyllableRestored, label),
                MemoryCardCopy.EarnInLevel(label),
            };

            foreach (string copy in playerFacingCopy)
            {
                Assert.IsNotEmpty(copy);
                StringAssert.Contains("Ugnayan Level 2", copy,
                    "Every level-naming surface must use the era-relative label: " + copy);
                StringAssert.DoesNotContain(
                    globalLevelNumber.ToString(), copy,
                    "A global 1-15 id leaked into player-facing copy: " + copy);
            }
        }
    }
}
