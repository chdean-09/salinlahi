using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Ruling D1 (docs/design/spec-rulings-2026-09.md) step 5. Each era runs the same five-step
    /// reveal progression — words, words, sentence, sentence, paragraph — and a level's challenge
    /// mode is fixed by its position in that era. Until now that table lived only in a document.
    ///
    /// The check matters because a wrong mode is SILENT. A level with no challenge sequence
    /// refuses to complete at runtime and shows the content-missing panel, so it announces itself
    /// the first time anyone plays it. A level whose sequence assesses the wrong thing plays,
    /// clears and completes exactly like a correct one.
    /// </summary>
    [TestFixture]
    public sealed class ChallengeModeEraProgressionTests
    {
        private const string CampaignAssetPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        private static IReadOnlyList<ContentValidationIssue> ProgressionIssues(CampaignConfigSO campaign)
        {
            var matched = new List<ContentValidationIssue>();
            foreach (ContentValidationIssue issue in CampaignConfigValidator.Validate(campaign))
            {
                if (issue.Code == ContentValidationCode.ChallengeModeEraProgressionInvalid)
                    matched.Add(issue);
            }

            return matched;
        }

        private static string Describe(IReadOnlyList<ContentValidationIssue> issues)
        {
            var text = new StringBuilder();
            for (int i = 0; i < issues.Count; i++)
                text.AppendLine(issues[i].Path + " — " + issues[i].Message);
            return text.ToString();
        }

        // -------------------------------------------------------------------
        // The shipped campaign
        // -------------------------------------------------------------------

        /// <summary>
        /// The shipped campaign matches the progression on every authored level.
        ///
        /// This began as an exception list. Levels 14 and 15 shipped modes their era position
        /// contradicted — TimedMemory at step 4, WordPlacement at step 5 — each authored against a
        /// ticket acceptance criterion (SALIN-156 AC3, SALIN-158 AC2). Ruling D1 settled it on
        /// 2026-09-17: the reveal table is the source of truth where it and a ticket disagree, and
        /// both levels were moved onto the table's modes.
        ///
        /// Recorded because the history is the argument for the assertion. A level's mode can be
        /// wrong for a well-documented reason and stay wrong for months, because nothing playing
        /// the level can tell: it plays, clears and completes exactly like a correct one.
        ///
        /// Levels 6, 7, 8, 10 and 13 are not covered here — they have no challengeSequence at all,
        /// which this rule deliberately leaves to the runtime, where it already refuses the phase
        /// outright. When step 3 authors them, they fall under this assertion automatically.
        /// </summary>
        [Test]
        public void ShippedCampaign_MatchesTheEraProgression()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignAssetPath}.");

            IReadOnlyList<ContentValidationIssue> issues = ProgressionIssues(campaign);

            CollectionAssert.IsEmpty(issues,
                "A level whose challenge mode contradicts its era position assesses the wrong "
                + "thing and still completes, so no amount of playtesting will surface it. Each "
                + "line below names the unit, what it is, and what its position restores.\n"
                + Describe(issues));
        }

        // -------------------------------------------------------------------
        // The rule
        // -------------------------------------------------------------------

        [TestCase(0, ChallengeMode.WordPlacement)]
        [TestCase(1, ChallengeMode.WordPlacement)]
        [TestCase(2, ChallengeMode.SentenceRestoration)]
        [TestCase(3, ChallengeMode.SentenceRestoration)]
        [TestCase(4, ChallengeMode.ParagraphRestoration)]
        public void EraPosition_AcceptsItsOwnMode(int eraLocalIndex, ChallengeMode mode)
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            AssignSequence(fixture, eraLocalIndex, mode);

            CollectionAssert.IsEmpty(ProgressionIssues(fixture.Campaign),
                $"Era step {eraLocalIndex + 1} restores {mode}, so a unit in that mode is correct.");
        }

        [TestCase(0, ChallengeMode.ParagraphRestoration)]
        [TestCase(2, ChallengeMode.WordPlacement)]
        [TestCase(3, ChallengeMode.TimedMemory)]
        [TestCase(4, ChallengeMode.WordPlacement)]
        public void EraPosition_ReportsAModeItDoesNotRestore(int eraLocalIndex, ChallengeMode mode)
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            AssignSequence(fixture, eraLocalIndex, mode);

            CollectionAssert.IsNotEmpty(ProgressionIssues(fixture.Campaign),
                $"Era step {eraLocalIndex + 1} does not restore {mode}. Left unreported, the level " +
                "assesses the wrong thing and still completes.");
        }

        [Test]
        public void GuidedTracing_IsAllowedAtAnyPosition()
        {
            for (int eraLocalIndex = 0; eraLocalIndex < 5; eraLocalIndex++)
            {
                using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
                AssignSequence(fixture, eraLocalIndex, ChallengeMode.GuidedTracing);

                CollectionAssert.IsEmpty(ProgressionIssues(fixture.Campaign),
                    "GuidedTracing teaches a symbol rather than restoring text, so it sits " +
                    $"outside the progression — including at era step {eraLocalIndex + 1}.");
            }
        }

        [Test]
        public void MissingSequence_IsNotReportedHere()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();

            CollectionAssert.IsEmpty(ProgressionIssues(fixture.Campaign),
                "A level with no challengeSequence is left to the runtime, which refuses to " +
                "complete the phase and shows the content-missing panel (SALIN-223). Reporting " +
                "it here as well would flag every level of a fixture that authors no sequences.");
        }

        private void AssignSequence(CampaignTestFixture fixture, int eraLocalIndex, ChallengeMode mode)
        {
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _created.Add(sequence);
            sequence.sequenceId = "progression-fixture";
            sequence.units = new[]
            {
                new ChallengeUnitDefinition { unitId = "u1", mode = mode },
            };

            LevelConfigSO level = fixture.Campaign.eras[0].levels[eraLocalIndex];
            Assert.IsNotNull(level, "Fixture: the synthetic campaign must have five levels per era.");
            level.challengeSequence = sequence;
        }
    }
}
