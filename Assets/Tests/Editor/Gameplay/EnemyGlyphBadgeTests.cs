using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyGlyphBadgeTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();
        }

        [Test]
        public void Refresh_NullVisualCharacter_DisablesRenderer()
        {
            (Enemy enemy, EnemyGlyphBadge badge, SpriteRenderer renderer) = CreateEnemyWithBadge(assignedCharacter: null);
            badge.Refresh();
            Assert.IsFalse(renderer.enabled);
        }

        [Test]
        public void SetCharacter_AssignsBadgeSprite_FromCharacter()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", badge: CreateSprite(Color.green));
            (Enemy enemy, EnemyGlyphBadge badge, SpriteRenderer renderer) = CreateEnemyWithBadge(ch);
            badge.Refresh();
            Assert.IsTrue(renderer.enabled);
            Assert.AreSame(ch.badgeSprite, renderer.sprite);
        }

        [Test]
        public void SetCharacter_UsesScrambledSprite_WhenOverrideActiveAndAssetPresent()
        {
            BaybayinCharacterSO normal = CreateCharacter("BA", badge: CreateSprite(Color.green));
            BaybayinCharacterSO scrambleSource = CreateCharacter("KA",
                badge: CreateSprite(Color.red),
                scrambled: CreateSprite(Color.magenta));
            (Enemy enemy, EnemyGlyphBadge badge, SpriteRenderer renderer) = CreateEnemyWithBadge(normal);
            enemy.ApplyVisualCharacterOverride(this, scrambleSource);
            badge.Refresh();
            Assert.AreSame(scrambleSource.scrambledBadgeSprite, renderer.sprite);
        }

        [Test]
        public void SetCharacter_UsesNormalSprite_WhenOverrideActiveButScrambledAssetMissing()
        {
            BaybayinCharacterSO normal = CreateCharacter("BA", badge: CreateSprite(Color.green));
            BaybayinCharacterSO scrambleSource = CreateCharacter("KA",
                badge: CreateSprite(Color.red),
                scrambled: null);
            (Enemy enemy, EnemyGlyphBadge badge, SpriteRenderer renderer) = CreateEnemyWithBadge(normal);
            enemy.ApplyVisualCharacterOverride(this, scrambleSource);
            badge.Refresh();
            Assert.AreSame(scrambleSource.badgeSprite, renderer.sprite);
        }

        [Test]
        public void Refresh_NoOp_WhenSwapInFlight()
        {
            BaybayinCharacterSO original = CreateCharacter("BA", badge: CreateSprite(Color.green));
            BaybayinCharacterSO next = CreateCharacter("KA", badge: CreateSprite(Color.red));
            (Enemy enemy, EnemyGlyphBadge badge, SpriteRenderer renderer) = CreateEnemyWithBadge(original);
            badge.Refresh();
            Sprite spriteBeforeSwap = renderer.sprite;
            badge.PlaySwap(next);
            Assert.IsTrue(badge.IsSwapping);
            badge.Refresh();
            Assert.AreSame(spriteBeforeSwap, renderer.sprite,
                "Refresh() must be a no-op while a swap is in flight.");
        }

        [Test]
        public void Layout_AppliesEnemyOverride_WhenToggleSet()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", badge: CreateSprite(Color.green));
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.assignedCharacter = ch;
            data.overrideBadgeOffset = true;
            data.glyphBadgeOffsetOverride = new Vector2(2.5f, 3.5f);
            data.overrideBadgeScale = true;
            data.glyphBadgeScaleOverride = 2f;
            _objectsToDestroy.Add(data);
            (Enemy enemy, EnemyGlyphBadge badge, _) = CreateEnemyAndBadgeShells();
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();
            Assert.AreEqual(2.5f, badge.transform.localPosition.x, 0.001f);
            Assert.AreEqual(3.5f, badge.transform.localPosition.y, 0.001f);
            Assert.AreEqual(2f, badge.transform.localScale.x, 0.001f);
        }

        [Test]
        public void Layout_FallsBackToConfig_WhenToggleClear()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", badge: CreateSprite(Color.green));
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.assignedCharacter = ch;
            data.overrideBadgeOffset = false;
            data.overrideBadgeScale = false;
            _objectsToDestroy.Add(data);
            (Enemy enemy, EnemyGlyphBadge badge, _) = CreateEnemyAndBadgeShells();
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();
            Assert.AreEqual(0f, badge.transform.localPosition.x, 0.001f);
            Assert.AreEqual(1.2f, badge.transform.localPosition.y, 0.001f);
            Assert.AreEqual(1f, badge.transform.localScale.x, 0.001f);
        }

        // Regression: ApplyLayout used to cache the parent's lossyScale once.
        // When the boss collapse animation later changed root localScale, the
        // child badge was visually squashed and the boss counter (which anchors
        // to the badge) shifted with it. RecomputeBaseFromParentScale (also
        // invoked from LateUpdate) must restore the world-stable scale.
        [Test]
        public void Layout_RecomputesAfterParentScaleChange_StaysWorldStable()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", badge: CreateSprite(Color.green));
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.assignedCharacter = ch;
            data.overrideBadgeOffset = true;
            data.glyphBadgeOffsetOverride = new Vector2(0f, 1.2f);
            data.overrideBadgeScale = true;
            data.glyphBadgeScaleOverride = 1f;
            _objectsToDestroy.Add(data);

            (Enemy enemy, EnemyGlyphBadge badge, _) = CreateEnemyAndBadgeShells();
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();

            // Parent localScale changes (mimics boss collapse squash).
            enemy.transform.localScale = new Vector3(1f, 0.85f, 1f);
            badge.RecomputeBaseFromParentScale();

            // World-space offset/scale should still match the configured values.
            Vector3 worldPos = badge.transform.position;
            Vector3 worldScale = badge.transform.lossyScale;
            Assert.AreEqual(0f, worldPos.x, 0.001f);
            Assert.AreEqual(1.2f, worldPos.y, 0.001f,
                "Badge world Y offset must remain equal to the configured world offset.");
            Assert.AreEqual(1f, worldScale.x, 0.001f);
            Assert.AreEqual(1f, worldScale.y, 0.001f,
                "Badge world Y scale must remain stable after parent scale changes.");
        }

        [Test]
        public void ResetForPool_ClearsAlphaPositionScaleRotation_AndStopsRoutines()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", badge: CreateSprite(Color.green));
            BaybayinCharacterSO next = CreateCharacter("KA", badge: CreateSprite(Color.red));
            (Enemy enemy, EnemyGlyphBadge badge, SpriteRenderer renderer) = CreateEnemyWithBadge(ch);
            badge.Refresh();
            badge.PlaySwap(next);
            Assert.IsTrue(badge.IsSwapping);
            renderer.color = new Color(1, 1, 1, 0.2f);
            badge.transform.localScale = new Vector3(3f, 3f, 3f);
            badge.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            badge.ResetForPool();
            Assert.AreEqual(1f, renderer.color.a, 0.001f);
            Assert.IsFalse(badge.IsSwapping, "ResetForPool must stop in-flight routines.");
        }

        private (Enemy, EnemyGlyphBadge, SpriteRenderer) CreateEnemyAndBadgeShells()
        {
            GameObject root = new GameObject("Enemy_Test");
            root.SetActive(false);
            root.AddComponent<SpriteRenderer>();
            root.AddComponent<BoxCollider2D>();
            root.AddComponent<EnemyMover>();
            Enemy enemy = root.AddComponent<Enemy>();
            GameObject badgeGO = new GameObject("GlyphBadge");
            badgeGO.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = badgeGO.AddComponent<SpriteRenderer>();
            EnemyGlyphBadge badge = badgeGO.AddComponent<EnemyGlyphBadge>();
            GlyphBadgeConfigSO config = ScriptableObject.CreateInstance<GlyphBadgeConfigSO>();
            SetPrivateField(badge, "_config", config);
            _objectsToDestroy.Add(config);
            SetPrivateField(enemy, "_showDebugLabels", false);
            _objectsToDestroy.Add(root);
            root.SetActive(true);
            return (enemy, badge, renderer);
        }

        private (Enemy, EnemyGlyphBadge, SpriteRenderer) CreateEnemyWithBadge(BaybayinCharacterSO assignedCharacter)
        {
            GameObject root = new GameObject("Enemy_Test");
            root.SetActive(false);
            root.AddComponent<SpriteRenderer>();
            root.AddComponent<BoxCollider2D>();
            root.AddComponent<EnemyMover>();
            Enemy enemy = root.AddComponent<Enemy>();

            GameObject badgeGO = new GameObject("GlyphBadge");
            badgeGO.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = badgeGO.AddComponent<SpriteRenderer>();
            EnemyGlyphBadge badge = badgeGO.AddComponent<EnemyGlyphBadge>();

            GlyphBadgeConfigSO config = ScriptableObject.CreateInstance<GlyphBadgeConfigSO>();
            SetPrivateField(badge, "_config", config);
            _objectsToDestroy.Add(config);

            SetPrivateField(enemy, "_showDebugLabels", false);
            root.SetActive(true);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "test";
            data.moveSpeed = 1f;
            data.maxHealth = 1;
            data.assignedCharacter = assignedCharacter;
            _objectsToDestroy.Add(data);
            _objectsToDestroy.Add(root);

            Assert.IsTrue(enemy.Initialize(data));
            return (enemy, badge, renderer);
        }

        private BaybayinCharacterSO CreateCharacter(string id, Sprite badge = null, Sprite scrambled = null)
        {
            BaybayinCharacterSO ch = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ch.characterID = id;
            ch.syllable = id.ToLowerInvariant();
            ch.badgeSprite = badge;
            ch.scrambledBadgeSprite = scrambled;
            _objectsToDestroy.Add(ch);
            return ch;
        }

        private Sprite CreateSprite(Color color)
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
