using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class TutorialSpotlightOverlayTests
    {
        private GameObject _cameraObject;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("TestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _camera.aspect = 1f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void ComputeScreenRect_ProducesPositiveDimensions()
        {
            Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0f));
            Rect rect = TutorialSpotlightOverlay.ComputeScreenRect(worldBounds, _camera, paddingWorld: 0f);
            Assert.Greater(rect.width, 0f);
            Assert.Greater(rect.height, 0f);
        }

        [Test]
        public void ComputeScreenRect_PaddingExpandsRect()
        {
            Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0f));
            Rect basic = TutorialSpotlightOverlay.ComputeScreenRect(worldBounds, _camera, paddingWorld: 0f);
            Rect padded = TutorialSpotlightOverlay.ComputeScreenRect(worldBounds, _camera, paddingWorld: 1f);
            Assert.Greater(padded.width, basic.width);
            Assert.Greater(padded.height, basic.height);
        }

        [Test]
        public void ComputeScreenRect_OffsetTargetMovesScreenRect()
        {
            Bounds centered = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0f));
            Bounds offset = new Bounds(new Vector3(3f, 0f, 0f), new Vector3(2f, 2f, 0f));
            Rect centerRect = TutorialSpotlightOverlay.ComputeScreenRect(centered, _camera, paddingWorld: 0f);
            Rect offsetRect = TutorialSpotlightOverlay.ComputeScreenRect(offset, _camera, paddingWorld: 0f);
            Assert.Greater(offsetRect.center.x, centerRect.center.x);
        }
    }
}
