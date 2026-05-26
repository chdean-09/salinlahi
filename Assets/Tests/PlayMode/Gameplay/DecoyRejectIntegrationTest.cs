using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class DecoyRejectIntegrationTest
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
        public IEnumerator ApplyDecoyPenalty_StopsMover_ShakesBadge_ReturnsToPool()
        {
            yield return null;

            BaybayinCharacterSO character = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "BA", GlyphBadgePlayModeTestHelpers.CreateSprite(Color.green));
            _objectsToDestroy.Add(character);

            const float shakeMagnitude = 0.1f;
            GlyphBadgeConfigSO config = GlyphBadgePlayModeTestHelpers.CreateBadgeConfig(
                decoyFlash: 0.1f,
                decoyShake: 0.3f,
                decoyShakeMagnitude: shakeMagnitude);
            _objectsToDestroy.Add(config);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "maestro_decoy_test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.assignedCharacter = character;
            data.isDecoy = true;
            _objectsToDestroy.Add(data);

            Enemy enemy = CreateMaestroShell();
            (EnemyGlyphBadge badge, _) =
                GlyphBadgePlayModeTestHelpers.AddGlyphBadgeChild(enemy.gameObject, config);
            EnemyMover mover = enemy.GetComponent<EnemyMover>();
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();
            badge.Refresh();
            mover.SetSpeed(data.moveSpeed);

            Vector3 baseBadgePosition = badge.transform.localPosition;
            enemy.ApplyDecoyPenalty();

            yield return null;
            Assert.IsFalse(mover.IsMoving, "Decoy reject must stop movement before the shake plays.");

            bool sawShake = false;
            float rejectEnd = Time.realtimeSinceStartup
                + config.decoyRejectFlashDuration
                + config.decoyRejectShakeDuration
                + 0.05f;
            while (Time.realtimeSinceStartup < rejectEnd)
            {
                float deviation = Mathf.Abs(badge.transform.localPosition.x - baseBadgePosition.x);
                if (deviation >= 0.5f * shakeMagnitude)
                    sawShake = true;
                yield return null;
            }

            Assert.IsTrue(sawShake, "Badge should shake horizontally during the reject window.");
            Assert.IsFalse(enemy.gameObject.activeSelf,
                "Enemy should be inactive after the reject animation completes.");
        }

        private Enemy CreateMaestroShell()
        {
            GameObject go = new GameObject("Enemy_Maestro_Decoy_Test");
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
