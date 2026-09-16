using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// <c>IntroductionOutcome.IntroduceAndSuppress</c> promises that "the ability is inert this
    /// spawn". Four abilities already honoured it; Takip's cover, Bakod's shield and Hati's split
    /// did not, so the card explained what the enemy does while the enemy was already doing it.
    ///
    /// <para>
    /// These assert <b>observable ability outcomes</b> — an uncovered badge, a resolvable enemy
    /// behind Bakod, an empty field after a suppressed Hati falls — never merely that a flag was
    /// stored. Suppression is driven through <c>Enemy.ApplyIntroductionSpawnSuppression</c> rather
    /// than by calling each controller directly, so a controller that grows the member but is never
    /// wired into Enemy still fails here.
    /// </para>
    ///
    /// <para>
    /// <b>The second half of every test is the one that matters.</b> The enemy shell is pooled, and
    /// a suppression that does not clear on the next spawn leaves the ability permanently dead on a
    /// shell that looks identical to a working one. EditMode never runs <c>OnEnable</c>, so these
    /// exercise the path that has to work without it: <c>Enemy.Initialize</c> restating suppression
    /// on every spawn, which is precisely why that restatement exists.
    /// </para>
    /// </summary>
    [TestFixture]
    public class IntroductionSpawnSuppressionTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private readonly List<Enemy> _snapshot = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<EnemyPool>();
            var trackerGo = new GameObject("ActiveEnemyTracker_Suppression_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<EnemyPool>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            _snapshot.Clear();
        }

        // ------------------------------------------------------------- Takip: the glyph cover

        [Test]
        public void GlyphCover_StaysOffWhileSuppressed_AndCoversAgainOnTheNextSpawn()
        {
            Enemy enemy = CreateShellEnemy(withBadge: true);
            EnemyDataSO takip = CreateData("takip");
            takip.coversOwnGlyph = true;
            takip.glyphCoverInitialRevealSeconds = 1f;
            takip.glyphCoverHiddenSeconds = 2f;
            takip.glyphCoverRevealSeconds = 0.5f;
            Assert.IsTrue(enemy.Initialize(takip));
            enemy.AssignCharacter(CreateCharacter("TA"));

            var cover = enemy.GetComponent<GlyphCoverController>();
            Assert.IsNotNull(cover, "coversOwnGlyph should attach GlyphCoverController");
            SpriteRenderer badgeRenderer = enemy.GlyphBadge.GetComponent<SpriteRenderer>();

            Suppress(enemy, true);
            Assert.IsTrue(cover.IsSuppressedForIntroductionSpawn,
                "Enemy must reach GlyphCoverController when it applies the introduction suppression");

            // Far past every authored window: an unsuppressed Takip would have covered and
            // uncovered several times over by now.
            cover.Tick(5f);
            cover.Tick(5f);
            cover.Tick(5f);
            Assert.IsFalse(cover.IsCovered, "a suppressed Takip never covers its glyph");
            Assert.IsTrue(badgeRenderer.enabled,
                "the badge the introduction card is framing must stay readable");

            // The next spawn out of this pooled shell.
            Assert.IsTrue(enemy.Initialize(takip));
            Assert.IsFalse(cover.IsSuppressedForIntroductionSpawn,
                "Initialize restates suppression on every spawn, so a pooled shell comes back armed");

            cover.Tick(1.1f);
            Assert.IsTrue(cover.IsCovered, "the initial reveal elapsed -> the ability is live again");
            Assert.IsFalse(badgeRenderer.enabled);
        }

        [Test]
        public void GlyphCover_SuppressedMidLife_UncoversImmediately()
        {
            Enemy enemy = CreateShellEnemy(withBadge: true);
            EnemyDataSO takip = CreateData("takip");
            takip.coversOwnGlyph = true;
            takip.glyphCoverInitialRevealSeconds = 1f;
            takip.glyphCoverHiddenSeconds = 5f;
            takip.glyphCoverRevealSeconds = 0.5f;
            Assert.IsTrue(enemy.Initialize(takip));
            enemy.AssignCharacter(CreateCharacter("TA"));

            var cover = enemy.GetComponent<GlyphCoverController>();
            cover.Tick(1.1f);
            Assert.IsTrue(cover.IsCovered, "precondition: the badge is hidden");

            Suppress(enemy, true);

            // Not "on the next tick": a hidden window of five seconds would otherwise leave the card
            // framing a glyph the player cannot read.
            Assert.IsFalse(cover.IsCovered, "suppression withdraws the cover, it does not merely stop it");
            Assert.IsTrue(enemy.GlyphBadge.GetComponent<SpriteRenderer>().enabled);
        }

        // ------------------------------------------------------------ Bakod: the shield

        [Test]
        public void BakodShield_HoldsNobodyWhileSuppressed_AndBlocksAgainOnTheNextSpawn()
        {
            Enemy bakod = CreateEnemy(y: -5f, configure: data => data.blocksEnemiesBehind = true);
            Enemy behind = CreateEnemy(y: -1f);

            var shield = bakod.GetComponent<BakodShieldController>();
            Assert.IsNotNull(shield, "blocksEnemiesBehind should attach BakodShieldController");

            Suppress(bakod, true);
            Assert.IsTrue(shield.IsSuppressedForIntroductionSpawn,
                "Enemy must reach BakodShieldController when it applies the introduction suppression");

            shield.Tick(0.016f);
            Assert.AreEqual(0, shield.BlockedCount, "a suppressed Bakod holds nobody");
            Assert.IsFalse(behind.IsResolutionBlocked,
                "an enemy behind a suppressed Bakod must resolve normally");

            // The next spawn out of this pooled shell.
            Assert.IsTrue(bakod.Initialize(bakod.Data));
            Assert.IsFalse(shield.IsSuppressedForIntroductionSpawn,
                "Initialize restates suppression on every spawn, so a pooled shell comes back armed");

            shield.Tick(0.016f);
            Assert.IsTrue(shield.IsBlocking(behind), "the shield is live again on the next spawn");
            Assert.IsTrue(behind.IsResolutionBlocked);
        }

        [Test]
        public void BakodShield_SuppressedMidLife_ReleasesEveryHoldItOwns()
        {
            Enemy bakod = CreateEnemy(y: -5f, configure: data => data.blocksEnemiesBehind = true);
            Enemy behind = CreateEnemy(y: -1f);

            var shield = bakod.GetComponent<BakodShieldController>();
            shield.Tick(0.016f);
            Assert.IsTrue(behind.IsResolutionBlocked, "precondition: the shield is up");

            Suppress(bakod, true);

            // A hold left behind by a suppressed shield is the worst failure this ability has: an
            // unresolvable enemy with nothing visibly blocking it.
            Assert.IsFalse(behind.IsResolutionBlocked, "suppression releases the holds it owns");
            Assert.AreEqual(0, behind.ResolutionBlockCount);
            Assert.AreEqual(0, shield.BlockedCount);
        }

        // ------------------------------------------------------------- Hati: the split

        [Test]
        public void HatiSplit_SpawnsNoPiecesWhileSuppressed_AndSplitsAgainOnTheNextSpawn()
        {
            CreateEnemyPool(CreateEnemyPrefab());

            EnemyDataSO piece = CreateData("hati-minion");
            Enemy hati = CreateEnemy(y: -3f, configure: data =>
            {
                data.splitsOnDefeat = true;
                data.splitCount = 2;
                data.splitSpawnData = piece;
                data.splitOffsetX = 0.9f;
            });
            hati.AssignCharacter(CreateCharacter("HA"));

            var split = hati.GetComponent<HatiSplitController>();
            Assert.IsNotNull(split, "splitsOnDefeat should attach HatiSplitController");

            Suppress(hati, true);
            Assert.IsTrue(split.IsSuppressedForIntroductionSpawn,
                "Enemy must reach HatiSplitController when it applies the introduction suppression");

            split.SpawnOnDefeat();
            Assert.AreEqual(0, CountActive(piece),
                "defeating a suppressed Hati must not hand the player two more enemies");

            // The next spawn out of this pooled shell.
            Assert.IsTrue(hati.Initialize(hati.Data));
            Assert.IsFalse(split.IsSuppressedForIntroductionSpawn,
                "Initialize restates suppression on every spawn, so a pooled shell comes back armed");

            split.SpawnOnDefeat();
            Assert.AreEqual(2, CountActive(piece), "the split is live again on the next spawn");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Drives suppression the way the introduction beat does — through Enemy — rather than by
        /// calling the controller. A controller that grows the member without being wired into
        /// <c>ApplyIntroductionSpawnSuppression</c> fails every test that goes through here.
        /// </summary>
        private static void Suppress(Enemy enemy, bool suppressed)
        {
            MethodInfo method = typeof(Enemy).GetMethod(
                "ApplyIntroductionSpawnSuppression",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Enemy.ApplyIntroductionSpawnSuppression");
            method.Invoke(enemy, new object[] { suppressed });
        }

        private int CountActive(EnemyDataSO data)
        {
            _tracker.FillActiveEnemiesSnapshot(_snapshot);

            int count = 0;
            for (int i = 0; i < _snapshot.Count; i++)
            {
                if (_snapshot[i] != null && _snapshot[i].Data == data)
                    count++;
            }

            return count;
        }

        private Enemy CreateEnemy(float y, System.Action<EnemyDataSO> configure = null)
        {
            Enemy enemy = CreateShellEnemy(withBadge: false);
            enemy.transform.position = new Vector3(0f, y, 0f);

            EnemyDataSO data = CreateData("suppression-test");
            data.assignedCharacter = CreateCharacter("BA");
            configure?.Invoke(data);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private Enemy CreateShellEnemy(bool withBadge)
        {
            var go = new GameObject("CorruptedShell_Suppression_Test");
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

            // EditMode never runs Awake on AddComponent; drive it by hand as the sibling fixtures do.
            InvokePrivateVoid(enemy, "Awake");
            if (badge != null)
                InvokePrivateVoid(badge, "Awake");

            return enemy;
        }

        private Enemy CreateEnemyPrefab()
        {
            var prefabGo = new GameObject("EnemyPrefab_Suppression_Test");
            prefabGo.SetActive(false);
            _objectsToDestroy.Add(prefabGo);

            prefabGo.AddComponent<SpriteRenderer>();
            prefabGo.AddComponent<BoxCollider2D>();
            prefabGo.AddComponent<EnemyMover>();
            Enemy enemy = prefabGo.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            InvokePrivateVoid(enemy, "Awake");
            return enemy;
        }

        private EnemyPool CreateEnemyPool(Enemy prefab)
        {
            var poolGo = new GameObject("EnemyPool_Suppression_Test");
            poolGo.SetActive(false);
            _objectsToDestroy.Add(poolGo);

            var pool = poolGo.AddComponent<EnemyPool>();
            SetPrivateField(pool, "_enemyPrefab", prefab);
            SetPrivateField(pool, "_defaultCapacity", 0);
            SetPrivateField(pool, "_maxSize", 8);
            poolGo.SetActive(true);
            InvokePrivateVoid(pool, "Awake");
            return pool;
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateVoid(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
