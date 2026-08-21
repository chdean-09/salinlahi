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

        [Test]
        public void Plan_NullConfig_PlansOnlyTheLegacyPhases()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(null);

            AssertPlanned(plan, LevelPhase.Story, LevelPhase.Defense, LevelPhase.AtomicSave, LevelPhase.Results);
        }

        [Test]
        public void Plan_LegacyConfigWithoutRevisedContent_PlansOnlyTheLegacyPhases()
        {
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(CreateLegacyConfig());

            AssertPlanned(plan, LevelPhase.Story, LevelPhase.Defense, LevelPhase.AtomicSave, LevelPhase.Results);
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

            Assert.IsTrue(LevelPhasePlan.FromConfig(config).Has(LevelPhase.ContextChallenge));
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

            Assert.IsTrue(LevelPhasePlan.FromConfig(config).Has(LevelPhase.MemoryReward));
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
            Assert.AreEqual(LevelPhase.AtomicSave, machine.Phase);
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
            Assert.AreEqual(LevelPhase.AtomicSave, machine.Phase);
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
            machine.ReportSaveResult(accepted: true);
            machine.ReportPhaseComplete(LevelPhase.Results);

            CollectionAssert.AreEqual(
                new[]
                {
                    (LevelPhase.NotStarted, LevelPhase.Story),
                    (LevelPhase.Story, LevelPhase.Defense),
                    (LevelPhase.Defense, LevelPhase.AtomicSave),
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
