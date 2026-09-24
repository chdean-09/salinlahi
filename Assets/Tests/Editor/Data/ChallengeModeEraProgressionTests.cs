using System.Collections.Generic;
using System.Linq;
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

        // -------------------------------------------------------------------
        // The shipped campaign
        // -------------------------------------------------------------------

        /// <summary>
        /// The shipped campaign's current authored mode map. The Ugnayan content landed after the
        /// original D1 progression pin and deliberately uses a sentence board for Level 6 and a
        /// mixed word/sentence sequence for Level 7. Keep those content decisions explicit here;
        /// otherwise the next campaign validation run silently treats the new assets as stale.
        /// The synthetic tests below continue to pin the general D1 validator rule.
        /// </summary>
        [Test]
        public void ShippedCampaign_MatchesTheCurrentAuthoredModeMap()
        {
            var expected = new Dictionary<int, ChallengeMode[]>
            {
                [1] = new[] { ChallengeMode.WordPlacement, ChallengeMode.WordPlacement },
                [2] = new[] { ChallengeMode.WordPlacement, ChallengeMode.WordPlacement },
                [3] = new[] { ChallengeMode.SentenceRestoration },
                [4] = new[] { ChallengeMode.SentenceRestoration },
                [5] = new[]
                {
                    ChallengeMode.ParagraphRestoration,
                    ChallengeMode.ParagraphRestoration,
                    ChallengeMode.ParagraphRestoration,
                },
                [6] = new[] { ChallengeMode.SentenceRestoration, ChallengeMode.SentenceRestoration },
                [7] = new[] { ChallengeMode.WordPlacement, ChallengeMode.SentenceRestoration },
                [8] = new[] { ChallengeMode.SentenceRestoration, ChallengeMode.SentenceRestoration },
                [9] = new[] { ChallengeMode.SentenceRestoration, ChallengeMode.SentenceRestoration },
                [10] = new[]
                {
                    ChallengeMode.ParagraphRestoration,
                    ChallengeMode.ParagraphRestoration,
                    ChallengeMode.ParagraphRestoration,
                },
                [11] = new[] { ChallengeMode.WordPlacement, ChallengeMode.WordPlacement },
                [12] = new[] { ChallengeMode.WordPlacement, ChallengeMode.WordPlacement },
                [13] = new[] { ChallengeMode.SentenceRestoration, ChallengeMode.SentenceRestoration },
                [14] = new[] { ChallengeMode.SentenceRestoration, ChallengeMode.SentenceRestoration },
                [15] = new[]
                {
                    ChallengeMode.ParagraphRestoration,
                    ChallengeMode.ParagraphRestoration,
                    ChallengeMode.ParagraphRestoration,
                },
            };

            foreach (KeyValuePair<int, ChallengeMode[]> contract in expected)
            {
                string path = $"Assets/ScriptableObjects/Levels/Level{contract.Key}_Config.asset";
                LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
                Assert.IsNotNull(level, $"Expected the authored Level {contract.Key} asset at {path}.");
                Assert.IsNotNull(level.challengeSequence,
                    $"Level {contract.Key} must assign its current authored challenge sequence.");
                CollectionAssert.AreEqual(
                    contract.Value,
                    (level.challengeSequence.units ?? System.Array.Empty<ChallengeUnitDefinition>())
                        .Where(unit => unit != null)
                        .Select(unit => unit.mode)
                        .ToArray(),
                    $"Level {contract.Key} challenge modes changed without updating its content contract.");
            }
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
