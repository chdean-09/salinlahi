using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class FrailePhaserBadgeFadeTest
    {
        private readonly List<Object> _objectsToDestroy = new();
        private float _previousTimeScale = 1f;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _previousTimeScale;
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator FadeOut_MatchesEnemyAndBadgeAlpha()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return null;

            BaybayinCharacterSO character = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "BA", GlyphBadgePlayModeTestHelpers.CreateSprite(Color.green));
            _objectsToDestroy.Add(character);

            GlyphBadgeConfigSO config = GlyphBadgePlayModeTestHelpers.CreateBadgeConfig();
            _objectsToDestroy.Add(config);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "fraile_badge_fade_test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.assignedCharacter = character;
            data.isPhaser = true;
            data.phaserInterval = 0.02f;
            data.phaserFadeOutDuration = 0.18f;
            data.phaserFadeOutPulseCount = 4;
            data.phaserFadeOutPulseAmplitude = 1f;
            _objectsToDestroy.Add(data);

            Enemy enemy = CreateFraileShell();
            (EnemyGlyphBadge badge, SpriteRenderer badgeRenderer) =
                GlyphBadgePlayModeTestHelpers.AddGlyphBadgeChild(enemy.gameObject, config);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer enemyRenderer = enemy.GetComponent<SpriteRenderer>();
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();
            badge.Refresh();

            yield return GlyphBadgePlayModeTestHelpers.WaitUntilOrTimeout(
                () => enemyRenderer.color.a < 0.95f && phaser.IsVisible,
                timeoutSeconds: 0.8f);

            Assert.Less(enemyRenderer.color.a, 0.95f,
                "Enemy sprite should be mid fade-out before sampling alphas.");

            float midpointAlpha = (enemyRenderer.color.a + badgeRenderer.color.a) * 0.5f;
            Assert.AreEqual(enemyRenderer.color.a, badgeRenderer.color.a, 0.1f,
                "Badge alpha should track the enemy body alpha during Phaser fade-out.");
            Assert.Less(midpointAlpha, 0.95f, "Both renderers should be partially faded.");
        }

        private Enemy CreateFraileShell()
        {
            GameObject go = new GameObject("Enemy_Fraile_Badge_Fade_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.AddComponent<PhaserEnemy>();
            GlyphBadgePlayModeTestHelpers.DisableDebugLabels(enemy);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            return enemy;
        }
    }
}
