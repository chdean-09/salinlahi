using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Ruling D1 (docs/design/spec-rulings-2026-09.md). ExecuteContextChallenge used to run
    /// ExecuteCombatRestoration and then yield break before the board was reached, so no
    /// combat-restoration level had ever shown its authored challenge. The board is restored
    /// for SentenceRestoration and ParagraphRestoration units, keyed on unit mode rather than
    /// level number, and WordPlacement stays retired because slot-fill during Defense subsumes it.
    ///
    /// The routing itself is a coroutine inside a MonoBehaviour and needs a scene; the DECISION
    /// is <see cref="LevelFlowController.SelectPostCombatBoardUnitIds"/>, which is pure and is
    /// what this fixture pins — both as a rule and against the shipped campaign.
    ///
    /// The shipped-campaign half matters more than it looks. Every synthetic assertion below
    /// would still pass if Levels 3-5 shipped with their modes flipped to WordPlacement, or if
    /// someone re-introduced the early yield break: the rule would be correct and inert. So the
    /// campaign tests assert a NON-EMPTY selection on the three levels that actually gained a
    /// board, which is the assertion that fails if the board is retired again.
    /// </summary>
    [TestFixture]
    public sealed class PostCombatChallengeBoardSelectionTests
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

        private ChallengeSequenceSO Sequence(params (string id, ChallengeMode mode)[] units)
        {
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _created.Add(sequence);
            sequence.sequenceId = "selection-fixture";

            var definitions = new ChallengeUnitDefinition[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                definitions[i] = new ChallengeUnitDefinition
                {
                    unitId = units[i].id,
                    mode = units[i].mode,
                };
            }

            sequence.units = definitions;
            return sequence;
        }

        private static List<string> Select(
            ChallengeSequenceSO sequence,
            IReadOnlyList<string> segmentUnitIds = null,
            bool isSegmented = false)
        {
            return LevelFlowController.SelectPostCombatBoardUnitIds(
                sequence, segmentUnitIds, isSegmented);
        }

        // -------------------------------------------------------------------
        // The rule
        // -------------------------------------------------------------------

        [TestCase(ChallengeMode.SentenceRestoration)]
        [TestCase(ChallengeMode.ParagraphRestoration)]
        public void RestorationUnit_OpensTheBoard(ChallengeMode mode)
        {
            CollectionAssert.AreEqual(
                new[] { "u1" },
                Select(Sequence(("u1", mode))),
                $"{mode} is part of the reveal progression D1 restores, so it must still play " +
                "on the board after the combat-restoration pass.");
        }

        [TestCase(ChallengeMode.WordPlacement)]
        [TestCase(ChallengeMode.GuidedTracing)]
        [TestCase(ChallengeMode.TimedMemory)]
        public void NonRestorationUnit_StaysRetired(ChallengeMode mode)
        {
            CollectionAssert.IsEmpty(
                Select(Sequence(("u1", mode))),
                $"{mode} must not open a board after combat restoration. WordPlacement is " +
                "subsumed by slot-fill during Defense; GuidedTracing and TimedMemory are not " +
                "part of the progression D1 restores. Selecting one here would make the player " +
                "do the same work twice, or play a mode the level never intended.");
        }

        [Test]
        public void MixedSequence_SelectsOnlyTheRestorationUnits_InAuthoredOrder()
        {
            ChallengeSequenceSO sequence = Sequence(
                ("place-1", ChallengeMode.WordPlacement),
                ("line-1", ChallengeMode.SentenceRestoration),
                ("place-2", ChallengeMode.WordPlacement),
                ("para-1", ChallengeMode.ParagraphRestoration));

            CollectionAssert.AreEqual(
                new[] { "line-1", "para-1" },
                Select(sequence),
                "An unsegmented level plays its restoration units in authored order, with the " +
                "WordPlacement units dropped rather than reordered around.");
        }

        [Test]
        public void SegmentedLevel_SelectsOnlyItsOwnUnits_InSegmentOrder()
        {
            ChallengeSequenceSO sequence = Sequence(
                ("line-1", ChallengeMode.SentenceRestoration),
                ("line-2", ChallengeMode.SentenceRestoration),
                ("line-3", ChallengeMode.SentenceRestoration));

            CollectionAssert.AreEqual(
                new[] { "line-3", "line-1" },
                Select(sequence, new[] { "line-3", "line-1" }, isSegmented: true),
                "Segment order wins over authored order, matching the list the segmented path " +
                "hands to ChallengeFlowController.Play — which plays a subset in the order named.");
        }

        [Test]
        public void SegmentedLevel_WithAWaveOnlySegment_SelectsNothing()
        {
            ChallengeSequenceSO sequence = Sequence(("line-1", ChallengeMode.SentenceRestoration));

            CollectionAssert.IsEmpty(
                Select(sequence, new string[0], isSegmented: true),
                "A trailing wave-only segment names no units. It must select nothing so the " +
                "combat pass completes the phase itself, rather than opening an empty board.");
            CollectionAssert.IsEmpty(
                Select(sequence, null, isSegmented: true),
                "A null segment list is the same case and must not throw.");
        }

        [Test]
        public void SegmentedLevel_IgnoresIdsTheSequenceDoesNotContain()
        {
            ChallengeSequenceSO sequence = Sequence(("line-1", ChallengeMode.SentenceRestoration));

            CollectionAssert.AreEqual(
                new[] { "line-1" },
                Select(sequence, new[] { "ghost", "line-1" }, isSegmented: true),
                "An unknown id is already refused upstream by TryResolveCombatRestorationTargets; " +
                "selection must not invent an entry for it, because ChallengeFlowController " +
                "refuses to play a subset naming a unit the sequence does not contain.");
        }

        [Test]
        public void MissingContent_SelectsNothingRatherThanThrowing()
        {
            CollectionAssert.IsEmpty(Select(null), "A null sequence must select nothing.");

            ChallengeSequenceSO empty = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _created.Add(empty);
            empty.units = null;
            CollectionAssert.IsEmpty(Select(empty), "A null units array must select nothing.");

            ChallengeSequenceSO holed = Sequence(("line-1", ChallengeMode.SentenceRestoration));
            holed.units = new[] { null, holed.units[0], new ChallengeUnitDefinition
            {
                unitId = string.Empty, mode = ChallengeMode.SentenceRestoration,
            } };
            CollectionAssert.AreEqual(
                new[] { "line-1" },
                Select(holed),
                "A null unit or one with no unitId cannot be named in a subset, so it is skipped " +
                "rather than crashing the phase or producing an unplayable id.");
        }

        // -------------------------------------------------------------------
        // The shipped campaign
        // -------------------------------------------------------------------

        private static LevelConfigSO ShippedLevel(string stableId)
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignAssetPath}.");
            Assert.IsTrue(campaign.TryGetLevel(stableId, out LevelConfigSO level),
                $"Expected the shipped campaign to contain {stableId}.");
            return level;
        }

        private static bool UsesCombatRestoration(LevelConfigSO level)
        {
            return level.activeClueCombatEnabled && level.activeClueRestorationEnabled;
        }

        [TestCase("level.ugat.03", "Level 3")]
        [TestCase("level.ugat.04", "Level 4")]
        [TestCase("level.ugat.05", "Level 5")]
        public void ShippedRestorationLevel_StillOpensItsBoardAfterCombat(string stableId, string label)
        {
            LevelConfigSO level = ShippedLevel(stableId);

            Assert.IsTrue(UsesCombatRestoration(level),
                $"{label} ({stableId}) must stay on the combat-restoration path for this " +
                "assertion to mean anything. If it opted out, the board would be reached by the " +
                "ordinary route and this fixture would pass without covering D1 at all.");

            CollectionAssert.IsNotEmpty(
                LevelFlowController.SelectPostCombatBoardUnitIds(
                    level.challengeSequence, null, isSegmented: false),
                $"{label} ({stableId}) selects no board units, so its authored challenge never " +
                "plays — the exact defect D1 ruled on. Either its units were flipped away from " +
                "SentenceRestoration/ParagraphRestoration, or the selection rule was narrowed.");
        }

        [TestCase("level.ugat.01", "Level 1")]
        [TestCase("level.ugat.02", "Level 2")]
        public void ShippedWordLevel_StaysOnSlotFillAlone(string stableId, string label)
        {
            LevelConfigSO level = ShippedLevel(stableId);

            Assert.IsTrue(UsesCombatRestoration(level),
                $"{label} ({stableId}) is expected on the combat-restoration path.");

            CollectionAssert.IsEmpty(
                LevelFlowController.SelectPostCombatBoardUnitIds(
                    level.challengeSequence, null, isSegmented: false),
                $"{label} ({stableId}) is a word level. D1 keeps WordPlacement retired because " +
                "the player already assembled those words during Defense; opening a board here " +
                "would make them do it twice.");
        }
    }
}
