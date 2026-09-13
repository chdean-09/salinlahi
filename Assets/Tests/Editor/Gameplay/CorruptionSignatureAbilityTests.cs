using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// The corruption roster has no prefabs, so its signature abilities are data flags that
    /// Enemy.Initialize turns into components on the shared shell. These tests cover the wiring,
    /// Takip's cover cycle, and Iligaw's decoy-copy data without needing frames.
    /// </summary>
    [TestFixture]
    public class CorruptionSignatureAbilityTests
    {
        private readonly List<UnityEngine.Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void Initialize_AttachesAbilityComponentsFromData_AndTogglesThemOnReuse()
        {
            Enemy enemy = CreateShellEnemy(withBadge: false);

            EnemyDataSO takip = CreateData("takip");
            takip.coversOwnGlyph = true;
            Assert.IsTrue(enemy.Initialize(takip));
            GlyphCoverController cover = enemy.GetComponent<GlyphCoverController>();
            Assert.IsNotNull(cover, "coversOwnGlyph should attach GlyphCoverController");
            Assert.IsTrue(cover.enabled);
            Assert.IsNull(enemy.GetComponent<KempeiScrambleController>(), "no flag, no scramble component");
            Assert.IsNull(enemy.GetComponent<MirrorDecoyController>());
            Assert.IsNull(enemy.GetComponent<BakodShieldController>(), "no flag, no Bakod shield component");
            Assert.IsNull(enemy.GetComponent<AshFirstSlotController>(), "no flag, no Abo ash component");

            // The same pooled shell reused for another type must not keep Takip's ability.
            EnemyDataSO mantsa = CreateData("mantsa");
            mantsa.stainsNearbyGlyphs = true;
            Assert.IsTrue(enemy.Initialize(mantsa));
            Assert.IsFalse(enemy.GetComponent<GlyphCoverController>().enabled);
            Assert.IsTrue(enemy.GetComponent<KempeiScrambleController>().enabled);

            EnemyDataSO iligaw = CreateData("iligaw");
            iligaw.spawnsMirrorDecoy = true;
            iligaw.zigzagAmplitude = 1f;
            iligaw.zigzagFrequency = 0.5f;
            Assert.IsTrue(enemy.Initialize(iligaw));
            Assert.IsTrue(enemy.GetComponent<MirrorDecoyController>().enabled);
            Assert.IsTrue(enemy.GetComponent<PensionadoMover>().enabled, "zigzag amplitude should enable the zigzag mover");
            Assert.IsFalse(enemy.GetComponent<KempeiScrambleController>().enabled);
            Assert.IsFalse(enemy.GetComponent<GlyphCoverController>().enabled);

            // SALIN-286 and SALIN-284 join the same data-flag pattern.
            EnemyDataSO bakod = CreateData("bakod");
            bakod.blocksEnemiesBehind = true;
            Assert.IsTrue(enemy.Initialize(bakod));
            Assert.IsTrue(enemy.GetComponent<BakodShieldController>().enabled,
                "blocksEnemiesBehind should attach and enable BakodShieldController");
            Assert.IsFalse(enemy.GetComponent<MirrorDecoyController>().enabled);

            EnemyDataSO abo = CreateData("abo");
            abo.ashesFirstSlot = true;
            Assert.IsTrue(enemy.Initialize(abo));
            Assert.IsTrue(enemy.GetComponent<AshFirstSlotController>().enabled,
                "ashesFirstSlot should attach and enable AshFirstSlotController");
            Assert.IsFalse(enemy.GetComponent<BakodShieldController>().enabled,
                "a shell reused for Abo must not keep Bakod's shield");

            EnemyDataSO plain = CreateData("plain");
            Assert.IsTrue(enemy.Initialize(plain));
            Assert.IsFalse(enemy.GetComponent<MirrorDecoyController>().enabled);
            Assert.IsFalse(enemy.GetComponent<PensionadoMover>().enabled);
            Assert.IsFalse(enemy.GetComponent<BakodShieldController>().enabled);
            Assert.IsFalse(enemy.GetComponent<AshFirstSlotController>().enabled);
        }

        [Test]
        public void GlyphCover_RevealsFirst_ThenCyclesHiddenAndRevealed()
        {
            Enemy enemy = CreateShellEnemy(withBadge: true);
            EnemyDataSO takip = CreateData("takip");
            takip.coversOwnGlyph = true;
            takip.glyphCoverInitialRevealSeconds = 1f;
            takip.glyphCoverHiddenSeconds = 2f;
            takip.glyphCoverRevealSeconds = 0.5f;
            Assert.IsTrue(enemy.Initialize(takip));
            enemy.AssignCharacter(CreateCharacter("TA"));

            GlyphCoverController cover = enemy.GetComponent<GlyphCoverController>();
            SpriteRenderer badgeRenderer = enemy.GlyphBadge.GetComponent<SpriteRenderer>();
            Assert.IsTrue(badgeRenderer.enabled, "badge visible before the first cover");

            cover.Tick(0.5f);
            Assert.IsFalse(cover.IsCovered, "still inside the initial reveal window");
            Assert.IsTrue(badgeRenderer.enabled);

            cover.Tick(0.6f);
            Assert.IsTrue(cover.IsCovered, "initial reveal elapsed -> covered");
            Assert.IsFalse(badgeRenderer.enabled, "covered badge renderer is off");
            Assert.IsNotNull(badgeRenderer.sprite, "cover hides the badge without dropping its sprite");

            cover.Tick(1.9f);
            Assert.IsTrue(cover.IsCovered, "hidden window not over yet");

            cover.Tick(0.2f);
            Assert.IsFalse(cover.IsCovered, "hidden window elapsed -> revealed");
            Assert.IsTrue(badgeRenderer.enabled);

            cover.Tick(0.6f);
            Assert.IsTrue(cover.IsCovered, "reveal window elapsed -> covered again");

            // A glyph refresh underneath the cover must not un-hide it.
            enemy.GlyphBadge.Refresh();
            Assert.IsFalse(badgeRenderer.enabled);
        }

        [Test]
        public void GlyphCover_DoesNothingWhenDataDoesNotCover()
        {
            Enemy enemy = CreateShellEnemy(withBadge: true);
            Assert.IsTrue(enemy.Initialize(CreateData("plain")));
            enemy.AssignCharacter(CreateCharacter("MA"));
            var cover = enemy.gameObject.AddComponent<GlyphCoverController>();
            _objectsToDestroy.Add(cover);

            cover.Tick(5f);
            cover.Tick(5f);

            Assert.IsFalse(cover.IsCovered);
            Assert.IsTrue(enemy.GlyphBadge.GetComponent<SpriteRenderer>().enabled);
        }

        [Test]
        public void MirrorDecoyData_IsADecoyCopyOfItsSource_SharesPoolAndGlyph_AndIsCached()
        {
            EnemyDataSO iligaw = CreateData("iligaw");
            iligaw.spawnsMirrorDecoy = true;
            iligaw.dealsContactDamage = true;
            iligaw.assignedCharacter = CreateCharacter("EI");
            iligaw.zigzagAmplitude = 1.1f;

            EnemyDataSO decoy = MirrorDecoyController.GetDecoyData(iligaw);
            _objectsToDestroy.Add(decoy);

            Assert.IsNotNull(decoy);
            Assert.AreNotSame(iligaw, decoy);
            Assert.AreEqual(iligaw.enemyID, decoy.enemyID, "same id keeps the copy in the source's pool and discovery identity");
            Assert.AreSame(iligaw.assignedCharacter, decoy.assignedCharacter, "the copy carries the correct symbol");
            Assert.AreEqual(iligaw.zigzagAmplitude, decoy.zigzagAmplitude, "the copy changes direction like its source");
            Assert.IsTrue(decoy.isDecoy);
            Assert.IsFalse(decoy.dealsContactDamage, "a false copy must not damage the base");
            Assert.IsFalse(decoy.spawnsMirrorDecoy, "copies do not spawn copies");
            Assert.IsTrue(decoy.suppressDiscovery);
            Assert.AreSame(decoy, MirrorDecoyController.GetDecoyData(iligaw), "one runtime data per source");
        }

        [Test]
        public void DecoyCopy_NeverRaisesItsOwnDiscoveryEvent()
        {
            bool raised = false;
            Action<EnemyDataSO, Enemy> handler = (_, __) => raised = true;
            EventBus.OnEnemyDiscovered += handler;
            try
            {
                Enemy enemy = CreateShellEnemy(withBadge: false);
                EnemyDataSO decoy = MirrorDecoyController.GetDecoyData(CreateData("iligaw"));
                _objectsToDestroy.Add(decoy);
                Assert.IsTrue(enemy.Initialize(decoy));
                Assert.IsFalse(raised, "a mirrored copy is not a new enemy to discover");
            }
            finally
            {
                EventBus.OnEnemyDiscovered -= handler;
            }
        }

        // ---------------------------------------------------------------- helpers

        private Enemy CreateShellEnemy(bool withBadge)
        {
            var go = new GameObject("CorruptedShell_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);

            EnemyGlyphBadge badge = null;
            if (withBadge)
            {
                var badgeGo = new GameObject("GlyphBadge");
                badgeGo.transform.SetParent(go.transform, false);
                badgeGo.AddComponent<SpriteRenderer>();
                badge = badgeGo.AddComponent<EnemyGlyphBadge>();
            }

            go.SetActive(true);
            _objectsToDestroy.Add(go);

            InvokePrivate(enemy, "Awake");
            if (badge != null)
                InvokePrivate(badge, "Awake");
            return enemy;
        }

        private EnemyDataSO CreateData(string enemyID)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            data.displayName = enemyID;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            _objectsToDestroy.Add(data);
            return data;
        }

        private BaybayinCharacterSO CreateCharacter(string id)
        {
            var texture = new Texture2D(4, 4);
            _objectsToDestroy.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            _objectsToDestroy.Add(sprite);

            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.syllable = id.ToLowerInvariant();
            character.badgeSprite = sprite;
            _objectsToDestroy.Add(character);
            return character;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, method);
            info.Invoke(target, null);
        }
    }
}
