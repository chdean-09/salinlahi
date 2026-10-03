using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.UI
{
    [TestFixture]
    public sealed class LevelButtonHighlightTests
    {
        private static readonly Vector3 AuthoredBase = new(0.9f, 1.2f, 1f);

        private readonly List<Object> _objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator HighlightedButton_HoldsSteadyScale_InsteadOfPulsing()
        {
            LevelButton button = CreateButton();

            button.SetHighlighted(true);

            AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                "immediately after SetHighlighted(true)");

            float end = Time.realtimeSinceStartup + 1.3f;
            while (Time.realtimeSinceStartup < end)
            {
                AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                    "while highlighted");
                yield return null;
            }
            AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                "at the end of the observation window");
        }

        [UnityTest]
        public IEnumerator HighlightToggle_Repeated_DoesNotDriftFromAuthoredBase()
        {
            LevelButton button = CreateButton();

            for (int i = 0; i < 5; i++)
            {
                button.SetHighlighted(true);
                button.SetHighlighted(true);
                AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                    $"after highlight on, iteration {i}");
                yield return null;
                AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                    $"a frame after highlight on, iteration {i}");

                button.SetHighlighted(false);
                AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                    $"after highlight off, iteration {i}");
                yield return null;
                AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                    $"a frame after highlight off, iteration {i}");
            }
        }

        [UnityTest]
        public IEnumerator DisableRestoresBase_ReenableKeepsAuthoredSize()
        {
            LevelButton button = CreateButton();

            button.SetHighlighted(true);
            yield return null;

            button.gameObject.SetActive(false);
            AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                "OnDisable must keep the authored base scale");

            button.gameObject.SetActive(true);
            AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                "OnEnable must keep the authored base scale while highlighted");
            yield return null;
            AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                "size stays at authored base after re-enable");

            button.SetHighlighted(false);
            button.gameObject.SetActive(false);
            yield return null;
            button.gameObject.SetActive(true);
            yield return null;
            AssertScaleApproximately(AuthoredBase, button.transform.localScale,
                "with the highlight cleared, disable/enable leaves the base scale alone");
        }

        private LevelButton CreateButton()
        {
            var go = new GameObject("LevelButton_HighlightTest", typeof(RectTransform));
            _objectsToDestroy.Add(go);
            go.transform.localScale = AuthoredBase;
            return go.AddComponent<LevelButton>();
        }

        private static void AssertScaleApproximately(Vector3 expected, Vector3 actual, string when)
        {
            Assert.IsTrue(
                Mathf.Approximately(expected.x, actual.x)
                && Mathf.Approximately(expected.y, actual.y)
                && Mathf.Approximately(expected.z, actual.z),
                $"localScale {when}: expected {expected}, got {actual}");
        }
    }
}
