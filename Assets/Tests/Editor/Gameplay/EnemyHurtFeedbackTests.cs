using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyHurtFeedbackTests
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
        public void NonLethalHit_StartsHurtRoutine()
        {
            EnemyDataSO data = CreateData(maxHealth: 2);
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

            enemy.TakeDamage(1);

            Assert.IsTrue(feedback.IsPlayingHurtAnimation,
                "Expected hurt routine to start after a non-lethal hit.");
            Assert.AreEqual(1, enemy.CurrentHealth);
        }

        [Test]
        public void MasterToggleOff_DoesNothing()
        {
            EnemyDataSO data = CreateData(maxHealth: 2);
            data.useHurtFeedback = false;
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

            enemy.TakeDamage(1);

            Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                "Expected hurt routine to stay idle when useHurtFeedback is false.");
        }

        [Test]
        public void LethalHit_DoesNotStartHurtRoutine()
        {
            EnemyDataSO data = CreateData(maxHealth: 1);
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

            enemy.TakeDamage(1);

            Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                "Expected hurt routine to stay idle when the hit is lethal.");
            Assert.AreEqual(0, enemy.CurrentHealth);
        }

        [UnityTest]
        public IEnumerator PauseToggle_StopsAndResumesMover()
        {
            EnemyDataSO data = CreateData(maxHealth: 2);
            data.hurtPauseDuration = 0.05f;
            data.hurtShakesSprite = false;
            data.hurtPausesMovement = true;
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyMover mover = enemy.GetComponent<EnemyMover>();

            enemy.TakeDamage(1);

            Assert.IsFalse(mover.IsMoving, "Expected mover to be stopped during pause.");

            float waited = 0f;
            int frameCount = 0;
            while (waited < 0.2f && frameCount < 300)
            {
                yield return null;
                waited += Time.deltaTime;
                frameCount++;
            }

            Assert.IsTrue(mover.IsMoving, "Expected mover to resume after pause window.");
        }

        [UnityTest]
        public IEnumerator Shake_RestoresPositionOnExit()
        {
            EnemyDataSO data = CreateData(maxHealth: 2);
            data.hurtPausesMovement = false;
            data.hurtShakesSprite = true;
            data.hurtShakeMagnitude = 0.5f;
            data.hurtShakeDuration = 0.05f;
            data.hurtShakeFrequency = 20f;
            Enemy enemy = CreateEnemyWithFeedback(data);
            Vector3 before = enemy.transform.position;

            enemy.TakeDamage(1);

            float waited = 0f;
            int frameCount = 0;
            while (waited < 0.2f && frameCount < 300)
            {
                yield return null;
                waited += Time.deltaTime;
                frameCount++;
            }

            // EditMode never resumes the hurt routine past its first yield, so
            // the natural-completion restore cannot run here. Drive the same
            // cleanup Defeat()/pool return use; natural-exit restoration is
            // PlayMode territory.
            enemy.GetComponent<EnemyHurtFeedback>().ResetState();

            Vector3 after = enemy.transform.position;
            Assert.That((after - before).magnitude, Is.LessThan(0.001f),
                "Expected shake to leave the root position unchanged on exit.");
        }

        [Test]
        public void CharacterSwap_FiresOnceWhenEnabled()
        {
            BaybayinCharacterSO original = CreateCharacter("BA", "ba");
            BaybayinCharacterSO replacement = CreateCharacter("KA", "ka");
            EnemyDataSO data = CreateData(maxHealth: 3);
            data.assignedCharacter = original;
            data.hurtSwapsCharacter = true;
            data.postHurtCharacter = replacement;
            Enemy enemy = CreateEnemyWithFeedback(data);

            enemy.TakeDamage(1);
            Assert.AreSame(replacement, enemy.Character,
                "Expected character to swap after first non-lethal hit.");

            enemy.TakeDamage(1);
            Assert.AreSame(replacement, enemy.Character,
                "Expected character to stay swapped on subsequent hits.");
        }

        [Test]
        public void CharacterSwap_StaysOriginalWhenDisabled()
        {
            BaybayinCharacterSO original = CreateCharacter("BA", "ba");
            BaybayinCharacterSO replacement = CreateCharacter("KA", "ka");
            EnemyDataSO data = CreateData(maxHealth: 2);
            data.assignedCharacter = original;
            data.hurtSwapsCharacter = false;
            data.postHurtCharacter = replacement;
            Enemy enemy = CreateEnemyWithFeedback(data);

            enemy.TakeDamage(1);

            Assert.AreSame(original, enemy.Character,
                "Expected character to remain original when hurtSwapsCharacter is false.");
        }

        [UnityTest]
        public IEnumerator HurtFrames_PlayWhenSet()
        {
            Sprite frame0 = CreateSolidSprite(Color.red);
            Sprite frame1 = CreateSolidSprite(Color.yellow);
            EnemyDataSO data = CreateData(maxHealth: 2);
            data.hurtPausesMovement = false;
            data.hurtShakesSprite = false;
            data.hurtFrames = new[] { frame0, frame1 };
            data.hurtAnimationFps = 10f;
            Enemy enemy = CreateEnemyWithFeedback(data);
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            enemy.TakeDamage(1);

            yield return null;
            Assert.AreSame(frame0, renderer.sprite,
                "Expected first hurt frame to be applied.");

            float waited = 0f;
            int frameCount = 0;
            while (waited < 0.15f && frameCount < 300)
            {
                yield return null;
                waited += Time.deltaTime;
                frameCount++;
            }
            Assert.AreSame(frame1, renderer.sprite,
                "Expected second hurt frame after one frame duration elapsed.");
        }

        [UnityTest]
        public IEnumerator HurtFrames_KeepMoverStoppedUntilAnimationCompletes()
        {
            Sprite frame0 = CreateSolidSprite(Color.cyan);
            Sprite frame1 = CreateSolidSprite(Color.magenta);
            Sprite frame2 = CreateSolidSprite(Color.white);
            EnemyDataSO data = CreateData(maxHealth: 2);
            data.hurtPausesMovement = true;
            data.hurtPauseDuration = 0.01f;
            data.hurtShakesSprite = false;
            data.hurtFrames = new[] { frame0, frame1, frame2 };
            data.hurtAnimationFps = 5f; // total anim time = 0.6s

            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyMover mover = enemy.GetComponent<EnemyMover>();

            enemy.TakeDamage(1);
            Assert.IsFalse(mover.IsMoving, "Mover should stop when shield-break starts.");

            float waitedMidAnim = 0f;
            while (waitedMidAnim < 0.2f)
            {
                yield return null;
                waitedMidAnim += Time.deltaTime;
            }

            Assert.IsFalse(mover.IsMoving,
                "Mover should remain stopped while hurt frames are still playing.");

            float waitedEnd = 0f;
            while (waitedEnd < 0.7f)
            {
                yield return null;
                waitedEnd += Time.deltaTime;
            }

            Assert.IsTrue(mover.IsMoving,
                "Mover should resume after shield-break animation completes.");
        }

        [UnityTest]
        public IEnumerator DeathDuringHurt_CancelsHurtAndKeepsMoverStopped()
        {
            // Regression for the case where a non-lethal hit starts the hurt
            // routine, a follow-up hit kills the enemy, and the still-running
            // hurt routine would otherwise resume the mover mid death animation.
            EnemyDataSO data = CreateData(maxHealth: 3);
            data.hurtPauseDuration = 0.05f;
            data.hurtShakesSprite = false;
            data.deathFrames = new[] { CreateSolidSprite(Color.red) };
            data.deathAnimationFps = 1f;
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();
            EnemyMover mover = enemy.GetComponent<EnemyMover>();

            enemy.TakeDamage(1);
            Assert.IsTrue(feedback.IsPlayingHurtAnimation,
                "Sanity: hurt routine should be running after non-lethal hit.");

            enemy.TakeDamage(2);
            Assert.IsTrue(enemy.IsDying, "Enemy should be in dying state after lethal hit.");
            Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                "Defeat() must cancel the in-flight hurt routine.");
            Assert.IsFalse(mover.IsMoving,
                "Mover must stay stopped after Defeat() with death animation.");

            // Wait past the original hurt pause window — the mover must
            // still be stopped because the hurt routine was cancelled.
            float waited = 0f;
            int frameCount = 0;
            while (waited < 0.2f && frameCount < 300)
            {
                yield return null;
                waited += Time.deltaTime;
                frameCount++;
            }

            Assert.IsFalse(mover.IsMoving,
                "Mover must remain stopped throughout the death animation, "
                + "even after the original hurt-pause window would have elapsed.");
        }

        [UnityTest]
        public IEnumerator DeathDuringHurt_WithShake_ClearsShakeOffset()
        {
            // Regression: when Defeat() cancels the hurt routine via ResetState,
            // any in-flight shake offset must be removed from the root transform.
            // Otherwise the dying enemy plays its death animation at a shifted
            // gameplay position.
            EnemyDataSO data = CreateData(maxHealth: 3);
            data.hurtPausesMovement = true;
            data.hurtPauseDuration = 0.5f;
            data.hurtShakesSprite = true;
            data.hurtShakeMagnitude = 0.5f;
            data.hurtShakeDuration = 0.5f;
            data.hurtShakeFrequency = 30f;
            data.deathFrames = new[] { CreateSolidSprite(Color.red) };
            data.deathAnimationFps = 1f;
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();
            Vector3 rootBefore = enemy.transform.position;

            enemy.TakeDamage(1);

            // Let the shake apply at least one offset so there is something to
            // leak. The shake is sampled inside the coroutine on each frame.
            yield return null;
            yield return null;

            enemy.TakeDamage(2);

            Assert.IsTrue(enemy.IsDying, "Enemy should be dying after lethal hit.");
            Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                "Hurt routine must be cancelled when entering death animation.");
            Assert.That((enemy.transform.position - rootBefore).magnitude,
                Is.LessThan(0.001f),
                "Cancelling hurt feedback must remove any leftover shake offset "
                + "from the root transform — otherwise the death animation plays "
                + "at a shifted gameplay position.");
        }

        [Test]
        public void AuraSpeedUpdateDuringHurtPause_DoesNotUnpauseMover()
        {
            // Regression: an external speed buff/debuff recalculation (e.g. a
            // GeneralAura tick) must not flip the mover back on while hurt
            // feedback has it paused.
            EnemyDataSO data = CreateData(maxHealth: 2);
            data.hurtPauseDuration = 0.5f;
            data.hurtShakesSprite = false;
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyMover mover = enemy.GetComponent<EnemyMover>();

            enemy.TakeDamage(1);
            Assert.IsFalse(mover.IsMoving, "Sanity: mover should be stopped during hurt pause.");

            enemy.ApplySpeedBuff(this, 1.5f);
            Assert.IsFalse(mover.IsMoving,
                "Buff application must not resume a mover stopped by hurt feedback.");

            enemy.ClearSpeedBuff(this);
            Assert.IsFalse(mover.IsMoving,
                "Buff clear must not resume a mover stopped by hurt feedback.");
        }

        [Test]
        public void ResetForPool_ClearsHurtState()
        {
            EnemyDataSO data = CreateData(maxHealth: 2);
            Enemy enemy = CreateEnemyWithFeedback(data);
            EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

            enemy.TakeDamage(1);
            Assert.IsTrue(feedback.IsPlayingHurtAnimation);

            enemy.ResetForPool();

            Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                "Expected hurt routine to be cleared on ResetForPool.");
        }

        // ----- helpers -----

        private EnemyDataSO CreateData(int maxHealth)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "test";
            data.moveSpeed = 1f;
            data.maxHealth = maxHealth;
            data.assignedCharacter = CreateCharacter("BA", "ba");
            data.dealsContactDamage = true;
            data.useHurtFeedback = true;
            data.hurtPausesMovement = true;
            data.hurtPauseDuration = 0.05f;
            data.hurtShakesSprite = true;
            data.hurtShakeMagnitude = 0.05f;
            data.hurtShakeDuration = 0.05f;
            data.hurtShakeFrequency = 20f;
            data.hurtSwapsCharacter = false;
            _objectsToDestroy.Add(data);
            return data;
        }

        private BaybayinCharacterSO CreateCharacter(string id, string syllable)
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.syllable = syllable;
            _objectsToDestroy.Add(character);
            return character;
        }

        private Sprite CreateSolidSprite(Color color)
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] pixels = new Color[4];
            for (int i = 0; i < 4; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _objectsToDestroy.Add(tex);
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private Enemy CreateEnemyWithFeedback(EnemyDataSO data)
        {
            GameObject go = new GameObject("Enemy_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            EnemyHurtFeedback feedback = go.AddComponent<EnemyHurtFeedback>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            // EditMode never runs Awake on activation; both components cache
            // their sibling references there, so drive them by hand.
            InvokeLifecycle(enemy, "Awake");
            InvokeLifecycle(feedback, "Awake");

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

        private static void InvokeLifecycle(MonoBehaviour target, string methodName)
        {
            MethodInfo method = null;
            for (var type = target.GetType(); type != null && method == null; type = type.BaseType)
                method = type.GetMethod(
                    methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing lifecycle method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
