using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class SpanishEnemyVariantDataTests
    {
        [Test]
        public void Guardia_HasExpectedFastSpeedAndSpanishEra()
        {
            EnemyDataSO soldado = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Soldado.asset");
            EnemyDataSO guardia = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Guardia.asset");

            Assert.NotNull(soldado);
            Assert.NotNull(guardia);
            Assert.AreEqual("guardia", guardia.enemyID);
            Assert.AreEqual(Era.Spanish, guardia.era);
            Assert.AreEqual(1, guardia.maxHealth);
            Assert.That(guardia.moveSpeed, Is.EqualTo(soldado.moveSpeed * 1.5f).Within(0.001f));
        }

        [Test]
        public void Capitan_HasExpectedShieldedAndSlowStats()
        {
            EnemyDataSO soldado = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Soldado.asset");
            EnemyDataSO capitan = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Capitan.asset");

            Assert.NotNull(soldado);
            Assert.NotNull(capitan);
            Assert.AreEqual("capitan", capitan.enemyID);
            Assert.AreEqual(Era.Spanish, capitan.era);
            Assert.AreEqual(2, capitan.maxHealth);
            Assert.That(capitan.moveSpeed, Is.EqualTo(soldado.moveSpeed * 0.7f).Within(0.001f));
            Assert.IsTrue(capitan.useHurtFeedback);
        }
    }
}