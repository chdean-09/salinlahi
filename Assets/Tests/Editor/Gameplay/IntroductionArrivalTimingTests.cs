using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Playtest 2026-09-17. Takip's introduction card landed about ten seconds after it spawned,
    /// by which time the player had already killed it. The measured budget, at 1.07 units/second:
    /// ~5.8s walking from the authored spawn height down to the visible top, then ~4.5s more to
    /// reach the halt line. Both halves are fixed here, and both are pinned below.
    ///
    /// <para>
    /// Neither number was reachable from a test before: the spawn height lived in a scene transform
    /// and the halt line was read straight off a serialized field inside a coroutine. The rules are
    /// now pure functions, which is the only reason this fixture can exist.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class IntroductionArrivalTimingTests
    {
        // The values measured from the Level 2 session that reported this.
        private const float ViewTopWorldY = 7.82f;
        private const float ViewBottomWorldY = -16.52f;
        private const float AuthoredSpawnY = 11.39f;
        private const float MarginWorld = 0.35f;
        private const float HudClearanceWorld = 0.25f;
        private const float SceneHaltLine = 0.2f;

        private static Rect View =>
            new Rect(-10f, ViewBottomWorldY, 20f, ViewTopWorldY - ViewBottomWorldY);

        private GameObject _cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
        }

        /// <summary>
        /// A camera reproducing the reported session's view: -16.52 to 7.82. A real one rather than
        /// null, because IsFramedForLesson returns TRUE for a null camera — a test passing null
        /// would pass without ever evaluating the rule it claims to check.
        /// </summary>
        private Camera MeasuredCamera()
        {
            _cameraObject = new GameObject("IntroductionArrivalTimingTests_Camera");
            Camera camera = _cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = (ViewTopWorldY - ViewBottomWorldY) * 0.5f;
            camera.transform.position =
                new Vector3(0f, (ViewTopWorldY + ViewBottomWorldY) * 0.5f, -10f);
            return camera;
        }

        // -------------------------------------------------------------------
        // Spawn height
        // -------------------------------------------------------------------

        [Test]
        public void AuthoredSpawnHeight_FarAboveTheView_IsCappedToTheLead()
        {
            float clamped = WaveSpawner.ClampSpawnY(AuthoredSpawnY, ViewTopWorldY, 1.6f);

            Assert.AreEqual(ViewTopWorldY + 1.6f, clamped, 1e-4f,
                "The authored spawn sits 3.57 units above the visible top, which is about 5.8 "
                + "seconds of walking before the enemy can be seen at all — and its introduction "
                + "cannot start until it is. Capped, that becomes about 1.5 seconds.");
        }

        [Test]
        public void SpawnHeightAlreadyCloseEnough_IsLeftWhereItWas()
        {
            float authored = ViewTopWorldY + 0.4f;

            Assert.AreEqual(authored, WaveSpawner.ClampSpawnY(authored, ViewTopWorldY, 1.6f), 1e-4f,
                "This is a cap, never a lift. A level that deliberately spawns close must not have "
                + "its enemies pushed further off-screen to satisfy the lead.");
        }

        [Test]
        public void NegativeLead_DoesNotPullTheSpawnIntoView()
        {
            Assert.AreEqual(ViewTopWorldY, WaveSpawner.ClampSpawnY(AuthoredSpawnY, ViewTopWorldY, -5f),
                1e-4f,
                "A negative lead must clamp to the view's top edge, not below it: spawning inside "
                + "the view pops the enemy into existence in front of the player.");
        }

        // -------------------------------------------------------------------
        // The halt line
        // -------------------------------------------------------------------

        /// <summary>
        /// The enemy's bounds the moment it is wholly on screen: top level with the view's top,
        /// less the margin the framing check requires.
        /// </summary>
        private static Bounds JustFullyVisible()
        {
            // A hair inside the margin rather than exactly on it: sitting the top at
            // viewTop - margin puts the check on float equality, which decides the test by
            // rounding rather than by the rule.
            float top = ViewTopWorldY - MarginWorld - 0.05f;
            return new Bounds(new Vector3(0f, top - 1.7f, 0f), new Vector3(1f, 3.4f, 1f));
        }

        [Test]
        public void CardPath_FramesTheEnemyAsSoonAsItIsFullyVisible()
        {
            Assert.IsTrue(
                EnemyIntroductionBeat.IsFramedForLesson(
                    MeasuredCamera(), JustFullyVisible(), MarginWorld, null, HudClearanceWorld,
                    haltLineViewportFromTop: 0f),
                "With no halt line the card must fire the moment the enemy clears the top edge. "
                + "Waiting longer is the ten-second arrival the playtest reported.");
        }

        [Test]
        public void LessonPath_StillWaitsForItsHaltLine()
        {
            Assert.IsFalse(
                EnemyIntroductionBeat.IsFramedForLesson(
                    MeasuredCamera(), JustFullyVisible(), MarginWorld, null, HudClearanceWorld,
                    SceneHaltLine),
                "The lesson keeps the halt line: beat 7 reveals the badge and needs room below the "
                + "top edge to do it without clipping. An enemy level with the top edge is not yet "
                + "framed for a lesson, and scoping the line to lessons must not quietly drop it.");
        }

        [Test]
        public void LessonHaltLine_CostsMostOfTheScreenHeight()
        {
            float ceiling = EnemyIntroductionBeat.ResolveHaltCeilingWorldY(
                null, View, null, HudClearanceWorld, SceneHaltLine);

            Assert.AreEqual(2.95f, ceiling, 0.01f,
                "The measured ceiling from the reported session. In a view 24.3 units tall, a line "
                + "a fifth of the way down sits 4.87 units below the top — about 4.5 seconds of "
                + "walking. That cost is the reason the card no longer pays it.");
        }
    }
}
