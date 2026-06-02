using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class DrawingCanvasTests
    {
        private GameObject _cameraObject;
        private GameObject _canvasObject;

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null)
                Object.DestroyImmediate(_canvasObject);

            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
        }

        [UnityTest]
        public IEnumerator ClearCanvas_DoesNotDestroyStrokeStartedAfterClearRequest()
        {
            _cameraObject = new GameObject("Main Camera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();

            _canvasObject = new GameObject("DrawingCanvas");
            DrawingCanvas canvas = _canvasObject.AddComponent<DrawingCanvas>();
            GlyphBadgePlayModeTestHelpers.SetPrivateField(canvas, "_clearDelaySeconds", 0.05f);

            canvas.BeginStroke();
            canvas.EndStroke();
            canvas.ClearCanvas();

            canvas.BeginStroke();
            Assert.AreEqual(2, _canvasObject.transform.childCount,
                "The new stroke should coexist with the pending clear until the clear delay elapses.");

            yield return new WaitForSeconds(0.075f);
            yield return null;

            Assert.AreEqual(1, _canvasObject.transform.childCount,
                "A delayed clear should only remove strokes that existed when ClearCanvas was requested.");
        }

        [UnityTest]
        public IEnumerator BeginStroke_ConfiguresRoundedLineJoins()
        {
            _cameraObject = new GameObject("Main Camera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();

            _canvasObject = new GameObject("DrawingCanvas");
            DrawingCanvas canvas = _canvasObject.AddComponent<DrawingCanvas>();

            canvas.BeginStroke();

            yield return null;

            LineRenderer line = _canvasObject.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line);
            Assert.GreaterOrEqual(line.numCapVertices, 4);
            Assert.GreaterOrEqual(line.numCornerVertices, 4);
        }

        [UnityTest]
        public IEnumerator SetPoints_ReplacesCurrentStrokePositions()
        {
            _cameraObject = new GameObject("Main Camera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();

            _canvasObject = new GameObject("DrawingCanvas");
            DrawingCanvas canvas = _canvasObject.AddComponent<DrawingCanvas>();

            canvas.BeginStroke();
            canvas.AddPoint(new Vector2(10f, 10f));
            canvas.AddPoint(new Vector2(20f, 20f));
            canvas.SetPoints(new[]
            {
                new Vector2(30f, 30f),
                new Vector2(40f, 40f),
                new Vector2(50f, 50f)
            });

            yield return null;

            LineRenderer line = _canvasObject.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line);
            Assert.AreEqual(3, line.positionCount);
        }

        [UnityTest]
        public IEnumerator DiscardCurrentStroke_RemovesOnlyActiveStroke()
        {
            _cameraObject = new GameObject("Main Camera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();

            _canvasObject = new GameObject("DrawingCanvas");
            DrawingCanvas canvas = _canvasObject.AddComponent<DrawingCanvas>();

            canvas.BeginStroke();
            canvas.AddPoint(new Vector2(10f, 10f));
            canvas.EndStroke();

            canvas.BeginStroke();
            canvas.AddPoint(new Vector2(20f, 20f));
            canvas.DiscardCurrentStroke();

            yield return null;

            Assert.AreEqual(1, _canvasObject.transform.childCount,
                "Rejecting an active tap must not clear completed strokes waiting for recognition.");
        }
    }
}
