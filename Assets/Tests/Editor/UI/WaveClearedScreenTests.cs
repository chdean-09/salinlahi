using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-232. EditMode coverage for the Wave Cleared screen's copy and its no-surface
    /// contract. The flow behaviour it gates — the machine holding in Defense until the
    /// button is tapped — is PlayMode's job and lives in WaveClearedScreenFlowTests.
    /// </summary>
    [TestFixture]
    public sealed class WaveClearedScreenTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            // BuildFallbackUi parents the screen under a canvas it creates itself, so the
            // canvas — not the screen — is the root that has to go.
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();

            GameObject canvas = GameObject.Find("[Runtime] WaveClearedCanvas");
            while (canvas != null)
            {
                Object.DestroyImmediate(canvas);
                canvas = GameObject.Find("[Runtime] WaveClearedCanvas");
            }
        }

        // ------------------------------------------------------------------
        // Copy (AC-1, AC-4) and the D-021 / D-006 regression pin.
        // ------------------------------------------------------------------

        [Test]
        public void BannerLabel_IsPresentableCopy()
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(WaveClearedCopy.BannerLabel),
                "AC-1: the screen shows a banner, so the banner needs a heading.");
        }

        /// <summary>
        /// AC-4. Pins the ticket's literal label. The wording is disputed — it names
        /// ContextChallenge as "the Memory" and carries pre-D-003 restoration vocabulary
        /// (see the WaveClearedCopy banner) — and that dispute is product's to settle.
        /// Until it does, a silent reword here would quietly drop a stated acceptance
        /// criterion, so the literal is pinned rather than trusted.
        /// </summary>
        [Test]
        public void ContinueLabel_IsTheTicketsLiteralLabel()
        {
            Assert.AreEqual(
                "Restore the Memory",
                WaveClearedCopy.ContinueLabel,
                "AC-4 names this label verbatim. If product has approved new wording, change "
                + "this pin deliberately and record the approval — do not adjust it to match "
                + "an incidental edit.");
        }

        /// <summary>
        /// D-021 (LOCKED) + D-006. The live SALIN-232 description still asks for a "combat
        /// accuracy" readout on this screen; D-021 cut the displayed accuracy statistic and
        /// Master F27 withdrew the allowance, so it is deliberately absent. This test is
        /// what stops a later reader "restoring" it from the stale ticket text. It also
        /// covers D-006, which retired player-facing "tracing" prose.
        /// </summary>
        [Test]
        public void NoCopyStringMentionsAccuracyOrTracing()
        {
            var accuracy = new Regex("accur", RegexOptions.IgnoreCase);
            var tracing = new Regex("trac", RegexOptions.IgnoreCase);

            foreach (FieldInfo field in typeof(WaveClearedCopy).GetFields(
                BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                    continue;

                var value = (string)field.GetValue(null);
                Assert.IsFalse(
                    accuracy.IsMatch(value),
                    "WaveClearedCopy." + field.Name + " = \"" + value + "\" reads as an accuracy "
                    + "statistic. D-021 (LOCKED) cut that display and no combat-accuracy metric "
                    + "exists (LevelResultsCalculator.cs:23-28). The stale ticket text is not "
                    + "authority to add one.");
                Assert.IsFalse(
                    tracing.IsMatch(value),
                    "WaveClearedCopy." + field.Name + " = \"" + value + "\" uses \"tracing\" in "
                    + "player-facing copy. D-006 replaced it with \"drawing\" in prose and UI "
                    + "strings (code identifiers keep Trace/Tracing).");
            }
        }

        // ------------------------------------------------------------------
        // The no-surface contract.
        // ------------------------------------------------------------------

        /// <summary>
        /// Negative control. On an EditMode host nothing is playing and no references are
        /// authored, so no surface can be built — Present must report that rather than
        /// claiming a screen is up. This is the case the flow's "proceed immediately, never
        /// hold" branch exists for, and without this test that branch is unreachable
        /// evidence-wise: every other test here builds the UI first and would pass whether
        /// or not Present could ever return false.
        /// </summary>
        [Test]
        public void Present_WithNoSurface_ReturnsFalseAndPresentsNothing()
        {
            WaveClearedScreenUI screen = CreateBareScreen();

            bool presented = screen.Present(2, 3, () => { });

            Assert.IsFalse(
                presented,
                "An unbuildable screen must report false so the flow advances immediately. "
                + "Returning true would strand a level the player legitimately cleared on a "
                + "wait nothing can satisfy.");
            Assert.IsFalse(screen.IsPresented);
        }

        [Test]
        public void Hide_ClearsIsPresentedAndDeactivatesTheOverlay()
        {
            WaveClearedScreenUI screen = CreateBuiltScreen();

            Assert.IsTrue(screen.Present(2, 3, () => { }), "Setup: the built screen must present.");
            Assert.IsTrue(screen.IsPresented);
            GameObject overlay = (GameObject)Serialized(screen, "_overlayRoot");
            Assert.IsTrue(overlay.activeSelf, "Setup: presenting must activate the overlay.");

            screen.Hide();

            Assert.IsFalse(screen.IsPresented);
            Assert.IsFalse(
                overlay.activeSelf,
                "Terminal cleanup calls Hide() while the banner may be up "
                + "(LevelFlowController.HandleMachinePhaseChanged). An overlay left active "
                + "sits over the defeat and exit screens at sortingOrder 300.");
        }

        /// <summary>
        /// The SALIN-272 aliasing defect: three serialized fields pointing at one GameObject,
        /// which no functional test could see. Read through <see cref="SerializedObject"/>
        /// rather than by widening the runtime API for a test, following
        /// VictoryScreenSceneWiringTests.
        /// </summary>
        [Test]
        public void BuiltControls_AreFourDistinctObjects()
        {
            WaveClearedScreenUI screen = CreateBuiltScreen();

            Object overlay = Serialized(screen, "_overlayRoot");
            Object banner = Serialized(screen, "_bannerText");
            Object hearts = Serialized(screen, "_heartsText");
            Object continueButton = Serialized(screen, "_continueButton");

            Assert.IsNotNull(overlay, "_overlayRoot was not built.");
            Assert.IsNotNull(banner, "_bannerText was not built.");
            Assert.IsNotNull(hearts, "_heartsText was not built.");
            Assert.IsNotNull(continueButton, "_continueButton was not built.");

            var distinct = new HashSet<GameObject>
            {
                overlay as GameObject,
                ((Component)banner).gameObject,
                ((Component)hearts).gameObject,
                ((Component)continueButton).gameObject,
            };

            Assert.AreEqual(
                4,
                distinct.Count,
                "The four controls must live on four separate GameObjects. Aliasing them is "
                + "the SALIN-272 defect: hiding or disabling one would silently take the "
                + "others with it, and no functional assertion can see that.");
        }

        /// <summary>
        /// Negative control for <see cref="WaveClearedScreenUI.HasRequiredReferences"/>.
        ///
        /// HOW THIS CONTROL BREAKS THE SURFACE, AND WHY NOT BY THE OBVIOUS ROUTE. Removing
        /// the control's RectTransform is NOT a usable technique and must not be reached for
        /// here. Measured on Unity 6000.3.9f1 in this repo: DestroyImmediate on the
        /// RectTransform of an object that still carries a Graphic is refused outright, with
        /// "Can't remove RectTransform because Image (Script) depends on it" logged as an
        /// error and the transform still in place — so the control strips nothing, and in
        /// EditMode it then fails on the unhandled log rather than on its subject, which
        /// reads at a glance like the control ran. (Removing the last Transform from a
        /// GameObject is refused in its own right regardless of Graphics.)
        ///
        /// The Button component itself is destroyed instead — that is what
        /// HasRequiredReferences actually reads — and its Image goes with it so nothing on
        /// the object still presents as an interactive control.
        /// </summary>
        [Test]
        public void Present_WithTheContinueControlStripped_ReportsNoSurface()
        {
            WaveClearedScreenUI screen = CreateBuiltScreen();
            var button = (Button)Serialized(screen, "_continueButton");
            GameObject buttonObject = button.gameObject;

            Object.DestroyImmediate(button);
            Object.DestroyImmediate(buttonObject.GetComponent<Graphic>());
            Assert.IsNull(
                buttonObject.GetComponent<Button>(),
                "Setup: the continue control must actually be gone for this to be a control.");

            Assert.IsFalse(
                screen.HasRequiredReferences,
                "A screen whose continue control is gone has no usable surface.");
            Assert.IsFalse(
                screen.Present(2, 3, () => { }),
                "Present must report false rather than putting up a banner with nothing to "
                + "tap — that would hold the flow on a gate the player cannot release.");
            Assert.IsFalse(screen.IsPresented);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private WaveClearedScreenUI CreateBareScreen()
        {
            GameObject host = new GameObject("WaveClearedScreen");
            _objectsToDestroy.Add(host);
            return host.AddComponent<WaveClearedScreenUI>();
        }

        /// <summary>
        /// Forces the runtime construction that Present would do for itself in play mode.
        /// Application.isPlaying is false in an EditMode run, so Present deliberately
        /// refuses to build (see Present_WithNoSurface_ReturnsFalseAndPresentsNothing);
        /// the private builder is invoked directly rather than widening the API for a test.
        /// </summary>
        private WaveClearedScreenUI CreateBuiltScreen()
        {
            WaveClearedScreenUI screen = CreateBareScreen();

            MethodInfo build = typeof(WaveClearedScreenUI).GetMethod(
                "BuildFallbackUi", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(
                build,
                "WaveClearedScreenUI.BuildFallbackUi no longer exists. The runtime-built "
                + "approach is what keeps this ticket's serialized-asset conflict surface at "
                + "zero; reconcile that before deleting it.");
            build.Invoke(screen, null);

            return screen;
        }

        private static Object Serialized(WaveClearedScreenUI screen, string fieldName)
        {
            SerializedObject serialized = new SerializedObject(screen);
            SerializedProperty property = serialized.FindProperty(fieldName);
            Assert.IsNotNull(
                property,
                "WaveClearedScreenUI no longer serializes '" + fieldName + "'. If the field was "
                + "renamed, add [FormerlySerializedAs] before updating this guard.");
            return property.objectReferenceValue;
        }
    }
}
