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
            while (waited < 0.2f)
            {
                yield return null;
                waited += Time.deltaTime;
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
            while (waited < 0.2f)
            {
                yield return null;
                waited += Time.deltaTime;
            }

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
            while (waited < 0.15f)
            {
                yield return null;
                waited += Time.deltaTime;
            }
            Assert.AreSame(frame1, renderer.sprite,
                "Expected second hurt frame after one frame duration elapsed.");
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
            go.AddComponent<EnemyHurtFeedback>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

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
    }
}
