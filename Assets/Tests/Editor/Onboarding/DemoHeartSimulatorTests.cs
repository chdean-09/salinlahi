using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class DemoHeartSimulatorTests
    {
        [Test]
        public void TutorialBaseHitDemo_RaisesEvent()
        {
            int received = -1;
            System.Action<int> handler = damage => received = damage;
            EventBus.OnTutorialBaseHitDemo += handler;
            try
            {
                EventBus.RaiseTutorialBaseHitDemo(2);
                Assert.AreEqual(2, received);
            }
            finally
            {
                EventBus.OnTutorialBaseHitDemo -= handler;
            }
        }

        [Test]
        public void TutorialBaseHitDemo_DoesNotInvokeOnBaseHitListeners()
        {
            int realBaseHits = 0;
            System.Action<int> realListener = _ => realBaseHits++;
            EventBus.OnBaseHit += realListener;
            try
            {
                EventBus.RaiseTutorialBaseHitDemo(1);
                Assert.AreEqual(0, realBaseHits, "Demo event must not be observed by OnBaseHit subscribers.");
            }
            finally
            {
                EventBus.OnBaseHit -= realListener;
            }
        }

        [Test]
        public void HeartSystem_DoesNotDecrement_OnTutorialDemoEvent()
        {
            GameObject host = new GameObject("HeartSystemHost");
            try
            {
                HeartSystem hearts = host.AddComponent<HeartSystem>();
                int initial = hearts.GetCurrentHearts();
                EventBus.RaiseTutorialBaseHitDemo(1);
                Assert.AreEqual(initial, hearts.GetCurrentHearts(),
                    "HeartSystem must not decrement when the tutorial demo event is raised.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HeartDisplay_TutorialDemoHit_UsesColorFallbackWhenSpritesAreMissing()
        {
            GameObject displayHost = new GameObject("HeartDisplayHost");
            GameObject[] heartObjects = new GameObject[3];
            try
            {
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

                // EditMode never runs OnEnable; subscribe to EventBus by hand.
                InvokeLifecycle(display, "OnEnable");

                EventBus.RaiseHeartsChanged(3);
                Assert.AreEqual(Color.red, icons[2].color);

                EventBus.RaiseTutorialBaseHitDemo(1);
                Assert.AreEqual(new Color(1f, 1f, 1f, 0.25f), icons[2].color);

                EventBus.RaiseTutorialBaseRestoreDemo();
                Assert.AreEqual(Color.red, icons[2].color);
            }
            finally
            {
                // Unsubscribe even on assert failure so EventBus never keeps a
                // destroyed HeartDisplay registered for later tests.
                InvokeLifecycle(displayHost.GetComponent<HeartDisplay>(), "OnDisable");
                Object.DestroyImmediate(displayHost);
                DestroyRuntimeObject("TutorialHeartDamageOverlay");
                for (int i = 0; i < heartObjects.Length; i++)
                {
                    if (heartObjects[i] != null)
                        Object.DestroyImmediate(heartObjects[i]);
                }
            }
        }

        [Test]
        public void HeartDisplay_TutorialDemoHit_StillShowsDamageWhenHeartCountHasNotSynced()
        {
            GameObject displayHost = new GameObject("HeartDisplayHost");
            GameObject[] heartObjects = new GameObject[3];
            try
            {
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

                // EditMode never runs OnEnable; subscribe to EventBus by hand.
                InvokeLifecycle(display, "OnEnable");

                EventBus.RaiseTutorialBaseHitDemo(1);

                Assert.AreEqual(new Color(1f, 1f, 1f, 0.25f), icons[2].color);
                Transform damageIndicator = FindTransformByName("TutorialHeartDamageIndicator");
                Assert.NotNull(damageIndicator, "Tutorial heart hit should create a visible damage indicator on the damaged heart.");
                AssertDamageIndicatorText(damageIndicator, "-1");

                Canvas overlayCanvas = damageIndicator.GetComponentInParent<Canvas>();
                Assert.NotNull(overlayCanvas);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, overlayCanvas.renderMode);
                Assert.Greater(overlayCanvas.sortingOrder, RenderOrder.CutsceneCanvas,
                    "Tutorial heart damage indicator should render above tutorial dialogue/cutscene canvases.");
            }
            finally
            {
                // Unsubscribe even on assert failure so EventBus never keeps a
                // destroyed HeartDisplay registered for later tests.
                InvokeLifecycle(displayHost.GetComponent<HeartDisplay>(), "OnDisable");
                Object.DestroyImmediate(displayHost);
                DestroyRuntimeObject("TutorialHeartDamageOverlay");
                for (int i = 0; i < heartObjects.Length; i++)
                {
                    if (heartObjects[i] != null)
                        Object.DestroyImmediate(heartObjects[i]);
                }
            }
        }

        private static void InvokeLifecycle(MonoBehaviour target, string methodName)
        {
            if (target == null)
                return;
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing lifecycle method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        private static void AssertDamageIndicatorText(Transform marker, string expected)
        {
            Component[] components = marker.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().Name != "TextMeshProUGUI")
                    continue;

                PropertyInfo textProperty = component.GetType().GetProperty("text");
                Assert.NotNull(textProperty, "TextMeshProUGUI should expose a text property.");
                Assert.AreEqual(expected, textProperty.GetValue(component));
                return;
            }

            Assert.Fail("Tutorial damage indicator should include a TextMeshProUGUI component.");
        }

        private static Transform FindTransformByName(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                    return transforms[i];
            }

            return null;
        }

        private static void DestroyRuntimeObject(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }
}
