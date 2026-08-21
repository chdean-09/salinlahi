using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    public sealed class ActiveClueSelectorTests
    {
        private static ClueCandidate Candidate(
            string id, float distance, long sequence, bool eligible = true)
            => new ClueCandidate(id, distance, sequence, eligible);

        [Test]
        public void SelectIndex_EmptyList_ReturnsNegativeOne()
        {
            Assert.That(ActiveClueSelector.SelectIndex(new List<ClueCandidate>()),
                Is.EqualTo(-1));
        }

        [Test]
        public void SelectIndex_NullList_ReturnsNegativeOne()
        {
            Assert.That(ActiveClueSelector.SelectIndex(null), Is.EqualTo(-1));
        }

        [Test]
        public void SelectIndex_NoEligibleCandidates_ReturnsNegativeOne()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 1f, 1, eligible: false),
                Candidate("symbol.ma", 2f, 2, eligible: false),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(-1));
        }

        [Test]
        public void SelectIndex_PicksClosestToBase()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 5f, 1),
                Candidate("symbol.ma", 2f, 2),
                Candidate("symbol.na", 9f, 3),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }

        [Test]
        public void SelectIndex_SkipsIneligibleEvenWhenClosest()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 1f, 1, eligible: false),
                Candidate("symbol.ma", 4f, 2),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }

        [Test]
        public void SelectIndex_PairedEnemies_BreaksTieOnLowestSpawnSequence()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 3f, 77),
                Candidate("symbol.ma", 3f, 12),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }

        [Test]
        public void SelectIndex_PairedEnemies_IsIndependentOfListOrder()
        {
            var ascending = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 3f, 12),
                Candidate("symbol.ma", 3f, 77),
            };
            var descending = new List<ClueCandidate>
            {
                Candidate("symbol.ma", 3f, 77),
                Candidate("symbol.ba", 3f, 12),
            };

            Assert.That(ascending[ActiveClueSelector.SelectIndex(ascending)].SpawnSequence,
                Is.EqualTo(12));
            Assert.That(descending[ActiveClueSelector.SelectIndex(descending)].SpawnSequence,
                Is.EqualTo(12));
        }

        [Test]
        public void SelectIndex_DistancesWithinEpsilon_TreatedAsTie()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 3f, 90),
                Candidate("symbol.ma", 3f + (ActiveClueSelector.TieEpsilon * 0.5f), 4),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1),
                "Two enemies on the same row will not have bit-identical Y values.");
        }

        // Epsilon comparison is not transitive: a chain of three candidates can have each
        // adjacent pair within TieEpsilon while the endpoints are further apart than it.
        // Comparing only against the running best therefore made the winner depend on list
        // order, which is exactly what the spec says this function must never do.
        [Test]
        public void SelectIndex_ThreeChainedNearEqualDistances_IsIndependentOfListOrder()
        {
            const float step = ActiveClueSelector.TieEpsilon * 0.8f;

            var ascending = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 0f, 3),
                Candidate("symbol.ma", step, 2),
                Candidate("symbol.na", step * 2f, 1),
            };
            var descending = new List<ClueCandidate>
            {
                Candidate("symbol.na", step * 2f, 1),
                Candidate("symbol.ma", step, 2),
                Candidate("symbol.ba", 0f, 3),
            };

            ClueCandidate ascendingWinner = ascending[ActiveClueSelector.SelectIndex(ascending)];
            ClueCandidate descendingWinner = descending[ActiveClueSelector.SelectIndex(descending)];

            Assert.That(descendingWinner.SpawnSequence, Is.EqualTo(ascendingWinner.SpawnSequence),
                "The same candidate set must produce the same winner regardless of order.");

            // The tie band is [globalMin, globalMin + TieEpsilon]. Sequence 3 sits at the
            // global minimum and sequence 2 is inside the band with a lower sequence, so
            // sequence 2 wins. Sequence 1 sits at 1.6x epsilon and is outside the band.
            Assert.That(ascendingWinner.SpawnSequence, Is.EqualTo(2),
                "The lowest spawn sequence within the tie band of the global minimum must win.");
            Assert.That(ascendingWinner.DistanceToBase - 0f,
                Is.LessThanOrEqualTo(ActiveClueSelector.TieEpsilon),
                "The winner must lie within the tie band of the globally closest candidate.");
        }

        [Test]
        public void SelectIndex_MultipleLanes_UsesDistanceOnlyNotLaneOrder()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 6f, 1),
                Candidate("symbol.ma", 6f, 2),
                Candidate("symbol.na", 5.5f, 3),
            };

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(2));
        }

        [Test]
        public void SelectIndex_ArmoredTargetStaysSelectedAcrossRepeatedCalls()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 2f, 1),
                Candidate("symbol.ma", 8f, 2),
            };

            int first = ActiveClueSelector.SelectIndex(candidates);
            int second = ActiveClueSelector.SelectIndex(candidates);

            Assert.That(first, Is.EqualTo(0));
            Assert.That(second, Is.EqualTo(first), "Selection must be a pure function of input.");
        }

        [Test]
        public void SelectIndex_AfterTargetRemoval_SelectsNextClosest()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("symbol.ba", 2f, 1),
                Candidate("symbol.ma", 8f, 2),
            };
            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(0));

            candidates[0] = Candidate("symbol.ba", 2f, 1, eligible: false);

            Assert.That(ActiveClueSelector.SelectIndex(candidates), Is.EqualTo(1));
        }
    }

    public sealed class LevelConfigClueObjectiveSourceTests
    {
        [Test]
        public void IsClueCombatActive_FalseWhenLevelDoesNotArmClueCombat()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = false;

            var source = new LevelConfigClueObjectiveSource(level, () => true);

            Assert.IsFalse(source.IsClueCombatActive);
            Object.DestroyImmediate(level);
        }

        [Test]
        public void IsClueCombatActive_FalseWhenNotPlaying()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;

            var source = new LevelConfigClueObjectiveSource(level, () => false);

            Assert.IsFalse(source.IsClueCombatActive,
                "Clue combat must not be active while paused or between phases.");
            Object.DestroyImmediate(level);
        }

        [Test]
        public void IsClueCombatActive_TrueWhenArmedAndPlaying()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;

            var source = new LevelConfigClueObjectiveSource(level, () => true);

            Assert.IsTrue(source.IsClueCombatActive);
            Object.DestroyImmediate(level);
        }

        [Test]
        public void IsClueCombatActive_FalseWhenLevelIsNull()
        {
            var source = new LevelConfigClueObjectiveSource(null, () => true);

            Assert.IsFalse(source.IsClueCombatActive);
        }

        [Test]
        public void CurrentObjectiveContentIds_ReturnsCanonicalStableIdsWithoutDuplicates()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            BaybayinCharacterSO ba = CreateSymbol("BA", "symbol.ba");
            BaybayinCharacterSO ma = CreateSymbol("MA", "symbol.ma");

            level.learningRequirements.Add(Requirement(ba));
            level.practiceRequirements.Add(Requirement(ma));
            // Same symbol in both lists must collapse to one id.
            level.practiceRequirements.Add(Requirement(ba));

            var source = new LevelConfigClueObjectiveSource(level, () => true);
            var ids = new List<string>(source.CurrentObjectiveContentIds);

            Assert.That(ids, Is.EquivalentTo(new[] { "symbol.ba", "symbol.ma" }),
                "Objective ids must be canonical stableIds, not combat characterIDs.");

            Object.DestroyImmediate(ba);
            Object.DestroyImmediate(ma);
            Object.DestroyImmediate(level);
        }

        [Test]
        public void CurrentObjectiveContentIds_SkipsRequirementsWithNoSymbol()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.learningRequirements.Add(new ContentRequirement());
            level.learningRequirements.Add(Requirement(null));

            var source = new LevelConfigClueObjectiveSource(level, () => true);

            Assert.That(source.CurrentObjectiveContentIds, Is.Empty,
                "A requirement with no symbol must not produce an empty or null id.");

            Object.DestroyImmediate(level);
        }

        private static ContentRequirement Requirement(BaybayinCharacterSO symbol)
        {
            return new ContentRequirement
            {
                kind = ContentRequirementKind.Practice,
                symbolValue = new SymbolValueReference { symbol = symbol },
                requiredSuccesses = 1,
            };
        }

        private static BaybayinCharacterSO CreateSymbol(string characterId, string stableId)
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.stableId = stableId;
            return symbol;
        }
    }
}
