using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    public class CampaignLookupTests
    {
        [Test]
        public void LevelConfig_NewSchemaCollectionsAreNonNullAndFocusWordsAreInline()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            try
            {
                level.levelNumber = 1;
                level.stableId = "level.ugat.01";
                level.eraLocalOrder = 1;
                level.focusWords.Add(new FocusWordDefinition { stableId = "level.ugat.01.focus.01" });
                level.focusWords.Add(new FocusWordDefinition { stableId = "level.ugat.01.focus.02" });

                Assert.AreEqual(1, level.levelNumber);
                Assert.AreEqual(2, level.focusWords.Count);
                Assert.IsNotNull(level.cumulativeSymbolPool);
                Assert.IsNotNull(level.learningRequirements);
                Assert.IsNotNull(level.practiceRequirements);
                Assert.IsNotNull(level.rewardIds);
                Assert.IsNotNull(level.masteryRequirements);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void EraConfig_RetainsPresentationFieldsAlongsideStableIdentity()
        {
            EraConfigSO era = ScriptableObject.CreateInstance<EraConfigSO>();
            try
            {
                era.eraName = "Ugat";
                era.stableId = "era.ugat";
                era.order = 1;

                Assert.AreEqual("Ugat", era.eraName);
                Assert.IsNull(era.backgroundSprite);
                Assert.IsNull(era.bannerSprite);
                Assert.IsNotNull(era.levels);
                Assert.AreEqual("era.ugat", era.stableId);
                Assert.AreEqual(1, era.order);
            }
            finally
            {
                Object.DestroyImmediate(era);
            }
        }

        [Test]
        public void CampaignConfig_LookupsUseStableIdsAndRejectUnknownIds()
        {
            CampaignConfigSO campaign = CreateLookupCampaign();
            try
            {
                Assert.IsTrue(campaign.TryGetEra("era.ugat", out EraConfigSO ugat));
                Assert.AreEqual("Era renamed for display", ugat.eraName);
                Assert.IsTrue(campaign.TryGetLevel("level.ugat.01", out LevelConfigSO level));
                Assert.AreEqual(99, level.levelNumber);
                Assert.IsTrue(campaign.TryGetSymbol("symbol.dara", out BaybayinCharacterSO dara));
                Assert.AreEqual("DA / RA", dara.syllable);
                Assert.IsTrue(campaign.TryGetSpokenValue(
                    "symbol.dara", "value.ra", out SpokenValueDefinition ra));
                Assert.AreEqual("RA", ra.displayValue);

                Assert.IsFalse(campaign.TryGetEra("era.unknown", out _));
                Assert.IsFalse(campaign.TryGetLevel("level.unknown.01", out _));
                Assert.IsFalse(campaign.TryGetSymbol("symbol.unknown", out _));
                Assert.IsFalse(campaign.TryGetSpokenValue("symbol.dara", "value.unknown", out _));
            }
            finally
            {
                DestroyCampaign(campaign);
            }
        }

        [Test]
        public void CampaignConfig_LookupsRejectDuplicateIds()
        {
            CampaignConfigSO campaign = CreateLookupCampaign();
            EraConfigSO duplicateEra = ScriptableObject.CreateInstance<EraConfigSO>();
            LevelConfigSO duplicateLevel = ScriptableObject.CreateInstance<LevelConfigSO>();
            BaybayinCharacterSO duplicateSymbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            try
            {
                duplicateEra.stableId = "era.ugat";
                duplicateLevel.stableId = "level.ugat.01";
                duplicateSymbol.stableId = "symbol.dara";
                duplicateSymbol.spokenValues = new List<SpokenValueDefinition>
                {
                    new() { stableId = "value.ra" },
                };
                campaign.eras.Add(duplicateEra);
                campaign.eras[0].levels.Add(duplicateLevel);
                campaign.symbols.Add(duplicateSymbol);

                Assert.IsFalse(campaign.TryGetEra("era.ugat", out _));
                Assert.IsFalse(campaign.TryGetLevel("level.ugat.01", out _));
                Assert.IsFalse(campaign.TryGetSymbol("symbol.dara", out _));
                Assert.IsFalse(campaign.TryGetSpokenValue("symbol.dara", "value.ra", out _));
            }
            finally
            {
                DestroyCampaign(campaign);
                Object.DestroyImmediate(duplicateEra);
                Object.DestroyImmediate(duplicateLevel);
                Object.DestroyImmediate(duplicateSymbol);
            }
        }

        private static CampaignConfigSO CreateLookupCampaign()
        {
            CampaignConfigSO campaign = ScriptableObject.CreateInstance<CampaignConfigSO>();
            EraConfigSO era = ScriptableObject.CreateInstance<EraConfigSO>();
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();

            campaign.manifest = CampaignIdentityManifest.CreateRevisedV1();
            campaign.eras = new List<EraConfigSO> { era };
            campaign.symbols = new List<BaybayinCharacterSO> { symbol };
            era.stableId = "era.ugat";
            era.eraName = "Era renamed for display";
            era.levels = new List<LevelConfigSO> { level };
            level.stableId = "level.ugat.01";
            level.levelNumber = 99;
            symbol.stableId = "symbol.dara";
            symbol.syllable = "DA / RA";
            symbol.spokenValues = new List<SpokenValueDefinition>
            {
                new() { stableId = "value.da", displayValue = "DA" },
                new() { stableId = "value.ra", displayValue = "RA" },
            };
            return campaign;
        }

        private static void DestroyCampaign(CampaignConfigSO campaign)
        {
            if (campaign == null)
                return;

            HashSet<Object> destroyed = new();
            if (campaign.eras != null)
            {
                for (int i = 0; i < campaign.eras.Count; i++)
                {
                    EraConfigSO era = campaign.eras[i];
                    if (era == null || !destroyed.Add(era))
                        continue;

                    if (era.levels != null)
                    {
                        for (int j = 0; j < era.levels.Count; j++)
                        {
                            LevelConfigSO level = era.levels[j];
                            if (level != null && destroyed.Add(level))
                                Object.DestroyImmediate(level);
                        }
                    }

                    Object.DestroyImmediate(era);
                }
            }

            if (campaign.symbols != null)
            {
                for (int i = 0; i < campaign.symbols.Count; i++)
                {
                    BaybayinCharacterSO symbol = campaign.symbols[i];
                    if (symbol != null && destroyed.Add(symbol))
                        Object.DestroyImmediate(symbol);
                }
            }

            Object.DestroyImmediate(campaign);
        }
    }
}
