using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class ShokanBadgeSwapIntegrationTest
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

        [UnityTest]
        public IEnumerator HurtSwap_KeepsOriginalBadgeThenSwaps_AndShakesRoot()
        {
            yield return null;

            BaybayinCharacterSO original = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "BA", GlyphBadgePlayModeTestHelpers.CreateSprite(Color.green));
            BaybayinCharacterSO postHurt = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "KA", GlyphBadgePlayModeTestHelpers.CreateSprite(Color.red));
            _objectsToDestroy.Add(original);
            _objectsToDestroy.Add(postHurt);

            GlyphBadgeConfigSO config = GlyphBadgePlayModeTestHelpers.CreateBadgeConfig();
            _objectsToDestroy.Add(config);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "shokan_swap_test";
            data.moveSpeed = 1f;
            data.maxHealth = 2;
            data.assignedCharacter = original;
            data.useHurtFeedback = true;
            data.hurtSwapsCharacter = true;
            data.postHurtCharacter = postHurt;
            data.hurtPausesMovement = true;
            data.hurtPauseDuration = 0.5f;
            data.hurtShakesSprite = true;
            data.hurtShakeMagnitude = 0.08f;
            data.hurtShakeDuration = 0.25f;
            data.hurtShakeFrequency = 30f;
            _objectsToDestroy.Add(data);

            Enemy enemy = CreateEnemyShell();
            (EnemyGlyphBadge badge, SpriteRenderer badgeRenderer) =
                GlyphBadgePlayModeTestHelpers.AddGlyphBadgeChild(enemy.gameObject, config);
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();
            badge.Refresh();

            Vector3 positionBeforeHit = enemy.transform.position;
            enemy.TakeDamage(1);

            Assert.AreEqual(1, enemy.CurrentHealth);
            Assert.AreSame(original.badgeSprite, badgeRenderer.sprite,
                "Badge should still show the pre-swap sprite while the swap coroutine is in flight.");

            float swapDuration = config.swapOutDuration + config.swapInDuration;
            yield return new WaitForSeconds(swapDuration + 0.05f);

            Assert.AreSame(postHurt.badgeSprite, badgeRenderer.sprite,
                "Badge should show the post-hurt character after the swap animation completes.");

            bool sawShakeOffset = false;
            float shakeEnd = Time.realtimeSinceStartup + data.hurtShakeDuration + 0.05f;
            while (Time.realtimeSinceStartup < shakeEnd)
            {
                if (Vector3.Distance(enemy.transform.position, positionBeforeHit) > 0.001f)
                    sawShakeOffset = true;
                yield return null;
            }

            Assert.IsTrue(sawShakeOffset,
                "Root transform should jitter during the hurt-feedback shake window.");
        }

        private Enemy CreateEnemyShell()
        {
            GameObject go = new GameObject("Enemy_Shokan_Swap_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.AddComponent<EnemyHurtFeedback>();
            GlyphBadgePlayModeTestHelpers.DisableDebugLabels(enemy);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            return enemy;
        }
    }
}
