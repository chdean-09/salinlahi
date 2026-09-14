using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    /// <summary>
    /// The framing rule the eight-beat enemy lesson rests on: the enemy it is about must be inside
    /// the camera's view before the beat halts it.
    ///
    /// <para>
    /// <b>These numbers are measurements, not inventions.</b> A driven Level 1 play session found
    /// the camera seeing y -10.03..10.03 (orthographic, size 10.025, at the origin) while Iligaw and
    /// its mirror copy sat frozen at y = 11.40 for the entire lesson — 1.37 world units above the
    /// top of the frame. Beat 1 dimmed an empty field, beat 2's split was never seen, and the card
    /// named an enemy the player had never laid eyes on. Twelve hundred green tests had nothing to
    /// say about any of it, because nothing asserted where the subject was.
    /// </para>
    /// </summary>
    [TestFixture]
    public class EnemyIntroductionFramingTests
    {
        private const float MeasuredOrthographicSize = 10.025f;
        private const float MeasuredSpawnY = 11.40f;
        private const float MeasuredAspect = 900f / 1604f;

        private GameObject _cameraHost;

        [TearDown]
        public void TearDown()
        {
            if (_cameraHost != null)
                Object.DestroyImmediate(_cameraHost);
            _cameraHost = null;
        }

        private Camera CreateLevel1Camera()
        {
            _cameraHost = new GameObject("FramingTestCamera", typeof(Camera));
            _cameraHost.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = _cameraHost.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = MeasuredOrthographicSize;
            camera.aspect = MeasuredAspect;
            return camera;
        }

        [Test]
        public void CameraWorldRect_MatchesTheMeasuredOrthographicView()
        {
            Camera camera = CreateLevel1Camera();

            Assert.IsTrue(EnemyIntroductionBeat.TryGetCameraWorldRect(camera, out Rect view));
            Assert.AreEqual(10.025f, view.yMax, 0.01f, "Top edge of the visible world.");
            Assert.AreEqual(-10.025f, view.yMin, 0.01f, "Bottom edge of the visible world.");
        }

        [Test]
        public void EnemyAtTheMeasuredSpawnHeight_IsNotInsideTheCameraView()
        {
            Camera camera = CreateLevel1Camera();
            Bounds enemy = new Bounds(new Vector3(0f, MeasuredSpawnY, 0f), new Vector3(1f, 2f, 1f));

            Assert.IsFalse(
                EnemyIntroductionBeat.IsVerticallyInsideView(camera, enemy, marginWorld: 0.35f),
                "An enemy frozen at the spawn height the driven session measured is above the top "
                + "of the frame. The lesson must not begin here.");
        }

        [Test]
        public void EnemyThatHasWalkedIntoTheLane_IsInsideTheCameraView()
        {
            Camera camera = CreateLevel1Camera();
            Bounds enemy = new Bounds(new Vector3(0f, 6f, 0f), new Vector3(1f, 2f, 1f));

            Assert.IsTrue(
                EnemyIntroductionBeat.IsVerticallyInsideView(camera, enemy, marginWorld: 0.35f));
        }

        [Test]
        public void TheMarginKeepsAnEnemyGrazingTheTopEdgeOutOfFrame()
        {
            Camera camera = CreateLevel1Camera();

            // Top of the sprite exactly on the camera's top edge: inside by a bare reading of the
            // rect, and still wrong. Beat 7 raises a glyph badge above the enemy, which is how that
            // reveal became a twenty-pixel sliver clipped by the top of the screen.
            Bounds grazing = new Bounds(new Vector3(0f, 9.025f, 0f), new Vector3(1f, 2f, 1f));

            Assert.IsTrue(EnemyIntroductionBeat.IsVerticallyInsideView(camera, grazing, 0f),
                "Sanity check: with no margin this bound is inside the rect.");
            Assert.IsFalse(EnemyIntroductionBeat.IsVerticallyInsideView(camera, grazing, 0.35f),
                "With the beat's clearance it is not, and the beat keeps waiting.");
        }

        [Test]
        public void EnemyBelowTheBottomEdge_IsNotInsideTheCameraView()
        {
            Camera camera = CreateLevel1Camera();
            Bounds pastTheShrine = new Bounds(new Vector3(0f, -11f, 0f), new Vector3(1f, 2f, 1f));

            Assert.IsFalse(
                EnemyIntroductionBeat.IsVerticallyInsideView(camera, pastTheShrine, 0.35f));
        }

        [Test]
        public void ANullCameraNeverBlocksTheLesson()
        {
            // There is nothing to frame against, and a lesson that waits forever for a camera that
            // does not exist is a worse failure than one that plays unframed.
            Assert.IsTrue(EnemyIntroductionBeat.IsVerticallyInsideView(
                null, new Bounds(new Vector3(0f, 999f, 0f), Vector3.one), 0.35f));
        }

        [Test]
        public void APerspectiveCameraYieldsNoWorldRectAndNeverBlocksTheLesson()
        {
            _cameraHost = new GameObject("PerspectiveTestCamera", typeof(Camera));
            Camera camera = _cameraHost.GetComponent<Camera>();
            camera.orthographic = false;

            Assert.IsFalse(EnemyIntroductionBeat.TryGetCameraWorldRect(camera, out _),
                "No projection plane is assumed for a perspective camera.");
            Assert.IsTrue(EnemyIntroductionBeat.IsVerticallyInsideView(
                camera, new Bounds(new Vector3(0f, 999f, 0f), Vector3.one), 0.35f));
        }

        [Test]
        public void TheRuleFollowsTheCameraRatherThanAHardcodedHeight()
        {
            Camera camera = CreateLevel1Camera();
            Bounds enemy = new Bounds(new Vector3(0f, MeasuredSpawnY, 0f), new Vector3(1f, 2f, 1f));

            Assert.IsFalse(EnemyIntroductionBeat.IsVerticallyInsideView(camera, enemy, 0.35f));

            // Same enemy, camera panned up to look at it: now it is on screen. A hardcoded y could
            // not tell these two situations apart.
            _cameraHost.transform.position = new Vector3(0f, MeasuredSpawnY, -10f);
            Assert.IsTrue(EnemyIntroductionBeat.IsVerticallyInsideView(camera, enemy, 0.35f));
        }
    }
}
