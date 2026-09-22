using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public sealed class RestorationObjectiveTextFormatterTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objectsToDestroy.Count - 1; index >= 0; index--)
            {
                if (_objectsToDestroy[index] != null)
                    Object.DestroyImmediate(_objectsToDestroy[index]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void MarkedContext_UsesMarkedTargetBeforeRestoration()
        {
            BaybayinCharacterSO ma = CreateSymbol("MA");
            RestorationObjectiveDefinition definition = CreateDefinition(
                RestorationDisplayMode.MarkedContext,
                "Ang ", Target("sentence.ma", ma));

            Assert.AreEqual(
                "Ang <u>MA</u>",
                RestorationObjectiveTextFormatter.Render(definition));
        }

        [Test]
        public void HiddenContext_RevealsOnlyTheRestoredOccurrence()
        {
            BaybayinCharacterSO ma = CreateSymbol("MA");
            RestorationObjectiveDefinition definition = CreateDefinition(
                RestorationDisplayMode.HiddenContext,
                "Ang ", Target("sentence.ma", ma));
            RestorationObjectiveState state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.AreEqual(
                "Ang __",
                RestorationObjectiveTextFormatter.Render(definition, state));

            Assert.IsTrue(state.TryRestore(ma.stableId).Applied);
            Assert.AreEqual(
                "Ang MA",
                RestorationObjectiveTextFormatter.Render(definition, state));
        }

        [Test]
        public void ClueOnlyWords_HideWordUntilItsUnitCompletes()
        {
            BaybayinCharacterSO ma = CreateSymbol("MA");
            RestorationObjectiveDefinition definition = CreateDefinition(
                RestorationDisplayMode.ClueOnlyWords,
                "", Target("word.ma", ma));
            definition.units[0].clue = "clue";
            RestorationObjectiveState state = new RestorationObjectiveState();
            state.Configure(definition);

            Assert.AreEqual(
                "clue\n__",
                RestorationObjectiveTextFormatter.Render(definition, state));

            Assert.IsTrue(state.TryRestore(ma.stableId).Applied);
            Assert.AreEqual(
                "clue\nMA",
                RestorationObjectiveTextFormatter.Render(definition, state));
        }

        [Test]
        public void GuidedWords_ShowWordAndClueWithoutRuntimeState()
        {
            BaybayinCharacterSO ma = CreateSymbol("MA");
            RestorationObjectiveDefinition definition = CreateDefinition(
                RestorationDisplayMode.GuidedWords,
                "", Target("word.ma", ma));
            definition.units[0].clue = "meaning";

            Assert.AreEqual(
                "MA — meaning",
                RestorationObjectiveTextFormatter.Render(definition));
        }

        [Test]
        public void RestorationRail_ScalesLongObjectiveToViewportWidth()
        {
            float scale = ActiveCluePresenter.CalculateRailScale(2108f, 1920f);

            Assert.Less(scale, 1f);
            Assert.That(2108f * scale, Is.LessThanOrEqualTo(1920f + 0.001f));
        }

        [Test]
        public void RestorationRail_KeepsShortObjectiveAtNominalScale()
        {
            Assert.AreEqual(1f, ActiveCluePresenter.CalculateRailScale(720f, 1920f));
        }

        [Test]
        public void RestorationRailLayoutPolicy_ScalesEveryGeometryInputTogether()
        {
            RestorationRailLayoutMetrics metrics = RestorationRailLayoutPolicy.Calculate(
                nominalWidth: 2000f,
                availableWidth: 1000f,
                slotSize: new Vector2(120f, 80f),
                slotSpacing: 16f,
                wordGap: 80f,
                labelFontSize: 46f,
                labelRowHeight: 56f,
                labelGap: 12f,
                separatorFontSize: 40f);

            Assert.AreEqual(0.5f, metrics.Scale, 0.0001f);
            Assert.AreEqual(new Vector2(60f, 40f), metrics.SlotSize);
            Assert.AreEqual(8f, metrics.SlotSpacing, 0.0001f);
            Assert.AreEqual(40f, metrics.WordGap, 0.0001f);
            Assert.AreEqual(23f, metrics.LabelFontSize, 0.0001f);
            Assert.AreEqual(28f, metrics.LabelRowHeight, 0.0001f);
            Assert.AreEqual(6f, metrics.LabelGap, 0.0001f);
            Assert.AreEqual(20f, metrics.SeparatorFontSize, 0.0001f);
        }

        private BaybayinCharacterSO CreateSymbol(string label)
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.stableId = "symbol." + label.ToLowerInvariant();
            symbol.characterID = label;
            symbol.syllable = label.ToLowerInvariant();
            _objectsToDestroy.Add(symbol);
            return symbol;
        }

        private static RestorationObjectiveDefinition CreateDefinition(
            RestorationDisplayMode displayMode,
            string literal,
            RestorationObjectiveToken target)
        {
            return new RestorationObjectiveDefinition
            {
                displayMode = displayMode,
                units = new List<RestorationObjectiveUnit>
                {
                    new RestorationObjectiveUnit
                    {
                        stableId = "unit.01",
                        tokens = new List<RestorationObjectiveToken>
                        {
                            new RestorationObjectiveToken
                            {
                                kind = RestorationTokenKind.Literal,
                                literalText = literal,
                            },
                            target,
                        },
                    },
                },
            };
        }

        private static RestorationObjectiveToken Target(
            string occurrenceId,
            BaybayinCharacterSO symbol)
        {
            return new RestorationObjectiveToken
            {
                kind = RestorationTokenKind.Target,
                occurrenceId = occurrenceId,
                target = new SymbolValueReference { symbol = symbol },
            };
        }
    }
}
