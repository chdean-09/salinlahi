using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// Subclass used to flip IsBoss on for boss-exclusion tests without
    /// touching production code (Enemy.IsBoss is virtual and defaults to false).
    public class TestBossEnemy : Enemy
    {
        public override bool IsBoss => true;
    }

    [TestFixture]
    public class GeneralAuraTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private GameObject _gameManagerGO;
        private GameObject _trackerGO;

        [SetUp]
        public void SetUp()
        {
            _gameManagerGO = new GameObject("GameManager_Test");
            GameManager gm = _gameManagerGO.AddComponent<GameManager>();
            SetSingletonInstance(gm);
            gm.StartGame();
            _objectsToDestroy.Add(_gameManagerGO);

            _trackerGO = new GameObject("ActiveEnemyTracker_Test");
            ActiveEnemyTracker tracker = _trackerGO.AddComponent<ActiveEnemyTracker>();
            SetSingletonInstance(tracker);
            _objectsToDestroy.Add(_trackerGO);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<ActiveEnemyTracker>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
        }

        [Test]
        public void AppliesBuffToAmericanNonBossWithinRadius()
        {
            Enemy general = CreateGeneral(Vector3.zero, radius: 5f, buffMul: 2f);
            Enemy american = CreatePawn<Enemy>(new Vector3(2f, 0f, 0f), Era.American);

            float baseSpeed = american.EffectiveSpeed;
            ForceAuraTick(general);

            Assert.AreEqual(baseSpeed * 2f, american.EffectiveSpeed, 0.0001f,
                "American non-boss within radius should receive the speed buff.");
        }

        [Test]
        public void DoesNotBuffBoss()
        {
            Enemy general = CreateGeneral(Vector3.zero, radius: 5f, buffMul: 2f);
            Enemy boss = CreatePawn<TestBossEnemy>(new Vector3(2f, 0f, 0f), Era.American);

            float before = boss.EffectiveSpeed;
            ForceAuraTick(general);

            Assert.AreEqual(before, boss.EffectiveSpeed, 0.0001f,
                "Bosses must be excluded from the General's aura.");
        }

        [Test]
        public void DoesNotBuffDifferentEra()
        {
            Enemy general = CreateGeneral(Vector3.zero, radius: 5f, buffMul: 2f);
            Enemy spanish = CreatePawn<Enemy>(new Vector3(2f, 0f, 0f), Era.Spanish);

            float before = spanish.EffectiveSpeed;
            ForceAuraTick(general);

            Assert.AreEqual(before, spanish.EffectiveSpeed, 0.0001f,
                "Cross-era enemies must not be buffed.");
        }

        [Test]
        public void DoesNotBuffDyingEnemy()
        {
            Enemy general = CreateGeneral(Vector3.zero, radius: 5f, buffMul: 2f);
            Enemy dyingAmerican = CreatePawn<Enemy>(new Vector3(2f, 0f, 0f), Era.American);

            float before = dyingAmerican.EffectiveSpeed;
            SetPrivateField(dyingAmerican, "_isDying", true);
            ForceAuraTick(general);

            Assert.AreEqual(before, dyingAmerican.EffectiveSpeed, 0.0001f,
                "Dying enemies must be excluded from General aura buff application.");
        }

        [Test]
        public void RemovesBuffWhenEnemyLeavesRadius()
        {
            Enemy general = CreateGeneral(Vector3.zero, radius: 5f, buffMul: 2f);
            Enemy american = CreatePawn<Enemy>(new Vector3(2f, 0f, 0f), Era.American);
            float baseSpeed = american.EffectiveSpeed;

            ForceAuraTick(general);
            Assert.AreEqual(baseSpeed * 2f, american.EffectiveSpeed, 0.0001f,
                "Sanity: enemy should be buffed before leaving radius.");

            american.transform.position = new Vector3(20f, 0f, 0f);
            ForceAuraTick(general);

            Assert.AreEqual(baseSpeed, american.EffectiveSpeed, 0.0001f,
                "Speed must drop back to base once the enemy leaves the radius.");
        }

        [Test]
        public void RemovesBuffImmediatelyOnGeneralDefeatWithDeathAnimation()
        {
            Enemy general = CreateGeneral(Vector3.zero, radius: 5f, buffMul: 2f, withDeathFrames: true);
            Enemy american = CreatePawn<Enemy>(new Vector3(2f, 0f, 0f), Era.American);
            float baseSpeed = american.EffectiveSpeed;

            ForceAuraTick(general);
            Assert.AreEqual(baseSpeed * 2f, american.EffectiveSpeed, 0.0001f,
                "Sanity: buff should be applied before defeat.");

            // Defeat takes the deathFrames branch — GameObject stays active during the animation.
            general.Defeat();
            Assert.IsTrue(general.IsDying,
                "General with deathFrames should be flagged as dying after Defeat().");

            Assert.AreEqual(baseSpeed, american.EffectiveSpeed, 0.0001f,
                "Speed buff must drop the same frame the General is defeated, "
                + "not deferred until the death animation finishes.");
        }

        // ----- helpers -----

        private Enemy CreateGeneral(Vector3 position, float radius, float buffMul, bool withDeathFrames = false)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "general_test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.era = Era.American;
            data.auraRadius = radius;
            data.auraSpeedMultiplier = buffMul;
            data.assignedCharacter = CreateCharacter("GA");

            if (withDeathFrames)
            {
                data.deathFrames = new[] { CreateSolidSprite(Color.red) };
                data.deathAnimationFps = 1f;
            }

            _objectsToDestroy.Add(data);

            GameObject go = new GameObject("General_Test");
            go.SetActive(false);
            go.transform.position = position;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            // Add GeneralAura AFTER activation. Adding it before SetActive(true) on an inactive
            // GameObject changes the Awake ordering enough that Enemy.Awake observes _mover as null
            // (matches the working DecoyEnemy/EnemyHurtFeedback test patterns).
            go.AddComponent<GeneralAura>();
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private Enemy CreatePawn<TEnemy>(Vector3 position, Era era) where TEnemy : Enemy
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = $"pawn_{era}";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.era = era;
            data.assignedCharacter = CreateCharacter("BA");
            _objectsToDestroy.Add(data);

            GameObject go = new GameObject($"Pawn_{era}_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<TEnemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.transform.position = position;
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

        /// Forces a GeneralAura tick by resetting its tick gate and invoking Update via reflection.
        /// Lets EditMode tests sample the per-tick effect without waiting real time.
        private static void ForceAuraTick(Enemy general)
        {
            GeneralAura aura = general.GetComponent<GeneralAura>();
            Assert.IsNotNull(aura, "Test setup error: General is missing GeneralAura.");
            SetPrivateField(aura, "_nextTick", 0f);
            InvokePrivateMethod(aura, "Update");
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
