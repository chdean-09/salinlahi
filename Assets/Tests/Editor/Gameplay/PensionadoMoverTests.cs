using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class PensionadoMoverTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private GameObject _gameManagerGO;

        [SetUp]
        public void SetUp()
        {
            _gameManagerGO = new GameObject("GameManager_Test");
            GameManager gm = _gameManagerGO.AddComponent<GameManager>();
            SetSingletonInstance(gm);
            gm.StartGame();
            _objectsToDestroy.Add(_gameManagerGO);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<GameManager>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
        }

        [Test]
        public void UsesSpawnXAsBaseX()
        {
            EnemyDataSO data = CreateZigzagData(amplitude: 1f, frequency: 1f);
            Enemy enemy = CreateEnemyWithMover(data, spawnX: 5f);
            PensionadoMover mover = enemy.GetComponent<PensionadoMover>();

            // First Update captures _baseX from the current transform position.
            InvokePrivateMethod(mover, "Update");

            float baseX = (float)GetPrivateField(mover, "_baseX");
            Assert.AreEqual(5f, baseX, 0.0001f,
                "Pensionado should anchor zigzag around the spawn X, not the pool position.");
        }

        [Test]
        public void DoesNotMoveHorizontallyWhileEnemyIsDying()
        {
            EnemyDataSO data = CreateZigzagData(amplitude: 1f, frequency: 1f);
            data.deathFrames = new[] { CreateSolidSprite(Color.red) };
            data.deathAnimationFps = 1f;

            Enemy enemy = CreateEnemyWithMover(data, spawnX: 0f);
            PensionadoMover mover = enemy.GetComponent<PensionadoMover>();

            // Capture _baseX with one Update so subsequent Updates would normally move the sprite.
            InvokePrivateMethod(mover, "Update");

            // Trigger the death-animation path. The GameObject stays active while dying.
            enemy.Defeat();
            Assert.IsTrue(enemy.IsDying, "Defeat with deathFrames should mark the enemy as dying.");

            float xAtDeath = enemy.transform.position.x;

            // Run several variant Updates. With the IsDying gate, none should change x.
            for (int i = 0; i < 5; i++)
                InvokePrivateMethod(mover, "Update");

            Assert.AreEqual(xAtDeath, enemy.transform.position.x, 0.0001f,
                "Pensionado must not slide horizontally during the death animation.");
        }

        // ----- helpers -----

        private EnemyDataSO CreateZigzagData(float amplitude, float frequency)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "pensionado_test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.zigzagAmplitude = amplitude;
            data.zigzagFrequency = frequency;
            data.assignedCharacter = CreateCharacter("BA");
            _objectsToDestroy.Add(data);
            return data;
        }

        private BaybayinCharacterSO CreateCharacter(string id)
        {
            BaybayinCharacterSO c = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            c.characterID = id;
            c.syllable = id.ToLowerInvariant();
            _objectsToDestroy.Add(c);
            return c;
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

        private Enemy CreateEnemyWithMover(EnemyDataSO data, float spawnX)
        {
            GameObject go = new GameObject("Pensionado_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.AddComponent<PensionadoMover>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);

            // Set the spawn X AFTER OnEnable so PensionadoMover.Update captures it like real spawn flow.
            go.transform.position = new Vector3(spawnX, 0f, 0f);
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo f = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return f.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo f = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            f.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo m = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"Missing method '{methodName}' on {target.GetType().Name}.");
            m.Invoke(target, null);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { null });
        }
    }
}
