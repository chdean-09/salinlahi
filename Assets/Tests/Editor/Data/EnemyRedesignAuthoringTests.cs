using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    [TestFixture]
    public sealed class EnemyRedesignAuthoringTests
    {
        private const string EnemyDir = "Assets/ScriptableObjects/Enemies/";

        private static EnemyDataSO Load(string fileName)
        {
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(EnemyDir + fileName);
            Assert.IsNotNull(data, "Missing enemy asset: " + fileName);
            return data;
        }

        [Test]
        public void CurriculumCopy_IsAuthoredForEveryPrimaryEnemy()
        {
            string[] files =
            {
                "EnemyData_AbongSimula.asset", "EnemyData_Bakod.asset",
                "EnemyData_Daan-Lihis.asset", "EnemyData_Gapos.asset",
                "EnemyData_Hati.asset", "EnemyData_Iligaw.asset",
                "EnemyData_Kadena.asset", "EnemyData_Labo.asset",
                "EnemyData_Mantsa.asset", "EnemyData_NawalangMukha.asset",
                "EnemyData_Ngatngat.asset", "EnemyData_Punit.asset",
                "EnemyData_Ragasa.asset", "EnemyData_Salungat.asset",
                "EnemyData_Takip.asset", "EnemyData_Uhaw.asset",
                "EnemyData_Walang-Awa.asset", "EnemyData_YaposngDilim.asset",
            };

            foreach (string file in files)
            {
                EnemyDataSO data = Load(file);
                Assert.IsNotEmpty(data.corruptedMeaning, data.enemyID + " needs corruptedMeaning");
                Assert.IsNotEmpty(data.trueMeaning, data.enemyID + " needs trueMeaning");
                Assert.IsNotEmpty(data.restoredLesson, data.enemyID + " needs restoredLesson");
                Assert.AreNotEqual(EnemyLearningAbility.None, data.learningAbility,
                    data.enemyID + " needs a learning ability assignment");
            }
        }

        [Test]
        public void RecommendedLearningAbilities_AreDistinctAcrossThePrimaryRoster()
        {
            var seen = new HashSet<EnemyLearningAbility>();
            string[] files =
            {
                "EnemyData_AbongSimula.asset", "EnemyData_Bakod.asset",
                "EnemyData_Daan-Lihis.asset", "EnemyData_Gapos.asset",
                "EnemyData_Hati.asset", "EnemyData_Iligaw.asset",
                "EnemyData_Kadena.asset", "EnemyData_Labo.asset",
                "EnemyData_Mantsa.asset", "EnemyData_NawalangMukha.asset",
                "EnemyData_Ngatngat.asset", "EnemyData_Punit.asset",
                "EnemyData_Ragasa.asset", "EnemyData_Salungat.asset",
                "EnemyData_Takip.asset", "EnemyData_Uhaw.asset",
                "EnemyData_Walang-Awa.asset", "EnemyData_YaposngDilim.asset",
            };

            foreach (string file in files)
                seen.Add(Load(file).learningAbility);

            Assert.GreaterOrEqual(seen.Count, 12,
                "The roster should not collapse into one generic speed or health gimmick.");
        }

        [Test]
        public void ApprovedBalanceScalars_AreAuthoredOnEnemyData()
        {
            Assert.AreEqual(2, Load("EnemyData_YaposngDilim.asset").maxHealth);
            Assert.AreEqual(2, Load("EnemyData_Walang-Awa.asset").maxHealth);

            EnemyDataSO mantsa = Load("EnemyData_Mantsa.asset");
            Assert.AreEqual(3, mantsa.scrambleFalseBurstCount);
            Assert.AreEqual(2.5f, mantsa.scrambleRadius, 0.0001f);
            Assert.AreEqual(3f, mantsa.scrambleTrueGlyphMinDwell, 0.0001f);
            Assert.AreEqual(3.6f, mantsa.scrambleTrueGlyphMaxDwell, 0.0001f);

            EnemyDataSO ragasa = Load("EnemyData_Ragasa.asset");
            Assert.AreEqual(2f, ragasa.glyphCoverInitialRevealSeconds, 0.0001f);
            Assert.AreEqual(1.25f, ragasa.glyphCoverRevealSeconds, 0.0001f);
            Assert.AreEqual(1.4f, ragasa.glyphCoverHiddenSeconds, 0.0001f);

            EnemyDataSO takip = Load("EnemyData_Takip.asset");
            Assert.AreEqual(2f, takip.glyphCoverInitialRevealSeconds, 0.0001f);
            Assert.AreEqual(1.5f, takip.glyphCoverRevealSeconds, 0.0001f);
            Assert.AreEqual(2f, takip.glyphCoverHiddenSeconds, 0.0001f);

            EnemyDataSO daanLihis = Load("EnemyData_Daan-Lihis.asset");
            Assert.AreEqual(0.9f, daanLihis.zigzagAmplitude, 0.0001f);
            Assert.AreEqual(0.5f, daanLihis.zigzagFrequency, 0.0001f);
            Assert.AreEqual(1.5f, daanLihis.moveSpeed, 0.0001f);

            Assert.AreEqual(1.6f, Load("EnemyData_Ngatngat.asset").moveSpeed, 0.0001f);

            EnemyDataSO labo = Load("EnemyData_Labo.asset");
            Assert.IsTrue(labo.isPhaser);
            Assert.AreEqual(3f, labo.phaserVisibleHoldMin, 0.0001f);
            Assert.AreEqual(4f, labo.phaserVisibleHoldMax, 0.0001f);
            Assert.AreEqual(3f, labo.phaserInitialVisibleDelayMin, 0.0001f);
            Assert.AreEqual(4f, labo.phaserInitialVisibleDelayMax, 0.0001f);
            Assert.AreEqual(0.7f, labo.phaserInvisibleHoldMin, 0.0001f);
            Assert.AreEqual(1f, labo.phaserInvisibleHoldMax, 0.0001f);

            Assert.AreEqual(2, Load("EnemyData_Kadena.asset").maxHealth);
            Assert.AreEqual(1, Load("EnemyData_Hati.asset").maxHealth);
            Assert.AreEqual(1, Load("EnemyData_HatiMinion.asset").maxHealth);
        }

        [Test]
        public void DiscoveryCopy_ExposesCurriculumFields()
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "mantsa";
            data.description = "Ink lore.";
            data.corruptedMeaning = "A mark is incomplete.";
            data.trueMeaning = "A mark carries a remembered sound.";
            data.restoredLesson = "Separate stable strokes from corruption.";

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual(data.corruptedMeaning, copy.CorruptedMeaning);
            Assert.AreEqual(data.trueMeaning, copy.TrueMeaning);
            Assert.AreEqual(data.restoredLesson, copy.RestoredLesson);
            Object.DestroyImmediate(data);
        }
    }
}
