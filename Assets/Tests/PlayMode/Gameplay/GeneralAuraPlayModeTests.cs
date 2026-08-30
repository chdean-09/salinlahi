using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class GeneralAuraPlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private GameObject _gameManagerGO;
        private GameObject _trackerGO;

        [SetUp]
        public void SetUp()
        {
            // Earlier fixtures boot real scenes (e.g. ElInquisidorTest loads
            // Bootstrap) whose DontDestroyOnLoad singletons survive into this
            // fixture. Creating a fresh manager while a leaked Instance is
            // alive makes the new one's Awake duplicate-guard destroy it a
            // frame later, and Singleton.OnDestroy then nulls the forced
            // Instance — silently disabling the aura. Reuse a live leaked
            // instance instead of fighting it (destroying it would starve the
            // later fixtures that also lean on it), and only create our own
            // when none exists.
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                _gameManagerGO = new GameObject("GameManager_PlayMode_Aura_Test");
                gm = _gameManagerGO.AddComponent<GameManager>();
                SetSingletonInstance(gm);
                _objectsToDestroy.Add(_gameManagerGO);
            }
            gm.StartGame();

            if (ActiveEnemyTracker.Instance == null)
            {
                _trackerGO = new GameObject("ActiveEnemyTracker_PlayMode_Aura_Test");
                ActiveEnemyTracker tracker = _trackerGO.AddComponent<ActiveEnemyTracker>();
                SetSingletonInstance(tracker);
                _objectsToDestroy.Add(_trackerGO);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Only clear singletons this fixture installed itself; a reused
            // leaked instance stays untouched for the fixtures after us.
            if (_gameManagerGO != null)
                ClearSingletonInstance<GameManager>();
            if (_trackerGO != null)
                ClearSingletonInstance<ActiveEnemyTracker>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            _gameManagerGO = null;
            _trackerGO = null;
        }

        [UnityTest]
        public IEnumerator DyingEnemy_IsNotBuffedByGeneralAura()
        {
            Enemy general = CreateGeneral(Vector3.zero, radius: 5f, buffMul: 2f);
            Enemy ally = CreatePawn(new Vector3(2f, 0f, 0f), Era.American, withDeathFrames: true);
            float baseSpeed = ally.EffectiveSpeed;

            ForceAuraTick(general);
            Assert.AreEqual(baseSpeed * 2f, ally.EffectiveSpeed, 0.0001f,
                "Sanity: living ally in radius should be buffed.");

            ally.Defeat();
            Assert.IsTrue(ally.IsDying, "Enemy should stay active and marked dying during death animation.");

            ForceAuraTick(general);
            Assert.AreEqual(baseSpeed, ally.EffectiveSpeed, 0.0001f,
                "Dying enemy must be removed from General aura buffs.");

            yield return null;
        }

        private Enemy CreateGeneral(Vector3 position, float radius, float buffMul)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "general_playmode_test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.era = Era.American;
            data.auraRadius = radius;
            data.auraSpeedMultiplier = buffMul;
            data.assignedCharacter = CreateCharacter("GA");
            _objectsToDestroy.Add(data);

            GameObject go = new GameObject("General_PlayMode_Test");
            go.SetActive(false);
            go.transform.position = position;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            go.AddComponent<GeneralAura>();
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private Enemy CreatePawn(Vector3 position, Era era, bool withDeathFrames)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = $"ally_{era}_playmode_test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.era = era;
            data.assignedCharacter = CreateCharacter("BA");
            if (withDeathFrames)
            {
                data.deathFrames = new[] { CreateSolidSprite(Color.red) };
                data.deathAnimationFps = 1f;
            }
            _objectsToDestroy.Add(data);

            GameObject go = new GameObject($"Ally_{era}_PlayMode_Test");
            go.SetActive(false);
            go.transform.position = position;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
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

        private static void ForceAuraTick(Enemy general)
        {
            GeneralAura aura = general.GetComponent<GeneralAura>();
            Assert.IsNotNull(aura, "Test setup error: General is missing GeneralAura.");
            SetPrivateField(aura, "_nextTick", 0f);
            InvokePrivateMethod(aura, "Update");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
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
