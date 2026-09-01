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

        [Test]
        public void ShippedDaSymbol_CarriesBothDaAndRaReadings()
        {
            BaybayinCharacterSO da = LoadCatalog().FirstOrDefault(s => s.characterID == "DA");
            Assert.IsNotNull(da, "DA is not in the campaign catalog.");

            List<string> ids = da.spokenValues.Select(v => v.stableId).ToList();
            CollectionAssert.Contains(ids, "value.da", "DA lost its primary reading.");
            CollectionAssert.Contains(ids, "value.ra",
                "DA lost value.ra. HARAYA (SALIN-155) cannot be authored without it, and no other " +
                "level would notice it was gone.");
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

        [Test]
        public void ResolveSyllable_Ra_ResolvesToTheDaGlyphWithItsSecondReading()
        {
            List<BaybayinCharacterSO> catalog = LoadCatalog();

            Assert.IsTrue(CampaignLevelDataTool.TryResolveSyllable(
                catalog, "RA", out BaybayinCharacterSO ra, out string value),
                "RA must resolve even though it is not a taught symbol.");
            Assert.AreEqual("DA", ra.characterID,
                "RA is a reading of the DA glyph, not a symbol of its own.");
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

            CollectionAssert.AreEqual(
                new[] { "HA/value.ha", "DA/value.ra", "YA/value.ya" }, resolved);
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
