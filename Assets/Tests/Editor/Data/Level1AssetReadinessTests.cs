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
    /// pronunciation, tracing animations, context art) are human follow-ups and
    /// deliberately not asserted.
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
                    $"{symbol.stableId} needs its enemy glyph sprite.");
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

            foreach (SymbolValueReference reference in level.cumulativeSymbolPool)
            {
                // The readable-visual guarantee cannot depend on audio that is
                // not recorded yet; AudioManager.PlayPronunciation null-guards.
                Assert.IsTrue(ClueChannelResolver.HasReadableVisual(resolved)
                    || reference.symbol.pronunciationClip != null,
                    $"{reference.symbol.stableId} would be unplayable without audio.");
            }
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
