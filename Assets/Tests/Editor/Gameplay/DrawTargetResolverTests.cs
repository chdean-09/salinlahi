using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Covers the multi-target draw policy: a correct draw resolves against ANY eligible
    /// on-screen enemy carrying the drawn glyph, not only the one closest to the base.
    ///
    /// Deliberately UnityEngine-free, like <see cref="ActiveClueSelectorTests"/>, so every
    /// targeting rule is an EditMode assertion rather than a scene rehearsal.
    /// </summary>
    public sealed class DrawTargetResolverTests
    {
        private const int ChainThreshold = 3;

        private static ClueCandidate Candidate(
            string id, float distance, long sequence, bool eligible = true)
            => new ClueCandidate(id, distance, sequence, eligible);

        private static List<int> Resolve(
            IReadOnlyList<ClueCandidate> candidates,
            string drawnId,
            bool chainAllMatches = false,
            int chainThreshold = ChainThreshold)
        {
            var targets = new List<int>();
            DrawTargetResolver.SelectTargets(
                candidates, drawnId, chainAllMatches, chainThreshold, targets);
            return targets;
        }

        private static string Show(List<int> targets) => string.Join(",", targets);

        // ---------------------------------------------------------------- misses

        [Test]
        public void SelectTargets_NullCandidates_ResolvesToNothing()
        {
            Assert.That(Show(Resolve(null, "ba")), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SelectTargets_EmptyCandidates_ResolvesToNothing()
        {
            Assert.That(Show(Resolve(new List<ClueCandidate>(), "ba")), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SelectTargets_NullDrawnId_ResolvesToNothing()
        {
            var candidates = new List<ClueCandidate> { Candidate("ba", 1f, 1) };
            Assert.That(Show(Resolve(candidates, null)), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SelectTargets_NoEnemyCarriesTheGlyph_ResolvesToNothing()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("ka", 1f, 1),
                Candidate("da", 2f, 2),
            };

            Assert.That(Show(Resolve(candidates, "ba")), Is.EqualTo(string.Empty));
        }

        [Test]
        public void SelectTargets_OnlyIneligibleCarrierMatches_ResolvesToNothing()
        {
            // Bosses, dying, phased-out and resolution-blocked enemies all arrive here as
            // IsEligible=false and are unreachable by any draw. Iligaw's false copy is NOT one of
            // them — it arrives eligible and is struck like any other carrier; see
            // ActiveClueDecoyTargetingTests for that rule and for where its credit is withheld.
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 1f, 1, eligible: false),
                Candidate("ka", 2f, 2),
            };

            Assert.That(Show(Resolve(candidates, "ba")), Is.EqualTo(string.Empty));
        }

        // ------------------------------------------------- the behaviour change

        [Test]
        public void SelectTargets_MatchingEnemyIsNotTheClosest_StillResolves()
        {
            // THE feature. Index 1 is closest to the base but carries a different glyph.
            // Before multi-target this draw was a flat miss; now it kills the "ba" carrier.
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 9f, 1),
                Candidate("ka", 1f, 2),
            };

            Assert.That(Show(Resolve(candidates, "ba")), Is.EqualTo("0"));
        }

        [Test]
        public void SelectTargets_NonMatchingEnemiesAreNeverTargeted()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("ka", 1f, 1),
                Candidate("ba", 4f, 2),
                Candidate("da", 5f, 3),
                Candidate("ba", 6f, 4),
                Candidate("ba", 7f, 5),
            };

            Assert.That(
                Show(Resolve(candidates, "ba", chainAllMatches: true)),
                Is.EqualTo("1,3,4"));
        }

        // ------------------------------------------------------ how many die

        [Test]
        public void SelectTargets_ChainDisabled_ResolvesOnlyTheClosestCarrier()
        {
            // Level 1 authors multiKillChainEnabled=0, so one draw is one kill even when
            // several carriers are on screen. Targetability widened; lethality did not.
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 7f, 1),
                Candidate("ba", 3f, 2),
                Candidate("ba", 5f, 3),
            };

            Assert.That(Show(Resolve(candidates, "ba")), Is.EqualTo("1"));
        }

        [Test]
        public void SelectTargets_ChainEnabledBelowThreshold_ResolvesOnlyTheClosestCarrier()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 7f, 1),
                Candidate("ba", 3f, 2),
            };

            Assert.That(
                Show(Resolve(candidates, "ba", chainAllMatches: true)),
                Is.EqualTo("1"));
        }

        [Test]
        public void SelectTargets_ChainEnabledAtThreshold_ResolvesEveryCarrierClosestFirst()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 7f, 1),
                Candidate("ba", 3f, 2),
                Candidate("ba", 5f, 3),
            };

            Assert.That(
                Show(Resolve(candidates, "ba", chainAllMatches: true)),
                Is.EqualTo("1,2,0"));
        }

        [Test]
        public void SelectTargets_ChainCountsOnlyEligibleCarriers()
        {
            // Three carriers on screen but one is unreachable (shielded, phased out or dying), so
            // the real count is 2 and the chain must not arm. Mirrors CombatResolver's
            // realMatchCount rule.
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 7f, 1),
                Candidate("ba", 3f, 2),
                Candidate("ba", 5f, 3, eligible: false),
            };

            Assert.That(
                Show(Resolve(candidates, "ba", chainAllMatches: true)),
                Is.EqualTo("1"));
        }

        // ------------------------------------------------------- determinism

        [Test]
        public void SelectTargets_SingleTargetIsOrderIndependent()
        {
            var ascending = new List<ClueCandidate>
            {
                Candidate("ba", 3f, 2),
                Candidate("ba", 5f, 3),
                Candidate("ba", 7f, 1),
            };

            Assert.That(Show(Resolve(ascending, "ba")), Is.EqualTo("0"));
        }

        [Test]
        public void SelectTargets_TiedCarriersBreakOnSpawnSequence()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 3f, 9),
                Candidate("ba", 3f, 4),
            };

            Assert.That(Show(Resolve(candidates, "ba")), Is.EqualTo("1"));
        }

        [Test]
        public void SelectTargets_ClearsCallerBufferBeforeFilling()
        {
            var targets = new List<int> { 41, 42 };
            DrawTargetResolver.SelectTargets(
                new List<ClueCandidate> { Candidate("ka", 1f, 1) },
                "ba",
                false,
                ChainThreshold,
                targets);

            Assert.That(targets.Count, Is.EqualTo(0));
        }

        // ------------------------------------------------------- CountMatches

        [Test]
        public void CountMatches_CountsOnlyEligibleCarriersOfTheDrawnGlyph()
        {
            var candidates = new List<ClueCandidate>
            {
                Candidate("ba", 1f, 1),
                Candidate("ba", 2f, 2, eligible: false),
                Candidate("ka", 3f, 3),
                Candidate("ba", 4f, 4),
            };

            Assert.That(DrawTargetResolver.CountMatches(candidates, "ba"), Is.EqualTo(2));
        }

        [Test]
        public void CountMatches_NullInputs_ReturnZero()
        {
            Assert.That(DrawTargetResolver.CountMatches(null, "ba"), Is.EqualTo(0));
            Assert.That(
                DrawTargetResolver.CountMatches(
                    new List<ClueCandidate> { Candidate("ba", 1f, 1) }, null),
                Is.EqualTo(0));
        }
    }
}
