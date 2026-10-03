using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public sealed class RestorationObjectiveStateTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void RepeatedSymbol_RestoresOneOccurrenceInTheActiveUnit()
        {
            BaybayinCharacterSO ta = Symbol("TA", "symbol.test.ta");
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata", Target("bata.ba", ba), Target("bata.ta.01", ta)),
                Unit("mata", Target("mata.ma", Symbol("MA", "symbol.test.ma")), Target("mata.ta.01", ta)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            RestorationProgressResult first = state.TryRestore(ta.stableId);

            Assert.IsTrue(first.Applied);
            Assert.AreEqual("bata.ta.01", first.OccurrenceId);
            Assert.AreEqual(1, state.RestoredTargetCount);
            Assert.AreEqual("bata", state.ActiveUnitId);
            Assert.IsFalse(state.IsComplete);
        }

        [Test]
        public void InaNa_RestoresBeforeI_AndAmaCanRestoreBeforeInaCompletes()
        {
            BaybayinCharacterSO i = Symbol("I", "symbol.test.ina.i");
            BaybayinCharacterSO na = Symbol("NA", "symbol.test.ina.na");
            BaybayinCharacterSO a = Symbol("A", "symbol.test.ama.a");
            BaybayinCharacterSO ma = Symbol("MA", "symbol.test.ama.ma");
            RestorationObjectiveDefinition definition = Definition(
                Unit("ina", Target("ina.i", i, 0), Target("ina.na", na, 1)),
                Unit("ama", Target("ama.a", a, 2), Target("ama.ma", ma, 3)));
            var state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.AreEqual("ina.na", state.TryRestore(na.stableId).OccurrenceId);
            Assert.AreEqual("ama.a", state.TryRestore(a.stableId).OccurrenceId);
            Assert.IsTrue(state.IsOccurrenceRestored("ina.na"));
            Assert.IsTrue(state.IsOccurrenceRestored("ama.a"));
            Assert.AreEqual("ina.i", state.TryRestore(i.stableId).OccurrenceId);
            Assert.AreEqual("ama.ma", state.TryRestore(ma.stableId).OccurrenceId);
            Assert.IsTrue(state.IsComplete);
        }

        [Test]
        public void LevelFourFinale_MustWaitForLastWaveAndEveryOtherOccurrence()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level4_Config.asset");
            Assert.IsNotNull(level);
            var objectiveObject = new GameObject("restoration objective test");
            var coordinatorObject = new GameObject("spawn assignment test");
            _created.Add(objectiveObject);
            _created.Add(coordinatorObject);
            RestorationObjectiveController objective =
                objectiveObject.AddComponent<RestorationObjectiveController>();
            SpawnAssignmentCoordinator coordinator =
                coordinatorObject.AddComponent<SpawnAssignmentCoordinator>();
            objective.Configure(level);
            coordinator.ApplyLevel(level, null);
            Assert.IsTrue(coordinator.IsActive);
            Assert.AreEqual(SpawnGateRegistry.FinalWaveReached,
                coordinator.Slots[5].GateToken);
            const string finale = "level.ugat.04.sentence.na.03";
            Assert.IsFalse(coordinator.CanRestoreOccurrence(finale));
            Assert.IsFalse(objective.CanRestoreOccurrence(finale));

            Assert.IsTrue(objective.TryRestore("symbol.ei").Applied);
            Assert.IsTrue(objective.TryRestore("symbol.na").Applied);
            Assert.IsTrue(objective.TryRestore("symbol.a").Applied);
            Assert.IsTrue(objective.TryRestore("symbol.na").Applied);
            Assert.IsFalse(objective.TryRestore("symbol.na").Applied,
                "The finale occurrence must wait before the last wave.");

            coordinator.OpenGate(SpawnGateRegistry.FinalWaveReached);
            Assert.IsFalse(objective.TryRestore("symbol.na").Applied,
                "The last wave alone must not release the finale while other slots remain.");
            Assert.IsTrue(objective.TryRestore("symbol.ma").Applied);
            Assert.AreEqual(finale, objective.TryRestore("symbol.na").OccurrenceId);
            Assert.IsTrue(objective.IsComplete);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void FirstFiveLevels_RestoreEveryNonFinalOccurrenceInAnyOrder(int levelNumber)
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"Assets/ScriptableObjects/Levels/Level{levelNumber}_Config.asset");
            Assert.IsNotNull(level);
            var objectiveObject = new GameObject("restoration objective test");
            var coordinatorObject = new GameObject("spawn assignment test");
            _created.Add(objectiveObject);
            _created.Add(coordinatorObject);
            RestorationObjectiveController objective =
                objectiveObject.AddComponent<RestorationObjectiveController>();
            SpawnAssignmentCoordinator coordinator =
                coordinatorObject.AddComponent<SpawnAssignmentCoordinator>();
            objective.Configure(level);
            coordinator.ApplyLevel(level, null);

            IReadOnlyList<SpawnSlot> slots = coordinator.Slots;
            Assert.Greater(slots.Count, 1);
            int finaleIndex = slots.Count - 1;
            for (int index = finaleIndex - 1; index >= 0; index--)
            {
                RestorationProgressResult result = objective.TryRestore(slots[index].SymbolStableId);
                Assert.IsTrue(result.Applied,
                    $"Level {levelNumber} slot {index} failed to restore out of order.");
            }

            Assert.AreEqual(finaleIndex, objective.State.RestoredTargetCount);
            Assert.IsFalse(objective.IsComplete);
            if (levelNumber == 1)
                coordinator.OpenGate(SpawnGateRegistry.AboAshShown);
            Assert.IsFalse(objective.TryRestore(slots[finaleIndex].SymbolStableId).Applied,
                "The last occurrence must wait until the final wave.");

            coordinator.OpenGate(SpawnGateRegistry.FinalWaveReached);
            Assert.AreEqual(slots[finaleIndex].OccurrenceId,
                objective.TryRestore(slots[finaleIndex].SymbolStableId).OccurrenceId);
            Assert.IsTrue(objective.IsComplete);
        }

        [Test]
        public void DrawFeedback_ReportsAnUnrestoredLaterOccurrenceAsARealFill()
        {
            BaybayinCharacterSO i = Symbol("I", "symbol.test.feedback.i");
            BaybayinCharacterSO na = Symbol("NA", "symbol.test.feedback.na");
            BaybayinCharacterSO a = Symbol("A", "symbol.test.feedback.a");
            RestorationObjectiveDefinition definition = Definition(
                Unit("ina", Target("ina.i", i, 0), Target("ina.na", na, 1)),
                Unit("ama", Target("ama.a", a, 2)));
            var state = new RestorationObjectiveState();
            state.Configure(definition);
            var slots = new List<TargetTextSlotMap.Slot>();
            TargetTextSlotMap.Build(definition, state, slots);

            Assert.AreEqual(DrawTextRelation.FillsCursorSlot,
                TargetTextSlotMap.Classify(slots, na.characterID, out int naSlot, out _));
            Assert.AreEqual(1, naSlot);
            Assert.AreEqual(DrawTextRelation.FillsCursorSlot,
                TargetTextSlotMap.Classify(slots, a.characterID, out int aSlot, out _));
            Assert.AreEqual(2, aSlot);
        }


        [Test]
        public void NextTargetSymbol_ReportsOnlyTheFirstIncompleteOrderedOccurrence()
        {
            BaybayinCharacterSO ma = Symbol("MA", "symbol.test.next.ma");
            BaybayinCharacterSO ta = Symbol("TA", "symbol.test.next.ta");
            RestorationObjectiveDefinition definition = Definition(
                Unit("ordered",
                    Target("ordered.ma", ma, 0),
                    Target("ordered.ta", ta, 1)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.AreEqual(ma.stableId, state.NextTargetSymbolStableId);
            Assert.IsTrue(state.TryRestore(ma.stableId).Applied);
            Assert.AreEqual(ta.stableId, state.NextTargetSymbolStableId);
        }

        [Test]
        public void LaterUnitSymbol_RestoresWhileAnEarlierUnitIsIncomplete()
        {
            BaybayinCharacterSO ma = Symbol("MA", "symbol.test.ma");
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata", Target("bata.ba", ba), Target("bata.ta", Symbol("TA", "symbol.test.ta"))),
                Unit("mata", Target("mata.ma", ma), Target("mata.ta", Symbol("TA", "symbol.test.ta"))));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            RestorationProgressResult result = state.TryRestore(ma.stableId);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual("mata.ma", result.OccurrenceId);
            Assert.AreEqual(1, state.RestoredTargetCount);
            Assert.AreEqual("bata", state.ActiveUnitId);
        }

        [Test]
        public void Reset_ClearsAllOccurrencesAndReactivatesTheFirstUnit()
        {
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata", Target("bata.ba", ba)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);
            Assert.IsTrue(state.TryRestore(ba.stableId).Applied);

            state.Reset();

            Assert.AreEqual(0, state.RestoredTargetCount);
            Assert.AreEqual("bata", state.ActiveUnitId);
            Assert.IsFalse(state.IsComplete);
        }

        [Test]
        public void ExplicitCompletionOrder_CanTeachRepeatedSymbolsBeforeVisibleWordOrder()
        {
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba.order");
            BaybayinCharacterSO ta = Symbol("TA", "symbol.test.ta.order");
            BaybayinCharacterSO ma = Symbol("MA", "symbol.test.ma.order");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata",
                    Target("bata.ba", ba, 1),
                    Target("bata.ta", ta, 0)),
                Unit("mata",
                    Target("mata.ma", ma, 2),
                    Target("mata.ta", ta, 3)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.AreEqual("bata.ta", state.TryRestore(ta.stableId).OccurrenceId);
            Assert.AreEqual("bata.ba", state.TryRestore(ba.stableId).OccurrenceId);
            Assert.AreEqual("mata.ma", state.TryRestore(ma.stableId).OccurrenceId);
            Assert.AreEqual("mata.ta", state.TryRestore(ta.stableId).OccurrenceId);
            Assert.IsTrue(state.IsComplete);
        }

        [Test]
        public void ExplicitCompletionOrder_DoesNotBlockALaterDrawnSymbol()
        {
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba.blocked");
            BaybayinCharacterSO ta = Symbol("TA", "symbol.test.ta.blocked");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata",
                    Target("bata.ba", ba, 1),
                    Target("bata.ta", ta, 0)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.AreEqual("bata.ba", state.TryRestore(ba.stableId).OccurrenceId);
            Assert.AreEqual(1, state.RestoredTargetCount);
            Assert.AreEqual("bata.ta", state.TryRestore(ta.stableId).OccurrenceId);
            Assert.IsTrue(state.IsComplete);
        }

        [Test]
        public void TargetMap_PreservesVisualSlotsWhileUsingCompletionCursor()
        {
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba.map");
            BaybayinCharacterSO ta = Symbol("TA", "symbol.test.ta.map");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata",
                    Target("bata.ba", ba, 1),
                    Target("bata.ta", ta, 0)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);
            var slots = new List<TargetTextSlotMap.Slot>();
            TargetTextSlotMap.Build(definition, state, slots);

            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(1, TargetTextSlotMap.FindCursorIndex(slots));
            DrawTextRelation relation = TargetTextSlotMap.Classify(
                slots, ta.characterID, out int slotIndex, out int cursorIndex);

            Assert.AreEqual(DrawTextRelation.FillsCursorSlot, relation);
            Assert.AreEqual(1, slotIndex);
            Assert.AreEqual(1, cursorIndex);
        }

        [Test]
        public void SentenceObjectives_AdvanceThroughRepeatedOccurrencesInOrder()
        {
            BaybayinCharacterSO i = Symbol("I", "symbol.test.i.sentence");
            BaybayinCharacterSO na = Symbol("NA", "symbol.test.na.sentence");
            BaybayinCharacterSO a = Symbol("A", "symbol.test.a.sentence");
            BaybayinCharacterSO ma = Symbol("MA", "symbol.test.ma.sentence");
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba.sentence");
            BaybayinCharacterSO ta = Symbol("TA", "symbol.test.ta.sentence");

            RestorationObjectiveDefinition definition = Definition(
                Unit("marked.one",
                    Target("l3.ma.01", ma, 0),
                    Target("l3.ba.01", ba, 1),
                    Target("l3.ta.01", ta, 2)),
                Unit("marked.two",
                    Target("l3.ma.02", ma, 3),
                    Target("l3.ta.02", ta, 4),
                    Target("l3.ma.03", ma, 5)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);
            string[] sequence = { ma.stableId, ba.stableId, ta.stableId, ma.stableId, ta.stableId, ma.stableId };
            for (int index = 0; index < sequence.Length; index++)
                Assert.IsTrue(state.TryRestore(sequence[index]).Applied);

            Assert.IsTrue(state.IsComplete);
            Assert.AreEqual(6, state.RestoredTargetCount);
            Assert.IsFalse(state.TryRestore(i.stableId).Applied);
            Assert.IsFalse(state.TryRestore(na.stableId).Applied);
            Assert.IsFalse(state.TryRestore(a.stableId).Applied);
        }

        [Test]
        public void SpokenValueIdentity_CanDisambiguateASharedSymbol()
        {
            BaybayinCharacterSO ei = Symbol("E/I", "symbol.test.ei");
            RestorationObjectiveDefinition definition = Definition(
                Unit("spoken.e", Target("spoken.e", ei, 0, "value.e")),
                Unit("spoken.i", Target("spoken.i", ei, 1, "value.i")));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.AreEqual("spoken.i", state.TryRestore(ei.stableId, "value.i").OccurrenceId);
            Assert.AreEqual("spoken.e", state.TryRestore(ei.stableId, "value.e").OccurrenceId);
        }

        private BaybayinCharacterSO Symbol(string characterId, string stableId)
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.stableId = stableId;
            _created.Add(symbol);
            return symbol;
        }

        private static RestorationObjectiveDefinition Definition(
            params RestorationObjectiveUnit[] units)
        {
            return new RestorationObjectiveDefinition
            {
                displayMode = RestorationDisplayMode.ClueOnlyWords,
                units = new List<RestorationObjectiveUnit>(units),
            };
        }

        private static RestorationObjectiveUnit Unit(
            string stableId, params RestorationObjectiveToken[] tokens)
        {
            return new RestorationObjectiveUnit
            {
                stableId = stableId,
                tokens = new List<RestorationObjectiveToken>(tokens),
            };
        }

        private static RestorationObjectiveToken Target(
            string occurrenceId, BaybayinCharacterSO symbol, int completionOrder = -1,
            string spokenValueId = null)
        {
            return new RestorationObjectiveToken
            {
                occurrenceId = occurrenceId,
                completionOrder = completionOrder,
                kind = RestorationTokenKind.Target,
                target = new SymbolValueReference { symbol = symbol, spokenValueId = spokenValueId },
            };
        }
    }
}
