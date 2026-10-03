using System.Collections.Generic;
using NUnit.Framework;
using Salinlahi.Debug;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyDiscoveryProgressResetterTests
    {
        private GameObject _trackerGo;
        private GameObject _resetterGo;
        private GameObject _enemyGo;
        private EnemyDataSO _data;
        private EnemyDataSO _raisedData;
        private Enemy _raisedEnemy;
        private readonly List<Object> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            EnemyDiscoveryProgress.ResetForTests();
            TutorialRuntimeState.Clear();
            EventBus.OnEnemyDiscovered += HandleEnemyDiscovered;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnEnemyDiscovered -= HandleEnemyDiscovered;
            TutorialRuntimeState.Clear();
            EnemyDiscoveryProgress.ResetForTests();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void ClearEnemyDiscoveryProgress_WithActiveEnemy_ClearsProgressAndReplaysActiveEnemyWithoutMarkingDiscovered()
        {
            Enemy enemy = CreateTrackedEnemy("soldado");
            EnemyDiscoveryProgress.TryMarkDiscovered(_data, out _);
            _resetterGo = new GameObject("EnemyDiscoveryProgressResetter");
            _objectsToDestroy.Add(_resetterGo);
            EnemyDiscoveryProgressResetter resetter = _resetterGo.AddComponent<EnemyDiscoveryProgressResetter>();

            resetter.ClearEnemyDiscoveryProgress();

            Assert.AreSame(_data, _raisedData);
            Assert.AreSame(enemy, _raisedEnemy);
            Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(_data));
        }

        [Test]
        public void ClearAndReplayFirstActiveEnemyDiscovery_WithActiveEnemy_RaisesDiscoveryWithoutMarkingDiscovered()
        {
            Enemy enemy = CreateTrackedEnemy("soldado");
            EnemyDiscoveryProgress.TryMarkDiscovered(_data, out _);
            _resetterGo = new GameObject("EnemyDiscoveryProgressResetter");
            _objectsToDestroy.Add(_resetterGo);
            EnemyDiscoveryProgressResetter resetter = _resetterGo.AddComponent<EnemyDiscoveryProgressResetter>();

            resetter.ClearAndReplayFirstActiveEnemyDiscovery();

            Assert.AreSame(_data, _raisedData);
            Assert.AreSame(enemy, _raisedEnemy);
            Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(_data));
        }

        [Test]
        public void ClearEnemyDiscoveryProgress_WithMultipleActiveEnemyTypes_ReplaysEachActiveType()
        {
            Enemy first = CreateTrackedEnemy("soldado");
            EnemyDataSO secondData = ScriptableObject.CreateInstance<EnemyDataSO>();
            _objectsToDestroy.Add(secondData);
            secondData.enemyID = "fraile";
            Enemy second = CreateEnemy(secondData);
            ActiveEnemyTracker.Instance.Register(second);
            int raisedCount = 0;
            EventBus.OnEnemyDiscovered += CountDiscovery;
            EnemyDiscoveryProgress.TryMarkDiscovered(_data, out _);
            EnemyDiscoveryProgress.TryMarkDiscovered(secondData, out _);
            _resetterGo = new GameObject("EnemyDiscoveryProgressResetter");
            _objectsToDestroy.Add(_resetterGo);
            EnemyDiscoveryProgressResetter resetter = _resetterGo.AddComponent<EnemyDiscoveryProgressResetter>();

            try
            {
                resetter.ClearEnemyDiscoveryProgress();

                Assert.AreEqual(2, raisedCount);
                Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(_data));
                Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(secondData));
            }
            finally
            {
                EventBus.OnEnemyDiscovered -= CountDiscovery;
            }

            void CountDiscovery(EnemyDataSO data, Enemy enemy)
            {
                if ((data == _data && enemy == first) || (data == secondData && enemy == second))
                    raisedCount++;
            }
        }

        private Enemy CreateTrackedEnemy(string enemyID)
        {
            _trackerGo = new GameObject("ActiveEnemyTracker");
            _objectsToDestroy.Add(_trackerGo);
            ActiveEnemyTracker tracker = _trackerGo.AddComponent<ActiveEnemyTracker>();
            InvokeAwake(tracker);

            _data = ScriptableObject.CreateInstance<EnemyDataSO>();
            _objectsToDestroy.Add(_data);
            _data.enemyID = enemyID;
            Enemy enemy = CreateEnemy(_data);
            tracker.Register(enemy);
            return enemy;
        }

        private Enemy CreateEnemy(EnemyDataSO data)
        {
            GameObject enemyGo = new GameObject("Enemy");
            _objectsToDestroy.Add(enemyGo);
            if (_enemyGo == null)
                _enemyGo = enemyGo;
            enemyGo.AddComponent<BoxCollider2D>();
            enemyGo.AddComponent<EnemyMover>();
            Enemy enemy = enemyGo.AddComponent<Enemy>();
            SetPrivateField(enemy, "_data", data);
            return enemy;
        }

        private void HandleEnemyDiscovered(EnemyDataSO data, Enemy enemy)
        {
            _raisedData = data;
            _raisedEnemy = enemy;
        }

        private static void InvokeAwake(Object target)
        {
            System.Type type = target.GetType();
            while (type != null)
            {
                System.Reflection.MethodInfo method = type.GetMethod(
                    "Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(target, null);
                    return;
                }

                type = type.BaseType;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Type type = target.GetType();
            while (type != null)
            {
                System.Reflection.FieldInfo field = type.GetField(
                    fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail($"{target.GetType().Name}.{fieldName} field not found.");
        }
    }
}
