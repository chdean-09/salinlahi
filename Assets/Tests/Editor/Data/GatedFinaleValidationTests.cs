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
        public void LevelGatingWithNoWaves_IsRejected()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.activeClueCombatEnabled = true;
            level.spawnAssignmentPolicy.gateFinalSlotToFinalWave = true;

            // Isolate the no-waves branch: the fixture level already authors two slots (one
            // decomposition symbol per focus word), so the slot-count branch must stay quiet here.
            int slotCount = level.focusWords
                .Where(focus => focus?.decomposition != null)
                .SelectMany(focus => focus.decomposition)
                .Count(reference => reference?.symbol != null);
            Assert.GreaterOrEqual(slotCount, 2,
                "precondition: this test isolates the no-waves branch, so the slot-count branch " +
                "must not also be able to fire.");

            // level.waves prefers a non-empty authored list, then falls back to expanding
            // waveCurve; a stray waveCurve would silently defeat the zero-waves precondition below.
            level.waveCurve = null;
            level.waves = new List<WaveDefinition>();
            Assert.AreEqual(0, level.waves.Count,
                "precondition: the level must genuinely have no waves, not just an empty authored " +
                "list masking a waveCurve fallback.");

            List<string> offenders = Offenders(fixture.Campaign);
            Assert.IsTrue(offenders.Any(o => o.Contains("authors no waves")),
                "a level that gates its final slot but authors no waves can never open the gate, " +
                "and must be rejected with the no-waves message rather than only the slot-count one.\n"
                + string.Join("\n", offenders));
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
