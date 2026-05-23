using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    // EditMode cannot reliably change Screen.width / Screen.height (it reflects the
    // editor Game view). Tests fall into two groups: (a) pure-formula tests that feed
    // explicit deviceAspect / orthoSize values, and (b) behavior tests that inject
    // mock Camera and SpriteRenderer dependencies. The Apply() handler wired to
    // OnPlayAreaChanged in production is verified manually in the Task 10 smoke pass.

    [TestFixture]
    public class PillarFillTests
    {
        // --- Geometry: ComputePillarWidth ---

        [Test]
        public void ComputePillarWidth_DeviceAspectLessThanTarget_ReturnsZero()
        {
            // 9:21 portrait phone, target 9:16, refW=11.25, orthoSize=10 (width-locked => 13.125 actually)
            // For this test we feed the values directly.
            float w = PillarFill.ComputePillarWidth(refWidth: 11.25f, orthoSize: 13.125f, deviceAspect: 9f / 21f);
            Assert.AreEqual(0f, w, 0.0001f);
        }

        [Test]
        public void ComputePillarWidth_DeviceAspectAtTarget_ReturnsZero()
        {
            // 9:16 exactly: viewportWorldWidth = 2 * 10 * 0.5625 = 11.25; (11.25 - 11.25)/2 = 0
            float w = PillarFill.ComputePillarWidth(11.25f, 10f, 9f / 16f);
            Assert.AreEqual(0f, w, 0.0001f);
        }

        [Test]
        public void ComputePillarWidth_3by4_Returns_1_875()
        {
            // viewportWorldWidth = 2 * 10 * 0.75 = 15; (15 - 11.25)/2 = 1.875
            float w = PillarFill.ComputePillarWidth(11.25f, 10f, 3f / 4f);
            Assert.AreEqual(1.875f, w, 0.0001f);
        }

        [Test]
        public void ComputePillarWidth_4by5_Returns_2_375()
        {
            // viewportWorldWidth = 2 * 10 * 0.8 = 16; (16 - 11.25)/2 = 2.375
            float w = PillarFill.ComputePillarWidth(11.25f, 10f, 4f / 5f);
            Assert.AreEqual(2.375f, w, 0.0001f);
        }

        // --- Apply() behavior: mode resolution ---

        private static GameObject MakeRig(out PillarFill pf, out Camera cam, out SpriteRenderer left, out SpriteRenderer right)
        {
            GameObject root = new GameObject("PF_Test");
            cam = root.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.backgroundColor = Color.red;

            GameObject leftGo = new GameObject("LeftPillar");
            leftGo.transform.SetParent(root.transform);
            left = leftGo.AddComponent<SpriteRenderer>();

            GameObject rightGo = new GameObject("RightPillar");
            rightGo.transform.SetParent(root.transform);
            right = rightGo.AddComponent<SpriteRenderer>();

            pf = root.AddComponent<PillarFill>();
            pf.InjectDependenciesForTests(cam, left, right);
            return root;
        }

        [Test]
        public void Apply_ModeNone_DisablesRenderers_AndLeavesCameraColorAlone()
        {
            var go = MakeRig(out var pf, out var cam, out var left, out var right);
            try
            {
                pf.ApplyForTests(PillarFillMode.None, color: Color.green, sprite: null);
                Assert.IsFalse(left.enabled);
                Assert.IsFalse(right.enabled);
                Assert.AreEqual(Color.red, cam.backgroundColor);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Apply_ModeColor_DisablesRenderers_AndOverwritesCameraColor()
        {
            var go = MakeRig(out var pf, out var cam, out var left, out var right);
            try
            {
                pf.ApplyForTests(PillarFillMode.Color, color: Color.green, sprite: null);
                Assert.IsFalse(left.enabled);
                Assert.IsFalse(right.enabled);
                Assert.AreEqual(Color.green, cam.backgroundColor);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Apply_ModeSprite_NullSprite_LogsWarningAndDisables()
        {
            var go = MakeRig(out var pf, out var cam, out var left, out var right);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex(".*no sprite assigned.*"));
                pf.ApplyForTests(PillarFillMode.Sprite, color: Color.white, sprite: null);
                Assert.IsFalse(left.enabled);
                Assert.IsFalse(right.enabled);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Apply_ModeSprite_WithoutPlayColumn_LogsWarningAndDisables()
        {
            var go = MakeRig(out var pf, out var cam, out var left, out var right);
            Sprite blank = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 4, 4),
                new Vector2(0.5f, 0.5f));
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex(".*AspectLockedCamera or Camera missing.*"));
                pf.ApplyForTests(PillarFillMode.Sprite, color: Color.white, sprite: blank);
                Assert.IsFalse(left.enabled);
                Assert.IsFalse(right.enabled);
            }
            finally
            {
                Object.DestroyImmediate(blank);
                Object.DestroyImmediate(go);
            }
        }
    }
}
