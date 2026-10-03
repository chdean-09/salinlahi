using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Guards the contextual-reading premise the level authoring tool depends on, against the
    /// SHIPPED character assets rather than synthetic ones.
    ///
    /// SALIN-155 requires HARAYA = HA + RA + YA. RA is not a taught symbol -- DA and RA share one
    /// glyph, and RA is Char_DA's SECOND spoken value. Level 13 is the only place in the campaign
    /// where that second value is load-bearing, so nothing else would notice if it disappeared.
    /// </summary>
    public class CampaignSyllableResolutionTests
    {
        private const string CampaignPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        private static List<BaybayinCharacterSO> LoadCatalog()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignPath);
            Assert.IsNotNull(campaign, $"{CampaignPath} is missing.");
            List<BaybayinCharacterSO> symbols = campaign.symbols.Where(s => s != null).ToList();
            Assert.IsNotEmpty(symbols, "Campaign catalog has no symbols.");
            return symbols;
        }

        /// <summary>
        /// SALIN-217 (ruling Q2 / OQ-6). This test used to require Char_DA to carry value.ra,
        /// because HARAYA had no other way to spell its middle syllable. Char_RA now supplies it as
        /// a taught symbol of its own, so the shipped catalog must hold the two readings on two
        /// separate identities — and DA keeping a stray value.ra would put the campaign back at
        /// 19 spoken values across 18 symbols and trip SPOKEN_VALUE_COUNT_INVALID.
        /// </summary>
        [Test]
        public void ShippedCatalog_CarriesDaAndRaOnSeparateSymbols()
        {
            List<BaybayinCharacterSO> catalog = LoadCatalog();

            BaybayinCharacterSO da = catalog.FirstOrDefault(s => s.characterID == "DA");
            Assert.IsNotNull(da, "DA is not in the campaign catalog.");
            List<string> daIds = da.spokenValues.Select(v => v.stableId).ToList();
            CollectionAssert.Contains(daIds, "value.da", "DA lost its primary reading.");
            CollectionAssert.DoesNotContain(daIds, "value.ra",
                "value.ra belongs to symbol.ra now, not to the DA glyph.");

            BaybayinCharacterSO ra = catalog.FirstOrDefault(s => s.characterID == "RA");
            Assert.IsNotNull(ra, "RA is not in the campaign catalog.");
            CollectionAssert.Contains(ra.spokenValues.Select(v => v.stableId).ToList(), "value.ra",
                "RA lost value.ra. HARAYA (SALIN-155) cannot be authored without it.");
        }

        [Test]
        public void ResolveSyllable_PlainToken_UsesTheSymbolsOwnFirstValue()
        {
            List<BaybayinCharacterSO> catalog = LoadCatalog();

            Assert.IsTrue(CampaignLevelDataTool.TryResolveSyllable(
                catalog, "HA", out BaybayinCharacterSO ha, out string haValue));
            Assert.AreEqual("HA", ha.characterID);
            Assert.AreEqual("value.ha", haValue);
        }

        [Test]
        public void ResolveSyllable_Da_PrefersTheCharacterIdOverAnyContextualValue()
        {
            List<BaybayinCharacterSO> catalog = LoadCatalog();

            Assert.IsTrue(CampaignLevelDataTool.TryResolveSyllable(
                catalog, "DA", out BaybayinCharacterSO da, out string value));
            Assert.AreEqual("DA", da.characterID);
            Assert.AreEqual("value.da", value,
                "DA must resolve to its own first reading, not to whichever value matched first.");
        }

        /// <summary>
        /// SALIN-217 (ruling Q2 / OQ-6): RA used to resolve to Char_DA's second reading because it
        /// was not a taught symbol. It is one now, so it resolves to its own Char_RA by character
        /// id — the same path every other symbol takes.
        /// </summary>
        [Test]
        public void ResolveSyllable_Ra_ResolvesToItsOwnTaughtSymbol()
        {
            List<BaybayinCharacterSO> catalog = LoadCatalog();

            Assert.IsTrue(CampaignLevelDataTool.TryResolveSyllable(
                catalog, "RA", out BaybayinCharacterSO ra, out string value),
                "RA must resolve as a taught symbol of its own.");
            Assert.AreEqual("RA", ra.characterID,
                "RA is its own visual identity, not a reading of the DA glyph.");
            Assert.AreEqual("symbol.ra", ra.stableId);
            Assert.AreEqual("value.ra", value);
        }

        [Test]
        public void ResolveSyllable_HarayaDecomposition_ResolvesEndToEnd()
        {
            List<BaybayinCharacterSO> catalog = LoadCatalog();

            // SALIN-155 AC1, verbatim: "HARAYA is HA + RA + YA".
            var resolved = new List<string>();
            foreach (string token in new[] { "HA", "RA", "YA" })
            {
                Assert.IsTrue(CampaignLevelDataTool.TryResolveSyllable(
                    catalog, token, out BaybayinCharacterSO symbol, out string value),
                    $"HARAYA syllable '{token}' did not resolve.");
                resolved.Add($"{symbol.characterID}/{value}");
            }

            // SALIN-217: the middle syllable is Char_RA now, not Char_DA's second reading. This is
            // the resolution SALIN-155 will author Level 13's HARAYA focus word against.
            CollectionAssert.AreEqual(
                new[] { "HA/value.ha", "RA/value.ra", "YA/value.ya" }, resolved);
        }

        [Test]
        public void ResolveSyllable_UnknownToken_Fails()
        {
            List<BaybayinCharacterSO> catalog = LoadCatalog();

            Assert.IsFalse(CampaignLevelDataTool.TryResolveSyllable(
                catalog, "ZZ", out _, out _));
            Assert.IsFalse(CampaignLevelDataTool.TryResolveSyllable(
                catalog, string.Empty, out _, out _));
            Assert.IsFalse(CampaignLevelDataTool.TryResolveSyllable(
                null, "HA", out _, out _));
        }
    }
}
