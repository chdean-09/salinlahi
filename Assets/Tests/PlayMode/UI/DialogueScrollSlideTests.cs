using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.UI
{
    /// <summary>
    /// The scroll's entrance and exit can only be observed in play mode: the tween runs on a
    /// coroutine, and an edit-mode run has no player loop to advance it. These watch the
    /// panel's transform directly rather than through a screenshot, which cannot sample a
    /// 0.35s slide finely enough in batch mode.
    /// </summary>
    public class DialogueScrollSlideTests
    {
        private GameManager _gameManager;
        private DialogueController _controller;
        private DialogueSO _dialogue;

        [SetUp]
        public void SetUp()
        {
            // A leftover DontDestroyOnLoad manager from an earlier test would be destroyed as
            // a duplicate and null out Instance, so reuse whatever is already there.
            _gameManager = GameManager.Instance;
            if (_gameManager == null)
                _gameManager = new GameObject("TestGameManager").AddComponent<GameManager>();
            _gameManager.ExitDialoguePause();
            _gameManager.StartGame();

            _controller = DialogueController.CreateRuntime();

            _dialogue = ScriptableObject.CreateInstance<DialogueSO>();
            _dialogue.lines = new[]
            {
                new DialogueLine { speakerName = "Tagapagsalaysay", text = "Isang alaala." },
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null)
                Object.DestroyImmediate(_controller.transform.root.gameObject);
            if (_gameManager != null)
                Object.DestroyImmediate(_gameManager.gameObject);
            if (_dialogue != null)
                Object.DestroyImmediate(_dialogue);

            Time.timeScale = 1f;
        }

        private RectTransform PanelRect()
        {
            // The overlay the controller built for itself.
            Transform overlay = _controller.transform.Find("DialogueOverlay");
            Assert.IsNotNull(overlay, "The runtime dialogue overlay was not built.");
            return overlay as RectTransform;
        }

        [UnityTest]
        public IEnumerator Scroll_RisesFromBelowTheScreen_BeforeAnyCopyAppears()
        {
            _controller.Play(_dialogue);
            RectTransform panel = PanelRect();

            // Read before yielding: a slow batch-mode frame can be long enough to carry the
            // whole 0.35s slide, which would hide the parked position entirely.
            float start = panel.anchoredPosition.y;
            Assert.Less(start, -1f, "The scroll should start below the screen.");

            float previous = start;
            bool everMovedUp = false;

            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline
                   && panel.anchoredPosition.y < -0.5f)
            {
                float current = panel.anchoredPosition.y;
                Assert.GreaterOrEqual(current, previous - 0.01f,
                    "The scroll must not dip back down while rising.");
                if (current > previous)
                    everMovedUp = true;
                previous = current;
                yield return null;
            }

            Assert.IsTrue(everMovedUp, "The scroll never moved — the slide did not run.");
            Assert.AreEqual(0f, panel.anchoredPosition.y, 0.5f,
                "The scroll should settle flush with the bottom of the screen.");
        }

        [UnityTest]
        public IEnumerator Scroll_SinksAndHides_AfterTheLastLine()
        {
            _controller.Play(_dialogue);
            RectTransform panel = PanelRect();

            // Wait for the copy, not for the position: the panel reaches its resting Y at the
            // end of the nested slide, but the controller is not idle until the parent
            // coroutine resumes a frame later. Tapping in that window is swallowed by the
            // "still sliding" guard, so waiting on the rendered line is what makes this stable.
            TMPro.TMP_Text speaker =
                panel.Find("SpeakerText").GetComponent<TMPro.TMP_Text>();
            float settleDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < settleDeadline
                   && string.IsNullOrEmpty(speaker.text))
            {
                yield return null;
            }

            Assert.IsNotEmpty(speaker.text, "The line never rendered after the scroll rose.");

            GameObject overlay = panel.gameObject;
            Assert.IsTrue(overlay.activeInHierarchy, "The scroll should be on screen.");

            bool completed = false;
            void OnComplete() => completed = true;
            EventBus.OnDialogueComplete += OnComplete;

            try
            {
                // Skip the typewriter, then advance past the final line.
                _controller.SendMessage("SkipTypewriter");
                yield return null;
                _controller.SendMessage("OnTapCatcherPressed");

                // Checked synchronously, which is what makes this frame-rate independent: a
                // batch-mode frame can be longer than the 0.30s tween, so the intermediate
                // positions are not reliably observable. What IS observable is that the tap
                // no longer tears the panel down on the spot — it withdraws first, and the
                // flow is not told the beat is over until it has.
                Assert.IsTrue(overlay.activeInHierarchy,
                    "The scroll must withdraw before it hides, not vanish on the tap.");
                Assert.IsFalse(completed,
                    "DialogueComplete must not fire until the scroll has withdrawn.");

                float deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline && overlay.activeInHierarchy)
                    yield return null;

                Assert.IsFalse(overlay.activeInHierarchy, "The scroll should end hidden.");
                Assert.IsTrue(completed, "DialogueComplete should fire once it has withdrawn.");
            }
            finally
            {
                EventBus.OnDialogueComplete -= OnComplete;
            }
        }
    }
}
