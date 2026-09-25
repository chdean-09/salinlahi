using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// The shipped scenes self-host the end screens: VictoryScreenUI and DefeatScreenUI sit
    /// ON the panel GameObjects they present (VictoryPanel / DefeatPanel in Gameplay.unity
    /// and Level_01_Tutorial.unity), and those objects are serialized inactive.
    ///
    /// Awake on an inactive-at-load object is deferred to its FIRST activation — which here
    /// is the Show() call's own _panel.SetActive(true). An Awake that unconditionally hides
    /// _panel therefore runs inside that SetActive and the screen never renders a frame:
    /// the player finishes (or loses) the level and is left on a bare frozen battlefield
    /// with no navigation. These tests pin the self-hosting guard on Awake.
    /// </summary>
    [TestFixture]
    public sealed class EndScreenActivationTests
    {
        private readonly List<Object> _objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _objectsToDestroy)
            {
                if (item != null)
                    Object.Destroy(item);
            }
            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator VictoryScreen_SelfHostedPanel_StaysActiveOnFirstShow()
        {
            // Inactive BEFORE the component is added, matching the serialized scene state:
            // that is what defers Awake into the Show() call below.
            GameObject panelObject = new GameObject("VictoryPanel_SelfHosted", typeof(RectTransform));
            _objectsToDestroy.Add(panelObject);
            panelObject.SetActive(false);

            VictoryScreenUI screen = panelObject.AddComponent<VictoryScreenUI>();
            GlyphBadgePlayModeTestHelpers.SetPrivateField(screen, "_panel", panelObject);
            yield return null;

            screen.Show();
            yield return null;

            Assert.IsTrue(
                panelObject.activeSelf,
                "Show() on a self-hosted panel must leave the panel active. An Awake-time " +
                "_panel.SetActive(false) fires on this first activation and self-hides the " +
                "results screen — the level completes onto a bare field with no way onward.");
        }

        [UnityTest]
        public IEnumerator DefeatScreen_SelfHostedPanel_StaysActiveOnFirstShow()
        {
            GameObject panelObject = new GameObject("DefeatPanel_SelfHosted", typeof(RectTransform));
            _objectsToDestroy.Add(panelObject);
            panelObject.SetActive(false);

            DefeatScreenUI screen = panelObject.AddComponent<DefeatScreenUI>();
            GlyphBadgePlayModeTestHelpers.SetPrivateField(screen, "_panel", panelObject);
            yield return null;

            screen.Show();
            yield return null;

            Assert.IsTrue(
                panelObject.activeSelf,
                "Show() on a self-hosted panel must leave the panel active. An Awake-time " +
                "_panel.SetActive(false) fires on this first activation and self-hides the " +
                "defeat screen.");
        }

        [UnityTest]
        public IEnumerator VictoryScreen_SeparatePanel_StillHidesAtStartup()
        {
            // The other serialized shape the Awake-hide exists for: the screen component on a
            // live manager object, _panel pointing at a DIFFERENT panel that starts active.
            // That panel must still be hidden at startup. The host starts inactive so Awake
            // is deferred until _panel is wired — Awake on an active host would run at
            // AddComponent with _panel still null and the hide would never fire.
            GameObject host = new GameObject("VictoryScreen_Host");
            GameObject panel = new GameObject("VictoryPanel_Child", typeof(RectTransform));
            panel.transform.SetParent(host.transform, false);
            host.SetActive(false);
            _objectsToDestroy.Add(host);

            VictoryScreenUI screen = host.AddComponent<VictoryScreenUI>();
            GlyphBadgePlayModeTestHelpers.SetPrivateField(screen, "_panel", panel);
            host.SetActive(true);
            yield return null;

            Assert.IsFalse(
                panel.activeSelf,
                "A _panel on another object is still hidden by Awake at startup — the guard " +
                "must only exempt the self-hosted case.");
        }
    }
}
