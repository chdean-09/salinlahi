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
        /// The mandated challenge-less fixture.
        ///
        /// SALIN-223 changed what this fixture has to encode. ContextChallenge is now planned on
        /// every level, so a level with no authored challenge no longer skips the phase — it
        /// refuses to complete it, and the flow never reaches AtomicSave, which is the only
        /// caller of the resolver. The resolver is therefore unreachable on such a level in real
        /// play, and this test now covers the rule itself rather than a reachable campaign state:
        /// a planned-AND-completed ContextChallenge satisfies both flags it produces.
        /// </summary>
        [Test]
        public void Resolve_LevelThatAuthorsNoChallengeSequence_TreatsWordsAndContextAsSatisfied_SALIN220()
        {
            LevelConfigSO config = CreateFullyAuthoredConfig();
            config.challengeSequence = null;
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);
            Assert.IsTrue(plan.Has(LevelPhase.ContextChallenge),
                "precondition (SALIN-223): ContextChallenge is planned whether or not content exists");
            Assert.IsTrue(plan.ContextChallengeContentMissing,
                "precondition: and the plan reports the missing content rather than hiding it");

            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(
                plan,
                new[] { LevelPhase.Story, LevelPhase.RequiredPractice, LevelPhase.ContextChallenge });

            Assert.IsTrue(flags.wordsRestored);
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
            // SALIN-223: the sixth level plans ContextChallenge despite authoring none, so the
            // completed set has to include it for the gate to open. In real play that level
            // never reaches the resolver at all — the flow refuses to complete phase 6 and so
            // never runs AtomicSave.
            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(
                plan,
                new[] { LevelPhase.Story, LevelPhase.RequiredPractice, LevelPhase.ContextChallenge });

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
            // SALIN-223: ContextChallenge is planned on a null config too, so it joins Story as
            // an objective a legacy plan can leave false. That is why it is completed below.
            Assert.IsTrue(legacy.Has(LevelPhase.ContextChallenge));

            LevelObjectiveFlags flags = LevelObjectiveFlagResolver.Resolve(
                legacy, new[] { LevelPhase.ContextChallenge });

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
            // SALIN-223: ContextChallenge is planned on a null config as well, so it has to be
            // completed here too for every objective to be satisfied.
            AssertAllSatisfied(LevelObjectiveFlagResolver.Resolve(
                LevelPhasePlan.FromConfig(null),
                new[] { LevelPhase.Story, LevelPhase.ContextChallenge }));
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

        // ---------------------------------------------------------------------
        // SALIN-226 × SALIN-220 interaction
        // ---------------------------------------------------------------------

        [Test]
        public void Resolve_AfterAThreeSegmentRun_WritesTheSameFiveFlagsAsAnUnsegmentedRun_SALIN226()
        {
            LevelConfigSO segmented = CreateSegmentedConfig(3);
            LevelPhasePlan segmentedPlan = LevelPhasePlan.FromConfig(segmented);
            Assert.AreEqual(3, segmentedPlan.SegmentCount, "Fixture: the plan must be segmented.");

            LevelFlowMachine machine = new LevelFlowMachine(segmentedPlan);
            machine.Begin();
            int guard = 0;
            while (machine.Phase != LevelPhase.AtomicSave && !machine.IsTerminal && guard++ < 32)
                machine.ReportPhaseComplete(machine.Phase);
            Assert.AreEqual(LevelPhase.AtomicSave, machine.Phase,
                "Fixture: the run must reach the save the resolver is called from.");

            LevelObjectiveFlags segmentedFlags =
                LevelObjectiveFlagResolver.Resolve(segmentedPlan, machine.CompletedPhases);

            LevelPhasePlan unsegmentedPlan = LevelPhasePlan.FromConfig(CreateFullyAuthoredConfig());
            LevelObjectiveFlags unsegmentedFlags = LevelObjectiveFlagResolver.Resolve(
                unsegmentedPlan,
                new[]
                {
                    LevelPhase.Story,
                    LevelPhase.RequiredPractice,
                    LevelPhase.ContextChallenge,
                });

            Assert.AreEqual(unsegmentedFlags.storyViewed, segmentedFlags.storyViewed);
            Assert.AreEqual(unsegmentedFlags.symbolsPracticed, segmentedFlags.symbolsPracticed);
            Assert.AreEqual(unsegmentedFlags.wordsRestored, segmentedFlags.wordsRestored);
            Assert.AreEqual(unsegmentedFlags.contextPassed, segmentedFlags.contextPassed);
            Assert.AreEqual(
                unsegmentedFlags.finalSyllableRestored, segmentedFlags.finalSyllableRestored);
            Assert.IsTrue(segmentedFlags.wordsRestored,
                "A fully completed segmented run satisfies the challenge objective.");
        }

        /// <summary>
        /// SALIN-226 × SALIN-220, pinned rather than reasoned about. CompletedPhases is a
        /// HashSet, so ContextChallenge is recorded at the end of segment 1 while segments
        /// 2..N are still pending — meaning the resolver WOULD report the challenge
        /// objective satisfied mid-run if anything asked it to.
        ///
        /// That is harmless today for exactly one reason, asserted here: the resolver's sole
        /// call site is ComputeCompletionResults, reached only from ExecuteAtomicSave, and
        /// AtomicSave is unreachable until the last segment completes. If a future ticket
        /// resolves these flags any earlier, this stops being harmless and becomes a live
        /// defect that unlocks the next level on a half-finished run.
        /// </summary>
        [Test]
        public void Resolve_MidRunAfterSegmentOne_WouldReportTheChallengeDone_ButAtomicSaveIsUnreachable_SALIN226()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateSegmentedConfig(3));
            LevelFlowMachine machine = new LevelFlowMachine(plan);
            machine.Begin();
            int guard = 0;
            while (machine.Phase != LevelPhase.ContextChallenge && !machine.IsTerminal && guard++ < 32)
                machine.ReportPhaseComplete(machine.Phase);

            machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            Assert.AreEqual(1, machine.CurrentSegmentIndex, "Setup: segment 1 of 3 finished.");
            Assert.AreEqual(LevelPhase.Defense, machine.Phase);

            LevelObjectiveFlags midRun =
                LevelObjectiveFlagResolver.Resolve(plan, machine.CompletedPhases);
            Assert.IsTrue(midRun.wordsRestored,
                "Documented consequence of the HashSet: the flag is already true mid-run.");
            Assert.IsTrue(midRun.contextPassed);

            // The reason it does not matter: nothing can commit from here.
            Assert.AreNotEqual(LevelPhase.AtomicSave, machine.Phase);
            Assert.IsFalse(machine.HasCompleted(LevelPhase.AtomicSave));
            Assert.IsFalse(machine.ReportSaveResult(accepted: true),
                "AtomicSave — the only path to the resolver — is unreachable mid-loop.");
        }

        private LevelConfigSO CreateSegmentedConfig(int segmentCount)
        {
            LevelConfigSO config = CreateFullyAuthoredConfig();
            var units = new List<ChallengeUnitDefinition>();
            for (int i = 0; i < segmentCount; i++)
            {
                units.Add(new ChallengeUnitDefinition { unitId = $"seg-unit-{i}" });
                config.waves.Add(new WaveDefinition());
                config.flowSegments.Add(new LevelFlowSegment
                {
                    waveCount = 1,
                    challengeUnitIds = new[] { $"seg-unit-{i}" },
                });
            }

            config.challengeSequence.units = units.ToArray();
            return config;
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
