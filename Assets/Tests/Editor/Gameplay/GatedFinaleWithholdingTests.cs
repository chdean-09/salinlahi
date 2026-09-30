using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// The coordinator half of the locked-box rule: it must report the real carriers on the field
    /// to the director and leave Iligaw's copies out, since a copy's kill never restores a box.
    /// </summary>
    public sealed class GatedFinaleLiveCarrierWiringTests
    {
        private readonly List<Object> _objectsToDestroy = new List<Object>();
        private BaybayinCharacterSO _ma;
        private BaybayinCharacterSO _na;

        [SetUp]
        public void SetUp()
        {
            ClearTrackerInstance();
            var trackerGo = new GameObject("ActiveEnemyTracker_LiveCarrier_Test");
            _objectsToDestroy.Add(trackerGo);
            typeof(Singleton<ActiveEnemyTracker>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { trackerGo.AddComponent<ActiveEnemyTracker>() });

            _ma = Symbol("MA", "symbol.ma");
            _na = Symbol("NA", "symbol.na");
        }

        [TearDown]
        public void TearDown()
        {
            ClearTrackerInstance();
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void RealCarrierOnTheField_WithholdsTheLockedSymbol()
        {
            SpawnAssignmentCoordinator coordinator = CoordinatorForMaNaMa();
            CreateEnemy(_ma, isDecoy: false);

            SpawnAssignment assignment = coordinator.AssignNext(null);

            Assert.AreNotEqual("symbol.ma", assignment.SymbolStableId,
                "A real MA is already walking for the one unlocked MA box, so the coordinator must "
                + "tell the director, or a second MA can only die into the locked box.");
        }

        [Test]
        public void FalseCopyOnTheField_IsNotCountedAsACarrier()
        {
            SpawnAssignmentCoordinator coordinator = CoordinatorForMaNaMa();
            CreateEnemy(_ma, isDecoy: true);

            SpawnAssignment assignment = coordinator.AssignNext(null);

            Assert.AreEqual("symbol.ma", assignment.SymbolStableId,
                "A copy wearing MA restores nothing when it dies, so it must not use up the "
                + "unlocked MA box's carrier; the real MA still has to be offered.");
        }

        /// <summary>[MA, NA] then [MA]: the last MA is the locked finale.</summary>
        private SpawnAssignmentCoordinator CoordinatorForMaNaMa()
        {
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(level);
            level.activeClueCombatEnabled = true;
            level.spawnAssignmentPolicy = new SpawnAssignmentPolicy
            {
                gateFinalSlotToFinalWave = true,
                minSpawnsBeforeNeeded = 0,
                neededWeight = 1f,
                activeSlotWindow = 1,
                choiceMomentSlotIndex = -1,
                offTargetFillerWeight = 0f,
            };
            level.focusWords = new List<FocusWordDefinition>
            {
                Word("word.test.mana", _ma, _na),
                Word("word.test.ma", _ma),
            };

            var go = new GameObject("SpawnAssignmentCoordinator_LiveCarrier_Test");
            _objectsToDestroy.Add(go);
            var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(level, null);
            Assert.AreEqual(3, coordinator.Slots.Count, "setup: [MA, NA, MA] expected.");
            Assert.AreEqual(SpawnGateRegistry.FinalWaveReached, coordinator.Slots[2].GateToken,
                "setup: the last MA must be the locked finale.");
            return coordinator;
        }

        private static FocusWordDefinition Word(string stableId, params BaybayinCharacterSO[] symbols)
        {
            var decomposition = new List<SymbolValueReference>();
            foreach (BaybayinCharacterSO symbol in symbols)
                decomposition.Add(new SymbolValueReference { symbol = symbol });
            return new FocusWordDefinition { stableId = stableId, decomposition = decomposition };
        }

        private BaybayinCharacterSO Symbol(string characterId, string stableId)
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.stableId = stableId;
            _objectsToDestroy.Add(symbol);
            return symbol;
        }

        private void CreateEnemy(BaybayinCharacterSO character, bool isDecoy)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = isDecoy ? "iligaw_anino" : "mantsa";
            data.assignedCharacter = character;
            data.isDecoy = isDecoy;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            _objectsToDestroy.Add(data);

            var go = new GameObject(isDecoy ? "FalseCopy_Test" : "RealCarrier_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            typeof(Enemy).GetField("_showDebugLabels", BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(enemy, false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            typeof(Enemy).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?
                .Invoke(enemy, null);
            Assert.IsTrue(enemy.Initialize(data), "setup: the enemy must initialize and register.");
        }

        private static void ClearTrackerInstance()
        {
            typeof(Singleton<ActiveEnemyTracker>)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?
                .SetValue(null, null);
        }
    }

    /// <summary>
    /// Closes the last unverified gate of the gated-finale feature.
    ///
    /// <see cref="GatedFinaleSlotTests"/> proves the coordinator derives the gate onto the right
    /// slot - the last one whose symbol occurs exactly once, NOT simply the last slot, because
    /// restoration is by symbol and a repeated symbol's gate withholds nothing.
    /// <see cref="FinalWaveIndexTests"/> proves <c>WaveManager.IsFinalWaveIndex</c> picks the
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

        private static SpawnAssignmentRequest RequestWithCarriers(
            bool[] restored,
            float now,
            string[] liveCarriers,
            params string[] openGates)
        {
            SpawnAssignmentRequest request = Request(
                restored, now, activeEnemies: liveCarriers.Length, openGates: openGates);
            request.LiveCarrierSymbols = liveCarriers;
            return request;
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

        [Test]
        public void FinaleInMiddle_WaitsForLaterOccurrencesEvenAfterFinalWaveBegins()
        {
            var slots = new List<SpawnSlot>
            {
                new SpawnSlot(Ei, "sentence.one", 0),
                new SpawnSlot(Ma, "sentence.one", 1, FinalWaveGate),
                new SpawnSlot(Na, "sentence.two", 0),
            };
            SpawnAssignmentPolicy policy = Policy();
            policy.minSpawnsBeforeNeeded = 0;
            var director = new SpawnAssignmentDirector(
                slots, policy, new AlwaysFavoursNeededRandom());
            var restored = new[] { true, false, false };

            SpawnAssignment beforeLastOther = director.AssignNext(
                Request(restored, now: 100f, activeEnemies: 0, openGates: FinalWaveGate));
            Assert.AreEqual(Na, beforeLastOther.SymbolStableId);
            Assert.AreEqual(2, beforeLastOther.SlotIndex);

            restored[2] = true;
            SpawnAssignment afterLastOther = director.AssignNext(
                Request(restored, now: 110f, activeEnemies: 0, openGates: FinalWaveGate));
            Assert.AreEqual(Ma, afterLastOther.SymbolStableId);
            Assert.AreEqual(1, afterLastOther.SlotIndex);
        }

        /// <summary>
        /// Level 3's shape: MA twice, the last MA locked until the final wave. With one MA carrier
        /// already walking for the unlocked box, a second MA could only be killed into the locked
        /// box: an enemy dies and nothing fills, which the 2026-09-29 playtest read as a correct
        /// drawing that did not register.
        /// </summary>
        private static List<SpawnSlot> RepeatedLockedMaSlots()
        {
            return new List<SpawnSlot>
            {
                new SpawnSlot(Ma, "sentence.one", 0),
                new SpawnSlot(Na, "sentence.one", 1),
                new SpawnSlot(Ma, "sentence.two", 0, FinalWaveGate),
            };
        }

        private static SpawnAssignmentDirector ImmediateDirector(List<SpawnSlot> slots)
        {
            SpawnAssignmentPolicy policy = Policy();
            policy.minSpawnsBeforeNeeded = 0;
            return new SpawnAssignmentDirector(slots, policy, new AlwaysFavoursNeededRandom());
        }

        [Test]
        public void LockedRepeatedSymbol_WithItsUnlockedBoxAlreadyCarried_IsNotSpawnedAgain()
        {
            SpawnAssignmentDirector director = ImmediateDirector(RepeatedLockedMaSlots());
            var restored = new[] { false, false, false };

            for (int spawn = 0; spawn < 30; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(
                    RequestWithCarriers(restored, now: spawn * 5f, liveCarriers: new[] { Ma }));

                Assert.That(assignment.SymbolStableId, Is.Not.EqualTo(Ma),
                    "A second MA was assigned on spawn " + spawn + " (role " + assignment.Role
                    + ") while the one unlocked MA box already has a carrier walking. Whichever MA "
                    + "the player kills second can only land on the locked box and fill nothing.");
            }
        }

        [Test]
        public void LockedRepeatedSymbol_IsStillSpawned_WhileItsUnlockedBoxHasNoCarrier()
        {
            SpawnAssignmentDirector director = ImmediateDirector(RepeatedLockedMaSlots());

            SpawnAssignment assignment = director.AssignNext(
                RequestWithCarriers(new[] { false, false, false }, now: 0f, liveCarriers: new string[0]));

            Assert.AreEqual(Ma, assignment.SymbolStableId,
                "With no MA on the field the unlocked MA box must still be offered, or the rule "
                + "above would be passing by starving MA outright.");
            Assert.AreEqual(SpawnAssignmentRole.Needed, assignment.Role);
            Assert.AreEqual(0, assignment.SlotIndex);
        }

        [Test]
        public void LockedRepeatedSymbol_AllowsOneCarrierPerUnlockedBox_NotOnePerSymbol()
        {
            var slots = new List<SpawnSlot>
            {
                new SpawnSlot(Ma, "sentence.one", 0),
                new SpawnSlot(Ma, "sentence.one", 1),
                new SpawnSlot(Na, "sentence.one", 2),
                new SpawnSlot(Ma, "sentence.two", 0, FinalWaveGate),
            };
            SpawnAssignmentDirector director = ImmediateDirector(slots);

            SpawnAssignment assignment = director.AssignNext(RequestWithCarriers(
                new[] { false, false, false, false }, now: 0f, liveCarriers: new[] { Ma }));

            Assert.AreEqual(Ma, assignment.SymbolStableId,
                "Two unlocked MA boxes can take two carriers; one on the field leaves room for another.");
        }

        [Test]
        public void SymbolWithoutALockedBox_IsNotRationedByLiveCarriers()
        {
            var slots = new List<SpawnSlot>
            {
                new SpawnSlot(Ma, "word.one", 0),
                new SpawnSlot(Na, "word.one", 1),
                new SpawnSlot(A, "word.two", 0, FinalWaveGate),
            };
            SpawnAssignmentDirector director = ImmediateDirector(slots);

            SpawnAssignment assignment = director.AssignNext(RequestWithCarriers(
                new[] { false, false, false }, now: 0f, liveCarriers: new[] { Ma, Ma }));

            Assert.AreEqual(Ma, assignment.SymbolStableId,
                "MA has no locked box, so a surplus MA can only die into an already-restored box, "
                + "the one kill-without-fill the design accepts. Rationing it would change pacing "
                + "on every level for nothing.");
        }
    }
}
