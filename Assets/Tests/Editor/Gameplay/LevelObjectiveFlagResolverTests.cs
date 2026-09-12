using System.Collections.Generic;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-220 D2 — an objective the level does not author counts as SATISFIED.
    /// </summary>
    /// <remarks>
    /// This is the rule that stops the ticket bricking the campaign, and it is the one that no
    /// persistence fixture can catch: those all use Level 1, which authors every phase. Five of
    /// the fifteen levels author no challenge sequence, so a literal "all five must be true" gate
    /// would leave them permanently unable to unlock their successor, with every existing test
    /// still green. Hence a deliberate fixture on a challenge-less level below.
    /// </remarks>
    [TestFixture]
    public sealed class LevelObjectiveFlagResolverTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void Resolve_FullyAuthoredLevelWithEveryPhaseCompleted_SetsAllFiveFlagsTrue_SALIN220()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateFullyAuthoredConfig());

            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(
                plan,
                new[] { LevelPhase.Story, LevelPhase.RequiredPractice, LevelPhase.ContextChallenge });

            AssertAllSatisfied(flags);
        }

        /// <summary>
        /// The mandated challenge-less fixture. Levels 6, 7, 8, 10 and 13 author no challenge
        /// sequence, so ContextChallenge is never planned and never completed there. Both flags it
        /// would produce must still come out true or those levels can never unlock a successor.
        /// </summary>
        [Test]
        public void Resolve_LevelThatAuthorsNoChallengeSequence_TreatsWordsAndContextAsSatisfied_SALIN220()
        {
            LevelConfigSO config = CreateFullyAuthoredConfig();
            config.challengeSequence = null;
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);
            Assert.IsFalse(plan.Has(LevelPhase.ContextChallenge),
                "precondition: a level with no challenge sequence plans no ContextChallenge phase");

            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(
                plan, new[] { LevelPhase.Story, LevelPhase.RequiredPractice });

            Assert.IsTrue(flags.wordsRestored,
                "Unauthored means satisfied. False here hard-locks levels 6, 7, 8, 10 and 13.");
            Assert.IsTrue(flags.contextPassed);
            AssertAllSatisfied(flags);
        }

        /// <summary>
        /// The same rule against the campaign fixture's SIXTH level rather than a hand-built
        /// config, so the claim is about a real position in the configured level order.
        /// </summary>
        [Test]
        public void Resolve_SixthConfiguredLevelWithoutAChallenge_SatisfiesEveryObjective_SALIN220()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(fixture.Campaign);
            Assert.IsTrue(fixture.Campaign.TryGetLevel(levelIds[5], out LevelConfigSO sixth));
            Assert.IsNull(sixth.challengeSequence,
                "precondition: the sixth configured level authors no challenge sequence");

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(sixth);
            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(
                plan, new[] { LevelPhase.Story, LevelPhase.RequiredPractice });

            AssertAllSatisfied(flags);
            Assert.IsTrue(LevelObjectiveGate.AllSatisfied(RecordFrom(flags)),
                "The gate must open for a level that authored none of the missing objectives.");
        }

        /// <summary>
        /// The other half of D2: an objective the level DOES author and the player did NOT finish
        /// stays false. Without this the rule would be "always true" rather than "satisfied when
        /// unauthored", and the mechanism 228/229/242 build on would not exist.
        /// </summary>
        [Test]
        public void Resolve_AuthoredPhaseThatWasNotCompleted_LeavesItsObjectiveFalse_SALIN220()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateFullyAuthoredConfig());

            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(
                plan, new[] { LevelPhase.Story });

            Assert.IsTrue(flags.storyViewed);
            Assert.IsFalse(flags.symbolsPracticed, "RequiredPractice was planned and not completed.");
            Assert.IsFalse(flags.wordsRestored, "ContextChallenge was planned and not completed.");
            Assert.IsFalse(flags.contextPassed);
            Assert.AreEqual(LevelObjectives.SymbolsPracticed,
                LevelObjectiveGate.FirstUnsatisfied(RecordFrom(flags)));
        }

        [Test]
        public void Resolve_FinalSyllableIsAlwaysSatisfiedUntilSALIN229_SALIN220()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateFullyAuthoredConfig());

            // Nothing completed at all: the one flag with no producer anywhere is still true.
            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(plan, new LevelPhase[0]);

            Assert.IsTrue(flags.finalSyllableRestored,
                "LevelConfigSO.finalRestorationValue has no runtime reader until SALIN-229. " +
                "When 229 lands, this expectation changes with it.");
        }

        [Test]
        public void Resolve_WithNoPlanAtAll_DegradesToAllSatisfied_SALIN220()
        {
            AssertAllSatisfied(LevelObjectiveFlagResolver.Resolve(null, null));
        }

        /// <summary>
        /// Story is the one objective whose phase <see cref="LevelPhasePlan.Has"/> plans
        /// UNCONDITIONALLY, so it is the only one of the five that a level authoring no revised
        /// content can still leave false. In real play the driver auto-completes the Story phase
        /// even on the levels with no authored story, which is why this stays a no-op in
        /// production — but the resolver must not hand it out for free, or the flag would mean
        /// nothing once SALIN-242 gives it a real producer.
        /// </summary>
        [Test]
        public void Resolve_LegacyPlanWithNothingCompleted_SatisfiesAllButStory_SALIN220()
        {
            LevelPhasePlan legacy = LevelPhasePlan.FromConfig(null);
            Assert.IsTrue(legacy.Has(LevelPhase.Story), "precondition: Story is always planned");
            Assert.IsFalse(legacy.Has(LevelPhase.RequiredPractice));
            Assert.IsFalse(legacy.Has(LevelPhase.ContextChallenge));

            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(legacy, new LevelPhase[0]);

            Assert.IsFalse(flags.storyViewed, "Story was planned and never completed.");
            Assert.IsTrue(flags.symbolsPracticed);
            Assert.IsTrue(flags.wordsRestored);
            Assert.IsTrue(flags.contextPassed);
            Assert.IsTrue(flags.finalSyllableRestored);
        }

        [Test]
        public void Resolve_LegacyPlanWithTheStoryPhaseCompleted_SatisfiesEveryObjective_SALIN220()
        {
            // What a level with no authored story actually produces: the Story phase is planned,
            // the executor finds nothing to play, and the driver auto-completes it.
            AssertAllSatisfied(LevelObjectiveFlagResolver.Resolve(
                LevelPhasePlan.FromConfig(null), new[] { LevelPhase.Story }));
        }

        [TestCase(LevelObjectives.StoryViewed)]
        [TestCase(LevelObjectives.SymbolsPracticed)]
        [TestCase(LevelObjectives.WordsRestored)]
        [TestCase(LevelObjectives.ContextPassed)]
        [TestCase(LevelObjectives.FinalSyllableRestored)]
        public void Resolve_WithTheEditorFaultSet_ClearsExactlyThatObjective_SALIN220(string objectiveId)
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateFullyAuthoredConfig());
            LevelPhase[] completed = { LevelPhase.Story, LevelPhase.RequiredPractice, LevelPhase.ContextChallenge };

            LevelObjectiveFlagResolver.EditorForceUnsatisfied = objectiveId;
            try
            {
                LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(plan, completed);

                Assert.AreEqual(objectiveId, LevelObjectiveGate.FirstUnsatisfied(RecordFrom(flags)),
                    "The AC6 fault menu must clear exactly the objective it names.");
            }
            finally
            {
                LevelObjectiveFlagResolver.EditorForceUnsatisfied = null;
            }

            AssertAllSatisfied(LevelObjectiveFlagResolver.Resolve(plan, completed));
        }

        private static void AssertAllSatisfied(LevelObjectiveFlags flags)
        {
            Assert.IsTrue(flags.storyViewed);
            Assert.IsTrue(flags.symbolsPracticed);
            Assert.IsTrue(flags.wordsRestored);
            Assert.IsTrue(flags.contextPassed);
            Assert.IsTrue(flags.finalSyllableRestored);
        }

        private static LevelProgressRecord RecordFrom(LevelObjectiveFlags flags)
        {
            return new LevelProgressRecord
            {
                levelId = "level.ugat.01",
                storyViewed = flags.storyViewed,
                symbolsPracticed = flags.symbolsPracticed,
                wordsRestored = flags.wordsRestored,
                contextPassed = flags.contextPassed,
                finalSyllableRestored = flags.finalSyllableRestored,
            };
        }

        private LevelConfigSO CreateFullyAuthoredConfig()
        {
            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(config);
            config.focusWords.Add(new FocusWordDefinition());
            config.learningRequirements.Add(new ContentRequirement());
            config.practiceRequirements.Add(new ContentRequirement());
            config.rewardIds.Add("reward.test");

            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _objectsToDestroy.Add(sequence);
            config.challengeSequence = sequence;
            return config;
        }
    }
}
