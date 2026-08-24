using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.UI
{
    /// <summary>
    /// SALIN-137 AC1/AC2 in Play Mode.
    ///
    /// These tests MUST be Play Mode: <see cref="LevelLockNoticePanel"/> builds its own
    /// surface from <c>Awake</c> when the serialized references are unwired, and Unity
    /// does not run Awake/OnEnable on runtime-created GameObjects in Edit Mode. The
    /// authored-reference path has no lifecycle dependency and is covered in Edit Mode by
    /// <c>Salinlahi.Tests.Editor.UI.LevelLockNoticePanelTests</c>.
    ///
    /// The AC1 button tests come in wired and UNWIRED pairs on purpose. Injecting
    /// <c>_completionBadge</c> by reflection proves the authored path but structurally
    /// cannot catch the scene's real state, where no instance serializes that field at
    /// all; the unwired variants cover exactly that.
    /// </summary>
    [TestFixture]
    public sealed class LevelLockRuntimeSurfaceTests
    {
        private readonly List<Object> _objectsToDestroy = new List<Object>();

        // NOTE: there is deliberately NO fixture-wide LogAssert.ignoreFailingMessages here.
        // LogAssert only fails a test on Error/Assert/Exception, never on Log or Warning,
        // so suppressing fixture-wide would have bought nothing for the expected warnings
        // while hiding a genuine NRE in the runtime-build path these tests exist to prove.
        // The single test that can legitimately reach a LogError scopes the flag itself.

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();

            // Belt and braces. The panel now reaps its own runtime overlay in OnDestroy
            // (see LevelLockNoticePanel), but Destroy is deferred to end-of-frame in Play
            // Mode, and a test that fails before its teardown may skip the destroy path.
            DestroyByName("[Runtime] LevelLockNotice");
            DestroyByName("[Runtime] LevelLockNoticeCanvas");
        }

        // ---------------------------------------------------------------
        // AC2 — the runtime-built explanation surface
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator UnwiredPanel_BuildsItsOwnSurfaceAndStartsHidden()
        {
            LevelLockNoticePanel panel = CreateUnwiredPanel();
            yield return null;

            Assert.IsTrue(panel.HasRequiredReferences,
                "The panel must build its own surface when the scene authors none.");
            Assert.IsFalse(panel.IsShowing, "The notice starts hidden.");
        }

        [UnityTest]
        public IEnumerator UnwiredPanel_PresentPrerequisite_ShowsTheExplanation()
        {
            LevelLockNoticePanel panel = CreateUnwiredPanel();
            yield return null;

            panel.PresentPrerequisite(3, crossesEra: false, requiredEraName: null);

            Assert.IsTrue(panel.IsShowing);
            Assert.AreEqual(LevelLockNoticeCopy.Prerequisite(3, false, null), panel.VisibleMessage);
        }

        [UnityTest]
        public IEnumerator UnwiredPanel_Hide_ClearsTheRuntimeSurface()
        {
            LevelLockNoticePanel panel = CreateUnwiredPanel();
            yield return null;
            panel.PresentPrerequisite(2, crossesEra: false, requiredEraName: null);
            Assert.IsTrue(panel.IsShowing, "precondition");

            panel.Hide();

            Assert.IsFalse(panel.IsShowing);
        }

        [UnityTest]
        public IEnumerator UnwiredPanel_OnDestroy_ReapsTheOverlayItParentedElsewhere()
        {
            // A canvas the PANEL DOES NOT OWN. Without one, ResolveCanvas would build its
            // own canvas under the panel and the overlay would die with it for free —
            // which is exactly the case that would make this test unable to fail.
            var foreignCanvas = new GameObject("ForeignCanvas_PlayModeTest", typeof(Canvas));
            _objectsToDestroy.Add(foreignCanvas);

            var host = new GameObject("LevelLockNoticePanel_LeakTest");
            _objectsToDestroy.Add(host);
            LevelLockNoticePanel panel = host.AddComponent<LevelLockNoticePanel>();
            yield return null;
            Assert.IsTrue(panel.HasRequiredReferences, "precondition: the fallback surface was built");

            GameObject overlay = FindIncludingInactive("[Runtime] LevelLockNotice");
            Assert.IsNotNull(overlay, "precondition: the runtime overlay exists");
            Transform borrowedParent = overlay.transform.parent;
            Assert.IsFalse(overlay.transform.IsChildOf(host.transform),
                "precondition: the overlay hangs off a canvas the panel does not own, so "
                + "destroying the panel would NOT reap it for free");

            Object.DestroyImmediate(host);
            yield return null;

            Assert.IsTrue(overlay == null,
                "The panel must destroy the overlay it parented to someone else's canvas; "
                + "otherwise it orphans on that canvas for the rest of the session.");
            Assert.IsTrue(borrowedParent != null, "The borrowed canvas itself must survive.");
        }

        // ---------------------------------------------------------------
        // AC2 — a locked scroll is pressable and does not enter gameplay
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator LockedLevelButton_StaysInteractableSoThePressIsObservable()
        {
            LevelButtonHarness harness = CreateLevelButton();
            yield return null;

            harness.Button.Setup(harness.Config, isUnlocked: false, isCompleted: false);

            Assert.IsTrue(harness.UnityButton.interactable,
                "AC2 needs the press to reach OnPressed; interactable=false would swallow it.");
        }

        [UnityTest]
        public IEnumerator LockedLevelButton_Press_ReportsToTheOwnerInsteadOfLoadingGameplay()
        {
            LevelButtonHarness harness = CreateLevelButton();
            yield return null;
            LevelConfigSO reported = null;
            int callCount = 0;
            harness.Button.SetLockedPressHandler(config => { reported = config; callCount++; });
            harness.Button.Setup(harness.Config, isUnlocked: false, isCompleted: false);

            harness.UnityButton.onClick.Invoke();

            Assert.AreEqual(1, callCount, "A locked press must report exactly once.");
            Assert.AreSame(harness.Config, reported);
        }

        [UnityTest]
        public IEnumerator UnlockedLevelButton_Press_DoesNotReportALockedPress()
        {
            LevelButtonHarness harness = CreateLevelButton();
            yield return null;
            int callCount = 0;
            harness.Button.SetLockedPressHandler(_ => callCount++);
            harness.Button.Setup(harness.Config, isUnlocked: true, isCompleted: false);

            // Scoped, and ONLY for this test: OnPressed's unlocked tail normally stops at
            // the "could not be persisted" guard because ProgressManager.Instance is null,
            // but a ProgressManager leaked by an earlier Play Mode fixture (its singleton
            // is DontDestroyOnLoad) would let the press run on to SceneLoader and LogError.
            // The other tests in this fixture must stay able to fail on a real error.
            LogAssert.ignoreFailingMessages = true;

            harness.UnityButton.onClick.Invoke();

            Assert.AreEqual(0, callCount, "An unlocked press is an entry attempt, not an explanation.");
        }

        // ---------------------------------------------------------------
        // AC1 — three visually distinct states
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator LevelButton_RendersLockedUnlockedAndCompletedDistinctly()
        {
            LevelButtonHarness harness = CreateLevelButton();
            yield return null;

            AssertThreeStatesRenderDistinctly(harness);
        }

        /// <summary>
        /// The same AC1 contract with <c>_completionBadge</c> LEFT UNWIRED, which is the
        /// state every LevelButton instance in <c>Assets/_Scenes/LevelSelect.unity</c> is
        /// actually in — none of the five serialize that field. The test above injects it
        /// by reflection and therefore cannot catch an unwired scene; this one can. The
        /// harness reproduces the scene's real shape: an unwired field plus the
        /// LevelButton prefab's authored <c>CompletionCheck</c> child.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelButton_WithUnwiredBadge_StillRendersCompletedDistinctly()
        {
            LevelButtonHarness harness = CreateLevelButton(wireCompletionBadge: false);
            yield return null;

            AssertThreeStatesRenderDistinctly(harness);
        }

        [UnityTest]
        public IEnumerator LevelButton_WithUnwiredBadge_AdoptsThePrefabsCompletionCheckChild()
        {
            LevelButtonHarness harness = CreateLevelButton(wireCompletionBadge: false);
            yield return null;
            Assert.IsNull(GetPrivateField(harness.Button, "_completionBadge"),
                "precondition: the badge starts unwired, exactly as the scene leaves it");

            harness.Button.Setup(harness.Config, isUnlocked: true, isCompleted: true);

            Assert.AreSame(harness.CompletionBadge, GetPrivateField(harness.Button, "_completionBadge"),
                "The unwired field must resolve to the prefab's existing CompletionCheck child, "
                + "not to newly constructed art.");
        }

        [UnityTest]
        public IEnumerator LevelButton_WithNoBadgeAnywhere_StillSetsUpWithoutThrowing()
        {
            LevelButtonHarness harness = CreateLevelButton(
                wireCompletionBadge: false, createCompletionBadgeChild: false);
            yield return null;

            Assert.DoesNotThrow(() => harness.Button.Setup(harness.Config, isUnlocked: true, isCompleted: true));
            Assert.IsFalse(harness.LockIcon.activeSelf, "the other two states still render");
        }

        private static void AssertThreeStatesRenderDistinctly(LevelButtonHarness harness)
        {
            harness.Button.Setup(harness.Config, isUnlocked: false, isCompleted: false);
            Assert.IsTrue(harness.LockIcon.activeSelf, "locked: lock overlay shown");
            Assert.IsFalse(harness.CompletionBadge.activeSelf, "locked: no completion badge");

            harness.Button.Setup(harness.Config, isUnlocked: true, isCompleted: false);
            Assert.IsFalse(harness.LockIcon.activeSelf, "unlocked: no lock overlay");
            Assert.IsFalse(harness.CompletionBadge.activeSelf, "unlocked: no completion badge");

            harness.Button.Setup(harness.Config, isUnlocked: true, isCompleted: true);
            Assert.IsFalse(harness.LockIcon.activeSelf, "completed: no lock overlay");
            Assert.IsTrue(harness.CompletionBadge.activeSelf, "completed: completion badge shown");
        }

        // ---------------------------------------------------------------
        // AC1/AC3 — a level with no authored numberSprite
        // ---------------------------------------------------------------

        /// <summary>
        /// Buttons are reused across eras and levels 6-15 have no <c>numberSprite</c>, so
        /// a Setup that leaves the previous sprite in place renders era one's numbered
        /// scrolls under era two's levels. Showing the WRONG number on the screen AC3 is
        /// demonstrated from is worse than showing none.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelButton_ReusedForALevelWithNoNumberSprite_ClearsTheStaleSprite()
        {
            LevelButtonHarness harness = CreateLevelButton();
            yield return null;
            var texture = new Texture2D(4, 4);
            _objectsToDestroy.Add(texture);
            Sprite numbered = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            _objectsToDestroy.Add(numbered);
            harness.Config.numberSprite = numbered;
            harness.Button.Setup(harness.Config, isUnlocked: true, isCompleted: false);
            Assert.AreSame(numbered, harness.ScrollImage.sprite, "precondition: the numbered scroll is up");

            LevelConfigSO unnumbered = ScriptableObject.CreateInstance<LevelConfigSO>();
            unnumbered.name = "LevelLockTestConfig_NoNumber";
            unnumbered.levelNumber = 6;
            unnumbered.stableId = "level.ugnayan.01";
            _objectsToDestroy.Add(unnumbered);

            harness.Button.Setup(unnumbered, isUnlocked: true, isCompleted: false);

            Assert.IsNull(harness.ScrollImage.sprite,
                "A level with no numberSprite must not keep the PREVIOUS level's number.");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private LevelLockNoticePanel CreateUnwiredPanel()
        {
            var host = new GameObject("LevelLockNoticePanel_PlayModeTest");
            _objectsToDestroy.Add(host);
            return host.AddComponent<LevelLockNoticePanel>();
        }

        /// <summary>
        /// Builds a LevelButton shaped like the real one.
        /// <paramref name="wireCompletionBadge"/> false reproduces
        /// <c>Assets/_Scenes/LevelSelect.unity</c> exactly: the serialized field is absent,
        /// but the prefab's <c>CompletionCheck</c> child is present.
        /// <paramref name="createCompletionBadgeChild"/> false removes even that.
        /// </summary>
        private LevelButtonHarness CreateLevelButton(
            bool wireCompletionBadge = true, bool createCompletionBadgeChild = true)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            _objectsToDestroy.Add(canvasObject);

            var buttonObject = new GameObject("LevelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);

            var lockIcon = new GameObject("LockOverlay");
            lockIcon.transform.SetParent(buttonObject.transform, false);

            GameObject badge = null;
            if (createCompletionBadgeChild)
            {
                badge = new GameObject("CompletionCheck");
                badge.transform.SetParent(buttonObject.transform, false);
                badge.SetActive(false);
            }

            LevelButton levelButton = buttonObject.AddComponent<LevelButton>();
            SetPrivateField(levelButton, "_button", buttonObject.GetComponent<Button>());
            SetPrivateField(levelButton, "_scrollImage", buttonObject.GetComponent<Image>());
            SetPrivateField(levelButton, "_lockIcon", lockIcon);
            if (wireCompletionBadge && badge != null)
                SetPrivateField(levelButton, "_completionBadge", badge);

            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.name = "LevelLockTestConfig";
            config.levelNumber = 3;
            config.stableId = "level.ugat.03";
            _objectsToDestroy.Add(config);

            return new LevelButtonHarness
            {
                Button = levelButton,
                UnityButton = buttonObject.GetComponent<Button>(),
                ScrollImage = buttonObject.GetComponent<Image>(),
                LockIcon = lockIcon,
                CompletionBadge = badge,
                Config = config,
            };
        }

        private static void SetPrivateField(Object target, string fieldName, object value) =>
            PrivateField(target, fieldName).SetValue(target, value);

        private static object GetPrivateField(Object target, string fieldName) =>
            PrivateField(target, fieldName).GetValue(target);

        private static FieldInfo PrivateField(Object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return field;
        }

        /// <summary>
        /// The runtime overlay sits inactive at rest, and <c>GameObject.Find</c> only sees
        /// active objects — so it would silently miss exactly the object being looked for.
        /// </summary>
        private static GameObject FindIncludingInactive(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.scene.IsValid() && all[i].name == name)
                    return all[i].gameObject;
            }
            return null;
        }

        private static void DestroyByName(string name)
        {
            GameObject found = FindIncludingInactive(name);
            while (found != null)
            {
                Object.DestroyImmediate(found);
                found = FindIncludingInactive(name);
            }
        }

        private sealed class LevelButtonHarness
        {
            public LevelButton Button;
            public Button UnityButton;
            public Image ScrollImage;
            public GameObject LockIcon;
            public GameObject CompletionBadge;
            public LevelConfigSO Config;
        }
    }
}
