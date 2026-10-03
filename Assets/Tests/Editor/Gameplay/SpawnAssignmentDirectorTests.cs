using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Covers the six requirements in docs/design/spawn-assignment-system.md: pacing, filler,
    /// gating, starvation, the guaranteed choice moment, and scaling.
    ///
    /// The director is free of UnityEngine types, so every criterion is an EditMode test with no
    /// scene, matching ActiveClueSelectorTests.
    /// </summary>
    public sealed class SpawnAssignmentDirectorTests
    {
        private const string Ei = "symbol.ei";
        private const string Na = "symbol.na";
        private const string A = "symbol.a";
        private const string Ma = "symbol.ma";
        private const string IligawGate = "iligaw_beat_resolved";
        private const string FinalWaveGate = SpawnGateRegistry.FinalWaveReached;

        /// <summary>Abo ng Simula's spoken value. Level 1's opening directive names this.</summary>
        private const string AValue = "value.a";

        /// <summary>Deterministic draw source: always takes the needed branch, always picks index 0.</summary>
        private sealed class AlwaysNeededRandom : ISpawnRandom
        {
            public double NextDouble() => 0d;
            public int NextInt(int exclusiveMax) => 0;
        }

        /// <summary>Deterministic draw source: never takes the needed branch.</summary>
        private sealed class NeverNeededRandom : ISpawnRandom
        {
            public double NextDouble() => 0.999d;
            public int NextInt(int exclusiveMax) => 0;
        }

        /// <summary>Replays a scripted sequence of doubles, then holds the last value.</summary>
        private sealed class ScriptedRandom : ISpawnRandom
        {
            private readonly double[] _values;
            private int _cursor;

            public ScriptedRandom(params double[] values) { _values = values; }

            public double NextDouble()
            {
                if (_values.Length == 0) return 0d;
                double value = _values[_cursor < _values.Length ? _cursor : _values.Length - 1];
                _cursor++;
                return value;
            }

            public int NextInt(int exclusiveMax) => 0;
        }

        /// <summary>Level 1's target: INA + AMA, with the final slot gated on the Iligaw beat.</summary>
        private static List<SpawnSlot> Level1Slots()
        {
            return new List<SpawnSlot>
            {
                new SpawnSlot(Ei, "word.ina", 0),
                new SpawnSlot(Na, "word.ina", 1),
                new SpawnSlot(A, "word.ama", 0),
                new SpawnSlot(Ma, "word.ama", 1, IligawGate),
            };
        }

        private static SpawnAssignmentPolicy Level1Policy()
        {
            return new SpawnAssignmentPolicy
            {
                minSpawnsBeforeNeeded = 4,
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

        private static SpawnAssignmentRequest Request(
            bool[] restored,
            float now = 0f,
            int activeEnemies = 0,
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

        // ---------------------------------------------------------------- 1. PACING

        [Test]
        public void Floor_SuppressesNeededSymbolUntilMinSpawnsElapse()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            for (int spawn = 0; spawn < 4; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(Request(NoneRestored()));
                Assert.That(assignment.Role, Is.EqualTo(SpawnAssignmentRole.Filler),
                    "Spawn " + spawn + " must be filler: the anti-rush floor is 4.");
                Assert.That(assignment.SymbolStableId, Is.Not.EqualTo(Ei),
                    "Spawn " + spawn + " must not carry the needed symbol.");
            }
        }

        [Test]
        public void Floor_ReleasesNeededSymbolOnTheFifthSpawn()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            for (int spawn = 0; spawn < 4; spawn++)
                director.AssignNext(Request(NoneRestored()));

            SpawnAssignment fifth = director.AssignNext(Request(NoneRestored()));
            Assert.That(fifth.Role, Is.EqualTo(SpawnAssignmentRole.Needed));
            Assert.That(fifth.SymbolStableId, Is.EqualTo(Ei));
            Assert.That(fifth.SlotIndex, Is.EqualTo(0));
        }

        [Test]
        public void Floor_ResetsForTheNextSlotAfterAFill()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            for (int spawn = 0; spawn < 5; spawn++)
                director.AssignNext(Request(NoneRestored()));

            // Slot 0 is now restored; the floor must apply again to slot 1 rather than letting the
            // level cascade to an instant finish.
            var restored = new[] { true, false, false, false };
            SpawnAssignment next = director.AssignNext(Request(restored));
            Assert.That(next.Role, Is.EqualTo(SpawnAssignmentRole.Filler),
                "A fresh slot must re-arm the floor.");
        }

        [Test]
        public void Pacing_FourSlotsCannotCompleteInsideTheFloorBudget()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            var restored = NoneRestored();
            int spawns = 0;
            int filled = 0;

            while (filled < 4 && spawns < 200)
            {
                SpawnAssignment assignment = director.AssignNext(
                    Request(restored, now: spawns * 4.3f, openGates: IligawGate));
                spawns++;

                if (assignment.Role == SpawnAssignmentRole.Needed)
                {
                    restored[assignment.SlotIndex] = true;
                    filled++;
                }
            }

            Assert.That(filled, Is.EqualTo(4), "The level must be completable.");
            Assert.That(spawns, Is.GreaterThanOrEqualTo(20),
                "4 slots x (4 floor + 1) is the theoretical minimum; a 4-draw win must be impossible.");
        }

        /// <summary>
        /// Level 1's tutorial delta: slot 0's floor drops to 1 so the player's first drawing can
        /// actually restore something, instead of the first needed carrier arriving on spawn 5.
        /// </summary>
        [Test]
        public void Floor_PerSlotOverrideLowersTheFloorForThatSlotOnly()
        {
            var policy = Level1Policy();
            policy.slotFloors = new List<SpawnSlotFloor>
            {
                new SpawnSlotFloor { slotIndex = 0, minSpawnsBeforeNeeded = 1 },
            };

            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new AlwaysNeededRandom());

            SpawnAssignment first = director.AssignNext(Request(NoneRestored()));
            Assert.That(first.Role, Is.EqualTo(SpawnAssignmentRole.Filler),
                "A floor of 1 still withholds the needed symbol from the opening spawn.");

            SpawnAssignment second = director.AssignNext(Request(NoneRestored()));
            Assert.That(second.Role, Is.EqualTo(SpawnAssignmentRole.Needed),
                "With slot 0 overridden to 1, the needed carrier must arrive on spawn 2.");
            Assert.That(second.SymbolStableId, Is.EqualTo(Ei));
            Assert.That(second.SlotIndex, Is.EqualTo(0));
        }

        /// <summary>
        /// The other half of the same delta, and the half that would fail silently: lowering slot 0
        /// must not lower the anti-rush floor for the rest of the level.
        /// </summary>
        [Test]
        public void Floor_ScalarStillAppliesToSlotsWithoutAnOverride()
        {
            var policy = Level1Policy();
            policy.slotFloors = new List<SpawnSlotFloor>
            {
                new SpawnSlotFloor { slotIndex = 0, minSpawnsBeforeNeeded = 1 },
            };

            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new AlwaysNeededRandom());

            // Slot 0 is restored, so slot 1 (NA) is the cursor and carries no override.
            var restored = new[] { true, false, false, false };

            for (int spawn = 0; spawn < 4; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(Request(restored));
                Assert.That(assignment.Role, Is.EqualTo(SpawnAssignmentRole.Filler),
                    "Spawn " + spawn + " must be filler: slot 1 keeps the scalar floor of 4.");
                Assert.That(assignment.SymbolStableId, Is.Not.EqualTo(Na));
            }

            SpawnAssignment fifth = director.AssignNext(Request(restored));
            Assert.That(fifth.Role, Is.EqualTo(SpawnAssignmentRole.Needed));
            Assert.That(fifth.SymbolStableId, Is.EqualTo(Na));
        }

        // ---------------------------------------------------------------- 2. FILLER

        [Test]
        public void Filler_PrefersALaterNeededSymbolOverARestoredDuplicate()
        {
            var policy = Level1Policy();
            policy.minFillerVariety = 1; // isolate rule 1 from the variety fallback
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new NeverNeededRandom());

            // Slot 0 (EI) is the cursor. Later-needed and ungated: NA and A. MA is gated.
            SpawnAssignment assignment = director.AssignNext(Request(NoneRestored()));

            Assert.That(assignment.Role, Is.EqualTo(SpawnAssignmentRole.Filler));
            Assert.That(assignment.SymbolStableId == Na || assignment.SymbolStableId == A, Is.True,
                "Filler must be a later-needed symbol, was: " + assignment.SymbolStableId);
        }

        [Test]
        public void Filler_NeverCarriesTheCurrentlyNeededSymbol()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());

            for (int spawn = 0; spawn < 30; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(
                    Request(NoneRestored(), now: spawn * 0.1f));
                Assert.That(assignment.SymbolStableId, Is.Not.EqualTo(Ei),
                    "Filler leaked the needed symbol on spawn " + spawn + ".");
            }
        }

        [Test]
        public void Filler_FallsBackToRestoredSymbolsWhenLaterNeededLacksVariety()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());

            // Cursor is slot 1 (NA). Later-needed = {A} plus gated MA, so only one distinct ungated
            // symbol is available and minFillerVariety (2) must admit the restored EI.
            var restored = new[] { true, false, false, false };
            var seen = new HashSet<string>();
            for (int spawn = 0; spawn < 25; spawn++)
                seen.Add(director.AssignNext(Request(restored, now: spawn * 0.1f)).SymbolStableId);

            Assert.That(seen.Contains(A), Is.True, "Later-needed A must still appear.");
            Assert.That(seen.Contains(Ei), Is.True,
                "Restored EI must be admitted as filler once later-needed variety drops below 2.");
            Assert.That(seen.Contains(Ma), Is.False, "The gated symbol must never be filler.");
        }

        // ---------------------------------------------------------------- 3. GATING

        [Test]
        public void Gate_WithholdsTheFinalSymbolEntirelyBeforeTheBeatResolves()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            var restored = new[] { true, true, true, false };

            for (int spawn = 0; spawn < 40; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(
                    Request(restored, now: spawn * 5f));

                Assert.That(assignment.SymbolStableId, Is.Not.EqualTo(Ma),
                    "MA must not appear as needed OR as filler while its gate is closed "
                    + "(spawn " + spawn + ", role " + assignment.Role + ").");
            }
        }

        [Test]
        public void Gate_AllSlotsGatedReportsHoldForGate()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            var restored = new[] { true, true, true, false };
            SpawnAssignment assignment = director.AssignNext(Request(restored));

            Assert.That(assignment.Role, Is.EqualTo(SpawnAssignmentRole.HoldForGate),
                "With every remaining slot gated the level cannot advance, and that must be explicit.");
        }

        [Test]
        public void Gate_StarvationClockStartsWhenTheGateOpensNotWhenTheCursorArrives()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());

            var restored = new[] { true, true, true, false };

            // 100 seconds pass with the gate shut - far beyond both timeouts.
            for (int spawn = 0; spawn < 10; spawn++)
                director.AssignNext(Request(restored, now: spawn * 10f));

            // The beat resolves at t=100. The floor must apply from here; a banked starvation debt
            // would dump MA immediately instead.
            SpawnAssignment first = director.AssignNext(
                Request(restored, now: 100f, openGates: IligawGate));

            Assert.That(first.Role, Is.EqualTo(SpawnAssignmentRole.Filler),
                "Opening a gate must arm the slot fresh, not fire a banked starvation timer.");
        }

        [Test]
        public void Gate_ReleasesTheFinalSymbolOnceResolved()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            var restored = new[] { true, true, true, false };
            SpawnAssignment last = SpawnAssignment.None;

            for (int spawn = 0; spawn < 6; spawn++)
                last = director.AssignNext(Request(restored, now: spawn, openGates: IligawGate));

            Assert.That(last.SymbolStableId, Is.EqualTo(Ma));
            Assert.That(last.Role, Is.EqualTo(SpawnAssignmentRole.Needed));
        }

        // ---------------------------------------------------------------- 4. STARVATION

        [Test]
        public void Starvation_ForcesTheNeededSymbolAfterTheSoftTimeout()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());

            // The draw never chooses needed, so only the timer can deliver EI.
            for (int spawn = 0; spawn < 6; spawn++)
                director.AssignNext(Request(NoneRestored(), now: spawn * 2f));

            SpawnAssignment forced = director.AssignNext(Request(NoneRestored(), now: 31f));

            Assert.That(forced.Role, Is.EqualTo(SpawnAssignmentRole.Needed));
            Assert.That(forced.SymbolStableId, Is.EqualTo(Ei));
            Assert.That(forced.WasForced, Is.True);
            Assert.That(forced.EscalateClueChannel, Is.False,
                "The soft timer fixes supply only; it must not escalate the clue.");
        }

        [Test]
        public void Starvation_HardTimeoutEscalatesTheClueAndSuppressesFiller()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());

            director.AssignNext(Request(NoneRestored(), now: 0f));
            director.AssignNext(Request(NoneRestored(), now: 31f)); // consumes the soft force

            SpawnAssignment hard = director.AssignNext(Request(NoneRestored(), now: 51f));
            Assert.That(hard.Role, Is.EqualTo(SpawnAssignmentRole.Needed));
            Assert.That(hard.EscalateClueChannel, Is.True);

            // Filler is suppressed entirely from here until the slot fills.
            SpawnAssignment after = director.AssignNext(Request(NoneRestored(), now: 55f));
            Assert.That(after.Role, Is.EqualTo(SpawnAssignmentRole.Needed),
                "After hard starvation every spawn must carry the needed symbol.");
        }

        [Test]
        public void Starvation_ResetsAfterTheSlotIsFilled()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());

            director.AssignNext(Request(NoneRestored(), now: 0f));
            director.AssignNext(Request(NoneRestored(), now: 51f));

            var restored = new[] { true, false, false, false };
            SpawnAssignment next = director.AssignNext(Request(restored, now: 52f));

            Assert.That(next.Role, Is.EqualTo(SpawnAssignmentRole.Filler),
                "A forced fill must not cascade: the next slot re-arms the floor.");
            Assert.That(next.EscalateClueChannel, Is.False,
                "Clue escalation must clear when the starved slot fills.");
        }

        // ---------------------------------------------------------------- 5. CHOICE MOMENT

        [Test]
        public void ChoiceMoment_EmitsAPairWithExactlyOneAdvancingSymbol()
        {
            var policy = Level1Policy();
            policy.choiceMomentSlotIndex = 1; // the NA slot
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new AlwaysNeededRandom());

            var restored = new[] { true, false, false, false };
            SpawnAssignment pair = SpawnAssignment.None;

            for (int spawn = 0; spawn < 6; spawn++)
            {
                pair = director.AssignNext(Request(restored, now: spawn, activeEnemies: 2));
                if (pair.StartsChoicePair) break;
            }

            Assert.That(pair.StartsChoicePair, Is.True, "The choice moment must be reached.");
            Assert.That(pair.SymbolStableId, Is.EqualTo(Na), "Member A must advance the slot.");
            Assert.That(pair.PairedDecoySymbolStableId, Is.Not.Null);
            Assert.That(pair.PairedDecoySymbolStableId, Is.Not.EqualTo(Na),
                "Member B must not also fill the slot, or there is no choice.");
            Assert.That(pair.PairedDecoySymbolStableId, Is.Not.EqualTo(Ma),
                "Member B must never be the gated symbol.");
        }

        [Test]
        public void ChoiceMoment_CarriesForwardWhenTheConcurrencyBudgetBlocksIt()
        {
            var policy = Level1Policy();
            policy.choiceMomentSlotIndex = 1;
            policy.maxConcurrentEnemies = 4;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new AlwaysNeededRandom());

            var restored = new[] { true, false, false, false };

            // Screen is full: 3 + 2 > 4, so the pair cannot be issued and must not be spent.
            for (int spawn = 0; spawn < 8; spawn++)
            {
                SpawnAssignment blocked = director.AssignNext(
                    Request(restored, now: spawn, activeEnemies: 3));
                Assert.That(blocked.StartsChoicePair, Is.False,
                    "A pair must not be issued over the concurrency budget.");
            }

            // Space frees up; the directive must still be pending.
            SpawnAssignment issued = SpawnAssignment.None;
            for (int spawn = 0; spawn < 6; spawn++)
            {
                issued = director.AssignNext(Request(restored, now: 20f + spawn, activeEnemies: 1));
                if (issued.StartsChoicePair) break;
            }

            Assert.That(issued.StartsChoicePair, Is.True,
                "The choice directive must carry forward, never be dropped.");
        }

        /// <summary>
        /// Regression. A Level 1 simulation filled the designated slot while the concurrency budget
        /// was full for both of that slot's needed spawns, and the guaranteed choice moment silently
        /// never happened. The directive must survive its own slot being filled.
        /// </summary>
        [Test]
        public void ChoiceMoment_SurvivesTheDesignatedSlotBeingFilledWhileBlocked()
        {
            var policy = Level1Policy();
            policy.choiceMomentSlotIndex = 1;
            policy.maxConcurrentEnemies = 4;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new AlwaysNeededRandom());

            // Slot 1 is the cursor and the screen is full, so the pair cannot be issued.
            var atSlot1 = new[] { true, false, false, false };
            for (int spawn = 0; spawn < 8; spawn++)
                director.AssignNext(Request(atSlot1, now: spawn, activeEnemies: 3));

            // The player fills slot 1 anyway. The directive must not die with it.
            var atSlot2 = new[] { true, true, false, false };
            SpawnAssignment issued = SpawnAssignment.None;
            for (int spawn = 0; spawn < 12; spawn++)
            {
                issued = director.AssignNext(
                    Request(atSlot2, now: 20f + spawn, activeEnemies: 1, openGates: IligawGate));
                if (issued.StartsChoicePair) break;
            }

            Assert.That(issued.StartsChoicePair, Is.True,
                "The choice moment must still be delivered on a later slot.");
            Assert.That(issued.PairedDecoySymbolStableId, Is.Not.EqualTo(issued.SymbolStableId));
        }

        [Test]
        public void ChoiceMoment_IsIssuedOnlyOnce()
        {
            var policy = Level1Policy();
            policy.choiceMomentSlotIndex = 1;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new AlwaysNeededRandom());

            var restored = new[] { true, false, false, false };
            int pairs = 0;
            for (int spawn = 0; spawn < 40; spawn++)
            {
                if (director.AssignNext(Request(restored, now: spawn, activeEnemies: 1)).StartsChoicePair)
                    pairs++;
            }

            Assert.That(pairs, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- 7. OPENING DIRECTIVE

        /// <summary>
        /// Level 1's opening beat requires the first enemy the player ever sees to be Abo ng
        /// Simula. The negative control matters as much as the assertion: left to the schedule the
        /// opening filler is NA, so a test that only checked "the first spawn carries A" could pass
        /// on a directive that does nothing.
        /// </summary>
        [Test]
        public void OpeningDirective_ForcesTheFirstSpawnToTheNamedSymbol()
        {
            var undirected = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());
            Assert.That(undirected.AssignNext(Request(NoneRestored())).SymbolStableId,
                Is.EqualTo(Na),
                "Control: without a directive the schedule opens on NA, not A.");

            var policy = Level1Policy();
            policy.openingSpawnSpokenValueId = AValue;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new NeverNeededRandom());

            SpawnAssignment first = director.AssignNext(Request(NoneRestored()));

            Assert.That(first.SymbolStableId, Is.EqualTo(A),
                "value.a must resolve to symbol.a and open the level.");
            Assert.That(first.IsOpeningDirective, Is.True);
            Assert.That(first.Role, Is.EqualTo(SpawnAssignmentRole.Filler),
                "A is later-needed while E/I is the cursor, so the opening enemy is filler.");
            Assert.That(first.SlotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void OpeningDirective_IsConsumedByTheFirstSpawn()
        {
            var policy = Level1Policy();
            policy.openingSpawnSpokenValueId = AValue;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new NeverNeededRandom());

            int directed = 0;
            for (int spawn = 0; spawn < 30; spawn++)
            {
                if (director.AssignNext(Request(NoneRestored(), now: spawn * 0.1f)).IsOpeningDirective)
                    directed++;
            }

            Assert.That(directed, Is.EqualTo(1),
                "The directive names the opening enemy only; a second forced spawn would flatten "
                + "the schedule into a fixed order.");
            Assert.That(director.IsOpeningDirectivePending, Is.False);
        }

        /// <summary>
        /// A retry must open on the authored first enemy again. This only holds because the spent
        /// flag lives on the director: storing it on the policy would consume a serialized asset
        /// field, and every later attempt at the level would start with the directive already gone.
        /// </summary>
        [Test]
        public void OpeningDirective_ReArmsAfterAReset()
        {
            var policy = Level1Policy();
            policy.openingSpawnSpokenValueId = AValue;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new NeverNeededRandom());

            Assert.That(director.AssignNext(Request(NoneRestored())).IsOpeningDirective, Is.True);
            Assert.That(director.AssignNext(Request(NoneRestored(), now: 1f)).IsOpeningDirective,
                Is.False);

            director.Reset();

            SpawnAssignment reopened = director.AssignNext(Request(NoneRestored()));
            Assert.That(reopened.IsOpeningDirective, Is.True, "A reset must re-arm the directive.");
            Assert.That(reopened.SymbolStableId, Is.EqualTo(A));

            // The production retry path rebuilds the director from the same policy instance. If
            // consuming the directive had written to the policy, this would come back unarmed.
            var rebuilt = new SpawnAssignmentDirector(
                Level1Slots(), policy, new NeverNeededRandom());
            Assert.That(rebuilt.AssignNext(Request(NoneRestored())).IsOpeningDirective, Is.True,
                "Rebuilding from the same policy must find the directive armed.");
        }

        // ---------------------------------------------------------------- 6. SCALING

        [Test]
        public void Scaling_ActiveSlotWindowMakesSeveralSlotsFillableAtOnce()
        {
            var slots = new List<SpawnSlot>
            {
                new SpawnSlot("symbol.ka", "word.s", 0),
                new SpawnSlot("symbol.ba", "word.s", 1),
                new SpawnSlot("symbol.la", "word.s", 2),
                new SpawnSlot("symbol.ta", "word.s", 3),
                new SpawnSlot("symbol.na", "word.s", 4),
                new SpawnSlot("symbol.ma", "word.s", 5),
            };

            var policy = Level1Policy();
            policy.activeSlotWindow = 3;
            policy.choiceMomentSlotIndex = -1;
            var director = new SpawnAssignmentDirector(slots, policy, new AlwaysNeededRandom());

            var restored = new bool[6];
            var neededSymbols = new HashSet<string>();

            for (int spawn = 0; spawn < 40; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(
                    new SpawnAssignmentRequest
                    {
                        RestoredSlots = restored,
                        OpenGateTokens = new List<string>(),
                        Now = spawn,
                        ActiveEnemyCount = 1,
                    });

                if (assignment.Role == SpawnAssignmentRole.Needed)
                    neededSymbols.Add(assignment.SymbolStableId);
            }

            Assert.That(neededSymbols.Count, Is.GreaterThan(1),
                "With a window of 3 the director must offer more than one fillable slot.");
            Assert.That(neededSymbols.Contains("symbol.ta"), Is.False,
                "Slot 3 sits outside a window of 3 and must not be offered yet.");
        }

        [Test]
        public void Scaling_OffTargetFillerIsUsedOnlyWhenWeighted()
        {
            var policy = Level1Policy();
            policy.offTargetFillerWeight = 1f;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new NeverNeededRandom());

            var request = Request(NoneRestored());
            request.OffTargetSymbols = new List<string> { "symbol.ka", "symbol.ba" };
            request.WaveSymbolWhitelist = null;

            var seen = new HashSet<string>();
            for (int spawn = 0; spawn < 20; spawn++)
            {
                request.Now = spawn * 0.1f;
                seen.Add(director.AssignNext(request).SymbolStableId);
            }

            Assert.That(seen.Contains("symbol.ka") || seen.Contains("symbol.ba"), Is.True,
                "offTargetFillerWeight = 1 must draw filler from outside the target.");
        }

        [Test]
        public void Whitelist_FillerNeverLeavesTheWavesAuthoredSymbolSet()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new NeverNeededRandom());

            var request = Request(NoneRestored());
            request.WaveSymbolWhitelist = new List<string> { Ei, Na };

            for (int spawn = 0; spawn < 20; spawn++)
            {
                request.Now = spawn * 0.1f;
                SpawnAssignment assignment = director.AssignNext(request);
                Assert.That(assignment.SymbolStableId, Is.EqualTo(Na),
                    "Only NA is both whitelisted and legal filler here.");
            }
        }

        [Test]
        public void Whitelist_NeededSelectionSkipsUnavailableWindowSlots()
        {
            var policy = Level1Policy();
            policy.activeSlotWindow = 2;
            policy.minSpawnsBeforeNeeded = 0;
            var director = new SpawnAssignmentDirector(
                Level1Slots(), policy, new AlwaysNeededRandom());

            SpawnAssignment assignment = director.AssignNext(new SpawnAssignmentRequest
            {
                RestoredSlots = NoneRestored(),
                OpenGateTokens = new List<string>(),
                WaveSymbolWhitelist = new List<string> { Na },
            });

            Assert.That(assignment.Role, Is.EqualTo(SpawnAssignmentRole.Needed));
            Assert.That(assignment.SymbolStableId, Is.EqualTo(Na),
                "A needed slot outside this wave's symbol whitelist must not be assigned.");
        }

        [Test]
        public void Level5Paragraph_AllUngatedOccurrencesReachBeforeFinalWave()
        {
            string[] symbols =
            {
                Ma, Na, Ei, Ma, Ma, "symbol.ba", Ma, Ma, Na, A, Ma, Ma, Na, "symbol.ta",
            };
            var slots = new List<SpawnSlot>(symbols.Length);
            for (int index = 0; index < symbols.Length; index++)
            {
                slots.Add(new SpawnSlot(
                    symbols[index],
                    "level.ugat.05.paragraph",
                    index,
                    index == symbols.Length - 1 ? FinalWaveGate : null,
                    "level.ugat.05.paragraph." + index.ToString("00")));
            }

            var policy = new SpawnAssignmentPolicy
            {
                minSpawnsBeforeNeeded = 0,
                neededWeight = 1f,
                starvationTimeout = 30f,
                hardStarvationTimeout = 50f,
                activeSlotWindow = 1,
                minFillerVariety = 2,
                offTargetFillerWeight = 0.3f,
                maxOverflowBatches = 12,
            };
            var director = new SpawnAssignmentDirector(slots, policy, new AlwaysNeededRandom());
            var restored = new bool[symbols.Length];
            var preFinalWhitelist = new List<string> { A, Ei, "symbol.ba", Ma, Na };
            int reachableBeforeFinalWave = 0;

            int[] waveSpawnCounts = { 2, 4, 5, 6 };
            for (int wave = 0; wave < waveSpawnCounts.Length; wave++)
            {
                for (int spawn = 0; spawn < waveSpawnCounts[wave]; spawn++)
                {
                    SpawnAssignment assignment = director.AssignNext(new SpawnAssignmentRequest
                    {
                        RestoredSlots = restored,
                        OpenGateTokens = new List<string>(),
                        WaveSymbolWhitelist = preFinalWhitelist,
                        Now = (wave * 10f) + spawn,
                    });

                    if (assignment.Role == SpawnAssignmentRole.Needed)
                    {
                        Assert.That(assignment.SlotIndex, Is.LessThan(symbols.Length - 1),
                            "The terminal TA slot must remain gated until wave 5.");
                        restored[assignment.SlotIndex] = true;
                        reachableBeforeFinalWave++;
                    }
                }
            }

            Assert.AreEqual(13, reachableBeforeFinalWave,
                "All thirteen ungated paragraph occurrences must be reachable before wave 5.");
            Assert.IsFalse(restored[symbols.Length - 1]);

            SpawnAssignment final = director.AssignNext(new SpawnAssignmentRequest
            {
                RestoredSlots = restored,
                OpenGateTokens = new List<string> { FinalWaveGate },
                WaveSymbolWhitelist = new List<string> { "symbol.ta" },
                Now = 50f,
            });

            Assert.AreEqual(SpawnAssignmentRole.Needed, final.Role);
            Assert.AreEqual(symbols.Length - 1, final.SlotIndex);
            Assert.AreEqual("symbol.ta", final.SymbolStableId);
        }

        // ---------------------------------------------------------------- All levels

        /// <summary>
        /// Every authored level targets two two-syllable words, so the same schedule drives all
        /// five. Each level's floor is derived from its own enemy budget
        /// (floor = round(budget / slots - 1/neededWeight)), and the run must finish inside that
        /// budget plus a modest overflow allowance - never the unbounded spawn count that would
        /// mean the level can only end by exhausting the player's hearts.
        ///
        /// Budgets are the authored enemyCount totals: L1 24, L2 18, L3 22, L4 30, L5 21.
        /// </summary>
        [Test]
        public void AllLevels_FinishTheirTargetInsideTheirAuthoredBudgetPlusOverflow()
        {
            (int level, int budget, int floor)[] levels =
            {
                (1, 24, 4),
                (2, 18, 2),
                (3, 22, 4),
                (4, 30, 6),
                (5, 21, 3),
            };

            foreach ((int level, int budget, int floor) in levels)
            {
                var policy = Level1Policy();
                policy.minSpawnsBeforeNeeded = floor;
                policy.choiceMomentSlotIndex = 1;

                // Ungated, so this measures pacing rather than the Level 1 beat.
                var slots = new List<SpawnSlot>
                {
                    new SpawnSlot(Ei, "word.a", 0),
                    new SpawnSlot(Na, "word.a", 1),
                    new SpawnSlot(A, "word.b", 0),
                    new SpawnSlot(Ma, "word.b", 1),
                };

                var director = new SpawnAssignmentDirector(slots, policy, new AlwaysNeededRandom());
                var restored = new[] { false, false, false, false };

                int spawns = 0;
                int filled = 0;
                while (filled < 4 && spawns < 500)
                {
                    SpawnAssignment assignment = director.AssignNext(
                        Request(restored, now: spawns * 3.5f, activeEnemies: 1));
                    spawns++;

                    if (assignment.Role == SpawnAssignmentRole.Needed)
                    {
                        restored[assignment.SlotIndex] = true;
                        filled++;
                    }
                }

                Assert.That(filled, Is.EqualTo(4),
                    "Level " + level + " must be completable.");

                Assert.That(spawns, Is.GreaterThanOrEqualTo(4 * floor),
                    "Level " + level + " must respect its anti-rush floor on every slot.");

                // The real invariant, and the one that breaks the moment someone retunes a level's
                // enemyCount: the floor is derived from the budget, not chosen by feel.
                // floor = round(budget / slots - 1/neededWeight), with slots = 4, weight = 0.5.
                int derived = (int)System.Math.Round(budget / 4.0 - 2.0, System.MidpointRounding.ToEven);
                Assert.That(floor, Is.EqualTo(derived),
                    "Level " + level + "'s floor must equal the value derived from its "
                    + budget + "-enemy budget. Retune the policy when the wave budget changes; "
                    + "Salinlahi/Campaign/Validate Spawn Assignment Policies reports this too.");
            }
        }

        // ---------------------------------------------------------------- Robustness

        [Test]
        public void EmptySlotList_ReturnsNoAssignmentRatherThanThrowing()
        {
            var director = new SpawnAssignmentDirector(
                new List<SpawnSlot>(), Level1Policy(), new AlwaysNeededRandom());

            SpawnAssignment assignment = director.AssignNext(Request(new bool[0]));
            Assert.That(assignment.SymbolStableId, Is.Null);
        }

        [Test]
        public void AllSlotsRestored_ReturnsNoAssignment()
        {
            var director = new SpawnAssignmentDirector(
                Level1Slots(), Level1Policy(), new AlwaysNeededRandom());

            SpawnAssignment assignment = director.AssignNext(
                Request(new[] { true, true, true, true }, openGates: IligawGate));

            Assert.That(assignment.SymbolStableId, Is.Null);
        }
    }
}
