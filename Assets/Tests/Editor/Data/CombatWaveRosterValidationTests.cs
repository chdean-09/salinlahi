using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Ugat QA 2026-09-16: Level 5 was the last Ugat level on hand-authored waves, and those waves
    /// narrowed the roster - wave 1 listed characters [EI] only, wave 3 listed [NA] only. The
    /// spawn director's filler pool honours that whitelist, so once EI was restored every filler
    /// spawn was a restored EI and the marked enemy carried a symbol no open slot wanted; and a
    /// needed MA in wave 1 found no Mantsa in the wave, so MA spawned on a Bakod.
    ///
    /// An empty wave list is the healthy shape - it means "carry the whole level roster" - so only
    /// a non-empty, narrowed list is reported. A curve-driven level such as Levels 2-5 passes on
    /// merit instead: WaveCurveExpander copies the full roster into every wave. Scoped
    /// deliberately to WAVE_ROSTER_NARROWS_RESTORATION: the authored campaign still reports
    /// unrelated issues owned by other tickets.
    /// </summary>
    [TestFixture]
    public sealed class CombatWaveRosterValidationTests
    {
        private const string CampaignAssetPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        /// <summary>
        /// Authors the level's first focus word to need exactly two distinct symbols and returns
        /// them. The shipped fixture level needs only one symbol, and one symbol cannot express
        /// narrowing - a wave carrying it carries everything - so the setup is spelled out here
        /// rather than inherited.
        /// </summary>
        private static List<BaybayinCharacterSO> RequireTwoSymbols(
            CampaignTestFixture fixture,
            LevelConfigSO level)
        {
            BaybayinCharacterSO first = fixture.Campaign.symbols[0];
            BaybayinCharacterSO second = fixture.Campaign.symbols[1];
            Assert.AreNotSame(first, second, "test setup: the campaign must author two symbols");

            level.focusWords[0].decomposition = new List<SymbolValueReference>
            {
                new SymbolValueReference { symbol = first },
                new SymbolValueReference { symbol = second },
            };

            return new List<BaybayinCharacterSO> { first, second };
        }

        private static List<string> Offenders(CampaignConfigSO campaign)
        {
            return CampaignConfigValidator.Validate(campaign)
                .Where(issue => issue.Code == ContentValidationCode.WaveRosterNarrowsRestoration)
                .Select(issue => $"{issue.Code} @ {issue.Path}: {issue.Message}")
                .ToList();
        }

        [Test]
        public void NarrowedCharacterList_OnACombatRestorationWave_IsReported()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.activeClueCombatEnabled = true;

            List<BaybayinCharacterSO> required = RequireTwoSymbols(fixture, level);

            level.waves ??= new List<WaveDefinition>();
            var wave = new WaveDefinition { enemyCount = 1, spawnInterval = 1f };
            wave.characters.Add(required[0]);   // Level 5 wave 1's shape: one glyph only.
            level.waves.Add(wave);

            Assert.IsNotEmpty(Offenders(fixture.Campaign),
                "a combat-restoration wave listing one of its level's needed symbols must be "
                + "reported: the filler pool can only draw that glyph, so once it is restored the "
                + "marked enemy carries a symbol no open slot wants");

            // Widening the wave to the full needed set clears it.
            foreach (BaybayinCharacterSO symbol in required.Skip(1))
                wave.characters.Add(symbol);

            Assert.IsEmpty(Offenders(fixture.Campaign),
                "a wave carrying every needed symbol is the healthy shape");
        }

        [Test]
        public void EmptyWaveLists_InheritTheLevelRoster_AndAreNotReported()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.activeClueCombatEnabled = true;
            RequireTwoSymbols(fixture, level);

            level.waves ??= new List<WaveDefinition>();
            level.waves.Add(new WaveDefinition { enemyCount = 1, spawnInterval = 1f });

            Assert.IsEmpty(Offenders(fixture.Campaign),
                "an empty wave list means 'carry the whole level roster' - it is what the wave "
                + "curve emits and must never be reported as narrowing");
        }

        [Test]
        public void NarrowedWave_OnALevelWithoutCombatRestoration_IsNotReported()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.activeClueCombatEnabled = false;

            List<BaybayinCharacterSO> required = RequireTwoSymbols(fixture, level);
            level.waves ??= new List<WaveDefinition>();
            var wave = new WaveDefinition { enemyCount = 1, spawnInterval = 1f };
            wave.characters.Add(required[0]);
            level.waves.Add(wave);

            Assert.IsEmpty(Offenders(fixture.Campaign),
                "a legacy-combat level does not restore its text from spawned glyphs, so a narrow "
                + "wave roster is not a defect there");
        }

        /// <summary>
        /// The regression gate for the migration itself: before Level 5 moved onto Curve_Ugat this
        /// reported all three of its authored waves.
        /// </summary>
        [Test]
        public void ShippedCampaign_HasNoCombatWaveThatNarrowsItsRestorationRoster()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignAssetPath}.");

            List<string> offenders = Offenders(campaign);

            Assert.IsEmpty(offenders,
                "Every combat-restoration wave must be able to spawn every symbol its focus text "
                + "needs. Leave a wave's characters/enemyTypes empty to inherit the level roster.\n"
                + string.Join("\n", offenders));
        }
    }
}
