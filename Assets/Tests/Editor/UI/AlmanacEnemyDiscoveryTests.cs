using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class AlmanacEnemyDiscoveryTests
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
        public void IsDiscovered_BeforeEnemyIsMarked_ReturnsFalse()
        {
            EnemyDataSO data = CreateEnemyData("soldado");

            Assert.IsFalse(AlmanacEnemyDiscovery.IsDiscovered(data));

            Object.DestroyImmediate(data);
        }

        [Test]
        public void IsDiscovered_AfterEnemyIsMarked_ReturnsTrue()
        {
            EnemyDataSO data = CreateEnemyData("  FRAILE  ");

            EnemyDiscoveryProgress.TryMarkDiscovered(data, out _);

            Assert.IsTrue(AlmanacEnemyDiscovery.IsDiscovered(data));
            Object.DestroyImmediate(data);
        }

        [Test]
        public void IsDiscovered_WithNullOrBlankEnemy_DoesNotUnlock()
        {
            EnemyDataSO blank = CreateEnemyData(" ");

            Assert.IsFalse(AlmanacEnemyDiscovery.IsDiscovered(null));
            Assert.IsFalse(AlmanacEnemyDiscovery.IsDiscovered(blank));

            Object.DestroyImmediate(blank);
        }

        private static EnemyDataSO CreateEnemyData(string enemyID)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            return data;
        }
    }
}
