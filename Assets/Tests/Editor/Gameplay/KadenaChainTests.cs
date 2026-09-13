using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-287 (Kadena): on spawn Kadena chains the nearest other enemy, and the chained enemy
    /// can be neither damaged nor marked until Kadena is defeated.
    ///
    /// <para>
    /// These assert <b>observable combat outcomes</b> — a chained enemy keeps its health through a
    /// correct draw of its own symbol, and loses it again once Kadena falls — never merely that a
    /// flag was set. An ability that is wired but inert passes a flag-reading test and fails these.
    /// Both directions are asserted deliberately: a chain that never lifts is worse than no chain.
    /// </para>
    ///
    /// <para>
    /// <c>AOE_ExcludesTheChainedEnemy</c> is the control SALIN-286 proved necessary: a filter
    /// written at the single-target call site alone left seven of eight tests green and only the
    /// AOE case failed.
    /// </para>
    /// </summary>
    [TestFixture]
    public class KadenaChainTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            var trackerGo = new GameObject("ActiveEnemyTracker_Kadena_Test");
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
        public void Chain_HoldsTheNearestOtherEnemy_WhichThenKeepsItsHealthThroughAResolve()
        {
            BaybayinCharacterSO chainedSymbol = CreateCharacter("BA", "ba");
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: -5f);
            Enemy chained = CreateEnemy(chainedSymbol, x: 0f, y: -4f);

            Assert.AreEqual(2, kadena.CurrentHealth, "Kadena keeps hp 2 (AUDIT.md:467 Note)");

            KadenaChainController chain = TickChain(kadena);
            Assert.AreSame(chained, chain.ChainedEnemy, "the only other enemy is the chained one");
            Assert.IsTrue(chained.IsResolutionBlocked, "precondition: the chained enemy is held");

            CombatResolver resolver = CreateResolver();
            bool missed = false;
            EventBus.OnDrawingMissed += HandleMissed;

            try
            {
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", chainedSymbol.characterID);

                // The assertion the negative control must break: the chained enemy survives a
                // correct draw of its own symbol.
                Assert.AreEqual(1, chained.CurrentHealth,
                    "an enemy chained by a live Kadena must not be damaged by drawing its symbol");
                Assert.IsTrue(missed, "the draw finds no eligible target, so it reads as a miss");
            }
            finally
            {
                EventBus.OnDrawingMissed -= HandleMissed;
            }

            void HandleMissed() => missed = true;
        }

        [Test]
        public void Chain_PicksTheNearestCandidate_NotMerelyTheFirstRegistered()
        {
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: 0f);
            // Registration order is creation order, so the far enemy is first in the snapshot: a
            // controller that took the first candidate would pass with this one and fail the next.
            Enemy far = CreateEnemy(CreateCharacter("BA", "ba"), x: 0f, y: 5f);
            Enemy near = CreateEnemy(CreateCharacter("DA", "da"), x: 0f, y: 1f);

            KadenaChainController chain = TickChain(kadena);

            Assert.AreSame(near, chain.ChainedEnemy, "the nearest other enemy is chained");
            Assert.IsTrue(near.IsResolutionBlocked);
            Assert.IsFalse(far.IsResolutionBlocked, "only one enemy is chained");
        }

        [Test]
        public void Chain_TiesResolveByLowestYThenSpawnSequence()
        {
            // D-011: equidistant candidates resolve deterministically, or the choice follows float
            // noise and this test is flaky in one direction and silently wrong in the other.
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: 0f);

            Enemy level = CreateEnemy(CreateCharacter("BA", "ba"), x: 3f, y: 0f);
            Enemy lower = CreateEnemy(CreateCharacter("DA", "da"), x: 0f, y: -3f);

            KadenaChainController chain = TickChain(kadena);
            Assert.AreSame(lower, chain.ChainedEnemy,
                "equidistant candidates resolve to the lower Y — nearer the base — first");

            // Second tiebreak: same distance AND same Y, so the lower SpawnSequence wins. The pair
            // above leaves play and Kadena is re-initialized, which is also the pooled-reuse path.
            level.gameObject.SetActive(false);
            lower.gameObject.SetActive(false);
            Enemy earlyRegistered = CreateEnemy(CreateCharacter("HA", "ha"), x: -3f, y: -2f);
            Enemy lateRegistered = CreateEnemy(CreateCharacter("WA", "wa"), x: 3f, y: -2f);

            // Re-initializing the earlier-registered enemy gives it the HIGHER SpawnSequence, so
            // iteration order and spawn order now disagree. A controller that merely keeps the
            // first candidate it walks past would pass a same-order fixture and fail this one.
            Assert.IsTrue(earlyRegistered.Initialize(earlyRegistered.Data));
            Assert.Less(lateRegistered.SpawnSequence, earlyRegistered.SpawnSequence, "precondition");

            Assert.IsTrue(kadena.Initialize(kadena.Data), "re-initialize Kadena for a fresh spawn");
            chain.Tick(0.016f);

            Assert.AreSame(lateRegistered, chain.ChainedEnemy,
                "with distance and Y tied, the lower SpawnSequence wins regardless of walk order");
        }

        [Test]
        public void AOE_ExcludesTheChainedEnemy()
        {
            // Drives the AOE collection and burst paths, which are different call sites from the
            // single-target path the tests above drive. This is the case that caught SALIN-286's
            // mis-placed filter.
            BaybayinCharacterSO assigned = CreateCharacter("BA", "ba");
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: -4f);

            Enemy chained = CreateEnemy(assigned, x: 0f, y: -3.5f); // nearest to Kadena
            Enemy free1 = CreateEnemy(assigned, x: 0f, y: -7f);
            Enemy free2 = CreateEnemy(assigned, x: 0f, y: -6f);
            Enemy free3 = CreateEnemy(assigned, x: 0f, y: -5f);

            KadenaChainController chain = TickChain(kadena);
            Assert.AreSame(chained, chain.ChainedEnemy, "precondition");
            Assert.IsFalse(free1.IsResolutionBlocked, "the other enemies are untouched");

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", assigned.characterID);

            Assert.AreEqual(0, free1.CurrentHealth);
            Assert.AreEqual(0, free2.CurrentHealth);
            Assert.AreEqual(0, free3.CurrentHealth);
            Assert.AreEqual(1, chained.CurrentHealth,
                "the AOE burst must not reach the chained enemy");
        }

        [Test]
        public void ChainedEnemy_IsNotOfferedAsAnActiveClue()
        {
            // Wider than the word "invulnerable", and required: a chained enemy that can be marked
            // but not damaged hands the player a target they cannot resolve — exactly the
            // "I drew it and nothing happened" bug the ticket's design note warns against.
            // ActiveClueDirector.cs:214-219 makes the same argument for Bakod.
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: -5f);
            Enemy chained = CreateEnemy(CreateCharacter("BA", "ba"), x: 0f, y: -4f);

            Assert.IsTrue(IsEligibleClue(chained), "precondition: eligible before the chain lands");

            TickChain(kadena);

            Assert.IsTrue(chained.IsResolutionBlocked);
            Assert.IsFalse(IsEligibleClue(chained), "a chained enemy is never offered as the clue");
        }

        [Test]
        public void Chain_Lifts_WhenKadenaDies_AndTheEnemyResolvesAgain()
        {
            // The release path. A block that never clears is worse than no block: it strands an
            // unresolvable enemy and the level cannot be finished.
            BaybayinCharacterSO chainedSymbol = CreateCharacter("BA", "ba");
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: -5f);
            Enemy chained = CreateEnemy(chainedSymbol, x: 0f, y: -4f);

            KadenaChainController chain = TickChain(kadena);
            Assert.IsTrue(chained.IsResolutionBlocked, "precondition");

            SetPrivateField(kadena, "_isDying", true);
            chain.Tick(0.016f);

            Assert.IsFalse(chained.IsResolutionBlocked, "defeating Kadena lifts the chain");
            Assert.AreEqual(0, chained.ResolutionBlockCount);
            Assert.IsNull(chain.ChainedEnemy);

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", chainedSymbol.characterID);

            Assert.AreEqual(0, chained.CurrentHealth,
                "once Kadena falls the previously chained enemy resolves normally");
        }

        [Test]
        public void Chain_ReleasesItsHold_WhenTheControllerIsDisabled()
        {
            // Pool safety. A Kadena recycled while still holding its chain strands a permanently
            // unresolvable enemy: no exception, no failing suite, an unfinishable level.
            BaybayinCharacterSO chainedSymbol = CreateCharacter("BA", "ba");
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: -5f);
            Enemy chained = CreateEnemy(chainedSymbol, x: 0f, y: -4f);

            KadenaChainController chain = TickChain(kadena);
            Assert.IsTrue(chained.IsResolutionBlocked, "precondition");

            // EditMode does not run Unity's enable/disable callbacks, so drive it directly.
            InvokePrivateVoid(chain, "OnDisable");

            Assert.IsFalse(chained.IsResolutionBlocked, "a disabled chain holds nothing");
            Assert.AreEqual(0, chained.ResolutionBlockCount);

            CombatResolver resolver = CreateResolver();
            SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
            InvokePrivate<object>(resolver, "HandleCharacterRecognized", chainedSymbol.characterID);
            Assert.AreEqual(0, chained.CurrentHealth, "a released enemy is resolvable again");
        }

        [Test]
        public void ChainedSalungat_CostsNoHeart_BecauseTheBlockPrecedesTheDecoyBranch()
        {
            // Emergent and reachable in play: Level7_Config.asset wave 1 lists Salungat and Kadena
            // as its only two enemy types. The eligibility filter (CombatResolver.cs:331) runs
            // before the decoy branch (:347), so drawing a chained decoy's symbol costs nothing.
            // Recorded as a design consequence for the owner, and pinned here so it cannot change
            // silently in either direction.
            BaybayinCharacterSO decoySymbol = CreateCharacter("BA", "ba");
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: -5f);
            Enemy decoy = CreateEnemy(decoySymbol, x: 0f, y: -4f, isDecoy: true);

            KadenaChainController chain = TickChain(kadena);
            Assert.IsTrue(decoy.IsResolutionBlocked, "precondition");

            int baseHits = 0;
            EventBus.OnBaseHit += HandleBaseHit;

            try
            {
                CombatResolver resolver = CreateResolver();
                SetPrivateField(resolver, "_pronunciationLeadSeconds", 0f);
                InvokePrivate<object>(resolver, "HandleCharacterRecognized", decoySymbol.characterID);

                Assert.AreEqual(0, baseHits, "a chained decoy is inert in both directions");
                Assert.IsFalse(decoy.IsDying, "the decoy penalty never ran");

                // Control: the same draw against the same decoy once the chain lifts does cost a
                // heart, so the assertion above is about the chain and not about a broken fixture.
                SetPrivateField(kadena, "_isDying", true);
                chain.Tick(0.016f);
                Assert.IsFalse(decoy.IsResolutionBlocked, "precondition for the control");

                // A fresh resolver for the control draw: the SALIN-135 echo gate
                // (CombatResolver.cs:110, :205-214) treats a second draw of the same symbol on the
                // same instance as one attempt and swallows it.
                CombatResolver controlResolver = CreateResolver();
                SetPrivateField(controlResolver, "_pronunciationLeadSeconds", 0f);
                InvokePrivate<object>(controlResolver, "HandleCharacterRecognized", decoySymbol.characterID);
                Assert.AreEqual(1, baseHits, "an unchained decoy still costs a heart");
            }
            finally
            {
                EventBus.OnBaseHit -= HandleBaseHit;
            }

            void HandleBaseHit(int damage) => baseHits += damage;
        }

        [Test]
        public void Kadena_SpawnedAlone_ChainsTheFirstEnemyToArrive_AndThenNeverReTargets()
        {
            // "On spawn" read literally no-ops whenever Kadena is the first enemy of its wave
            // (Level7_Config.asset wave 1 spawns 5 enemies at 2.2 s intervals). Acquisition happens
            // on the first tick a candidate exists, and is frozen from then on.
            Enemy kadena = CreateKadena(CreateCharacter("KA", "ka"), x: 0f, y: 0f);

            KadenaChainController chain = TickChain(kadena);
            Assert.IsFalse(chain.IsChaining, "alone on screen, Kadena has nothing to chain yet");

            Enemy firstArrival = CreateEnemy(CreateCharacter("BA", "ba"), x: 0f, y: 8f);
            chain.Tick(0.016f);
            Assert.AreSame(firstArrival, chain.ChainedEnemy,
                "the chain is acquired on the first tick a candidate exists, not silently skipped");

            // A nearer enemy arriving later must not steal the chain: one target, held for life.
            Enemy nearerLater = CreateEnemy(CreateCharacter("DA", "da"), x: 0f, y: 1f);
            chain.Tick(0.016f);

            Assert.AreSame(firstArrival, chain.ChainedEnemy, "Kadena never re-targets");
            Assert.IsFalse(nearerLater.IsResolutionBlocked);
            Assert.IsTrue(firstArrival.IsResolutionBlocked);
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
            float x,
            float y,
            bool chainsNearestEnemy = false,
            bool isDecoy = false,
            int maxHealth = 1)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.assignedCharacter = assigned;
            data.maxHealth = maxHealth;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.isDecoy = isDecoy;
            data.chainsNearestEnemy = chainsNearestEnemy;
            _objectsToDestroy.Add(data);

            var go = new GameObject("Enemy_Kadena_Test");
            go.SetActive(false);
            go.transform.position = new Vector3(x, y, 0f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);

            go.SetActive(true);
            _objectsToDestroy.Add(go);

            // EditMode never runs Awake on AddComponent; drive it by hand as the sibling fixtures do.
            InvokePrivateVoid(enemy, "Awake");

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private Enemy CreateKadena(BaybayinCharacterSO assigned, float x, float y)
            => CreateEnemy(assigned, x, y, chainsNearestEnemy: true, maxHealth: 2);

        /// <summary>Drives one chain evaluation and returns the controller Enemy.Initialize attached.</summary>
        private static KadenaChainController TickChain(Enemy kadena)
        {
            var chain = kadena.GetComponent<KadenaChainController>();
            Assert.IsNotNull(chain, "chainsNearestEnemy should attach KadenaChainController");
            Assert.IsTrue(chain.enabled);
            chain.Tick(0.016f);
            return chain;
        }

        private CombatResolver CreateResolver()
        {
            var go = new GameObject("CombatResolver_Kadena_Test");
            _objectsToDestroy.Add(go);
            return go.AddComponent<CombatResolver>();
        }

        private static bool IsEligibleClue(Enemy enemy)
        {
            MethodInfo method = typeof(ActiveClueDirector).GetMethod(
                "IsEligibleClue", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing ActiveClueDirector.IsEligibleClue.");
            return (bool)method.Invoke(null, new object[] { enemy });
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
