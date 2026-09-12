using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Exhaustive transition coverage for the pure-C# LF-CONTRACT-v2 phase machine
    /// (SALIN-178). PlayMode coverage of the coroutine host lives in
    /// LevelFlowControllerPhaseTests; this fixture is the authority on legality of
    /// every report in every state.
    /// </summary>
    [TestFixture]
    public sealed class LevelFlowMachineTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        private static readonly LevelPhase[] PlayablePhases =
        {
            LevelPhase.Story,
            LevelPhase.FocusWords,
            LevelPhase.SymbolLearning,
            LevelPhase.RequiredPractice,
            LevelPhase.Defense,
            LevelPhase.ContextChallenge,
            LevelPhase.MemoryReward,
            LevelPhase.AtomicSave,
            LevelPhase.Results,
        };

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

        // ---------------------------------------------------------------------
        // LevelPhasePlan
        // ---------------------------------------------------------------------

        // SALIN-223 inverted these two. They previously asserted that a config with no
        // revised content planned NEITHER ContextChallenge nor MemoryReward, which is the
        // behaviour the ticket reverses: an unplanned phase is skipped with no executor
        // involvement, so such a level completed on wave clear alone.
        [Test]
        public void Plan_NullConfig_PlansLegacyPhasesAndTheAlwaysPlannedContentPhases()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(null);

            AssertPlanned(plan, LevelPhase.Story, LevelPhase.Defense, LevelPhase.ContextChallenge,
                LevelPhase.MemoryReward, LevelPhase.AtomicSave, LevelPhase.Results);
        }

        [Test]
        public void Plan_LegacyConfigWithoutRevisedContent_StillPlansContextChallengeAndMemoryReward()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateLegacyConfig());

            AssertPlanned(plan, LevelPhase.Story, LevelPhase.Defense, LevelPhase.ContextChallenge,
                LevelPhase.MemoryReward, LevelPhase.AtomicSave, LevelPhase.Results);
        }

        [Test]
        public void Plan_FocusWordsAuthored_PlansFocusWordsPhase()
        {
            LevelConfigSO config = CreateLegacyConfig();
            config.focusWords.Add(new FocusWordDefinition());

            Assert.IsTrue(LevelPhasePlan.FromConfig(config).Has(LevelPhase.FocusWords));
        }

        [Test]
        public void Plan_LearningRequirementsAuthored_PlansSymbolLearningPhase()
        {
            LevelConfigSO config = CreateLegacyConfig();
            config.learningRequirements.Add(new ContentRequirement());

            Assert.IsTrue(LevelPhasePlan.FromConfig(config).Has(LevelPhase.SymbolLearning));
        }

        [Test]
        public void Plan_PracticeRequirementsAuthored_PlansRequiredPracticePhase()
        {
            LevelConfigSO config = CreateLegacyConfig();
            config.practiceRequirements.Add(new ContentRequirement());

            Assert.IsTrue(LevelPhasePlan.FromConfig(config).Has(LevelPhase.RequiredPractice));
        }

        [Test]
        public void Plan_ChallengeSequenceAuthored_PlansContextChallengePhase()
        {
            LevelConfigSO config = CreateLegacyConfig();
            config.challengeSequence = CreateChallengeSequence();

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);
            Assert.IsTrue(plan.Has(LevelPhase.ContextChallenge));
            Assert.IsFalse(plan.ContextChallengeContentMissing,
                "An authored challenge sequence is exactly what the content predicate looks for.");
        }

        [Test]
        public void Plan_ChallengePrototypeEnabled_DoesNotPlanContextChallengePhase()
        {
            // The prototype path plays the challenge as a pre-wave tutorial
            // replacement inside the Defense executor; planning phase 6 as well
            // would run the same sequence twice.
            LevelConfigSO config = CreateLegacyConfig();
            config.challengeSequence = CreateChallengeSequence();
            config.challengePrototypeEnabled = true;

            Assert.IsFalse(LevelPhasePlan.FromConfig(config).Has(LevelPhase.ContextChallenge));
        }

        [Test]
        public void Plan_RewardIdsAuthored_PlansMemoryRewardPhase()
        {
            LevelConfigSO config = CreateLegacyConfig();
            config.rewardIds.Add("reward.test");

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);
            Assert.IsTrue(plan.Has(LevelPhase.MemoryReward));
            Assert.IsTrue(plan.MemoryRewardContentMissing,
                "SALIN-223 requires BOTH keys: reward ids without a memory cutscene is a "
                + "half-authored level, and half-authored must not read as complete.");
        }

        // ---------------------------------------------------------------------
        // SALIN-223: both content phases are planned on every level, and missing
        // content is reported rather than silently skipped.
        // ---------------------------------------------------------------------

        [Test]
        public void Plan_MissingChallengeSequence_StillPlansContextChallenge()
        {
            LevelConfigSO config = CreateLegacyConfig();
            Assert.IsNull(config.challengeSequence, "Setup: the level must have no challenge authored.");

            Assert.IsTrue(LevelPhasePlan.FromConfig(config).Has(LevelPhase.ContextChallenge),
                "An unauthored challenge must still be planned, so the executor gets a "
                + "chance to refuse. Skipping the phase is how the level used to complete "
                + "on wave clear alone.");
        }

        [Test]
        public void Plan_EmptyRewardIds_StillPlansMemoryReward()
        {
            LevelConfigSO config = CreateLegacyConfig();
            Assert.IsEmpty(config.rewardIds, "Setup: the level must have no rewards authored.");

            Assert.IsTrue(LevelPhasePlan.FromConfig(config).Has(LevelPhase.MemoryReward));
        }

        [Test]
        public void Plan_NullConfig_StillPlansContextChallengeAndMemoryReward()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(null);

            Assert.IsTrue(plan.Has(LevelPhase.ContextChallenge));
            Assert.IsTrue(plan.Has(LevelPhase.MemoryReward));
            Assert.IsTrue(plan.ContextChallengeContentMissing);
            Assert.IsTrue(plan.MemoryRewardContentMissing);
        }

        [Test]
        public void Plan_MissingChallengeSequence_ReportsContextChallengeContentMissing()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateLegacyConfig());

            Assert.IsTrue(plan.ContextChallengeContentMissing);
        }

        [Test]
        public void Plan_EmptyRewardIds_ReportsMemoryRewardContentMissing()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateLegacyConfig());

            Assert.IsTrue(plan.MemoryRewardContentMissing);
        }

        [Test]
        public void Plan_NeverPlansLifecycleStates()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateFullConfig());

            Assert.IsFalse(plan.Has(LevelPhase.NotStarted));
            Assert.IsFalse(plan.Has(LevelPhase.Completed));
            Assert.IsFalse(plan.Has(LevelPhase.Defeated));
            Assert.IsFalse(plan.Has(LevelPhase.Exited));
        }

        // ---------------------------------------------------------------------
        // Begin
        // ---------------------------------------------------------------------

        [Test]
        public void Machine_StartsNotStartedAndNonTerminal()
        {
            LevelFlowMachine machine = CreateLegacyMachine();

            Assert.AreEqual(LevelPhase.NotStarted, machine.Phase);
            Assert.IsFalse(machine.IsTerminal);
            Assert.IsFalse(machine.IsPaused);
        }

        [Test]
        public void Begin_EntersTheFirstPlannedPhase()
        {
            LevelFlowMachine machine = CreateLegacyMachine();

            machine.Begin();

            Assert.AreEqual(LevelPhase.Story, machine.Phase);
        }

        [Test]
        public void Begin_Twice_SecondCallIsIgnored()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();
            machine.ReportPhaseComplete(LevelPhase.Story);

            machine.Begin();

            Assert.AreEqual(LevelPhase.Defense, machine.Phase,
                "A second Begin must not rewind the machine.");
        }

        // ---------------------------------------------------------------------
        // Phase completion and skipping
        // ---------------------------------------------------------------------

        [Test]
        public void ReportPhaseComplete_CurrentPhase_AdvancesSkippingUnplannedPhases()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();

            Assert.IsTrue(machine.ReportPhaseComplete(LevelPhase.Story));

            Assert.AreEqual(LevelPhase.Defense, machine.Phase,
                "A legacy plan must skip FocusWords, SymbolLearning, and RequiredPractice.");
        }

        [Test]
        public void FullPlan_TraversesAllNinePhasesInDeclarationOrder()
        {
            LevelFlowMachine machine = CreateFullMachine();
            machine.Begin();

            var visited = new List<LevelPhase> { machine.Phase };
            while (!machine.IsTerminal)
            {
                if (machine.Phase == LevelPhase.AtomicSave)
                    Assert.IsTrue(machine.ReportSaveResult(accepted: true));
                else
                    Assert.IsTrue(machine.ReportPhaseComplete(machine.Phase));

                visited.Add(machine.Phase);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    LevelPhase.Story,
                    LevelPhase.FocusWords,
                    LevelPhase.SymbolLearning,
                    LevelPhase.RequiredPractice,
                    LevelPhase.Defense,
                    LevelPhase.ContextChallenge,
                    LevelPhase.MemoryReward,
                    LevelPhase.AtomicSave,
                    LevelPhase.Results,
                    LevelPhase.Completed,
                },
                visited);
        }

        [Test]
        public void ReportPhaseComplete_WrongPhase_IsRejectedWithoutStateChange()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();

            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.Defense));
            Assert.AreEqual(LevelPhase.Story, machine.Phase);
        }

        [Test]
        public void ReportPhaseComplete_BeforeBegin_IsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();

            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.Story));
            Assert.AreEqual(LevelPhase.NotStarted, machine.Phase);
        }

        [Test]
        public void ReportPhaseComplete_DuplicateForAPastPhase_IsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();
            machine.ReportPhaseComplete(LevelPhase.Story);

            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.Story),
                "A duplicate completion event for an already-finished phase must be inert.");
            Assert.AreEqual(LevelPhase.Defense, machine.Phase);
        }

        // ---------------------------------------------------------------------
        // Defense completion
        // ---------------------------------------------------------------------

        [Test]
        public void ReportDefenseComplete_DuringDefense_Advances()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.Defense);

            Assert.IsTrue(machine.ReportDefenseComplete());
            // SALIN-223: ContextChallenge is planned on every level, so Defense no longer
            // hands straight to AtomicSave even on a config with no content authored.
            Assert.AreEqual(LevelPhase.ContextChallenge, machine.Phase);
        }

        [Test]
        public void ReportDefenseComplete_OutsideDefense_IsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();

            Assert.IsFalse(machine.ReportDefenseComplete());
            Assert.AreEqual(LevelPhase.Story, machine.Phase);
        }

        [Test]
        public void ReportDefenseComplete_Twice_SecondIsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.Defense);
            machine.ReportDefenseComplete();

            Assert.IsFalse(machine.ReportDefenseComplete(),
                "Defense systems report defense completion once; duplicates must be inert.");
            Assert.AreEqual(LevelPhase.ContextChallenge, machine.Phase);
        }

        // ---------------------------------------------------------------------
        // Atomic save gate
        // ---------------------------------------------------------------------

        [Test]
        public void ReportPhaseComplete_ForAtomicSave_IsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.AtomicSave);

            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.AtomicSave),
                "AtomicSave advances only through ReportSaveResult, never a bare completion.");
            Assert.AreEqual(LevelPhase.AtomicSave, machine.Phase);
        }

        [Test]
        public void ReportSaveResult_Accepted_AdvancesToResults()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.AtomicSave);

            Assert.IsTrue(machine.ReportSaveResult(accepted: true));
            Assert.AreEqual(LevelPhase.Results, machine.Phase);
        }

        [Test]
        public void ReportSaveResult_Rejected_StaysInAtomicSaveForRetry()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.AtomicSave);

            Assert.IsTrue(machine.ReportSaveResult(accepted: false),
                "A rejected save is a legal report; the machine holds for the retry loop.");
            Assert.AreEqual(LevelPhase.AtomicSave, machine.Phase);
            Assert.IsFalse(machine.IsTerminal);
        }

        [Test]
        public void ReportSaveResult_RejectedThenAccepted_ReachesResults()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.AtomicSave);

            machine.ReportSaveResult(accepted: false);
            machine.ReportSaveResult(accepted: true);

            Assert.AreEqual(LevelPhase.Results, machine.Phase);
        }

        [Test]
        public void ReportSaveResult_OutsideAtomicSave_IsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();

            Assert.IsFalse(machine.ReportSaveResult(accepted: true),
                "Results must be unreachable except through the AtomicSave phase.");
            Assert.AreEqual(LevelPhase.Story, machine.Phase);
        }

        // ---------------------------------------------------------------------
        // Completion terminal
        // ---------------------------------------------------------------------

        [Test]
        public void ReportPhaseComplete_Results_EntersCompletedTerminal()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.Results);

            Assert.IsTrue(machine.ReportPhaseComplete(LevelPhase.Results));
            Assert.AreEqual(LevelPhase.Completed, machine.Phase);
            Assert.IsTrue(machine.IsTerminal);
        }

        [Test]
        public void TerminalMachine_RejectsEveryFurtherReport()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            AdvanceTo(machine, LevelPhase.Results);
            machine.ReportPhaseComplete(LevelPhase.Results);

            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.Results));
            Assert.IsFalse(machine.ReportDefenseComplete());
            Assert.IsFalse(machine.ReportSaveResult(accepted: true));
            Assert.IsFalse(machine.ReportDefeat());
            Assert.IsFalse(machine.RequestExit());
            Assert.AreEqual(LevelPhase.Completed, machine.Phase);
        }

        // ---------------------------------------------------------------------
        // Defeat and exit
        // ---------------------------------------------------------------------

        [Test]
        public void ReportDefeat_FromEveryPlayablePhase_EntersDefeated()
        {
            foreach (LevelPhase target in PlayablePhases)
            {
                LevelFlowMachine machine = CreateFullMachine();
                AdvanceTo(machine, target);

                Assert.IsTrue(machine.ReportDefeat(), $"Defeat must be legal during {target}.");
                Assert.AreEqual(LevelPhase.Defeated, machine.Phase, $"Defeat during {target}.");
                Assert.IsTrue(machine.IsTerminal);
            }
        }

        [Test]
        public void ReportDefeat_BeforeBegin_EntersDefeated()
        {
            LevelFlowMachine machine = CreateLegacyMachine();

            Assert.IsTrue(machine.ReportDefeat(),
                "A defeat raised before the flow starts must still terminate the machine.");
            Assert.AreEqual(LevelPhase.Defeated, machine.Phase);
        }

        [Test]
        public void ReportDefeat_Twice_SecondIsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();
            machine.ReportDefeat();

            Assert.IsFalse(machine.ReportDefeat());
            Assert.AreEqual(LevelPhase.Defeated, machine.Phase);
        }

        [Test]
        public void RequestExit_FromEveryPlayablePhase_EntersExited()
        {
            foreach (LevelPhase target in PlayablePhases)
            {
                LevelFlowMachine machine = CreateFullMachine();
                AdvanceTo(machine, target);

                Assert.IsTrue(machine.RequestExit(), $"Exit must be legal during {target}.");
                Assert.AreEqual(LevelPhase.Exited, machine.Phase, $"Exit during {target}.");
                Assert.IsTrue(machine.IsTerminal);
            }
        }

        [Test]
        public void RequestExit_AfterDefeat_IsRejected()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();
            machine.ReportDefeat();

            Assert.IsFalse(machine.RequestExit());
            Assert.AreEqual(LevelPhase.Defeated, machine.Phase);
        }

        // ---------------------------------------------------------------------
        // Pause
        // ---------------------------------------------------------------------

        [Test]
        public void NotifyPaused_TogglesIsPausedAndResumeClearsIt()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();

            machine.NotifyPaused();
            Assert.IsTrue(machine.IsPaused);

            machine.NotifyResumed();
            Assert.IsFalse(machine.IsPaused);
        }

        [Test]
        public void NotifyPaused_OnTerminalMachine_IsIgnored()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();
            machine.ReportDefeat();

            machine.NotifyPaused();

            Assert.IsFalse(machine.IsPaused, "A finished level cannot be paused.");
        }

        [Test]
        public void ReportDefeat_WhilePaused_StillEntersDefeated()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();
            machine.NotifyPaused();

            Assert.IsTrue(machine.ReportDefeat());
            Assert.AreEqual(LevelPhase.Defeated, machine.Phase);
            Assert.IsFalse(machine.IsPaused, "Terminal states clear the pause flag.");
        }

        // ---------------------------------------------------------------------
        // Change notification
        // ---------------------------------------------------------------------

        [Test]
        public void PhaseChanged_ReportsEveryTransitionWithPreviousAndNext()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            var transitions = new List<(LevelPhase From, LevelPhase To)>();
            machine.PhaseChanged += (from, to) => transitions.Add((from, to));

            machine.Begin();
            machine.ReportPhaseComplete(LevelPhase.Story);
            machine.ReportDefenseComplete();
            // SALIN-223: the two content phases sit between Defense and AtomicSave on
            // every plan now, including this content-less one.
            machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            machine.ReportPhaseComplete(LevelPhase.MemoryReward);
            machine.ReportSaveResult(accepted: true);
            machine.ReportPhaseComplete(LevelPhase.Results);

            CollectionAssert.AreEqual(
                new[]
                {
                    (LevelPhase.NotStarted, LevelPhase.Story),
                    (LevelPhase.Story, LevelPhase.Defense),
                    (LevelPhase.Defense, LevelPhase.ContextChallenge),
                    (LevelPhase.ContextChallenge, LevelPhase.MemoryReward),
                    (LevelPhase.MemoryReward, LevelPhase.AtomicSave),
                    (LevelPhase.AtomicSave, LevelPhase.Results),
                    (LevelPhase.Results, LevelPhase.Completed),
                },
                transitions);
        }

        [Test]
        public void PhaseChanged_DoesNotFireForRejectedReports()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();

            int changes = 0;
            machine.PhaseChanged += (_, _) => changes++;

            machine.ReportPhaseComplete(LevelPhase.Defense);
            machine.ReportSaveResult(accepted: true);
            machine.ReportDefenseComplete();

            Assert.AreEqual(0, changes);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static void AssertPlanned(LevelPhasePlan plan, params LevelPhase[] expected)
        {
            var expectedSet = new HashSet<LevelPhase>(expected);
            foreach (LevelPhase phase in PlayablePhases)
            {
                Assert.AreEqual(expectedSet.Contains(phase), plan.Has(phase),
                    $"Unexpected plan membership for {phase}.");
            }
        }

        // ---------------------------------------------------------------------
        // SALIN-220 — the completed-phase record
        // ---------------------------------------------------------------------

        [Test]
        public void CompletedPhases_OnAFreshMachine_IsEmpty_SALIN220()
        {
            LevelFlowMachine machine = CreateFullMachine();

            Assert.IsNotNull(machine.CompletedPhases);
            Assert.AreEqual(0, machine.CompletedPhases.Count);
            Assert.IsNotNull(machine.Plan);
        }

        [Test]
        public void CompletedPhases_RecordEveryAcceptedReportOnAFullRun_SALIN220()
        {
            LevelFlowMachine machine = CreateFullMachine();
            machine.Begin();

            int guard = 0;
            while (!machine.IsTerminal && guard++ < 16)
            {
                if (machine.Phase == LevelPhase.Defense)
                    machine.ReportDefenseComplete();
                else if (machine.Phase == LevelPhase.AtomicSave)
                    machine.ReportSaveResult(accepted: true);
                else
                    machine.ReportPhaseComplete(machine.Phase);
            }

            Assert.AreEqual(LevelPhase.Completed, machine.Phase);
            foreach (LevelPhase phase in PlayablePhases)
                Assert.IsTrue(machine.HasCompleted(phase),
                    $"A fully authored run must record {phase} as completed.");
        }

        /// <summary>
        /// SALIN-220. Skipped phases must be absent, not recorded as complete. The distinction is
        /// what lets LevelObjectiveFlagResolver treat "the level never authored this" separately
        /// from "the player did this", instead of guessing from the machine alone.
        /// </summary>
        [Test]
        public void CompletedPhases_NeverRecordAPhaseThePlanSkipped_SALIN220()
        {
            LevelFlowMachine machine = CreateLegacyMachine();
            machine.Begin();

            int guard = 0;
            while (!machine.IsTerminal && guard++ < 16)
            {
                if (machine.Phase == LevelPhase.Defense)
                    machine.ReportDefenseComplete();
                else if (machine.Phase == LevelPhase.AtomicSave)
                    machine.ReportSaveResult(accepted: true);
                else
                    machine.ReportPhaseComplete(machine.Phase);
            }

            Assert.IsTrue(machine.HasCompleted(LevelPhase.Story));
            Assert.IsTrue(machine.HasCompleted(LevelPhase.Defense));
            // SALIN-223: a legacy config DOES plan ContextChallenge, so this loop completes it.
            // The test's subject is unchanged — a phase the plan skipped is never recorded — and
            // FocusWords, SymbolLearning and RequiredPractice are still the skipped ones.
            Assert.IsTrue(machine.HasCompleted(LevelPhase.ContextChallenge));
            Assert.IsFalse(machine.HasCompleted(LevelPhase.RequiredPractice),
                "A legacy config plans no RequiredPractice, so it can never be recorded complete.");
            Assert.IsFalse(machine.HasCompleted(LevelPhase.FocusWords));
            Assert.IsFalse(machine.HasCompleted(LevelPhase.SymbolLearning));
        }

        [Test]
        public void CompletedPhases_IgnoreRejectedReportsAndARejectedSave_SALIN220()
        {
            LevelFlowMachine machine = CreateFullMachine();
            machine.Begin();

            // Wrong phase, and a defense report from outside Defense: both rejected.
            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.MemoryReward));
            Assert.IsFalse(machine.ReportDefenseComplete());
            Assert.AreEqual(0, machine.CompletedPhases.Count);

            AdvanceTo(machine, LevelPhase.AtomicSave);
            Assert.IsFalse(machine.HasCompleted(LevelPhase.AtomicSave));
            machine.ReportSaveResult(accepted: false);
            Assert.IsFalse(machine.HasCompleted(LevelPhase.AtomicSave),
                "A rejected save holds the machine in AtomicSave; nothing completed.");

            machine.ReportSaveResult(accepted: true);
            Assert.IsTrue(machine.HasCompleted(LevelPhase.AtomicSave));
        }

        [Test]
        public void CompletedPhases_AfterADefeat_HoldOnlyWhatWasFinishedBeforeIt_SALIN220()
        {
            LevelFlowMachine machine = CreateFullMachine();
            AdvanceTo(machine, LevelPhase.Defense);

            Assert.IsTrue(machine.ReportDefeat());

            Assert.IsTrue(machine.HasCompleted(LevelPhase.Story));
            Assert.IsFalse(machine.HasCompleted(LevelPhase.Defense),
                "Defeat is not a completion of the phase it happened in.");
        }

        // ---------------------------------------------------------------------
        // SALIN-226 — alternating defense/restoration segments
        // ---------------------------------------------------------------------

        [Test]
        public void Segments_UnsegmentedLevel_RunsOneSegmentAndTheNinePhaseTraversalIsUnchanged_SALIN226()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateFullConfig());
            Assert.AreEqual(1, plan.SegmentCount);
            Assert.IsFalse(plan.SegmentPlanInvalid);

            LevelFlowMachine machine = new LevelFlowMachine(plan);
            var visited = new List<LevelPhase>();
            machine.PhaseChanged += (_, next) => visited.Add(next);

            machine.Begin();
            int guard = 0;
            while (!machine.IsTerminal && guard++ < 32)
            {
                if (machine.Phase == LevelPhase.AtomicSave)
                    machine.ReportSaveResult(accepted: true);
                else
                    machine.ReportPhaseComplete(machine.Phase);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    LevelPhase.Story,
                    LevelPhase.FocusWords,
                    LevelPhase.SymbolLearning,
                    LevelPhase.RequiredPractice,
                    LevelPhase.Defense,
                    LevelPhase.ContextChallenge,
                    LevelPhase.MemoryReward,
                    LevelPhase.AtomicSave,
                    LevelPhase.Results,
                    LevelPhase.Completed,
                },
                visited,
                "With one segment the machine must be indistinguishable from the "
                + "pre-SALIN-226 machine.");
            Assert.AreEqual(0, machine.CurrentSegmentIndex);
        }

        [Test]
        public void Segments_ContextChallengeCompleteWithSegmentsRemaining_ReturnsToDefense_SALIN226()
        {
            LevelFlowMachine machine = CreateSegmentedMachine(2);
            AdvanceTo(machine, LevelPhase.ContextChallenge);
            Assert.AreEqual(0, machine.CurrentSegmentIndex);

            Assert.IsTrue(machine.ReportPhaseComplete(LevelPhase.ContextChallenge));

            Assert.AreEqual(LevelPhase.Defense, machine.Phase,
                "Segment 1 of 2 must loop back to Defense, not advance to MemoryReward.");
            Assert.AreEqual(1, machine.CurrentSegmentIndex);
        }

        [Test]
        public void Segments_LastContextChallengeComplete_AdvancesToMemoryReward_SALIN226()
        {
            LevelFlowMachine machine = CreateSegmentedMachine(2);
            AdvanceTo(machine, LevelPhase.ContextChallenge);

            machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            Assert.AreEqual(LevelPhase.Defense, machine.Phase);
            machine.ReportDefenseComplete();
            Assert.AreEqual(LevelPhase.ContextChallenge, machine.Phase);

            Assert.IsTrue(machine.ReportPhaseComplete(LevelPhase.ContextChallenge));

            Assert.AreEqual(LevelPhase.MemoryReward, machine.Phase,
                "The final segment must leave the loop through the ordinary forward rule.");
            Assert.AreEqual(1, machine.CurrentSegmentIndex,
                "The cursor must not run past the last segment.");
        }

        [Test]
        public void Segments_ThreeSegments_AlternateExactlyThreeTimesThenAdvance_SALIN226()
        {
            LevelFlowMachine machine = CreateSegmentedMachine(3);
            var visited = new List<LevelPhase>();
            AdvanceTo(machine, LevelPhase.Defense);
            machine.PhaseChanged += (_, next) => visited.Add(next);

            for (int segment = 0; segment < 3; segment++)
            {
                Assert.AreEqual(LevelPhase.Defense, machine.Phase,
                    $"Segment {segment} must open in Defense.");
                machine.ReportDefenseComplete();
                Assert.AreEqual(LevelPhase.ContextChallenge, machine.Phase);
                machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    LevelPhase.ContextChallenge,
                    LevelPhase.Defense,
                    LevelPhase.ContextChallenge,
                    LevelPhase.Defense,
                    LevelPhase.ContextChallenge,
                    LevelPhase.MemoryReward,
                },
                visited,
                "Three segments alternate Defense/ContextChallenge three times, then leave.");
            Assert.AreEqual(2, machine.CurrentSegmentIndex);
        }

        [Test]
        public void Segments_RejectedContextChallengeReport_DoesNotAdvanceTheSegmentCursor_SALIN226()
        {
            LevelFlowMachine machine = CreateSegmentedMachine(3);
            AdvanceTo(machine, LevelPhase.Defense);

            // Wrong phase for the current state: rejected, and rejected reports change nothing.
            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.ContextChallenge));
            Assert.AreEqual(0, machine.CurrentSegmentIndex,
                "A rejected report must not move the segment cursor.");
            Assert.AreEqual(LevelPhase.Defense, machine.Phase);

            machine.ReportDefenseComplete();
            Assert.IsTrue(machine.ReportPhaseComplete(LevelPhase.ContextChallenge));
            Assert.AreEqual(1, machine.CurrentSegmentIndex);

            // A duplicate report for a phase already left is rejected too.
            Assert.IsFalse(machine.ReportPhaseComplete(LevelPhase.ContextChallenge));
            Assert.AreEqual(1, machine.CurrentSegmentIndex);
        }

        [Test]
        public void Segments_AtomicSaveIsUnreachableUntilTheLastSegmentCompletes_SALIN226()
        {
            LevelFlowMachine machine = CreateSegmentedMachine(3);
            AdvanceTo(machine, LevelPhase.Defense);

            for (int segment = 0; segment < 2; segment++)
            {
                machine.ReportDefenseComplete();
                machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
                Assert.AreNotEqual(LevelPhase.AtomicSave, machine.Phase,
                    "AC-3: the terminal phases are not part of the loop and must stay "
                    + "unreachable while segments remain.");
                Assert.IsFalse(machine.HasCompleted(LevelPhase.AtomicSave));
                Assert.IsFalse(machine.ReportSaveResult(accepted: true),
                    "A save cannot be reported from inside the segment loop.");
            }

            machine.ReportDefenseComplete();
            machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            Assert.AreEqual(LevelPhase.MemoryReward, machine.Phase);
            machine.ReportPhaseComplete(LevelPhase.MemoryReward);
            Assert.AreEqual(LevelPhase.AtomicSave, machine.Phase,
                "AtomicSave opens only after the last segment.");
        }

        [Test]
        public void Segments_DefeatDuringASecondSegment_IsTerminalAndRecordsOnlyWhatFinished_SALIN226()
        {
            LevelFlowMachine machine = CreateSegmentedMachine(3);
            AdvanceTo(machine, LevelPhase.Defense);
            machine.ReportDefenseComplete();
            machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            Assert.AreEqual(1, machine.CurrentSegmentIndex);
            Assert.AreEqual(LevelPhase.Defense, machine.Phase);

            Assert.IsTrue(machine.ReportDefeat());

            Assert.AreEqual(LevelPhase.Defeated, machine.Phase);
            Assert.IsTrue(machine.IsTerminal);
            Assert.IsTrue(machine.HasCompleted(LevelPhase.Story));
            Assert.IsFalse(machine.HasCompleted(LevelPhase.MemoryReward));
            Assert.IsFalse(machine.HasCompleted(LevelPhase.AtomicSave));
            Assert.IsFalse(machine.ReportDefenseComplete(),
                "Nothing is reportable after a defeat, segments or not.");
        }

        [Test]
        public void Segments_WithChallengePrototypeEnabled_AreRejectedAndNeverLoop_SALIN226()
        {
            LevelConfigSO config = CreateSegmentedConfig(2);
            config.challengePrototypeEnabled = true;

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);
            Assert.AreEqual(1, plan.SegmentCount);
            Assert.IsTrue(plan.SegmentPlanInvalid,
                "The prototype carve-out leaves ContextChallenge unplanned, so segments "
                + "must be reported invalid rather than silently collapsing.");
            Assert.IsFalse(plan.Has(LevelPhase.ContextChallenge));

            LevelFlowMachine machine = new LevelFlowMachine(plan);
            AdvanceTo(machine, LevelPhase.Defense);
            machine.ReportDefenseComplete();

            Assert.AreEqual(LevelPhase.MemoryReward, machine.Phase,
                "With ContextChallenge unplanned there is no restoration leg to loop through.");
            Assert.AreEqual(0, machine.CurrentSegmentIndex);
        }

        [Test]
        public void Segments_ConsumingMoreWavesThanTheLevelAuthors_AreRejected_SALIN226()
        {
            LevelConfigSO config = CreateSegmentedConfig(2);
            config.waves.Clear();
            config.waves.Add(new WaveDefinition());

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);

            Assert.IsTrue(plan.SegmentPlanInvalid,
                "Segments partition the wave list; overrunning it would silently drop "
                + "authored waves.");
            Assert.AreEqual(1, plan.SegmentCount);
        }

        private LevelFlowMachine CreateSegmentedMachine(int segmentCount)
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateSegmentedConfig(segmentCount));
            Assert.AreEqual(segmentCount, plan.SegmentCount, "Fixture: segments must be accepted.");
            Assert.IsFalse(plan.SegmentPlanInvalid, "Fixture: the segment plan must be valid.");
            return new LevelFlowMachine(plan);
        }

        /// <summary>
        /// A level with <paramref name="segmentCount"/> segments of one wave each, every
        /// segment playing one challenge unit. Synthetic on purpose: no shipped level config
        /// is edited by SALIN-226, and Level 5 has no waves to group (SALIN-247 authors them).
        /// </summary>
        private LevelConfigSO CreateSegmentedConfig(int segmentCount)
        {
            LevelConfigSO config = CreateFullConfig();
            ChallengeSequenceSO sequence = config.challengeSequence;

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

            sequence.units = units.ToArray();
            return config;
        }

        private LevelFlowMachine CreateLegacyMachine()
        {
            return new LevelFlowMachine(LevelPhasePlan.FromConfig(CreateLegacyConfig()));
        }

        private LevelFlowMachine CreateFullMachine()
        {
            return new LevelFlowMachine(LevelPhasePlan.FromConfig(CreateFullConfig()));
        }

        private static void AdvanceTo(LevelFlowMachine machine, LevelPhase target)
        {
            machine.Begin();
            int guard = 0;
            while (machine.Phase != target && !machine.IsTerminal && guard++ < 16)
            {
                if (machine.Phase == LevelPhase.AtomicSave)
                    machine.ReportSaveResult(accepted: true);
                else
                    machine.ReportPhaseComplete(machine.Phase);
            }

            Assert.AreEqual(target, machine.Phase, "Test setup failed to reach the target phase.");
        }

        private LevelConfigSO CreateLegacyConfig()
        {
            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(config);
            return config;
        }

        private LevelConfigSO CreateFullConfig()
        {
            LevelConfigSO config = CreateLegacyConfig();
            config.focusWords.Add(new FocusWordDefinition());
            config.learningRequirements.Add(new ContentRequirement());
            config.practiceRequirements.Add(new ContentRequirement());
            config.challengeSequence = CreateChallengeSequence();
            config.rewardIds.Add("reward.test");
            return config;
        }

        private ChallengeSequenceSO CreateChallengeSequence()
        {
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _objectsToDestroy.Add(sequence);
            return sequence;
        }
    }
}
