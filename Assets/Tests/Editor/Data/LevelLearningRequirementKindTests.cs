using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// SALIN-224 — the symbol learning cards must teach only the symbols a level introduces.
    ///
    /// <see cref="SymbolLearningCardController"/> presents one card per <c>Instruction</c>-kind entry
    /// in <c>learningRequirements</c>, and the authored data used to list each level's whole
    /// cumulative pool as Instruction, so the player re-watched every previously taught card at every
    /// level. The rule these tests pin is:
    ///
    ///   an entry is Instruction iff its symbol's <c>firstIntroductionLevelId</c> equals that level's
    ///   <c>stableId</c>; every other entry is Practice.
    ///
    /// Review symbols stay in the list as Practice rather than being removed — an empty
    /// <c>learningRequirements</c> is a validator error (CampaignConfigValidator.ValidateRequirementList).
    ///
    /// Deliberately derived, never enumerated: every assertion below is expressed against
    /// <c>campaign.symbols</c> and each symbol's own <c>firstIntroductionLevelId</c>. No symbol count
    /// and no symbol list is hard-coded, so the suite holds as the campaign's roster changes — it
    /// satisfied the 17-symbol model and satisfies the 18 that SALIN-217 [T05] authored without
    /// edit. It is the guard rail that fails if a newly added symbol is never instructed anywhere;
    /// symbol.ra is exempt only because its introduction level (Level 13) still has no authored
    /// learning list, which is SALIN-250's gap.
    ///
    /// These tests read the authored assets in place. They neither run a bootstrap nor write anything.
    /// </summary>
    [TestFixture]
    public sealed class LevelLearningRequirementKindTests
    {
        private static CampaignConfigSO LoadCampaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
                RevisedCampaignBootstrap.CampaignAssetPath);

            Assert.IsNotNull(campaign,
                $"Expected the campaign asset at {RevisedCampaignBootstrap.CampaignAssetPath}.");
            return campaign;
        }

        /// <summary>Every level in the campaign, in era then level order.</summary>
        private static List<LevelConfigSO> AllLevels(CampaignConfigSO campaign)
        {
            var levels = new List<LevelConfigSO>();
            foreach (EraConfigSO era in campaign.eras ?? new List<EraConfigSO>())
            {
                if (era?.levels == null) continue;
                levels.AddRange(era.levels.Where(level => level != null));
            }

            Assert.IsNotEmpty(levels, "Expected the campaign to resolve at least one level.");
            return levels;
        }

        /// <summary>
        /// Levels that actually carry a learning list. Level 13 ships with
        /// <c>learningRequirements: []</c> — a pre-existing authoring hole owned by SALIN-250 [T38],
        /// not by this ticket — so it is skipped rather than allowed to redden this suite over
        /// someone else's gap.
        /// </summary>
        private static List<LevelConfigSO> AuthoredLevels(CampaignConfigSO campaign)
        {
            return AllLevels(campaign)
                .Where(level => level.learningRequirements != null && level.learningRequirements.Count > 0)
                .ToList();
        }

        private static IEnumerable<ContentRequirement> EntriesOfKind(LevelConfigSO level,
                                                                    ContentRequirementKind kind)
        {
            return level.learningRequirements.Where(r => r != null && r.kind == kind);
        }

        private static string SymbolIdOf(ContentRequirement requirement)
        {
            return requirement.symbolValue?.symbol?.stableId;
        }

        [Test]
        public void EveryInstructionEntry_IntroducesASymbolAtThisLevel()
        {
            CampaignConfigSO campaign = LoadCampaign();

            foreach (LevelConfigSO level in AuthoredLevels(campaign))
            {
                foreach (ContentRequirement entry in EntriesOfKind(level, ContentRequirementKind.Instruction))
                {
                    BaybayinCharacterSO symbol = entry.symbolValue?.symbol;
                    Assert.IsNotNull(symbol,
                        $"{level.stableId}: an Instruction learning entry has no symbol.");

                    Assert.AreEqual(level.stableId, symbol.firstIntroductionLevelId,
                        $"{level.stableId} instructs {symbol.characterID}, but that symbol is first " +
                        $"introduced at '{symbol.firstIntroductionLevelId}'. A level may only teach " +
                        "the symbols it introduces; everything else is review.");
                }
            }
        }

        [Test]
        public void EveryReviewSymbol_IsPracticeKind()
        {
            CampaignConfigSO campaign = LoadCampaign();

            foreach (LevelConfigSO level in AuthoredLevels(campaign))
            {
                foreach (ContentRequirement entry in level.learningRequirements)
                {
                    BaybayinCharacterSO symbol = entry?.symbolValue?.symbol;
                    if (symbol == null || symbol.firstIntroductionLevelId == level.stableId)
                        continue;

                    Assert.AreEqual(ContentRequirementKind.Practice, entry.kind,
                        $"{level.stableId}: {symbol.characterID} was introduced at " +
                        $"'{symbol.firstIntroductionLevelId}', so its learning entry must be Practice " +
                        "review, not a card the player has already watched.");
                }
            }
        }

        [Test]
        public void EverySymbol_IsInstructedExactlyOnceAcrossTheCampaign()
        {
            CampaignConfigSO campaign = LoadCampaign();

            List<LevelConfigSO> authored = AuthoredLevels(campaign);

            var instructedAt = new Dictionary<string, List<string>>();
            foreach (LevelConfigSO level in authored)
            {
                foreach (ContentRequirement entry in EntriesOfKind(level, ContentRequirementKind.Instruction))
                {
                    string symbolId = SymbolIdOf(entry);
                    if (symbolId == null) continue;

                    if (!instructedAt.TryGetValue(symbolId, out List<string> levels))
                        instructedAt[symbolId] = levels = new List<string>();
                    levels.Add(level.stableId);
                }
            }

            // Asserted against the campaign's own roster, never a literal count.
            Assert.IsNotNull(campaign.symbols, "Expected the campaign to carry a symbol roster.");
            foreach (BaybayinCharacterSO symbol in campaign.symbols.Where(s => s != null))
            {
                bool introducedInAnAuthoredLevel =
                    !string.IsNullOrEmpty(symbol.firstIntroductionLevelId)
                    && authored.Any(l => l.stableId == symbol.firstIntroductionLevelId);

                // A symbol whose introduction level has no authored learning list yet (Level 13, per
                // SALIN-250) cannot be instructed anywhere, and that gap is not this ticket's.
                if (!introducedInAnAuthoredLevel)
                    continue;

                Assert.IsTrue(instructedAt.TryGetValue(symbol.stableId, out List<string> levels),
                    $"{symbol.characterID} ({symbol.stableId}) is never instructed anywhere in the " +
                    $"campaign, but it is introduced at '{symbol.firstIntroductionLevelId}'.");

                Assert.AreEqual(1, levels.Count,
                    $"{symbol.characterID} is instructed at {levels.Count} levels " +
                    $"({string.Join(", ", levels)}); a symbol is taught exactly once.");

                Assert.AreEqual(symbol.firstIntroductionLevelId, levels[0],
                    $"{symbol.characterID} is instructed at '{levels[0]}' but introduced at " +
                    $"'{symbol.firstIntroductionLevelId}'.");
            }
        }

        [Test]
        public void Level2_InstructsBaAndTaOnly()
        {
            CampaignConfigSO campaign = LoadCampaign();

            Assert.IsTrue(campaign.TryGetLevel("level.ugat.02", out LevelConfigSO level),
                "Expected level.ugat.02 to resolve.");

            string[] instructed = EntriesOfKind(level, ContentRequirementKind.Instruction)
                .Select(SymbolIdOf)
                .OrderBy(id => id)
                .ToArray();

            // AC1 — Level 2 shows BA and TA cards only.
            CollectionAssert.AreEqual(new[] { "symbol.ba", "symbol.ta" }, instructed,
                "Level 2 must teach exactly the two symbols it introduces, BA and TA.");
        }

        [Test]
        public void Level2_CombatDiscoverySuppressesReferenceCards_WithoutChangingInstructionSemantics()
        {
            CampaignConfigSO campaign = LoadCampaign();

            Assert.IsTrue(campaign.TryGetLevel("level.ugat.02", out LevelConfigSO level),
                "Expected level.ugat.02 to resolve.");

            Assert.IsTrue(level.suppressSymbolLearningCards,
                "Level 2 uses combat discovery and must not show reference-form symbol cards.");
            Assert.IsFalse(SymbolLearningCardController.HasPresentableRequirement(level),
                "Suppressing Level 2 reference cards must make the SymbolLearning presentation skip.");
            Assert.IsFalse(LevelPhasePlan.FromConfig(level).Has(LevelPhase.SymbolLearning),
                "A suppressed card list must not create an empty SymbolLearning phase.");

            Assert.AreEqual(2,
                EntriesOfKind(level, ContentRequirementKind.Instruction).Count(),
                "BA and TA remain Instruction entries for semantic learning/progression data.");
        }

        [Test]
        public void Level3_InstructsNothing()
        {
            CampaignConfigSO campaign = LoadCampaign();

            Assert.IsTrue(campaign.TryGetLevel("level.ugat.03", out LevelConfigSO level),
                "Expected level.ugat.03 to resolve.");

            // AC2 — Level 3 introduces no symbol, so it shows no card and goes straight to practice.
            CollectionAssert.IsEmpty(
                EntriesOfKind(level, ContentRequirementKind.Instruction).ToArray(),
                "Level 3 introduces no new symbol, so it must present no learning card.");

            Assert.IsFalse(SymbolLearningCardController.HasPresentableRequirement(level),
                "Level 3 has no presentable Instruction entry, so the SymbolLearning phase must " +
                "complete without taking drawing suppression.");
        }

        [Test]
        public void EveryAuthoredLevel_KeepsANonEmptyLearningRequirementList()
        {
            CampaignConfigSO campaign = LoadCampaign();

            // AC3 (plan review R3) — review symbols are reclassified, never removed, so the list
            // stays non-empty for CampaignConfigValidator.ValidateRequirementList.
            //
            // Level 13 (level.pamana.03) is the one level excluded: its learningRequirements has been
            // empty since before this ticket and is owned by SALIN-250 [T38], which authors that
            // level's focus words, pool, and requirements. The skip is deliberate, not an oversight.
            List<LevelConfigSO> empty = AllLevels(campaign)
                .Where(level => level.stableId != "level.pamana.03")
                .Where(level => level.learningRequirements == null || level.learningRequirements.Count == 0)
                .ToList();

            CollectionAssert.IsEmpty(empty.Select(l => l.stableId).ToArray(),
                "Every authored level must keep a non-empty learningRequirements list — review " +
                "symbols become Practice entries rather than being dropped.");
        }
    }
}
