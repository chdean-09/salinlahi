using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class PhaserEnemyTests
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
        public IEnumerator IsPhaser_True_TogglesVisibilityOnCoroutine()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 0.02f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            Assert.IsTrue(phaser.IsVisible);
            Assert.IsTrue(renderer.enabled);

            yield return WaitUntilOrTimeout(
                () => GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                timeoutSeconds: 1.2f);

            Assert.IsTrue(GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                "Phaser should reach an invisible state at least once.");
        }

        [UnityTest]
        public IEnumerator IsPhaser_False_DoesNotToggleVisibility()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(isPhaser: false, phaserInterval: 0.02f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            yield return new WaitForSeconds(0.08f);
            Assert.IsTrue(phaser.IsVisible);
            Assert.Greater(renderer.color.a, 0.95f);
        }

        [UnityTest]
        public IEnumerator DisableEnable_ResetsToVisibleStateForPoolSafety()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 0.02f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            yield return WaitUntilOrTimeout(
                () => GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                timeoutSeconds: 1.2f);

            Assert.IsTrue(GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                "Sanity: test should reach an invisible state first.");
            enemy.gameObject.SetActive(false);
            yield return null;

            enemy.gameObject.SetActive(true);
            yield return null;

            Assert.IsTrue(phaser.IsVisible);
            Assert.Greater(renderer.color.a, 0.95f);
        }

        [UnityTest]
        public IEnumerator Phaser_UsesPhaserIntervalAsToggleHoldDuration()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(
                isPhaser: true,
                phaserInterval: 0.02f,
                phaserFadeOutDuration: 0f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();

            yield return new WaitForSeconds(0.01f);
            Assert.IsTrue(phaser.IsVisible);

            yield return WaitUntilOrTimeout(
                () => GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                timeoutSeconds: 0.6f);

            Assert.IsTrue(GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                "Phaser should become invisible according to phaserInterval timing.");
        }

        [UnityTest]
        public IEnumerator Phaser_InitialVisibleDelay_DoesNotUseVisibleHoldRange()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(
                isPhaser: true,
                phaserInterval: 0.02f,
                phaserFadeOutDuration: 0f,
                phaserInitialVisibleDelayMin: 0.05f,
                phaserInitialVisibleDelayMax: 0.05f,
                phaserVisibleHoldMin: 1.8f,
                phaserVisibleHoldMax: 2.7f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();

            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                "Phaser should complete first invisibility using initial delay, not visible hold range.");
        }

        [UnityTest]
        public IEnumerator Phaser_ContinuouslyCyclesWhileActive()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(
                isPhaser: true,
                phaserInterval: 0.02f,
                phaserFadeOutDuration: 0f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();

            yield return WaitUntilOrTimeout(() => !phaser.IsVisible, timeoutSeconds: 0.6f);
            Assert.IsFalse(phaser.IsVisible, "Phaser should enter an invisible state.");

            yield return WaitUntilOrTimeout(() => phaser.IsVisible, timeoutSeconds: 0.6f);
            Assert.IsTrue(phaser.IsVisible, "Phaser should return to a visible state.");

            yield return WaitUntilOrTimeout(() => !phaser.IsVisible, timeoutSeconds: 0.6f);
            Assert.IsFalse(phaser.IsVisible, "Phaser should continue cycling and become invisible again.");
        }

        [UnityTest]
        public IEnumerator Phaser_PulsesBeforeBecomingInvisible()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(
                isPhaser: true,
                phaserInterval: 0.02f,
                phaserFadeOutDuration: 0.18f,
                phaserFadeOutPulseCount: 4,
                phaserFadeOutPulseAmplitude: 1f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
            float minAlpha = 1f;
            float maxAlpha = 0f;
            int significantDirectionChanges = 0;
            float previousAlpha = renderer.color.a;
            int previousDirection = 0;

            yield return WaitUntilOrTimeout(() => renderer.color.a < 0.999f, timeoutSeconds: 0.6f);

            Assert.Less(renderer.color.a, 0.999f, "Pulse/fade should begin within the expected window.");

            float sampleStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - sampleStart < 0.12f)
            {
                float alpha = renderer.color.a;
                minAlpha = Mathf.Min(minAlpha, alpha);
                maxAlpha = Mathf.Max(maxAlpha, alpha);

                float delta = alpha - previousAlpha;
                int direction = 0;
                if (delta > 0.01f) direction = 1;
                if (delta < -0.01f) direction = -1;

                if (direction != 0 && previousDirection != 0 && direction != previousDirection)
                    significantDirectionChanges++;

                if (direction != 0)
                    previousDirection = direction;

                previousAlpha = alpha;
                yield return null;
            }

            Assert.IsTrue(phaser.IsVisible, "Phaser should still be visible during the warning pulse/fade-out.");
            Assert.Less(minAlpha, 0.95f, "Pulse warning should dip below full visibility.");
            Assert.Greater(maxAlpha, 0.98f, "Pulse warning should rebound near full visibility.");
            Assert.GreaterOrEqual(significantDirectionChanges, 2, "Pulse warning should alternate alpha direction at least twice.");
            Assert.Greater(renderer.color.a, 0.98f, "Pulse warning should rebound near full visibility before invisibility.");

            yield return WaitUntilOrTimeout(
                () => GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                timeoutSeconds: 0.8f);

            Assert.IsTrue(GetPrivateField<bool>(phaser, "_hasCompletedInvisibleState"),
                "Phaser should become fully invisible after warning pulse duration.");
        }

        [UnityTest]
        public IEnumerator Phaser_RemainsDamageableDuringFadeOutWarning()
        {
            ConfigureDeterministicTime();
            yield return null;

            Enemy enemy = CreateEnemy(
                isPhaser: true,
                phaserInterval: 0.02f,
                maxHealth: 2,
                phaserFadeOutDuration: 0.18f,
                phaserFadeOutPulseCount: 1,
                phaserFadeOutPulseAmplitude: 1f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            yield return WaitUntilOrTimeout(
                () => renderer.color.a < 0.95f && phaser.IsVisible,
                timeoutSeconds: 0.6f);

            Assert.IsTrue(phaser.IsVisible, "Phaser should still be hittable during warning fade-out.");
            enemy.TakeDamage(1);
            Assert.AreEqual(1, enemy.CurrentHealth);
        }

        [Test]
        public void InvisiblePhaser_IgnoresTakeDamage()
        {
            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 5f, maxHealth: 2);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SetPrivateField(phaser, "_isVisible", false);

            enemy.TakeDamage(1);

            Assert.AreEqual(2, enemy.CurrentHealth);
        }

        [Test]
        public void VisiblePhaser_TakesDamageNormally()
        {
            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 5f, maxHealth: 2);
            enemy.TakeDamage(1);
            Assert.AreEqual(1, enemy.CurrentHealth);
        }

        private Enemy CreateEnemy(
            bool isPhaser,
            float phaserInterval,
            int maxHealth = 1,
            float phaserFadeOutDuration = 0.3f,
            int phaserFadeOutPulseCount = 3,
            float phaserFadeOutPulseAmplitude = 0.2f,
            float phaserInitialVisibleDelayMin = 0f,
            float phaserInitialVisibleDelayMax = 0f,
            float phaserVisibleHoldMin = 0f,
            float phaserVisibleHoldMax = 0f)
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            character.syllable = "ba";
            _objectsToDestroy.Add(character);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "fraile_test";
            data.maxHealth = maxHealth;
            data.moveSpeed = 1f;
            data.assignedCharacter = character;
            data.isPhaser = isPhaser;
            data.phaserInterval = phaserInterval;
            data.phaserFadeOutDuration = phaserFadeOutDuration;
            data.phaserFadeOutPulseCount = phaserFadeOutPulseCount;
            data.phaserFadeOutPulseAmplitude = phaserFadeOutPulseAmplitude;
            data.phaserInitialVisibleDelayMin = phaserInitialVisibleDelayMin;
            data.phaserInitialVisibleDelayMax = phaserInitialVisibleDelayMax;
            data.phaserVisibleHoldMin = phaserVisibleHoldMin;
            data.phaserVisibleHoldMax = phaserVisibleHoldMax;
            _objectsToDestroy.Add(data);

            GameObject go = new GameObject("Enemy_Phaser_Component_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.AddComponent<PhaserEnemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            Assert.AreEqual(1, go.GetComponents<Enemy>().Length);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> predicate, float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (!predicate() && Time.realtimeSinceStartup - start < timeoutSeconds)
                yield return null;
        }

        private void ConfigureDeterministicTime()
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }
    }
}
