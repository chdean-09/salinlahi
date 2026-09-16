using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Closes the last unverified gate of the gated-finale feature.
    ///
    /// <see cref="GatedFinaleSlotTests"/> proves the coordinator derives the gate onto the last
    /// slot. <see cref="FinalWaveIndexTests"/> proves <c>WaveManager.IsFinalWaveIndex</c> picks the
    /// right wave. <see cref="GatedFinaleCampaignOptInTests"/> proves the shipped Levels 2-4 opt
    /// in. None of those prove the gate actually withholds anything: they all show the gate is
    /// attached and that it opens, but not that <see cref="SpawnAssignmentDirector"/> refuses to
    /// assign the gated symbol - needed or filler - while the token is closed. If the director
    /// happily assigned it anyway, a level could still finish before its final wave and every one
    /// of those tests would still pass.
    ///
    /// This class is the missing property: while the gated slot's token is closed, the gated
    /// symbol must never be assigned, in any role; once the token opens, it must become assignable;
    /// and only the gated slot is affected, not the whole director.
    ///
    /// Pure EditMode, no MonoBehaviour and no scene, matching the idiom in
    /// <see cref="SpawnAssignmentDirectorTests"/>: private static factories build the slots and
    /// policy, a Request helper takes openGates as its lever, and a nested fake ISpawnRandom always
    /// favours the needed slot so a bug that let the gate through would fail loudly rather than by
    /// chance.
    /// </summary>
    public sealed class GatedFinaleWithholdingTests
    {
        private const string Ei = "symbol.ei";
        private const string Na = "symbol.na";
        private const string A = "symbol.a";
        private const string Ma = "symbol.ma";

        private const string FinalWaveGate = SpawnGateRegistry.FinalWaveReached;

        /// <summary>
        /// Deterministic draw source that always takes the needed branch and always picks index 0.
        /// Chosen deliberately over a random/neutral fake: if the gate were ever ignored, this is
        /// the random that would hand the gated symbol out fastest, so the test fails loudly
        /// instead of passing by luck.
        /// </summary>
        private sealed class AlwaysFavoursNeededRandom : ISpawnRandom
        {
            public double NextDouble() => 0d;
            public int NextInt(int exclusiveMax) => 0;
        }

        /// <summary>Four flattened slots with the last gated on the finale token, none restored.</summary>
        private static List<SpawnSlot> GatedFinaleSlots()
        {
            return new List<SpawnSlot>
            {
                new SpawnSlot(Ei, "word.ina", 0),
                new SpawnSlot(Na, "word.ina", 1),
                new SpawnSlot(A, "word.ama", 0),
                new SpawnSlot(Ma, "word.ama", 1, FinalWaveGate),
            };
        }

        private static SpawnAssignmentPolicy Policy()
        {
            return new SpawnAssignmentPolicy
            {
                minSpawnsBeforeNeeded = 2,
                neededWeight = 0.5f,
                starvationTimeout = 30f,
                hardStarvationTimeout = 50f,
                minFillerVariety = 2,
                offTargetFillerWeight = 0f,
                activeSlotWindow = 1,
                choiceMomentSlotIndex = -1,
                choicePairWindow = 1.5f,
                maxConcurrentEnemies = 8,
                assignmentSeed = 1234,
            };
        }

        /// <summary>
        /// The whitelist includes MA alongside every other target symbol. That is load-bearing: if
        /// MA were left out of the whitelist, a test that never saw it assigned would prove nothing
        /// about the gate - it would just be proving the whitelist excludes it. Including it here
        /// means a failure to see it assigned can only be the gate's doing.
        /// </summary>
        private static SpawnAssignmentRequest Request(
            bool[] restored,
            float now,
            int activeEnemies,
            params string[] openGates)
        {
            return new SpawnAssignmentRequest
            {
                RestoredSlots = restored,
                OpenGateTokens = new List<string>(openGates),
                Now = now,
                ActiveEnemyCount = activeEnemies,
                WaveSymbolWhitelist = new List<string> { Ei, Na, A, Ma },
            };
        }

        private static bool[] NoneRestored() => new[] { false, false, false, false };

        [Test]
        public void ClosedGate_NeverAssignsTheGatedSymbolInAnyRole()
        {
            var director = new SpawnAssignmentDirector(
                GatedFinaleSlots(), Policy(), new AlwaysFavoursNeededRandom());

            var restored = NoneRestored();

            for (int spawn = 0; spawn < 60; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(
                    Request(restored, now: spawn * 5f, activeEnemies: 0));

                Assert.That(assignment.SymbolStableId, Is.Not.EqualTo(Ma),
                    "MA was assigned on spawn " + spawn + " (role " + assignment.Role + ") while "
                    + "the finale gate is closed. If this can happen, a level could complete its "
                    + "target text before its final wave ever begins, defeating the entire point "
                    + "of the gated finale.");

                if (assignment.Role == SpawnAssignmentRole.Needed)
                    restored[assignment.SlotIndex] = true;
            }
        }

        [Test]
        public void OpenGate_MakesTheGatedSymbolAssignable()
        {
            var director = new SpawnAssignmentDirector(
                GatedFinaleSlots(), Policy(), new AlwaysFavoursNeededRandom());

            // Slots 0-2 restored, so the cursor sits on slot 3 (MA) as soon as the window advances.
            var restored = new[] { true, true, true, false };
            bool sawMaAssigned = false;

            for (int spawn = 0; spawn < 20 && !sawMaAssigned; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(
                    Request(restored, now: spawn * 5f, activeEnemies: 0, openGates: FinalWaveGate));

                if (assignment.SymbolStableId == Ma && assignment.Role == SpawnAssignmentRole.Needed)
                    sawMaAssigned = true;
            }

            Assert.That(sawMaAssigned, Is.True,
                "MA was never assigned as Needed even with " + FinalWaveGate + " open. If the gate "
                + "cannot release once opened, the finale is permanently unreachable and the level "
                + "is unwinnable.");
        }

        [Test]
        public void ClosedGate_StillAssignsEveryOtherSlotsSymbol()
        {
            var director = new SpawnAssignmentDirector(
                GatedFinaleSlots(), Policy(), new AlwaysFavoursNeededRandom());

            var restored = NoneRestored();
            var neededSymbolsSeen = new HashSet<string>();

            for (int spawn = 0; spawn < 60; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(
                    Request(restored, now: spawn * 5f, activeEnemies: 0));

                if (assignment.Role == SpawnAssignmentRole.Needed)
                {
                    neededSymbolsSeen.Add(assignment.SymbolStableId);
                    restored[assignment.SlotIndex] = true;
                }
            }

            Assert.That(neededSymbolsSeen.Contains(Ei), Is.True,
                "EI was never assigned as Needed. A bug that froze the whole director (rather than "
                + "just withholding the gated slot) would also make test 1 pass for the wrong "
                + "reason, so the other three slots must keep filling normally.");
            Assert.That(neededSymbolsSeen.Contains(Na), Is.True,
                "NA was never assigned as Needed while only the finale slot should be withheld.");
            Assert.That(neededSymbolsSeen.Contains(A), Is.True,
                "A was never assigned as Needed while only the finale slot should be withheld.");
        }
    }
}
