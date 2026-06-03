using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class ComboTeachBeatTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void DefeatChainTargets_AwardsComboCreditForEachEnemy()
        {
            List<Level1TutorialEnemyController> targets = new()
            {
                new Level1TutorialEnemyController(CreateEnemy()),
                new Level1TutorialEnemyController(CreateEnemy()),
                new Level1TutorialEnemyController(CreateEnemy()),
            };

            int targetedCount = 0;
            int chainStepCount = 0;
            int aoeCount = 0;
            int chainHitTargetCount = 0;
            EventBus.OnEnemyTargeted += HandleEnemyTargeted;
            EventBus.OnChainAttackStep += HandleChainStep;
            EventBus.OnAOETriggered += HandleAoeTriggered;
            EventBus.OnChainAttackHit += HandleChainHit;

            try
            {
                int defeatedCount = ComboTeachBeat.DefeatChainTargets(targets, awardComboCredit: true);

                Assert.AreEqual(3, defeatedCount);
                Assert.AreEqual(3, targetedCount);
                Assert.AreEqual(3, chainStepCount);
                Assert.AreEqual(3, aoeCount);
                Assert.AreEqual(3, chainHitTargetCount);
            }
            finally
            {
                EventBus.OnEnemyTargeted -= HandleEnemyTargeted;
                EventBus.OnChainAttackStep -= HandleChainStep;
                EventBus.OnAOETriggered -= HandleAoeTriggered;
                EventBus.OnChainAttackHit -= HandleChainHit;
            }

            void HandleEnemyTargeted(Enemy _) => targetedCount++;
            void HandleChainStep(Enemy _) => chainStepCount++;
            void HandleAoeTriggered(int count) => aoeCount = count;
            void HandleChainHit(IReadOnlyList<Enemy> enemies) => chainHitTargetCount = enemies.Count;
        }

        private Enemy CreateEnemy()
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            _objectsToDestroy.Add(data);

            GameObject go = new("ComboTeachBeat_TestEnemy");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }
    }
}
