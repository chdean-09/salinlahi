using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class BossGlyphVisibilityBinderTests
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
        public void Construct_DoesNotThrow()
        {
            (GameObject go, _, _, _) = CreateBinderRig(0, 3, expected: null);
            Assert.IsNotNull(go.GetComponent<BossGlyphVisibilityBinder>());
        }

        [Test]
        public void VulnerabilityActive_ShowsBadgeWithExpectedCharacter()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, SpriteRenderer renderer, FakeBoss fake) =
                CreateBinderRig(correctDraws: 0, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            Assert.AreSame(ch.badgeSprite, renderer.sprite);
            Assert.IsTrue(renderer.enabled);
        }

        [Test]
        public void DrawnThisPhaseChanged_NonTerminal_TriggersSwap()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, _, FakeBoss fake) =
                CreateBinderRig(correctDraws: 1, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            fake.RaiseDrawnThisPhaseChanged();
            Assert.IsTrue(badge.IsSwapping);
            Assert.IsFalse(badge.IsPlayingFinalDraw);
        }

        [Test]
        public void DrawnThisPhaseChanged_Terminal_TriggersFinalDraw_NotSwap()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, _, FakeBoss fake) =
                CreateBinderRig(correctDraws: 3, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            fake.RaiseDrawnThisPhaseChanged();
            Assert.IsTrue(badge.IsPlayingFinalDraw);
            Assert.IsFalse(badge.IsSwapping);
        }

        [Test]
        public void BossDamaged_HidesBadge()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, SpriteRenderer renderer, FakeBoss fake) =
                CreateBinderRig(correctDraws: 0, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            EventBus.RaiseBossDamaged(0, 1);
            Assert.AreEqual(0f, renderer.color.a, 0.001f);
        }

        [Test]
        public void VulnerabilityExpired_HidesBadge_WithoutFinalDraw()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, SpriteRenderer renderer, FakeBoss fake) =
                CreateBinderRig(correctDraws: 1, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            EventBus.RaiseBossVulnerabilityExpired(0);
            Assert.AreEqual(0f, renderer.color.a, 0.001f);
            Assert.IsFalse(badge.IsPlayingFinalDraw);
        }

        // Regression: HandleDrawnThisPhaseChanged fired on init with
        // CorrectDrawsThisWindow == 0 would trigger PlaySwap even though no
        // player draw had landed. The binder now ignores that initial signal.
        [Test]
        public void DrawnThisPhaseChanged_OnInitWithZeroDraws_DoesNothing()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, _, FakeBoss fake) =
                CreateBinderRig(correctDraws: 0, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            fake.RaiseDrawnThisPhaseChanged();
            Assert.IsFalse(badge.IsSwapping,
                "Init signal must not start a swap before the first correct draw.");
            Assert.IsFalse(badge.IsPlayingFinalDraw);
        }

        // Regression: HandleBossDamaged previously called Hide() which stops
        // _finalDrawRoutine via StopCoroutine. The seal-broken animation must
        // run to completion; the routine self-hides at its end.
        [Test]
        public void BossDamaged_DuringFinalDraw_DoesNotCancelFinalDraw()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, _, FakeBoss fake) =
                CreateBinderRig(correctDraws: 3, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            fake.RaiseDrawnThisPhaseChanged();
            Assert.IsTrue(badge.IsPlayingFinalDraw,
                "Terminal draw should kick off the seal-broken animation.");
            EventBus.RaiseBossDamaged(0, 0);
            Assert.IsTrue(badge.IsPlayingFinalDraw,
                "BossDamaged must not cancel the in-flight final-draw routine.");
        }

        // Regression: DrawingFailed fired outside the active vulnerability
        // window briefly revealed the hidden boss glyph because FailFlashRoutine
        // writes alpha 1 unconditionally.
        [Test]
        public void DrawingFailed_OutsideVulnerability_DoesNotPlayFailFlash()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, SpriteRenderer renderer, FakeBoss fake) =
                CreateBinderRig(correctDraws: 0, required: 3, expected: ch);
            EventBus.RaiseBossStarted(null);
            // No RaiseBossVulnerabilityWindowActive — boss is not targetable.
            Color before = renderer.color;
            EventBus.RaiseDrawingFailed();
            Assert.AreEqual(before.r, renderer.color.r, 0.001f);
            Assert.AreEqual(before.g, renderer.color.g, 0.001f);
            Assert.AreEqual(before.b, renderer.color.b, 0.001f);
            Assert.AreEqual(before.a, renderer.color.a, 0.001f,
                "Fail flash must not alter color (especially alpha) when boss is hidden.");
        }

        [Test]
        public void DrawingFailed_DuringVulnerability_PlaysFailFlash()
        {
            BaybayinCharacterSO ch = CreateCharacter("BA", CreateSprite(Color.green));
            (_, EnemyGlyphBadge badge, SpriteRenderer renderer, FakeBoss fake) =
                CreateBinderRig(correctDraws: 0, required: 3, expected: ch);
            // Make FakeBoss targetable so the binder's IsTargetable gate passes.
            fake.SetTargetableForTest(true);
            EventBus.RaiseBossStarted(null);
            EventBus.RaiseBossVulnerabilityWindowActive(0);
            EventBus.RaiseDrawingFailed();
            // FailFlashRoutine writes failFlashColor on its first iteration.
            GlyphBadgeConfigSO config = badge.Config;
            Assert.AreEqual(config.failFlashColor.r, renderer.color.r, 0.01f);
            Assert.AreEqual(config.failFlashColor.g, renderer.color.g, 0.01f);
            Assert.AreEqual(config.failFlashColor.b, renderer.color.b, 0.01f);
        }

        private (GameObject, EnemyGlyphBadge, SpriteRenderer, FakeBoss) CreateBinderRig(
            int correctDraws, int required, BaybayinCharacterSO expected)
        {
            GameObject root = new GameObject("FakeBoss");
            root.SetActive(false);
            root.AddComponent<SpriteRenderer>();
            root.AddComponent<BoxCollider2D>();
            root.AddComponent<EnemyMover>();
            BossEnemy enemy = root.AddComponent<BossEnemy>();
            TestEnemyDebugLabels.Disable(enemy);

            GameObject badgeGO = new GameObject("GlyphBadge");
            badgeGO.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = badgeGO.AddComponent<SpriteRenderer>();
            EnemyGlyphBadge badge = badgeGO.AddComponent<EnemyGlyphBadge>();
            GlyphBadgeConfigSO config = ScriptableObject.CreateInstance<GlyphBadgeConfigSO>();
            TestPrivateFields.Set(badge, "_config", config);
            _objectsToDestroy.Add(config);

            FakeBoss fake = root.AddComponent<FakeBoss>();
            fake.Configure(expected, correctDraws, required);

            GameObject gmGO = new GameObject("GameManager");
            GameManager gm = gmGO.AddComponent<GameManager>();
            _objectsToDestroy.Add(gmGO);
            gm.SetCurrentBoss(fake);

            root.AddComponent<BossGlyphVisibilityBinder>();
            _objectsToDestroy.Add(root);
            root.SetActive(true);
            return (root, badge, renderer, fake);
        }

        private BaybayinCharacterSO CreateCharacter(string id, Sprite badge)
        {
            BaybayinCharacterSO ch = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ch.characterID = id;
            ch.syllable = id.ToLowerInvariant();
            ch.badgeSprite = badge;
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
    }

    public class FakeBoss : BossController
    {
        private BaybayinCharacterSO _fakeExpected;
        private int _fakeCorrectDraws;
        private int _fakeRequired;

        public void Configure(BaybayinCharacterSO expected, int correctDraws, int required)
        {
            _fakeExpected = expected;
            _fakeCorrectDraws = correctDraws;
            _fakeRequired = required;
        }

        public override BaybayinCharacterSO CurrentExpectedCharacter => _fakeExpected;
        public override int CorrectDrawsThisWindow => _fakeCorrectDraws;
        public override int RequiredCharactersForCurrentPhase => _fakeRequired;

        public void RaiseDrawnThisPhaseChanged() => RaiseOnDrawnThisPhaseChanged();

        // BossController.IsTargetable is non-virtual and reads
        // (_state == State.Vulnerable && _isVulnerableActiveWindow). Set the
        // underlying base-class fields via reflection. We resolve them through
        // typeof(BossController) because Type.GetField with NonPublic does not
        // walk the inheritance chain for private base-class fields.
        public void SetTargetableForTest(bool targetable)
        {
            System.Type stateType = typeof(BossController).GetNestedType("State",
                System.Reflection.BindingFlags.NonPublic);
            object stateValue = targetable
                ? System.Enum.Parse(stateType, "Vulnerable")
                : System.Enum.Parse(stateType, "Idle");
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(BossController).GetField("_state", flags).SetValue(this, stateValue);
            typeof(BossController).GetField("_isVulnerableActiveWindow", flags).SetValue(this, targetable);
        }
    }

    internal static class TestEnemyDebugLabels
    {
        public static void Disable(Enemy enemy) =>
            TestPrivateFields.Set(enemy, "_showDebugLabels", false);
    }

    internal static class TestPrivateFields
    {
        public static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
