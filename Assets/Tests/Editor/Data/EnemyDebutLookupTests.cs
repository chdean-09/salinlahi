using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// The debut rule: a type's debut level is the first level in campaign order whose wave
    /// roster spawns it. That level introduces the type on every attempt; no later level does.
    /// </summary>
    [TestFixture]
    public class EnemyDebutLookupTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        private EnemyDataSO Enemy(string id)
        {
            EnemyDataSO d = Make<EnemyDataSO>();
            d.enemyID = id;
            d.displayName = id;
            return d;
        }

        private LevelConfigSO Level(int number, string stableId, params EnemyDataSO[] types)
        {
            LevelConfigSO l = Make<LevelConfigSO>();
            l.levelNumber = number;
            l.stableId = stableId;
            l.waves = new List<WaveDefinition>
            {
                new WaveDefinition { enemyTypes = new List<EnemyDataSO>(types) },
            };
            return l;
        }

        private EraConfigSO Era(int order, params LevelConfigSO[] levels)
        {
            EraConfigSO e = Make<EraConfigSO>();
            e.order = order;
            e.levels = new List<LevelConfigSO>(levels);
            return e;
        }

        private CampaignConfigSO Campaign(params EraConfigSO[] eras)
        {
            CampaignConfigSO c = Make<CampaignConfigSO>();
            c.eras = new List<EraConfigSO>(eras);
            return c;
        }

        [Test]
        public void ATypeDebutsOnTheFirstLevelThatSpawnsIt_AndOnNoLaterLevel()
        {
            EnemyDataSO iligaw = Enemy("iligaw");
            EnemyDataSO bakod = Enemy("bakod");
            LevelConfigSO l1 = Level(1, "level.1", iligaw);
            LevelConfigSO l2 = Level(2, "level.2", iligaw, bakod);
            CampaignConfigSO campaign = Campaign(Era(1, l1, l2));

            Assert.IsTrue(EnemyDebutLookup.DebutsOn(campaign, l1, iligaw), "Iligaw debuts on 1");
            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, l2, iligaw),
                "Iligaw was met on 1, so Level 2 must not re-introduce him");
            Assert.IsTrue(EnemyDebutLookup.DebutsOn(campaign, l2, bakod), "Bakod debuts on 2");
            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, l1, bakod));
        }

        [Test]
        public void CampaignOrderFollowsEraOrderThenLevelNumber_NotListOrder()
        {
            EnemyDataSO hati = Enemy("hati");
            LevelConfigSO l6 = Level(6, "level.6", hati);
            LevelConfigSO l2 = Level(2, "level.2", hati);
            // Authored out of order on purpose: era 2 listed first, and Level 6 before Level 2.
            CampaignConfigSO campaign = Campaign(Era(2, l6), Era(1, l6, l2));

            Assert.AreSame(l2, EnemyDebutLookup.FindDebutLevel(campaign, hati));
            Assert.IsTrue(EnemyDebutLookup.DebutsOn(campaign, l2, hati));
            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, l6, hati));
        }

        [Test]
        public void MatchesByEnemyIdAsWellAsByReference()
        {
            // A pooled or domain-reloaded instance is a different object with the same ID.
            EnemyDataSO authored = Enemy("mantsa");
            EnemyDataSO runtimeCopy = Enemy("MANTSA");
            LevelConfigSO l1 = Level(1, "level.1", authored);
            CampaignConfigSO campaign = Campaign(Era(1, l1));

            Assert.IsTrue(EnemyDebutLookup.DebutsOn(campaign, l1, runtimeCopy));
        }

        [Test]
        public void MatchesTheLevelByStableIdWhenTheReferenceDiffers()
        {
            EnemyDataSO abo = Enemy("abo");
            LevelConfigSO authored = Level(1, "level.1", abo);
            LevelConfigSO reloaded = Level(1, "level.1", abo);
            CampaignConfigSO campaign = Campaign(Era(1, authored));

            Assert.IsTrue(EnemyDebutLookup.DebutsOn(campaign, reloaded, abo));
        }

        [Test]
        public void FailsClosed_WithNoCampaign_AnUnlistedLevel_OrAnUnspawnedType()
        {
            EnemyDataSO abo = Enemy("abo");
            EnemyDataSO never = Enemy("never-spawns");
            LevelConfigSO l1 = Level(1, "level.1", abo);
            LevelConfigSO unlisted = Level(9, "level.9", abo);
            CampaignConfigSO campaign = Campaign(Era(1, l1));

            Assert.IsFalse(EnemyDebutLookup.DebutsOn(null, l1, abo), "no campaign");
            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, unlisted, abo),
                "a level the campaign does not list cannot be anyone's debut");
            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, l1, never), "never spawned");
            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, null, abo));
            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, l1, null));
        }

        [Test]
        public void DecoysAndSuppressedTypesNeverDebut()
        {
            EnemyDataSO decoy = Enemy("iligaw-decoy");
            decoy.isDecoy = true;
            LevelConfigSO l1 = Level(1, "level.1", decoy);
            CampaignConfigSO campaign = Campaign(Era(1, l1));

            Assert.IsFalse(EnemyDebutLookup.DebutsOn(campaign, l1, decoy),
                "the roster the debut is derived from is the introducible roster");
        }
    }
}
