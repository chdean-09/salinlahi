using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyDiscoveryEventTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private int _eventCount;
        private EnemyDataSO _lastData;
        private Enemy _lastEnemy;

        [SetUp]
        public void SetUp()
        {
            EnemyDiscoveryProgress.ResetForTests();
            _eventCount = 0;
            _lastData = null;
            _lastEnemy = null;
            EventBus.OnEnemyDiscovered += HandleEnemyDiscovered;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnEnemyDiscovered -= HandleEnemyDiscovered;
            EnemyDiscoveryProgress.ResetForTests();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void Initialize_WithNewEnemyID_MarksDiscoveredAndRaisesOnce()
        {
            Enemy enemy = CreateEnemy();
            EnemyDataSO data = CreateEnemyData("soldado");

            Assert.IsTrue(enemy.Initialize(data));

            Assert.AreEqual(1, _eventCount);
            Assert.AreSame(data, _lastData);
            Assert.AreSame(enemy, _lastEnemy);
            Assert.IsTrue(EnemyDiscoveryProgress.HasDiscovered(data));
        }

        [Test]
        public void Initialize_WithRepeatedEnemyID_DoesNotRaiseAgain()
        {
            Enemy firstEnemy = CreateEnemy();
            Enemy secondEnemy = CreateEnemy();
            EnemyDataSO firstData = CreateEnemyData("soldado");
            EnemyDataSO secondData = CreateEnemyData("SOLDADO");

            Assert.IsTrue(firstEnemy.Initialize(firstData));
            Assert.IsTrue(secondEnemy.Initialize(secondData));

            Assert.AreEqual(1, _eventCount);
            Assert.AreSame(firstEnemy, _lastEnemy);
        }

        [Test]
        public void Initialize_WithBlankEnemyID_DoesNotRaise()
        {
            Enemy enemy = CreateEnemy();
            EnemyDataSO data = CreateEnemyData(" ");

            Assert.IsTrue(enemy.Initialize(data));

            Assert.AreEqual(0, _eventCount);
            Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(data));
        }

        [Test]
        public void Initialize_WithNullEnemyData_DoesNotRaise()
        {
            Enemy enemy = CreateEnemy();

            LogAssert.Expect(LogType.Error, "[Salinlahi] Enemy.Initialize: EnemyDataSO is null.");

            Assert.IsFalse(enemy.Initialize(null));
            Assert.AreEqual(0, _eventCount);
        }

        [Test]
        public void Initialize_WithBossEnemy_DoesNotMarkOrRaise()
        {
            Enemy bossEnemy = CreateBossEnemy();
            EnemyDataSO data = CreateEnemyData("elinquisidor");

            Assert.IsTrue(bossEnemy.Initialize(data));

            Assert.AreEqual(0, _eventCount);
            Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(data));
        }

        private void HandleEnemyDiscovered(EnemyDataSO data, Enemy enemy)
        {
            _eventCount++;
            _lastData = data;
            _lastEnemy = enemy;
        }

        private Enemy CreateEnemy()
        {
            GameObject go = new GameObject("Enemy_Discovery_Event_Test");
            _objectsToDestroy.Add(go);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            return enemy;
        }

        private Enemy CreateBossEnemy()
        {
            GameObject go = new GameObject("BossEnemy_Discovery_Event_Test");
            _objectsToDestroy.Add(go);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<BossEnemy>();
            return enemy;
        }

        private EnemyDataSO CreateEnemyData(string enemyID)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            _objectsToDestroy.Add(data);
            return data;
        }
    }
}
