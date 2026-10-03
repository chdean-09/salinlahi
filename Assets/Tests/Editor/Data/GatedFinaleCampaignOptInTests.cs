using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Checks finale gates and escort budgets against the shipped campaign assets. Levels 1-4
    /// use unbounded escorts so a missed draw cannot exhaust the shortened defense. Level 5
    /// retains its bounded paragraph pacing.
    /// </summary>
    [TestFixture]
    public sealed class GatedFinaleCampaignOptInTests
    {
        private const string CampaignAssetPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        private const int DefaultMaxOverflowBatches = 12;

        private static CampaignConfigSO LoadCampaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignAssetPath}.");
            return campaign;
        }

        private static LevelConfigSO GetLevel(CampaignConfigSO campaign, string stableId)
        {
            Assert.IsTrue(campaign.TryGetLevel(stableId, out LevelConfigSO level),
                $"Expected the shipped campaign to contain {stableId}.");
            return level;
        }

        [TestCase("level.ugat.02", "Level 2")]
        [TestCase("level.ugat.03", "Level 3")]
        [TestCase("level.ugat.04", "Level 4")]
        public void GatedLevel_OptsIntoFinaleGatingWithUnboundedEscorts(string stableId, string label)
        {
            CampaignConfigSO campaign = LoadCampaign();
            LevelConfigSO level = GetLevel(campaign, stableId);

            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave,
                $"{label} ({stableId}) ships with gateFinalSlotToFinalWave = false. The gated-" +
                "finale feature is scoped to Levels 2-4; with this level opted out, its finale " +
                "can complete early and the feature is inert in the shipping build even though " +
                "every synthetic-fixture test for the engine, validator and escort budget still " +
                "passes.");

            Assert.AreEqual(0, level.spawnAssignmentPolicy.maxOverflowBatches,
                $"{label} ({stableId}) must ship maxOverflowBatches = 0 (unbounded escorts, via " +
                "SpawnAssignmentPolicy.OverflowIsUnbounded) so a gated finale can never run out " +
                "of overflow batches before its final slot is restored. A bounded budget here " +
                "means the level can deadlock instead of finishing.");

            Assert.IsTrue(level.spawnAssignmentPolicy.OverflowIsUnbounded,
                $"{label} ({stableId}): maxOverflowBatches = " +
                $"{level.spawnAssignmentPolicy.maxOverflowBatches} does not read as unbounded " +
                "through OverflowIsUnbounded, which is the property the escort director actually " +
                "consults.");
        }

        /// <summary>
        /// The Level 2 defect, asserted against the SHIPPED data rather than a synthetic shape.
        ///
        /// <para>
        /// Level 2's BATA + MATA flatten to <c>[ba, ta, ma, ta]</c>. Restoration is by symbol -
        /// <c>ActiveCluePresenter.ActiveClueRestorationState.Apply</c> fills every slot matching a
        /// defeated carrier's symbol across every focus word - so a gate on a REPEATED symbol
        /// withholds nothing at all. The engine derived the gate onto the literal last slot,
        /// <c>ta@MATA</c>, so the level the feature was built for was never gated, while Levels 3
        /// and 4 (which end on a unique symbol) held and hid the defect from every per-task review.
        /// </para>
        ///
        /// <para>
        /// This test makes that return impossible silently: it re-derives the gate from the shipped
        /// asset and requires the gated symbol to occur exactly once. An authoring edit that adds a
        /// second MA to Level 2, or a regression that puts the gate back on the tail, fails here.
        /// </para>
        /// </summary>
        [TestCase("level.ugat.02", "Level 2")]
        [TestCase("level.ugat.03", "Level 3")]
        [TestCase("level.ugat.04", "Level 4")]
        public void GatedLevel_WithholdsASymbolThatOccursExactlyOnce(string stableId, string label)
        {
            CampaignConfigSO campaign = LoadCampaign();
            LevelConfigSO level = GetLevel(campaign, stableId);

            var go = new GameObject(nameof(SpawnAssignmentCoordinator));
            try
            {
                var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
                coordinator.ApplyLevel(level, null);

                IReadOnlyList<SpawnSlot> slots = coordinator.Slots;
                string gatedSymbol = null;
                int gatedIndex = -1;
                for (int index = 0; index < slots.Count; index++)
                {
                    if (slots[index].GateToken != SpawnGateRegistry.FinalWaveReached)
                        continue;

                    gatedSymbol = slots[index].SymbolStableId;
                    gatedIndex = index;
                }

                Assert.IsNotNull(gatedSymbol,
                    $"{label} ({stableId}) opts into the gated finale but no slot carries " +
                    $"{SpawnGateRegistry.FinalWaveReached}, so nothing is withheld.");

                var flattened = new List<string>(slots.Count);
                for (int index = 0; index < slots.Count; index++)
                    flattened.Add(slots[index].SymbolStableId);

                // Until 2026-09-17 this asserted the gated symbol occurred EXACTLY ONCE, because
                // restoration was by symbol and a repeated one was filled for free by another
                // slot's carrier -- the Level 2 defect. Per-slot restoration removed that, so the
                // assertion is now about POSITION: the withheld slot must be the last one, or the
                // level can be completed before reaching it.
                Assert.AreEqual(slots.Count - 1, gatedIndex,
                    $"{label} ({stableId}) gates slot {gatedIndex} of " +
                    $"[{string.Join(", ", flattened)}] on '{gatedSymbol}'. Anything but the last " +
                    "slot can be restored while later slots remain, so the level finishes without " +
                    "the gate ever mattering.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase("level.ugat.01", "Level 1")]
        public void LevelOne_UsesFinaleGateWithUnboundedEscorts(string stableId, string label)
        {
            CampaignConfigSO campaign = LoadCampaign();
            LevelConfigSO level = GetLevel(campaign, stableId);

            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave,
                $"{label} ({stableId}) must withhold its final MA occurrence until wave 3.");

            Assert.AreEqual(0, level.spawnAssignmentPolicy.maxOverflowBatches,
                $"{label} ({stableId}) needs escorts until the gated occurrence is restored.");
        }

        [Test]
        public void Level5_UsesItsAuthoredFinaleGateWithBoundedEscortBudget()
        {
            CampaignConfigSO campaign = LoadCampaign();
            LevelConfigSO level = GetLevel(campaign, "level.ugat.05");

            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave,
                "Level 5's authored mastery paragraph ends on TA and must withhold that final " +
                "occurrence until the final wave.");
            Assert.AreEqual(DefaultMaxOverflowBatches,
                level.spawnAssignmentPolicy.maxOverflowBatches,
                "Level 5 deliberately uses bounded overflow; unbounded escort behavior belongs " +
                "to Levels 2-4.");
        }
    }
}
