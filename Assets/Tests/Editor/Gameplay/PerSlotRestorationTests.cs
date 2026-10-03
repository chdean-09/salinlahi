using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// One defeated carrier restores ONE slot.
    ///
    /// <para>
    /// <b>The rule this replaces.</b> <c>Apply</c> filled every slot whose symbol matched, across
    /// every focus word, from a single kill. Four separate mechanisms exist only to route around
    /// that: DerivedFinaleGate's "last symbol occurring exactly once", the GatedFinaleUnwinnable
    /// validator rule, Level 2's finale landing on MA rather than its last slot, and the authored
    /// finale gating every slot carrying its symbol. A kill that fills one slot makes the last slot
    /// always withholdable, and all four become unnecessary.
    /// </para>
    ///
    /// <para>
    /// Every fixture in the suite used words whose symbols were distinct, so symbol-wide
    /// restoration passed all of them. These are the assertions that could not.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class PerSlotRestorationTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        private BaybayinCharacterSO Symbol(string characterId, string stableId)
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.stableId = stableId;
            _created.Add(symbol);
            return symbol;
        }

        private static FocusWordDefinition Word(
            string stableId, string spelling, params BaybayinCharacterSO[] symbols)
        {
            var decomposition = new List<SymbolValueReference>(symbols.Length);
            foreach (BaybayinCharacterSO symbol in symbols)
                decomposition.Add(new SymbolValueReference { symbol = symbol });

            return new FocusWordDefinition
            {
                stableId = stableId,
                latinSpelling = spelling,
                displayLabel = spelling,
                meaning = spelling,
                decomposition = decomposition,
            };
        }

        /// <summary>Level 3's shape: BATA + TAMA, with TA in both.</summary>
        private ActiveClueRestorationState LevelThreeShape(
            out BaybayinCharacterSO ba, out BaybayinCharacterSO ta, out BaybayinCharacterSO ma)
        {
            ba = Symbol("BA", "symbol.test.slot.ba");
            ta = Symbol("TA", "symbol.test.slot.ta");
            ma = Symbol("MA", "symbol.test.slot.ma");

            var state = new ActiveClueRestorationState();
            state.Configure(new List<FocusWordDefinition>
            {
                Word("word.bata", "BATA", ba, ta),
                Word("word.tama", "TAMA", ta, ma),
            });
            return state;
        }

        [Test]
        public void OneKill_RestoresOneSlot_EvenWhenTheSymbolRepeats()
        {
            ActiveClueRestorationState state = LevelThreeShape(out _, out BaybayinCharacterSO ta, out _);

            state.Apply(ta.stableId);

            Assert.AreEqual(1, state.RestoredSlotCount,
                "One TA carrier fell, so one TA slot is restored. Filling both from a single kill "
                + "is what makes a repeated symbol ungateable and lets a level finish early.");
        }

        [Test]
        public void SecondKill_RestoresTheSecondSlot()
        {
            ActiveClueRestorationState state = LevelThreeShape(out _, out BaybayinCharacterSO ta, out _);

            state.Apply(ta.stableId);
            state.Apply(ta.stableId);

            Assert.AreEqual(2, state.RestoredSlotCount,
                "BATA and TAMA each need their own TA. Two kills, two slots.");
        }

        [Test]
        public void RepeatedSymbol_IsRestoredInReadingOrder()
        {
            ActiveClueRestorationState state = LevelThreeShape(
                out _, out BaybayinCharacterSO ta, out _);

            state.Apply(ta.stableId);

            Assert.IsTrue(state.IsTargetComplete("word.bata", ta.stableId),
                "The first unrestored TA in reading order is BATA's, so that is the one filled. "
                + "Reading order is what the clue rail renders and what the player is following.");
            Assert.IsFalse(state.IsTargetComplete("word.tama", ta.stableId),
                "TAMA's TA is still owed.");
        }

        [Test]
        public void WordIsNotComplete_UntilEveryOneOfItsSlotsIsEarned()
        {
            ActiveClueRestorationState state = LevelThreeShape(
                out BaybayinCharacterSO ba, out BaybayinCharacterSO ta, out _);

            state.Apply(ba.stableId);
            state.Apply(ta.stableId);

            Assert.IsTrue(state.IsWordComplete("word.bata"), "BATA had BA and TA; both are earned.");
            Assert.IsFalse(state.IsComplete,
                "TAMA still needs its own TA and its MA. The level is not finished.");
        }

        [Test]
        public void DistinctSymbols_BehaveExactlyAsBefore()
        {
            BaybayinCharacterSO ei = Symbol("EI", "symbol.test.slot.ei");
            BaybayinCharacterSO na = Symbol("NA", "symbol.test.slot.na");

            var state = new ActiveClueRestorationState();
            state.Configure(new List<FocusWordDefinition> { Word("word.ina", "INA", ei, na) });

            state.Apply(ei.stableId);
            Assert.AreEqual(1, state.RestoredSlotCount);
            state.Apply(na.stableId);

            Assert.IsTrue(state.IsComplete,
                "A word whose symbols are all distinct is unaffected by this change. Every level "
                + "shipped before Level 3 is this shape, which is why nothing caught the old rule.");
        }

        [Test]
        public void ApplyingASymbolTheTextDoesNotNeed_RestoresNothing()
        {
            ActiveClueRestorationState state = LevelThreeShape(out _, out _, out _);
            BaybayinCharacterSO stranger = Symbol("HA", "symbol.test.slot.ha");

            state.Apply(stranger.stableId);

            Assert.AreEqual(0, state.RestoredSlotCount,
                "A carrier the target text never asked for restores nothing.");
        }
    }
}
