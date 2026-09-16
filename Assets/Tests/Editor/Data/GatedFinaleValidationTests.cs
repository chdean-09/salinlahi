using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    [TestFixture]
    public sealed class GatedFinaleValidationTests
    {
        private static List<string> Offenders(CampaignConfigSO campaign)
        {
            return CampaignConfigValidator.Validate(campaign)
                .Where(i => i.Code == ContentValidationCode.GatedFinaleUnwinnable)
                .Select(i => $"{i.Code} @ {i.Path}: {i.Message}")
                .ToList();
        }

        [Test]
        public void LevelGatingItsOnlySlot_IsRejected()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.activeClueCombatEnabled = true;
            level.spawnAssignmentPolicy.gateFinalSlotToFinalWave = true;

            // Collapse the level to a single slot: gating it would withhold the win condition.
            BaybayinCharacterSO only = fixture.Campaign.symbols[0];
            level.focusWords[0].decomposition = new List<SymbolValueReference>
            {
                new SymbolValueReference { symbol = only },
            };
            for (int i = level.focusWords.Count - 1; i >= 1; i--)
                level.focusWords.RemoveAt(i);

            Assert.IsNotEmpty(Offenders(fixture.Campaign),
                "a level that gates its only slot can never be completed and must not validate.");
        }

        [Test]
        public void ShippedCampaign_HasNoUnwinnableGatedFinale()
        {
            var campaign = UnityEditor.AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
                "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset");
            Assert.IsNotNull(campaign);
            Assert.IsEmpty(Offenders(campaign));
        }
    }
}
