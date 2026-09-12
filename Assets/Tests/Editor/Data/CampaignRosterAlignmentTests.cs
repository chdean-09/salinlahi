using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// SALIN-216 (audit T04): every level's combat roster must equal its cumulative symbol pool,
    /// which is the set of symbols taught at or before that level. Before this ticket, Levels 6-9
    /// and 11-14 carried untaught glyphs, Level 10's roster was [A], Level 15's was [NGA], and
    /// Level 13's cumulativeSymbolPool was an empty list.
    ///
    /// Scoped deliberately to COMBAT_ROSTER_INVALID and CUMULATIVE_POOL_INVALID. The authored
    /// campaign still reports unrelated Errors (media, Level 13 requirements, severities) owned by
    /// SALIN-215 / SALIN-217 / SALIN-222, so asserting a clean validation outright would fail for
    /// reasons outside this ticket.
    /// </summary>
    [TestFixture]
    public sealed class CampaignRosterAlignmentTests
    {
        private const string CampaignAssetPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        /// <summary>The ticket's own acceptance list for Level 6, spelled out rather than derived.</summary>
        private static readonly string[] LevelSixSymbols =
        {
            "symbol.a", "symbol.ei", "symbol.ba", "symbol.ma",
            "symbol.na", "symbol.ta", "symbol.ga", "symbol.wa",
        };

        private static CampaignConfigSO LoadCampaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignAssetPath}.");
            return campaign;
        }

        [Test]
        public void EveryLevel_CombatRosterAndCumulativePool_MatchTheTaughtSymbols()
        {
            CampaignConfigSO campaign = LoadCampaign();

            List<string> offenders = CampaignConfigValidator.Validate(campaign)
                .Where(issue => issue.Code == ContentValidationCode.CombatRosterInvalid
                             || issue.Code == ContentValidationCode.CumulativePoolInvalid)
                .Select(issue => $"{issue.Code} @ {issue.Path}: {issue.Message}")
                .ToList();

            Assert.IsEmpty(offenders,
                "SALIN-216: every level's roster and pool must equal its taught symbols.\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void LevelSix_CombatRoster_IsExactlyTheEightTaughtSymbols()
        {
            CampaignConfigSO campaign = LoadCampaign();
            Assert.IsTrue(campaign.TryGetLevel("level.ugnayan.01", out LevelConfigSO level));

            CollectionAssert.AreEquivalent(
                LevelSixSymbols,
                level.allowedCharacters.Select(symbol => symbol.stableId).ToArray(),
                "SALIN-216 acceptance: Level 6 may only ask A, E/I, BA, MA, NA, TA, GA, WA.");
        }

        [Test]
        public void LevelSix_EveryWave_NamesItsGlyphsInsteadOfFallingBackToEnemyData()
        {
            CampaignConfigSO campaign = LoadCampaign();
            Assert.IsTrue(campaign.TryGetLevel("level.ugnayan.01", out LevelConfigSO level));

            var roster = new HashSet<string>(level.allowedCharacters.Select(s => s.stableId));

            for (int i = 0; i < level.waves.Count; i++)
            {
                WaveDefinition wave = level.waves[i];

                // An empty list sends WaveSpawner.SelectCharacterForSpawn to
                // EnemyDataSO.assignedCharacter, which no roster gates — that is the path that
                // let Level 6 demand KA, SA, HA, LA, NGA and PA from its corruption enemies.
                Assert.IsNotEmpty(wave.characters,
                    $"SALIN-216: Level 6 wave {i + 1} must name its glyphs explicitly.");

                foreach (BaybayinCharacterSO symbol in wave.characters)
                {
                    Assert.IsNotNull(symbol, $"Level 6 wave {i + 1} has a null character entry.");
                    Assert.Contains(symbol.stableId, roster.ToList(),
                        $"Level 6 wave {i + 1} asks {symbol.stableId}, which Level 6 has not taught.");
                }
            }
        }

        [Test]
        public void EveryLevelSixToFifteenWave_StaysInsideItsOwnRoster()
        {
            CampaignConfigSO campaign = LoadCampaign();

            for (int globalIndex = 5; globalIndex < ContentIdentity.RevisedLevelIds.Count; globalIndex++)
            {
                string levelId = ContentIdentity.RevisedLevelIds[globalIndex];
                Assert.IsTrue(campaign.TryGetLevel(levelId, out LevelConfigSO level), levelId);

                if (level.waves == null)
                    continue;

                var roster = new HashSet<string>(
                    level.allowedCharacters.Where(s => s != null).Select(s => s.stableId));

                for (int i = 0; i < level.waves.Count; i++)
                {
                    foreach (BaybayinCharacterSO symbol in level.waves[i].characters)
                    {
                        Assert.IsTrue(symbol != null && roster.Contains(symbol.stableId),
                            $"SALIN-216: {levelId} wave {i + 1} references a glyph outside its roster. " +
                            "LevelConfigSO.OnValidate would prune it silently at the next import.");
                    }
                }
            }
        }
    }
}
