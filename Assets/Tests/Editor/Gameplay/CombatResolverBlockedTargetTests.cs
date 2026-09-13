using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-286 (Bakod): enemies behind a live Bakod cannot be resolved.
    ///
    /// <para>
    /// These assert <b>observable combat outcomes</b> — a shielded enemy keeps its health while an
    /// unshielded one loses it — never merely that a flag was read. An ability that is wired but
    /// inert would pass the second kind of test and fail these.
    /// </para>
    ///
    /// <para>
    /// <c>AOE_ExcludesEnemiesBehindBakod</c> is the important one: <c>IsEligibleCombatTarget</c> has
    /// five call sites, and a filter written at the single-target site alone would still pass every
    /// single-target test here while leaking through the AOE and chain paths in the shipped game.
    /// That test drives a different path from the one the single-target tests drive.
    /// </para>
    /// </summary>
    [TestFixture]
    public class CombatResolverBlockedTargetTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            var trackerGo = new GameObject("ActiveEnemyTracker_Bakod_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<GameManager>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void SingleTarget_EnemyBehindBakod_IsNotResolved_AndKeepsItsHealth()
        {
            BaybayinCharacterSO shieldedSymbol = CreateCharacter("BA", "ba");
            // Lower Y is nearer the base, so "behind Bakod" is "greater Y than Bakod".
            Enemy bakod = CreateBakod(CreateCharacter("KA", "ka"), y: -5f);
            Enemy shielded = CreateEnemy(shieldedSymbol, y: -1f);

            TickShield(bakod);
            Assert.IsTrue(shielded.IsResolutionBlocked, "precondition: Bakod shields the enemy behind it");

            CombatResolver resolver = CreateResolver();
            bool missed = false;
            EventBus.OnDrawingMissed += HandleMissed;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", shieldedSymbol.characterID);

                // The assertion the negative control must break: the shielded enemy survives a
                // correct draw of its own symbol.
                Assert.AreEqual(1, shielded.CurrentHealth,
                    "an enemy behind a live Bakod must not be damaged by drawing its symbol");
                Assert.IsTrue(missed, "the draw finds no eligible target, so it reads as a miss");
            }
            finally
            {
                EventBus.OnDrawingMissed -= HandleMissed;
            }

            void HandleMissed() => missed = true;
        }

        [Test]
        public void SingleTarget_BakodItself_StaysResolvable()
        {
            BaybayinCharacterSO bakodSymbol = CreateCharacter("KA", "ka");
            Enemy bakod = CreateBakod(bakodSymbol, y: -5f);
            Enemy shielded = CreateEnemy(CreateCharacter("BA", "ba"), y: -1f);

            TickShield(bakod);
            Assert.IsTrue(shielded.IsResolutionBlocked, "precondition");
            Assert.IsFalse(bakod.IsResolutionBlocked, "Bakod must never shield itself");

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", bakodSymbol.characterID);

            Assert.AreEqual(0, bakod.CurrentHealth,
                "the shield must leave a route through it — Bakod itself stays resolvable");
        }

        [Test]
        public void AOE_ExcludesEnemiesBehindBakod()
        {
            // Drives the AOE collection and burst paths (CombatResolver.cs:139 and :160), which are
            // different call sites from the single-target path the tests above drive. A Bakod filter
            // implemented at the single-target call site would pass those and fail this.
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy bakod = CreateBakod(CreateCharacter("KA", "ka"), y: -4f);

            Enemy inFront1 = CreateEnemy(assigned, y: -7f);
            Enemy inFront2 = CreateEnemy(assigned, y: -6f);
            Enemy inFront3 = CreateEnemy(assigned, y: -5f);
            Enemy behind = CreateEnemy(assigned, y: -1f);

            TickShield(bakod);
            Assert.IsTrue(behind.IsResolutionBlocked, "precondition");
            Assert.IsFalse(inFront1.IsResolutionBlocked, "enemies past Bakod are not shielded");

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

            Assert.AreEqual(0, inFront1.CurrentHealth);
            Assert.AreEqual(0, inFront2.CurrentHealth);
            Assert.AreEqual(0, inFront3.CurrentHealth);
            Assert.AreEqual(1, behind.CurrentHealth,
                "the AOE burst must not reach through Bakod");
        }

        [Test]
        public void Shield_Lifts_WhenBakodDies_AndTheEnemyResolvesAgain()
        {
            BaybayinCharacterSO shieldedSymbol = CreateCharacter("BA", "ba");
            Enemy bakod = CreateBakod(CreateCharacter("KA", "ka"), y: -5f);
            Enemy shielded = CreateEnemy(shieldedSymbol, y: -1f);

            TickShield(bakod);
            Assert.IsTrue(shielded.IsResolutionBlocked, "precondition");

            // Bakod starts dying; the next tick must drop every hold it owns.
            SetPrivateField(bakod, "_isDying", true);
            TickShield(bakod);
            Assert.IsFalse(shielded.IsResolutionBlocked, "defeating Bakod lifts the shield");

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", shieldedSymbol.characterID);

            Assert.AreEqual(0, shielded.CurrentHealth,
                "once Bakod falls the previously shielded enemy resolves normally");
        }

        [Test]
        public void Shield_ReleasesEveryHold_WhenTheControllerIsDisabled()
        {
            // Pool safety. A Bakod recycled while holding blocks would otherwise strand a
            // permanently unresolvable enemy on screen: no exception, no failing suite, an
            // unfinishable level.
            BaybayinCharacterSO shieldedSymbol = CreateCharacter("BA", "ba");
            Enemy bakod = CreateBakod(CreateCharacter("KA", "ka"), y: -5f);
            Enemy shielded = CreateEnemy(shieldedSymbol, y: -1f);

            BakodShieldController shield = TickShield(bakod);
            Assert.IsTrue(shielded.IsResolutionBlocked, "precondition");

            // EditMode does not run Unity's enable/disable callbacks, so drive it directly.
            InvokePrivateVoid(shield, "OnDisable");

            Assert.IsFalse(shielded.IsResolutionBlocked, "a disabled shield holds nothing");
            Assert.AreEqual(0, shielded.ResolutionBlockCount);

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", shieldedSymbol.characterID);
            Assert.AreEqual(0, shielded.CurrentHealth,
                "a released enemy is resolvable again");
        }

        [Test]
        public void ResetForPool_ClearsResolutionBlocks_SoARecycledShellIsNeverBornBlocked()
        {
            Enemy bakod = CreateBakod(CreateCharacter("KA", "ka"), y: -5f);
            Enemy shielded = CreateEnemy(CreateCharacter("BA", "ba"), y: -1f);

            TickShield(bakod);
            Assert.IsTrue(shielded.IsResolutionBlocked, "precondition");

            shielded.ResetForPool();

            Assert.IsFalse(shielded.IsResolutionBlocked,
                "a shell returned to the pool must not carry a block back into play");
            Assert.AreEqual(0, shielded.ResolutionBlockCount);
        }

        [Test]
        public void ResolutionBlocks_AreRefCountedBySource_SoTwoAbilitiesCompose()
        {
            // The seam is deliberately neutral and multi-source: SALIN-287 (Kadena) is meant to add
            // a second block source without touching resolver code. Releasing one source must not
            // release another's hold.
            Enemy enemy = CreateEnemy(CreateCharacter("BA", "ba"), y: -1f);
            var firstSource = new object();
            var secondSource = new object();

            enemy.AddResolutionBlock(firstSource);
            enemy.AddResolutionBlock(firstSource);
            Assert.AreEqual(1, enemy.ResolutionBlockCount, "a source re-asserting its own block does not stack");

            enemy.AddResolutionBlock(secondSource);
            Assert.AreEqual(2, enemy.ResolutionBlockCount);

            enemy.RemoveResolutionBlock(firstSource);
            Assert.IsTrue(enemy.IsResolutionBlocked,
                "the second source still holds it, so it stays blocked");

            enemy.RemoveResolutionBlock(secondSource);
            Assert.IsFalse(enemy.IsResolutionBlocked);
        }

        [Test]
        public void BlockedEnemy_CarriesAVisibleTell_ThatClearsWhenTheBlockLifts()
        {
            // Criterion 3: a blocked enemy must not read as a bug. The tell is the placeholder dim
            // on EnemyGlyphBadge (see SetResolutionBlocked) pending authored art.
            Enemy enemy = CreateEnemy(CreateCharacter("BA", "ba"), y: -1f, withBadge: true);
            EnemyGlyphBadge badge = enemy.GlyphBadge;
            Assert.IsNotNull(badge, "precondition: this enemy has a badge");

            SpriteRenderer badgeRenderer = badge.GetComponent<SpriteRenderer>();
            Color unblocked = badgeRenderer.color;

            var source = new object();
            enemy.AddResolutionBlock(source);

            Assert.IsTrue(badge.IsResolutionBlocked, "the badge is told about the block");
            Assert.AreNotEqual(unblocked, badgeRenderer.color,
                "a blocked enemy must look different from an unblocked one");
            Assert.IsFalse(badge.IsCovered,
                "the blocked tell must not read as a Takip cover — a different ability");

            enemy.RemoveResolutionBlock(source);
            Assert.IsFalse(badge.IsResolutionBlocked);
            Assert.AreEqual(unblocked, badgeRenderer.color, "the tell clears with the block");
        }

        // ---------------------------------------------------------------- helpers

        private BaybayinCharacterSO CreateCharacter(string id, string syllable)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.syllable = syllable;
            _objectsToDestroy.Add(character);
            return character;
        }

        private Enemy CreateEnemy(
            BaybayinCharacterSO assigned,
            float y,
            bool blocksEnemiesBehind = false,
            bool withBadge = false)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.assignedCharacter = assigned;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.blocksEnemiesBehind = blocksEnemiesBehind;
            _objectsToDestroy.Add(data);

            var go = new GameObject("Enemy_Bakod_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(0f, y, 0f);
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

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private Enemy CreateBakod(BaybayinCharacterSO assigned, float y)
            => CreateEnemy(assigned, y, blocksEnemiesBehind: true);

        /// <summary>Drives one shield evaluation and returns the controller Enemy.Initialize attached.</summary>
        private static BakodShieldController TickShield(Enemy bakod)
        {
            var shield = bakod.GetComponent<BakodShieldController>();
            Assert.IsNotNull(shield, "blocksEnemiesBehind should attach BakodShieldController");
            Assert.IsTrue(shield.enabled);
            shield.Tick(0.016f);
            return shield;
        }

        private CombatResolver CreateResolver()
        {
            var go = new GameObject("CombatResolver_Bakod_Test");
            _objectsToDestroy.Add(go);
            return go.AddComponent<CombatResolver>();
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
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }

        private static void InvokePrivateVoid(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
