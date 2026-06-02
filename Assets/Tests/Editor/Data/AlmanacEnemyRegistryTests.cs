using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    [TestFixture]
    public class AlmanacEnemyRegistryTests
    {
        [Test]
        public void IsBoss_TrueOnlyWhenBossConfigSet()
        {
            EnemyDataSO enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            BossConfigSO boss = ScriptableObject.CreateInstance<BossConfigSO>();
            try
            {
                Assert.IsFalse(new AlmanacEnemyEntry { enemyData = enemy }.IsBoss);
                Assert.IsTrue(new AlmanacEnemyEntry { enemyData = enemy, bossConfig = boss }.IsBoss);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(boss);
            }
        }

        [Test]
        public void RegularEntry_ResolvesFromEnemyData()
        {
            EnemyDataSO enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.displayName = "Soldado";
            enemy.description = "A foot soldier.";
            enemy.portraitSprite = MakeSprite();
            try
            {
                var entry = new AlmanacEnemyEntry { enemyData = enemy };
                Assert.AreEqual("Soldado", entry.ResolveDisplayName());
                Assert.AreEqual("A foot soldier.", entry.ResolveDescription());
                Assert.AreSame(enemy.portraitSprite, entry.ResolvePortrait());
            }
            finally { Object.DestroyImmediate(enemy); }
        }

        [Test]
        public void RegularEntry_PortraitFallsBackToFirstWalkFrame_WhenPortraitNull()
        {
            EnemyDataSO enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            Sprite frame0 = MakeSprite();
            enemy.portraitSprite = null;
            enemy.walkFrames = new[] { frame0, MakeSprite() };
            try
            {
                var entry = new AlmanacEnemyEntry { enemyData = enemy };
                Assert.AreSame(frame0, entry.ResolvePortrait());
            }
            finally { Object.DestroyImmediate(enemy); }
        }

        [Test]
        public void BossEntry_ResolvesFromBossConfig()
        {
            BossConfigSO boss = ScriptableObject.CreateInstance<BossConfigSO>();
            boss.bossName = "El Inquisidor";
            boss.description = "The first boss.";
            boss.bossSprite = MakeSprite();
            EnemyDataSO bossEnemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                var entry = new AlmanacEnemyEntry { enemyData = bossEnemy, bossConfig = boss };
                Assert.AreEqual("El Inquisidor", entry.ResolveDisplayName());
                Assert.AreEqual("The first boss.", entry.ResolveDescription());
                Assert.AreSame(boss.bossSprite, entry.ResolvePortrait());
            }
            finally
            {
                Object.DestroyImmediate(boss);
                Object.DestroyImmediate(bossEnemy);
            }
        }

        private static Sprite MakeSprite()
        {
            var tex = new Texture2D(2, 2);
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
    }
}
