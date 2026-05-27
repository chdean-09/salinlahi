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

            float waited = 0f;
            while (waited < 1f && GetPrivateField<bool>(_player, "_isTypewriting"))
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

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
            yield return null;
            yield return null;

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

            float waited = 0f;
            while (waited < 1f && GetPrivateField<bool>(_player, "_isTypewriting"))
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
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
            yield return null;

            CanvasGroup cg = GetPrivateField<CanvasGroup>(_player, "_canvasGroup");
            Assert.AreEqual(1f, cg.alpha, "Alpha should be 1 for None transition");
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
