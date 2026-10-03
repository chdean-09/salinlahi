using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Covers the stained-badge readability policy: a badge showing a false face must change
    /// slowly enough to read, and must not look like a genuine glyph while it does.
    ///
    /// Deliberately UnityEngine-free, like <see cref="DrawTargetResolverTests"/>.
    /// </summary>
    public sealed class GlyphStainCycleTests
    {
        private const float Tolerance = 0.0001f;

        // --- Timing floor -------------------------------------------------------------------

        [Test]
        public void TrueFaceDwellIsRaisedToTheReadabilityFloor()
        {
            // The face the player must actually read and act on can never be brief.
            Assert.That(GlyphStainCycle.ResolveInterval(0.18f, 0.36f, 0f),
                Is.EqualTo(GlyphStainCycle.MinimumReadableInterval).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveInterval(0.18f, 0.36f, 1f),
                Is.EqualTo(GlyphStainCycle.MinimumReadableInterval).Within(Tolerance));
        }

        [Test]
        public void ReadabilityFloorIsClearlySlowerThanTheOldAuthoredMaximum()
        {
            Assert.That(GlyphStainCycle.MinimumReadableInterval, Is.GreaterThan(0.36f * 2f));
            Assert.That(GlyphStainCycle.DefaultTrueMinInterval,
                Is.GreaterThanOrEqualTo(GlyphStainCycle.MinimumReadableInterval));
            Assert.That(GlyphStainCycle.DefaultTrueMaxInterval,
                Is.GreaterThan(GlyphStainCycle.DefaultTrueMinInterval));
        }

        // --- Asymmetry: wrong faces churn, the true face rests ------------------------------

        [Test]
        public void FalseFacesChurnFarFasterThanTheTrueFaceRests()
        {
            float fastest = GlyphStainCycle.ResolveFalseInterval(
                GlyphStainCycle.DefaultFalseMinInterval, GlyphStainCycle.DefaultFalseMaxInterval, 1f);
            float shortestTrue = GlyphStainCycle.ResolveInterval(
                GlyphStainCycle.DefaultTrueMinInterval, GlyphStainCycle.DefaultTrueMaxInterval, 0f);

            Assert.That(fastest, Is.LessThan(shortestTrue * 0.5f),
                "A wrong face must be gone well before the player could mistake it for the answer.");
        }

        [Test]
        public void FalseFaceHasItsOwnFloorSoItCanChurnWithoutStrobing()
        {
            Assert.That(GlyphStainCycle.MinimumFalseGlyphInterval,
                Is.LessThan(GlyphStainCycle.MinimumReadableInterval),
                "The churn floor must be faster than the read floor, or there is no asymmetry.");

            // Fast, but never a hard strobe - the wrong face is dimmed, not flashed.
            Assert.That(GlyphStainCycle.MinimumFalseGlyphInterval, Is.GreaterThanOrEqualTo(0.2f));
            Assert.That(GlyphStainCycle.ResolveFalseInterval(0.01f, 0.02f, 0f),
                Is.EqualTo(GlyphStainCycle.MinimumFalseGlyphInterval).Within(Tolerance));
        }

        [Test]
        public void AWrongFaceIsNeverHeldLongerThanTheTrueFace()
        {
            var cycle = new GlyphStainCycle(now: 0f, unitRandom: 1f);
            float firstFalseDwell = cycle.NextToggleTime;

            // Churn through the whole burst, then land on the true face.
            float t = 0f;
            for (int i = 0; i < 64 && cycle.IsFalseGlyphVisible; i++)
            {
                t = cycle.NextToggleTime;
                cycle.Advance(t, 1f);
            }

            Assert.That(cycle.IsFalseGlyphVisible, Is.False, "the cycle must reach the true face");
            float trueDwell = cycle.NextToggleTime - t;
            Assert.That(trueDwell, Is.GreaterThan(firstFalseDwell),
                "the true face must rest longer than any wrong face churns");
        }

        [Test]
        public void TheBurstShowsSeveralWrongFacesBeforeTheTrueOne()
        {
            var cycle = new GlyphStainCycle(now: 0f, unitRandom: 0f);

            int falseFaces = 1; // the cycle opens on a wrong face
            float t = 0f;
            for (int i = 0; i < 64 && cycle.IsFalseGlyphVisible; i++)
            {
                t = cycle.NextToggleTime;
                cycle.Advance(t, 0f);
                if (cycle.IsFalseGlyphVisible) falseFaces++;
            }

            Assert.That(falseFaces, Is.GreaterThanOrEqualTo(2),
                "'scroll fast' means several wrong faces, not one slow alternation.");
            Assert.That(falseFaces, Is.EqualTo(GlyphStainCycle.DefaultFalseBurstCount));
        }

        [Test]
        public void EveryChurnStepAsksForAFreshWrongFace()
        {
            var cycle = new GlyphStainCycle(now: 0f, unitRandom: 0f);
            Assert.That(cycle.NeedsNewFalseGlyph, Is.True, "the opening wrong face must be rolled");

            float t = cycle.NextToggleTime;
            bool changed = cycle.Advance(t, 0f);

            Assert.That(changed, Is.True);
            Assert.That(cycle.IsFalseGlyphVisible, Is.True, "still mid-burst");
            Assert.That(cycle.NeedsNewFalseGlyph, Is.True,
                "a churn step that reuses the same wrong glyph does not read as scrolling");
        }

        [Test]
        public void IntervalInterpolatesBetweenAuthoredBoundsAboveTheFloor()
        {
            Assert.That(GlyphStainCycle.ResolveInterval(2f, 4f, 0f), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveInterval(2f, 4f, 0.5f), Is.EqualTo(3f).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveInterval(2f, 4f, 1f), Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void ReversedAndOutOfRangeInputsStayInsideTheAuthoredBand()
        {
            Assert.That(GlyphStainCycle.ResolveInterval(4f, 2f, 0f), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveInterval(2f, 4f, -5f), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveInterval(2f, 4f, 9f), Is.EqualTo(4f).Within(Tolerance));
        }

        // --- Cycle --------------------------------------------------------------------------

        [Test]
        public void CycleStartsOnAWrongFaceAndHoldsItForItsWholeDwell()
        {
            var cycle = new GlyphStainCycle(now: 0f, unitRandom: 0f);
            float dwell = cycle.NextToggleTime;

            Assert.That(cycle.IsFalseGlyphVisible, Is.True);
            Assert.That(cycle.Advance(dwell - 0.01f, 0f), Is.False, "toggled before the dwell elapsed");
            Assert.That(cycle.IsFalseGlyphVisible, Is.True);
        }

        [Test]
        public void CycleReturnsToTheChurnAfterTheTrueFaceRests()
        {
            var cycle = new GlyphStainCycle(now: 0f, unitRandom: 0f);

            float t = 0f;
            for (int i = 0; i < 64 && cycle.IsFalseGlyphVisible; i++)
            {
                t = cycle.NextToggleTime;
                cycle.Advance(t, 0f);
            }

            Assert.That(cycle.IsFalseGlyphVisible, Is.False);

            cycle.Advance(cycle.NextToggleTime, 0f);
            Assert.That(cycle.IsFalseGlyphVisible, Is.True, "the stain resumes churning");
        }

        // --- False-glyph tell ---------------------------------------------------------------

        [Test]
        public void ShownGlyphMatchingTheTrueSymbolIsNotFalse()
        {
            Assert.That(GlyphStainCycle.IsFalseGlyph("symbol.na", "symbol.na"), Is.False);
            Assert.That(GlyphStainCycle.IsFalseGlyph("SYMBOL.NA", "symbol.na"), Is.False);
        }

        [Test]
        public void ShownGlyphDifferingFromTheTrueSymbolIsFalse()
        {
            Assert.That(GlyphStainCycle.IsFalseGlyph("symbol.ma", "symbol.na"), Is.True);
        }

        [Test]
        public void UnknownIdsAreNeverClaimedFalse()
        {
            Assert.That(GlyphStainCycle.IsFalseGlyph(null, "symbol.na"), Is.False);
            Assert.That(GlyphStainCycle.IsFalseGlyph("", "symbol.na"), Is.False);
            Assert.That(GlyphStainCycle.IsFalseGlyph("symbol.ma", null), Is.False);
        }

        [Test]
        public void FalseFaceRendersDimmerThanATrueFace()
        {
            float trueAlpha = GlyphStainCycle.ResolveBadgeAlpha(1f, showingFalseGlyph: false,
                GlyphStainCycle.DefaultFalseGlyphAlpha);
            float falseAlpha = GlyphStainCycle.ResolveBadgeAlpha(1f, showingFalseGlyph: true,
                GlyphStainCycle.DefaultFalseGlyphAlpha);

            Assert.That(trueAlpha, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(falseAlpha, Is.LessThan(trueAlpha));
            Assert.That(falseAlpha, Is.GreaterThan(0f), "a false face must stay readable, not vanish");
        }

        [Test]
        public void FalseFaceDimMultipliesTheRoutineAlphaInsteadOfOverwritingIt()
        {
            // Mid-swap the coroutine owns alpha 0.5; the tell must ride on top of it.
            Assert.That(GlyphStainCycle.ResolveBadgeAlpha(0.5f, true, 0.4f),
                Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveBadgeAlpha(0f, true, 0.4f),
                Is.EqualTo(0f).Within(Tolerance), "a hidden badge stays hidden");
        }

        [Test]
        public void BadgeAlphaClampsDegenerateInputs()
        {
            Assert.That(GlyphStainCycle.ResolveBadgeAlpha(5f, false, 0.4f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveBadgeAlpha(-1f, true, 0.4f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(GlyphStainCycle.ResolveBadgeAlpha(1f, true, 5f), Is.EqualTo(1f).Within(Tolerance));
        }
    }
}
