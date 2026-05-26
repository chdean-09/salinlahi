using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class EnemyBadgeFinalDrawOnDefeatTest
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

        // Regression: enemies without deathFrames (e.g. Kisha, Kempei, Maestro,
        // Shokan) used to call Enemy.ReturnToPool synchronously after
        // PlayFinalDraw, which deactivated the badge and stopped the coroutine
        // before the animation could render. Now the no-death-frames path waits
        // for IsPlayingFinalDraw to complete before returning to the pool.
        [UnityTest]
        public IEnumerator Defeat_WithNoDeathFrames_PlaysBadgeFinalDrawToCompletion()
        {
            yield return null;

            BaybayinCharacterSO character = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "BA", GlyphBadgePlayModeTestHelpers.CreateSprite(Color.green));
            _objectsToDestroy.Add(character);

            GlyphBadgeConfigSO config = GlyphBadgePlayModeTestHelpers.CreateBadgeConfig();
            config.finalDrawChargeDuration = 0.1f;
            config.finalDrawReleaseDuration = 0.2f;
            _objectsToDestroy.Add(config);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "no_death_frames_test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.assignedCharacter = character;
            // Critically: deathFrames left null/empty.
            _objectsToDestroy.Add(data);

            Enemy enemy = CreateEnemyShell();
            (EnemyGlyphBadge badge, _) =
                GlyphBadgePlayModeTestHelpers.AddGlyphBadgeChild(enemy.gameObject, config);
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();
            badge.Refresh();

            enemy.Defeat();

            Assert.IsTrue(enemy.IsDying,
                "Defeat must mark enemy dying before the badge final-draw runs.");
            Assert.IsTrue(enemy.gameObject.activeSelf,
                "Enemy must stay active until the badge final-draw completes.");
            yield return null;
            Assert.IsTrue(badge.IsPlayingFinalDraw,
                "Badge final-draw coroutine must be running after Defeat with no death frames.");

            // Sample mid-animation: still active, still playing.
            float midPoint = config.finalDrawChargeDuration * 0.5f;
            yield return new WaitForSeconds(midPoint);
            Assert.IsTrue(enemy.gameObject.activeSelf,
                "Enemy must remain active while the badge final-draw is mid-flight.");
            Assert.IsTrue(badge.IsPlayingFinalDraw,
                "Badge final-draw should still be in flight mid-animation.");

            // Wait for completion (with a small buffer for the per-frame yield).
            float remaining = (config.finalDrawChargeDuration + config.finalDrawReleaseDuration) - midPoint + 0.1f;
            yield return new WaitForSeconds(remaining);

            Assert.IsFalse(badge.IsPlayingFinalDraw,
                "Badge final-draw should complete and clear its coroutine handle.");
            Assert.IsFalse(enemy.gameObject.activeSelf,
                "Enemy should be returned to the pool after the badge final-draw completes.");
        }

        private Enemy CreateEnemyShell()
        {
            GameObject go = new GameObject("Enemy_NoDeathFrames_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            GlyphBadgePlayModeTestHelpers.DisableDebugLabels(enemy);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            return enemy;
        }
    }
}
