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
