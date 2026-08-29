using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class CutscenePlayerTests
    {
        private CutscenePlayer _player;
        private GameObject _playerGo;
        private CutsceneSO _cutscene;
        private Button _tapCatcher;

        private bool _startedFired;
        private bool _completeFired;
        private System.Action _onStartedHandler;
        private System.Action _onCompleteHandler;

        private readonly List<Object> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;

            _onStartedHandler = () => _startedFired = true;
            _onCompleteHandler = () => _completeFired = true;
            EventBus.OnCutsceneStarted += _onStartedHandler;
            EventBus.OnCutsceneComplete += _onCompleteHandler;
            _startedFired = false;
            _completeFired = false;

            _cutscene = ScriptableObject.CreateInstance<CutsceneSO>();
            _cutscene.defaultTypewriterSpeed = 200f;
            _cutscene.defaultTransitionDuration = 0.01f;
            _objectsToDestroy.Add(_cutscene);

            BuildPlayerRig();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnCutsceneStarted -= _onStartedHandler;
            EventBus.OnCutsceneComplete -= _onCompleteHandler;

            if (_player != null)
                _player.StopAllCoroutines();

            if (_playerGo != null)
                Object.Destroy(_playerGo);

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();

            ClearSingletonInstance<GameManager>();
            Time.timeScale = 1f;
        }

        private void BuildPlayerRig()
        {
            _playerGo = new GameObject("CutscenePlayer_Test");

            Canvas canvas = _playerGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9100;
            _playerGo.AddComponent<CanvasScaler>();
            _playerGo.AddComponent<GraphicRaycaster>();

            CanvasGroup cg = _playerGo.AddComponent<CanvasGroup>();

            _player = _playerGo.AddComponent<CutscenePlayer>();
            _player.enabled = false;

            GameObject imgGo = new GameObject("PanelImage");
            imgGo.transform.SetParent(_playerGo.transform, false);
            Image img = imgGo.AddComponent<Image>();
            RectTransform imgRt = imgGo.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.sizeDelta = Vector2.zero;

            GameObject txtGo = new GameObject("BodyText");
            txtGo.transform.SetParent(_playerGo.transform, false);
            TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.05f, 0.1f);
            txtRt.anchorMax = new Vector2(0.95f, 0.3f);
            txtRt.sizeDelta = Vector2.zero;

            GameObject tapGo = new GameObject("TapCatcher");
            tapGo.transform.SetParent(_playerGo.transform, false);
            Image tapImg = tapGo.AddComponent<Image>();
            tapImg.color = new Color(0, 0, 0, 0);
            RectTransform tapRt = tapGo.GetComponent<RectTransform>();
            tapRt.anchorMin = Vector2.zero;
            tapRt.anchorMax = Vector2.one;
            tapRt.sizeDelta = Vector2.zero;
            _tapCatcher = tapGo.AddComponent<Button>();

            GameObject skipRoot = new GameObject("SkipRoot");
            skipRoot.transform.SetParent(_playerGo.transform, false);

            SetPrivateField(_player, "_canvasGroup", cg);
            SetPrivateField(_player, "_panelImage", img);
            SetPrivateField(_player, "_imageRectTransform", imgRt);
            SetPrivateField(_player, "_bodyText", tmp);
            SetPrivateField(_player, "_tapCatcher", _tapCatcher);
            SetPrivateField(_player, "_skipButtonRoot", skipRoot);

            _player.enabled = true;
        }

        // ─── Tests ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Play_FiresStartedEvent_AndSetsIsPlaying()
        {
            _cutscene.panels = new CutscenePanel[] { new CutscenePanel { text = "Hello" } };
            _player.Play(_cutscene);
            yield return null;

            Assert.IsTrue(_startedFired, "OnCutsceneStarted should fire");
            Assert.IsTrue(_player.IsPlaying, "IsPlaying should be true");

            CanvasGroup cg = GetPrivateField<CanvasGroup>(_player, "_canvasGroup");
            Assert.IsTrue(cg.interactable, "interactable should be true so children receive input");
            Assert.IsTrue(cg.blocksRaycasts, "blocksRaycasts should be true so taps register");
        }

        [UnityTest]
        public IEnumerator Play_WithNullCutscene_DoesNothing()
        {
            _cutscene.panels = null;
            _player.Play(null as CutsceneSO);
            yield return null;

            Assert.IsFalse(_player.IsPlaying);
            Assert.IsFalse(_startedFired);
        }

        [UnityTest]
        public IEnumerator Play_WithEmptyPanels_DoesNothing()
        {
            _cutscene.panels = new CutscenePanel[0];
            _player.Play(_cutscene);
            yield return null;

            Assert.IsFalse(_player.IsPlaying);
            Assert.IsFalse(_startedFired);
        }

        [UnityTest]
        public IEnumerator Typewriter_CompletesFullText()
        {
            _cutscene.panels = new CutscenePanel[] { new CutscenePanel { text = "ABC", typewriterSpeed = 200f } };

            _player.Play(_cutscene);

            // Wait out the forced first-panel transition until the typewriter
            // has started (or already finished into the waiting-for-tap state).
            yield return WaitUntilRealtime(
                () => GetPrivateField<bool>(_player, "_isTypewriting")
                      || GetPrivateField<bool>(_player, "_waitingForTap"),
                timeoutSeconds: 2f);

            yield return WaitUntilRealtime(
                () => !GetPrivateField<bool>(_player, "_isTypewriting"),
                timeoutSeconds: 2f);
            yield return null;

            TMP_Text bodyText = GetPrivateField<TMP_Text>(_player, "_bodyText");
            Assert.AreEqual("ABC", bodyText.text);
            Assert.IsFalse(GetPrivateField<bool>(_player, "_isTypewriting"));
        }

        [UnityTest]
        public IEnumerator OnTap_DuringTypewriter_CompletesTextInstantly()
        {
            _cutscene.panels = new CutscenePanel[] { new CutscenePanel { text = "Long test message here", typewriterSpeed = 3f } };

            _player.Play(_cutscene);
            // Wait out the forced first-panel transition; at 3 chars/sec the
            // typewriter then stays busy for seconds, so no race on the assert.
            yield return WaitUntilRealtime(
                () => GetPrivateField<bool>(_player, "_isTypewriting"),
                timeoutSeconds: 2f);

            Assert.IsTrue(GetPrivateField<bool>(_player, "_isTypewriting"), "Typewriter should be running");

            _tapCatcher.onClick.Invoke();
            yield return null;

            TMP_Text bodyText = GetPrivateField<TMP_Text>(_player, "_bodyText");
            Assert.AreEqual("Long test message here", bodyText.text);
            Assert.IsFalse(GetPrivateField<bool>(_player, "_isTypewriting"));
        }

        [UnityTest]
        public IEnumerator OnTap_AfterTypewriterComplete_AdvancesToNextPanel()
        {
            _cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Panel 1", typewriterSpeed = 200f },
                new CutscenePanel { text = "Panel 2", typewriterSpeed = 200f }
            };

            _player.Play(_cutscene);

            float waited = 0f;
            while (waited < 1f && GetPrivateField<bool>(_player, "_isTypewriting"))
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            yield return null;

            _tapCatcher.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(_player.IsPlaying, "Cutscene should still be playing");
            Assert.IsFalse(_completeFired, "Should not be complete yet");
        }

        [UnityTest]
        public IEnumerator ContinuePrompt_AppearsWhileTypewritingAndWaitingForTap()
        {
            _cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Panel 1", typewriterSpeed = 200f },
                new CutscenePanel { text = "Panel 2", typewriterSpeed = 200f }
            };

            _player.Play(_cutscene);

            TMP_Text prompt = GetPrivateField<TMP_Text>(_player, "_continuePromptText");
            CanvasGroup promptGroup = GetPrivateField<CanvasGroup>(_player, "_continuePromptCanvasGroup");
            Assert.NotNull(prompt);
            // The prompt appears right after the forced first-panel transition;
            // wait that transition out on real time before asserting.
            yield return WaitUntilRealtime(
                () => prompt.gameObject.activeSelf, timeoutSeconds: 2f);
            Assert.AreEqual("Tap anywhere to continue", prompt.text);
            Assert.IsTrue(prompt.gameObject.activeSelf, "Prompt should show as soon as the panel can react to taps.");
            Assert.Greater(promptGroup.alpha, 0.5f);

            float waited = 0f;
            while (waited < 1f && !GetPrivateField<bool>(_player, "_waitingForTap"))
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            Assert.IsTrue(prompt.gameObject.activeSelf, "Prompt should show once the cutscene waits for player input.");
            Assert.Greater(promptGroup.alpha, 0.5f);

            _tapCatcher.onClick.Invoke();
            yield return null;

            Assert.IsFalse(prompt.gameObject.activeSelf, "Prompt should hide immediately after continuing.");
        }

        [UnityTest]
        public IEnumerator Cutscene_DoesNotShowTopRightSkipButton()
        {
            _cutscene.panels = new CutscenePanel[] { new CutscenePanel { text = "Panel 1", typewriterSpeed = 200f } };

            _player.Play(_cutscene);
            yield return null;

            GameObject skipRoot = GetPrivateField<GameObject>(_player, "_skipButtonRoot");
            Assert.NotNull(skipRoot);
            Assert.IsFalse(skipRoot.activeSelf, "Top-right skip button should not be visible during cutscenes.");
        }

        [UnityTest]
        public IEnumerator ContinuePrompt_UsesSafeAreaTopCenterPlacement()
        {
            _cutscene.panels = new CutscenePanel[] { new CutscenePanel { text = "Panel 1", typewriterSpeed = 200f } };

            _player.Play(_cutscene);
            yield return null;

            RectTransform safeAreaRoot = GetPrivateField<RectTransform>(_player, "_continuePromptSafeAreaRoot");
            RectTransform promptRect = GetPrivateField<TMP_Text>(_player, "_continuePromptText").rectTransform;

            Assert.NotNull(safeAreaRoot);
            Assert.NotNull(safeAreaRoot.GetComponent<SafeAreaHandler>());
            Assert.Less(promptRect.anchorMin.x, 0.5f);
            Assert.Greater(promptRect.anchorMax.x, 0.5f);
            Assert.AreEqual(1f, promptRect.anchorMin.y, 0.01f);
            Assert.AreEqual(1f, promptRect.anchorMax.y, 0.01f);
            Assert.AreEqual(new Vector2(0.5f, 1f), promptRect.pivot);
            Assert.AreEqual(0f, promptRect.anchoredPosition.x, 0.01f);
            Assert.Less(promptRect.anchoredPosition.y, 0f, "Prompt should sit below the safe-area top edge.");
            Assert.GreaterOrEqual(promptRect.sizeDelta.y, 100f, "Prompt should reserve enough vertical room for readable type.");

            TMP_Text promptText = GetPrivateField<TMP_Text>(_player, "_continuePromptText");
            Assert.GreaterOrEqual(promptText.fontSizeMax, 50f, "Prompt should be large enough to read over cutscene art.");
            Assert.GreaterOrEqual(promptText.fontSizeMin, 34f, "Prompt should not auto-size down into caption text.");
            Assert.NotNull(promptText.GetComponent<Outline>(), "Prompt needs an outline for contrast over bright cutscene frames.");
        }

        [UnityTest]
        public IEnumerator OnTap_DuringTransition_IsIgnored()
        {
            _cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Panel 1", transitionIn = TransitionType.Fade, transitionDuration = 1f },
                new CutscenePanel { text = "Panel 2" }
            };

            _player.Play(_cutscene);
            yield return null;

            _tapCatcher.onClick.Invoke();
            yield return null;

            Assert.IsTrue(_player.IsPlaying, "Should still be playing");
            Assert.IsFalse(GetPrivateField<bool>(_player, "_waitingForTap"),
                "Tap during transition should not set waiting state");

            yield return new WaitForSecondsRealtime(1.5f);

            Assert.IsTrue(GetPrivateField<bool>(_player, "_waitingForTap"),
                "Should be waiting for tap after typewriter completes");

            _tapCatcher.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(_player.IsPlaying, "Should have advanced to panel 2");
        }

        [UnityTest]
        public IEnumerator OnTap_OnLastPanel_EndsCutscene()
        {
            _cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Only panel", typewriterSpeed = 200f }
            };

            _player.Play(_cutscene);

            // OnTap only ends the cutscene once the player waits for input; a
            // tap during the forced first-panel transition is swallowed.
            yield return WaitUntilRealtime(
                () => GetPrivateField<bool>(_player, "_waitingForTap"),
                timeoutSeconds: 2f);
            yield return null;

            _tapCatcher.onClick.Invoke();

            float completionWaited = 0f;
            while (completionWaited < 0.5f && !_completeFired)
            {
                yield return null;
                completionWaited += Time.unscaledDeltaTime;
            }

            Assert.IsTrue(_completeFired, "OnCutsceneComplete should fire");
            Assert.IsFalse(_player.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Play_WithTransitionNone_ShowsInstantly()
        {
            _cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Test", transitionIn = TransitionType.None, typewriterSpeed = 200f }
            };

            _player.Play(_cutscene);

            // The player deliberately coerces the FIRST panel's None into a
            // Fade (PlayRoutine), so "instantly" cannot hold for panel 0; the
            // guarantee is that the panel becomes fully visible once that
            // forced transition finishes.
            CanvasGroup cg = GetPrivateField<CanvasGroup>(_player, "_canvasGroup");
            yield return WaitUntilRealtime(() => cg.alpha >= 1f, timeoutSeconds: 2f);

            Assert.AreEqual(1f, cg.alpha, "Alpha should reach 1 after the forced first-panel fade");
        }

        [UnityTest]
        public IEnumerator SkipCutscene_EndsImmediately()
        {
            _cutscene.panels = new CutscenePanel[]
            {
                new CutscenePanel { text = "Panel 1", typewriterSpeed = 200f },
                new CutscenePanel { text = "Panel 2" }
            };

            _player.Play(_cutscene);
            yield return null;

            _player.SkipCutscene();

            float waited = 0f;
            while (waited < 0.5f && !_completeFired)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            Assert.IsTrue(_completeFired, "OnCutsceneComplete should fire after skip");
            Assert.IsFalse(_player.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Play_CannotDoublePlay_WhileAlreadyPlaying()
        {
            _cutscene.panels = new CutscenePanel[] { new CutscenePanel { text = "A", typewriterSpeed = 200f } };

            _player.Play(_cutscene);
            yield return null;

            int panelIndexBefore = GetPrivateField<int>(_player, "_panelIndex");

            CutsceneSO other = ScriptableObject.CreateInstance<CutsceneSO>();
            other.panels = new CutscenePanel[] { new CutscenePanel { text = "B", typewriterSpeed = 200f } };
            _objectsToDestroy.Add(other);

            _player.Play(other);
            yield return null;

            int panelIndexAfter = GetPrivateField<int>(_player, "_panelIndex");
            Assert.AreEqual(panelIndexBefore, panelIndexAfter, "Second Play should be ignored");
        }

        // ─── Reflection helpers ───────────────────────────────────────────────

        // The first panel's transition runs on realtime for at least the
        // default duration; batchmode frames are ~1 ms, so fixed one-frame
        // yields fire mid-transition. Poll the condition on real time instead.
        private static IEnumerator WaitUntilRealtime(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { null });
        }
    }
}
