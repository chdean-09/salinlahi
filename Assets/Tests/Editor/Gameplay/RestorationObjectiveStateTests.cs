using System.Collections.Generic;
using NUnit.Framework;
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
        public void LaterUnitSymbol_DoesNotBypassAnIncompleteEarlierUnit()
        {
            BaybayinCharacterSO ma = Symbol("MA", "symbol.test.ma");
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata", Target("bata.ba", ba), Target("bata.ta", Symbol("TA", "symbol.test.ta"))),
                Unit("mata", Target("mata.ma", ma), Target("mata.ta", Symbol("TA", "symbol.test.ta"))));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            RestorationProgressResult result = state.TryRestore(ma.stableId);

            Assert.IsFalse(result.Applied);
            Assert.AreEqual(0, state.RestoredTargetCount);
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
        public void ExplicitCompletionOrder_BlocksALaterSymbolUntilTheEarlierOccurrenceIsRestored()
        {
            BaybayinCharacterSO ba = Symbol("BA", "symbol.test.ba.blocked");
            BaybayinCharacterSO ta = Symbol("TA", "symbol.test.ta.blocked");
            RestorationObjectiveDefinition definition = Definition(
                Unit("bata",
                    Target("bata.ba", ba, 1),
                    Target("bata.ta", ta, 0)));

            var state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.IsFalse(state.TryRestore(ba.stableId).Applied,
                "A later completion-order symbol must not bypass the active earlier target.");
            Assert.AreEqual(0, state.RestoredTargetCount);
            Assert.AreEqual("bata.ta", state.TryRestore(ta.stableId).OccurrenceId);
            Assert.AreEqual("bata.ba", state.TryRestore(ba.stableId).OccurrenceId);
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

            Assert.IsFalse(state.TryRestore(ei.stableId, "value.i").Applied);
            Assert.AreEqual("spoken.e", state.TryRestore(ei.stableId, "value.e").OccurrenceId);
            Assert.AreEqual("spoken.i", state.TryRestore(ei.stableId, "value.i").OccurrenceId);
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
