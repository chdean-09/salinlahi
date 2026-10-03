using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// The time-driven half of the hurt-feedback coverage. These tests wait for
    /// the hurt coroutine to advance across real frames (pause-window expiry,
    /// frame-by-frame animation, natural mover resume), which EditMode cannot
    /// exercise: coroutines there never resume past their first yield. The
    /// state-at-entry assertions stay in the EditMode EnemyHurtFeedbackTests.
    /// </summary>
    [TestFixture]
    public class EnemyHurtFeedbackPlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.Destroy(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
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
            // Batch PlayMode can run 300 frames before the pause window elapses.
            while (waited < 0.2f && frameCount < 100000)
            {
                yield return null;
                waited += Time.deltaTime;
                frameCount++;
            }

            Assert.IsTrue(mover.IsMoving, "Expected mover to resume after pause window.");
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

            // Batch PlayMode can run thousands of frames per second, so a small
            // frame cap could exhaust before 0.15s of animation time passes —
            // wait on elapsed time with only a generous runaway guard.
            float waited = 0f;
            int frameCount = 0;
            while (waited < 0.15f && frameCount < 100000)
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
            // PlayMode runs Awake on activation, so no manual lifecycle driving.
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

    public class YaposVictoryCelebrationPlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private float _originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _originalTimeScale = Time.timeScale;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _originalTimeScale;
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.Destroy(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CelebrationLoopsWhileGameTimeIsPausedAndClearStopsIt()
        {
            GameObject canvasObject = new GameObject("YaposVictoryCanvas_Test", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _objectsToDestroy.Add(canvasObject);

            GameObject panel = new GameObject("VictoryPanel_Test", typeof(RectTransform));
            panel.transform.SetParent(canvasObject.transform, false);
            VictoryScreenUI screen = panel.AddComponent<VictoryScreenUI>();
            SetPrivateField(screen, "_panel", panel);

            Sprite first = CreateSprite(Color.white);
            Sprite second = CreateSprite(Color.yellow);
            Sprite third = CreateSprite(Color.red);
            Sprite fourth = CreateSprite(Color.blue);
            screen.SetYaposCelebration(new[] { first, second, third, fourth });
            Image image = panel.transform.Find("[Runtime] YaposCelebration").GetComponent<Image>();
            Assert.AreSame(first, image.sprite);

            Time.timeScale = 0f;
            float elapsed = 0f;
            while (elapsed < 0.6f && image.sprite == first)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Assert.AreNotSame(first, image.sprite,
                "The victory celebration must keep animating while the game is paused.");

            screen.ClearYaposCelebration();
            Assert.IsFalse(image.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator HealthFormStaysVisibleThroughoutHurtPauseAndWalkResumesOnThatForm()
        {
            Sprite baseFrame = CreateSprite(Color.white);
            Sprite twoHealthFrame = CreateSprite(Color.yellow);
            Sprite alternateTwoHealthFrame = CreateSprite(Color.green);
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "YA";
            character.syllable = "ya";
            _objectsToDestroy.Add(character);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "yapos-ng-dilim";
            data.assignedCharacter = character;
            data.maxHealth = 3;
            data.moveSpeed = 1f;
            data.useHurtFeedback = true;
            data.hurtPausesMovement = true;
            data.hurtPauseDuration = 0.2f;
            data.hurtShakesSprite = false;
            data.walkFrames = new[] { baseFrame };
            data.healthWalkFrames = new[]
            {
                new EnemyHealthWalkFrames { health = 2, frames = new[] { twoHealthFrame, alternateTwoHealthFrame } },
            };
            _objectsToDestroy.Add(data);

            GameObject enemyObject = new GameObject("YaposHurtForm_Test");
            enemyObject.SetActive(false);
            SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
            enemyObject.AddComponent<BoxCollider2D>();
            EnemyMover mover = enemyObject.AddComponent<EnemyMover>();
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemyObject.AddComponent<EnemyHurtFeedback>();
            enemyObject.SetActive(true);
            _objectsToDestroy.Add(enemyObject);

            Assert.IsTrue(enemy.Initialize(data));
            enemy.TakeDamage(1);
            Assert.AreSame(twoHealthFrame, renderer.sprite);
            Assert.IsFalse(mover.IsMoving, "Hurt feedback should hold movement during the damage pause.");

            yield return new WaitForSecondsRealtime(0.08f);
            Assert.AreSame(twoHealthFrame, renderer.sprite,
                "Hurt feedback must not replace the selected health form during its pause.");

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(mover.IsMoving, "Movement should resume when the hurt pause ends.");
            Assert.Contains(renderer.sprite, new[] { twoHealthFrame, alternateTwoHealthFrame },
                "The resumed walk cycle must continue on the two-HP form.");
        }

        private Sprite CreateSprite(Color color)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            _objectsToDestroy.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }
    }
}
