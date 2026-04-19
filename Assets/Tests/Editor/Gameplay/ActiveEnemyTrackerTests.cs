using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class ActiveEnemyTrackerTests
    {
        private GameObject _trackerGO;
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _trackerGO = new GameObject("ActiveEnemyTracker_Test");
            _tracker = _trackerGO.AddComponent<ActiveEnemyTracker>();
            typeof(ActiveEnemyTracker).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { _tracker });
        }

        [TearDown]
        public void TearDown()
        {
            if (_trackerGO != null)
                Object.DestroyImmediate(_trackerGO);
        }

        private Enemy CreateEnemy(string characterID, float yPosition)
        {
            var go = new GameObject("Enemy_Test");
            go.transform.position = new Vector3(0, yPosition, 0);

            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterID;

            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.assignedCharacter = character;

            var enemy = go.AddComponent<Enemy>();
            enemy.Initialize(data);

            return enemy;
        }

        private void DestroyEnemy(Enemy enemy)
        {
            if (enemy != null && enemy.gameObject != null)
                Object.DestroyImmediate(enemy.gameObject);
        }

        [Test]
        public void Register_IncrementsActiveCount()
        {
            var enemy = CreateEnemy("A", 0);
            _tracker.Register(enemy);

            Assert.AreEqual(1, _tracker.ActiveCount);

            Object.DestroyImmediate(enemy.Data);
            Object.DestroyImmediate(enemy.Character);
            DestroyEnemy(enemy);
        }

        [Test]
        public void Unregister_DecrementsActiveCount()
        {
            var enemy = CreateEnemy("A", 0);
            _tracker.Register(enemy);
            _tracker.Unregister(enemy);

            Assert.AreEqual(0, _tracker.ActiveCount);

            Object.DestroyImmediate(enemy.Data);
            Object.DestroyImmediate(enemy.Character);
            DestroyEnemy(enemy);
        }

        [Test]
        public void FindClosestToBase_WithCharacterID_ReturnsNearestEnemy()
        {
            var close = CreateEnemy("BA", -5);
            var far = CreateEnemy("BA", -1);
            _tracker.Register(close);
            _tracker.Register(far);

            Enemy result = _tracker.FindClosestToBase("BA");
            Assert.IsNotNull(result);
            Assert.AreEqual(close, result);

            Object.DestroyImmediate(close.Data);
            Object.DestroyImmediate(close.Character);
            Object.DestroyImmediate(far.Data);
            Object.DestroyImmediate(far.Character);
            DestroyEnemy(close);
            DestroyEnemy(far);
        }

        [Test]
        public void FindClosestToBase_NoMatchingCharacter_ReturnsNull()
        {
            var enemy = CreateEnemy("KA", 0);
            _tracker.Register(enemy);

            Enemy result = _tracker.FindClosestToBase("BA");
            Assert.IsNull(result);

            Object.DestroyImmediate(enemy.Data);
            Object.DestroyImmediate(enemy.Character);
            DestroyEnemy(enemy);
        }

        [Test]
        public void FindAllWithCharacter_FiltersCorrectly()
        {
            var ba1 = CreateEnemy("BA", -3);
            var ka1 = CreateEnemy("KA", -2);
            var ba2 = CreateEnemy("BA", -1);
            _tracker.Register(ba1);
            _tracker.Register(ka1);
            _tracker.Register(ba2);

            List<Enemy> result = _tracker.FindAllWithCharacter("BA");
            Assert.AreEqual(2, result.Count);

            Object.DestroyImmediate(ba1.Data);
            Object.DestroyImmediate(ba1.Character);
            Object.DestroyImmediate(ka1.Data);
            Object.DestroyImmediate(ka1.Character);
            Object.DestroyImmediate(ba2.Data);
            Object.DestroyImmediate(ba2.Character);
            DestroyEnemy(ba1);
            DestroyEnemy(ka1);
            DestroyEnemy(ba2);
        }

        [Test]
        public void IsClear_NoEnemies_ReturnsTrue()
        {
            Assert.IsTrue(_tracker.IsClear);
        }

        [Test]
        public void IsClear_WithEnemies_ReturnsFalse()
        {
            var enemy = CreateEnemy("A", 0);
            _tracker.Register(enemy);

            Assert.IsFalse(_tracker.IsClear);

            Object.DestroyImmediate(enemy.Data);
            Object.DestroyImmediate(enemy.Character);
            DestroyEnemy(enemy);
        }
    }
}