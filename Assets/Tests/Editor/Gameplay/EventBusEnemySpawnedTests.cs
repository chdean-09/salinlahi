using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EventBusEnemySpawnedTests
    {
        [Test]
        public void RaiseEnemySpawned_InvokesSubscribersWithEnemyPayload()
        {
            GameObject go = new("Enemy");
            go.AddComponent<BoxCollider2D>();
            Enemy enemy = go.AddComponent<Enemy>();
            Enemy received = null;

            void Handler(Enemy payload) => received = payload;
            EventBus.OnEnemySpawned += Handler;

            try
            {
                EventBus.RaiseEnemySpawned(enemy);
                Assert.AreSame(enemy, received);
            }
            finally
            {
                EventBus.OnEnemySpawned -= Handler;
                Object.DestroyImmediate(go);
            }
        }
    }
}
