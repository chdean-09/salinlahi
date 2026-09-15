using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Onboarding
{
    /// <summary>
    /// The heart-loss demo's one promise: it costs nothing.
    ///
    /// <para>
    /// <b>What actually happened.</b> The demo spawns a real pooled enemy on top of the shrine so
    /// the player can watch a heart empty, then removed it with the ordinary combat defeat. Level
    /// 1's demo enemy is Hati, whose data carries <c>splitsOnDefeat</c> with <c>splitCount 2</c>, so
    /// that defeat put two live minions on the base and each raised a genuine
    /// <c>EventBus.OnBaseHit</c>. Measured live: <c>_currentHearts = 1</c> of 3 before the player's
    /// first real combat, with the HUD drawing two filled hearts over it. The existing tests proved
    /// the demo EVENT does not decrement hearts — which was true, and was never the leak.
    /// </para>
    /// </summary>
    [TestFixture]
    public class HeartLossDemoAccountingTests
    {
        private bool _previousIgnoreFailingMessages;

        [SetUp]
        public void SetUp()
        {
            // The guards under test warn on purpose — a silent swallow would hide the next version
            // of this bug — and DebugLogger.LogWarning is [Conditional], so whether the warning
            // reaches the log at all depends on the build's defines. Neither expecting it nor
            // failing on it is a fact worth asserting here; the heart COUNT is.
            _previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;
            TutorialRuntimeState.Clear();
        }

        [Test]
        public void DemoWindowIsClosedByDefault()
        {
            TutorialRuntimeState.Clear();
            Assert.IsFalse(TutorialRuntimeState.IsHeartLossDemoActive);
        }

        [Test]
        public void DemoWindowIsNotGatedOnTheTutorialHavingStarted()
        {
            // The demo runs before TutorialRuntimeState.Begin has been called for the level, unlike
            // the combat-override and input-lock flags. A guard that only arms once something else
            // is true is not a guard.
            TutorialRuntimeState.Clear();
            TutorialRuntimeState.SetHeartLossDemoActive(true);

            Assert.IsFalse(TutorialRuntimeState.IsActive);
            Assert.IsTrue(TutorialRuntimeState.IsHeartLossDemoActive);
        }

        [Test]
        public void ClearClosesTheDemoWindow()
        {
            TutorialRuntimeState.SetHeartLossDemoActive(true);
            TutorialRuntimeState.Clear();

            Assert.IsFalse(TutorialRuntimeState.IsHeartLossDemoActive);
        }

        [Test]
        public void RealBaseHitDuringTheDemo_CostsNoHearts()
        {
            GameObject host = new GameObject("ShrineHost");
            try
            {
                HeartSystem hearts = host.AddComponent<HeartSystem>();
                PlayerBase playerBase = host.AddComponent<PlayerBase>();
                InvokeLifecycle(playerBase, "Awake");
                InvokeLifecycle(hearts, "Awake");
                InvokeLifecycle(playerBase, "OnEnable");

                int before = hearts.GetCurrentHearts();
                TutorialRuntimeState.SetHeartLossDemoActive(true);

                // Two hits: exactly what Hati's two split minions delivered on the shrine.
                EventBus.RaiseBaseHit(1);
                EventBus.RaiseBaseHit(1);

                Assert.AreEqual(before, hearts.GetCurrentHearts(),
                    "Nothing may reach HeartSystem while the heart-loss demo owns the field.");
            }
            finally
            {
                InvokeLifecycle(host.GetComponent<PlayerBase>(), "OnDisable");
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RealBaseHitOutsideTheDemo_StillCostsAHeart()
        {
            // The guard must be a window, not a switch. A demo that turned real damage off for the
            // rest of the level would be a worse bug than the one it fixes.
            GameObject host = new GameObject("ShrineHost");
            try
            {
                HeartSystem hearts = host.AddComponent<HeartSystem>();
                PlayerBase playerBase = host.AddComponent<PlayerBase>();
                InvokeLifecycle(playerBase, "Awake");
                InvokeLifecycle(hearts, "Awake");
                InvokeLifecycle(playerBase, "OnEnable");

                int before = hearts.GetCurrentHearts();
                TutorialRuntimeState.SetHeartLossDemoActive(false);
                EventBus.RaiseBaseHit(1);

                Assert.AreEqual(before - 1, hearts.GetCurrentHearts());
            }
            finally
            {
                InvokeLifecycle(host.GetComponent<PlayerBase>(), "OnDisable");
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HeartDisplayNeverDrawsAFilledHeartToTheRightOfAnEmptyOne()
        {
            // The HUD drew RED / GREY / RED while the model held one heart: the demo emptied the
            // rightmost slot, real damage landed underneath it, and the demo's restore then lit its
            // remembered slot back up regardless of what the model said.
            GameObject displayHost = new GameObject("HeartDisplayHost");
            GameObject shrineHost = new GameObject("ShrineHost");
            GameObject[] heartObjects = new GameObject[3];
            try
            {
                HeartSystem hearts = shrineHost.AddComponent<HeartSystem>();
                InvokeLifecycle(hearts, "Awake");

                HeartDisplay display = displayHost.AddComponent<HeartDisplay>();
                Image[] icons = new Image[3];
                for (int i = 0; i < icons.Length; i++)
                {
                    heartObjects[i] = new GameObject($"Heart_{i}", typeof(RectTransform), typeof(Image));
                    heartObjects[i].transform.SetParent(displayHost.transform, false);
                    icons[i] = heartObjects[i].GetComponent<Image>();
                }

                typeof(HeartDisplay)
                    .GetField("_heartIcons", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(display, icons);

                InvokeLifecycle(display, "OnEnable");

                EventBus.RaiseHeartsChanged(3);
                EventBus.RaiseTutorialBaseHitDemo(1);

                // Real damage lands inside the demo window, the way the split minions used to.
                hearts.LoseHeart(2);

                EventBus.RaiseTutorialBaseRestoreDemo();

                AssertHeartsFillFromTheLeft(icons, hearts.GetCurrentHearts());
            }
            finally
            {
                InvokeLifecycle(displayHost.GetComponent<HeartDisplay>(), "OnDisable");
                Object.DestroyImmediate(displayHost);
                Object.DestroyImmediate(shrineHost);
                DestroyRuntimeObject("TutorialHeartDamageOverlay");
                for (int i = 0; i < heartObjects.Length; i++)
                {
                    if (heartObjects[i] != null)
                        Object.DestroyImmediate(heartObjects[i]);
                }
            }
        }

        private static void AssertHeartsFillFromTheLeft(Image[] icons, int expectedFilled)
        {
            Color filled = Color.red;
            bool seenEmpty = false;
            int filledCount = 0;

            for (int i = 0; i < icons.Length; i++)
            {
                bool isFilled = icons[i].color == filled;
                if (isFilled)
                {
                    filledCount++;
                    Assert.IsFalse(seenEmpty,
                        $"Heart {i} is filled but an emptier heart sits to its left. The row must "
                        + "read left to right.");
                }
                else
                {
                    seenEmpty = true;
                }
            }

            Assert.AreEqual(expectedFilled, filledCount,
                "The HUD must draw exactly as many hearts as the model holds.");
        }

        private static void InvokeLifecycle(MonoBehaviour target, string methodName)
        {
            if (target == null)
                return;
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
                method.Invoke(target, null);
        }

        private static void DestroyRuntimeObject(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }
}
