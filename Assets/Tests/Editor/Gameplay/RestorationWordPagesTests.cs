using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Level 15's one-word-at-a-time finale: how the objective splits into words, that only
    /// Level 15 opts in, and that the spawn director never offers a later word's box early.
    /// </summary>
    public sealed class RestorationWordPagesTests
    {
        private const string LevelPath = "Assets/ScriptableObjects/Levels/Level{0}_Config.asset";

        // A token is only a target when it names a real symbol, so the fixture owns one.
        private BaybayinCharacterSO _symbol;

        [SetUp]
        public void SetUp()
        {
            _symbol = UnityEngine.ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            _symbol.stableId = "symbol.test";
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_symbol);
        }

        private RestorationObjectiveToken Target(string occurrenceId) => new RestorationObjectiveToken
        {
            kind = RestorationTokenKind.Target,
            occurrenceId = occurrenceId,
            target = new SymbolValueReference { symbol = _symbol },
        };

        private static RestorationObjectiveToken Literal(string text) => new RestorationObjectiveToken
        {
            kind = RestorationTokenKind.Literal,
            literalText = text,
        };

        private static RestorationObjectiveDefinition Definition(params RestorationObjectiveUnit[] units)
            => new RestorationObjectiveDefinition
            {
                oneWordAtATime = true,
                units = new List<RestorationObjectiveUnit>(units),
            };

        private static RestorationObjectiveUnit Unit(string id, params RestorationObjectiveToken[] tokens)
            => new RestorationObjectiveUnit { stableId = id, tokens = new List<RestorationObjectiveToken>(tokens) };

        [Test]
        public void Build_SplitsOnWhitespaceAndKeepsInWordLiteralsTogether()
        {
            // DALA ni Juan ANG mga KAniYANG
            RestorationWordPages pages = RestorationWordPages.Build(Definition(Unit("u1",
                Target("da"), Target("la"), Literal(" ni Juan "),
                Target("a"), Target("nga"), Literal(" mga "),
                Target("ka"), Literal("ni"), Target("ya"), Target("nga2"))));

            Assert.That(pages.PageCount, Is.EqualTo(3), "Literal-only words carry no boxes.");
            Assert.That(pages.PageOf("da"), Is.EqualTo(0));
            Assert.That(pages.PageOf("la"), Is.EqualTo(0));
            Assert.That(pages.PageOf("nga"), Is.EqualTo(1));
            Assert.That(pages.PageOf("ka"), Is.EqualTo(2));
            Assert.That(pages.PageOf("nga2"), Is.EqualTo(2),
                "KAniYANG is one word even though 'ni' sits between its targets.");
            Assert.That(pages.IndexInPage("nga2"), Is.EqualTo(2));
            Assert.That(pages.SlotCountOf(2), Is.EqualTo(3));
            Assert.That(pages.MaxSlotCount, Is.EqualTo(3));
            Assert.That(pages.PageOf("missing"), Is.EqualTo(RestorationWordPages.NoPage));
        }

        [Test]
        public void Build_AUnitBoundaryAlwaysEndsTheWord()
        {
            RestorationWordPages pages = RestorationWordPages.Build(Definition(
                Unit("u1", Target("ha"), Target("ra")),
                Unit("u2", Target("ya"))));

            Assert.That(pages.PageCount, Is.EqualTo(2));
            Assert.That(pages.PageOf("ya"), Is.EqualTo(1));
        }

        [Test]
        public void CurrentPage_IsTheFirstWordWithAnEmptyBoxAndStaysOnTheLastWhenDone()
        {
            RestorationWordPages pages = RestorationWordPages.Build(Definition(Unit("u1",
                Target("a"), Literal(" "), Target("b"), Target("c"), Literal(" "), Target("d"))));

            var restored = new HashSet<string>();
            Assert.That(pages.CurrentPage(restored.Contains), Is.EqualTo(0));

            restored.Add("a");
            Assert.That(pages.CurrentPage(restored.Contains), Is.EqualTo(1));

            restored.Add("c");
            Assert.That(pages.CurrentPage(restored.Contains), Is.EqualTo(1),
                "A word with any empty box is still the current word.");

            restored.Add("b");
            restored.Add("d");
            Assert.That(pages.CurrentPage(restored.Contains), Is.EqualTo(2));
        }

        [Test]
        public void CompletedPageCount_CountsOnlyWordsWithEveryBoxFilled()
        {
            RestorationWordPages pages = RestorationWordPages.Build(Definition(Unit("u1",
                Target("a"), Literal(" "), Target("b"), Target("c"), Literal(" "), Target("d"))));

            var restored = new HashSet<string>();
            Assert.That(pages.CompletedPageCount(restored.Contains), Is.EqualTo(0));

            restored.Add("a");
            restored.Add("b");
            Assert.That(pages.CompletedPageCount(restored.Contains), Is.EqualTo(1),
                "A half-filled word does not count as completed.");

            restored.Add("c");
            restored.Add("d");
            Assert.That(pages.CompletedPageCount(restored.Contains), Is.EqualTo(3));
        }

        [Test]
        public void OnlyLevel15OptsIntoOneWordAtATime()
        {
            for (int level = 1; level <= 15; level++)
            {
                var config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(string.Format(LevelPath, level));
                Assert.That(config, Is.Not.Null, "Level " + level + " config must load.");
                Assert.That(RestorationWordPages.IsEnabled(config.restorationObjective),
                    Is.EqualTo(level == 15),
                    "Only the Level 15 finale shows its target text one word at a time.");
            }
        }

        [Test]
        public void Level15_SplitsIntoReadableWordsThatCoverEveryTarget()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(string.Format(LevelPath, 15));
            RestorationWordPages pages = RestorationWordPages.Build(config.restorationObjective);

            int targets = 0;
            foreach (RestorationObjectiveUnit unit in config.restorationObjective.units)
            {
                foreach (RestorationObjectiveToken token in unit.tokens)
                {
                    if (token?.IsTarget != true)
                        continue;

                    targets++;
                    Assert.That(pages.PageOf(token.occurrenceId), Is.Not.EqualTo(RestorationWordPages.NoPage),
                        token.occurrenceId + " must belong to a word.");
                }
            }

            // DALA, DAMA, PAMAYAnan · HANGA, HALAGA · SANGA, HARAYA.
            Assert.That(targets, Is.EqualTo(17));
            Assert.That(pages.PageCount, Is.EqualTo(7));
            Assert.That(pages.MaxSlotCount, Is.LessThanOrEqualTo(4),
                "A single word must fit the rail at full box size.");
        }

        [Test]
        public void Director_NeverOffersALaterWordUntilTheCurrentWordIsFull()
        {
            // HANGA | NG | TAong: pages 0, 0, 1, 2 with a window wide enough to span them all.
            var slots = new List<SpawnSlot>
            {
                new SpawnSlot("symbol.ha", "u", 0, occurrenceId: "ha", pageIndex: 0),
                new SpawnSlot("symbol.nga", "u", 1, occurrenceId: "nga", pageIndex: 0),
                new SpawnSlot("symbol.nga", "u", 3, occurrenceId: "nga2", pageIndex: 1),
                new SpawnSlot("symbol.ta", "u", 5, occurrenceId: "ta", pageIndex: 2),
            };
            var policy = new SpawnAssignmentPolicy
            {
                minSpawnsBeforeNeeded = 0,
                neededWeight = 1f,
                activeSlotWindow = 4,
                choiceMomentSlotIndex = -1,
                gateFinalSlotToFinalWave = false,
            };
            var director = new SpawnAssignmentDirector(slots, policy, new AlwaysNeededRandom());
            var restored = new[] { false, false, false, false };

            var offered = new List<int>();
            for (int spawn = 0; spawn < 40 && System.Array.IndexOf(restored, false) >= 0; spawn++)
            {
                SpawnAssignment assignment = director.AssignNext(new SpawnAssignmentRequest
                {
                    RestoredSlots = restored,
                    Now = spawn,
                });
                if (assignment.Role != SpawnAssignmentRole.Needed)
                    continue;

                int currentPage = SpawnSlot.CurrentPage(slots, index => restored[index]);
                Assert.That(slots[assignment.SlotIndex].PageIndex, Is.EqualTo(currentPage),
                    "A needed spawn must target the word on screen, never a later one.");
                offered.Add(assignment.SlotIndex);
                restored[assignment.SlotIndex] = true;
            }

            Assert.That(restored, Is.All.True, "The paged objective must still be completable.");
            Assert.That(offered.IndexOf(2), Is.GreaterThan(offered.IndexOf(0)));
            Assert.That(offered.IndexOf(2), Is.GreaterThan(offered.IndexOf(1)));
            Assert.That(offered.IndexOf(3), Is.GreaterThan(offered.IndexOf(2)));
        }

        [Test]
        public void UnpagedSlots_KeepTheWholeWindowOpen()
        {
            var slots = new List<SpawnSlot>
            {
                new SpawnSlot("symbol.ha", "u", 0, occurrenceId: "ha"),
                new SpawnSlot("symbol.ta", "u", 2, occurrenceId: "ta"),
            };

            Assert.That(SpawnSlot.CurrentPage(slots, _ => false), Is.EqualTo(RestorationWordPages.NoPage));
            Assert.That(slots[1].IsBeyondPage(RestorationWordPages.NoPage), Is.False);
        }

        private sealed class AlwaysNeededRandom : ISpawnRandom
        {
            public double NextDouble() => 0d;
            public int NextInt(int exclusiveMax) => 0;
        }
    }
}
