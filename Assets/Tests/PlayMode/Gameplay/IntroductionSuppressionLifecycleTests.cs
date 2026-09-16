using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// Ugat QA 2026-09-16. The EditMode suite covers the path that must hold for a pooled respawn -
    /// Enemy.Initialize restating suppression on every spawn - but it cannot reach the SECOND
    /// clearing path, because EditMode never fires OnEnable. That path is what catches a shell that
    /// is returned to the pool while suppressed and then handed out again: if OnEnable did not
    /// clear the flag, that enemy would be permanently inert, which is worse than the bug the
    /// suppression fixed.
    ///
    /// PlayMode, not EditMode, for exactly that reason: these tests deactivate and reactivate the
    /// GameObject, which is what EnemyPool.OnGet does, and assert the flag cleared on its own.
    /// </summary>
    [TestFixture]
    public sealed class IntroductionSuppressionLifecycleTests
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

        /// <summary>
        /// The controllers resolve their Enemy in Awake and their Update runs every frame in
        /// PlayMode, so a bare GameObject with only the controller on it throws. This builds the
        /// same shell the EditMode fixture does - the shared "[Enemy] Corrupted" shape - but lets
        /// Unity run the lifecycle instead of invoking Awake by hand.
        /// </summary>
        private T CreateControllerOnAnEnemyShell<T>() where T : MonoBehaviour
        {
            var go = new GameObject(typeof(T).Name + "_LifecycleTest");
            go.SetActive(false);                     // build it before any callback runs
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            go.AddComponent<Enemy>();

            var badgeGo = new GameObject("GlyphBadge");
            badgeGo.transform.SetParent(go.transform, false);
            badgeGo.AddComponent<SpriteRenderer>();
            badgeGo.AddComponent<EnemyGlyphBadge>();

            T controller = go.AddComponent<T>();
            _objectsToDestroy.Add(go);
            go.SetActive(true);                      // Awake + OnEnable fire here, for real
            return controller;
        }

        [UnityTest]
        public IEnumerator GlyphCover_ClearsSuppressionWhenTheShellIsReEnabled()
        {
            yield return AssertClearsOnReEnable(CreateControllerOnAnEnemyShell<GlyphCoverController>(),
                c => c.SetSuppressedForIntroductionSpawn(true),
                c => c.IsSuppressedForIntroductionSpawn);
        }

        [UnityTest]
        public IEnumerator BakodShield_ClearsSuppressionWhenTheShellIsReEnabled()
        {
            // The highest-risk of the three: this class had no OnEnable at all before the fix.
            yield return AssertClearsOnReEnable(CreateControllerOnAnEnemyShell<BakodShieldController>(),
                c => c.SetSuppressedForIntroductionSpawn(true),
                c => c.IsSuppressedForIntroductionSpawn);
        }

        [UnityTest]
        public IEnumerator HatiSplit_ClearsSuppressionWhenTheShellIsReEnabled()
        {
            yield return AssertClearsOnReEnable(CreateControllerOnAnEnemyShell<HatiSplitController>(),
                c => c.SetSuppressedForIntroductionSpawn(true),
                c => c.IsSuppressedForIntroductionSpawn);
        }

        private static IEnumerator AssertClearsOnReEnable<T>(
            T controller,
            System.Action<T> suppress,
            System.Func<T, bool> isSuppressed) where T : MonoBehaviour
        {
            yield return null;   // let Awake/OnEnable run for the initial activation

            suppress(controller);
            Assert.IsTrue(isSuppressed(controller),
                typeof(T).Name + " did not take the suppression in the first place.");

            // Exactly what EnemyPool does with a shell: park it, then hand it out again.
            controller.gameObject.SetActive(false);
            yield return null;
            controller.gameObject.SetActive(true);
            yield return null;

            Assert.IsFalse(isSuppressed(controller),
                typeof(T).Name + " stayed suppressed after the shell was re-enabled. A pooled "
                + "enemy handed out again would have this ability permanently inert - a worse "
                + "failure than the unsuppressed introduction this was added to fix.");
        }
    }
}
