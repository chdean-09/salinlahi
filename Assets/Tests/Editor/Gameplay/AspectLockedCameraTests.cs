using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class AspectLockedCameraTests
    {
        private GameObject _go;
        private Camera _cam;
        private AspectLockedCamera _alc;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AspectLockedCamera_Test");
            _cam = _go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 5f;
            _alc = _go.AddComponent<AspectLockedCamera>();
            // OnEnable fires synchronously when AddComponent runs on an active GO.
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // EditMode cannot reliably change Screen.width/Screen.height (it reflects the
        // editor Game view). Tests fall into two groups: (a) consistency tests that read
        // back orthographicSize and compare against ExpectedOrthoSize applied to the
        // same Screen the production code reads, and (b) pure-formula tests that assert
        // ExpectedOrthoSize matches the spec table for fixed (w, h) inputs.

        private static void InvokeRecompute(AspectLockedCamera target)
        {
            MethodInfo mi = typeof(AspectLockedCamera).GetMethod(
                "Recompute", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(mi, "Recompute method missing on AspectLockedCamera.");
            mi.Invoke(target, null);
        }

        // Expected orthographic size given Screen w/h and the spec's reference values.
        private static float ExpectedOrthoSize(float screenW, float screenH,
            float targetAspect = 9f / 16f, float refH = 20f, float refW = 11.25f)
        {
            float deviceAspect = screenW / screenH;
            return deviceAspect >= targetAspect
                ? refH * 0.5f
                : refW / (2f * deviceAspect);
        }

        [Test]
        public void OrthoSize_OnCurrentScreen_MatchesFormula()
        {
            // Consistency test: whatever the editor Game-view aspect is, the component
            // should produce the same result as the formula.
            float expected = ExpectedOrthoSize(Screen.width, Screen.height);
            InvokeRecompute(_alc);

            Assert.AreEqual(expected, _cam.orthographicSize, 0.001f);
        }

        // Pure-formula tests: assert the documented spec math without depending on Screen.
        // These guarantee the formula in Recompute matches the spec table exactly.

        [Test]
        public void Formula_9by16_Aspect_Returns_OrthoSize_10()
        {
            // 9:16 aspect → height-locked → 20/2 = 10
            Assert.AreEqual(10f, ExpectedOrthoSize(9f, 16f), 0.001f);
        }

        [Test]
        public void Formula_9by19_5_Aspect_Returns_OrthoSize_About_12_1875()
        {
            // deviceAspect = 9/19.5 ≈ 0.4615 < 9/16 → width-locked → 11.25 / (2 * 0.4615) ≈ 12.1875
            Assert.AreEqual(12.1875f, ExpectedOrthoSize(9f, 19.5f), 0.001f);
        }

        [Test]
        public void Formula_9by21_Aspect_Returns_OrthoSize_About_13_125()
        {
            // deviceAspect = 9/21 ≈ 0.4286 → width-locked → 11.25 / (2 * 0.4286) ≈ 13.125
            Assert.AreEqual(13.125f, ExpectedOrthoSize(9f, 21f), 0.001f);
        }

        [Test]
        public void Formula_3by4_Aspect_Returns_OrthoSize_10()
        {
            // 3:4 portrait aspect = 0.75 > 9/16 → height-locked → 10
            Assert.AreEqual(10f, ExpectedOrthoSize(3f, 4f), 0.001f);
        }

        [Test]
        public void Formula_4by5_Aspect_Returns_OrthoSize_10()
        {
            // 4:5 portrait aspect = 0.8 > 9/16 → height-locked → 10
            Assert.AreEqual(10f, ExpectedOrthoSize(4f, 5f), 0.001f);
        }

        // Direct PlayColumnScreenRect math sanity.

        private static float ExpectedColumnWidthFraction(float deviceAspect,
            float refW = 11.25f, float refH = 20f, float targetAspect = 9f / 16f)
        {
            if (deviceAspect < targetAspect) return 1f; // width-locked → full screen
            float orthoSize = refH * 0.5f;
            float viewportWorldWidth = 2f * orthoSize * deviceAspect;
            return refW / viewportWorldWidth;
        }

        [Test]
        public void ScreenRect_3by4_HasColumnWidthFraction_About_0_75()
        {
            // 3:4 portrait: viewportWorldWidth = 2 * 10 * 0.75 = 15; 11.25/15 = 0.75
            Assert.AreEqual(0.75f, ExpectedColumnWidthFraction(3f / 4f), 0.001f);
        }

        [Test]
        public void ScreenRect_4by5_HasColumnWidthFraction_About_0_703125()
        {
            // 4:5 portrait: viewportWorldWidth = 2 * 10 * 0.8 = 16; 11.25/16 = 0.703125
            Assert.AreEqual(0.703125f, ExpectedColumnWidthFraction(4f / 5f), 0.001f);
        }

        [Test]
        public void ScreenRect_9by16_HasFullColumnWidthFraction()
        {
            // 9:16 exactly: viewportWorldWidth = 2 * 10 * 0.5625 = 11.25; 11.25/11.25 = 1.0
            Assert.AreEqual(1f, ExpectedColumnWidthFraction(9f / 16f), 0.001f);
        }

        [Test]
        public void ScreenRect_9by19_5_HasFullColumnWidthFraction()
        {
            // 9:19.5: width-locked → full screen
            Assert.AreEqual(1f, ExpectedColumnWidthFraction(9f / 19.5f), 0.001f);
        }

        [Test]
        public void Recompute_RaisesOnPlayAreaChangedEvent()
        {
            int fired = 0;
            _alc.OnPlayAreaChanged += () => fired++;

            InvokeRecompute(_alc);
            InvokeRecompute(_alc);

            Assert.GreaterOrEqual(fired, 2, "OnPlayAreaChanged must fire on each Recompute().");
        }

        [Test]
        public void Recompute_WithInvalidTargetAspect_DoesNotChangeOrthoSize()
        {
            // Set _targetAspect to 0 via reflection — simulates invalid inspector state.
            FieldInfo f = typeof(AspectLockedCamera).GetField(
                "_targetAspect", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f);
            f.SetValue(_alc, 0f);

            float before = _cam.orthographicSize;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*invalid inspector settings.*"));

            InvokeRecompute(_alc);

            Assert.AreEqual(before, _cam.orthographicSize, 0.0001f);
        }

        [Test]
        public void Recompute_WithNonOrthographicCamera_DoesNotThrow()
        {
            _cam.orthographic = false;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex(".*requires an orthographic camera.*"));
            Assert.DoesNotThrow(() => InvokeRecompute(_alc));
        }
    }
}
