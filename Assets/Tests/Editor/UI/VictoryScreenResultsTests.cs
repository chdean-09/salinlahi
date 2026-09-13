using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-234 — the Level Results screen.
    ///
    /// Two defects these tests exist to keep fixed, both of which were invisible to the
    /// whole suite before this ticket:
    ///
    /// (1) <c>Show()</c> read <c>ProgressManager.GetStars</c>, which on the revised path
    /// returns the ALL-TIME BEST (ProgressManager.cs:619-620 -> CampaignProgressRepository.cs:50),
    /// so a replay that scored worse still showed the earlier run's stars.
    ///
    /// (2) Gameplay.unity — the scene the game actually plays — serializes
    /// <c>_starCountText: {fileID: 0}</c> and <c>_starIcons: []</c> (Gameplay.unity:6347-6348),
    /// so every line of the star rendering no-opped there while 961 EditMode and 172 PlayMode
    /// tests stayed green. Nothing asserted those fields. The fix builds the missing controls
    /// at runtime rather than editing the scenes; these tests are what make that observable.
    /// </summary>
    [TestFixture]
    public sealed class VictoryScreenResultsTests
    {
        private GameObject _screenObject;
        private GameObject _progressManagerObject;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_screenObject != null) Object.DestroyImmediate(_screenObject);
            if (_progressManagerObject != null) Object.DestroyImmediate(_progressManagerObject);
            ClearSingletonInstance<ProgressManager>();

            PlayerPrefs.DeleteKey(ProgressManager.SelectedLevelKey);
            for (int i = 1; i <= 15; i++)
            {
                PlayerPrefs.DeleteKey($"salinlahi.progress.unlocked.{i}");
                PlayerPrefs.DeleteKey($"salinlahi.progress.stars.{i}");
            }
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------
        // AC-10 — the screen shows THIS attempt, the save keeps the best.
        // ------------------------------------------------------------------

        [Test]
        public void PresentResults_ShowsTheAttemptStars_NotTheSavedBest()
        {
            ProgressManager progress = CreateProgressManager();
            progress.MarkLevelComplete(1, 3);
            progress.TrySetSelectedLevelNumber(1);
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 2));

            Assert.AreEqual(
                "2/3",
                StarCountText(screen).text,
                "The screen must read the stars this attempt earned. Reading " +
                "ProgressManager.GetStars instead reports the all-time best (3 here), so a " +
                "weaker replay congratulates the player on an earlier run.");
        }

        [Test]
        public void Show_WithoutAttemptResults_KeepsTheLegacyProgressManagerRead()
        {
            ProgressManager progress = CreateProgressManager();
            progress.MarkLevelComplete(1, 3);
            progress.TrySetSelectedLevelNumber(1);
            VictoryScreenUI screen = CreateScreen();

            screen.Show();

            Assert.AreEqual(
                "3/3",
                StarCountText(screen).text,
                "The legacy path computes no LevelResults (ProgressManager.cs:489-490), so the " +
                "GetStars read must stay as the fallback rather than being deleted.");
        }

        [Test]
        public void PresentResults_LightsExactlyTheEarnedStarIcons()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 2));

            GameObject[] icons = StarIcons(screen);
            Assert.AreEqual(3, icons.Length, "The screen shows three star slots.");
            Assert.IsTrue(icons[0].activeSelf, "Star 1 must be lit at two stars.");
            Assert.IsTrue(icons[1].activeSelf, "Star 2 must be lit at two stars.");
            Assert.IsFalse(icons[2].activeSelf, "Star 3 must be dark at two stars.");
        }

        // ------------------------------------------------------------------
        // The runtime-built controls (defect 2). See EnsureRuntimeControls for why
        // these are built rather than authored into the two scenes.
        // ------------------------------------------------------------------

        /// <summary>
        /// NOTE on falsifying this guard: uGUI silently repairs a missing RectTransform on an
        /// object that carries a Graphic, so removing only the RectTransform from a built
        /// control does NOT make this fail. The negative control has to remove the Graphic as
        /// well — the same trap documented at CampaignSaveNoticeSceneWiringTests.cs:63-70.
        /// </summary>
        [Test]
        public void RuntimeControls_AreBuiltUnderThePanel_AndEachCarriesARectTransform()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 1));

            GameObject panel = Panel(screen);
            foreach (string name in new[]
                     {
                         VictoryScreenUI.RuntimeStarCountName,
                         VictoryScreenUI.RuntimeStarIconsName,
                         VictoryScreenUI.RuntimeReplayButtonName,
                     })
            {
                Transform control = panel.transform.Find(name);
                Assert.IsNotNull(
                    control,
                    "'" + name + "' was not built under the victory panel. Gameplay.unity " +
                    "authors none of these, so nothing else creates them and the control is " +
                    "invisible at runtime with no error.");
                Assert.IsNotNull(
                    control.GetComponent<RectTransform>(),
                    "'" + name + "' has a plain Transform, not a RectTransform, so uGUI cannot " +
                    "lay it out or draw it.");
            }

            foreach (GameObject icon in StarIcons(screen))
            {
                Assert.IsNotNull(
                    icon.GetComponent<RectTransform>(),
                    "Star icon '" + icon.name + "' has no RectTransform.");
                Assert.IsNotNull(
                    icon.GetComponent<Graphic>(),
                    "Star icon '" + icon.name + "' has no Graphic, so an 'active' star draws " +
                    "nothing and the star display is silently empty.");
            }
        }

        [Test]
        public void RuntimeControls_AreNotRebuiltOnASecondShow()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 1));
            screen.PresentResults(Results(stars: 3));

            GameObject panel = Panel(screen);
            int replayButtons = 0;
            foreach (Transform child in panel.transform)
            {
                if (child.name == VictoryScreenUI.RuntimeReplayButtonName)
                    replayButtons++;
            }

            Assert.AreEqual(
                1,
                replayButtons,
                "Show() runs on every completion, so the fallback construction must be " +
                "idempotent. Duplicated buttons stack invisibly and multiply the click.");
        }

        [Test]
        public void AuthoredDisplayFields_AreNeverReplacedByRuntimeControls()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();
            GameObject panel = Panel(screen);

            GameObject authoredText = new GameObject("AuthoredStarCount", typeof(RectTransform));
            authoredText.transform.SetParent(panel.transform, false);
            TextMeshProUGUI authoredLabel = authoredText.AddComponent<TextMeshProUGUI>();
            SetPrivateField(screen, "_starCountText", authoredLabel);

            screen.PresentResults(Results(stars: 2));

            Assert.AreSame(
                authoredLabel,
                StarCountText(screen),
                "Level_01_Tutorial.unity authors _starCountText and three _starIcons " +
                "(Level_01_Tutorial.unity:4787-4788). A populated field must always win, or " +
                "that scene silently stops rendering through its own objects.");
            Assert.IsNull(
                panel.transform.Find(VictoryScreenUI.RuntimeStarCountName),
                "No runtime star-count object may be built when the scene authored one.");
            Assert.AreEqual("2/3", authoredLabel.text);
        }

        // ------------------------------------------------------------------
        // AC-8 / BTN-REPLAY.
        // ------------------------------------------------------------------

        [Test]
        public void ReplayButton_UsesTheApprovedLabelAndPresentsARaycastSurface()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 1));

            Transform button = Panel(screen).transform.Find(VictoryScreenUI.RuntimeReplayButtonName);
            Assert.IsNotNull(button, "The Replay Level control was not built.");

            var replay = button.GetComponent<Button>();
            Assert.IsNotNull(replay, "The Replay Level control carries no Button.");
            Assert.IsNotNull(
                replay.targetGraphic,
                "The Replay Level button has no target Graphic, so it presents no raycast " +
                "surface and can never be clicked.");
            Assert.IsTrue(
                replay.targetGraphic.raycastTarget,
                "The Replay Level button's Graphic has raycastTarget disabled, so clicks pass " +
                "through it.");

            var label = button.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(label, "The Replay Level button has no label.");
            Assert.AreEqual(LevelResultsCopy.ReplayLevelLabel, label.text);
        }

        [Test]
        public void ReplayPressed_DoesNotAdvanceTheSelectedLevel()
        {
            ProgressManager progress = CreateProgressManager();
            progress.MarkLevelComplete(1, 2);
            progress.TrySetSelectedLevelNumber(1);
            VictoryScreenUI screen = CreateScreen();

            // No SceneLoader singleton exists in EditMode and creating one would really load a
            // scene, so the handler's "SceneLoader not available" LogError is expected noise.
            LogAssert.ignoreFailingMessages = true;
            InvokePrivate(screen, "OnReplayPressed");

            Assert.AreEqual(
                1,
                progress.GetSelectedLevelNumber(),
                "Replay reloads the level just finished. Calling TrySetSelectedLevelNumber " +
                "here would turn Replay into a second Next Level.");
        }

        [Test]
        public void NextLevelPressed_StillAdvances_ProvingTheReplayGuardDiscriminates()
        {
            ProgressManager progress = CreateProgressManager();
            progress.MarkLevelComplete(1, 2);
            progress.TrySetSelectedLevelNumber(1);
            VictoryScreenUI screen = CreateScreen();

            LogAssert.ignoreFailingMessages = true;
            InvokePrivate(screen, "OnNextLevelPressed");

            Assert.AreEqual(
                2,
                progress.GetSelectedLevelNumber(),
                "If this ever reads 1, the previous test proves nothing — both handlers would " +
                "be leaving the selected level alone for an unrelated reason.");
        }

        // ------------------------------------------------------------------
        // SALIN-253 AC-5 — era-aware Next Level suppression.
        // (This is SALIN-258's deferred AC-3, recorded at docs/audit/BACKLOG.md:719.)
        //
        // ⚠️ WHY THESE LEVELS AND NOT LEVEL 15.
        // The rule this replaced was `currentLevel >= 15`. That rule and a correct era-aware
        // rule AGREE ON EVERY LEVEL IN THE CAMPAIGN EXCEPT GLOBAL 5 AND GLOBAL 10 — the ends
        // of Ugat and Ugnayan. A Next-Level test written at level 15, which is the natural
        // first instinct, therefore passes whether or not AC-5 works and proves nothing.
        //
        // The pair below is global 5 (era-final: the button must be HIDDEN, where the old rule
        // SHOWED it) and global 4 (mid-era: still SHOWN). The second is the control — without
        // it, a change that simply hid the button always would satisfy the first.
        // ------------------------------------------------------------------

        [Test]
        public void NextLevelButton_IsHiddenAfterUgatLevel5()
        {
            ProgressManager progress = CreateProgressManager();
            progress.TrySetSelectedLevelNumber(5);
            VictoryScreenUI screen = CreateScreen();
            Button nextLevel = AttachNextLevelButton(screen);

            screen.PresentResults(Results(stars: 3), isEraFinalLevel: true);

            Assert.IsFalse(
                nextLevel.gameObject.activeSelf,
                "Ugat Level 5 is the end of an era. The old `currentLevel >= 15` rule left " +
                "Next Level showing here and advanced the player straight into Ugnayan " +
                "Level 1, skipping the era completion flow entirely.");
        }

        [Test]
        public void NextLevelButton_IsShownAfterUgatLevel4()
        {
            ProgressManager progress = CreateProgressManager();
            progress.TrySetSelectedLevelNumber(4);
            VictoryScreenUI screen = CreateScreen();
            Button nextLevel = AttachNextLevelButton(screen);

            screen.PresentResults(Results(stars: 3), isEraFinalLevel: false);

            Assert.IsTrue(
                nextLevel.gameObject.activeSelf,
                "Level 4 is mid-era and must still offer Next Level. Without this control, a " +
                "change that hid the button unconditionally would satisfy the test above.");
        }

        /// <summary>
        /// REGRESSION GUARD, NOT AC-5 COVERAGE. The campaign's final level hides Next Level
        /// under the old rule and the new one alike, so this case cannot discriminate between
        /// them. It exists only to prove the global fallback was not deleted along the way.
        /// </summary>
        [Test]
        public void NextLevelButton_IsStillHiddenAtTheFinalLevel_RegressionGuard()
        {
            ProgressManager progress = CreateProgressManager();
            progress.TrySetSelectedLevelNumber(15);
            VictoryScreenUI screen = CreateScreen();
            Button nextLevel = AttachNextLevelButton(screen);

            // No flag pushed — the legacy path. The global check must still hold on its own.
            screen.PresentResults(Results(stars: 3));

            Assert.IsFalse(
                nextLevel.gameObject.activeSelf,
                "The legacy progress path pushes no era flag, so `currentLevel >= 15` must " +
                "survive as the fallback. The degraded path must never GAIN a Next Level " +
                "button it did not have before.");
        }

        // ------------------------------------------------------------------
        // OWNER RULING R1 (2026-09-13) + D-006.
        // ------------------------------------------------------------------

        [Test]
        public void ResultsCopy_OffersNoAccuracyReadoutAndNoTracingProse()
        {
            foreach (FieldInfo field in typeof(LevelResultsCopy).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                    continue;

                var value = (string)field.GetValue(null);
                Assert.IsFalse(
                    value.ToLowerInvariant().Contains("tracing"),
                    "LevelResultsCopy." + field.Name + " = \"" + value + "\" carries player-facing " +
                    "\"tracing\" prose. D-006 rules prose and UI strings say \"drawing\"; code " +
                    "identifiers are explicitly unchanged.");
                Assert.IsFalse(
                    value.ToLowerInvariant().Contains("accuracy"),
                    "LevelResultsCopy." + field.Name + " = \"" + value + "\" shows an accuracy " +
                    "figure. Owner ruling R1 cut the accuracy DISPLAY from this screen. It did " +
                    "NOT cut the scoring input — see LevelResultsScoringWeightPinTests.");
            }
        }

        // ------------------------------------------------------------------
        // Helpers.
        // ------------------------------------------------------------------

        private static LevelResults Results(int stars)
        {
            return new LevelResults(new Dictionary<string, float>(), stars);
        }

        private VictoryScreenUI CreateScreen()
        {
            _screenObject = new GameObject("VictoryScreen_Test");
            GameObject panel = new GameObject("VictoryPanel", typeof(RectTransform));
            panel.transform.SetParent(_screenObject.transform, false);
            panel.SetActive(false);
            VictoryScreenUI screen = _screenObject.AddComponent<VictoryScreenUI>();
            SetPrivateField(screen, "_panel", panel);
            return screen;
        }

        /// <summary>
        /// SALIN-253. Gives the screen a real _nextLevelButton to toggle.
        ///
        /// Gameplay.unity and Level_01_Tutorial.unity both author this field — it is covered by
        /// VictoryScreenSceneWiringTests:53 — but this fixture builds a bare screen, so without
        /// this the visibility branch would run against a null and every assertion about the
        /// button would silently pass on an object that never existed.
        /// </summary>
        private static Button AttachNextLevelButton(VictoryScreenUI screen)
        {
            GameObject buttonObject = new GameObject(
                "NextLevelButton_Test", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(Panel(screen).transform, false);

            var button = buttonObject.GetComponent<Button>();
            SetPrivateField(screen, "_nextLevelButton", button);
            return button;
        }

        private ProgressManager CreateProgressManager()
        {
            _progressManagerObject = new GameObject("ProgressManager_Test");
            ProgressManager progress = _progressManagerObject.AddComponent<ProgressManager>();
            // EditMode never runs Awake on AddComponent, and Awake is what assigns Instance.
            InvokePrivate(progress, "Awake");
            progress.ClearAllProgress();
            return progress;
        }

        private static GameObject Panel(VictoryScreenUI screen) =>
            GetPrivateField<GameObject>(screen, "_panel");

        private static TextMeshProUGUI StarCountText(VictoryScreenUI screen) =>
            GetPrivateField<TextMeshProUGUI>(screen, "_starCountText");

        private static GameObject[] StarIcons(VictoryScreenUI screen) =>
            GetPrivateField<GameObject[]>(screen, "_starIcons");

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing serialized field '" + name + "'.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing serialized field '" + name + "'.");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = null;
            for (System.Type type = target.GetType(); type != null && method == null; type = type.BaseType)
            {
                method = type.GetMethod(
                    methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            Assert.IsNotNull(method, "Missing method '" + methodName + "'.");
            method.Invoke(target, null);
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")
                .GetSetMethod(true)
                .Invoke(null, new object[] { null });
        }
    }
}
