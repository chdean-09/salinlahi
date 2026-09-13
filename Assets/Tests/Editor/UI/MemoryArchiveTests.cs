using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-240 — the Memory Archive derivation.
    ///
    /// <see cref="MemoryArchiveModel"/> is pure and static precisely so this fixture can
    /// exercise the whole of it with in-memory ScriptableObject fixtures: no scene, no
    /// SaveManager, no Library dependency. The MonoBehaviours on top of it are thin, so
    /// almost all of the ticket's logic is under test here.
    ///
    /// NO SYMBOL STABLE ID IS HARD-CODED ANYWHERE IN THIS FILE. D-025 is renaming
    /// symbol.dara -> symbol.da on a concurrent branch; symbols are referenced as objects,
    /// which that rename does not touch.
    /// </summary>
    [TestFixture]
    public sealed class MemoryArchiveTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _created)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            _created.Clear();
        }

        // ----- era and level ordering -------------------------------------------------

        [Test]
        public void Build_GroupsEntriesByEraInAuthoredOrder()
        {
            CampaignConfigSO campaign = Campaign(
                Era("Pamana", 3, Level(11, 1, "Level 11")),
                Era("Ugat", 1, Level(1, 1, "Ang Unang Tinig")),
                Era("Ugnayan", 2, Level(6, 1, "Level 6")));

            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.Build(campaign, null);

            Assert.AreEqual(
                new[] { "Ugat", "Ugnayan", "Pamana" },
                new[] { entries[0].EraName, entries[1].EraName, entries[2].EraName },
                "The archive must render eras by EraConfigSO.order, not by their position in "
                + "the campaign's list.");
        }

        [Test]
        public void Build_OrdersLevelsByEraLocalOrderWithinAnEra()
        {
            CampaignConfigSO campaign = Campaign(
                Era("Ugat", 1,
                    Level(3, 3, "Third"),
                    Level(1, 1, "First"),
                    Level(2, 2, "Second")));

            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.Build(campaign, null);

            Assert.AreEqual(
                new[] { 1, 2, 3 },
                new[] { entries[0].EraLocalOrder, entries[1].EraLocalOrder, entries[2].EraLocalOrder },
                "EraLocalOrder is the collectible number on the card, so an out-of-order "
                + "archive would number the cards wrongly.");
        }

        // ----- memory id extraction ---------------------------------------------------

        [Test]
        public void ResolveMemoryId_ReturnsTheMemoryReward()
        {
            LevelConfigSO level = Level(1, 1, "Ang Unang Tinig");
            level.rewardIds = new List<string> { "memory.ugat.01" };

            Assert.AreEqual("memory.ugat.01", MemoryArchiveModel.ResolveMemoryId(level));
        }

        [Test]
        public void ResolveMemoryId_IgnoresRewardIdsThatAreNotMemories()
        {
            LevelConfigSO level = Level(1, 1, "Ang Unang Tinig");
            level.rewardIds = new List<string> { "badge.ugat.01", "memory.ugat.01" };

            Assert.AreEqual(
                "memory.ugat.01",
                MemoryArchiveModel.ResolveMemoryId(level),
                "Only ids carrying LevelRewardResolver.MemoryRewardPrefix are memory grants. "
                + "Reading the first reward id of any kind would open a card for a badge.");
        }

        [Test]
        public void ResolveMemoryId_ReturnsNullWhenTheLevelGrantsNoMemory()
        {
            LevelConfigSO level = Level(6, 1, "Mula Awa sa Gawa");
            level.rewardIds = new List<string>();

            Assert.IsNull(
                MemoryArchiveModel.ResolveMemoryId(level),
                "Levels 6-15 ship rewardIds: [] under D-015. That must read as 'no memory', "
                + "not as an error.");
        }

        // ----- unlocked / locked resolution -------------------------------------------

        [Test]
        public void Build_MarksAClaimedMemoryUnlocked()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugat", 1, AuthoredLevel1()));

            IReadOnlyList<MemoryArchiveEntry> entries =
                MemoryArchiveModel.Build(campaign, new[] { "memory.ugat.01" });

            Assert.IsTrue(entries[0].IsUnlocked);
        }

        [Test]
        public void Build_LeavesAnUnclaimedMemoryLocked()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugat", 1, AuthoredLevel1()));

            IReadOnlyList<MemoryArchiveEntry> entries =
                MemoryArchiveModel.Build(campaign, new[] { "memory.ugat.05" });

            Assert.IsFalse(
                entries[0].IsUnlocked,
                "A memory belonging to a different level must not unlock this slot.");
        }

        [Test]
        public void Build_TreatsANullUnlockedListAsEverythingLocked()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugat", 1, AuthoredLevel1()));

            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.Build(campaign, null);

            Assert.IsFalse(
                entries[0].IsUnlocked,
                "A null unlocked list is the uninitialised-save case. It must degrade to "
                + "all-locked rather than throwing on a player's screen.");
        }

        // ----- Levels 6-15, which are silhouettes BY DESIGN (D-015) -------------------

        [Test]
        public void Build_StillProducesAnEntryForALevelThatGrantsNoMemory()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugnayan", 2, Level(6, 1, "Mula Awa sa Gawa")));

            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.Build(campaign, null);

            Assert.AreEqual(
                1,
                entries.Count,
                "The archive enumerates LEVELS, not memory ids. Keying on ids would drop "
                + "every Level 6-15 slot and AC-8 requires one for each of them.");
            Assert.AreEqual(6, entries[0].LevelNumber);
        }

        [Test]
        public void Build_MarksALevelWithoutAuthoredMemoryContentAsUnauthored()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugnayan", 2, Level(6, 1, "Mula Awa sa Gawa")));

            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.Build(campaign, null);

            Assert.IsFalse(
                entries[0].HasAuthoredContent,
                "HasAuthoredContent == false is the silhouette state and is CORRECT for "
                + "Levels 6-15 under D-015. It is not a defect and must not be 'fixed' by "
                + "inventing content.");
        }

        // ----- the empty archive, a first-class state ----------------------------------

        [Test]
        public void Build_WithNothingUnlockedStillRendersEverySlot()
        {
            CampaignConfigSO campaign = Campaign(
                Era("Ugat", 1, AuthoredLevel1(), Level(2, 2, "Mga Mata ng Bata")),
                Era("Ugnayan", 2, Level(6, 1, "Mula Awa sa Gawa")));

            IReadOnlyList<MemoryArchiveEntry> entries =
                MemoryArchiveModel.Build(campaign, new string[0]);

            Assert.AreEqual(3, entries.Count);
            foreach (MemoryArchiveEntry entry in entries)
                Assert.IsFalse(entry.IsUnlocked);
        }

        [Test]
        public void Build_WithNothingUnlockedDoesNotThrow()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugat", 1, AuthoredLevel1()));

            Assert.DoesNotThrow(
                () => MemoryArchiveModel.Build(campaign, new string[0]),
                "A fresh save is the state most players open the archive in first.");
        }

        // ----- degradation --------------------------------------------------------------

        [Test]
        public void Build_WithANullCampaignReturnsAnEmptyArchiveRatherThanThrowing()
        {
            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.Build(null, null);

            Assert.IsNotNull(entries);
            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public void Build_SkipsNullErasAndNullLevels()
        {
            EraConfigSO era = Era("Ugat", 1, AuthoredLevel1());
            era.levels.Add(null);
            CampaignConfigSO campaign = Campaign(era);
            campaign.eras.Add(null);

            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.Build(campaign, null);

            Assert.AreEqual(
                1,
                entries.Count,
                "An unassigned slot in a campaign list must be skipped, not turned into a "
                + "null entry the UI then dereferences.");
        }

        // ----- card fields derived from authored content (AC-3, AC-4, AC-5) -------------

        [Test]
        public void BuildEntry_DerivesTheTargetWordsAndTheirMeanings()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugat", 1, AuthoredLevel1()));

            MemoryArchiveEntry entry = MemoryArchiveModel.Build(campaign, null)[0];

            Assert.AreEqual(2, entry.Words.Count);
            Assert.AreEqual("INA", entry.Words[0].Label);
            Assert.AreEqual("mother", entry.Words[0].Meaning);
            Assert.AreEqual("AMA", entry.Words[1].Label);
            Assert.AreEqual("father", entry.Words[1].Meaning);
            Assert.AreEqual(
                2,
                entry.Words[0].Symbols.Count,
                "AC-4's Baybayin forms are reached through the decomposition's symbol "
                + "references, which carry glyphOutlineSprite.");
        }

        [Test]
        public void BuildEntry_JoinsTheMemoryCutscenePanelsIntoLore()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugat", 1, AuthoredLevel1()));

            MemoryArchiveEntry entry = MemoryArchiveModel.Build(campaign, null)[0];

            Assert.AreEqual(
                "Panel one." + MemoryArchiveModel.LoreParagraphSeparator
                + "Panel two." + MemoryArchiveModel.LoreParagraphSeparator + "Panel three.",
                entry.Lore,
                "The lore is the restored-memory cutscene's authored panel text, reproduced "
                + "verbatim. This ticket drafts no narrative copy.");
            Assert.IsTrue(entry.HasAuthoredContent);
        }

        // ----- SALIN-258: the locked row names the level era-relatively -------------------

        /// <summary>
        /// The archive's one player-facing level number. Global level 7 is Ugnayan's second
        /// level, so the row must read "Earn in Ugnayan Level 2" and must not contain "7".
        ///
        /// Asserted through MemoryArchiveController.LockedRowLabel — the SAME expression
        /// BuildEntryRow renders — rather than recomposing the label in the test. A test that
        /// rebuilt the string itself would keep passing even if the controller stopped using it.
        ///
        /// An Ugat fixture would be worthless here: in Era 1 the era-local and global numbers
        /// are equal for all five levels, so it would pass against the old code too.
        /// </summary>
        [Test]
        public void LockedRow_NamesTheLevelEraRelatively_SALIN258()
        {
            // Level 7 carries no rewardIds and no memory cutscene, so it renders locked — which
            // is the D-015 state every Level 6-15 row is genuinely in.
            CampaignConfigSO campaign = Campaign(Era("Ugnayan", 2, Level(7, 2, "Sama-Samang Lakas")));

            MemoryArchiveEntry entry = MemoryArchiveModel.Build(campaign, null)[0];
            string label = MemoryArchiveController.LockedRowLabel(entry);

            Assert.IsFalse(entry.HasAuthoredContent, "precondition: this row renders locked");
            StringAssert.Contains("Earn in Ugnayan Level 2", label);
            StringAssert.DoesNotContain("7", label,
                "Global level 7 must never appear; it is Ugnayan Level 2 to the player.");
        }

        /// <summary>
        /// The degraded path: an entry whose era name is missing must still name something,
        /// rather than rendering "Earn in " with a blank where the level used to be.
        /// </summary>
        [Test]
        public void LockedRow_WithNoEraNameResolved_StillNamesTheLevel_SALIN258()
        {
            CampaignConfigSO campaign = Campaign(Era(null, 2, Level(7, 2, "Unresolved")));

            MemoryArchiveEntry entry = MemoryArchiveModel.Build(campaign, null)[0];
            string label = MemoryArchiveController.LockedRowLabel(entry);

            StringAssert.Contains("Earn in Level 7", label,
                "With no era name, the copy degrades to the plain form rather than going blank.");
        }

        // ----- SALIN-253: the per-era slice -----------------------------------------------

        /// <summary>
        /// BuildForEra must return that era's entries and ONLY that era's, in local order.
        ///
        /// The fixture deliberately puts a second era in the campaign and authors the target
        /// era's levels out of order, so an implementation that returned the whole campaign, or
        /// that returned the list unsorted, fails here rather than on a real save months later.
        /// </summary>
        [Test]
        public void BuildForEra_ReturnsExactlyTheErasEntriesInLocalOrder()
        {
            EraConfigSO ugat = Era("Ugat", 1,
                Level(3, 3, "Third"),
                Level(1, 1, "First"),
                Level(2, 2, "Second"));
            Era("Ugnayan", 2, Level(6, 1, "Not This One"));

            IReadOnlyList<MemoryArchiveEntry> entries = MemoryArchiveModel.BuildForEra(ugat, null);

            Assert.AreEqual(3, entries.Count, "Only Ugat's own levels belong on Ugat's screen.");
            Assert.AreEqual(
                new[] { "First", "Second", "Third" },
                new[] { entries[0].Title, entries[1].Title, entries[2].Title },
                "Entries must come back in authored eraLocalOrder, not in list order.");
        }

        [Test]
        public void BuildForEra_NullEra_ReturnsEmpty()
        {
            Assert.AreEqual(
                0,
                MemoryArchiveModel.BuildForEra(null, null).Count,
                "A null era returns an empty list rather than throwing: the caller is on the "
                + "results path, where an exception would take the Results screen down with it.");
        }

        // ----- fixtures -----------------------------------------------------------------

        /// <summary>
        /// Shaped like the shipped Level 1: two focus words with meanings, each decomposing
        /// into two symbols, a memory reward id, and a three-panel memory cutscene.
        /// </summary>
        private LevelConfigSO AuthoredLevel1()
        {
            LevelConfigSO level = Level(1, 1, "Ang Unang Tinig");
            level.rewardIds = new List<string> { "memory.ugat.01" };
            level.focusWords = new List<FocusWordDefinition>
            {
                FocusWord("INA", "mother"),
                FocusWord("AMA", "father")
            };
            level.contextMedia = new ContentMediaReferences
            {
                cutscene = Cutscene("Panel one.", "Panel two.", "Panel three.")
            };
            return level;
        }

        private FocusWordDefinition FocusWord(string label, string meaning) =>
            new()
            {
                displayLabel = label,
                meaning = meaning,
                decomposition = new List<SymbolValueReference>
                {
                    new() { symbol = Symbol() },
                    new() { symbol = Symbol() }
                }
            };

        private BaybayinCharacterSO Symbol() => Track(ScriptableObject.CreateInstance<BaybayinCharacterSO>());

        private CutsceneSO Cutscene(params string[] panelTexts)
        {
            CutsceneSO cutscene = Track(ScriptableObject.CreateInstance<CutsceneSO>());
            cutscene.panels = new CutscenePanel[panelTexts.Length];
            for (int i = 0; i < panelTexts.Length; i++)
                cutscene.panels[i] = new CutscenePanel { text = panelTexts[i] };
            return cutscene;
        }

        private LevelConfigSO Level(int levelNumber, int eraLocalOrder, string levelName)
        {
            LevelConfigSO level = Track(ScriptableObject.CreateInstance<LevelConfigSO>());
            level.levelNumber = levelNumber;
            level.eraLocalOrder = eraLocalOrder;
            level.levelName = levelName;
            level.stableId = "level.test." + levelNumber;
            level.rewardIds = new List<string>();
            level.focusWords = new List<FocusWordDefinition>();
            level.contextMedia = new ContentMediaReferences();
            return level;
        }

        private EraConfigSO Era(string eraName, int order, params LevelConfigSO[] levels)
        {
            EraConfigSO era = Track(ScriptableObject.CreateInstance<EraConfigSO>());
            era.eraName = eraName;
            era.order = order;
            era.levels = new List<LevelConfigSO>(levels);
            return era;
        }

        private CampaignConfigSO Campaign(params EraConfigSO[] eras)
        {
            CampaignConfigSO campaign = Track(ScriptableObject.CreateInstance<CampaignConfigSO>());
            campaign.eras = new List<EraConfigSO>(eras);
            return campaign;
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }
    }
}
