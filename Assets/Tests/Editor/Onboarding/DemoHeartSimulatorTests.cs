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

                EventBus.RaiseHeartsChanged(3);
                Assert.AreEqual(Color.red, icons[2].color);

                EventBus.RaiseTutorialBaseHitDemo(1);
                Assert.AreEqual(new Color(1f, 1f, 1f, 0.25f), icons[2].color);

                EventBus.RaiseTutorialBaseRestoreDemo();
                Assert.AreEqual(Color.red, icons[2].color);
            }
            finally
            {
                Object.DestroyImmediate(displayHost);
                for (int i = 0; i < heartObjects.Length; i++)
                {
                    if (heartObjects[i] != null)
                        Object.DestroyImmediate(heartObjects[i]);
                }
            }
        }
    }
}
