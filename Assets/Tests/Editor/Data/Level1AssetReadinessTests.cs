using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// SALIN-199: pins the Level 1 asset/fallback contract from
    /// docs/content/level-01-asset-manifest.md. Every EXISTS row a fallback
    /// depends on is asserted here so a deleted template or sprite fails loudly,
    /// and the audio-unavailable path is proven readable. MISSING rows (badges,
    /// pronunciation, tracing animations, context art) are human follow-ups; they
    /// are asserted only through the fallback that has to stand in for them.
    /// </summary>
    [TestFixture]
    public sealed class Level1AssetReadinessTests
    {
        private static LevelConfigSO LoadLevelOne(out CampaignConfigSO campaign)
        {
            RevisedCampaignBootstrap.Run();
            Level1NarrativeBootstrap.Run();
            campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
                RevisedCampaignBootstrap.CampaignAssetPath);
            Assert.IsNotNull(campaign);
            Assert.IsTrue(campaign.TryGetLevel("level.ugat.01", out LevelConfigSO level));
            return level;
        }

        [Test]
        public void ManifestDocument_ExistsAtItsStablePath()
        {
            Assert.IsTrue(File.Exists("docs/content/level-01-asset-manifest.md"),
                "The Level 1 asset manifest is the ticket's source of truth; do not move it silently.");
        }

        [Test]
        public void EveryLevelOneSymbol_HasRecognitionTemplatesAndAGlyphSprite()
        {
            LevelConfigSO level = LoadLevelOne(out _);

            foreach (SymbolValueReference reference in level.cumulativeSymbolPool)
            {
                BaybayinCharacterSO symbol = reference.symbol;
                Assert.IsNotNull(symbol);
                Assert.IsFalse(string.IsNullOrEmpty(symbol.templateFileName),
                    $"{symbol.stableId} needs a stroke-template file name for recognition.");
                Assert.IsNotNull(Resources.Load<TextAsset>($"Templates/{symbol.templateFileName}"),
                    $"{symbol.stableId}: template Resources/Templates/{symbol.templateFileName}.txt must load.");
                Assert.IsNotNull(symbol.displaySprite,
                    $"{symbol.stableId} needs its bare glyph sprite (Tracing Dojo).");
            }
        }

        [Test]
        public void ClueChannels_StayReadableWithoutPronunciationAudio()
        {
            LevelConfigSO level = LoadLevelOne(out _);

            ClueChannels resolved = ClueChannelResolver.Resolve(
                level.clueChannels, level.audioVisualFallback);
            Assert.IsTrue(ClueChannelResolver.HasReadableVisual(resolved),
                "Level 1 clues must resolve to a readable visual channel; EI/NA/A/MA "
                + "pronunciation clips do not exist yet (manifest MISSING rows).");

            // A level-wide channel bit says nothing about art, so each symbol has to
            // carry a prompt a player can actually see: its badge sprite, or the HUD's
            // Latin text. Pronunciation audio cannot rescue either (manifest MISSING rows).
            bool glyphChannel = (resolved & ClueChannels.Glyph) != ClueChannels.None;
            bool latinTextChannel = (resolved & ClueChannels.LatinText) != ClueChannels.None;

            foreach (SymbolValueReference reference in level.cumulativeSymbolPool)
            {
                BaybayinCharacterSO symbol = reference.symbol;
                bool badgeRenders = glyphChannel && symbol.badgeSprite != null;
                bool latinTextRenders = latinTextChannel
                    && !string.IsNullOrWhiteSpace(FindClueLatinSpelling(level, symbol.stableId));

                Assert.IsTrue(badgeRenders || latinTextRenders,
                    $"{symbol.stableId} would present no clue at all: badge art is missing "
                    + "and no Latin text renders for it. The badge fallback the manifest "
                    + "documents needs LatinText in clueChannels plus a focus word whose "
                    + "decomposition contains this symbol.");
            }
        }

        /// <summary>
        /// Mirrors ActiveCluePresenter.SetClueText: the HUD's Latin text is the spelling of
        /// the focus word containing the clue symbol, so a symbol outside every decomposition
        /// renders an empty string no matter which channels resolve.
        /// </summary>
        private static string FindClueLatinSpelling(LevelConfigSO level, string symbolStableId)
        {
            if (level.focusWords == null)
                return null;

            foreach (FocusWordDefinition word in level.focusWords)
            {
                if (word?.decomposition == null)
                    continue;

                foreach (SymbolValueReference reference in word.decomposition)
                {
                    if (reference?.symbol != null && reference.symbol.stableId == symbolStableId)
                        return word.latinSpelling;
                }
            }

            return null;
        }

        [Test]
        public void MemoryPresentation_RendersWithoutPanelArt()
        {
            LevelConfigSO level = LoadLevelOne(out CampaignConfigSO campaign);

            Assert.IsTrue(campaign.TryGetEra("era.ugat", out EraConfigSO era));
            Assert.IsNotNull(era.memoryReference);
            foreach (CutscenePanel panel in era.memoryReference.panels)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(panel.text),
                    "Text-only memory panels are the approved fallback until panel art lands; "
                    + "every panel must carry text.");
            }

            Assert.IsNotNull(level.focusWords[0].media.cutscene);
            Assert.IsNotNull(level.focusWords[1].media.cutscene);
        }
    }
}
