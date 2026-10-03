using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Campaign
{
    /// <summary>
    /// SALIN-253 — the era-boundary predicate.
    ///
    /// <see cref="EraBoundary"/> is pure and static precisely so this fixture can exercise all
    /// of it with in-memory ScriptableObject fixtures: no scene, no SaveManager, no
    /// AssetDatabase. The MonoBehaviours on top of it are thin, so the rule that decides
    /// whether the era completion screen appears at all is fully under test here.
    ///
    /// ⚠️ READ THIS BEFORE ADDING A CASE.
    /// On the shipped campaign — three eras of exactly five levels — a correct implementation
    /// and a naive `levelNumber % 5 == 0` one AGREE ON ALL FIFTEEN LEVELS. Every test below
    /// that uses the real 5/5/5 shape therefore passes under both, and proves only that the
    /// happy path works. <see cref="IsEraFinalLevel_ReadsTheAuthoredCount_NotModuloFive"/> is
    /// the single case in this file that discriminates between them, and it is the reason the
    /// file exists. If that test is ever deleted or softened, this fixture stops defending
    /// D4 and nothing else in the project would notice.
    /// </summary>
    [TestFixture]
    public sealed class EraBoundaryTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _created)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            _created.Clear();
        }

        // ----- IsEraFinalLevel ----------------------------------------------------------

        [Test]
        public void IsEraFinalLevel_UgatLevel5_IsTrue()
        {
            LevelConfigSO level5 = Level(5, 5);
            EraConfigSO ugat = Era("Ugat", 1, Level(1, 1), Level(2, 2), Level(3, 3), Level(4, 4), level5);

            Assert.IsTrue(
                EraBoundary.IsEraFinalLevel(ugat, level5),
                "Finishing Ugat Level 5 is the completion that must open the era screen. This "
                + "is the demo's last beat under D-015.");
        }

        [Test]
        public void IsEraFinalLevel_UgatLevel4_IsFalse()
        {
            LevelConfigSO level4 = Level(4, 4);
            EraConfigSO ugat = Era("Ugat", 1, Level(1, 1), Level(2, 2), Level(3, 3), level4, Level(5, 5));

            Assert.IsFalse(
                EraBoundary.IsEraFinalLevel(ugat, level4),
                "Level 4 is mid-era. An era screen here would interrupt the run and would also "
                + "suppress the Next Level button (AC-5), stranding the player.");
        }

        [Test]
        public void IsEraFinalLevel_PamanaLevel15_IsTrue()
        {
            LevelConfigSO level15 = Level(15, 5);
            EraConfigSO pamana = Era(
                "Pamana", 3, Level(11, 1), Level(12, 2), Level(13, 3), Level(14, 4), level15);

            Assert.IsTrue(
                EraBoundary.IsEraFinalLevel(pamana, level15),
                "The campaign's final level is also an era-final level. NOTE: this case passes "
                + "under the old `currentLevel >= 15` rule too, so it is a REGRESSION GUARD, "
                + "not evidence that the era-aware logic works. See the fixture summary.");
        }

        /// <summary>
        /// ⚠️ THE DISCRIMINATING CASE. A four-level era: the era-final level is local order 4,
        /// and local order 5 does not exist in it.
        ///
        /// A `% 5` implementation gets BOTH assertions below backwards — it would call local
        /// order 4 mid-era and local order 5 final. Nothing else in this project would catch
        /// that, because the authored campaign is 5/5/5 and the two implementations agree
        /// everywhere on it. This is also why EraBoundary reads era.levels.Count rather than
        /// ContentIdentity.RevisedLevelsPerEra, which is the same constant wearing a better name.
        /// </summary>
        [Test]
        public void IsEraFinalLevel_ReadsTheAuthoredCount_NotModuloFive()
        {
            LevelConfigSO fourth = Level(4, 4);
            LevelConfigSO strayFifth = Level(5, 5);
            EraConfigSO shortEra = Era("Synthetic", 1, Level(1, 1), Level(2, 2), Level(3, 3), fourth);

            Assert.IsTrue(
                EraBoundary.IsEraFinalLevel(shortEra, fourth),
                "In a FOUR-level era, local order 4 is the last level. A `% 5` implementation "
                + "returns false here and the era screen would never appear for that era.");
            Assert.IsFalse(
                EraBoundary.IsEraFinalLevel(shortEra, strayFifth),
                "Local order 5 is past the end of a four-level era. A `% 5` implementation "
                + "returns true here and would fire the era screen on a level the era does "
                + "not contain.");
        }

        [Test]
        public void IsEraFinalLevel_NullEraOrLevel_IsFalse()
        {
            EraConfigSO ugat = Era("Ugat", 1, Level(1, 1));

            Assert.IsFalse(EraBoundary.IsEraFinalLevel(null, Level(5, 5)), "A null era is unknowable.");
            Assert.IsFalse(EraBoundary.IsEraFinalLevel(ugat, null), "A null level is unknowable.");
            Assert.IsFalse(
                EraBoundary.IsEraFinalLevel(Era("Empty", 1), Level(1, 0)),
                "An eraLocalOrder of 0 means 'not authored on this config' and must never "
                + "match an empty era, which would fire the screen on every level of it.");
        }

        // ----- NextEra ------------------------------------------------------------------

        [Test]
        public void NextEra_AfterUgat_IsUgnayan()
        {
            EraConfigSO ugat = Era("Ugat", 1, Level(1, 1));
            EraConfigSO ugnayan = Era("Ugnayan", 2, Level(6, 1));
            EraConfigSO pamana = Era("Pamana", 3, Level(11, 1));
            CampaignConfigSO campaign = Campaign(ugat, ugnayan, pamana);

            Assert.AreSame(
                ugnayan,
                EraBoundary.NextEra(campaign, ugat),
                "Enter Next Era after Ugat must open Ugnayan (AC-4).");
        }

        [Test]
        public void NextEra_AfterPamana_IsNull()
        {
            EraConfigSO ugat = Era("Ugat", 1, Level(1, 1));
            EraConfigSO ugnayan = Era("Ugnayan", 2, Level(6, 1));
            EraConfigSO pamana = Era("Pamana", 3, Level(11, 1));
            CampaignConfigSO campaign = Campaign(ugat, ugnayan, pamana);

            Assert.IsNull(
                EraBoundary.NextEra(campaign, pamana),
                "There is no era after Pamana. Null is the normal 'campaign is over' result "
                + "and is what hides the Enter Next Era control.");
            Assert.IsNull(
                EraBoundary.NextEra(null, ugat), "A null campaign degrades to null, never throws.");
        }

        // ----- IndexOfEra ---------------------------------------------------------------

        /// <summary>
        /// The index must be the POSITION in the campaign's compacted era list, because that
        /// is what LevelSelectUI.ResolveEras() produces and ShowEra(int) consumes. Deriving it
        /// from EraConfigSO.order instead would open Level Select on the wrong era whenever
        /// the two disagree — and every test that sorted by order would still be green.
        /// The fixture below makes them disagree on purpose.
        /// </summary>
        [Test]
        public void IndexOfEra_ReturnsThePositionInCampaignOrder()
        {
            EraConfigSO first = Era("First", 3, Level(1, 1));
            EraConfigSO second = Era("Second", 1, Level(6, 1));
            CampaignConfigSO campaign = Campaign(first, second);

            Assert.AreEqual(
                0, EraBoundary.IndexOfEra(campaign, first),
                "'First' sits at position 0 even though its EraConfigSO.order is 3.");
            Assert.AreEqual(
                1, EraBoundary.IndexOfEra(campaign, second),
                "'Second' sits at position 1 even though its EraConfigSO.order is 1.");
        }

        [Test]
        public void IndexOfEra_UnknownEra_ReturnsMinusOne()
        {
            CampaignConfigSO campaign = Campaign(Era("Ugat", 1, Level(1, 1)));

            Assert.AreEqual(
                EraCompletionScreenUI.NoPendingEra,
                EraBoundary.IndexOfEra(campaign, Era("Orphan", 9, Level(99, 1))),
                "An era outside the campaign must not resolve to index 0, which would silently "
                + "send the player to Ugat.");
            Assert.AreEqual(-1, EraBoundary.IndexOfEra(campaign, null));
            Assert.AreEqual(-1, EraBoundary.IndexOfEra(null, null));
        }

        // ----- fixtures -----------------------------------------------------------------

        private LevelConfigSO Level(int levelNumber, int eraLocalOrder)
        {
            LevelConfigSO level = Track(ScriptableObject.CreateInstance<LevelConfigSO>());
            level.levelNumber = levelNumber;
            level.eraLocalOrder = eraLocalOrder;
            level.stableId = "level.test." + levelNumber;
            return level;
        }

        private EraConfigSO Era(string eraName, int order, params LevelConfigSO[] levels)
        {
            EraConfigSO era = Track(ScriptableObject.CreateInstance<EraConfigSO>());
            era.eraName = eraName;
            era.order = order;
            era.levels = new List<LevelConfigSO>(levels);
            return era;
        }

        private CampaignConfigSO Campaign(params EraConfigSO[] eras)
        {
            CampaignConfigSO campaign = Track(ScriptableObject.CreateInstance<CampaignConfigSO>());
            campaign.eras = new List<EraConfigSO>(eras);
            return campaign;
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }
    }
}
