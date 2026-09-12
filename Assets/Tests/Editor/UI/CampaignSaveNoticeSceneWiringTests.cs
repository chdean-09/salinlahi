using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-272 — the save-recovery notice overlay must actually be able to render and be
    /// dismissed in <c>MainMenu.unity</c>.
    ///
    /// This guards a class of regression that is silent in every automated channel. Before this
    /// ticket the panel's three <c>TMP_Text</c> fields all pointed at one object (the Settings
    /// panel header), its <c>_overlayRoot</c> carried a plain <c>Transform</c> instead of a
    /// <c>RectTransform</c> and sat outside the Canvas, and its confirm button had no
    /// <c>Graphic</c> so it could never be clicked. That state compiled, passed 955 EditMode
    /// tests, passed 172 PlayMode tests and passed both validator profiles, while the notice was
    /// invisible at runtime, the Settings header read "Continue", and the notice could never be
    /// acknowledged so it recurred on every launch. Nothing but manual Play Mode inspection could
    /// tell it from success.
    ///
    /// The serialized fields are private, so they are read through <see cref="SerializedObject"/>
    /// rather than by widening the runtime API for a test.
    /// </summary>
    [TestFixture]
    public sealed class CampaignSaveNoticeSceneWiringTests
    {
        private const string MainMenuScenePath = "Assets/_Scenes/MainMenu.unity";

        [Test]
        public void TextTargets_AreThreeDistinctObjects()
        {
            SerializedObject panel = OpenSceneAndReadPanel();

            Object title = Reference(panel, "_titleText");
            Object body = Reference(panel, "_bodyText");
            Object retry = Reference(panel, "_retryText");

            Assert.IsNotNull(title, "_titleText is unassigned in " + MainMenuScenePath + ".");
            Assert.IsNotNull(body, "_bodyText is unassigned in " + MainMenuScenePath + ".");
            Assert.IsNotNull(retry, "_retryText is unassigned in " + MainMenuScenePath + ".");

            Assert.AreNotSame(
                title,
                body,
                "_titleText and _bodyText share one object (" + Describe(title) + "). Present() " +
                "writes title then body, so the title is overwritten and never seen.");
            Assert.AreNotSame(
                title,
                retry,
                "_titleText and _retryText share one object (" + Describe(title) + "). Present() " +
                "writes the retry label last, so only that label survives.");
            Assert.AreNotSame(
                body,
                retry,
                "_bodyText and _retryText share one object (" + Describe(body) + "). Present() " +
                "writes the retry label last, so the body is overwritten and never seen.");
        }

        /// <summary>
        /// NOTE on falsifying this guard: uGUI silently repairs a missing RectTransform at scene
        /// load when the object carries a Graphic ("Creating missing RectTransform component for
        /// Image in ..." appears in the Editor log), so flipping the serialized transform to
        /// !u!4 while leaving the Image in place does NOT make this test fail. The shipped defect
        /// had no Graphic at all, which is precisely why nothing repaired it and nothing caught
        /// it. The negative control for this test therefore has to remove the Graphic as well.
        /// </summary>
        [Test]
        public void OverlayRoot_CarriesARectTransform()
        {
            GameObject overlayRoot = OverlayRoot(OpenSceneAndReadPanel());

            Assert.IsNotNull(
                overlayRoot.GetComponent<RectTransform>(),
                "The overlay root '" + overlayRoot.name + "' has a plain Transform, not a " +
                "RectTransform, so SetActive(true) activates an object uGUI cannot lay out or " +
                "draw. The notice is invisible with no error.");
        }

        [Test]
        public void OverlayRoot_IsUnderTheCanvas()
        {
            GameObject overlayRoot = OverlayRoot(OpenSceneAndReadPanel());

            Canvas canvas = overlayRoot.GetComponentInParent<Canvas>(true);
            Assert.IsNotNull(
                canvas,
                "The overlay root '" + overlayRoot.name + "' has no Canvas ancestor — it is a " +
                "loose scene root. Nothing outside a Canvas renders through uGUI.");
            Assert.IsNotNull(
                canvas.GetComponent<GraphicRaycaster>(),
                "The Canvas above the overlay has no GraphicRaycaster, so no control inside the " +
                "overlay can receive a click however it is wired.");
        }

        [Test]
        public void ConfirmButton_HasARaycastableTargetGraphic()
        {
            SerializedObject panel = OpenSceneAndReadPanel();

            var confirmButton = Reference(panel, "_confirmButton") as Button;
            Assert.IsNotNull(confirmButton, "_confirmButton is unassigned in " + MainMenuScenePath + ".");

            Graphic targetGraphic = confirmButton.targetGraphic;
            Assert.IsNotNull(
                targetGraphic,
                "The confirm button has no target Graphic, so it presents no raycast surface and " +
                "can never be clicked. HandleConfirmation() never runs, the notice is never " +
                "acknowledged, and it re-fires on every launch.");
            Assert.IsTrue(
                targetGraphic.raycastTarget,
                "The confirm button's target Graphic has raycastTarget disabled, so clicks pass " +
                "through it and the notice can never be dismissed.");
        }

        [Test]
        public void TextTargets_AreNotTheSettingsHeader()
        {
            SerializedObject panel = OpenSceneAndReadPanel();
            TMP_Text settingsHeader = FindSettingsPanelHeader();

            foreach (string field in new[] { "_titleText", "_bodyText", "_retryText" })
            {
                Assert.AreNotSame(
                    settingsHeader,
                    Reference(panel, field),
                    field + " points at the Settings panel header. Present() would overwrite it, " +
                    "so the Settings screen shows notice text instead of its own title.");
            }
        }

        [Test]
        public void OverlayRoot_IsInactiveInTheSavedScene()
        {
            GameObject overlayRoot = OverlayRoot(OpenSceneAndReadPanel());

            Assert.IsFalse(
                overlayRoot.activeSelf,
                "The overlay root is active as serialized, so it is visible for the frames before " +
                "Awake() calls Hide() — the notice flashes on a clean boot that has nothing to say.");
        }

        private static SerializedObject OpenSceneAndReadPanel()
        {
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            var panel = Object.FindFirstObjectByType<CampaignSaveNoticePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(
                panel,
                MainMenuScenePath + " should contain a CampaignSaveNoticePanel.");
            return new SerializedObject(panel);
        }

        private static Object Reference(SerializedObject panel, string fieldName)
        {
            SerializedProperty property = panel.FindProperty(fieldName);
            Assert.IsNotNull(
                property,
                "CampaignSaveNoticePanel no longer serializes '" + fieldName + "'. If the field " +
                "was renamed, add [FormerlySerializedAs] so existing scenes keep their wiring, " +
                "then update this guard.");
            return property.objectReferenceValue;
        }

        /// <summary>
        /// Names the shared object in a failure message. The whole point of this guard is that
        /// two fields collapsed onto one object, so the message has to say which object.
        /// </summary>
        private static string Describe(Object value)
        {
            if (value == null)
                return "<null>";

            var component = value as Component;
            return component != null
                ? component.gameObject.name + "." + component.GetType().Name
                : value.name;
        }

        private static GameObject OverlayRoot(SerializedObject panel)
        {
            var overlayRoot = Reference(panel, "_overlayRoot") as GameObject;
            Assert.IsNotNull(overlayRoot, "_overlayRoot is unassigned in " + MainMenuScenePath + ".");
            return overlayRoot;
        }

        /// <summary>
        /// The Settings header is inactive at scene load, so <c>GameObject.Find</c> cannot see it.
        /// It is located by hierarchy shape — a child named "Title" under "SettingsPanel" — rather
        /// than by fileID, so the guard survives a legitimate re-serialization of the scene.
        /// </summary>
        private static TMP_Text FindSettingsPanelHeader()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name != "Title" ||
                        transform.parent == null ||
                        transform.parent.name != "SettingsPanel")
                    {
                        continue;
                    }

                    var header = transform.GetComponent<TMP_Text>();
                    if (header != null)
                        return header;
                }
            }

            Assert.Fail(
                "Expected a TMP_Text on SettingsPanel/Title in " + MainMenuScenePath +
                ". If the Settings header moved, retarget this guard rather than deleting it — " +
                "it is the AC2 check that the notice does not clobber another screen's label.");
            return null;
        }
    }
}
