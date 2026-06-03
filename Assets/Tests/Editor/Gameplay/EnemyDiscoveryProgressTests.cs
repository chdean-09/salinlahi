using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyDiscoveryProgressTests
    {
        [SetUp]
        public void SetUp()
        {
            EnemyDiscoveryProgress.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            EnemyDiscoveryProgress.ResetForTests();
        }

        [Test]
        public void TryMarkDiscovered_WithNewEnemyID_ReturnsTrueAndPersists()
        {
            EnemyDataSO data = CreateEnemyData("  Soldado  ");

            bool marked = EnemyDiscoveryProgress.TryMarkDiscovered(data, out string enemyID);

            Assert.IsTrue(marked);
            Assert.AreEqual("soldado", enemyID);
            Assert.IsTrue(EnemyDiscoveryProgress.HasDiscovered(data));
            Object.DestroyImmediate(data);
        }

        [Test]
        public void TryMarkDiscovered_WithRepeatedEnemyID_ReturnsFalse()
        {
            EnemyDataSO first = CreateEnemyData("soldado");
            EnemyDataSO second = CreateEnemyData("SOLDADO");

            Assert.IsTrue(EnemyDiscoveryProgress.TryMarkDiscovered(first, out _));
            Assert.IsFalse(EnemyDiscoveryProgress.TryMarkDiscovered(second, out string enemyID));
            Assert.AreEqual("soldado", enemyID);

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void ResetForTests_ClearsDiscoveryState()
        {
            EnemyDataSO data = CreateEnemyData("fraile");
            EnemyDiscoveryProgress.TryMarkDiscovered(data, out _);

            EnemyDiscoveryProgress.ResetForTests();

            Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(data));
            Object.DestroyImmediate(data);
        }

        [Test]
        public void TryMarkDiscovered_WithNullData_ReturnsFalseSafely()
        {
            Assert.IsFalse(EnemyDiscoveryProgress.TryMarkDiscovered(null, out string enemyID));
            Assert.IsNull(enemyID);
        }

        [Test]
        public void TryMarkDiscovered_WithBlankEnemyID_ReturnsFalseSafely()
        {
            EnemyDataSO data = CreateEnemyData("   ");

            Assert.IsFalse(EnemyDiscoveryProgress.TryMarkDiscovered(data, out string enemyID));
            Assert.IsNull(enemyID);

            Object.DestroyImmediate(data);
        }

        private static EnemyDataSO CreateEnemyData(string enemyID)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            return data;
        }
    }
}
